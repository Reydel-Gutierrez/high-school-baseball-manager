using System;
using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// Public face of every District Hub program. Hub screens still use
/// prototype standings and squads; this turns those 32 rows into a
/// school you can open, scout, and remember.
/// </summary>
public static class SchoolProfiles
{
	public static string NormalizeId(string? teamId)
	{
		if (string.IsNullOrWhiteSpace(teamId))
		{
			return DistrictHubData.UserTeamId;
		}

		return teamId.Trim().ToLowerInvariant() switch
		{
			"lakes" or "great-lakes" or "greatlakes" => "great-lakes",
			"harbor" or "harbor-ridge" => "harbor-ridge",
			"delaware" or "delaware-valley" => "delaware-valley",
			_ => teamId.Trim(),
		};
	}

	public static SchoolSnapshot For(string teamId, TeamLevel level, GameSession? session = null)
	{
		string id = NormalizeId(teamId);
		HubTeam team = DistrictHubData.GetTeam(id);
		HubDistrict district = DistrictHubData.GetDistrict(team.DistrictId);
		List<HubStandingRow> standings = DistrictHubData.StandingsFor(team.DistrictId, level);
		int place = 4;
		foreach (HubStandingRow candidate in standings)
		{
			if (candidate.Team.Id == team.Id)
			{
				place = candidate.Rank;
				break;
			}
		}

		int wins = team.Wins(level);
		int losses = team.Losses(level);
		int seed = StableSeed(team.Id);
		int reputation = ReputationFor(team, level, place);
		LetterGrade facilities = LetterGradeExtensions.FromScore(reputation + ((seed % 7) - 3));
		List<HubSquadPlayer> roster = DistrictHubSquads.Squad(team.Id, level);
		(int pitching, int hitting, int fielding) = Strengths(roster, team);
		string board = team.IsUserTeam && session != null
			? BoardGoals.ScoreLine(session.BoardSatisfactionScore)
			: BoardLabel(place, wins, losses);

		int baseballBudget = 0;
		int sponsorships = 0;
		int budgetLeft = 0;
		int recruitingLeft = 0;
		int coachSalary = 0;
		OrganizationState? program = session?.LeagueProgram(id) ?? session?.RecruitingSchool(id);
		if (program != null)
		{
			baseballBudget = program.Profile.BaseballBudget;
			if (session != null)
			{
				bool userProgram = string.Equals(program.Id, session.CatalogOrganizationId, StringComparison.Ordinal)
					|| team.IsUserTeam;
				if (userProgram)
				{
					coachSalary = session.AnnualSalary;
				}

				sponsorships = ProgramBudget.CommittedSponsorship(session.RecruitingMarket.Agreements, program.Id);
				budgetLeft = ProgramBudget.Available(
					program.Profile,
					session.RecruitingMarket.Agreements,
					program.Id,
					coachSalary);
				recruitingLeft = ProgramBudget.RecruitingAvailable(
					program.Profile,
					session.RecruitingMarket.Agreements,
					program.Id,
					session.RecruitingMarket.Offers);
			}
		}

		int stateTitles = program?.Profile.StateTitles ?? Titles(reputation, seed, 0, 3);
		int regionalTitles = program?.Profile.RegionalTitles ?? Titles(reputation, seed, 1, 7);
		int districtTitles = program?.Profile.DistrictTitles ?? Titles(reputation, seed, 2, 12);

		return new SchoolSnapshot(
			team.Id,
			FullName(team),
			team.Name,
			team.ShortName,
			district.Id,
			district.Name,
			wins,
			losses,
			place,
			team.Streak(level),
			FormFromStreak(team.Streak(level), seed),
			program?.Profile.Reputation ?? reputation,
			program?.Profile.Facilities ?? facilities,
			board,
			pitching,
			hitting,
			fielding,
			stateTitles,
			regionalTitles,
			districtTitles,
			roster,
			BuildSchedule(team, level, seed),
			BuildHistory(team, reputation, seed, program),
			BuildRecords(team, roster, reputation, seed, program),
			baseballBudget,
			sponsorships,
			budgetLeft,
			recruitingLeft,
			coachSalary);
	}

