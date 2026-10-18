using HSBM.Domain;

namespace HSBM.Simulation;

public sealed class SimulatedGame
{
	public required Guid GameId { get; init; }
	public required DateOnly Date { get; init; }
	public required string AwayTeamId { get; init; }
	public required string HomeTeamId { get; init; }
	public required int AwayScore { get; init; }
	public required int HomeScore { get; init; }
	public required int InningsPlayed { get; init; }
	public required TeamLevel TeamLevel { get; init; }
	public required GameType GameType { get; init; }
	public required string WinningTeamId { get; init; }

	public bool Involves(string teamId) =>
		HomeTeamId == teamId || AwayTeamId == teamId;
}

public sealed class WorldDayResult
{
	public required DateOnly Date { get; init; }
	public required SeasonPhase Phase { get; init; }
	public List<SimulatedGame> GamesPlayed { get; init; } = [];
	public List<PlayerTransaction> Transactions { get; init; } = [];
	public bool Graduated { get; init; }
	public bool RolledOver { get; init; }
}

/// <summary>
/// One calendar day for the whole world: games, injuries, recruiting,
/// AI roster work, and scheduled season beats.
/// </summary>
public static class WorldDayProcessor
{
	public static WorldDayResult Run(
		CareerState state,
		DateOnly today,
		BaseballGameSimulator simulator)
	{
		int year = state.Career.CurrentSeasonYear;
		TryoutDesk.Migrate(state, today);
		var posted = new List<PlayerTransaction>();
		posted.AddRange(InjuryDesk.Tick(state, today));
		PostseasonDesk.Tick(state, today);

		var played = new List<SimulatedGame>();
		List<ScheduledGame> todays = state.Games
			.Where(game => game.Date == today && game.Status == GameStatus.Scheduled)
			.ToList();

		foreach (ScheduledGame scheduled in todays)
		{
			List<Player> home = Roster(state, scheduled.HomeTeamId, today);
			List<Player> away = Roster(state, scheduled.AwayTeamId, today);
			if (home.Count == 0 && away.Count == 0)
			{
				scheduled.Status = GameStatus.Postponed;
				continue;
			}

			if (home.Count == 0 || away.Count == 0)
			{
				GameSimulationResult forfeit = Forfeit(scheduled, home, away);
				ApplyResult(state, scheduled, forfeit);
				played.Add(ToSummary(scheduled, forfeit));
				continue;
			}

			ISeededRandom rng = new SeededRandom(SeededRandom.Mix(state.Career.RandomSeed, scheduled.Id));
			GameSimulationResult result = simulator.Simulate(
				scheduled,
				PlanFor(state, scheduled.HomeTeamId, today, home),
				PlanFor(state, scheduled.AwayTeamId, today, away),
				rng);
			ApplyResult(state, scheduled, result);
			played.Add(ToSummary(scheduled, result));
			ISeededRandom injuryRng = rng.Derive(19);
			posted.AddRange(InjuryDesk.FromGame(state, scheduled, result, injuryRng));
		}

		PostseasonDesk.Resolve(state, today);

		ISeededRandom developmentRng = new SeededRandom(SeededRandom.Mix(state.Career.RandomSeed, today.DayNumber ^ 0x0DE7));
		posted.AddRange(DevelopmentDesk.Weekly(state, today, developmentRng));

		if (today == SeasonCalendar.TryoutDecisionDay(year))
		{
			posted.AddRange(TryoutDesk.Finalize(state, today));
		}

		ISeededRandom deskRng = new SeededRandom(SeededRandom.Mix(state.Career.RandomSeed, today.DayNumber ^ 91));
		posted.AddRange(AiProgramController.AdvanceDay(state, today, deskRng));

		bool graduated = false;
		if (SeasonCalendar.Graduation(year).OccursOn(today)
			|| SeasonCalendar.SeniorFarewell(year).OccursOn(today))
		{
			posted.AddRange(LogGraduates(state, today));
			graduated = GraduateSeniors(state);
		}

		foreach (PlayerTransaction entry in posted)
		{
			if (string.IsNullOrWhiteSpace(entry.DedupKey))
			{
				state.Transactions.Add(entry);
				continue;
			}

			bool exists = false;
			foreach (PlayerTransaction existing in state.Transactions)
			{
				if (string.Equals(existing.DedupKey, entry.DedupKey, StringComparison.Ordinal))
				{
					exists = true;
					break;
				}
			}

			if (!exists)
			{
				state.Transactions.Add(entry);
			}
		}

		return new WorldDayResult
		{
			Date = today,
			Phase = CareerCalendar.PhaseOn(year, today),
			GamesPlayed = played,
			Transactions = posted,
			Graduated = graduated,
		};
	}

