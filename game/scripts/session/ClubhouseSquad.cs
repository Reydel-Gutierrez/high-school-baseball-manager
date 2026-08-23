using System;
using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// Prototype program squad used by Clubhouse screens. Same Cascade players
/// as the roster page, with the season-summary numbers a coach needs while
/// setting a lineup or rotation.
/// </summary>
public static class ClubhouseSquad
{
	public const string GroupPitchers = "pitchers";
	public const string GroupCatchers = "catchers";
	public const string GroupInfield = "infield";
	public const string GroupOutfield = "outfield";

	private static readonly IReadOnlyList<ClubhousePlayer> All = Build();

	public static IReadOnlyList<ClubhousePlayer> Players => All;

	public static List<ClubhousePlayer> ForTeam(TeamLevel level)
	{
		var squad = new List<ClubhousePlayer>();
		foreach (ClubhousePlayer player in All)
		{
			if (level.MatchesSquad(player.Squad))
			{
				squad.Add(player);
			}
		}

		return squad;
	}

	public static ClubhousePlayer? Find(int jersey)
	{
		foreach (ClubhousePlayer player in All)
		{
			if (player.Jersey == jersey)
			{
				return player;
			}
		}

		return null;
	}

	public static string SuggestDefense(ClubhousePlayer player)
	{
		if (player.IsPitcher)
		{
			return "P";
		}

		int slash = player.Position.IndexOf('/');
		string primary = slash > 0 ? player.Position[..slash] : player.Position;
		return primary switch
		{
			"RHP" or "LHP" => "P",
			_ => primary,
		};
	}

