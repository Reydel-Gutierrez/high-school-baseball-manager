using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Convention-based audio paths. Drop files with these names, or override
/// them at runtime through AudioManager.
/// </summary>
public static class AudioLibrary
{
	public const string MusicFolder = "res://assets/audio/music/";
	public const string AmbienceFolder = "res://assets/audio/ambience/";
	public const string UiFolder = "res://assets/audio/ui/";
	public const string SfxFolder = "res://assets/audio/sfx/";

	public const int SoundtrackSlotCount = 20;

	public const string AmbStadiumDay = AmbienceFolder + "amb_stadium_day.ogg";
	public const string AmbStadiumNight = AmbienceFolder + "amb_stadium_night.ogg";
	public const string AmbLockerRoom = AmbienceFolder + "amb_locker_room.ogg";

	public const string UiHover = UiFolder + "ui_hover.ogg";
	public const string UiSelect = UiFolder + "ui_select.ogg";
	public const string UiNavigate = UiFolder + "ui_navigate.ogg";
	public const string UiBack = UiFolder + "ui_back.ogg";
	public const string UiConfirm = UiFolder + "ui_confirm.ogg";
	public const string UiError = UiFolder + "ui_error.ogg";
	public const string UiNotification = UiFolder + "ui_notification.ogg";
	public const string UiAdvanceDay = UiFolder + "ui_advance_day.ogg";

	public const string SfxBatHit = SfxFolder + "sfx_bat_hit.wav";
	public const string SfxGloveCatch = SfxFolder + "sfx_glove_catch.wav";
	public const string SfxCrowdCheer = SfxFolder + "sfx_crowd_cheer.wav";
	public const string SfxUmpireCall = SfxFolder + "sfx_umpire_call.wav";

	public static IReadOnlyList<string> LoadSoundtrackPlaylist()
	{
		var tracks = new List<string>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (string fileName in ListMusicFolderFiles())
		{
			if (!IsSoundtrackFileName(fileName) || !seen.Add(fileName))
			{
				continue;
			}

			string path = MusicFolder + fileName;
			if (ResourceLoader.Exists(path))
			{
				tracks.Add(path);
			}
		}

		if (tracks.Count == 0)
		{
			for (int i = 1; i <= SoundtrackSlotCount; i++)
			{
				foreach (string ext in new[] { ".mp3", ".ogg" })
				{
					string path = $"{MusicFolder}track_{i:D2}{ext}";
					if (ResourceLoader.Exists(path))
					{
						tracks.Add(path);
						break;
					}
				}
			}
		}

		tracks.Sort(StringComparer.OrdinalIgnoreCase);
		return tracks;
	}

	private static string[] ListMusicFolderFiles()
	{
		try
		{
			string[] files = DirAccess.GetFilesAt(MusicFolder);
			if (files == null || files.Length == 0)
			{
				return Array.Empty<string>();
			}

			var names = new List<string>(files.Length);
			foreach (string file in files)
			{
				string name = file;
				if (name.EndsWith(".import", StringComparison.OrdinalIgnoreCase))
				{
					name = name[..^7];
				}

				names.Add(name);
			}

			return names.ToArray();
		}
		catch (Exception)
		{
			return Array.Empty<string>();
		}
	}

	private static bool IsSoundtrackFileName(string fileName)
	{
		string lower = fileName.ToLowerInvariant();
		if (!lower.StartsWith("track_"))
		{
			return false;
		}

		return lower.EndsWith(".mp3") || lower.EndsWith(".ogg") || lower.EndsWith(".wav");
	}

	public static string AmbiencePath(AmbienceTrack track) => track switch
	{
		AmbienceTrack.StadiumDay => AmbStadiumDay,
		AmbienceTrack.StadiumNight => AmbStadiumNight,
		AmbienceTrack.LockerRoom => AmbLockerRoom,
		_ => string.Empty,
	};

	public static string UiPath(UiSound sound) => sound switch
	{
		UiSound.Hover => UiHover,
		UiSound.Select => UiSelect,
		UiSound.Navigate => UiNavigate,
		UiSound.Back => UiBack,
		UiSound.Confirm => UiConfirm,
		UiSound.Error => UiError,
		UiSound.Notification => UiNotification,
		UiSound.AdvanceDay => UiAdvanceDay,
		_ => string.Empty,
	};

	public static string SfxPath(SfxId sfx) => sfx switch
	{
		SfxId.BatHit => SfxBatHit,
		SfxId.GloveCatch => SfxGloveCatch,
		SfxId.CrowdCheer => SfxCrowdCheer,
		SfxId.UmpireCall => SfxUmpireCall,
		_ => string.Empty,
	};
}

public enum AmbienceTrack
{
	None,
	StadiumDay,
	StadiumNight,
	LockerRoom,
}

public enum UiSound
{
	Hover,
	Select,
	Navigate,
	Back,
	Confirm,
	Error,
	Notification,
	AdvanceDay,
}

public enum SfxId
{
	BatHit,
	GloveCatch,
	CrowdCheer,
	UmpireCall,
}

public enum AudioChannel
{
	Master,
	Music,
	Ambience,
	Ui,
	Sfx,
}

/// <summary>
/// Click role for a control. Auto infers from the control's text.
/// </summary>
public enum UiClickSound
{
	Auto,
	Silent,
	Select,
	Navigate,
	Back,
	Confirm,
	AdvanceDay,
}
