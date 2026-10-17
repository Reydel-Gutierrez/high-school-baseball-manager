using HSBM.Domain;

namespace HSBM.Simulation;

public static class ScheduleGenerator
{
	public const int NonDistrictGamesPerTeam = 8;
	public const int ExhibitionGamesPerTeam = 2;

	public static List<ScheduledGame> Generate(
		LeagueCatalog catalog,
		IReadOnlyList<TeamIdentity> teams,
		int seasonYear,
		ISeededRandom rng)
	{
		return Generate(catalog, teams, seasonYear, catalog.OpeningDay(seasonYear), rng);
	}

	public static List<ScheduledGame> Generate(
		LeagueCatalog catalog,
		IReadOnlyList<TeamIdentity> teams,
		int seasonYear,
		DateOnly openingDay,
		ISeededRandom rng)
	{
		_ = openingDay;
		var games = new List<ScheduledGame>();
		foreach (TeamLevel level in new[] { TeamLevel.Varsity, TeamLevel.JuniorVarsity })
		{
			List<TeamIdentity> levelTeams = teams.Where(t => t.Level == level && t.RosterCap > 0).ToList();
			AddExhibitionGames(games, catalog, levelTeams, seasonYear, rng);
			AddDistrictGames(games, catalog, levelTeams, seasonYear, rng);
			AddNonDistrictGames(games, catalog, levelTeams, seasonYear, rng);
		}

		AssignDates(games, teams, seasonYear, rng);
		return games;
	}

	private static void AddDistrictGames(
		List<ScheduledGame> games,
		LeagueCatalog catalog,
		List<TeamIdentity> levelTeams,
		int seasonYear,
		ISeededRandom rng)
	{
		var byDistrict = levelTeams
			.GroupBy(t => catalog.DistrictIdFor(t.OrganizationId))
			.ToList();

		foreach (var group in byDistrict)
		{
			List<TeamIdentity> districtTeams = group.ToList();
			for (int i = 0; i < districtTeams.Count; i++)
			{
				for (int j = i + 1; j < districtTeams.Count; j++)
				{
					AddPair(games, districtTeams[i], districtTeams[j], seasonYear, GameType.District, rng, homeAndAway: true);
				}
			}
		}
	}

	private static void AddNonDistrictGames(
		List<ScheduledGame> games,
		LeagueCatalog catalog,
		List<TeamIdentity> levelTeams,
		int seasonYear,
		ISeededRandom rng)
	{
		AddLimitedPairs(games, catalog, levelTeams, seasonYear, GameType.NonDistrict, NonDistrictGamesPerTeam, rng, sameDistrict: false);
	}

	private static void AddExhibitionGames(
		List<ScheduledGame> games,
		LeagueCatalog catalog,
		List<TeamIdentity> levelTeams,
		int seasonYear,
		ISeededRandom rng)
	{
		AddLimitedPairs(games, catalog, levelTeams, seasonYear, GameType.Exhibition, ExhibitionGamesPerTeam, rng, sameDistrict: false);
	}

	private static void AddLimitedPairs(
		List<ScheduledGame> games,
		LeagueCatalog catalog,
		List<TeamIdentity> levelTeams,
		int seasonYear,
		GameType type,
		int perTeam,
		ISeededRandom rng,
		bool sameDistrict)
	{
		var remaining = levelTeams.ToDictionary(t => t.Id, _ => perTeam);
		var played = new HashSet<string>(StringComparer.Ordinal);

		var shuffled = levelTeams.ToList();
		rng.Shuffle(shuffled);

		int guard = 0;
		while (remaining.Values.Any(v => v > 0) && guard++ < 20000)
		{
			TeamIdentity? needy = shuffled.FirstOrDefault(t => remaining[t.Id] > 0);
			if (needy == null)
			{
				break;
			}

			var candidates = shuffled
				.Where(other =>
					other.Id != needy.Id
					&& remaining[other.Id] > 0
					&& catalog.SameDistrict(needy.OrganizationId, other.OrganizationId) == sameDistrict
					&& !played.Contains(PairKey(needy.Id, other.Id)))
				.ToList();

			if (candidates.Count == 0)
			{
				remaining[needy.Id] = 0;
				continue;
			}

			TeamIdentity opponent = rng.Pick(candidates);
			AddPair(games, needy, opponent, seasonYear, type, rng, homeAndAway: false);
			played.Add(PairKey(needy.Id, opponent.Id));
			remaining[needy.Id]--;
			remaining[opponent.Id]--;
		}
	}

