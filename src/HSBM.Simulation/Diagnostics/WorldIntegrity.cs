using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// Structural checks used by world generation and multi-season runs.
/// A non-empty list means the career is in a broken state.
/// </summary>
public static class WorldIntegrity
{
	public const int MinPlayableRoster = 9;

	public static IReadOnlyList<string> Inspect(CareerState state)
	{
		var issues = new List<string>();
		InspectLeagueShape(state, issues);
		InspectRosters(state, issues);
		InspectPlayers(state, issues);
		InspectSchedule(state, issues);
		InspectStandings(state, state.Career.CurrentSeasonYear, issues);
		return issues;
	}

	public static IReadOnlyList<string> InspectGenerated(GeneratedWorld world)
	{
		DateOnly start = SeasonCalendar.OpenPlayStart(world.SeasonYear);
		return Inspect(CareerWorldRunner.FromGenerated(world, "Integrity", ""));
	}

	public static IReadOnlyList<string> InspectAfterSeason(CareerState state, int seasonYear)
	{
		var issues = new List<string>();
		InspectLeagueShape(state, issues);
		InspectRosters(state, issues);
		InspectPlayers(state, issues);
		InspectStandings(state, seasonYear, issues);
		HistoricalSeason? archive = SeasonArchive.ForYear(state.World, seasonYear);
		if (archive == null)
		{
			issues.Add($"{seasonYear}: missing historical season archive.");
			return issues;
		}

		if (archive.Standings.Count != 32)
		{
			issues.Add($"{seasonYear}: archive standings have {archive.Standings.Count} rows, expected 32.");
		}

		int districtTitles = archive.Champions.Count(row => row.Title.Contains("District", StringComparison.Ordinal));
		if (districtTitles != LeagueCatalog.DistrictCount)
		{
			issues.Add($"{seasonYear}: {districtTitles} district champions, expected {LeagueCatalog.DistrictCount}.");
		}

		if (!archive.Champions.Any(row => row.Title == "State" && !string.IsNullOrWhiteSpace(row.OrganizationId)))
		{
			issues.Add($"{seasonYear}: no state champion was recorded.");
		}

		int unsigned = state.Players.Count(player => player.IsUnsigned && !player.Alumni);
		if (unsigned < LeagueCatalog.DistrictCount * LeagueCatalog.OpenPlayTargetPlayers)
		{
			issues.Add(
				$"{seasonYear}: incoming unsigned class is {unsigned}, "
				+ $"expected at least {LeagueCatalog.DistrictCount * LeagueCatalog.OpenPlayTargetPlayers}.");
		}

		int scheduled = state.Games.Count(game =>
			game.SeasonYear == state.Career.CurrentSeasonYear && game.Status == GameStatus.Scheduled);
		if (scheduled == 0)
		{
			issues.Add($"{state.Career.CurrentSeasonYear}: rollover produced no scheduled games.");
		}

		return issues;
	}

	public static IReadOnlyList<string> InspectGames(IEnumerable<SimulatedGame> games)
	{
		var issues = new List<string>();
		foreach (SimulatedGame game in games)
		{
			if (game.HomeTeamId == game.AwayTeamId)
			{
				issues.Add($"{game.GameId}: team played itself ({game.HomeTeamId}).");
			}

			if (game.HomeScore == game.AwayScore)
			{
				issues.Add($"{game.Date:yyyy-MM-dd} {game.HomeTeamId}: tied {game.HomeScore}-{game.AwayScore}.");
			}

			if (game.InningsPlayed < 9)
			{
				issues.Add($"{game.Date:yyyy-MM-dd} {game.HomeTeamId}: finished in {game.InningsPlayed} innings.");
			}

			if (string.IsNullOrWhiteSpace(game.WinningTeamId))
			{
				issues.Add($"{game.Date:yyyy-MM-dd} {game.HomeTeamId}: missing winner.");
			}
		}

		return issues;
	}

