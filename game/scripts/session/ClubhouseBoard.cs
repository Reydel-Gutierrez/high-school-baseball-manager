using System;
using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// In-memory Clubhouse assignments for the active program. Lineup and
/// rotation are per team (Varsity / JV) so the top-bar toggle keeps both.
/// </summary>
public static class ClubhouseBoard
{
	public const int LineupSize = 9;
	public const int RotationSize = 5;

	public static readonly string[] FieldPositions =
	[
		"C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "DH",
	];

	private static readonly Dictionary<string, LineupCard> Lineups = new();
	private static readonly Dictionary<string, RotationCard> Rotations = new();

	public static LineupCard GetLineup(TeamIdentity team)
	{
		if (!Lineups.TryGetValue(team.Id, out LineupCard? card))
		{
			card = CreateDefaultLineup(team.Level);
			Lineups[team.Id] = card;
		}

		return card;
	}

	public static RotationCard GetRotation(TeamIdentity team)
	{
		if (!Rotations.TryGetValue(team.Id, out RotationCard? card))
		{
			card = CreateDefaultRotation(team.Level);
			Rotations[team.Id] = card;
		}

		return card;
	}

	public static ClubhousePlayer? GetLikelyStarter(TeamIdentity team)
	{
		RotationCard rotation = GetRotation(team);
		return rotation.Starters[0] == 0 ? null : ClubhouseSquad.Find(rotation.Starters[0]);
	}

	private static LineupCard CreateDefaultLineup(TeamLevel level)
	{
		var card = new LineupCard();
		if (level == TeamLevel.JuniorVarsity)
		{
			card.Set(0, 6, "CF");
			card.Set(1, 1, "SS");
			card.Set(2, 31, "1B");
			card.Set(3, 14, "C");
			card.Set(4, 25, "3B");
			card.Set(5, 36, "LF");
			card.Set(6, 27, "2B");
			card.Set(7, 38, "RF");
			card.Set(8, 34, "DH");
			return card;
		}

		card.Set(0, 7, "CF");
		card.Set(1, 4, "SS");
		card.Set(2, 9, "1B");
		card.Set(3, 21, "C");
		card.Set(4, 15, "3B");
		card.Set(5, 3, "LF");
		card.Set(6, 2, "2B");
		card.Set(7, 18, "RF");
		card.Set(8, 33, "DH");
		return card;
	}

	private static RotationCard CreateDefaultRotation(TeamLevel level)
	{
		var card = new RotationCard();
		if (level == TeamLevel.JuniorVarsity)
		{
			card.Starters[0] = 5;
			card.Starters[1] = 23;
			card.Starters[2] = 17;
			card.Starters[3] = 40;
			card.Starters[4] = 29;
			return card;
		}

		card.Starters[0] = 11;
		card.Starters[1] = 32;
		card.Starters[2] = 12;
		card.Starters[3] = 22;
		card.Closer = 8;
		card.Setup = 28;
		return card;
	}
}

public sealed class LineupCard
{
	public LineupSlot[] Slots { get; } = new LineupSlot[ClubhouseBoard.LineupSize];

	public LineupCard()
	{
		for (int i = 0; i < Slots.Length; i++)
		{
			Slots[i] = new LineupSlot(0, ClubhouseBoard.FieldPositions[i]);
		}
	}

	public void Set(int index, int jersey, string position)
	{
		Slots[index] = new LineupSlot(jersey, position);
	}

	public void Swap(int a, int b)
	{
		(Slots[a], Slots[b]) = (Slots[b], Slots[a]);
	}

	public void SwapDefense(int a, int b)
	{
		if (a < 0 || b < 0 || a == b)
		{
			return;
		}

		string posA = Slots[a].Position;
		string posB = Slots[b].Position;
		Slots[a] = new LineupSlot(Slots[a].Jersey, posB);
		Slots[b] = new LineupSlot(Slots[b].Jersey, posA);
	}

