using System.Globalization;

namespace HSBM.Domain;

/// <summary>
/// Persistent commitment after a player signs. Annual sponsorship hits
/// the program budget for each remaining season.
/// </summary>
public sealed class PlayerAgreement
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid PlayerId { get; set; }
	public string OrganizationId { get; set; } = "";
	public int StartSeasonYear { get; set; }
	public int TermYears { get; set; }
	public int YearsRemaining { get; set; }
	public int AnnualSponsorship { get; set; }

	public int TotalCommitment => Math.Max(0, AnnualSponsorship) * Math.Max(1, TermYears);

	public string AnnualLabel => FormatMoney(AnnualSponsorship);

	public string TotalLabel => FormatMoney(TotalCommitment);

	public string TermLabel =>
		YearsRemaining <= 1
			? $"{YearsRemaining} year remaining"
			: $"{YearsRemaining} years remaining";

	public static PlayerAgreement FromOffer(RecruitingOffer offer) =>
		new()
		{
			Id = Guid.NewGuid(),
			PlayerId = offer.PlayerId,
			OrganizationId = offer.OrganizationId,
			StartSeasonYear = offer.SeasonYear,
			TermYears = offer.TermYears,
			YearsRemaining = offer.TermYears,
			AnnualSponsorship = offer.AnnualSponsorship,
		};

	public static string FormatMoney(int amount)
	{
		if (amount <= 0)
		{
			return "$0";
		}

		return "$" + amount.ToString("N0", CultureInfo.InvariantCulture);
	}
}