	private static void InspectLeagueShape(CareerState state, List<string> issues)
	{
		int programs = state.Organizations.Count(org => !LeagueCatalog.IsUnsignedOrganization(org.Id));
		if (programs != 32)
		{
			issues.Add($"League has {programs} programs, expected 32.");
		}

		if (state.Districts.Count != LeagueCatalog.DistrictCount)
		{
			issues.Add($"League has {state.Districts.Count} districts, expected {LeagueCatalog.DistrictCount}.");
		}

		foreach (DistrictIdentity district in state.Districts)
		{
			int schools = state.Organizations.Count(org =>
				org.DistrictId == district.Id && !LeagueCatalog.IsUnsignedOrganization(org.Id));
			if (schools != LeagueCatalog.SchoolsPerDistrict)
			{
				issues.Add($"{district.Id}: {schools} schools, expected {LeagueCatalog.SchoolsPerDistrict}.");
			}
		}

		foreach (OrganizationState org in state.Organizations.Where(o => !LeagueCatalog.IsUnsignedOrganization(o.Id)))
		{
			if (state.Teams.All(team => team.OrganizationId != org.Id || team.Level != TeamLevel.Varsity))
			{
				issues.Add($"{org.Id}: missing varsity team.");
			}

			if (state.Teams.All(team => team.OrganizationId != org.Id || team.Level != TeamLevel.JuniorVarsity))
			{
				issues.Add($"{org.Id}: missing JV team.");
			}
		}
	}

	private static void InspectRosters(CareerState state, List<string> issues)
	{
		DateOnly today = state.Career.CurrentDate;
		int year = state.Career.CurrentSeasonYear;
		int programCap = ProgramRosterRules.RegisteredCap(today, year);
		foreach (OrganizationState org in state.Organizations.Where(o => !LeagueCatalog.IsUnsignedOrganization(o.Id)))
		{
			List<Player> registered = state.Players
				.Where(player => player.OrganizationId == org.Id && !player.Alumni)
				.ToList();
			if (registered.Count > programCap)
			{
				issues.Add($"{org.Id}: {registered.Count} registered players exceeds cap {programCap}.");
			}
		}

		foreach (TeamIdentity team in state.Teams.Where(t => t.RosterCap > 0))
		{
			List<Player> roster = state.Players
				.Where(player => player.TeamId == team.Id && !player.Alumni)
				.ToList();
			bool inSeason = today >= SeasonCalendar.SigningDeadline(year)
				&& today <= SeasonCalendar.Graduation(year).Start;
			if (inSeason && roster.Count < MinPlayableRoster)
			{
				issues.Add($"{team.Id}: roster has {roster.Count} players, need {MinPlayableRoster}.");
			}

			bool afterCut = today >= SeasonCalendar.InitialRosterCut(year);
			if (afterCut && roster.Count > team.RosterCap)
			{
				issues.Add($"{team.Id}: roster has {roster.Count} players, cap is {team.RosterCap}.");
			}

			if (roster.Count > 0
				&& roster.Select(player => player.JerseyNumber).Distinct().Count() != roster.Count)
			{
				issues.Add($"{team.Id}: duplicate jersey numbers.");
			}

			if (inSeason && !roster.Any(player => player.PrimaryPosition == PlayerPosition.Catcher))
			{
				issues.Add($"{team.Id}: no catcher.");
			}

			if (inSeason && !roster.Any(player => player.IsPitcher))
			{
				issues.Add($"{team.Id}: no pitcher.");
			}
		}
	}

	private static void InspectPlayers(CareerState state, List<string> issues)
	{
		var ids = new HashSet<Guid>();
		foreach (Player player in state.Players)
		{
			if (player.Id == Guid.Empty)
			{
				issues.Add("Player with empty id.");
			}
			else if (!ids.Add(player.Id))
			{
				issues.Add($"Duplicate player id {player.Id:N}.");
			}

			if (string.IsNullOrWhiteSpace(player.FirstName) || string.IsNullOrWhiteSpace(player.LastName))
			{
				issues.Add($"{player.Id:N}: missing name.");
			}

			if (!player.Alumni
				&& ((int)player.Grade < (int)PlayerGrade.Freshman || (int)player.Grade > (int)PlayerGrade.Senior))
			{
				issues.Add($"{player.FullName}: invalid grade {player.Grade}.");
			}

			if (player.Overall != PlayerOverall.Calculate(player.PrimaryPosition, player.Ratings))
			{
				issues.Add($"{player.FullName}: overall {player.Overall} does not match ratings.");
			}

			if (player.Ratings.Contact < PlayerRatings.Min || player.Ratings.Contact > PlayerRatings.Max
				|| player.Ratings.Stuff < PlayerRatings.Min || player.Ratings.Stuff > PlayerRatings.Max)
			{
				issues.Add($"{player.FullName}: ratings outside {PlayerRatings.Min}-{PlayerRatings.Max}.");
			}

			if (player.Alumni && player.GraduatedYear <= 0)
			{
				issues.Add($"{player.FullName}: alumni missing graduation year.");
			}

			if (player.Alumni && !player.HasCollegeDestination)
			{
				issues.Add($"{player.FullName}: alumni missing college destination.");
			}
		}
	}

