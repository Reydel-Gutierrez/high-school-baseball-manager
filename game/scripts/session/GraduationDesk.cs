using System;
using System.Collections.Generic;
using System.Globalization;
using HSBM.Domain;

/// <summary>
/// This spring's graduating seniors: career lines, honors, and the next
/// school. Live careers use archived alumni; prototype clubhouse still
/// reads the current roster.
/// </summary>
public static class GraduationDesk
{
	public static GraduationSnapshot For(GameSession session)
	{
		if (session.World != null && session.HasSignedContract)
		{
			return FromWorld(session);
		}

		return FromPrototype(session);
	}

	private static GraduationSnapshot FromWorld(GameSession session)
	{
		GameWorld world = session.World!;
		int year = GameSession.SeasonYear;
		string orgId = session.Organization.Id;
		bool classPosted = false;
		foreach (Player player in world.Players)
		{
			if (player.Alumni
				&& player.GraduatedYear == year
				&& string.Equals(player.OrganizationId, orgId, StringComparison.Ordinal))
			{
				classPosted = true;
				break;
			}
		}

		IReadOnlyList<ProgramGraduate> source = classPosted
			? GraduateHistory.For(world.Players, world.BattingStats, world.PitchingStats, orgId, year)
			: GraduateHistory.Upcoming(world.Players, world.BattingStats, world.PitchingStats, orgId);

		var graduates = new List<GraduateRecord>(source.Count);
		foreach (ProgramGraduate row in source)
		{
			graduates.Add(ToRecord(session, row));
		}

		return new GraduationSnapshot(year, graduates);
	}

	private static GraduationSnapshot FromPrototype(GameSession session)
	{
		int year = GameSession.SeasonYear;
		var graduates = new List<GraduateRecord>();
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (ClubhousePlayer player in ClubhouseSquad.Players)
		{
			if (!player.Year.Equals("SR", StringComparison.OrdinalIgnoreCase) || !seen.Add(player.FullName))
			{
				continue;
			}

			graduates.Add(FromClubhouse(session, player));
		}

		graduates.Sort((a, b) => b.Overall.CompareTo(a.Overall));
		return new GraduationSnapshot(year, graduates);
	}

	private static GraduateRecord ToRecord(GameSession session, ProgramGraduate row)
	{
		IReadOnlyList<string> honors = HonorsFor(session, row.FullName, row.Overall);
		bool pitcher = row.CareerPitching != null && row.CareerBatting == null;
		GraduateHitting? hitting = row.CareerBatting == null
			? null
			: new GraduateHitting(
				row.CareerBatting.AtBats,
				(float)row.CareerBatting.Average,
				row.CareerBatting.HomeRuns,
				row.CareerBatting.RunsBattedIn,
				row.CareerBatting.StolenBases);
		GraduatePitching? pitching = row.CareerPitching == null
			? null
			: new GraduatePitching(
				row.CareerPitching.Wins,
				row.CareerPitching.Losses,
				row.CareerPitching.Strikeouts,
				(float)row.CareerPitching.Era,
				(float)row.CareerPitching.InningsPitched);
		return new GraduateRecord(
			row.PlayerId,
			row.FirstName,
			row.LastName,
			row.Position,
			row.Overall,
			row.Seasons,
			hitting,
			pitching,
			honors,
			row.Destination,
			Farewell(row.FullName, row.Position, pitcher, honors.Count));
	}

	private static GraduateRecord FromClubhouse(GameSession session, ClubhousePlayer player)
	{
		string fullName = player.FullName;
		CollegeCommit college = CollegeCatalog.Place(fullName, player.Overall, player.Overall + 4);
		IReadOnlyList<string> honors = HonorsFor(session, fullName, player.Overall);
		GraduateHitting? hitting = player.Hitting == null ? null : CareerHitting(player.Hitting);
		GraduatePitching? pitching = player.Pitching == null ? null : CareerPitching(player.Pitching);
		return new GraduateRecord(
			GraduateRecord.StableId(player.FirstName, player.LastName),
			player.FirstName,
			player.LastName,
			player.Position,
			player.Overall,
			4,
			hitting,
			pitching,
			honors,
			college,
			Farewell(fullName, player.Position, player.IsPitcher, honors.Count));
	}

