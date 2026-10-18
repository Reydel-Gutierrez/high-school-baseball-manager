using System;
using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// Year-turn recap shown before January 5 rolls the baseball year:
/// graduates, honors, the final record, and what changed for the chair.
/// </summary>
public static class SeasonRolloverDesk
{
	public static bool PendingOn(GameSession session) =>
		session.HasSignedContract
		&& !session.SeasonRolloverAcknowledged
		&& CareerCalendar.IsRolloverDay(GameSession.SeasonYear, session.CurrentDate.AddDays(1));

	public static SeasonRolloverSheet For(GameSession session)
	{
		int year = GameSession.SeasonYear;
		SchoolSnapshot school = SchoolProfiles.For(session.CatalogOrganizationId, TeamLevel.Varsity, session);
		OrganizationState? program = session.SchoolState(session.CatalogOrganizationId);
		ProgramSeasonResult? archived = LatestSeason(program, year);
		string record = archived?.RecordLabel ?? session.RecordLabelFor(TeamLevel.Varsity);
		string place = archived != null
			? Ordinal.Format(archived.DistrictFinish)
			: Ordinal.Format(school.DistrictPlace);
		string postseason = archived != null
			? archived.Postseason
			: SeasonReview.PostseasonLabel(session.WonDistrict, session.WonRegional, session.WonState, school.DistrictPlace);

		var honors = new List<SeasonRolloverHonor>();
		foreach (AwardScope scope in new[] { AwardScope.District, AwardScope.State })
		{
			AwardsCeremony banquet = AwardsDesk.For(session, scope);
			foreach (AwardHonor honor in banquet.Honors)
			{
				if (honor.Winner == null
					|| !string.Equals(honor.Winner.TeamId, session.CatalogOrganizationId, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				honors.Add(new SeasonRolloverHonor(
					honor.Name,
					honor.Winner.Name,
					honor.Winner.HeadlineStat,
					scope == AwardScope.State ? "STATE" : "DISTRICT"));
			}
		}

		var graduates = new List<SeasonRolloverGraduate>();
		foreach (GraduateRecord row in GraduationDesk.For(session).Players)
		{
			graduates.Add(new SeasonRolloverGraduate(
				row.FullName,
				row.Position,
				row.Destination.IsAssigned ? row.Destination.Label : "Unsigned",
				row.Overall));
			if (graduates.Count >= 8)
			{
				break;
			}
		}

		var developments = new List<string>
		{
			$"Board: {BoardGoals.ScoreLine(session.BoardSatisfactionScore)}.",
			$"Contract year {session.ContractYear} of {session.ContractLength}  ·  {session.CareerRecordLabel} career.",
		};
		if (program != null)
		{
			developments.Add($"Program reputation {program.Profile.Reputation}  ·  budget {program.Profile.BudgetLabel}.");
		}

		if (session.PendingJobOffers.Count > 0)
		{
			developments.Add($"{session.PendingJobOffers.Count} inbound job call{(session.PendingJobOffers.Count == 1 ? "" : "s")} still on the desk.");
		}

		return new SeasonRolloverSheet(
			year,
			session.Organization.Name.ToUpperInvariant(),
			record,
			place,
			postseason,
			graduates,
			honors,
			developments);
	}

	private static ProgramSeasonResult? LatestSeason(OrganizationState? program, int year)
	{
		if (program == null || program.SeasonHistory.Count == 0)
		{
			return null;
		}

		ProgramSeasonResult? match = null;
		foreach (ProgramSeasonResult season in program.SeasonHistory)
		{
			if (season.SeasonYear == year)
			{
				return season;
			}

			if (match == null || season.SeasonYear > match.SeasonYear)
			{
				match = season;
			}
		}

		return match;
	}
}

public sealed record SeasonRolloverSheet(
	int SeasonYear,
	string SchoolName,
	string RecordLabel,
	string DistrictPlace,
	string Postseason,
	IReadOnlyList<SeasonRolloverGraduate> Graduates,
	IReadOnlyList<SeasonRolloverHonor> Honors,
	IReadOnlyList<string> Developments);

public sealed record SeasonRolloverGraduate(string Name, string Position, string Destination, int Overall);

public sealed record SeasonRolloverHonor(string Award, string PlayerName, string Stat, string Scope);
