using HSBM.Domain;

namespace HSBM.Simulation;

public sealed class GamePlanContext
{
	public required string TeamId { get; init; }
	public DateOnly Date { get; init; }
	public TeamLineup? Lineup { get; init; }
	public TeamRotation? Rotation { get; init; }
	public int CompletedStarts { get; init; }
}

/// <summary>
/// Builds a game-day batting order and pitching staff from saved
/// Clubhouse cards when they are filled, otherwise from ratings.
/// </summary>
public static class CareerGamePlanProvider
{
	private static readonly AutomaticLineupProvider Automatic = new();

	public static TeamGamePlan Create(IReadOnlyList<Player> roster, GamePlanContext? context)
	{
		TeamGamePlan automatic = Automatic.Create(roster);
		if (context == null)
		{
			return automatic;
		}

		IReadOnlyList<LineupBatter> order = TrySavedOrder(roster, context.Lineup) ?? automatic.BattingOrder;
		Player starter = PickStarter(roster, context, automatic.StartingPitcher);
		Player? closer = PickNamed(roster, context.Rotation?.Closer) ?? automatic.Closer;
		if (closer != null && closer.Id == starter.Id)
		{
			closer = automatic.Closer != null && automatic.Closer.Id != starter.Id
				? automatic.Closer
				: null;
		}

		List<Player> bullpen = BuildBullpen(roster, context.Rotation, starter, closer, automatic.Bullpen);
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

	private static Player PickStarter(IReadOnlyList<Player> roster, GamePlanContext context, Player fallback)
	{
		if (context.Rotation == null)
		{
			return fallback;
		}

		var arms = new List<Player>();
		foreach (Guid? id in context.Rotation.Starters)
		{
			Player? match = Find(roster, id);
			if (match != null)
			{
				arms.Add(match);
			}
		}

		if (arms.Count == 0)
		{
			return fallback;
		}

		return arms[context.CompletedStarts % arms.Count];
	}

	private static List<Player> BuildBullpen(
		IReadOnlyList<Player> roster,
		TeamRotation? rotation,
		Player starter,
		Player? closer,
		IReadOnlyList<Player> automatic)
	{
		var used = new HashSet<Guid> { starter.Id };
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
