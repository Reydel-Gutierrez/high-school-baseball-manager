namespace HSBM.Domain;

/// <summary>
/// Live program prestige. This is the public ranking — not a fixed
/// talent band — and it moves with results, titles, and missed targets.
/// </summary>
public static class ProgramReputation
{
	public const int Min = 15;
	public const int Max = 95;

	public static int Clamp(int value)
	{
		if (value < Min)
		{
			return Min;
		}

		return value > Max ? Max : value;
	}

	public static int AfterSeason(
		int opening,
		int wins,
		int losses,
		int districtFinish,
		bool wonDistrict,
		bool wonRegional,
		bool wonState,
		bool targetMet)
	{
		bool winning = wins > losses;
		bool playoffs = districtFinish <= 4 || wonDistrict;
		int gain = SeasonReview.ReputationGain(
			winning,
			playoffs,
			wonDistrict,
			wonRegional,
			wonState,
			targetMet);
		return Clamp(opening + gain);
	}

	public static int AfterSeason(int opening, ProgramSeasonResult season, bool targetMet) =>
		AfterSeason(
			opening,
			season.Wins,
			season.Losses,
			season.DistrictFinish,
			season.WonDistrict,
			season.WonRegional,
			season.WonState,
			targetMet);

	public static ProgramTalentTier TalentBand(int reputation)
	{
		int clamped = Clamp(reputation);
		return clamped switch
		{
			>= 78 => ProgramTalentTier.Powerhouse,
			>= 64 => ProgramTalentTier.AboveAverage,
			>= 50 => ProgramTalentTier.Average,
			>= 36 => ProgramTalentTier.BelowAverage,
			_ => ProgramTalentTier.Rebuilding,
		};
	}

	public static bool IsPowerhouse(int reputation) => TalentBand(reputation) == ProgramTalentTier.Powerhouse;

	public static bool IsRebuilding(int reputation) => TalentBand(reputation) <= ProgramTalentTier.BelowAverage;

	public static BoardExpectation TargetExpectation(int reputation, int lastSeasonWins)
	{
		int games = Math.Max(1, ProgramProfile.SeasonGames);
		double winningPct = Math.Clamp(lastSeasonWins / (double)games, 0, 1);
		double strength = (Clamp(reputation) / 100.0) * 0.55 + winningPct * 0.45;
		if (strength >= 0.82)
		{
			return BoardExpectation.StateTitle;
		}

		if (strength >= 0.70)
		{
			return BoardExpectation.WinDistrict;
		}

		if (strength >= 0.58)
		{
			return BoardExpectation.DistrictPlayoffs;
		}

		if (strength >= 0.48)
		{
			return BoardExpectation.Compete;
		}

		if (strength >= 0.36)
		{
			return BoardExpectation.WinningSeason;
		}

		return BoardExpectation.Rebuild;
	}

	public static (BoardExpectation Minimum, BoardExpectation Target) SeasonObjectives(
		int reputation,
		int lastSeasonWins)
	{
		BoardExpectation target = TargetExpectation(reputation, lastSeasonWins);
		return (target.StepDown(), target);
	}

	public static void RefreshExpectations(ProgramProfile profile) =>
		(profile.MinimumExpectation, profile.TargetExpectation) =
			SeasonObjectives(profile.Reputation, profile.LastSeasonWins);

	public static void ApplySeason(OrganizationState org, ProgramSeasonResult season)
	{
		ProgramProfile profile = org.Profile;
		bool targetMet = SeasonReview.TargetMet(
			profile.TargetExpectation,
			season.Wins,
			season.Losses,
			season.DistrictFinish,
			season.WonDistrict,
			season.WonRegional,
			season.WonState);
		profile.LastSeasonWins = season.RegularWins;
		profile.LastSeasonLosses = season.RegularLosses;
		profile.DistrictFinish = season.DistrictFinish;
		if (season.WonDistrict)
		{
			profile.DistrictTitles++;
		}

		if (season.WonRegional)
		{
			profile.RegionalTitles++;
		}

		if (season.WonState)
		{
			profile.StateTitles++;
		}

		profile.Reputation = AfterSeason(profile.Reputation, season, targetMet);
		profile.BaseballBudget = Math.Max(
			0,
			profile.BaseballBudget + SeasonReview.FinanceDelta(
				season.Wins,
				season.Losses,
				season.DistrictFinish <= 4 || season.WonDistrict,
				season.WonDistrict,
				season.WonRegional,
				season.WonState));
		RefreshExpectations(profile);
		org.RefreshTalentFromReputation();
	}
}
