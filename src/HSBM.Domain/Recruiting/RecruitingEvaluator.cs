namespace HSBM.Domain;

/// <summary>
/// Shared player decision. User and AI offers go through the same score.
/// Financial value uses a log curve so money is visible but not decisive.
/// </summary>
public static class RecruitingEvaluator
{
	public const double DecisionMargin = 0.035;

	public static double ScoreOffer(
		Player player,
		OrganizationState school,
		IReadOnlyList<Player> returningRoster,
		RecruitingOffer offer,
		int? coachingReputation = null)
	{
		OfferFactors factors = Factors(player, school, returningRoster, offer, coachingReputation);
		RecruitingPriorities weights = player.Priorities;
		double score =
			(weights.Reputation * factors.Prestige) +
			(weights.PlayingTime * factors.PlayingTime) +
			(weights.RosterCompetition * factors.RosterOpportunity) +
			(weights.RecentSuccess * factors.Success) +
			(weights.ProgramStability * factors.Coaching) +
			(weights.VarsityOpportunity * factors.Varsity) +
			(weights.FinancialOffer * factors.Sponsorship) +
			(weights.Facilities * factors.Location);

		score += player.Personality.RecruitingBias(
			factors.Prestige,
			factors.PlayingTime,
			factors.Location,
			factors.Coaching,
			factors.Varsity,
			factors.Success) * 6.0;

		return score / 100.0;
	}

	public static RecruitingOffer? Choose(
		Player player,
		IReadOnlyList<ScoredOffer> scored,
		ISeededRandom rng)
	{
		if (scored.Count == 0)
		{
			return null;
		}

		List<ScoredOffer> ordered = [.. scored];
		ordered.Sort((a, b) => b.Score.CompareTo(a.Score));
		ScoredOffer best = ordered[0];
		if (ordered.Count > 1 && best.Score - ordered[1].Score < DecisionMargin)
		{
			double jitter = (rng.NextDouble() - 0.5) * 0.02;
			if (best.Score + jitter < ordered[1].Score)
			{
				return ordered[1].Offer;
			}
		}

		return best.Offer;
	}

	public static double FinancialScore(int annual)
	{
		int clamped = ProgramBudget.ClampOffer(annual);
		if (clamped <= 0)
		{
			return 0.08;
		}

		double log = Math.Log(1.0 + (clamped / 180.0));
		double cap = Math.Log(1.0 + (ProgramBudget.MaxAnnualOffer / 180.0));
		return 0.12 + (0.88 * (log / cap));
	}

	public static double SuccessScore(ProgramProfile profile)
	{
		double winPct = profile.LastSeasonWins / (double)ProgramProfile.SeasonGames;
		double finish = (9 - Math.Clamp(profile.DistrictFinish, 1, 8)) / 8.0;
		double titles = Math.Clamp(
			(profile.DistrictTitles * 0.04) + (profile.RegionalTitles * 0.08) + (profile.StateTitles * 0.14),
			0.0,
			0.35);
		return Math.Clamp((winPct * 0.55) + (finish * 0.30) + titles, 0.05, 1.0);
	}

	public static double StabilityScore(ProgramProfile profile)
	{
		if (!profile.HasOpening)
		{
			return 0.86;
		}

		return profile.VacancyReason switch
		{
			VacancyReason.Retirement => 0.62,
			VacancyReason.Reassignment => 0.48,
			VacancyReason.Resignation => 0.40,
			VacancyReason.Dismissal => 0.22,
			_ => 0.28,
		};
	}

	public static double FacilitiesScore(LetterGrade grade) =>
		Math.Clamp((int)grade / (double)LetterGrade.APlus, 0.08, 1.0);

	public static double LocationScore(ProgramProfile profile) =>
		(FacilitiesScore(profile.Facilities) + FacilitiesScore(profile.BaseballResources)) / 2.0;

