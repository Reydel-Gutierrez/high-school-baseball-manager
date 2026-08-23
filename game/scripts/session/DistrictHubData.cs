using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// Prototype league used by District Hub screens until standings, playoffs,
/// and awards are driven by simulation. Four districts of eight teams, with
/// Cascade in the Great Lakes District.
/// TODO(career-backend): Keep this prototype UI data until CareerService
/// standings/stats/news are wired. Static school identities now also live in
/// HSBM.Domain.LeagueCatalog; do not delete this file in the engine phase.
/// </summary>
public static class DistrictHubData
{
	public const int SeasonYear = 2026;
	public const string UserTeamId = "cascade";
	public const string UserDistrictId = "great-lakes";
	public const string UserCoachName = "Daniel Hart";

	public const string DistrictGreatLakes = "great-lakes";
	public const string DistrictNorthShore = "north-shore";
	public const string DistrictCapitalValley = "capital-valley";
	public const string DistrictIronRange = "iron-range";

	public static readonly IReadOnlyList<HubDistrict> Districts =
	[
		new(DistrictGreatLakes, "Great Lakes", "GL", "East"),
		new(DistrictNorthShore, "North Shore", "NS", "East"),
		new(DistrictCapitalValley, "Capital Valley", "CV", "West"),
		new(DistrictIronRange, "Iron Range", "IR", "West"),
	];