	private static void InspectSchedule(CareerState state, List<string> issues)
	{
		foreach (ScheduledGame game in state.Games)
		{
			if (game.HomeTeamId == game.AwayTeamId)
			{
				issues.Add($"Game {game.Id}: team played itself ({game.HomeTeamId}).");
			}

			if (game.Status == GameStatus.Completed && game.HomeScore == game.AwayScore)
			{
				issues.Add($"Game {game.Id}: completed as a tie {game.HomeScore}-{game.AwayScore}.");
			}
		}

		int year = state.Career.CurrentSeasonYear;
		var occupied = new Dictionary<string, HashSet<DateOnly>>(StringComparer.Ordinal);
		foreach (ScheduledGame game in state.Games.Where(g => g.SeasonYear == year && g.Status == GameStatus.Scheduled))
		{
			if (game.Date == default)
			{
				issues.Add($"Game {game.Id}: missing date.");
				continue;
			}

			MarkBusy(occupied, game.HomeTeamId, game.Date, game.Id, issues);
			MarkBusy(occupied, game.AwayTeamId, game.Date, game.Id, issues);
		}
	}

	private static void MarkBusy(
		Dictionary<string, HashSet<DateOnly>> occupied,
		string teamId,
		DateOnly date,
		Guid gameId,
		List<string> issues)
	{
		if (!occupied.TryGetValue(teamId, out HashSet<DateOnly>? days))
		{
			days = [];
			occupied[teamId] = days;
		}

		if (!days.Add(date))
		{
			issues.Add($"{teamId}: two scheduled games on {date:yyyy-MM-dd} (game {gameId}).");
		}
	}

	private static void InspectStandings(CareerState state, int seasonYear, List<string> issues)
	{
		foreach (TeamIdentity team in state.Teams.Where(t => t.RosterCap > 0 && t.Level == TeamLevel.Varsity))
		{
			if (LeagueCatalog.IsUnsignedOrganization(team.OrganizationId))
			{
				continue;
			}

			int wins = 0;
			int losses = 0;
			int districtWins = 0;
			int districtLosses = 0;
			int runsFor = 0;
			int runsAgainst = 0;
			foreach (ScheduledGame game in state.Games)
			{
				if (game.SeasonYear != seasonYear
					|| game.Status != GameStatus.Completed
					|| !game.GameType.CountsInStandings()
					|| !game.Involves(team.Id))
				{
					continue;
				}

				bool home = game.HomeTeamId == team.Id;
				int scored = home ? game.HomeScore ?? 0 : game.AwayScore ?? 0;
				int allowed = home ? game.AwayScore ?? 0 : game.HomeScore ?? 0;
				runsFor += scored;
				runsAgainst += allowed;
				bool won = game.WinningTeamId == team.Id;
				if (won)
				{
					wins++;
					if (game.GameType == GameType.District)
					{
						districtWins++;
					}
				}
				else
				{
					losses++;
					if (game.GameType == GameType.District)
					{
						districtLosses++;
					}
				}
			}

			if (wins + losses == 0)
			{
				continue;
			}

			TeamSeasonRecord? record = state.TeamRecords.FirstOrDefault(row =>
				row.TeamId == team.Id && row.SeasonYear == seasonYear);
			if (record == null)
			{
				issues.Add($"{team.Id}: missing {seasonYear} season record after {wins + losses} games.");
				continue;
			}

			if (record.Wins != wins || record.Losses != losses)
			{
				issues.Add($"{team.Id} {seasonYear}: record {record.Wins}-{record.Losses} != results {wins}-{losses}.");
			}

			if (record.DistrictWins != districtWins || record.DistrictLosses != districtLosses)
			{
				issues.Add(
					$"{team.Id} {seasonYear}: district {record.DistrictWins}-{record.DistrictLosses} "
					+ $"!= results {districtWins}-{districtLosses}.");
			}

			if (record.RunsFor != runsFor || record.RunsAgainst != runsAgainst)
			{
				issues.Add(
					$"{team.Id} {seasonYear}: scoring {record.RunsFor}-{record.RunsAgainst} "
					+ $"!= results {runsFor}-{runsAgainst}.");
			}
		}
	}
}
