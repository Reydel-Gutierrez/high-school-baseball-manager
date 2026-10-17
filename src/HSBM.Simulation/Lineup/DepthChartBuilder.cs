using HSBM.Domain;

namespace HSBM.Simulation;

public static class DepthChartBuilder
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

	public static DepthChart Build(IReadOnlyList<Player> roster)
	{
		if (roster.Count == 0)
		{
			return new DepthChart();
		}

		List<Player> pitchers = [];
		List<Player> hitters = [];
		foreach (Player player in roster)
		{
			if (player.IsPitcher)
			{
				pitchers.Add(player);
			}
			else
			{
				hitters.Add(player);
			}
		}

		if (hitters.Count == 0)
		{
			hitters = [.. roster];
		}

		var used = new HashSet<Guid>();
		var field = new List<DepthChartSlot>(FieldSlots.Length);
		foreach (PlayerPosition position in FieldSlots)
		{
			List<Player> depth = RankAt(hitters, roster, used, position);
			if (depth.Count > 0)
			{
				used.Add(depth[0].Id);
			}

			field.Add(new DepthChartSlot(position, depth));
		}

		List<PitchingAssignment> pitching = AssignPitching(pitchers, roster);
		foreach (PitchingAssignment arm in pitching)
		{
			used.Add(arm.Player.Id);
		}

		var bench = new List<Player>();
		foreach (Player player in roster)
		{
			if (used.Contains(player.Id))
			{
				continue;
			}

			bench.Add(player);
		}

		bench.Sort((a, b) =>
		{
			int overall = b.Overall.CompareTo(a.Overall);
			return overall != 0 ? overall : string.CompareOrdinal(a.LastName, b.LastName);
		});

		return new DepthChart
		{
			Field = field,
			Pitching = pitching,
			Bench = bench,
		};
	}

	private static List<Player> RankAt(
		List<Player> hitters,
		IReadOnlyList<Player> roster,
		HashSet<Guid> starterUsed,
		PlayerPosition position)
	{
		var ranked = new List<Player>();
		AddRanked(ranked, hitters, position);
		if (ranked.Count == 0)
		{
			AddRanked(ranked, roster, position);
		}

		ranked.Sort((a, b) => CompareAt(a, b, position));

		var ordered = new List<Player>(ranked.Count + 1);
		foreach (Player player in ranked)
		{
			if (!starterUsed.Contains(player.Id))
			{
				ordered.Add(player);
			}
		}

		if (ordered.Count == 0)
		{
			Player? fallback = BestUnused(roster, starterUsed, position);
			if (fallback != null)
			{
				ordered.Add(fallback);
			}
		}

		foreach (Player player in ranked)
		{
			if (!Contains(ordered, player.Id))
			{
				ordered.Add(player);
			}
		}

		return ordered;
	}

	private static void AddRanked(List<Player> ranked, IEnumerable<Player> pool, PlayerPosition position)
	{
		foreach (Player player in pool)
		{
			if (player.PrimaryPosition == position || player.SecondaryPosition == position)
			{
				ranked.Add(player);
			}
		}
	}

	private static int CompareAt(Player left, Player right, PlayerPosition position)
	{
		int listed = ListedScore(right, position).CompareTo(ListedScore(left, position));
		if (listed != 0)
		{
			return listed;
		}

		int overall = PlayerOverall.Calculate(position, right.Ratings)
			.CompareTo(PlayerOverall.Calculate(position, left.Ratings));
		return overall != 0 ? overall : string.CompareOrdinal(left.LastName, right.LastName);
	}

	private static Player? BestUnused(
		IReadOnlyList<Player> roster,
		HashSet<Guid> starterUsed,
		PlayerPosition position)
	{
		Player? best = null;
		int bestScore = int.MinValue;
		foreach (Player player in roster)
		{
			if (starterUsed.Contains(player.Id) || player.IsPitcher)
			{
				continue;
			}

			int score = PlayerOverall.Calculate(position, player.Ratings);
			if (score > bestScore)
			{
				bestScore = score;
				best = player;
			}
		}

		if (best != null)
		{
			return best;
		}

		foreach (Player player in roster)
		{
			if (starterUsed.Contains(player.Id))
			{
				continue;
			}

			int score = PlayerOverall.Calculate(position, player.Ratings);
			if (score > bestScore)
			{
				bestScore = score;
				best = player;
			}
		}

		return best;
	}

	private static bool Contains(List<Player> players, Guid id)
	{
		foreach (Player player in players)
		{
			if (player.Id == id)
			{
				return true;
			}
		}

		return false;
	}

	private static int ListedScore(Player player, PlayerPosition position)
	{
		if (player.PrimaryPosition == position)
		{
			return 2;
		}

		return player.SecondaryPosition == position ? 1 : 0;
	}

	private static List<PitchingAssignment> AssignPitching(List<Player> pitchers, IReadOnlyList<Player> roster)
	{
		var arms = new List<Player>();
		if (pitchers.Count > 0)
		{
			arms.AddRange(pitchers);
		}
		else
		{
			arms.AddRange(roster);
		}

		arms.Sort((a, b) =>
		{
			int rating = PlayerOverall.PitchingRating(b.Ratings).CompareTo(PlayerOverall.PitchingRating(a.Ratings));
			return rating != 0 ? rating : string.CompareOrdinal(a.LastName, b.LastName);
		});

		var assigned = new List<PitchingAssignment>(arms.Count);
		int starters = 0;
		Player? closer = BestCloser(arms);
		foreach (Player arm in arms)
		{
			if (closer != null && arm.Id == closer.Id)
			{
				continue;
			}

			if (starters == 0)
			{
				assigned.Add(new PitchingAssignment(arm, PitchingRole.Ace, assigned.Count));
				starters++;
				continue;
			}

			if (starters < 4 && (arm.PrimaryPosition == PlayerPosition.StartingPitcher || starters < 2))
			{
				assigned.Add(new PitchingAssignment(arm, PitchingRole.Starter, assigned.Count));
				starters++;
				continue;
			}

			assigned.Add(new PitchingAssignment(arm, PitchingRole.Reliever, assigned.Count));
		}

		if (closer != null)
		{
			assigned.Add(new PitchingAssignment(closer, PitchingRole.Closer, assigned.Count));
		}

		return assigned;
	}

	private static Player? BestCloser(List<Player> arms)
	{
		Player? best = null;
		double bestScore = double.MinValue;
		foreach (Player arm in arms)
		{
			if (arm.PrimaryPosition != PlayerPosition.ReliefPitcher && arms.Count > 3)
			{
				continue;
			}

			double score = (arm.Ratings.Control * 0.55) + (arm.Ratings.Stuff * 0.35) + (arm.Ratings.Movement * 0.10);
			if (score > bestScore)
			{
				bestScore = score;
				best = arm;
			}
		}

		return best;
	}
}