	public static readonly IReadOnlyList<HubTeam> Teams =
	[
		T("cascade", "Cascade Regional", "CASCADE", DistrictGreatLakes, true, 18, 4, "W6", 12, 8, "W2", 142, 68, 0.312f, 28, 2.41f),
		T("great-lakes", "Great Lakes", "GREAT LAKES", DistrictGreatLakes, false, 16, 6, "W3", 16, 6, "W4", 128, 79, 0.298f, 22, 2.88f),
		T("harbor-ridge", "Harbor Ridge", "HARBOR RIDGE", DistrictGreatLakes, false, 14, 8, "L1", 14, 8, "W1", 119, 91, 0.286f, 19, 3.12f),
		T("delaware-valley", "Delaware Valley", "DELAWARE VALLEY", DistrictGreatLakes, false, 12, 10, "W1", 13, 9, "L2", 108, 102, 0.274f, 16, 3.40f),
		T("bayview", "Bayview Central", "BAYVIEW", DistrictGreatLakes, false, 11, 11, "L2", 10, 12, "L1", 101, 110, 0.261f, 14, 3.72f),
		T("northfield", "Northfield Prep", "NORTHFIELD", DistrictGreatLakes, false, 9, 13, "L3", 9, 13, "W1", 92, 118, 0.248f, 11, 4.05f),
		T("lakeside", "Lakeside Academy", "LAKESIDE", DistrictGreatLakes, false, 7, 15, "L4", 8, 14, "L3", 81, 129, 0.236f, 8, 4.44f),
		T("pinecrest", "Pinecrest", "PINECREST", DistrictGreatLakes, false, 4, 18, "L7", 6, 16, "L5", 64, 148, 0.219f, 5, 5.12f),

		T("port-clinton", "Port Clinton", "PORT CLINTON", DistrictNorthShore, false, 17, 5, "W5", 15, 7, "W3", 136, 74, 0.305f, 24, 2.55f),
		T("sandusky", "Sandusky", "SANDUSKY", DistrictNorthShore, false, 15, 7, "W2", 14, 8, "W2", 124, 86, 0.291f, 20, 2.97f),
		T("vermilion", "Vermilion", "VERMILION", DistrictNorthShore, false, 13, 9, "L1", 12, 10, "L1", 111, 98, 0.277f, 17, 3.28f),
		T("huron", "Huron", "HURON", DistrictNorthShore, false, 12, 10, "W1", 11, 11, "W1", 104, 106, 0.268f, 15, 3.51f),
		T("avon-lake", "Avon Lake", "AVON LAKE", DistrictNorthShore, false, 10, 12, "L2", 10, 12, "L2", 96, 114, 0.255f, 12, 3.88f),
		T("westlake", "Westlake", "WESTLAKE", DistrictNorthShore, false, 9, 13, "L1", 8, 14, "L3", 88, 121, 0.247f, 10, 4.16f),
		T("rocky-river", "Rocky River", "ROCKY RIVER", DistrictNorthShore, false, 6, 16, "L5", 7, 15, "L2", 74, 134, 0.231f, 7, 4.62f),
		T("marblehead", "Marblehead", "MARBLEHEAD", DistrictNorthShore, false, 5, 17, "L6", 6, 16, "L4", 69, 141, 0.224f, 6, 4.91f),

		T("capital-city", "Capital City", "CAPITAL CITY", DistrictCapitalValley, false, 17, 5, "W4", 16, 6, "W5", 139, 71, 0.308f, 26, 2.49f),
		T("springfield", "Springfield", "SPRINGFIELD", DistrictCapitalValley, false, 15, 7, "W2", 14, 8, "W1", 122, 88, 0.289f, 21, 3.02f),
		T("beavercreek", "Beavercreek", "BEAVERCREEK", DistrictCapitalValley, false, 14, 8, "L1", 13, 9, "W2", 117, 94, 0.281f, 18, 3.19f),
		T("kettering", "Kettering", "KETTERING", DistrictCapitalValley, false, 13, 9, "W3", 12, 10, "L1", 109, 101, 0.272f, 16, 3.37f),
		T("centerville", "Centerville", "CENTERVILLE", DistrictCapitalValley, false, 11, 11, "L2", 11, 11, "L1", 99, 108, 0.259f, 13, 3.70f),
		T("fairborn", "Fairborn", "FAIRBORN", DistrictCapitalValley, false, 8, 14, "L3", 9, 13, "L2", 86, 122, 0.244f, 9, 4.21f),
		T("miamisburg", "Miamisburg", "MIAMISBURG", DistrictCapitalValley, false, 7, 15, "L1", 7, 15, "L4", 79, 128, 0.238f, 8, 4.38f),
		T("huber", "Huber Heights", "HUBER", DistrictCapitalValley, false, 5, 17, "L6", 6, 16, "L3", 71, 139, 0.226f, 6, 4.84f),

		T("hibbing", "Hibbing", "HIBBING", DistrictIronRange, false, 16, 6, "W3", 15, 7, "W2", 131, 80, 0.296f, 23, 2.74f),
		T("duluth-east", "Duluth East", "DULUTH EAST", DistrictIronRange, false, 15, 7, "W1", 14, 8, "W3", 125, 87, 0.288f, 19, 2.99f),
		T("iron-range", "Iron Range", "IRON RANGE", DistrictIronRange, false, 13, 9, "L1", 12, 10, "L2", 112, 99, 0.275f, 17, 3.33f),
		T("grand-rapids", "Grand Rapids", "GRAND RAPIDS", DistrictIronRange, false, 12, 10, "W2", 11, 11, "W1", 106, 105, 0.266f, 15, 3.48f),
		T("cloquet", "Cloquet", "CLOQUET", DistrictIronRange, false, 10, 12, "L2", 10, 12, "L1", 94, 116, 0.252f, 12, 3.91f),
		T("virginia", "Virginia", "VIRGINIA", DistrictIronRange, false, 9, 13, "L3", 8, 14, "L3", 87, 123, 0.245f, 10, 4.18f),
		T("eveleth", "Eveleth", "EVELETH", DistrictIronRange, false, 7, 15, "L4", 7, 15, "L2", 78, 131, 0.233f, 8, 4.51f),
		T("two-harbors", "Two Harbors", "TWO HARBORS", DistrictIronRange, false, 4, 18, "L8", 5, 17, "L6", 62, 151, 0.216f, 5, 5.22f),
	];

