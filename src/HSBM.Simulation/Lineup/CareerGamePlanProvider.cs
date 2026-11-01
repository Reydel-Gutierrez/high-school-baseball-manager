using HSBM.Domain;

namespace HSBM.Simulation;

public sealed class GamePlanContext
{
	public required string TeamId { get; init; }
	public DateOnly Date { get; init; }
	public TeamLineup? Lineup { get; init; }
	public TeamRotation? Rotation { get; init; }
	public int CompletedStarts { get; init; }

	/// <summary>Pitchers who still owe rest under <see cref="PitchCountRules"/> and may not pitch today.</summary>
	public IReadOnlySet<Guid>? Resting { get; init; }
}

/// <summary>
/// Builds a game-day batting order and pitching staff from saved
/// Clubhouse cards when they are filled, otherwise from ratings.
/// Pitchers who still owe rest are skipped: the rotation moves to the
/// next rested starter and they stay out of the bullpen.
/// </summary>
public static class CareerGamePlanProvider
{
	private static readonly AutomaticLineupProvider Automatic = new();

	public static TeamGamePlan Create(IReadOnlyList<Player> roster, GamePlanContext? context)
	{
		if (context == null)
		{
			return Automatic.Create(roster);
		}

		IReadOnlySet<Guid> resting = context.Resting ?? new HashSet<Guid>();
		TeamGamePlan automatic = Automatic.Create(WithoutResting(roster, resting));

		IReadOnlyList<LineupBatter> order = TrySavedOrder(roster, context.Lineup) ?? automatic.BattingOrder;
		Player starter = PickStarter(roster, context, resting, automatic.StartingPitcher);
		Player? closer = Rested(PickNamed(roster, context.Rotation?.Closer), resting) ?? automatic.Closer;
		if (closer != null && closer.Id == starter.Id)
		{
			closer = automatic.Closer != null && automatic.Closer.Id != starter.Id
				? automatic.Closer
				: null;
		}

		List<Player> bullpen = BuildBullpen(roster, context.Rotation, starter, closer, automatic.Bullpen, resting);
		return new TeamGamePlan
		{
			BattingOrder = order,
			StartingPitcher = starter,
			Bullpen = bullpen,
			Closer = closer,
		};
	}

	private static IReadOnlyList<LineupBatter>? TrySavedOrder(IReadOnlyList<Player> roster, TeamLineup? saved)
	{
		if (saved == null || saved.Slots.Count == 0)
		{
			return null;
		}

		var byId = new Dictionary<Guid, Player>();
		foreach (Player player in roster)
		{
			byId[player.Id] = player;
		}

		var used = new HashSet<Guid>();
		var order = new List<LineupBatter>(9);
		foreach (TeamLineupSlot slot in saved.Slots)
		{
			if (!slot.PlayerId.HasValue || !byId.TryGetValue(slot.PlayerId.Value, out Player? player))
			{
				continue;
			}

			if (!used.Add(player.Id))
			{
				continue;
			}

			PlayerPosition field = PlayerPositionExtensions.ParseLabel(slot.Position);
			if (string.Equals(slot.Position, "P", StringComparison.OrdinalIgnoreCase))
			{
				field = player.PrimaryPosition.IsPitcher() ? player.PrimaryPosition : PlayerPosition.StartingPitcher;
			}

			order.Add(new LineupBatter(player, field));
			if (order.Count == 9)
			{
				break;
			}
		}

		return order.Count >= 8 ? order : null;
	}

	/// <summary>The starter <see cref="Create"/> would send out, without building the whole plan.</summary>
	public static Player ProbableStarter(IReadOnlyList<Player> roster, GamePlanContext context) =>
		Create(roster, context).StartingPitcher;

	/// <summary>
	/// Roster without resting pitchers, unless that would leave no pitcher at all
	/// (then everyone stays and the rested-first order below still applies).
	/// </summary>
	private static IReadOnlyList<Player> WithoutResting(IReadOnlyList<Player> roster, IReadOnlySet<Guid> resting)
	{
		if (resting.Count == 0)
		{
			return roster;
		}

		var available = new List<Player>(roster.Count);
		bool anyArm = false;
		foreach (Player player in roster)
		{
			if (player.IsPitcher && resting.Contains(player.Id))
			{
				continue;
			}

			anyArm |= player.IsPitcher;
			available.Add(player);
		}

		return anyArm ? available : roster;
	}

	private static Player? Rested(Player? arm, IReadOnlySet<Guid> resting) =>
		arm != null && !resting.Contains(arm.Id) ? arm : null;

	private static Player PickStarter(IReadOnlyList<Player> roster, GamePlanContext context, IReadOnlySet<Guid> resting, Player fallback)
	{
		if (context.Rotation == null)
		{
			return fallback;
		}

		var arms = new List<Player>();
		foreach (Guid? id in context.Rotation.Starters)
		{
			// A saved card listing one arm in two seats still gives him one turn.
			Player? match = Find(roster, id);
			if (match != null && !arms.Contains(match))
			{
				arms.Add(match);
			}
		}

		if (arms.Count == 0)
		{
			return fallback;
		}

		// Turn order first; if this turn's arm still owes rest, the next rested one goes.
		for (int i = 0; i < arms.Count; i++)
		{
			Player arm = arms[(context.CompletedStarts + i) % arms.Count];
			if (!resting.Contains(arm.Id))
			{
				return arm;
			}
		}

		return fallback;
	}

	private static List<Player> BuildBullpen(
		IReadOnlyList<Player> roster,
		TeamRotation? rotation,
		Player starter,
		Player? closer,
		IReadOnlyList<Player> automatic,
		IReadOnlySet<Guid> resting)
	{
		var used = new HashSet<Guid>(resting) { starter.Id };
		var bullpen = new List<Player>();
		if (rotation != null)
		{
			TryAdd(bullpen, used, Find(roster, rotation.LongRelief));
			TryAdd(bullpen, used, Find(roster, rotation.Middle));
			TryAdd(bullpen, used, Find(roster, rotation.Setup));
		}

		foreach (Player arm in automatic)
		{
			if (closer != null && arm.Id == closer.Id)
			{
				continue;
			}

			TryAdd(bullpen, used, arm);
		}

		return bullpen;
	}

	private static void TryAdd(List<Player> bullpen, HashSet<Guid> used, Player? arm)
	{
		if (arm != null && used.Add(arm.Id))
		{
			bullpen.Add(arm);
		}
	}

	private static Player? PickNamed(IReadOnlyList<Player> roster, Guid? id) => Find(roster, id);

	private static Player? Find(IReadOnlyList<Player> roster, Guid? id)
	{
		if (!id.HasValue)
		{
			return null;
		}

		foreach (Player player in roster)
		{
			if (player.Id == id.Value)
			{
				return player;
			}
		}

		return null;
	}
}