	/// <summary>
	/// Set up a scheduled game to be played live with exactly the rosters,
	/// lineups, rotation, and random stream the day processor would use.
	/// Returns null when a side cannot field a team (it will forfeit).
	/// </summary>
	public static LiveGame? PrepareLive(CareerState state, ScheduledGame scheduled, BaseballGameSimulator simulator)
	{
		DateOnly today = scheduled.Date;
		List<Player> home = Roster(state, scheduled.HomeTeamId, today);
		List<Player> away = Roster(state, scheduled.AwayTeamId, today);
		if (home.Count == 0 || away.Count == 0)
		{
			return null;
		}

		ISeededRandom rng = new SeededRandom(SeededRandom.Mix(state.Career.RandomSeed, scheduled.Id));
		return simulator.StartLive(
			scheduled,
			PlanFor(state, scheduled.HomeTeamId, today, home),
			PlanFor(state, scheduled.AwayTeamId, today, away),
			rng,
			home,
			away);
	}

	private static TeamGamePlan PlanFor(
		CareerState state,
		string teamId,
		DateOnly today,
		IReadOnlyList<Player> roster)
	{
		return CareerGamePlanProvider.Create(roster, new GamePlanContext
		{
			TeamId = teamId,
			Date = today,
			Lineup = state.LineupFor(teamId),
			Rotation = state.RotationFor(teamId),
			CompletedStarts = state.CompletedStarts(teamId),
		});
	}

	private static List<Player> Roster(CareerState state, string teamId, DateOnly today)
	{
		var available = new List<Player>();
		var all = new List<Player>();
		foreach (Player player in state.Players)
		{
			if (player.TeamId != teamId || player.Alumni)
			{
				continue;
			}

			all.Add(player);
			if (player.CanPlay(today))
			{
				available.Add(player);
			}
		}

		return available.Count >= 9 ? available : all;
	}

	private static GameSimulationResult Forfeit(
		ScheduledGame scheduled,
		List<Player> home,
		List<Player> away)
	{
		bool homeWins = home.Count > 0;
		int homeScore = homeWins ? 7 : 0;
		int awayScore = homeWins ? 0 : 7;
		int[] homeInn = new int[9];
		int[] awayInn = new int[9];
		if (homeWins)
		{
			homeInn[0] = 7;
		}
		else
		{
			awayInn[0] = 7;
		}

		return new GameSimulationResult
		{
			GameId = scheduled.Id,
			HomeTeamId = scheduled.HomeTeamId,
			AwayTeamId = scheduled.AwayTeamId,
			HomeScore = homeScore,
			AwayScore = awayScore,
			InningsPlayed = 9,
			WinningPitcherId = Guid.Empty,
			LosingPitcherId = Guid.Empty,
			HomeInningRuns = homeInn,
			AwayInningRuns = awayInn,
			Batting = [],
			Pitching = [],
			Fielding = [],
			FieldingPlays = [],
			BoxScore = new GameBoxScore
			{
				GameId = scheduled.Id,
				HomeInningRuns = homeInn,
				AwayInningRuns = awayInn,
			},
		};
	}

	private static bool GraduateSeniors(CareerState state) =>
		PlayerGraduation.Apply(state.Players, state.Career.CurrentSeasonYear) > 0;

	private static List<PlayerTransaction> LogGraduates(CareerState state, DateOnly today)
	{
		var posted = new List<PlayerTransaction>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (PlayerTransaction existing in state.Transactions)
		{
			if (!string.IsNullOrWhiteSpace(existing.DedupKey))
			{
				seen.Add(existing.DedupKey);
			}
		}

		foreach (Player player in state.Players)
		{
			if (!PlayerGraduation.ShouldGraduate(player))
			{
				continue;
			}

			string school = player.OrganizationId;
			try
			{
				school = state.OrganizationById(player.OrganizationId).ShortName;
			}
			catch (KeyNotFoundException)
			{
			}

			PlayerTransaction entry = PlayerTransaction.Graduated(player, school, player.OrganizationId, today);
			if (string.IsNullOrWhiteSpace(entry.DedupKey) || !seen.Add(entry.DedupKey))
			{
				continue;
			}

			posted.Add(entry);
		}

		return posted;
	}