	public static readonly IReadOnlyList<HubAward> Awards =
	[
		new(
			"diamond-crown",
			"Diamond Crown",
			"Player of the Year",
			"The league's top all-around player. Hitting, defense, and the way the lineup turns over when he steps in.",
			false,
			new HubAwardCandidate("Jalen Brooks", "CF", "SR", "cascade", "CASCADE", ".397 AVG", "4 HR · 18 RBI · 16 SB", 0),
			[
				new("Jonah Kim", "CF", "JR", "great-lakes", "GREAT LAKES", ".381 AVG", "3 HR · 16 RBI · 14 SB", 16),
				new("Cole Brennan", "SS", "SR", "port-clinton", "PORT CLINTON", ".372 AVG", "5 HR · 21 RBI · 9 SB", 25),
				new("Aiden Glenn", "SS", "SR", "delaware-valley", "DELAWARE VALLEY", ".368 AVG", "2 HR · 14 RBI · 11 SB", 29),
			]),
		new(
			"fireball",
			"Fireball Trophy",
			"Pitcher of the Year",
			"The staff ace who owns Friday nights. Wins, ERA, and the innings a district title run is built on.",
			false,
			new HubAwardCandidate("Luca Marini", "RHP", "SR", "cascade", "CASCADE", "1.95 ERA", "5-0 · 36.0 IP · 28 K", 0),
			[
				new("Miles Harlan", "RHP", "SR", "capital-city", "CAPITAL CITY", "2.08 ERA", "6-1 · 38.1 IP · 41 K", 13),
				new("Tony Romano", "RHP", "SR", "great-lakes", "GREAT LAKES", "2.18 ERA", "5-1 · 34.2 IP · 31 K", 23),
				new("Grant Nash", "RHP", "SR", "delaware-valley", "DELAWARE VALLEY", "2.31 ERA", "4-2 · 33.0 IP · 29 K", 36),
			]),
		new(
			"thunder-bat",
			"Thunder Bat",
			"Hitter of the Year",
			"The middle-of-the-order bat that changes a game with one swing. Power, RBI, and extra-base damage.",
			false,
			new HubAwardCandidate("Isaiah Cole", "1B", "SR", "cascade", "CASCADE", "7 HR · 24 RBI", ".324 AVG · .662 SLG", 0),
			[
				new("Reid Slater", "1B", "SR", "great-lakes", "GREAT LAKES", "6 HR · 21 RBI", ".311 AVG · .598 SLG", 8),
				new("Wes Dale", "LF", "SR", "harbor-ridge", "HARBOR RIDGE", "6 HR · 19 RBI", ".305 AVG · .581 SLG", 12),
				new("Cole Brennan", "SS", "SR", "port-clinton", "PORT CLINTON", "5 HR · 21 RBI", ".372 AVG · .614 SLG", 14),
			]),
		new(
			"leather",
			"Leather Award",
			"Defensive Player of the Year",
			"The glove that saves runs the box score never sees. Range, arm, and the plays that keep a one-run lead intact.",
			false,
			new HubAwardCandidate("Drew Hale", "SS", "JR", "cascade", "CASCADE", "88 FLD", "0.342 AVG · 9 SB", 0),
			[
				new("Mateo Diaz", "SS", "JR", "great-lakes", "GREAT LAKES", "86 FLD", ".318 AVG · 7 SB", 6),
				new("Ian Frost", "SS", "SO", "harbor-ridge", "HARBOR RIDGE", "84 FLD", ".301 AVG · 12 SB", 11),
				new("Luis Porter", "C", "JR", "delaware-valley", "DELAWARE VALLEY", "83 FLD", "31% CS · .286 AVG", 14),
			]),
		new(
			"rising-star",
			"Rising Star",
			"Newcomer of the Year",
			"The underclassman who already looks like a Friday-night regular. Freshman or sophomore, varsity minutes.",
			false,
			new HubAwardCandidate("Cam Whitaker", "2B", "SO", "cascade", "CASCADE", ".311 AVG", "11 SB · .408 OBP", 0),
			[
				new("Eli Voss", "2B", "SO", "great-lakes", "GREAT LAKES", ".298 AVG", "9 SB · .391 OBP", 9),
				new("Ian Frost", "SS", "SO", "harbor-ridge", "HARBOR RIDGE", ".301 AVG", "12 SB · .377 OBP", 11),
				new("Bryce Cobb", "1B", "SO", "delaware-valley", "DELAWARE VALLEY", ".288 AVG", "4 HR · 15 RBI", 16),
			]),
		new(
			"sideline-cup",
			"Sideline Cup",
			"Coach of the Year",
			"The bench that turned a district race. Lineups, Friday pitching, and the way a program carries itself in April.",
			true,
			new HubAwardCandidate(UserCoachName, "HC", "STAFF", "cascade", "CASCADE", "18-4 · 1st", "Great Lakes District", 0),
			[
				new("Elena Voss", "HC", "STAFF", "great-lakes", "GREAT LAKES", "16-4 · 2nd", "Great Lakes District", 8),
				new("Marcus Quinn", "HC", "STAFF", "port-clinton", "PORT CLINTON", "17-5 · 1st", "North Shore District", 11),
				new("Ray Delgado", "HC", "STAFF", "capital-city", "CAPITAL CITY", "17-5 · 1st", "Capital Valley District", 14),
			]),
	];