	private static ClubhousePlayer? FindClubhouse(string first, string last)
	{
		foreach (ClubhousePlayer player in ClubhouseSquad.Players)
		{
			if (string.Equals(player.FirstName, first, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(player.LastName, last, StringComparison.OrdinalIgnoreCase))
			{
				return player;
			}
		}

		return null;
	}

	private static IReadOnlyList<string> HonorsFor(GameSession session, string fullName, int overall)
	{
		var honors = new List<string>();
		foreach (string honor in AwardsDesk.HonorsFor(session, fullName))
		{
			honors.Add(honor);
		}

		if (honors.Count == 0 && overall >= 74)
		{
			honors.Add("All-District");
		}

		return honors;
	}

	private static GraduateHitting CareerHitting(HittingLine senior)
	{
		int atBats = Math.Max(senior.AtBats * 3, senior.AtBats);
		int homeRuns = Math.Max(senior.HomeRuns, (int)Math.Round(senior.HomeRuns * 2.8, MidpointRounding.AwayFromZero));
		int rbi = Math.Max(senior.Rbi, (int)Math.Round(senior.Rbi * 3.1, MidpointRounding.AwayFromZero));
		float average = Math.Clamp(senior.Average - 0.012f, 0.220f, 0.380f);
		return new GraduateHitting(atBats, average, homeRuns, rbi, senior.StolenBases * 3);
	}

	private static GraduatePitching CareerPitching(PitchingLine senior)
	{
		int wins = Math.Max(senior.Wins, (int)Math.Round(senior.Wins * 3.2, MidpointRounding.AwayFromZero));
		int losses = Math.Max(senior.Losses, (int)Math.Round(Math.Max(1, senior.Losses) * 2.4, MidpointRounding.AwayFromZero));
		int strikeouts = Math.Max(senior.Strikeouts, (int)Math.Round(senior.Strikeouts * 3.4, MidpointRounding.AwayFromZero));
		float era = Math.Clamp(senior.Era + 0.18f, 1.40f, 5.50f);
		return new GraduatePitching(wins, losses, strikeouts, era, senior.Innings * 3.2f);
	}

	private static string Farewell(string name, string position, bool pitcher, int honorCount)
	{
		string first = name.Split(' ')[0];
		if (honorCount >= 2)
		{
			return pitcher
				? $"{first} gave this program its Fridays. The mound will feel empty next February."
				: $"{first} was the player the lineup turned over for. That does not get replaced in one winter.";
		}

		if (pitcher)
		{
			return $"Four years of innings nobody sees on a highlight. {first} kept this staff together.";
		}

		return $"{first} did the quiet work at {position}. That is how a roster holds through April.";
	}
}

public sealed record GraduationSnapshot(int ClassYear, IReadOnlyList<GraduateRecord> Players)
{
	public int Count => Players.Count;

	public string Headline => $"CLASS OF {ClassYear}";

	public string CountLabel => Count == 1 ? "1 PLAYER GRADUATING" : $"{Count} PLAYERS GRADUATING";
}

public sealed record GraduateRecord(
	Guid Id,
	string FirstName,
	string LastName,
	string Position,
	int Overall,
	int Years,
	GraduateHitting? Hitting,
	GraduatePitching? Pitching,
	IReadOnlyList<string> Honors,
	CollegeCommit Destination,
	string Farewell)
{
	public string FullName => $"{FirstName} {LastName}";

	public string YearsLabel => Years <= 1 ? "1-year career" : $"{Years}-year career";

	public string CareerLine
	{
		get
		{
			if (Pitching != null && Hitting == null)
			{
				return string.Create(CultureInfo.InvariantCulture, $"{Pitching.Era:0.00} ERA · {Pitching.Wins}-{Pitching.Losses} · {Pitching.Strikeouts} K");
			}

			if (Hitting != null)
			{
				return string.Create(CultureInfo.InvariantCulture, $"{Hitting.Average:.000} AVG · {Hitting.HomeRuns} HR · {Hitting.Rbi} RBI");
			}

			return "Career numbers still being archived.";
		}
	}

	public string HonorsLine => Honors.Count == 0 ? "Four years in the program" : string.Join("  ·  ", Honors);

	internal static Guid StableId(string firstName, string lastName)
	{
		unchecked
		{
			int hash = 17;
			foreach (char c in $"{firstName}|{lastName}".ToUpperInvariant())
			{
				hash = (hash * 31) + c;
			}

			byte[] bytes = new byte[16];
			BitConverter.GetBytes(hash).CopyTo(bytes, 0);
			BitConverter.GetBytes(hash ^ 0x5bd1e995).CopyTo(bytes, 4);
			BitConverter.GetBytes((firstName.Length * 7919) + lastName.Length).CopyTo(bytes, 8);
			BitConverter.GetBytes(hash * 37).CopyTo(bytes, 12);
			return new Guid(bytes);
		}
	}
}

public sealed record GraduateHitting(int AtBats, float Average, int HomeRuns, int Rbi, int StolenBases);

public sealed record GraduatePitching(int Wins, int Losses, int Strikeouts, float Era, float Innings);