	public int IndexOfPosition(string position)
	{
		for (int i = 0; i < Slots.Length; i++)
		{
			if (string.Equals(Slots[i].Position, position, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}

		return -1;
	}

	public void Insert(int index, int jersey)
	{
		string position = Slots[index].Position;
		if (string.IsNullOrEmpty(position))
		{
			ClubhousePlayer? player = ClubhouseSquad.Find(jersey);
			position = player == null ? string.Empty : ClubhouseSquad.SuggestDefense(player);
		}

		Slots[index] = new LineupSlot(jersey, position);
	}

	public void Bench(int index)
	{
		Slots[index] = new LineupSlot(0, Slots[index].Position);
	}

	public bool Contains(int jersey)
	{
		if (jersey == 0)
		{
			return false;
		}

		foreach (LineupSlot slot in Slots)
		{
			if (slot.Jersey == jersey)
			{
				return true;
			}
		}

		return false;
	}

	public int FilledCount
	{
		get
		{
			int count = 0;
			foreach (LineupSlot slot in Slots)
			{
				if (slot.Jersey != 0)
				{
					count++;
				}
			}

			return count;
		}
	}
}

public readonly record struct LineupSlot(int Jersey, string Position)
{
	public bool IsEmpty => Jersey == 0;
}

public sealed class RotationCard
{
	public int[] Starters { get; } = new int[ClubhouseBoard.RotationSize];
	public int Closer { get; set; }
	public int Setup { get; set; }
	public int Middle { get; set; }
	public int LongRelief { get; set; }

	public bool Contains(int jersey)
	{
		if (jersey == 0)
		{
			return false;
		}

		foreach (int starter in Starters)
		{
			if (starter == jersey)
			{
				return true;
			}
		}

		return Closer == jersey || Setup == jersey || Middle == jersey || LongRelief == jersey;
	}

	public int AssignedCount
	{
		get
		{
			int count = 0;
			foreach (int starter in Starters)
			{
				if (starter != 0)
				{
					count++;
				}
			}

			if (Closer != 0) count++;
			if (Setup != 0) count++;
			if (Middle != 0) count++;
			if (LongRelief != 0) count++;
			return count;
		}
	}
}

public readonly record struct RotationSeat(string Key, string Label)
{
	public static readonly RotationSeat[] StarterSeats =
	[
		new("sp1", "SP1"),
		new("sp2", "SP2"),
		new("sp3", "SP3"),
		new("sp4", "SP4"),
		new("sp5", "SP5"),
	];

	public static readonly RotationSeat[] BullpenSeats =
	[
		new("cl", "CLOSER"),
		new("su", "SETUP"),
		new("mr", "MIDDLE"),
		new("lr", "LONG"),
	];
}

public static class RotationCardExtensions
{
	public static int GetSeat(this RotationCard card, string key) => key switch
	{
		"sp1" => card.Starters[0],
		"sp2" => card.Starters[1],
		"sp3" => card.Starters[2],
		"sp4" => card.Starters[3],
		"sp5" => card.Starters[4],
		"cl" => card.Closer,
		"su" => card.Setup,
		"mr" => card.Middle,
		"lr" => card.LongRelief,
		_ => 0,
	};

	public static void SetSeat(this RotationCard card, string key, int jersey)
	{
		switch (key)
		{
			case "sp1": card.Starters[0] = jersey; break;
			case "sp2": card.Starters[1] = jersey; break;
			case "sp3": card.Starters[2] = jersey; break;
			case "sp4": card.Starters[3] = jersey; break;
			case "sp5": card.Starters[4] = jersey; break;
			case "cl": card.Closer = jersey; break;
			case "su": card.Setup = jersey; break;
			case "mr": card.Middle = jersey; break;
			case "lr": card.LongRelief = jersey; break;
		}
	}

	public static void SwapSeats(this RotationCard card, string a, string b)
	{
		int left = card.GetSeat(a);
		int right = card.GetSeat(b);
		card.SetSeat(a, right);
		card.SetSeat(b, left);
	}
}