	public static readonly HubStatTab[] BattingStats =
	[
		new("AVG", "AVG", "Batting Average"),
		new("G", "G", "Games"),
		new("AB", "AB", "At Bats"),
		new("R", "R", "Runs"),
		new("H", "H", "Hits"),
		new("2B", "2B", "Doubles"),
		new("3B", "3B", "Triples"),
		new("HR", "HR", "Home Runs"),
		new("RBI", "RBI", "Runs Batted In"),
		new("BB", "BB", "Walks"),
		new("SO", "SO", "Strikeouts"),
		new("SB", "SB", "Stolen Bases"),
		new("OBP", "OBP", "On-Base Percentage"),
		new("SLG", "SLG", "Slugging Percentage"),
		new("OPS", "OPS", "On-Base Plus Slugging"),
	];

	public static readonly HubStatTab[] PitchingStats =
	[
		new("ERA", "ERA", "Earned Run Average"),
		new("W", "W", "Wins"),
		new("L", "L", "Losses"),
		new("SV", "SV", "Saves"),
		new("G", "G", "Games"),
		new("GS", "GS", "Games Started"),
		new("IP", "IP", "Innings Pitched"),
		new("H", "H", "Hits Allowed"),
		new("R", "R", "Runs Allowed"),
		new("ER", "ER", "Earned Runs"),
		new("BB", "BB", "Walks"),
		new("SO", "SO", "Strikeouts"),
		new("WHIP", "WHIP", "Walks Plus Hits Per Inning"),
	];

	public static readonly IReadOnlyList<HubBatter> Batters =
	[
		Bat("Jalen Brooks", "CF", "cascade", "CASCADE", 22, 78, 28, 31, 8, 2, 4, 18, 12, 11, 16, 0.397f, 0.468f, 0.692f),
		Bat("Jonah Kim", "CF", "great-lakes", "GREAT LAKES", 22, 76, 24, 29, 6, 1, 3, 16, 10, 13, 14, 0.381f, 0.447f, 0.605f),
		Bat("Cole Brennan", "SS", "port-clinton", "PORT CLINTON", 22, 78, 26, 29, 7, 1, 5, 21, 9, 14, 9, 0.372f, 0.430f, 0.614f),
		Bat("Aiden Glenn", "SS", "delaware-valley", "DELAWARE VALLEY", 22, 76, 21, 28, 6, 2, 2, 14, 8, 12, 11, 0.368f, 0.422f, 0.579f),
		Bat("Drew Hale", "SS", "cascade", "CASCADE", 22, 76, 22, 26, 5, 1, 2, 15, 8, 12, 9, 0.342f, 0.405f, 0.513f),
		Bat("Mateo Cruz", "C", "cascade", "CASCADE", 21, 70, 19, 23, 4, 0, 5, 22, 9, 13, 1, 0.329f, 0.405f, 0.629f),
		Bat("Isaiah Cole", "1B", "cascade", "CASCADE", 22, 74, 20, 24, 5, 0, 7, 24, 11, 16, 0, 0.324f, 0.412f, 0.662f),
		Bat("Reid Slater", "1B", "great-lakes", "GREAT LAKES", 22, 74, 18, 23, 4, 0, 6, 21, 8, 15, 1, 0.311f, 0.378f, 0.598f),
		Bat("Cam Whitaker", "2B", "cascade", "CASCADE", 20, 61, 17, 19, 3, 1, 1, 9, 10, 9, 11, 0.311f, 0.408f, 0.443f),
		Bat("Wes Dale", "LF", "harbor-ridge", "HARBOR RIDGE", 22, 72, 16, 22, 5, 0, 6, 19, 7, 14, 3, 0.305f, 0.367f, 0.581f),
		Bat("Noah Barrett", "3B", "cascade", "CASCADE", 21, 69, 15, 21, 4, 0, 4, 17, 7, 15, 2, 0.304f, 0.368f, 0.551f),
		Bat("Ian Frost", "SS", "harbor-ridge", "HARBOR RIDGE", 20, 63, 18, 19, 3, 1, 1, 8, 8, 11, 12, 0.301f, 0.377f, 0.429f),
		Bat("Eli Voss", "2B", "great-lakes", "GREAT LAKES", 20, 64, 14, 19, 2, 0, 1, 9, 9, 10, 9, 0.297f, 0.391f, 0.375f),
		Bat("Marcus Bell", "1B", "cascade", "CASCADE", 22, 70, 13, 20, 4, 0, 5, 18, 8, 15, 0, 0.286f, 0.351f, 0.500f),
		Bat("Theo West", "CF", "delaware-valley", "DELAWARE VALLEY", 21, 68, 15, 19, 4, 1, 4, 14, 7, 14, 6, 0.279f, 0.347f, 0.529f),
		Bat("Ryan Peck", "RF", "cascade", "CASCADE", 20, 64, 12, 18, 3, 0, 3, 14, 7, 14, 4, 0.281f, 0.352f, 0.500f),
	];

