using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// Seeds the regional field from standings and schedules best-of-three
/// series through state. Playoff games do not count in the regular
/// standings.
/// </summary>
public static class PostseasonDesk
{
	public static void Tick(CareerState state, DateOnly today)
	{
		int year = state.Career.CurrentSeasonYear;
		if (today < SeasonCalendar.PlayoffSelection(year).Start
			&& today < SeasonCalendar.RegularSeasonEnd(year))
		{
			return;
		}

		EnsureBracket(state, today);
		ScheduleDueGames(state, today);
	}

	public static void Resolve(CareerState state, DateOnly today)
	{
		PostseasonState? bracket = state.World.Postseason;
		if (bracket == null || bracket.SeasonYear != state.Career.CurrentSeasonYear)
		{
			return;
		}

		foreach (PostseasonSeries series in bracket.Series)
		{
			ApplySeriesGames(state, series);
		}

		AdvanceRounds(state, bracket);
		if (today >= SeasonCalendar.RegularSeasonEnd(state.Career.CurrentSeasonYear))
		{
			SeasonArchive.WriteHistory(state);
		}

		FreezeAwards(state, today);
	}

	public static bool UserHasGameToday(CareerState state, string? userTeamId, DateOnly today)
	{
		if (string.IsNullOrWhiteSpace(userTeamId))
		{
			return false;
		}

		foreach (ScheduledGame game in state.Games)
		{
			if (game.Date == today
				&& game.Status == GameStatus.Scheduled
				&& game.GameType.IsPostseason()
				&& game.TeamLevel == TeamLevel.Varsity
				&& game.Involves(userTeamId))
			{
				return true;
			}
		}

		return false;
	}

	public static PostseasonState EnsureBracket(CareerState state, DateOnly today)
	{
		int year = state.Career.CurrentSeasonYear;
		PostseasonState? existing = state.World.Postseason;
		if (existing != null && existing.SeasonYear == year && existing.Qualifiers.Count > 0)
		{
			return existing;
		}

		if (today < SeasonCalendar.PlayoffSelection(year).Start)
		{
			return existing ?? new PostseasonState { SeasonYear = year };
		}

		ISeededRandom rng = new SeededRandom(
			SeededRandom.Mix(state.Career.RandomSeed, year ^ unchecked((int)0x51ED)));
		PostseasonState seeded = PostseasonField.Seed(
			year,
			state.Districts,
			LeagueStandings.FromWorld(state.World, year),
			rng);
		state.World.Postseason = seeded;
		return seeded;
	}

	private static void ScheduleDueGames(CareerState state, DateOnly today)
	{
		PostseasonState? bracket = state.World.Postseason;
		if (bracket == null || bracket.Qualifiers.Count == 0)
		{
			return;
		}

		int year = state.Career.CurrentSeasonYear;
		foreach (PostseasonSeries series in bracket.Series.ToList())
		{
			if (series.IsComplete)
			{
				continue;
			}

			SeasonEvent window = WindowFor(year, series.Round);
			if (today < window.Start || today > window.End)
			{
				continue;
			}

			if (HasOpenGame(state, series))
			{
				continue;
			}

			if (series.GamesPlayed >= 3)
			{
				continue;
			}

			ScheduleNext(state, series, today);
		}
	}

	private static void ScheduleNext(CareerState state, PostseasonSeries series, DateOnly today)
	{
		int gameNumber = series.GameIds.Count;
		bool homeHolds = gameNumber != 1;
		string home = homeHolds ? series.HomeTeamId : series.AwayTeamId;
		string away = homeHolds ? series.AwayTeamId : series.HomeTeamId;
		DateOnly date = NextOpenDate(state, home, away, today, WindowFor(state.Career.CurrentSeasonYear, series.Round));
		ISeededRandom rng = new SeededRandom(
			SeededRandom.Mix(state.Career.RandomSeed, series.Id.GetHashCode() ^ (gameNumber * 917)));
		var game = new ScheduledGame
		{
			Id = rng.NextGuid(),
			SeasonYear = state.Career.CurrentSeasonYear,
			Date = date,
			HomeTeamId = home,
			AwayTeamId = away,
			TeamLevel = TeamLevel.Varsity,
			GameType = series.Round == PostseasonRound.StateChampionship
				? GameType.StateTournament
				: GameType.RegionalTournament,
			Status = GameStatus.Scheduled,
		};
		state.Games.Add(game);
		series.GameIds.Add(game.Id);
	}

	private static DateOnly NextOpenDate(
		CareerState state,
		string homeTeamId,
		string awayTeamId,
		DateOnly today,
		SeasonEvent window)
	{
		DateOnly cursor = today < window.Start ? window.Start : today;
		while (cursor <= window.End)
		{
			if (!TeamBusy(state, homeTeamId, cursor) && !TeamBusy(state, awayTeamId, cursor))
			{
				return cursor;
			}

			cursor = cursor.AddDays(1);
		}

		return today;
	}

	private static bool TeamBusy(CareerState state, string teamId, DateOnly date)
	{
		foreach (ScheduledGame game in state.Games)
		{
			if (game.Date == date
				&& game.Status == GameStatus.Scheduled
				&& game.TeamLevel == TeamLevel.Varsity
				&& game.Involves(teamId))
			{
				return true;
			}
		}

		return false;
	}