	public static double CoachingScore(ProgramProfile profile, int coachingReputation)
	{
		double staff = StabilityScore(profile);
		double brand = Math.Clamp(coachingReputation / 100.0, 0.08, 1.0);
		return (staff * 0.55) + (brand * 0.45);
	}

	public static string FitSummary(
		Player player,
		OrganizationState school,
		IReadOnlyList<Player> returningRoster,
		RecruitingOffer offer,
		int? coachingReputation = null)
	{
		(string Label, int Weight, double Value)[] factors = VisibleFactors(
			player,
			school,
			returningRoster,
			offer,
			coachingReputation);
		Array.Sort(factors, (a, b) => (b.Weight * b.Value).CompareTo(a.Weight * a.Value));
		return $"{Qualitative(factors[0].Value)} {factors[0].Label.ToLowerInvariant()} · {Qualitative(factors[1].Value)} {factors[1].Label.ToLowerInvariant()}";
	}

	public static (string Label, int Weight, double Value)[] VisibleFactors(
		Player player,
		OrganizationState school,
		IReadOnlyList<Player> returningRoster,
		RecruitingOffer offer,
		int? coachingReputation = null)
	{
		OfferFactors scored = Factors(player, school, returningRoster, offer, coachingReputation);
		RecruitingPriorities weights = player.Priorities;
		return
		[
			("Playing time", weights.PlayingTime + weights.VarsityOpportunity, (scored.PlayingTime + scored.Varsity) / 2.0),
			("Prestige", weights.Reputation + weights.RecentSuccess, (scored.Prestige + scored.Success) / 2.0),
			("Roster opportunity", weights.RosterCompetition, scored.RosterOpportunity),
			("Coaching", weights.ProgramStability, scored.Coaching),
			("Location / resources", weights.Facilities, scored.Location),
			("Sponsorship", weights.FinancialOffer, scored.Sponsorship),
		];
	}

	private static OfferFactors Factors(
		Player player,
		OrganizationState school,
		IReadOnlyList<Player> returningRoster,
		RecruitingOffer offer,
		int? coachingReputation)
	{
		double prestige = school.Profile.Reputation / 100.0;
		double success = SuccessScore(school.Profile);
		double coaching = CoachingScore(school.Profile, coachingReputation ?? school.Profile.Reputation);
		double location = LocationScore(school.Profile);
		double playingTime = RosterNeed.PlayingTimeScore(returningRoster, player.PrimaryPosition, player.Overall);
		double competition = 1.0 - Math.Clamp(
			RosterNeed.BetterOrEqualCount(returningRoster, player.PrimaryPosition, player.Overall) / 4.0,
			0.0,
			1.0);
		double varsity = RosterNeed.VarsityOpportunityScore(returningRoster, player.PrimaryPosition, player.Overall);
		double money = FinancialScore(offer.AnnualSponsorship);

		if (player.Priorities.Archetype == RecruitingArchetype.UnderdogPath)
		{
			prestige = 1.0 - prestige;
			success = 1.0 - success;
		}

		return new OfferFactors(prestige, success, coaching, location, playingTime, competition, varsity, money);
	}

	private static string Qualitative(double value) =>
		value >= 0.72 ? "Strong" : value >= 0.42 ? "Fair" : "Thin";

	public static bool ReadyToDecide(
		int pendingOffers,
		int daysHeld,
		bool deadlineDay)
	{
		if (deadlineDay || pendingOffers >= 3)
		{
			return pendingOffers > 0;
		}

		if (pendingOffers >= 2 && daysHeld >= 1)
		{
			return true;
		}

		return pendingOffers >= 1 && daysHeld >= 2;
	}
}

public readonly record struct ScoredOffer(RecruitingOffer Offer, double Score);

internal readonly record struct OfferFactors(
	double Prestige,
	double Success,
	double Coaching,
	double Location,
	double PlayingTime,
	double RosterOpportunity,
	double Varsity,
	double Sponsorship);