	public static readonly IReadOnlyList<HubPitcher> Pitchers =
	[
		Pit("Luca Marini", "RHP", "cascade", "CASCADE", 5, 0, 0, 8, 8, 36.0f, 22, 9, 8, 8, 28, 1.95f, 0.89f),
		Pit("Miles Harlan", "RHP", "capital-city", "CAPITAL CITY", 6, 1, 0, 9, 9, 38.1f, 26, 11, 9, 11, 41, 2.08f, 0.97f),
		Pit("Tony Romano", "RHP", "great-lakes", "GREAT LAKES", 5, 1, 0, 8, 8, 34.2f, 24, 10, 8, 13, 31, 2.18f, 1.08f),
		Pit("Grant Nash", "RHP", "delaware-valley", "DELAWARE VALLEY", 4, 2, 0, 8, 8, 33.0f, 27, 12, 9, 11, 29, 2.31f, 1.14f),
		Pit("Max Steele", "RHP", "harbor-ridge", "HARBOR RIDGE", 4, 2, 0, 8, 8, 32.1f, 28, 13, 9, 10, 27, 2.44f, 1.19f),
		Pit("Trey Lawson", "LHP", "cascade", "CASCADE", 3, 1, 0, 7, 7, 29.1f, 26, 11, 9, 11, 24, 2.66f, 1.26f),
		Pit("Jake Pruitt", "RHP", "cascade", "CASCADE", 4, 2, 0, 8, 7, 31.0f, 28, 13, 11, 10, 26, 3.09f, 1.22f),
		Pit("Ben Ortiz", "RHP", "cascade", "CASCADE", 2, 1, 1, 9, 4, 24.2f, 22, 11, 9, 9, 19, 3.28f, 1.26f),
		Pit("Cole Reid", "RHP", "cascade", "CASCADE", 2, 1, 2, 10, 2, 22.1f, 21, 10, 8, 8, 21, 3.40f, 1.28f),
		Pit("Devin Shore", "LHP", "cascade", "CASCADE", 1, 2, 1, 10, 1, 18.2f, 18, 10, 8, 7, 16, 3.81f, 1.35f),
		Pit("Gavin Hale", "LHP", "great-lakes", "GREAT LAKES", 3, 2, 0, 7, 6, 26.0f, 24, 12, 10, 12, 22, 3.46f, 1.38f),
		Pit("Tyler Reed", "RHP", "harbor-ridge", "HARBOR RIDGE", 2, 3, 0, 8, 5, 23.0f, 25, 14, 12, 9, 18, 4.70f, 1.48f),
	];