	public static List<HubTeam> FindMentioned(string text)
	{
		var found = new List<HubTeam>();
		if (string.IsNullOrWhiteSpace(text))
		{
			return found;
		}

		foreach (HubTeam team in DistrictHubData.Teams)
		{
			if (ContainsName(text, team.Name) || ContainsName(text, team.ShortName))
			{
				found.Add(team);
			}
		}

		return found;
	}

	private static string FullName(HubTeam team)
	{
		foreach (OrganizationIdentity org in LeagueCatalog.Standard.Organizations)
		{
			if (org.Id == team.Id)
			{
				return org.Name;
			}
		}

		if (team.Name.Contains("High School", StringComparison.OrdinalIgnoreCase)
			|| team.Name.Contains("Academy", StringComparison.OrdinalIgnoreCase)
			|| team.Name.Contains("Prep", StringComparison.OrdinalIgnoreCase))
		{
			return team.Name;
		}

		return $"{team.Name} High School";
	}

	private static int ReputationFor(HubTeam team, TeamLevel level, int place)
	{
		int games = Math.Max(1, team.Wins(level) + team.Losses(level));
		double winPct = team.Wins(level) / (double)games;
		int raw = (int)Math.Round(38 + (winPct * 48) + ((9 - place) * 2.2), MidpointRounding.AwayFromZero);
		return Math.Clamp(raw, 28, 94);
	}

	private static (int Pitching, int Hitting, int Fielding) Strengths(List<HubSquadPlayer> roster, HubTeam team)
	{
		int pitchSum = 0;
		int pitchCount = 0;
		int hitSum = 0;
		int hitCount = 0;
		int fieldSum = 0;
		int fieldCount = 0;
		foreach (HubSquadPlayer player in roster)
		{
			if (IsPitcher(player.Position))
			{
				pitchSum += player.Overall;
				pitchCount++;
			}
			else
			{
				hitSum += (player.Contact + player.Power + player.Overall) / 3;
				hitCount++;
				fieldSum += (player.Field + player.Arm) / 2;
				fieldCount++;
			}
		}

		int pitching = pitchCount == 0
			? GradeFromEra(team.TeamEra)
			: Math.Clamp(pitchSum / pitchCount, 48, 94);
		int hitting = hitCount == 0
			? GradeFromAvg(team.TeamAvg)
			: Math.Clamp(hitSum / hitCount, 48, 94);
		int fielding = fieldCount == 0
			? Math.Clamp(72 + (team.RunsFor - team.RunsAgainst) / 8, 48, 92)
			: Math.Clamp(fieldSum / fieldCount, 48, 94);
		return (pitching, hitting, fielding);
	}

	private static bool IsPitcher(string position) =>
		position.Contains("HP", StringComparison.OrdinalIgnoreCase)
		|| position is "P" or "SP" or "RP";

	private static int GradeFromEra(float era) =>
		Math.Clamp((int)Math.Round(92 - ((era - 2.1f) * 12f), MidpointRounding.AwayFromZero), 48, 94);

	private static int GradeFromAvg(float avg) =>
		Math.Clamp((int)Math.Round((avg * 210f) + 18f, MidpointRounding.AwayFromZero), 48, 94);

	private static string BoardLabel(int place, int wins, int losses)
	{
		if (place == 1 && wins >= losses)
		{
			return "Confident";
		}

		if (place <= 2)
		{
			return "Stable";
		}

		if (place <= 4)
		{
			return "Watchful";
		}

		return wins + 3 < losses ? "Pressing" : "Uneasy";
	}

	private static string FormFromStreak(string streak, int seed)
	{
		char mark = streak.Length > 0 && char.ToUpperInvariant(streak[0]) == 'L' ? 'L' : 'W';
		int count = 1;
		if (streak.Length > 1 && int.TryParse(streak[1..], out int parsed))
		{
			count = Math.Clamp(parsed, 1, 8);
		}

		var letters = new List<char>();
		for (int i = 0; i < Math.Min(5, count); i++)
		{
			letters.Add(mark);
		}

		while (letters.Count < 5)
		{
			letters.Insert(0, (seed + letters.Count) % 3 == 0 ? (mark == 'W' ? 'L' : 'W') : mark);
		}

		return string.Join("  ", letters);
	}

