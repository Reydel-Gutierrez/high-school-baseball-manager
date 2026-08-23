using System;
using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// Prototype Cascade schedule used by the calendar until career games
/// are wired through GameSession. Dates line up with the home-page week
/// and the baseball-year important dates.
/// </summary>
public static class ClubhouseCalendar
{
	public static IReadOnlyList<CalendarContest> Contests { get; } = Build();

	public static IReadOnlyList<CalendarContest> On(DateOnly date)
	{
		var matches = new List<CalendarContest>();
		foreach (CalendarContest contest in Contests)
		{
			if (contest.Date == date)
			{
				matches.Add(contest);
			}
		}

		return matches;
	}

	public static IReadOnlyList<CalendarContest> InMonth(int year, int month)
	{
		var matches = new List<CalendarContest>();
		foreach (CalendarContest contest in Contests)
		{
			if (contest.Date.Year == year && contest.Date.Month == month)
			{
				matches.Add(contest);
			}
		}

		return matches;
	}

	public static string OpponentMark(string opponentId)
	{
		HubTeam team = DistrictHubData.GetTeam(opponentId);
		string[] parts = team.Name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length >= 2)
		{
			return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
		}

		string name = team.ShortName;
		return name.Length <= 3 ? name : name[..3];
	}

	private static List<CalendarContest> Build()
	{
		var games = new List<CalendarContest>();

		AddSeries(games, 2, 10, 11, "marblehead", true, "7:00 PM", "4:00 PM", 4, 2, 6, 5, exhibition: true);
		AddSeries(games, 2, 13, 14, "two-harbors", false, "4:30 PM", "1:00 PM", 8, 1, 7, 3, exhibition: true);

		AddSeries(games, 2, 16, 17, "port-clinton", true, "4:00 PM", "1:00 PM", 5, 3, 4, 6);
		AddSeries(games, 2, 20, 21, "sandusky", false, "7:00 PM", "4:00 PM", 3, 2, 2, 5);
		AddSeries(games, 2, 24, 25, "capital-city", true, "7:00 PM", "4:00 PM", 6, 4, 5, 4);
		AddSeries(games, 2, 27, 28, "hibbing", false, "4:30 PM", "1:00 PM", 2, 5, 3, 7);

		AddSeries(games, 3, 3, 4, "springfield", true, "7:00 PM", "4:00 PM", 7, 1, 8, 3);
		AddSeries(games, 3, 10, 11, "harbor-ridge", true, "7:00 PM", "4:00 PM", 4, 3, 3, 4);
		AddSeries(games, 3, 13, 14, "bayview", false, "7:00 PM", "4:00 PM", 5, 2, 6, 5);
		AddSeries(games, 3, 17, 18, "northfield", true, "7:00 PM", "4:00 PM", 9, 1, 7, 2);
		AddSeries(games, 3, 20, 21, "lakeside", false, "7:00 PM", "4:00 PM", 6, 0, 5, 3);
		AddSeries(games, 3, 24, 25, "pinecrest", true, "7:00 PM", "4:00 PM", 11, 2, 8, 4);
		AddSeries(games, 3, 27, 28, "great-lakes", false, "7:00 PM", "4:00 PM", 3, 4, 2, 6);

		AddSeries(games, 4, 3, 4, "harbor-ridge", false, "7:00 PM", "4:00 PM", 5, 4, 4, 4);
		AddSeries(games, 4, 7, 8, "bayview", true, "7:00 PM", "4:00 PM", 6, 3, 5, 2);
		AddSeries(games, 4, 10, 11, "northfield", false, "7:00 PM", "4:00 PM", 8, 1, 3, 5);
		AddSeries(games, 4, 14, 15, "lakeside", true, "7:00 PM", "4:00 PM", 7, 2, 6, 1);
		AddSeries(games, 4, 17, 18, "pinecrest", false, "7:00 PM", "4:00 PM", 10, 3, 7, 4);
		AddSeries(games, 4, 22, 23, "delaware-valley", true, "7:00 PM", "4:00 PM", 6, 2, 5, 3);
		AddSeries(games, 4, 25, 26, "great-lakes", true, "7:00 PM", "11:00 AM");
		AddSeries(games, 4, 28, 29, "harbor-ridge", true, "7:00 PM", "4:00 PM");

		AddSeries(games, 5, 1, 2, "bayview", false, "7:00 PM", "4:00 PM");
		Add(games, 5, 3, "northfield", true, TeamLevel.Varsity, "1:00 PM");

		return games;
	}

	private static void AddSeries(
		List<CalendarContest> games,
		int month,
		int varsityDay,
		int jvDay,
		string opponentId,
		bool home,
		string varsityTime,
		string jvTime,
		int? varsityUs = null,
		int? varsityThem = null,
		int? jvUs = null,
		int? jvThem = null,
		bool exhibition = false)
	{
		Add(games, month, varsityDay, opponentId, home, TeamLevel.Varsity, varsityTime, varsityUs, varsityThem, exhibition);
		Add(games, month, jvDay, opponentId, home, TeamLevel.JuniorVarsity, jvTime, jvUs, jvThem, exhibition);
	}

	private static void Add(
		List<CalendarContest> games,
		int month,
		int day,
		string opponentId,
		bool home,
		TeamLevel level,
		string time,
		int? teamScore = null,
		int? opponentScore = null,
		bool exhibition = false)
	{
		games.Add(new CalendarContest(
			new DateOnly(DistrictHubData.SeasonYear, month, day),
			opponentId,
			home,
			level,
			time,
			exhibition,
			teamScore,
			opponentScore));
	}
}

public sealed record CalendarContest(
	DateOnly Date,
	string OpponentId,
	bool Home,
	TeamLevel Level,
	string Time,
	bool Exhibition,
	int? TeamScore,
	int? OpponentScore)
{
	public bool Completed => TeamScore.HasValue && OpponentScore.HasValue;

	public bool Won => Completed && TeamScore > OpponentScore;

	public string VenueLabel => Home ? "HOME" : "AWAY";

	public string ResultLabel
	{
		get
		{
			if (!Completed)
			{
				return Exhibition ? "EXH" : "SCHED";
			}

			string mark = Won ? "W" : TeamScore == OpponentScore ? "T" : "L";
			string prefix = Exhibition ? "EXH " : string.Empty;
			return $"{prefix}{mark} {TeamScore}-{OpponentScore}";
		}
	}
}
