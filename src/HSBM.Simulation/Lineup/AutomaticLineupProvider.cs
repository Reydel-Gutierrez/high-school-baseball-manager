using HSBM.Domain;

namespace HSBM.Simulation;

public sealed record LineupBatter(Player Player, PlayerPosition FieldPosition);

public sealed class TeamGamePlan
{
	public required IReadOnlyList<LineupBatter> BattingOrder { get; init; }
	public required Player StartingPitcher { get; init; }
	public IReadOnlyList<Player> Bullpen { get; init; } = [];
	public Player? Closer { get; init; }
}

public interface ILineupProvider
{
	TeamGamePlan Create(IReadOnlyList<Player> roster);
}

public sealed class AutomaticLineupProvider : ILineupProvider
{
	private static readonly PlayerPosition[] FieldSlots =
	[
		PlayerPosition.Catcher,
		PlayerPosition.FirstBase,
		PlayerPosition.SecondBase,
		PlayerPosition.ThirdBase,
		PlayerPosition.Shortstop,
		PlayerPosition.LeftField,
		PlayerPosition.CenterField,
		PlayerPosition.RightField,
	];

	public TeamGamePlan Create(IReadOnlyList<Player> roster)
	{
		if (roster.Count == 0)
		{
			throw new InvalidOperationException("Cannot build a lineup from an empty roster.");
		}

		List<Player> pitchers = roster.Where(p => p.IsPitcher).ToList();
		List<Player> hitters = roster.Where(p => !p.IsPitcher).ToList();
		if (hitters.Count == 0)
		{
			hitters = roster.ToList();
		}

		Player starter = SelectStarter(pitchers, roster);
		var used = new HashSet<Guid> { starter.Id };
		var defense = new List<LineupBatter>(8);
		foreach (PlayerPosition position in FieldSlots)
		{
			Player? match = BestUnused(hitters, used, position) ?? BestUnused(roster, used, position);
			if (match == null)
			{
				break;
			}

			used.Add(match.Id);
			defense.Add(new LineupBatter(match, position));
		}

		var order = new List<Player>(9);
		foreach (LineupBatter slot in defense)
		{
			AddUnique(order, slot.Player);
		}

		foreach (Player hitter in hitters.OrderByDescending(p => PlayerOverall.OffensiveRating(p.Ratings)))
		{
			if (order.Count >= 9)
			{
				break;
			}

			AddUnique(order, hitter);
		}

		foreach (Player player in roster)
		{
			if (order.Count >= 9)
			{
				break;
			}

			AddUnique(order, player);
		}

		AddUnique(order, starter);
		if (order.Count == 0)
		{
			order.Add(starter);
		}

		var defenseById = new Dictionary<Guid, PlayerPosition>();
		foreach (LineupBatter slot in defense)
		{
			defenseById.TryAdd(slot.Player.Id, slot.FieldPosition);
		}

		var battingOrder = new List<LineupBatter>(order.Count);
		foreach (Player batter in order.Take(9))
		{
			PlayerPosition field = defenseById.TryGetValue(batter.Id, out PlayerPosition pos)
				? pos
				: batter.Id == starter.Id
					? PlayerPosition.StartingPitcher
					: PlayerPosition.LeftField;
			battingOrder.Add(new LineupBatter(batter, field));
		}

		Player? closer = SelectCloser(pitchers, starter);
		return new TeamGamePlan
		{
			BattingOrder = battingOrder,
			StartingPitcher = starter,
			Bullpen = BullpenFor(roster, starter, closer),
			Closer = closer,
		};
	}

	private static Player? SelectCloser(List<Player> pitchers, Player starter)
	{
		Player? closer = null;
		double best = double.MinValue;
		foreach (Player arm in pitchers)
		{
			if (arm.Id == starter.Id)
			{
				continue;
			}

			if (arm.PrimaryPosition != PlayerPosition.ReliefPitcher && pitchers.Count > 3)
			{
				continue;
			}

			double score = (arm.Ratings.Control * 0.55) + (arm.Ratings.Stuff * 0.35) + (arm.Ratings.Movement * 0.10);
			if (score > best)
			{
				best = score;
				closer = arm;
			}
		}

		return closer;
	}

	private static List<Player> BullpenFor(IReadOnlyList<Player> roster, Player starter, Player? closer)
	{
		var arms = new List<Player>();
		foreach (Player player in roster)
		{
			if (!player.IsPitcher || player.Id == starter.Id)
			{
				continue;
			}

			if (closer != null && player.Id == closer.Id)
			{
				continue;
			}

			arms.Add(player);
		}

		arms.Sort((a, b) =>
		{
			int rating = PlayerOverall.PitchingRating(b.Ratings).CompareTo(PlayerOverall.PitchingRating(a.Ratings));
			return rating != 0 ? rating : string.CompareOrdinal(a.LastName, b.LastName);
		});
		return arms;
	}

	private static Player SelectStarter(List<Player> pitchers, IReadOnlyList<Player> roster)
	{
		Player? sp = pitchers
			.Where(p => p.PrimaryPosition == PlayerPosition.StartingPitcher)
			.OrderByDescending(p => PlayerOverall.PitchingRating(p.Ratings))
			.FirstOrDefault();
		if (sp != null)
		{
			return sp;
		}

		return pitchers
			.OrderByDescending(p => PlayerOverall.PitchingRating(p.Ratings))
			.FirstOrDefault()
			?? roster.OrderByDescending(p => PlayerOverall.PitchingRating(p.Ratings)).First();
	}

	private static Player? BestUnused(
		IEnumerable<Player> pool,
		HashSet<Guid> used,
		PlayerPosition position)
	{
		return pool
			.Where(p => !used.Contains(p.Id) && (p.PrimaryPosition == position || p.SecondaryPosition == position))
			.OrderByDescending(p => p.PrimaryPosition == position ? 1 : 0)
			.ThenByDescending(p => PlayerOverall.Calculate(position, p.Ratings))
			.FirstOrDefault()
			?? pool
				.Where(p => !used.Contains(p.Id))
				.OrderByDescending(p => PlayerOverall.Calculate(position, p.Ratings))
				.FirstOrDefault();
	}

	private static void AddUnique(List<Player> order, Player player)
	{
		if (order.TrueForAll(existing => existing.Id != player.Id))
		{
			order.Add(player);
		}
	}
}
