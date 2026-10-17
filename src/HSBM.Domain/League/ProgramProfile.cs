namespace HSBM.Domain;

/// <summary>
/// Public face of a program. Prestige is live reputation, not a fixed
/// power ranking; <see cref="OrganizationState.TalentTier"/> follows it.
/// </summary>
public sealed class ProgramProfile
{
	public const int SeasonGames = 26;

	public int Reputation { get; set; } = 50;
	public LetterGrade Facilities { get; set; } = LetterGrade.C;
	public LetterGrade BaseballResources { get; set; } = LetterGrade.C;
	public int BaseballBudget { get; set; } = 40_000;
	public int LastSeasonWins { get; set; } = 13;
	public int LastSeasonLosses { get; set; } = 13;
	public int DistrictFinish { get; set; } = 4;
	public int DistrictTitles { get; set; }
	public int RegionalTitles { get; set; }
	public int StateTitles { get; set; }
	public BoardExpectation MinimumExpectation { get; set; } = BoardExpectation.WinningSeason;
	public BoardExpectation TargetExpectation { get; set; } = BoardExpectation.Compete;
	public bool HasOpening { get; set; }
	public VacancyReason VacancyReason { get; set; }

	public string LastSeasonLabel => $"{LastSeasonWins}\u2013{LastSeasonLosses}";

	public string BudgetLabel => $"${BaseballBudget / 1000}K";

	public string DistrictFinishLabel => Ordinal.Format(DistrictFinish);

	public ProgramResources Resources => new(
		BaseballBudget,
		Facilities,
		BaseballResources,
		MinimumExpectation,
		TargetExpectation);
}

/// <summary>
/// Budgets, facilities, baseball resources, and board expectations.
/// </summary>
public sealed record ProgramResources(
	int BaseballBudget,
	LetterGrade Facilities,
	LetterGrade BaseballResources,
	BoardExpectation MinimumExpectation,
	BoardExpectation TargetExpectation)
{
	public string BudgetLabel => $"${BaseballBudget / 1000}K";
}

public static class Ordinal
{
	public static string Format(int value)
	{
		int abs = Math.Abs(value);
		int lastTwo = abs % 100;
		int last = abs % 10;
		string suffix = lastTwo is 11 or 12 or 13
			? "TH"
			: last == 1 ? "ST"
			: last == 2 ? "ND"
			: last == 3 ? "RD"
			: "TH";
		return $"{value}{suffix}";
	}
}

public static class OrganizationNaming
{
	public static string DisplayName(string fullName)
	{
		const string suffix = " High School";
		return fullName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
			? fullName[..^suffix.Length]
			: fullName;
	}
}
