namespace HSBM.Domain;

/// <summary>
/// Annual baseball budget minus active player sponsorships. Shared by the
/// user and AI schools so recruiting spend is the same constraint.
/// </summary>
public static class ProgramBudget
{
	public const double RecruitingShare = 0.22;
	public const int MaxAnnualOffer = 1_500;
	public const int MinAnnualOffer = 0;

	public static int CommittedSponsorship(
		IReadOnlyList<PlayerAgreement> agreements,
		string organizationId)
	{
		int total = 0;
		foreach (PlayerAgreement agreement in agreements)
		{
			if (agreement.OrganizationId == organizationId && agreement.YearsRemaining > 0)
			{
				total += agreement.AnnualSponsorship;
			}
		}

		return total;
	}

	public static int Available(
		ProgramProfile profile,
		IReadOnlyList<PlayerAgreement> agreements,
		string organizationId,
		int coachSalary = 0) =>
		Math.Max(0, profile.BaseballBudget - CommittedSponsorship(agreements, organizationId) - Math.Max(0, coachSalary));

	public static int RecruitingPool(int baseballBudget) =>
		Math.Max(0, (int)Math.Round(baseballBudget * RecruitingShare, MidpointRounding.AwayFromZero));

	public static int ReservedOffers(
		IReadOnlyList<RecruitingOffer> offers,
		string organizationId,
		Guid? exceptOfferId = null)
	{
		int total = 0;
		foreach (RecruitingOffer offer in offers)
		{
			if (offer.IsActive
				&& offer.OrganizationId == organizationId
				&& offer.Id != exceptOfferId)
			{
				total += offer.AnnualSponsorship;
			}
		}

		return total;
	}

	public static int RecruitingAvailable(
		ProgramProfile profile,
		IReadOnlyList<PlayerAgreement> agreements,
		string organizationId,
		IReadOnlyList<RecruitingOffer>? offers = null,
		Guid? exceptOfferId = null)
	{
		int pool = RecruitingPool(profile.BaseballBudget);
		int committed = CommittedSponsorship(agreements, organizationId);
		int reserved = offers == null ? 0 : ReservedOffers(offers, organizationId, exceptOfferId);
		return Math.Max(0, pool - committed - reserved);
	}

	public static int ClampOffer(int annual) =>
		Math.Clamp(annual, MinAnnualOffer, MaxAnnualOffer);

	public static int SnapOffer(int annual)
	{
		int clamped = ClampOffer(annual);
		if (clamped <= 0)
		{
			return 0;
		}

		return (int)Math.Round(clamped / 50.0, MidpointRounding.AwayFromZero) * 50;
	}
}