	private static void AddPair(
		List<ScheduledGame> games,
		TeamIdentity a,
		TeamIdentity b,
		int seasonYear,
		GameType type,
		ISeededRandom rng,
		bool homeAndAway)
	{
		bool aHomeFirst = rng.NextDouble() < 0.5;
		games.Add(CreateGame(aHomeFirst ? a : b, aHomeFirst ? b : a, seasonYear, type, rng));
		if (homeAndAway)
		{
			games.Add(CreateGame(aHomeFirst ? b : a, aHomeFirst ? a : b, seasonYear, type, rng));
		}
	}

	private static ScheduledGame CreateGame(
		TeamIdentity home,
		TeamIdentity away,
		int seasonYear,
		GameType type,
		ISeededRandom rng) =>
		new()
		{
			Id = rng.NextGuid(),
			SeasonYear = seasonYear,
			Date = default,
			HomeTeamId = home.Id,
			AwayTeamId = away.Id,
			TeamLevel = home.Level,
			GameType = type,
			Status = GameStatus.Scheduled,
		};

	private static void AssignDates(
		List<ScheduledGame> games,
		IReadOnlyList<TeamIdentity> teams,
		int seasonYear,
		ISeededRandom rng)
	{
		SeasonEvent preseason = SeasonCalendar.Preseason(seasonYear);
		SeasonEvent nonDistrict = SeasonCalendar.NonDistrict(seasonYear);
		DateOnly districtStart = SeasonCalendar.ById(seasonYear, "district-play").Start;
		DateOnly districtEnd = SeasonCalendar.RegularSeasonEnd(seasonYear);

		List<DateOnly> exhibitionDays = BuildCalendar(preseason.Start, preseason.End);
		List<DateOnly> nonDistrictDays = BuildCalendar(nonDistrict.Start, nonDistrict.End);
		List<DateOnly> districtDays = BuildCalendar(districtStart, districtEnd);

		var occupied = new Dictionary<string, HashSet<DateOnly>>(StringComparer.Ordinal);
		foreach (TeamIdentity team in teams)
		{
			occupied[team.Id] = [];
		}

		Place(games.Where(g => g.GameType == GameType.Exhibition).ToList(), exhibitionDays, occupied, rng);
		Place(games.Where(g => g.GameType == GameType.NonDistrict).ToList(), nonDistrictDays, occupied, rng);
		Place(games.Where(g => g.GameType == GameType.District).ToList(), districtDays, occupied, rng);

		games.Sort((a, b) =>
		{
			int date = a.Date.CompareTo(b.Date);
			if (date != 0)
			{
				return date;
			}

			int level = a.TeamLevel.CompareTo(b.TeamLevel);
			return level != 0 ? level : string.CompareOrdinal(a.HomeTeamId, b.HomeTeamId);
		});
	}

	private static void Place(
		List<ScheduledGame> games,
		List<DateOnly> calendar,
		Dictionary<string, HashSet<DateOnly>> occupied,
		ISeededRandom rng)
	{
		rng.Shuffle(games);
		foreach (ScheduledGame game in games)
		{
			DateOnly? chosen = null;
			foreach (DateOnly day in calendar)
			{
				if (!occupied.TryGetValue(game.HomeTeamId, out HashSet<DateOnly>? homeBusy))
				{
					homeBusy = [];
					occupied[game.HomeTeamId] = homeBusy;
				}

				if (!occupied.TryGetValue(game.AwayTeamId, out HashSet<DateOnly>? awayBusy))
				{
					awayBusy = [];
					occupied[game.AwayTeamId] = awayBusy;
				}

				if (!homeBusy.Contains(day) && !awayBusy.Contains(day))
				{
					chosen = day;
					break;
				}
			}

			if (chosen == null)
			{
				DateOnly extra = calendar.Count == 0
					? SeasonCalendar.OpeningDay(game.SeasonYear)
					: calendar[^1].AddDays(1);
				while (extra.DayOfWeek is DayOfWeek.Sunday)
				{
					extra = extra.AddDays(1);
				}

				calendar.Add(extra);
				chosen = extra;
			}

			game.Date = chosen.Value;
			occupied[game.HomeTeamId].Add(chosen.Value);
			occupied[game.AwayTeamId].Add(chosen.Value);
		}
	}

	private static List<DateOnly> BuildCalendar(DateOnly start, DateOnly end)
	{
		var days = new List<DateOnly>();
		DateOnly cursor = start;
		while (cursor <= end)
		{
			if (cursor.DayOfWeek is DayOfWeek.Monday
				or DayOfWeek.Tuesday
				or DayOfWeek.Thursday
				or DayOfWeek.Friday
				or DayOfWeek.Saturday)
			{
				days.Add(cursor);
			}

			cursor = cursor.AddDays(1);
		}

		return days;
	}

	private static string PairKey(string a, string b) =>
		string.CompareOrdinal(a, b) < 0 ? $"{a}|{b}" : $"{b}|{a}";
}