	public static readonly IReadOnlyList<HubNewsItem> News =
	[
		new("APR 24", "DISTRICT", "Cascade opens a two-game lead in the Great Lakes",
			"A six-game winning streak has the Regional sitting 18-4 and alone in first. Great Lakes is 16-6 and still within reach if Friday's rivalry set tilts the other way."),
		new("APR 23", "RIVALRY", "Friday night: Cascade vs Great Lakes at home",
			"The top two in the district meet at Cascade. Marini is lined up for the ball, with Romano expected opposite him. A sweep would all but lock the 1-seed."),
		new("APR 22", "AWARDS", "Brooks still wearing the Diamond Crown",
			"Jalen Brooks is hitting .397 with 16 stolen bases. Jonah Kim and Cole Brennan are the closest chasers, but neither has matched the Cascade center fielder's on-base week."),
		new("APR 21", "PITCHING", "Marini's ERA keeps the Fireball on the hill",
			"Luca Marini is 5-0 with a 1.95 ERA. Capital City's Miles Harlan (2.08) is the only arm in the four districts within shouting distance."),
		new("APR 19", "PLAYOFFS", "East Regional would be Cascade-Sandusky if it started today",
			"Top two from Great Lakes draw North Shore. That puts Cascade opposite Sandusky in one semi and Port Clinton opposite Great Lakes in the other. West Regional would be Capital City and Hibbing as the 1-seeds."),
		new("APR 18", "NOTE", "Harbor Ridge drops a one-run game, slips to third",
			"Max Steele threw well enough to win and still took the loss. Harbor Ridge is 14-8, four back of Cascade, with Delaware Valley another two games behind."),
		new("APR 16", "AWARDS", "Sideline Cup watch: Hart, Voss, Quinn, Delgado",
			"Cascade's Daniel Hart is the frontrunner after the 18-4 start. Elena Voss (Great Lakes), Marcus Quinn (Port Clinton), and Ray Delgado (Capital City) are the three names behind him."),
		new("APR 14", "DISTRICT", "North Shore stays a two-team race",
			"Port Clinton is 17-5. Sandusky is 15-7. Vermilion is the only other club in that district above .500, and the gap to second is already two games."),
		new("APR 12", "HITTING", "Cole's Thunder Bat lead is built on the middle of the order",
			"Isaiah Cole has 7 home runs and 24 RBI. Reid Slater and Wes Dale are both at 6 homers. One hot weekend from either would make this a three-man race."),
		new("APR 10", "NOTE", "Open pool and the bottom of the Great Lakes",
			"Pinecrest is 4-18 and Lakeside 7-15. District offices have already heard from both programs about late-season Open Pool help for 2027."),
	];

	public static HubDistrict GetDistrict(string id)
	{
		foreach (HubDistrict district in Districts)
		{
			if (district.Id == id)
			{
				return district;
			}
		}

		return Districts[0];
	}

	public static HubTeam GetTeam(string id)
	{
		foreach (HubTeam team in Teams)
		{
			if (team.Id == id)
			{
				return team;
			}
		}

		return Teams[0];
	}

	public static List<HubTeam> TeamsInDistrict(string districtId, TeamLevel level)
	{
		var list = new List<HubTeam>();
		foreach (HubTeam team in Teams)
		{
			if (team.DistrictId == districtId)
			{
				list.Add(team);
			}
		}

		list.Sort((a, b) => CompareStandingsForLevel(a, b, level));
		return list;
	}

	public static List<HubStandingRow> StandingsFor(string districtId, TeamLevel level)
	{
		List<HubTeam> teams = TeamsInDistrict(districtId, level);
		var rows = new List<HubStandingRow>(teams.Count);
		int leaderWins = 0;
		int leaderLosses = 0;
		for (int i = 0; i < teams.Count; i++)
		{
			HubTeam team = teams[i];
			int wins = team.Wins(level);
			int losses = team.Losses(level);
			if (i == 0)
			{
				leaderWins = wins;
				leaderLosses = losses;
			}

			float gamesBack = ((leaderWins - wins) + (losses - leaderLosses)) / 2f;
			string gb = i == 0 ? "—" : gamesBack.ToString("0.0").Replace(".0", "");
			rows.Add(new HubStandingRow(
				i + 1,
				team,
				wins,
				losses,
				Pct(wins, losses),
				gb,
				team.Streak(level),
				i < 2));
		}

		return rows;
	}

	public static List<HubTeam> PlayoffField(string districtId, TeamLevel level)
	{
		List<HubTeam> ranked = TeamsInDistrict(districtId, level);
		return [ranked[0], ranked[1]];
	}

	public static HubStatTab[] StatsFor(string category) =>
		category == "pitching" ? PitchingStats : BattingStats;

	public static List<HubLeader> LeadersFor(string category, string stat)
	{
		var list = new List<HubLeader>();
		if (category == "pitching")
		{
			foreach (HubPitcher pitcher in Pitchers)
			{
				list.Add(ToLeader(pitcher, stat));
			}
		}
		else
		{
			foreach (HubBatter batter in Batters)
			{
				list.Add(ToLeader(batter, stat));
			}
		}

		bool lowerIsBetter = stat is "ERA" or "WHIP";
		list.Sort((a, b) =>
		{
			int cmp = lowerIsBetter
				? a.SortValue.CompareTo(b.SortValue)
				: b.SortValue.CompareTo(a.SortValue);
			return cmp != 0 ? cmp : string.CompareOrdinal(a.Name, b.Name);
		});

		return list;
	}

