namespace HSBM.Domain;

public enum RecruitingOfferStatus
{
	Pending = 0,
	Accepted = 1,
	Declined = 2,
	Withdrawn = 3,
	Expired = 4,
}

public sealed class RecruitingOffer
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid PlayerId { get; set; }
	public string OrganizationId { get; set; } = "";
	public int SeasonYear { get; set; }
	public int AnnualSponsorship { get; set; }
	public int TermYears { get; set; } = 4;
	public DateOnly OfferedOn { get; set; }
	public RecruitingOfferStatus Status { get; set; }

	public int TotalValue => Math.Max(0, AnnualSponsorship) * Math.Max(1, TermYears);

	public bool IsActive => Status == RecruitingOfferStatus.Pending;
}
