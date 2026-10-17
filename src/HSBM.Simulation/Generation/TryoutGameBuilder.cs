using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// Splits the invite list into Team Blue and Team White and turns each
/// day's already-rolled lines into a coherent scrimmage box score.
/// </summary>
public static class TryoutGameBuilder
{
	public static IReadOnlyList<TryoutGame> Build(SeasonEvent window, IReadOnlyList<TryoutProspect> prospects)
	{
		var blue = new List<TryoutProspect>();
		var white = new List<TryoutProspect>();
		for (int i = 0; i < prospects.Count; i++)
		{
			if (i % 2 == 0)
			{
				blue.Add(prospects[i]);
			}
			else
			{
				white.Add(prospects[i]);
			}
		}

		return
		[
			BuildGame(1, TryoutDay.Day1, TryoutCamp.ScrimmageDate(window, TryoutDay.Day1), blue, white),
			BuildGame(2, TryoutDay.Day2, TryoutCamp.ScrimmageDate(window, TryoutDay.Day2), blue, white),
			BuildGame(3, TryoutDay.Day3, TryoutCamp.ScrimmageDate(window, TryoutDay.Day3), blue, white),
		];
	}

	private static TryoutGame BuildGame(
		int number,
		TryoutDay day,
		DateOnly date,
		List<TryoutProspect> blue,
		List<TryoutProspect> white)
	{
		List<TryoutProspect> blueLineup = Lineup(blue);
		List<TryoutProspect> whiteLineup = Lineup(white);
		List<TryoutProspect> blueArms = Arms(blue, day);
		List<TryoutProspect> whiteArms = Arms(white, day);

		int blueRuns = TeamRuns(blueLineup, day);
		int whiteRuns = TeamRuns(whiteLineup, day);
		if (blueRuns == 0 && whiteRuns == 0)
		{
			blueRuns = 1;
		}

		int blueHits = TeamHits(blueLineup, day);
		int whiteHits = TeamHits(whiteLineup, day);
		var rng = new Random(Seed(date, number));
		int blueErrors = rng.Next(0, 3);
		int whiteErrors = rng.Next(0, 2);
		int[] blueInnings = SplitRuns(blueRuns, rng);
		int[] whiteInnings = SplitRuns(whiteRuns, rng);

		List<TryoutGameBatter> blueBatters = Batters(blueLineup, day);
		List<TryoutGameBatter> whiteBatters = Batters(whiteLineup, day);
		List<TryoutGamePitcher> bluePitchers = Pitchers(blueArms, day);
		List<TryoutGamePitcher> whitePitchers = Pitchers(whiteArms, day);

		bool blueWon = blueRuns >= whiteRuns;
		IReadOnlyList<TryoutGamePitcher> winningArms = blueWon ? bluePitchers : whitePitchers;
		IReadOnlyList<TryoutGamePitcher> losingArms = blueWon ? whitePitchers : bluePitchers;
		string winner = DecisionPitcher(winningArms, won: true);
		string loser = DecisionPitcher(losingArms, won: false);
		string? save = SavePitcher(winningArms, winner);

		return new TryoutGame(
			number,
			day,
			date,
			blueRuns,
			whiteRuns,
			blueHits,
			whiteHits,
			blueErrors,
			whiteErrors,
			blueInnings,
			whiteInnings,
			blueBatters,
			whiteBatters,
			bluePitchers,
			whitePitchers,
			Standouts(blueLineup, whiteLineup, blueArms, whiteArms, day),
			winner,
			loser,
			save,
			FieldingPlays(blueWon ? blueBatters : whiteBatters, rng));
	}

	private static List<TryoutProspect> Lineup(List<TryoutProspect> side)
	{
		var lineup = new List<TryoutProspect>(9);
		foreach (TryoutProspect prospect in side)
		{
			if (prospect.Position.IsPitcher())
			{
				continue;
			}

			lineup.Add(prospect);
			if (lineup.Count == 9)
			{
				return lineup;
			}
		}

		foreach (TryoutProspect prospect in side)
		{
			if (lineup.Contains(prospect))
			{
				continue;
			}

			lineup.Add(prospect);
			if (lineup.Count == 9)
			{
				break;
			}
		}

		return lineup;
	}

