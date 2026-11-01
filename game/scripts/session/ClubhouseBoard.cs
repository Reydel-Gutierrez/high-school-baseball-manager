using System;
using System.Collections.Generic;
using HSBM.Domain;
using HSBM.Simulation;

/// <summary>
/// Clubhouse assignments for the active program. Saved cards use player
/// IDs in the career world; the UI still works in jersey numbers.
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

	public static void Reset()
	{
		Lineups.Clear();
		Rotations.Clear();
	}

	public static LineupCard GetLineup(TeamIdentity team)
	{
		if (!Lineups.TryGetValue(team.Id, out LineupCard? card))
		{
			card = LoadOrCreateLineup(team);
			Lineups[team.Id] = card;
		}

		return card;
	}

	public static RotationCard GetRotation(TeamIdentity team)
	{
		if (!Rotations.TryGetValue(team.Id, out RotationCard? card))
		{
			card = LoadOrCreateRotation(team);
			Rotations[team.Id] = card;
			if (card.RemoveDuplicates())
			{
				// Older saves could hold the same arm in two seats; save the repaired card.
				Commit(team);
			}
		}

		return card;
	}

	public static void Commit(TeamIdentity team)
	{
		GameSession? session = GameSession.Current;
		if (session?.World == null)
		{
			return;
		}

		session.World.Lineups.RemoveAll(card => card.TeamId == team.Id);
		session.World.Lineups.Add(ToSavedLineup(team, GetLineup(team)));
		session.World.Rotations.RemoveAll(card => card.TeamId == team.Id);
		session.World.Rotations.Add(ToSavedRotation(team, GetRotation(team)));
		session.PersistClubhouse();
	}

	public static ClubhousePlayer? GetLikelyStarter(TeamIdentity team)
	{
		RotationCard rotation = GetRotation(team);
		GameSession? session = GameSession.Current;
		DateOnly gameDay = session == null
			? default
			: session.NextContest(team.Level)?.Date ?? session.CurrentDate;

		// Mirrors CareerGamePlanProvider: injured arms drop out of the turn order,
		// then the turn goes to the next arm who doesn't owe rest.
		var arms = new List<ClubhousePlayer>();
		foreach (int jersey in rotation.Starters)
		{
			ClubhousePlayer? arm = jersey == 0 ? null : ClubhouseSquad.Find(jersey, team.Level);
			Player? domain = arm == null || session == null ? null : session.FindWorldPlayer(arm.PlayerId);
			if (arm != null && (domain == null || domain.CanPlay(gameDay)))
			{
				arms.Add(arm);
			}
		}

		if (arms.Count == 0)
		{
			return null;
		}

		int starts = 0;
		HashSet<Guid> resting = [];
		if (session?.World != null)
		{
			foreach (ScheduledGame game in session.World.Games)
			{
				if (game.SeasonYear == GameSession.SeasonYear
					&& game.Status == GameStatus.Completed
					&& !game.GameType.IsExhibition()
					&& game.Involves(team.Id))
				{
					starts++;
				}
			}

			resting = PitcherWorkloads.Resting(session.World, team.Id, gameDay);
		}

		for (int i = 0; i < arms.Count; i++)
		{
			ClubhousePlayer arm = arms[(starts + i) % arms.Count];
			if (!resting.Contains(arm.PlayerId))
			{
				return arm;
			}
		}

		return arms[starts % arms.Count];
	}

	private static LineupCard LoadOrCreateLineup(TeamIdentity team)
	{
		GameSession? session = GameSession.Current;
		if (session?.World != null)
		{
			foreach (TeamLineup saved in session.World.Lineups)
			{
				if (saved.TeamId == team.Id && saved.FilledCount > 0)
				{
					return FromSavedLineup(saved, team);
				}
			}

			return FromAutomaticLineup(Roster(session, team));
		}

		return CreateDefaultLineup(team.Level);
	}

	private static RotationCard LoadOrCreateRotation(TeamIdentity team)
	{
		GameSession? session = GameSession.Current;
		if (session?.World != null)
		{
			foreach (TeamRotation saved in session.World.Rotations)
			{
				if (saved.TeamId == team.Id && HasPitcher(saved))
				{
					return FromSavedRotation(saved, team);
				}
			}

			return FromAutomaticRotation(Roster(session, team));
		}

		return CreateDefaultRotation(team.Level);
	}

	private static List<Player> Roster(GameSession session, TeamIdentity team)
	{
		var roster = new List<Player>();
		foreach (Player player in session.World!.Players)
		{
			if (player.TeamId == team.Id && !player.Alumni)
			{
				roster.Add(player);
			}
		}

		return roster;
	}

	private static LineupCard FromSavedLineup(TeamLineup saved, TeamIdentity team)
	{
		var card = new LineupCard();
		int count = Math.Min(saved.Slots.Count, LineupSize);
		for (int i = 0; i < count; i++)
		{
			TeamLineupSlot slot = saved.Slots[i];
			int jersey = JerseyFor(slot.PlayerId, team);
			card.Set(i, jersey, string.IsNullOrWhiteSpace(slot.Position) ? FieldPositions[i] : slot.Position);
		}

		return card;
	}

	private static RotationCard FromSavedRotation(TeamRotation saved, TeamIdentity team)
	{
		var card = new RotationCard();
		int count = Math.Min(saved.Starters.Count, RotationSize);
		for (int i = 0; i < count; i++)
		{
			card.Starters[i] = JerseyFor(saved.Starters[i], team);
		}

		card.Closer = JerseyFor(saved.Closer, team);
		card.Setup = JerseyFor(saved.Setup, team);
		card.Middle = JerseyFor(saved.Middle, team);
		card.LongRelief = JerseyFor(saved.LongRelief, team);
		return card;
	}

	private static LineupCard FromAutomaticLineup(IReadOnlyList<Player> roster)
	{
		if (roster.Count == 0)
		{
			return new LineupCard();
		}

		TeamGamePlan plan = new AutomaticLineupProvider().Create(roster);
		var card = new LineupCard();
		int count = Math.Min(plan.BattingOrder.Count, LineupSize);
		for (int i = 0; i < count; i++)
		{
			LineupBatter slot = plan.BattingOrder[i];
			string position = slot.FieldPosition.IsPitcher() ? "P" : slot.FieldPosition.ToCode();
			card.Set(i, slot.Player.JerseyNumber, position);
		}

		return card;
	}

	private static RotationCard FromAutomaticRotation(IReadOnlyList<Player> roster)
	{
		var card = new RotationCard();
		if (roster.Count == 0)
		{
			return card;
		}

		TeamGamePlan plan = new AutomaticLineupProvider().Create(roster);
		var arms = new List<Player>();
		foreach (Player player in roster)
		{
			if (!player.IsPitcher || player.Id == plan.StartingPitcher.Id)
			{
				continue;
			}

			if (plan.Closer != null && player.Id == plan.Closer.Id)
			{
				continue;
			}

			arms.Add(player);
		}

		arms.Sort((a, b) => PlayerOverall.PitchingRating(b.Ratings).CompareTo(PlayerOverall.PitchingRating(a.Ratings)));
		card.Starters[0] = plan.StartingPitcher.JerseyNumber;
		int seat = 1;
		foreach (Player arm in arms)
		{
			if (seat >= RotationSize)
			{
				break;
			}

			if (arm.PrimaryPosition == PlayerPosition.StartingPitcher)
			{
				card.Starters[seat] = arm.JerseyNumber;
				seat++;
			}
		}

		foreach (Player arm in arms)
		{
			if (seat >= RotationSize)
			{
				break;
			}

			if (card.Contains(arm.JerseyNumber))
			{
				continue;
			}

			card.Starters[seat] = arm.JerseyNumber;
			seat++;
		}

		card.Closer = plan.Closer?.JerseyNumber ?? 0;
		var relief = new List<int>();
		foreach (Player arm in plan.Bullpen)
		{
			if (!card.Contains(arm.JerseyNumber))
			{
				relief.Add(arm.JerseyNumber);
			}
		}

		card.Setup = relief.Count > 0 ? relief[0] : 0;
		card.Middle = relief.Count > 1 ? relief[1] : 0;
		card.LongRelief = relief.Count > 2 ? relief[2] : 0;
		return card;
	}

	private static TeamLineup ToSavedLineup(TeamIdentity team, LineupCard card)
	{
		var saved = new TeamLineup { TeamId = team.Id };
		foreach (LineupSlot slot in card.Slots)
		{
			saved.Slots.Add(new TeamLineupSlot
			{
				PlayerId = PlayerIdFor(slot.Jersey, team),
				Position = slot.Position,
			});
		}

		return saved;
	}

	private static TeamRotation ToSavedRotation(TeamIdentity team, RotationCard card)
	{
		var saved = new TeamRotation
		{
			TeamId = team.Id,
			Starters = [null, null, null, null, null],
		};
		for (int i = 0; i < card.Starters.Length && i < saved.Starters.Count; i++)
		{
			saved.Starters[i] = PlayerIdFor(card.Starters[i], team);
		}

		saved.Closer = PlayerIdFor(card.Closer, team);
		saved.Setup = PlayerIdFor(card.Setup, team);
		saved.Middle = PlayerIdFor(card.Middle, team);
		saved.LongRelief = PlayerIdFor(card.LongRelief, team);
		return saved;
	}

	private static bool HasPitcher(TeamRotation saved)
	{
		foreach (Guid id in saved.AssignedPitcherIds)
		{
			return id != Guid.Empty;
		}

		return false;
	}

	/// <summary>
	/// Jersey of a saved player, only while he is still on <paramref name="team"/>. A player who
	/// moved levels, was released, or graduated leaves the seat open instead of handing it to
	/// whoever wears his number now.
	/// </summary>
	private static int JerseyFor(Guid? playerId, TeamIdentity team)
	{
		if (!playerId.HasValue)
		{
			return 0;
		}

		GameSession? session = GameSession.Current;
		if (session?.World == null)
		{
			return 0;
		}

		foreach (Player player in session.World.Players)
		{
			if (player.Id == playerId.Value)
			{
				return player.TeamId == team.Id && !player.Alumni ? player.JerseyNumber : 0;
			}
		}

		return 0;
	}

	private static Guid? PlayerIdFor(int jersey, TeamIdentity team)
	{
		if (jersey == 0)
		{
			return null;
		}

		ClubhousePlayer? player = ClubhouseSquad.Find(jersey, team.Level);
		return player == null || player.PlayerId == Guid.Empty ? null : player.PlayerId;
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

	/// <summary>Clears any seat holding an arm already seated above it. Returns true when a seat changed.</summary>
	public bool RemoveDuplicates()
	{
		var seen = new HashSet<int>();
		bool changed = false;
		for (int i = 0; i < Starters.Length; i++)
		{
			Starters[i] = Keep(Starters[i]);
		}

		Closer = Keep(Closer);
		Setup = Keep(Setup);
		Middle = Keep(Middle);
		LongRelief = Keep(LongRelief);
		return changed;

		int Keep(int jersey)
		{
			if (jersey == 0 || seen.Add(jersey))
			{
				return jersey;
			}

			changed = true;
			return 0;
		}
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