	public static bool CanPlayAt(ClubhousePlayer player, string playingPosition)
	{
		if (string.IsNullOrWhiteSpace(playingPosition))
		{
			return true;
		}

		string playing = playingPosition.Trim();
		if (string.Equals(playing, "DH", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		foreach (string token in player.Position.Split('/'))
		{
			string eligible = token.Trim();
			if (eligible.Length == 0)
			{
				continue;
			}

			if (string.Equals(eligible, playing, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (string.Equals(playing, "P", StringComparison.OrdinalIgnoreCase)
				&& (string.Equals(eligible, "RHP", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(eligible, "LHP", StringComparison.OrdinalIgnoreCase)
					|| string.Equals(eligible, "P", StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}
		}

		return false;
	}

	private static List<ClubhousePlayer> Build()
	{
		const string marini = "res://assets/teams/01_Cascade_Regional_High_School/Players/Cascade_player_01_Marini.png";
		return
		[
			P("Luca", "Marini", 11, "RHP", "SR", "R/R", GroupPitchers, 88, 62, 58, 71, 90, 64, "Healthy", marini, "Varsity",
				null, Pit(8, 8, 5, 0, 0, 36.0f, 28, 1.95f, 0.89f, 8)),
			P("Jalen", "Brooks", 7, "CF", "SR", "L/R", GroupOutfield, 84, 82, 71, 91, 80, 86, "Healthy", null, "Varsity",
				Hit(22, 78, 0.397f, 4, 18, 0.468f, 0.692f, 16, 12, 11), null),
			P("Mateo", "Cruz", 21, "C", "JR", "R/R", GroupCatchers, 81, 74, 78, 58, 86, 84, "Healthy", null, "Varsity",
				Hit(21, 70, 0.329f, 5, 22, 0.405f, 0.629f, 1, 9, 13), null),
			P("Drew", "Hale", 4, "SS/2B", "JR", "R/R", GroupInfield, 80, 79, 68, 84, 82, 88, "Healthy", null, "Varsity",
				Hit(22, 76, 0.342f, 2, 15, 0.405f, 0.513f, 9, 8, 12), null),
			P("Isaiah", "Cole", 9, "1B", "SR", "L/L", GroupInfield, 79, 83, 86, 48, 72, 70, "Healthy", null, "Varsity",
				Hit(22, 74, 0.324f, 7, 24, 0.412f, 0.662f, 0, 11, 16), null),
			P("Trey", "Lawson", 32, "LHP", "SR", "L/L", GroupPitchers, 76, 40, 38, 54, 84, 42, "Healthy", null, "Varsity",
				null, Pit(7, 7, 3, 1, 0, 29.1f, 24, 2.66f, 1.26f, 11)),
			P("Noah", "Barrett", 15, "3B/1B", "JR", "R/R", GroupInfield, 77, 76, 81, 62, 85, 74, "Healthy", null, "Varsity",
				Hit(21, 69, 0.304f, 4, 17, 0.368f, 0.551f, 2, 7, 15), null),
			P("Cam", "Whitaker", 2, "2B/SS", "SO", "S/R", GroupInfield, 74, 78, 61, 82, 74, 80, "Healthy", null, "Varsity",
				Hit(20, 61, 0.311f, 1, 9, 0.408f, 0.443f, 11, 10, 9), null),
			P("Eli", "Vargas", 24, "LF/RF", "SR", "L/R", GroupOutfield, 73, 80, 77, 68, 71, 66, "Day-to-Day", null, "Varsity",
				Hit(16, 52, 0.308f, 3, 11, 0.379f, 0.558f, 3, 6, 10), null),
			P("Ryan", "Peck", 18, "RF/LF", "JR", "R/R", GroupOutfield, 72, 75, 79, 70, 83, 69, "Healthy", null, "Varsity",
				Hit(20, 64, 0.281f, 3, 14, 0.352f, 0.500f, 4, 7, 14), null),
			P("Ben", "Ortiz", 8, "RHP", "JR", "R/R", GroupPitchers, 71, 36, 34, 52, 79, 40, "Healthy", null, "Varsity",
				null, Pit(9, 4, 2, 1, 1, 24.2f, 19, 3.28f, 1.26f, 9)),
			P("Jake", "Pruitt", 12, "RHP", "SR", "R/R", GroupPitchers, 74, 34, 32, 50, 82, 38, "Healthy", null, "Varsity",
				null, Pit(8, 7, 4, 2, 0, 31.0f, 26, 3.09f, 1.22f, 10)),
			P("Cole", "Reid", 22, "RHP", "JR", "R/R", GroupPitchers, 73, 38, 36, 56, 80, 40, "Healthy", null, "Varsity",
				null, Pit(10, 2, 2, 1, 2, 22.1f, 21, 3.40f, 1.28f, 8)),
			P("Devin", "Shore", 28, "LHP", "JR", "L/L", GroupPitchers, 70, 36, 34, 52, 78, 38, "Healthy", null, "Varsity",
				null, Pit(10, 1, 1, 2, 1, 18.2f, 16, 3.81f, 1.35f, 7)),
			P("Finn", "Walsh", 16, "C/1B", "SO", "R/R", GroupCatchers, 70, 66, 68, 48, 76, 74, "Healthy", null, "Varsity",
				Hit(16, 48, 0.271f, 2, 11, 0.340f, 0.438f, 0, 6, 12), null),
			P("Marcus", "Bell", 33, "1B/3B", "SR", "R/R", GroupInfield, 72, 74, 80, 42, 68, 64, "Healthy", null, "Varsity",
				Hit(22, 70, 0.286f, 5, 18, 0.351f, 0.500f, 0, 8, 15), null),
			P("Andre", "Holt", 3, "LF/CF/RF", "JR", "L/R", GroupOutfield, 71, 73, 70, 74, 68, 66, "Healthy", null, "Varsity",
				Hit(20, 62, 0.274f, 2, 12, 0.348f, 0.435f, 8, 7, 13), null),
			P("Quinn", "Mercer", 10, "SS/2B", "SO", "R/R", GroupInfield, 69, 70, 58, 80, 74, 78, "Healthy", null, "Varsity",
				Hit(16, 50, 0.260f, 1, 8, 0.339f, 0.380f, 9, 6, 11), null),
			P("Tyler", "Knox", 19, "RF/CF", "SO", "R/R", GroupOutfield, 68, 69, 72, 66, 76, 64, "Healthy", null, "Varsity",
				Hit(16, 49, 0.245f, 2, 9, 0.321f, 0.408f, 3, 5, 12), null),
			P("Miles", "Grant", 5, "RHP", "SO", "R/R", GroupPitchers, 68, 33, 30, 58, 76, 38, "Healthy", null, "JV",
				null, Pit(8, 2, 1, 2, 0, 16.0f, 12, 5.06f, 1.63f, 8)),
			P("Owen", "Drake", 14, "C/1B", "SO", "R/R", GroupCatchers, 67, 64, 70, 50, 74, 72, "Healthy", null, "JV",
				Hit(12, 31, 0.258f, 1, 6, 0.324f, 0.419f, 0, 3, 9), null),
			P("Sam", "Keene", 6, "CF/LF", "FR", "L/R", GroupOutfield, 64, 66, 58, 78, 62, 60, "Healthy", null, "JV",
				Hit(11, 28, 0.250f, 0, 3, 0.344f, 0.357f, 5, 4, 8), null),
			P("Chris", "Nolan", 27, "2B/SS", "FR", "R/R", GroupInfield, 62, 61, 55, 70, 64, 68, "Healthy", null, "JV",
				Hit(9, 22, 0.227f, 0, 2, 0.292f, 0.273f, 1, 2, 7), null),
			P("Leo", "Hayes", 17, "RHP", "FR", "R/R", GroupPitchers, 64, 32, 30, 54, 74, 36, "Healthy", null, "JV",
				null, Pit(4, 3, 1, 2, 0, 12.0f, 8, 4.50f, 1.50f, 6)),
			P("Mason", "Crowe", 23, "LHP", "SO", "L/L", GroupPitchers, 66, 34, 32, 50, 76, 36, "Healthy", null, "JV",
				null, Pit(6, 5, 2, 2, 0, 18.0f, 14, 4.00f, 1.44f, 7)),
			P("Brett", "Lang", 29, "RHP", "FR", "R/R", GroupPitchers, 61, 30, 28, 52, 72, 34, "Healthy", null, "JV",
				null, Pit(7, 1, 1, 1, 0, 10.0f, 7, 5.40f, 1.70f, 6)),
			P("Seth", "Bowman", 40, "RHP", "SO", "R/R", GroupPitchers, 65, 33, 30, 48, 75, 36, "Healthy", null, "JV",
				null, Pit(9, 2, 1, 2, 1, 14.0f, 11, 4.82f, 1.55f, 7)),
			P("Nico", "Palumbo", 13, "C", "FR", "R/R", GroupCatchers, 63, 60, 64, 46, 70, 68, "Healthy", null, "JV",
				Hit(11, 26, 0.231f, 0, 4, 0.310f, 0.308f, 0, 3, 8), null),
			P("Evan", "Briggs", 1, "SS/2B", "SO", "R/R", GroupInfield, 66, 65, 54, 76, 70, 74, "Healthy", null, "JV",
				Hit(16, 48, 0.250f, 0, 6, 0.327f, 0.333f, 7, 5, 10), null),
			P("Cody", "Rhine", 20, "2B/SS", "FR", "S/R", GroupInfield, 60, 62, 48, 74, 62, 70, "Healthy", null, "JV",
				Hit(11, 24, 0.208f, 0, 2, 0.296f, 0.250f, 3, 3, 7), null),
			P("Will", "Hargrove", 25, "3B/1B", "SO", "R/R", GroupInfield, 65, 64, 70, 52, 74, 66, "Healthy", null, "JV",
				Hit(16, 47, 0.255f, 2, 9, 0.327f, 0.404f, 1, 4, 11), null),
			P("Jonah", "Vale", 31, "1B", "FR", "L/L", GroupInfield, 61, 63, 68, 40, 58, 60, "Healthy", null, "JV",
				Hit(11, 28, 0.214f, 1, 5, 0.303f, 0.357f, 0, 4, 8), null),
			P("Alex", "Ruiz", 34, "CF/LF", "SO", "R/R", GroupOutfield, 67, 66, 58, 82, 68, 70, "Healthy", null, "JV",
				Hit(16, 50, 0.260f, 0, 5, 0.333f, 0.340f, 10, 5, 9), null),
			P("Henry", "Cho", 36, "LF/RF", "FR", "L/R", GroupOutfield, 59, 60, 56, 70, 60, 58, "Healthy", null, "JV",
				Hit(11, 26, 0.231f, 0, 3, 0.310f, 0.308f, 2, 3, 8), null),
			P("Parker", "Dean", 38, "RF/LF", "SO", "R/R", GroupOutfield, 64, 63, 66, 62, 72, 60, "Day-to-Day", null, "JV",
				Hit(16, 46, 0.239f, 1, 7, 0.314f, 0.370f, 2, 4, 11), null),
			P("Ian", "Frost", 42, "2B/3B", "FR", "R/R", GroupInfield, 58, 57, 50, 68, 60, 66, "Healthy", null, "JV",
				Hit(11, 22, 0.182f, 0, 2, 0.269f, 0.227f, 1, 3, 7), null),
			P("Luke", "Santos", 44, "LF/CF/RF", "FR", "L/R", GroupOutfield, 57, 58, 52, 72, 58, 56, "Healthy", null, "JV",
				Hit(11, 24, 0.208f, 0, 2, 0.296f, 0.250f, 3, 3, 8), null),
		];
	}

	private static ClubhousePlayer P(
		string first,
		string last,
		int jersey,
		string position,
		string year,
		string batsThrows,
		string group,
		int overall,
		int contact,
		int power,
		int speed,
		int arm,
		int field,
		string status,
		string? portrait,
		string squad,
		HittingLine? hitting,
		PitchingLine? pitching) =>
		new(
			first,
			last,
			jersey,
			position,
			year,
			batsThrows,
			group,
			overall,
			contact,
			power,
			speed,
			arm,
			field,
			status,
			portrait,
			squad,
			hitting,
			pitching);

	private static HittingLine Hit(
		int games,
		int atBats,
		float average,
		int homeRuns,
		int rbi,
		float onBase,
		float slugging,
		int stolenBases,
		int walks,
		int strikeouts) =>
		new(games, atBats, average, homeRuns, rbi, onBase, slugging, stolenBases, walks, strikeouts);

	private static PitchingLine Pit(
		int games,
		int gamesStarted,
		int wins,
		int losses,
		int saves,
		float innings,
		int strikeouts,
		float era,
		float whip,
		int walks) =>
		new(games, gamesStarted, wins, losses, saves, innings, strikeouts, era, whip, walks);
}

public sealed record HittingLine(
	int Games,
	int AtBats,
	float Average,
	int HomeRuns,
	int Rbi,
	float OnBase,
	float Slugging,
	int StolenBases,
	int Walks,
	int Strikeouts)
{
	public float Ops => OnBase + Slugging;
}

public sealed record PitchingLine(
	int Games,
	int GamesStarted,
	int Wins,
	int Losses,
	int Saves,
	float Innings,
	int Strikeouts,
	float Era,
	float Whip,
	int Walks);

public sealed record ClubhousePlayer(
	string FirstName,
	string LastName,
	int Jersey,
	string Position,
	string Year,
	string BatsThrows,
	string Group,
	int Overall,
	int Contact,
	int Power,
	int Speed,
	int Arm,
	int Field,
	string Status,
	string? PortraitPath,
	string Squad,
	HittingLine? Hitting,
	PitchingLine? Pitching)
{
	public bool IsPitcher => Group == ClubhouseSquad.GroupPitchers;

	public bool IsHealthy => string.Equals(Status, "Healthy", StringComparison.OrdinalIgnoreCase);

	public string DisplayLast => LastName.ToUpperInvariant();
}