	public static void ApplyResult(CareerState state, ScheduledGame scheduled, GameSimulationResult result)
	{
		scheduled.Status = GameStatus.Completed;
		scheduled.HomeScore = result.HomeScore;
		scheduled.AwayScore = result.AwayScore;
		scheduled.InningsPlayed = result.InningsPlayed;
		scheduled.WinningTeamId = result.WinningTeamId;
		scheduled.LosingTeamId = result.LosingTeamId;
		scheduled.WinningPitcherId = result.WinningPitcherId;
		scheduled.LosingPitcherId = result.LosingPitcherId;
		scheduled.SavePitcherId = result.SavePitcherId;
		state.UpsertBoxScore(result.BoxScore);

		if (scheduled.GameType.IsExhibition() || !scheduled.GameType.CountsInStandings())
		{
			if (!scheduled.GameType.IsExhibition())
			{
				ApplyStats(state, result);
			}

			return;
		}

		bool district = scheduled.GameType == GameType.District;
		state.RecordFor(scheduled.HomeTeamId).ApplyGame(
			result.HomeScore,
			result.AwayScore,
			result.HomeScore > result.AwayScore,
			district);
		state.RecordFor(scheduled.AwayTeamId).ApplyGame(
			result.AwayScore,
			result.HomeScore,
			result.AwayScore > result.HomeScore,
			district);
		ApplyStats(state, result);
	}

	private static void ApplyStats(CareerState state, GameSimulationResult result)
	{
		foreach (PlayerGameBatting line in result.Batting)
		{
			Player player = state.Players.First(p => p.Id == line.PlayerId);
			BattingSeasonStats stats = state.BattingFor(player.Id, player.TeamId);
			stats.Games++;
			stats.PlateAppearances += line.PlateAppearances;
			stats.AtBats += line.AtBats;
			stats.Runs += line.Runs;
			stats.Hits += line.Hits;
			stats.Doubles += line.Doubles;
			stats.Triples += line.Triples;
			stats.HomeRuns += line.HomeRuns;
			stats.RunsBattedIn += line.RunsBattedIn;
			stats.Walks += line.Walks;
			stats.Strikeouts += line.Strikeouts;
			stats.StolenBases += line.StolenBases;
		}

		foreach (PlayerGamePitching line in result.Pitching)
		{
			Player player = state.Players.First(p => p.Id == line.PlayerId);
			PitchingSeasonStats stats = state.PitchingFor(player.Id, player.TeamId);
			stats.Games++;
			stats.GamesStarted += line.GamesStarted;
			stats.OutsRecorded += line.OutsRecorded;
			stats.HitsAllowed += line.HitsAllowed;
			stats.RunsAllowed += line.RunsAllowed;
			stats.EarnedRuns += line.EarnedRuns;
			stats.Walks += line.Walks;
			stats.Strikeouts += line.Strikeouts;
			stats.HomeRunsAllowed += line.HomeRunsAllowed;
			if (line.Win)
			{
				stats.Wins++;
			}

			if (line.Loss)
			{
				stats.Losses++;
			}

			if (line.Save)
			{
				stats.Saves++;
			}
		}

		var appeared = new HashSet<Guid>();
		foreach (PlayerGameBatting line in result.Batting)
		{
			if (line.PlateAppearances > 0)
			{
				appeared.Add(line.PlayerId);
			}
		}

		foreach (PlayerGamePitching line in result.Pitching)
		{
			appeared.Add(line.PlayerId);
		}

		foreach (PlayerGameFielding line in result.Fielding)
		{
			Player player = state.Players.First(p => p.Id == line.PlayerId);
			FieldingSeasonStats stats = state.FieldingFor(player.Id, player.TeamId);
			stats.Putouts += line.Putouts;
			stats.Assists += line.Assists;
			stats.Errors += line.Errors;
			appeared.Add(line.PlayerId);
		}

		foreach (Guid playerId in appeared)
		{
			Player player = state.Players.First(p => p.Id == playerId);
			state.FieldingFor(player.Id, player.TeamId).Games++;
		}
	}

	private static SimulatedGame ToSummary(ScheduledGame scheduled, GameSimulationResult result) =>
		new()
		{
			GameId = scheduled.Id,
			Date = scheduled.Date,
			AwayTeamId = scheduled.AwayTeamId,
			HomeTeamId = scheduled.HomeTeamId,
			AwayScore = result.AwayScore,
			HomeScore = result.HomeScore,
			InningsPlayed = result.InningsPlayed,
			TeamLevel = scheduled.TeamLevel,
			GameType = scheduled.GameType,
			WinningTeamId = result.WinningTeamId,
		};
}