	public static string Pct(int wins, int losses)
	{
		int games = wins + losses;
		if (games == 0)
		{
			return ".000";
		}

		float pct = wins / (float)games;
		string raw = pct.ToString("0.000");
		return raw.StartsWith("0") ? raw[1..] : raw;
	}

	private static int CompareStandingsForLevel(HubTeam a, HubTeam b, TeamLevel level)
	{
		int winCmp = b.Wins(level).CompareTo(a.Wins(level));
		if (winCmp != 0)
		{
			return winCmp;
		}

		int lossCmp = a.Losses(level).CompareTo(b.Losses(level));
		return lossCmp != 0 ? lossCmp : string.CompareOrdinal(a.ShortName, b.ShortName);
	}

	private static HubLeader ToLeader(HubBatter batter, string stat)
	{
		float value = stat switch
		{
			"AVG" => batter.Average,
			"G" => batter.Games,
			"AB" => batter.AtBats,
			"R" => batter.Runs,
			"H" => batter.Hits,
			"2B" => batter.Doubles,
			"3B" => batter.Triples,
			"HR" => batter.HomeRuns,
			"RBI" => batter.Rbi,
			"BB" => batter.Walks,
			"SO" => batter.Strikeouts,
			"SB" => batter.StolenBases,
			"OBP" => batter.OnBase,
			"SLG" => batter.Slugging,
			"OPS" => batter.Ops,
			_ => batter.Average,
		};

		string display = stat is "AVG" or "OBP" or "SLG" or "OPS"
			? FormatRate(value)
			: $"{(int)value}";

		return new HubLeader(batter.Name, batter.Position, batter.TeamId, batter.TeamShort, stat, display, value);
	}

	private static HubLeader ToLeader(HubPitcher pitcher, string stat)
	{
		float value = stat switch
		{
			"ERA" => pitcher.Era,
			"W" => pitcher.Wins,
			"L" => pitcher.Losses,
			"SV" => pitcher.Saves,
			"G" => pitcher.Games,
			"GS" => pitcher.GamesStarted,
			"IP" => pitcher.Innings,
			"H" => pitcher.Hits,
			"R" => pitcher.Runs,
			"ER" => pitcher.EarnedRuns,
			"BB" => pitcher.Walks,
			"SO" => pitcher.Strikeouts,
			"WHIP" => pitcher.Whip,
			_ => pitcher.Era,
		};

		string display = stat switch
		{
			"ERA" or "WHIP" => value.ToString("0.00"),
			"IP" => pitcher.Innings.ToString("0.0"),
			_ => $"{(int)value}",
		};

		return new HubLeader(pitcher.Name, pitcher.Position, pitcher.TeamId, pitcher.TeamShort, stat, display, value);
	}

	private static string FormatRate(float value)
	{
		string raw = value.ToString("0.000");
		return raw.StartsWith("0") ? raw[1..] : raw;
	}

	private static HubTeam T(
		string id,
		string name,
		string shortName,
		string districtId,
		bool isUser,
		int vW,
		int vL,
		string vStreak,
		int jW,
		int jL,
		string jStreak,
		int runsFor,
		int runsAgainst,
		float avg,
		int hr,
		float era) =>
		new(id, name, shortName, districtId, isUser, vW, vL, vStreak, jW, jL, jStreak, runsFor, runsAgainst, avg, hr, era);

	private static HubBatter Bat(
		string name,
		string position,
		string teamId,
		string teamShort,
		int games,
		int atBats,
		int runs,
		int hits,
		int doubles,
		int triples,
		int homeRuns,
		int rbi,
		int walks,
		int strikeouts,
		int stolenBases,
		float average,
		float onBase,
		float slugging) =>
		new(name, position, teamId, teamShort, games, atBats, runs, hits, doubles, triples, homeRuns, rbi, walks, strikeouts, stolenBases, average, onBase, slugging);

	private static HubPitcher Pit(
		string name,
		string position,
		string teamId,
		string teamShort,
		int wins,
		int losses,
		int saves,
		int games,
		int gamesStarted,
		float innings,
		int hits,
		int runs,
		int earnedRuns,
		int walks,
		int strikeouts,
		float era,
		float whip) =>
		new(name, position, teamId, teamShort, wins, losses, saves, games, gamesStarted, innings, hits, runs, earnedRuns, walks, strikeouts, era, whip);
}

