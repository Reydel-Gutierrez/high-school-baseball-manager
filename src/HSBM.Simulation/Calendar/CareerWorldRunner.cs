using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// Headless career driver used by LeagueSimulator and the multi-season
/// tests. Ticks the same clock as a live save, without SQLite I/O.
/// </summary>
public static class CareerWorldRunner
{
	public const int DefaultSeasons = 10;
	public const int MaxDaysPerSeason = 400;

	public static CareerState FromGenerated(
		GeneratedWorld world,
		string name = "League Sim",
		string userOrganizationId = "")
	{
		DateOnly start = SeasonCalendar.OpenPlayStart(world.SeasonYear);
		var state = new CareerState
		{
			Career = new Career
			{
				Id = Guid.NewGuid(),
				Name = name,
				CreatedAt = DateTimeOffset.UtcNow,
				CurrentDate = start,
				CurrentSeasonYear = world.SeasonYear,
				UserOrganizationId = userOrganizationId,
				RandomSeed = world.Seed,
				CompetitionLevel = world.CompetitionLevel,
				SaveVersion = Career.CurrentSaveVersion,
			},
			World = world.ToGameWorld(start),
		};
		state.TeamRecords = world.Teams
			.Where(team => team.RosterCap > 0)
			.Select(team => new TeamSeasonRecord
			{
				TeamId = team.Id,
				SeasonYear = world.SeasonYear,
			})
			.ToList();
		state.BattingStats = [];
		state.PitchingStats = [];
		state.FieldingStats = [];
		state.Lineups = [];
		state.Rotations = [];
		state.BoxScores = [];
		state.Offers = [];
		state.Agreements = [];
		state.SyncClockFromCareer();
		NewsWire.Publish(state);
		return state;
	}

	public static CareerState NewCareer(
		int seed,
		int seasonYear = LeagueCatalog.DefaultSeasonYear,
		string userOrganizationId = "",
		string name = "League Sim")
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, seed, seasonYear);
		return FromGenerated(world, name, userOrganizationId);
	}

	public static CareerTickResult AdvanceDay(
		CareerState state,
		BaseballGameSimulator? simulator = null,
		LeagueCatalog? catalog = null) =>
		CareerClock.Tick(state, simulator ?? new BaseballGameSimulator(), catalog);

	public static LeagueSimulationResult AdvanceUntil(
		CareerState state,
		Func<CareerState, CareerTickResult, bool> stop,
		BaseballGameSimulator? simulator = null,
		LeagueCatalog? catalog = null,
		int maxDays = MaxDaysPerSeason)
	{
		if (maxDays < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(maxDays), "Day count must be at least 1.");
		}

		BaseballGameSimulator engine = simulator ?? new BaseballGameSimulator();
		LeagueCatalog league = catalog ?? LeagueCatalog.Standard;
		int startYear = state.Career.CurrentSeasonYear;
		DateOnly origin = state.Career.CurrentDate;
		int days = 0;
		int games = 0;
		var issues = new List<string>();
		var seasons = new List<SeasonBalanceSnapshot>();
		CareerTickResult? last = null;
		while (days < maxDays)
		{
			CareerTickResult tick = CareerClock.Tick(state, engine, league);
			last = tick;
			days++;
			games += tick.Day.GamesPlayed.Count;
			issues.AddRange(WorldIntegrity.InspectGames(tick.Day.GamesPlayed));
			if (tick.RolledOver)
			{
				int finished = state.Career.CurrentSeasonYear - 1;
				issues.AddRange(WorldIntegrity.InspectAfterSeason(state, finished));
				seasons.Add(LeagueBalanceReport.SnapshotSeason(state, finished));
			}

			if (stop(state, tick))
			{
				break;
			}
		}

		issues.AddRange(WorldIntegrity.Inspect(state));
		return new LeagueSimulationResult
		{
			StartYear = startYear,
			EndYear = state.Career.CurrentSeasonYear,
			Origin = origin,
			CurrentDate = state.Career.CurrentDate,
			DaysAdvanced = days,
			GamesPlayed = games,
			RolledOver = last?.RolledOver == true,
			IntegrityIssues = Dedup(issues),
			Seasons = seasons,
			League = LeagueBalanceReport.SnapshotLeague(state),
		};
	}

	public static LeagueSimulationResult RunSeasons(
		CareerState state,
		int seasons,
		BaseballGameSimulator? simulator = null,
		LeagueCatalog? catalog = null)
	{
		if (seasons < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(seasons), "Season count must be at least 1.");
		}

		int targetYear = state.Career.CurrentSeasonYear + seasons;
		LeagueSimulationResult result = AdvanceUntil(
			state,
			(live, _) => live.Career.CurrentSeasonYear >= targetYear,
			simulator,
			catalog,
			maxDays: seasons * MaxDaysPerSeason);
		if (state.Career.CurrentSeasonYear < targetYear)
		{
			var issues = result.IntegrityIssues.ToList();
			issues.Add(
				$"Stopped after {result.DaysAdvanced} days at {state.Career.CurrentDate:yyyy-MM-dd} "
				+ $"(year {state.Career.CurrentSeasonYear}); expected year {targetYear}.");
			return result with { IntegrityIssues = issues };
		}

		return result with { SeasonsCompleted = seasons };
	}

	private static List<string> Dedup(List<string> issues)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		var unique = new List<string>();
		foreach (string issue in issues)
		{
			if (seen.Add(issue))
			{
				unique.Add(issue);
			}
		}

		return unique;
	}
}