	private static List<TryoutProspect> Arms(List<TryoutProspect> side, TryoutDay day)
	{
		var ranked = new List<TryoutProspect>();
		foreach (TryoutProspect prospect in side)
		{
			if (prospect.PitchingOn(day) != null)
			{
				ranked.Add(prospect);
			}
		}

		ranked.Sort((a, b) =>
		{
			int starter = StarterRank(a).CompareTo(StarterRank(b));
			return starter != 0 ? starter : b.PitchingOn(day)!.Outs.CompareTo(a.PitchingOn(day)!.Outs);
		});

		var used = new List<TryoutProspect>();
		int outs = 0;
		foreach (TryoutProspect arm in ranked)
		{
			used.Add(arm);
			outs += arm.PitchingOn(day)!.Outs;
			if (outs >= 21)
			{
				break;
			}
		}

		if (used.Count == 0 && ranked.Count > 0)
		{
			used.Add(ranked[0]);
		}

		return used;
	}

	private static int StarterRank(TryoutProspect prospect) =>
		prospect.Position == PlayerPosition.StartingPitcher ? 0 : 1;

	private static int TeamRuns(List<TryoutProspect> lineup, TryoutDay day)
	{
		int runs = 0;
		foreach (TryoutProspect prospect in lineup)
		{
			runs += prospect.HittingOn(day).Runs;
		}

		return Math.Clamp(runs, 0, 12);
	}

	private static int TeamHits(List<TryoutProspect> lineup, TryoutDay day)
	{
		int hits = 0;
		foreach (TryoutProspect prospect in lineup)
		{
			hits += prospect.HittingOn(day).Hits;
		}

		return hits;
	}

	private static List<TryoutGameBatter> Batters(List<TryoutProspect> lineup, TryoutDay day)
	{
		var batters = new List<TryoutGameBatter>(lineup.Count);
		foreach (TryoutProspect prospect in lineup)
		{
			TryoutHittingLine line = prospect.HittingOn(day);
			string slot = prospect.Position.IsPitcher() ? "DH" : prospect.Position.ToCode();
			batters.Add(new TryoutGameBatter(
				prospect.FullName,
				prospect.LastName.ToUpperInvariant(),
				slot,
				line.AtBats,
				line.Runs,
				line.Hits,
				line.HomeRuns,
				line.Rbi));
		}

		return batters;
	}

	private static List<TryoutGamePitcher> Pitchers(List<TryoutProspect> arms, TryoutDay day)
	{
		var pitchers = new List<TryoutGamePitcher>(arms.Count);
		foreach (TryoutProspect prospect in arms)
		{
			TryoutPitchingLine? line = prospect.PitchingOn(day);
			if (line == null)
			{
				continue;
			}

			pitchers.Add(new TryoutGamePitcher(
				prospect.FullName,
				prospect.LastName.ToUpperInvariant(),
				line.InningsLabel,
				line.Hits,
				line.Walks,
				line.Strikeouts));
		}

		return pitchers;
	}

	private static List<TryoutStandout> Standouts(
		List<TryoutProspect> blueLineup,
		List<TryoutProspect> whiteLineup,
		List<TryoutProspect> blueArms,
		List<TryoutProspect> whiteArms,
		TryoutDay day)
	{
		TryoutProspect? hitter = BestHitter(blueLineup, whiteLineup, day);
		TryoutProspect? pitcher = BestPitcher(blueArms, whiteArms, day);
		var list = new List<TryoutStandout>(2);
		if (hitter != null)
		{
			TryoutHittingLine line = hitter.HittingOn(day);
			string extra = line.HomeRuns > 0 ? " · HR" : line.Rbi > 0 ? " · RBI" : string.Empty;
			list.Add(new TryoutStandout(
				hitter.FullName,
				$"{line.Hits}-{line.AtBats} · {hitter.Position.ToCode()}{extra}"));
		}

		if (pitcher != null)
		{
			TryoutPitchingLine line = pitcher.PitchingOn(day)!;
			list.Add(new TryoutStandout(
				pitcher.FullName,
				$"{line.InningsLabel} IP · {line.Hits} H · {line.Strikeouts} K"));
		}

		return list;
	}