public sealed record HubDistrict(string Id, string Name, string Abbreviation, string Region);

public sealed record HubTeam(
	string Id,
	string Name,
	string ShortName,
	string DistrictId,
	bool IsUserTeam,
	int VarsityWins,
	int VarsityLosses,
	string VarsityStreak,
	int JvWins,
	int JvLosses,
	string JvStreak,
	int RunsFor,
	int RunsAgainst,
	float TeamAvg,
	int TeamHr,
	float TeamEra)
{
	public int Wins(TeamLevel level) => level == TeamLevel.Varsity ? VarsityWins : JvWins;

	public int Losses(TeamLevel level) => level == TeamLevel.Varsity ? VarsityLosses : JvLosses;

	public string Streak(TeamLevel level) => level == TeamLevel.Varsity ? VarsityStreak : JvStreak;

	public string Record(TeamLevel level) => $"{Wins(level)}-{Losses(level)}";
}

public sealed record HubStandingRow(
	int Rank,
	HubTeam Team,
	int Wins,
	int Losses,
	string Pct,
	string GamesBack,
	string Streak,
	bool Qualifies);

public sealed record HubAward(
	string Id,
	string TrophyName,
	string Category,
	string Blurb,
	bool IsCoachAward,
	HubAwardCandidate Leader,
	IReadOnlyList<HubAwardCandidate> Competitors);

public sealed record HubAwardCandidate(
	string Name,
	string Position,
	string Year,
	string TeamId,
	string TeamShort,
	string HeadlineStat,
	string Detail,
	int Gap);

public sealed record HubStatTab(string Key, string Label, string Title);

public sealed record HubBatter(
	string Name,
	string Position,
	string TeamId,
	string TeamShort,
	int Games,
	int AtBats,
	int Runs,
	int Hits,
	int Doubles,
	int Triples,
	int HomeRuns,
	int Rbi,
	int Walks,
	int Strikeouts,
	int StolenBases,
	float Average,
	float OnBase,
	float Slugging)
{
	public float Ops => OnBase + Slugging;
}

public sealed record HubPitcher(
	string Name,
	string Position,
	string TeamId,
	string TeamShort,
	int Wins,
	int Losses,
	int Saves,
	int Games,
	int GamesStarted,
	float Innings,
	int Hits,
	int Runs,
	int EarnedRuns,
	int Walks,
	int Strikeouts,
	float Era,
	float Whip);

public sealed record HubLeader(
	string Name,
	string Position,
	string TeamId,
	string TeamShort,
	string Stat,
	string Display,
	float SortValue);

public sealed record HubNewsItem(
	string Date,
	string Tag,
	string Headline,
	string Body);

public sealed record HubSquadPlayer(
	string FirstName,
	string LastName,
	int Jersey,
	string Position,
	string Year,
	string BatsThrows,
	int Overall,
	int Contact,
	int Power,
	int Speed,
	int Arm,
	int Field,
	string Status,
	string? PortraitPath,
	string TeamId,
	string TeamShort,
	string Squad,
	IReadOnlyList<HubPlayerSeason> Seasons)
{
	public string Name => $"{FirstName} {LastName}";

	public HubSeasonHitting? CurrentHitting =>
		Seasons.Count == 0 ? null : Seasons[Seasons.Count - 1].Hitting;

	public HubSeasonPitching? CurrentPitching =>
		Seasons.Count == 0 ? null : Seasons[Seasons.Count - 1].Pitching;
}

public sealed record HubPlayerSeason(
	int Year,
	string OrganizationName,
	HubSeasonHitting? Hitting,
	HubSeasonPitching? Pitching);

public sealed record HubSeasonHitting(
	int Games,
	int AtBats,
	int Runs,
	int Hits,
	int Doubles,
	int Triples,
	int HomeRuns,
	int Rbi,
	int Walks,
	int Strikeouts,
	int StolenBases,
	float Average,
	float OnBase,
	float Slugging)
{
	public float Ops => OnBase + Slugging;
}

public sealed record HubSeasonPitching(
	int Games,
	int GamesStarted,
	int Wins,
	int Losses,
	int Saves,
	float Innings,
	int Hits,
	int Runs,
	int EarnedRuns,
	int Walks,
	int Strikeouts,
	int HomeRuns,
	float Era,
	float Whip);
