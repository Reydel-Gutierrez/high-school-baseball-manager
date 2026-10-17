namespace HSBM.Domain;

/// <summary>
/// The sitting deal: term is job security, salary comes out of the
/// program baseball budget.
/// </summary>
public sealed class CoachContract
{
	public string OrganizationId { get; set; } = "";
	public string SchoolName { get; set; } = "";
	public int StartYear { get; set; }
	public int TermYears { get; set; }
	public int YearsRemaining { get; set; }
	public int AnnualSalary { get; set; }

	public bool IsActive => TermYears > 0 && AnnualSalary > 0;

	public int YearOnDeal
	{
		get
		{
			if (TermYears <= 0)
			{
				return 1;
			}

			if (YearsRemaining <= 0)
			{
				return TermYears;
			}

			return Math.Clamp(TermYears - YearsRemaining + 1, 1, TermYears);
		}
	}

	public string TermLabel => TermYears == 1 ? "1 Season" : $"{TermYears} Seasons";

	public string SalaryLabel => PlayerAgreement.FormatMoney(AnnualSalary);

	public string YearLabel => $"Year {YearOnDeal} of {Math.Max(1, TermYears)}";

	public string SalaryLine => $"{SalaryLabel} / year";

	public void TickSeason()
	{
		if (YearsRemaining > 0)
		{
			YearsRemaining--;
		}
	}

	public CoachContract Clone() => new()
	{
		OrganizationId = OrganizationId,
		SchoolName = SchoolName,
		StartYear = StartYear,
		TermYears = TermYears,
		YearsRemaining = YearsRemaining,
		AnnualSalary = AnnualSalary,
	};

	public static CoachContract For(OrganizationState org, int seasonYear) => new()
	{
		OrganizationId = org.Id,
		SchoolName = org.DisplayName,
		StartYear = seasonYear,
		TermYears = CoachSalary.TermYears(org.Profile),
		YearsRemaining = CoachSalary.TermYears(org.Profile),
		AnnualSalary = CoachSalary.From(org.Profile),
	};
}

public enum CoachingJobOfferStatus
{
	Pending = 0,
	Accepted = 1,
	Declined = 2,
}

/// <summary>
/// An inbound call: another program wants the sitting coach and has put
/// term and salary on paper.
/// </summary>
public sealed class CoachingJobOffer
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string OrganizationId { get; set; } = "";
	public string SchoolName { get; set; } = "";
	public string SchoolFullName { get; set; } = "";
	public int Reputation { get; set; }
	public LetterGrade Facilities { get; set; } = LetterGrade.C;
	public BoardExpectation TargetExpectation { get; set; } = BoardExpectation.Compete;
	public int TermYears { get; set; } = 3;
	public int AnnualSalary { get; set; }
	public CoachingJobOfferStatus Status { get; set; }

	public bool IsPending => Status == CoachingJobOfferStatus.Pending;

	public string TermLabel => TermYears == 1 ? "1 Season" : $"{TermYears} Seasons";

	public string SalaryLabel => PlayerAgreement.FormatMoney(AnnualSalary);

	public string ExpectationLabel => TargetExpectation.ToLabel();

	public string Preview => $"{SchoolName} wants a head coach.";

	public CoachContract ToContract(int seasonYear) => new()
	{
		OrganizationId = OrganizationId,
		SchoolName = SchoolName,
		StartYear = seasonYear,
		TermYears = TermYears,
		YearsRemaining = TermYears,
		AnnualSalary = AnnualSalary,
	};

	public CoachingJobOffer Clone() => new()
	{
		Id = Id,
		OrganizationId = OrganizationId,
		SchoolName = SchoolName,
		SchoolFullName = SchoolFullName,
		Reputation = Reputation,
		Facilities = Facilities,
		TargetExpectation = TargetExpectation,
		TermYears = TermYears,
		AnnualSalary = AnnualSalary,
		Status = Status,
	};
}

/// <summary>
/// Head-coach pay is a slice of the baseball budget — not a separate
/// purse. Better programs pay more and offer longer deals.
/// </summary>
public static class CoachSalary
{
	public const double MinShare = 0.10;
	public const double MaxShare = 0.16;
	public const int MinAnnual = 3_500;
	public const int MaxAnnual = 18_000;

	public static int From(ProgramProfile profile) =>
		From(profile.Reputation, profile.BaseballBudget);

	public static int From(int reputation, int baseballBudget)
	{
		double share = MinShare + (Math.Clamp(reputation, 1, 99) / 100.0) * (MaxShare - MinShare);
		int raw = (int)Math.Round(baseballBudget * share, MidpointRounding.AwayFromZero);
		int snapped = (int)Math.Round(raw / 500.0, MidpointRounding.AwayFromZero) * 500;
		return Math.Clamp(snapped, MinAnnual, MaxAnnual);
	}

	public static int TermYears(ProgramProfile profile) => TermYears(profile.TargetExpectation);

	public static int TermYears(BoardExpectation target) => target switch
	{
		BoardExpectation.StateTitle => 4,
		BoardExpectation.WinDistrict => 3,
		BoardExpectation.DistrictPlayoffs => 3,
		BoardExpectation.Compete => 3,
		_ => 2,
	};

	public static string TermLabel(int years) => years == 1 ? "1 Season" : $"{years} Seasons";
}