	private static TryoutProspect? BestHitter(
		List<TryoutProspect> blue,
		List<TryoutProspect> white,
		TryoutDay day)
	{
		TryoutProspect? best = null;
		int bestScore = int.MinValue;
		foreach (TryoutProspect prospect in Enumerate(blue, white))
		{
			TryoutHittingLine line = prospect.HittingOn(day);
			if (line.AtBats <= 0)
			{
				continue;
			}

			int score = (line.Hits * 10) + (line.Rbi * 3) + (line.HomeRuns * 5) + line.Doubles;
			if (score > bestScore)
			{
				bestScore = score;
				best = prospect;
			}
		}

		return best;
	}

	private static TryoutProspect? BestPitcher(
		List<TryoutProspect> blue,
		List<TryoutProspect> white,
		TryoutDay day)
	{
		TryoutProspect? best = null;
		int bestScore = int.MinValue;
		foreach (TryoutProspect prospect in Enumerate(blue, white))
		{
			TryoutPitchingLine? line = prospect.PitchingOn(day);
			if (line == null || line.Outs <= 0)
			{
				continue;
			}

			int score = (line.Strikeouts * 4) + line.Outs - (line.Hits * 2) - line.Walks;
			if (score > bestScore)
			{
				bestScore = score;
				best = prospect;
			}
		}

		return best;
	}

	private static IEnumerable<TryoutProspect> Enumerate(List<TryoutProspect> left, List<TryoutProspect> right)
	{
		foreach (TryoutProspect prospect in left)
		{
			yield return prospect;
		}

		foreach (TryoutProspect prospect in right)
		{
			yield return prospect;
		}
	}

	private static string DecisionPitcher(IReadOnlyList<TryoutGamePitcher> pitchers, bool won)
	{
		if (pitchers.Count == 0)
		{
			return "Staff";
		}

		if (won && pitchers.Count > 1 && Outs(pitchers[0].Innings) < 15)
		{
			return pitchers[1].Name;
		}

		return pitchers[0].Name;
	}

	private static string? SavePitcher(IReadOnlyList<TryoutGamePitcher> winning, string winner)
	{
		if (winning.Count < 2)
		{
			return null;
		}

		TryoutGamePitcher last = winning[^1];
		return last.Name == winner ? null : last.Name;
	}

	private static List<string> FieldingPlays(IReadOnlyList<TryoutGameBatter> side, Random rng)
	{
		string infielder = side.Count > 2 ? side[2].LastName : "HALE";
		string catcher = side.Count > 0 ? side[0].LastName : "CRUZ";
		string first = side.Count > 3 ? side[3].LastName : "COLE";
		string second = side.Count > 5 ? side[5].LastName : "PRICE";
		var plays = new List<string>
		{
			$"{infielder} to {second} to {first} \u2014 6-4-3 double play",
			$"{catcher} threw out a runner stealing second",
		};
		if (rng.Next(0, 2) == 0)
		{
			plays.Add($"E{rng.Next(3, 7)}: throwing error, {infielder}");
		}

		return plays;
	}

	private static int[] SplitRuns(int total, Random rng)
	{
		var innings = new int[TryoutGame.Innings];
		int left = total;
		int cursor = 0;
		while (left > 0)
		{
			int inning = cursor % TryoutGame.Innings;
			int add = Math.Min(left, rng.Next(1, 4));
			if (innings[inning] + add > 5)
			{
				add = 1;
			}

			innings[inning] += add;
			left -= add;
			cursor++;
			if (cursor > 40)
			{
				innings[0] += left;
				break;
			}
		}

		return innings;
	}

	private static int Outs(string innings)
	{
		string[] parts = innings.Split('.');
		int whole = parts.Length > 0 && int.TryParse(parts[0], out int n) ? n : 0;
		int extra = parts.Length > 1 && int.TryParse(parts[1], out int r) ? r : 0;
		return whole * 3 + extra;
	}

	private static int Seed(DateOnly date, int number)
	{
		int hash = 17;
		hash = hash * 31 + date.DayNumber;
		hash = hash * 31 + number;
		return hash;
	}
}
