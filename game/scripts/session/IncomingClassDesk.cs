using System;
using System.Collections.Generic;
using System.Globalization;
using HSBM.Domain;
using HSBM.Simulation;

/// <summary>
/// The program's signed incoming class for the current baseball year.
/// Lives as a working list through Signing Week, then locks after the deadline.
/// </summary>
public static class IncomingClassDesk
{
	public static IncomingClassSnapshot For(GameSession session)
	{
		int year = GameSession.SeasonYear;
		bool finalized = session.CurrentDate > SeasonCalendar.SigningDeadline(year);
		string orgId = session.CatalogOrganizationId;
		RecruitingMarket market = session.RecruitingMarket;
		var seen = new HashSet<Guid>();
		var players = new List<IncomingClassPlayer>();

		foreach (PlayerAgreement agreement in market.Agreements)
		{
			if (agreement.OrganizationId != orgId || agreement.StartSeasonYear != year)
			{
				continue;
			}

			Player? prospect = session.FindProspect(agreement.PlayerId);
			if (prospect == null || !seen.Add(prospect.Id))
			{
				continue;
			}

			players.Add(FromPlayer(prospect, agreement, session.OpenPlay, session.CurrentDate, finalized));
		}

		foreach (FinalRosterPlayer row in session.RosterBoard.Players)
		{
			if (!row.FromTryout || !seen.Add(row.Id))
			{
				continue;
			}

			Player? prospect = session.FindProspect(row.Id);
			if (prospect != null)
			{
				PlayerAgreement? agreement = market.AgreementFor(row.Id);
				players.Add(FromPlayer(prospect, agreement, session.OpenPlay, session.CurrentDate, finalized));
			}
			else
			{
				players.Add(new IncomingClassPlayer(
					row.Id,
					row.FirstName,
					row.LastName,
					row.PositionLabel,
					row.Year.ToAbbrev(),
					row.Overall,
					row.Potential,
					ProgramRosterRules.AssignmentLabel(row.Assignment),
					null,
					null,
					0));
			}
		}

		players.Sort((a, b) =>
		{
			int overall = b.Overall.CompareTo(a.Overall);
			return overall != 0 ? overall : string.Compare(a.LastName, b.LastName, StringComparison.Ordinal);
		});

		return new IncomingClassSnapshot(year, finalized, players);
	}

	private static IncomingClassPlayer FromPlayer(
		Player player,
		PlayerAgreement? agreement,
		DistrictOpenPlay openPlay,
		DateOnly today,
		bool finalized)
	{
		int looks = Math.Max(1, openPlay.LooksOn(today));
		bool reveal = finalized || agreement != null;
		string overallLabel = reveal
			? player.Overall.ToString(CultureInfo.InvariantCulture)
			: TryoutScouting.FormatOverall(player.Overall, looks, player.Id.GetHashCode());
		string potentialLabel = reveal
			? player.Potential.ToString(CultureInfo.InvariantCulture)
			: TryoutScouting.FormatPotential(player.Overall, player.Potential, looks, player.Id.GetHashCode());
		string squad = player.IsUnsigned
			? "Unsigned"
			: player.TeamLevel == TeamLevel.Varsity ? "Varsity" : "JV";
		return new IncomingClassPlayer(
			player.Id,
			player.FirstName,
			player.LastName,
			player.DisplayPosition,
			player.Grade.ToAbbrev(),
			player.Overall,
			player.Potential,
			squad,
			overallLabel,
			potentialLabel,
			agreement?.AnnualSponsorship ?? 0);
	}
}

public sealed record IncomingClassSnapshot(
	int SeasonYear,
	bool Finalized,
	IReadOnlyList<IncomingClassPlayer> Players)
{
	public int Count => Players.Count;

	public string Headline => $"{SeasonYear} INCOMING CLASS";

	public string CountLabel => Count == 1 ? "1 NEW PLAYER" : $"{Count} NEW PLAYERS";

	public string StatusLabel => Finalized ? "CLASS FINALIZED" : "SIGNING IN PROGRESS";
}

public sealed record IncomingClassPlayer(
	Guid Id,
	string FirstName,
	string LastName,
	string Position,
	string Year,
	int Overall,
	int Potential,
	string Squad,
	string? OverallLabel,
	string? PotentialLabel,
	int AnnualSponsorship)
{
	public string FullName => $"{FirstName} {LastName}";

	public string OverallText => string.IsNullOrEmpty(OverallLabel)
		? Overall.ToString(CultureInfo.InvariantCulture)
		: OverallLabel;

	public string PotentialText => string.IsNullOrEmpty(PotentialLabel)
		? Potential.ToString(CultureInfo.InvariantCulture)
		: PotentialLabel;
}
