using System;
using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// Copy and road-to-the-title steps for championship celebration screens.
/// Playoff sim should set the win flags on GameSession; this desk only
/// presents what was already won.
/// </summary>
public static class ChampionshipDesk
{
	public static ChampionshipSheet For(GameSession session, ChampionshipTitle title)
	{
		DistrictIdentity district = DistrictFor(session);
		string school = session.Organization.Name.ToUpperInvariant();
		int year = GameSession.SeasonYear;
		string record = session.RecordLabel;
		IReadOnlyList<ChampionshipRoadStep> road =
		[
			new("District Champions", title >= ChampionshipTitle.District, title == ChampionshipTitle.District),
			new("Regional Champions", title >= ChampionshipTitle.Regional, title == ChampionshipTitle.Regional),
			new("State Champions", title >= ChampionshipTitle.State, title == ChampionshipTitle.State),
		];

		return title switch
		{
			ChampionshipTitle.District => new ChampionshipSheet(
				title,
				year,
				$"{year} DISTRICT CHAMPIONS",
				school,
				record,
				$"{district.Name.ToUpperInvariant()} DISTRICT",
				"CONTINUE TO REGIONALS",
				road),
			ChampionshipTitle.Regional => new ChampionshipSheet(
				title,
				year,
				$"{year} REGIONAL CHAMPIONS",
				school,
				record,
				$"{district.Region.ToUpperInvariant()} REGIONAL",
				"CONTINUE TO STATE",
				road),
			ChampionshipTitle.State => new ChampionshipSheet(
				title,
				year,
				$"{year} STATE CHAMPIONS",
				school,
				record,
				"STATE CHAMPIONSHIP",
				"CONTINUE TO STATE AWARDS",
				road),
			_ => throw new ArgumentOutOfRangeException(nameof(title), title, "Unknown championship title."),
		};
	}

	public static ChampionshipTitle? PendingOn(GameSession session)
	{
		ChampionshipTitle? title = SeasonCalendar.ChampionshipOn(GameSession.SeasonYear, session.CurrentDate);
		if (title == null || session.ChampionshipAcknowledged(title.Value) || !session.Won(title.Value))
		{
			return null;
		}

		return title;
	}

	private static DistrictIdentity DistrictFor(GameSession session)
	{
		LeagueCatalog catalog = LeagueCatalog.Standard;
		string districtId = catalog.DistrictIdFor(session.CatalogOrganizationId);
		foreach (DistrictIdentity district in catalog.Districts)
		{
			if (district.Id == districtId)
			{
				return district;
			}
		}

		return catalog.Districts[0];
	}
}

public sealed record ChampionshipSheet(
	ChampionshipTitle Title,
	int SeasonYear,
	string Headline,
	string SchoolName,
	string RecordLabel,
	string ScopeLabel,
	string ContinueLabel,
	IReadOnlyList<ChampionshipRoadStep> Road);

public sealed record ChampionshipRoadStep(string Label, bool Earned, bool Current);
