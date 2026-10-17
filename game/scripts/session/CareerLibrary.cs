using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

/// <summary>
/// Local career slot index stored in user://. One entry per coaching
/// save. The active slot is the career currently on the dashboard.
/// </summary>
public static class CareerLibrary
{
	private const string Path = "user://careers.json";

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
	};

	private static CareerSaveIndex? _index;

	public static IReadOnlyList<CareerSaveSlot> Slots
	{
		get
		{
			EnsureReady();
			return _index!.Slots;
		}
	}

	public static string? ActiveId
	{
		get
		{
			EnsureReady();
			return _index!.ActiveId;
		}
	}

	public static void EnsureReady()
	{
		if (_index != null)
		{
			return;
		}

		_index = LoadFile() ?? new CareerSaveIndex();
		if (_index.Slots.Count == 0)
		{
			_index.Slots.AddRange(SampleSlots());
			SaveFile();
		}
	}

	public static IReadOnlyList<CareerSaveSlot> ListOrdered()
	{
		EnsureReady();
		return _index!.Slots
			.OrderByDescending(slot => slot.Id == _index.ActiveId)
			.ThenByDescending(slot => slot.LastPlayedUtc)
			.ToList();
	}

	public static CareerSaveSlot? Find(string? id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}

		EnsureReady();
		return _index!.Slots.Find(slot => slot.Id == id);
	}

	public static CareerSaveSlot? ActiveSlot()
	{
		EnsureReady();
		return Find(_index!.ActiveId);
	}

	public static string DatabasePath(string slotId) =>
		System.IO.Path.Combine(OS.GetUserDataDir(), "careers", slotId + ".db");

	public static void CopyDatabase(string fromId, string toId)
	{
		CopySidecar(DatabasePath(fromId), DatabasePath(toId));
	}

	public static void DeleteDatabase(string id)
	{
		DeleteSidecar(DatabasePath(id));
	}

	public static CareerSaveSlot Upsert(CareerSaveSlot slot)
	{
		EnsureReady();
		if (string.IsNullOrEmpty(slot.Id))
		{
			slot.Id = Guid.NewGuid().ToString("N");
		}

		slot.LastPlayedUtc = DateTime.UtcNow;
		int index = _index!.Slots.FindIndex(existing => existing.Id == slot.Id);
		if (index >= 0)
		{
			_index.Slots[index] = slot;
		}
		else
		{
			_index.Slots.Insert(0, slot);
		}

		_index.ActiveId = slot.Id;
		SaveFile();
		return slot;
	}

	public static void SetActive(string id)
	{
		EnsureReady();
		CareerSaveSlot? slot = Find(id);
		if (slot == null)
		{
			return;
		}

		slot.LastPlayedUtc = DateTime.UtcNow;
		_index!.ActiveId = id;
		SaveFile();
	}

	public static void ClearActive()
	{
		EnsureReady();
		_index!.ActiveId = null;
		SaveFile();
	}

	public static CareerSaveSlot? Rename(string id, string coachName)
	{
		EnsureReady();
		CareerSaveSlot? slot = Find(id);
		if (slot == null)
		{
			return null;
		}

		string trimmed = coachName.Trim();
		if (string.IsNullOrEmpty(trimmed))
		{
			return null;
		}

		slot.CoachName = trimmed;
		SaveFile();
		return slot;
	}

	public static bool Delete(string id)
	{
		EnsureReady();
		int removed = _index!.Slots.RemoveAll(slot => slot.Id == id);
		if (removed == 0)
		{
			return false;
		}

		if (_index.ActiveId == id)
		{
			_index.ActiveId = null;
		}

		DeleteDatabase(id);
		SaveFile();
		return true;
	}

	private static void CopySidecar(string from, string to)
	{
		string? directory = System.IO.Path.GetDirectoryName(to);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		foreach (string suffix in new[] { "", "-wal", "-shm" })
		{
			string source = from + suffix;
			if (File.Exists(source))
			{
				File.Copy(source, to + suffix, overwrite: true);
			}
		}
	}

	private static void DeleteSidecar(string path)
	{
		foreach (string suffix in new[] { "", "-wal", "-shm" })
		{
			string file = path + suffix;
			if (File.Exists(file))
			{
				File.Delete(file);
			}
		}
	}

	private static CareerSaveIndex? LoadFile()
	{
		if (!Godot.FileAccess.FileExists(Path))
		{
			return null;
		}

		using Godot.FileAccess file = Godot.FileAccess.Open(Path, Godot.FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return null;
		}

		string json = file.GetAsText();
		if (string.IsNullOrWhiteSpace(json))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<CareerSaveIndex>(json, JsonOptions);
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private static void SaveFile()
	{
		if (_index == null)
		{
			return;
		}

		string json = JsonSerializer.Serialize(_index, JsonOptions);
		using Godot.FileAccess? file = Godot.FileAccess.Open(Path, Godot.FileAccess.ModeFlags.Write);
		file?.StoreString(json);
	}

	private static List<CareerSaveSlot> SampleSlots()
	{
		return
		[
			new CareerSaveSlot
			{
				Id = "sample-reydel",
				CoachName = "Reydel Gutierrez",
				SchoolName = "Cascade Regional",
				SchoolShortName = "Cascade",
				OrganizationId = "org-cascade-regional",
				SeasonNumber = 4,
				SeasonYear = 2030,
				Wins = 22,
				Losses = 7,
				Status = "District Champions",
				LastPlayedUtc = new DateTime(2030, 6, 12, 18, 0, 0, DateTimeKind.Utc),
			},
			new CareerSaveSlot
			{
				Id = "sample-marcus",
				CoachName = "Marcus Johnson",
				SchoolName = "Delaware Valley",
				SchoolShortName = "Delaware Valley",
				OrganizationId = "org-delaware-valley",
				SeasonNumber = 11,
				SeasonYear = 2037,
				Wins = 18,
				Losses = 10,
				Status = "Regional Semifinal",
				LastPlayedUtc = new DateTime(2037, 5, 28, 18, 0, 0, DateTimeKind.Utc),
			},
		];
	}

	private sealed class CareerSaveIndex
	{
		public string? ActiveId { get; set; }

		public List<CareerSaveSlot> Slots { get; set; } = [];
	}
}