	private static int Titles(int reputation, int seed, int lane, int max)
	{
		int floor = reputation >= 80 ? 1 : 0;
		int span = Math.Max(1, max - floor);
		return floor + Math.Abs(seed / (11 + lane * 17)) % span;
	}

	private static List<SchoolGame> BuildSchedule(HubTeam team, TeamLevel level, int seed)
	{
		var games = new List<SchoolGame>();
		if (team.Id == DistrictHubData.UserTeamId)
		{
			foreach (CalendarContest contest in ClubhouseCalendar.Contests)
			{
				if (contest.Level != level)
				{
					continue;
				}

				HubTeam opponent = DistrictHubData.GetTeam(contest.OpponentId);
				games.Add(new SchoolGame(
					contest.Date,
					opponent.Id,
					opponent.Name,
					contest.Home,
					contest.Time,
					contest.Exhibition,
					contest.Completed,
					contest.TeamScore,
					contest.OpponentScore,
					contest.ResultLabel));
			}

			return games;
		}

		foreach (CalendarContest contest in ClubhouseCalendar.Contests)
		{
			if (contest.Level != level || contest.OpponentId != team.Id)
			{
				continue;
			}

			HubTeam user = DistrictHubData.GetTeam(DistrictHubData.UserTeamId);
			games.Add(new SchoolGame(
				contest.Date,
				user.Id,
				user.Name,
				!contest.Home,
				contest.Time,
				contest.Exhibition,
				contest.Completed,
				contest.OpponentScore,
				contest.TeamScore,
				FlipResult(contest)));
		}

		List<HubTeam> district = DistrictHubData.TeamsInDistrict(team.DistrictId, level);
		DateOnly opening = new(DistrictHubData.SeasonYear, 3, 24);
		int added = 0;
		foreach (HubTeam opponent in district)
		{
			if (opponent.Id == team.Id)
			{
				continue;
			}

			DateOnly date = opening.AddDays((Math.Abs(seed + added * 13) % 42) + added * 3);
			bool home = (seed + added) % 2 == 0;
			bool done = date < new DateOnly(DistrictHubData.SeasonYear, 4, 25);
			int us = 3 + Math.Abs(seed + added) % 8;
			int them = 2 + Math.Abs(seed + added * 5) % 8;
			games.Add(new SchoolGame(
				date,
				opponent.Id,
				opponent.Name,
				home,
				home ? "7:00 PM" : "4:30 PM",
				false,
				done,
				done ? us : null,
				done ? them : null,
				done ? (us > them ? $"W {us}-{them}" : $"L {us}-{them}") : "SCHED"));
			added++;
		}

		games.Sort((a, b) => a.Date.CompareTo(b.Date));
		return games;
	}

	private static string FlipResult(CalendarContest contest)
	{
		if (!contest.Completed)
		{
			return contest.ResultLabel;
		}

		string mark = contest.Won ? "L" : contest.TeamScore == contest.OpponentScore ? "T" : "W";
		return $"{mark} {contest.OpponentScore}-{contest.TeamScore}";
	}

	private static List<SchoolSeason> BuildHistory(
		HubTeam team,
		int reputation,
		int seed,
		OrganizationState? program)
	{
		if (program != null && program.SeasonHistory.Count > 0)
		{
			var fromCareer = new List<SchoolSeason>(program.SeasonHistory.Count);
			foreach (ProgramSeasonResult season in program.SeasonHistory)
			{
				fromCareer.Add(new SchoolSeason(
					season.SeasonYear,
					season.Wins,
					season.Losses,
					season.DistrictFinish,
					season.Postseason));
			}

			return fromCareer;
		}

		var seasons = new List<SchoolSeason>();
		int year = DistrictHubData.SeasonYear - 1;
		int baseline = team.Wins(TeamLevel.Varsity);
		for (int i = 0; i < 6; i++)
		{
			int swing = ((seed >> i) & 7) - 3;
			int wins = Math.Clamp(baseline + swing - i, 4, 24);
			int losses = 26 - wins;
			int finish = Math.Clamp(1 + Math.Abs(seed + i * 19) % 8, 1, 8);
			if (reputation > 78)
			{
				finish = Math.Min(finish, 3);
			}

			string postseason = ProgramPostseason.FromDistrictFinish(finish);
			seasons.Add(new SchoolSeason(year - i, wins, losses, finish, postseason));
		}

		seasons.Sort((a, b) => a.Year.CompareTo(b.Year));
		return seasons;
	}

