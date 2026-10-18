namespace HSBM.Domain;

/// <summary>
/// Authoritative career calendar: season phase, generated important dates,
/// and the live game list stored on the world.
/// </summary>
public static class CareerCalendar
{
	public static SeasonPhase PhaseOn(int seasonYear, DateOnly date)
	{
		if (date == SeasonCalendar.BaseballYearStart(seasonYear)
			|| date == SeasonCalendar.BaseballYearStart(seasonYear + 1))
		{
			return SeasonPhase.SeasonRollover;
		}

		if (SeasonCalendar.WinterWorkouts(seasonYear).OccursOn(date))
		{
			return SeasonPhase.WinterWorkouts;
		}

		if (SeasonCalendar.TryoutWeek(seasonYear).OccursOn(date)
			|| date == SeasonCalendar.TryoutDecisionDay(seasonYear))
		{
			return SeasonPhase.Tryouts;
		}

		if (SeasonCalendar.FinalRosterWeek(seasonYear).OccursOn(date)
			|| SeasonCalendar.Preseason(seasonYear).OccursOn(date))
		{
			return SeasonPhase.Preseason;
		}

		if (date > SeasonCalendar.RegularSeasonEnd(seasonYear)
			&& date <= SeasonCalendar.StateChampionship(seasonYear).End)
		{
			return SeasonPhase.Postseason;
		}

		if (date >= SeasonCalendar.OpeningDay(seasonYear)
			&& date <= SeasonCalendar.RegularSeasonEnd(seasonYear))
		{
			return SeasonPhase.RegularSeason;
		}

		return SeasonPhase.Offseason;
	}

	public static IReadOnlyList<SeasonEvent> ImportantDates(int seasonYear)
	{
		var dates = new List<SeasonEvent>();
		foreach (SeasonEvent item in SeasonCalendar.ForSeason(seasonYear))
		{
			if (item.Featured || item.Kind == SeasonEventKind.Milestone)
			{
				dates.Add(item);
			}
		}

		return dates;
	}

	public static SeasonEvent? NextImportant(int seasonYear, DateOnly today)
	{
		SeasonEvent? next = null;
		foreach (SeasonEvent item in ImportantDates(seasonYear))
		{
			if (item.End < today)
			{
				continue;
			}

			if (next == null || item.Start < next.Start || (item.Start == next.Start && item.Featured && !next.Featured))
			{
				next = item;
			}
		}

		return next;
	}

	public static bool IsSimStop(int seasonYear, DateOnly date)
	{
		foreach (SeasonEvent item in SeasonCalendar.On(seasonYear, date))
		{
			if (item.Start != date)
			{
				continue;
			}

			if (item.Kind == SeasonEventKind.Milestone || item.Featured)
			{
				return true;
			}
		}

		return false;
	}

	public static bool HasScheduledGame(GameWorld world, string teamId, DateOnly date)
	{
		foreach (ScheduledGame game in world.Games)
		{
			if (game.Date == date
				&& game.Status == GameStatus.Scheduled
				&& game.Involves(teamId))
			{
				return true;
			}
		}

		return false;
	}

	public static IReadOnlyList<ScheduledGame> GamesOn(GameWorld world, DateOnly date, string? teamId = null)
	{
		var matches = new List<ScheduledGame>();
		foreach (ScheduledGame game in world.Games)
		{
			if (game.Date != date)
			{
				continue;
			}

			if (!string.IsNullOrWhiteSpace(teamId) && !game.Involves(teamId))
			{
				continue;
			}

			matches.Add(game);
		}

		matches.Sort((a, b) =>
		{
			int level = a.TeamLevel.CompareTo(b.TeamLevel);
			return level != 0 ? level : string.CompareOrdinal(a.HomeTeamId, b.HomeTeamId);
		});
		return matches;
	}

	public static IReadOnlyList<ScheduledGame> GamesInMonth(
		GameWorld world,
		int year,
		int month,
		string? teamId = null)
	{
		var matches = new List<ScheduledGame>();
		foreach (ScheduledGame game in world.Games)
		{
			if (game.Date.Year != year || game.Date.Month != month)
			{
				continue;
			}

			if (!string.IsNullOrWhiteSpace(teamId) && !game.Involves(teamId))
			{
				continue;
			}

			matches.Add(game);
		}

		matches.Sort((a, b) => a.Date.CompareTo(b.Date));
		return matches;
	}

	public static IReadOnlyList<ScheduledGame> SeasonGames(GameWorld world, string teamId)
	{
		var matches = new List<ScheduledGame>();
		foreach (ScheduledGame game in world.Games)
		{
			if (game.Involves(teamId))
			{
				matches.Add(game);
			}
		}

		matches.Sort((a, b) =>
		{
			int date = a.Date.CompareTo(b.Date);
			return date != 0 ? date : a.TeamLevel.CompareTo(b.TeamLevel);
		});
		return matches;
	}

	public static DateOnly? NextEventOrGame(
		GameWorld world,
		int seasonYear,
		DateOnly today,
		string? teamId)
	{
		DateOnly? nextGame = null;
		if (!string.IsNullOrWhiteSpace(teamId))
		{
			foreach (ScheduledGame game in world.Games)
			{
				if (game.Status != GameStatus.Scheduled
					|| !game.Involves(teamId)
					|| game.Date < today)
				{
					continue;
				}

				if (nextGame == null || game.Date < nextGame.Value)
				{
					nextGame = game.Date;
				}
			}
		}

		DateOnly? nextMark = null;
		foreach (SeasonEvent item in ImportantDates(seasonYear))
		{
			if (item.Start < today)
			{
				continue;
			}

			if (item.Start == today && !IsSimStop(seasonYear, today))
			{
				continue;
			}

			if (nextMark == null || item.Start < nextMark.Value)
			{
				nextMark = item.Start;
			}
		}

		if (nextGame == null)
		{
			return nextMark;
		}

		if (nextMark == null)
		{
			return nextGame;
		}

		return nextGame.Value <= nextMark.Value ? nextGame : nextMark;
	}

	public static string GameTime(DateOnly date, TeamLevel level)
	{
		bool saturday = date.DayOfWeek == DayOfWeek.Saturday;
		if (level == TeamLevel.JuniorVarsity)
		{
			return saturday ? "11:00 AM" : "4:00 PM";
		}

		return saturday ? "1:00 PM" : "7:00 PM";
	}

	public static string OrganizationIdForTeam(GameWorld world, string teamId)
	{
		foreach (TeamIdentity team in world.Teams)
		{
			if (team.Id == teamId)
			{
				return team.OrganizationId;
			}
		}

		int dash = teamId.LastIndexOf('-');
		return dash > 0 ? teamId[..dash] : teamId;
	}

	public static bool IsRolloverDay(int seasonYear, DateOnly date) =>
		date == SeasonCalendar.BaseballYearStart(seasonYear + 1);
}
