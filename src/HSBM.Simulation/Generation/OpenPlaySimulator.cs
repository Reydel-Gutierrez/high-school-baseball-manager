using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// Runs District Open Play through the same game engine used for the season.
/// Box scores are discarded; only per-player lines are kept.
/// </summary>
public static class OpenPlaySimulator
{
	private const int SquadSize = 10;
	private const int GamesPerPlayer = LeagueCatalog.OpenPlayGamesPerPlayer;

	public static DistrictOpenPlay Simulate(
		string districtId,
		int seasonYear,
		IReadOnlyList<Player> players,
		ISeededRandom rng,
		BaseballGameSimulator? simulator = null)
	{
		SeasonEvent window = SeasonCalendar.OpenPlay(seasonYear);
		var config = new BaseballSimulationConfig { RegulationInnings = 5 };
		var lineups = new OpenPlayLineupProvider();
		simulator ??= new BaseballGameSimulator(config, lineups);
		List<List<Player>> squads = SplitSquads(players);
		var batting = new Dictionary<Guid, TryoutHittingLine>();
		var pitching = new Dictionary<Guid, TryoutPitchingLine>();

		int n = squads.Count;
		var rotation = Enumerable.Range(0, n).ToList();
		for (int round = 0; round < GamesPerPlayer; round++)
		{
			lineups.Round = round;
			for (int i = 0; i < n / 2; i++)
			{
				int homeIndex = rotation[i];
				int awayIndex = rotation[n - 1 - i];
				GameSimulationResult result = simulator.Simulate(
					new ScheduledGame
					{
						Id = rng.NextGuid(),
						SeasonYear = seasonYear,
						Date = window.Start.AddDays(Math.Min(round * 3, 13)),
						HomeTeamId = $"open-{homeIndex}",
						AwayTeamId = $"open-{awayIndex}",
						TeamLevel = TeamLevel.JuniorVarsity,
						GameType = GameType.NonDistrict,
						Status = GameStatus.Scheduled,
					},
					squads[homeIndex],
					squads[awayIndex],
					rng.Derive(round * 31 + i));
				ApplyBatting(batting, result.Batting);
				ApplyPitching(pitching, result.Pitching);
			}

			if (n > 2)
			{
				int last = rotation[^1];
				rotation.RemoveAt(rotation.Count - 1);
				rotation.Insert(1, last);
			}
		}

		var lines = new List<OpenPlayLine>(players.Count);
		foreach (Player player in players)
		{
			batting.TryGetValue(player.Id, out TryoutHittingLine? hit);
			pitching.TryGetValue(player.Id, out TryoutPitchingLine? pit);
			hit ??= TryoutHittingLine.Empty;
			lines.Add(new OpenPlayLine
			{
				PlayerId = player.Id,
				Hitting = hit,
				Pitching = player.IsPitcher ? pit : null,
				Grade = GradeOf(player, hit, pit),
			});
		}

		return new DistrictOpenPlay(
			districtId,
			seasonYear,
			window,
			players.Select(p => p.Id).ToList(),
			lines);
	}

	private static List<List<Player>> SplitSquads(IReadOnlyList<Player> players)
	{
		var pool = players.ToList();
		var squads = new List<List<Player>>();
		for (int i = 0; i < pool.Count; i += SquadSize)
		{
			int take = Math.Min(SquadSize, pool.Count - i);
			var squad = pool.GetRange(i, take);
			if (squad.Count >= 9)
			{
				squads.Add(squad);
			}
			else if (squads.Count > 0)
			{
				squads[^1].AddRange(squad);
			}
		}

		if (squads.Count % 2 == 1 && squads.Count > 1)
		{
			squads[^2].AddRange(squads[^1]);
			squads.RemoveAt(squads.Count - 1);
		}

		return squads;
	}

	private static void ApplyBatting(Dictionary<Guid, TryoutHittingLine> totals, IReadOnlyList<PlayerGameBatting> lines)
	{
		foreach (PlayerGameBatting line in lines)
		{
			var add = new TryoutHittingLine
			{
				Games = 1,
				AtBats = line.AtBats,
				Runs = line.Runs,
				Hits = line.Hits,
				Doubles = line.Doubles,
				Triples = line.Triples,
				HomeRuns = line.HomeRuns,
				Rbi = line.RunsBattedIn,
				Walks = line.Walks,
				Strikeouts = line.Strikeouts,
			};
			totals[line.PlayerId] = totals.TryGetValue(line.PlayerId, out TryoutHittingLine? existing)
				? existing.Combine(add)
				: add;
		}
	}

	private static void ApplyPitching(Dictionary<Guid, TryoutPitchingLine> totals, IReadOnlyList<PlayerGamePitching> lines)
	{
		foreach (PlayerGamePitching line in lines)
		{
			var add = new TryoutPitchingLine
			{
				Games = 1,
				GamesStarted = line.GamesStarted,
				Wins = line.Win ? 1 : 0,
				Losses = line.Loss ? 1 : 0,
				Outs = line.OutsRecorded,
				Hits = line.HitsAllowed,
				Runs = line.RunsAllowed,
				EarnedRuns = line.EarnedRuns,
				Walks = line.Walks,
				Strikeouts = line.Strikeouts,
				HomeRuns = line.HomeRunsAllowed,
			};
			totals[line.PlayerId] = totals.TryGetValue(line.PlayerId, out TryoutPitchingLine? existing)
				? existing.Combine(add)
				: add;
		}
	}

	private static LetterGrade GradeOf(Player player, TryoutHittingLine hitting, TryoutPitchingLine? pitching)
	{
		double score;
		if (player.IsPitcher && pitching != null && pitching.Outs > 0)
		{
			double era = pitching.Era;
			double k9 = pitching.Outs == 0 ? 0 : pitching.Strikeouts * 27.0 / pitching.Outs;
			score = 72 - (era * 6) + (k9 * 1.4) + ((player.Overall - 55) * 0.25);
		}
		else
		{
			double ops = hitting.OnBase + hitting.Slugging;
			score = 48 + (ops * 38) + ((player.Overall - 55) * 0.2);
		}

		return LetterGradeExtensions.FromScore(score);
	}
}

internal sealed class OpenPlayLineupProvider : ILineupProvider
{
	private static readonly PlayerPosition[] Field =
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

	public int Round { get; set; }

	public TeamGamePlan Create(IReadOnlyList<Player> roster)
	{
		if (roster.Count == 0)
		{
			throw new InvalidOperationException("Cannot build an Open Play lineup from an empty roster.");
		}

		var ordered = roster.OrderBy(p => p.Id).ToList();
		List<Player> active = ordered;
		if (ordered.Count > 9)
		{
			Guid sit = ordered[Round % ordered.Count].Id;
			active = ordered.Where(p => p.Id != sit).Take(9).ToList();
		}

		Player starter = active
			.Where(p => p.IsPitcher)
			.OrderByDescending(p => PlayerOverall.PitchingRating(p.Ratings))
			.FirstOrDefault()
			?? active.OrderByDescending(p => PlayerOverall.PitchingRating(p.Ratings)).First();

		var batting = new List<LineupBatter>(active.Count);
		int slot = 0;
		foreach (Player player in active.OrderByDescending(p => PlayerOverall.OffensiveRating(p.Ratings)))
		{
			PlayerPosition field = slot < Field.Length ? Field[slot] : PlayerPosition.LeftField;
			batting.Add(new LineupBatter(player, field));
			slot++;
		}

		return new TeamGamePlan
		{
			BattingOrder = batting,
			StartingPitcher = starter,
		};
	}
}