	private static List<SchoolRecordGroup> BuildRecords(
		HubTeam team,
		List<HubSquadPlayer> roster,
		int reputation,
		int seed,
		OrganizationState? program)
	{
		if (program != null && program.RecordBook.Count > 0)
		{
			return GroupsFrom(program.RecordBook);
		}

		string star = "Program";
		int best = 0;
		foreach (HubSquadPlayer player in roster)
		{
			if (player.Overall > best)
			{
				best = player.Overall;
				star = player.Name;
			}
		}

		int hr = Math.Max(team.TeamHr, 6 + seed % 8);
		return
		[
			new("Hits", [new(1, star, 90 + reputation)]),
			new("Home Runs", [new(1, star, hr)]),
			new("Wins", [new(1, "Program", 12 + reputation / 10)]),
		];
	}

	private static List<SchoolRecordGroup> GroupsFrom(IReadOnlyList<ProgramRecordHolder> holders)
	{
		var groups = new List<SchoolRecordGroup>();
		foreach (ProgramRecordCategory category in ProgramRecordBook.Categories)
		{
			var leaders = new List<SchoolRecordEntry>();
			foreach (ProgramRecordHolder holder in holders)
			{
				if (holder.Category != category)
				{
					continue;
				}

				leaders.Add(new SchoolRecordEntry(holder.Rank, holder.PlayerName, holder.Value));
			}

			if (leaders.Count > 0)
			{
				groups.Add(new SchoolRecordGroup(category.ToLabel(), leaders));
			}
		}

		return groups;
	}

	private static bool ContainsName(string text, string name) =>
		text.Contains(name, StringComparison.OrdinalIgnoreCase);

	private static int StableSeed(string value)
	{
		unchecked
		{
			int hash = 17;
			foreach (char c in value)
			{
				hash = (hash * 31) + c;
			}

			return Math.Abs(hash);
		}
	}
}

public sealed record SchoolSnapshot(
	string TeamId,
	string FullName,
	string Name,
	string ShortName,
	string DistrictId,
	string DistrictName,
	int Wins,
	int Losses,
	int DistrictPlace,
	string Streak,
	string RecentForm,
	int Reputation,
	LetterGrade Facilities,
	string BoardStatus,
	int Pitching,
	int Hitting,
	int Fielding,
	int StateTitles,
	int RegionalTitles,
	int DistrictTitles,
	IReadOnlyList<HubSquadPlayer> Roster,
	IReadOnlyList<SchoolGame> Schedule,
	IReadOnlyList<SchoolSeason> History,
	IReadOnlyList<SchoolRecordGroup> Records,
	int BaseballBudget = 0,
	int SponsorshipCommitted = 0,
	int BudgetAvailable = 0,
	int RecruitingAvailable = 0,
	int CoachSalary = 0)
{
	public string RecordLabel => $"{Wins}\u2013{Losses}";

	public string PlaceLabel => $"District: {Ordinal.Format(DistrictPlace)}";
}

public sealed record SchoolGame(
	DateOnly Date,
	string OpponentId,
	string OpponentName,
	bool Home,
	string Time,
	bool Exhibition,
	bool Completed,
	int? TeamScore,
	int? OpponentScore,
	string ResultLabel);

public sealed record SchoolSeason(int Year, int Wins, int Losses, int DistrictFinish, string Postseason);

public sealed record SchoolRecordGroup(string Category, IReadOnlyList<SchoolRecordEntry> Leaders);

public sealed record SchoolRecordEntry(int Rank, string PlayerName, int Value);
