using HSBM.Domain;

namespace HSBM.Application;

public sealed record JobListing(
	string OrganizationId,
	string SchoolName,
	string SchoolFullName,
	string DistrictName,
	int Reputation,
	int LastSeasonWins,
	int LastSeasonLosses,
	BoardExpectation TargetExpectation,
	BoardExpectation MinimumExpectation,
	LetterGrade Facilities,
	int BaseballBudget,
	int DistrictFinish,
	int DistrictTitles,
	int RegionalTitles,
	int StateTitles,
	LetterGrade PitchingOutlook,
	LetterGrade HittingOutlook,
	LetterGrade DefenseOutlook,
	LetterGrade RosterOutlook,
	bool HasOpening,
	VacancyReason VacancyReason,
	bool InReach)
{
	public const string OpenStatus = "OPEN";

	public string LastYearLabel => $"{LastSeasonWins}\u2013{LastSeasonLosses}";

	public string BudgetLabel => $"${BaseballBudget / 1000}K";

	public string DistrictFinishLabel => Ordinal.Format(DistrictFinish);

	public string ExpectationLabel => TargetExpectation.ToShortLabel();

	public string VacancyLabel => VacancyReason.ToLabel();

	public int AnnualCoachSalary => CoachSalary.From(Reputation, BaseballBudget);

	public int ContractTermYears => CoachSalary.TermYears(TargetExpectation);

	public string ContractTermLabel => CoachSalary.TermLabel(ContractTermYears);

	public string CoachSalaryLabel => PlayerAgreement.FormatMoney(AnnualCoachSalary);

	public bool Matches(string query)
	{
		if (string.IsNullOrWhiteSpace(query))
		{
			return true;
		}

		string needle = query.Trim();
		return SchoolName.Contains(needle, StringComparison.OrdinalIgnoreCase)
			|| DistrictName.Contains(needle, StringComparison.OrdinalIgnoreCase)
			|| ExpectationLabel.Contains(needle, StringComparison.OrdinalIgnoreCase)
			|| VacancyLabel.Contains(needle, StringComparison.OrdinalIgnoreCase);
	}
}

public sealed class SignContractResult
{
	public required string OrganizationId { get; init; }
	public required string Name { get; init; }
	public required string ShortName { get; init; }
	public required string DisplayName { get; init; }
}

public static class JobBoard
{
	public static IReadOnlyList<JobListing> From(CareerState state)
	{
		var districts = state.Districts.ToDictionary(d => d.Id, d => d.Name, StringComparer.Ordinal);
		var varsityByOrg = state.Teams
			.Where(team => team.Level == TeamLevel.Varsity)
			.ToDictionary(team => team.OrganizationId, team => team.Id, StringComparer.Ordinal);
		var playersByTeam = state.Players
			.Where(player => !player.Alumni)
			.GroupBy(player => player.TeamId)
			.ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

		int coachReputation = state.Career.Reputation;
		string userOrg = state.Career.UserOrganizationId;
		var listings = new List<JobListing>(state.Organizations.Count);
		foreach (OrganizationState org in state.Organizations)
		{
			if (LeagueCatalog.IsUnsignedOrganization(org.Id)
				|| string.Equals(org.Id, userOrg, StringComparison.Ordinal))
			{
				continue;
			}

			ProgramProfile profile = org.Profile;
			playersByTeam.TryGetValue(varsityByOrg.GetValueOrDefault(org.Id, ""), out List<Player>? roster);
			roster ??= [];
			LetterGrade pitching = Outlook.Pitching(roster);
			LetterGrade hitting = Outlook.Hitting(roster);
			LetterGrade defense = Outlook.Defense(roster);
			listings.Add(new JobListing(
				org.Id,
				org.DisplayName,
				org.Name,
				districts.GetValueOrDefault(org.DistrictId, org.DistrictId),
				profile.Reputation,
				profile.LastSeasonWins,
				profile.LastSeasonLosses,
				profile.TargetExpectation,
				profile.MinimumExpectation,
				profile.Facilities,
				profile.BaseballBudget,
				profile.DistrictFinish,
				profile.DistrictTitles,
				profile.RegionalTitles,
				profile.StateTitles,
				pitching,
				hitting,
				defense,
				LetterGradeExtensions.Blend(pitching, hitting, defense),
				profile.HasOpening,
				profile.VacancyReason,
				CoachReputation.CanReach(coachReputation, profile.Reputation)));
		}

		return listings
			.Where(job => job.HasOpening)
			.OrderByDescending(job => job.Reputation)
			.ThenBy(job => job.SchoolName, StringComparer.Ordinal)
			.ToList();
	}

	private static class Outlook
	{
		public static LetterGrade Pitching(IReadOnlyList<Player> roster)
		{
			List<Player> pitchers = roster.Where(player => player.IsPitcher).ToList();
			if (pitchers.Count == 0)
			{
				return LetterGrade.C;
			}

			return LetterGradeExtensions.FromScore(pitchers.Average(player => PlayerOverall.PitchingRating(player.Ratings)));
		}

		public static LetterGrade Hitting(IReadOnlyList<Player> roster)
		{
			List<Player> hitters = roster.Where(player => !player.IsPitcher).ToList();
			if (hitters.Count == 0)
			{
				return LetterGrade.C;
			}

			return LetterGradeExtensions.FromScore(hitters.Average(player => PlayerOverall.OffensiveRating(player.Ratings)));
		}

		public static LetterGrade Defense(IReadOnlyList<Player> roster)
		{
			List<Player> fielders = roster.Where(player => !player.IsPitcher).ToList();
			if (fielders.Count == 0)
			{
				return LetterGrade.C;
			}

			return LetterGradeExtensions.FromScore(fielders.Average(player => player.Ratings.Fielding));
		}
	}
}
