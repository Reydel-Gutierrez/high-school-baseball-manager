using System;
using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// End-of-year program Hall of Fame: one inductee, announced after
/// board review, then kept on the Career desk wall.
/// </summary>
public static class HallOfFameDesk
{
	public static bool PendingOn(GameSession session) =>
		session.HasSignedContract
		&& session.SeasonReviewAcknowledged
		&& !session.HallOfFameAcknowledged
		&& ThisYear(session) != null;

	public static ProgramHallOfFameInductee? ThisYear(GameSession session)
	{
		OrganizationState? school = ProgramFor(session);
		if (school == null)
		{
			return null;
		}

		int year = GameSession.SeasonYear;
		foreach (ProgramHallOfFameInductee inductee in school.HallOfFame)
		{
			if (inductee.InductedYear == year)
			{
				return inductee;
			}
		}

		return null;
	}

	public static IReadOnlyList<ProgramHallOfFameInductee> Wall(GameSession session)
	{
		OrganizationState? school = ProgramFor(session);
		if (school == null)
		{
			return [];
		}

		return school.HallOfFame;
	}

	public static OrganizationState? ProgramFor(GameSession session) =>
		session.LeagueProgram(session.CatalogOrganizationId)
		?? session.SchoolState(session.CatalogOrganizationId);

	public static ProgramHallOfFameInductee? Select(GameSession session)
	{
		OrganizationState? school = ProgramFor(session);
		if (school == null)
		{
			return null;
		}

		GraduateRecord? pick = null;
		int best = int.MinValue;
		foreach (GraduateRecord graduate in GraduationDesk.For(session).Players)
		{
			int score = Score(session, graduate);
			if (score > best)
			{
				best = score;
				pick = graduate;
			}
		}

		if (pick == null)
		{
			return null;
		}

		int year = GameSession.SeasonYear;
		int years = Math.Clamp(pick.Years, 1, 4);
		return new ProgramHallOfFameInductee
		{
			OrganizationId = school.Id,
			InductedYear = year,
			PlayerId = pick.Id,
			PlayerName = pick.FullName,
			Position = pick.Position,
			FirstSeasonYear = ProgramHallOfFame.FirstSeasonYear(year, years),
			LastSeasonYear = year,
			CareerLine = PlaqueLine(pick),
			Honors = ProgramHallOfFame.JoinHonors(PlaqueHonors(session, pick)),
		};
	}

	private static int Score(GameSession session, GraduateRecord graduate)
	{
		int score = graduate.Overall * 4;
		foreach (string honor in graduate.Honors)
		{
			score += 18;
			if (honor.Contains("MVP", StringComparison.OrdinalIgnoreCase))
			{
				score += 28;
			}

			if (honor.Contains("Pitcher of the Year", StringComparison.OrdinalIgnoreCase))
			{
				score += 22;
			}
		}

		if (session.WonState)
		{
			score += 24;
		}
		else if (session.WonRegional)
		{
			score += 12;
		}
		else if (session.WonDistrict)
		{
			score += 8;
		}

		return score;
	}

	private static List<string> PlaqueHonors(GameSession session, GraduateRecord graduate)
	{
		var honors = new List<string>();
		int districtMvp = 0;
		foreach (string honor in graduate.Honors)
		{
			if (honor.Contains("District MVP", StringComparison.OrdinalIgnoreCase))
			{
				districtMvp++;
				continue;
			}

			honors.Add(honor);
		}

		if (districtMvp >= 2)
		{
			honors.Insert(0, $"{districtMvp}\u00d7 District MVP");
		}
		else if (districtMvp == 1)
		{
			honors.Insert(0, "District MVP");
		}

		if (session.WonState)
		{
			honors.Add("State Champion");
		}
		else if (session.WonDistrict)
		{
			honors.Add("District Champion");
		}

		if (honors.Count == 0)
		{
			honors.Add("All-District");
		}

		if (honors.Count > 3)
		{
			honors.RemoveRange(3, honors.Count - 3);
		}

		return honors;
	}

	private static string PlaqueLine(GraduateRecord graduate)
	{
		if (graduate.Pitching != null && (graduate.Hitting == null || graduate.Position is "P" or "HP" or "SP" or "RP"))
		{
			return ProgramHallOfFame.PitchingLine(
				graduate.Pitching.Wins,
				graduate.Pitching.Losses,
				graduate.Pitching.Era);
		}

		if (graduate.Hitting != null)
		{
			return ProgramHallOfFame.BattingLine(graduate.Hitting.Average);
		}

		return "Career numbers archived.";
	}
}