	private static bool HasOpenGame(CareerState state, PostseasonSeries series)
	{
		foreach (Guid gameId in series.GameIds)
		{
			foreach (ScheduledGame game in state.Games)
			{
				if (game.Id == gameId && game.Status == GameStatus.Scheduled)
				{
					return true;
				}
			}
		}

		return false;
	}

	private static void ApplySeriesGames(CareerState state, PostseasonSeries series)
	{
		int homeWins = 0;
		int awayWins = 0;
		foreach (Guid gameId in series.GameIds)
		{
			ScheduledGame? game = FindGame(state, gameId);
			if (game == null || game.Status != GameStatus.Completed || string.IsNullOrEmpty(game.WinningTeamId))
			{
				continue;
			}

			if (game.WinningTeamId == series.HomeTeamId)
			{
				homeWins++;
			}
			else if (game.WinningTeamId == series.AwayTeamId)
			{
				awayWins++;
			}
		}

		series.HomeWins = homeWins;
		series.AwayWins = awayWins;
		if (homeWins >= PostseasonSeries.WinsNeeded)
		{
			series.WinnerTeamId = series.HomeTeamId;
		}
		else if (awayWins >= PostseasonSeries.WinsNeeded)
		{
			series.WinnerTeamId = series.AwayTeamId;
		}
	}

	private static ScheduledGame? FindGame(CareerState state, Guid id)
	{
		foreach (ScheduledGame game in state.Games)
		{
			if (game.Id == id)
			{
				return game;
			}
		}

		return null;
	}

	private static void AdvanceRounds(CareerState state, PostseasonState bracket)
	{
		ISeededRandom rng = new SeededRandom(
			SeededRandom.Mix(state.Career.RandomSeed, bracket.SeasonYear ^ unchecked((int)0xF1A1)));
		IEnumerable<string> regions = Regions(bracket).ToList();
		foreach (string region in regions)
		{
			IReadOnlyList<PostseasonSeries> semis = bracket.SeriesIn(PostseasonRound.RegionalSemifinal, region);
			if (semis.Count < 2 || semis.Any(series => !series.IsComplete))
			{
				continue;
			}

			if (bracket.SeriesIn(PostseasonRound.RegionalFinal, region).Count > 0)
			{
				continue;
			}

			string home = semis[0].WinnerTeamId ?? "";
			string away = semis[1].WinnerTeamId ?? "";
			if (string.IsNullOrEmpty(home) || string.IsNullOrEmpty(away))
			{
				continue;
			}

			bracket.Series.Add(PostseasonField.CreateSeries(
				PostseasonRound.RegionalFinal,
				region,
				home,
				away,
				rng.Derive(region.GetHashCode())));
		}

		foreach (PostseasonSeries final in bracket.SeriesIn(PostseasonRound.RegionalFinal))
		{
			if (!final.IsComplete || string.IsNullOrEmpty(final.WinnerTeamId))
			{
				continue;
			}

			if (string.Equals(final.Region, "West", StringComparison.OrdinalIgnoreCase))
			{
				bracket.WestChampionTeamId = final.WinnerTeamId;
			}
			else
			{
				bracket.EastChampionTeamId = final.WinnerTeamId;
			}
		}

		if (!string.IsNullOrEmpty(bracket.EastChampionTeamId)
			&& !string.IsNullOrEmpty(bracket.WestChampionTeamId)
			&& bracket.SeriesIn(PostseasonRound.StateChampionship).Count == 0)
		{
			bracket.Series.Add(PostseasonField.CreateSeries(
				PostseasonRound.StateChampionship,
				"State",
				bracket.EastChampionTeamId,
				bracket.WestChampionTeamId,
				rng.Derive(31)));
		}

		foreach (PostseasonSeries stateSeries in bracket.SeriesIn(PostseasonRound.StateChampionship))
		{
			if (stateSeries.IsComplete)
			{
				bracket.StateChampionTeamId = stateSeries.WinnerTeamId;
			}
		}
	}

	private static IEnumerable<string> Regions(PostseasonState bracket)
	{
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (PostseasonSeries series in bracket.Series)
		{
			if (series.Round == PostseasonRound.RegionalSemifinal && seen.Add(series.Region))
			{
				yield return series.Region;
			}
		}
	}

	private static SeasonEvent WindowFor(int year, PostseasonRound round) => round switch
	{
		PostseasonRound.RegionalFinal => SeasonCalendar.RegionalFinal(year),
		PostseasonRound.StateChampionship => SeasonCalendar.StateChampionship(year),
		_ => SeasonCalendar.RegionalSemis(year),
	};

	private static void FreezeAwards(CareerState state, DateOnly today)
	{
		int year = state.Career.CurrentSeasonYear;
		string userOrg = state.Career.UserOrganizationId;
		string coachName = state.Career.DisplayName;
		if (SeasonCalendar.DistrictAwards(year).OccursOn(today)
			&& AwardsEngine.Find(state.World.Awards, year, AwardScope.District) == null)
		{
			AwardsEngine.Upsert(
				state.World.Awards,
				AwardsEngine.Compile(state.World, AwardScope.District, year, userOrg, coachName, official: true));
		}

		if (SeasonCalendar.StateAwards(year).OccursOn(today)
			&& AwardsEngine.Find(state.World.Awards, year, AwardScope.State) == null)
		{
			AwardsEngine.Upsert(
				state.World.Awards,
				AwardsEngine.Compile(state.World, AwardScope.State, year, userOrg, coachName, official: true));
		}
	}
}
