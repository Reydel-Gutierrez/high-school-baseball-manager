using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class RecruitingTests
{
	[Fact]
	public void Prestige_seeker_picks_the_powerhouse_over_a_bigger_check()
	{
		Player player = Incoming(70, RecruitingArchetype.PrestigeSeeker);
		OrganizationState powerhouse = School("power", 88, 22, 1, ProgramTalentTier.Powerhouse);
		OrganizationState rebuild = School("rebuild", 28, 6, 8, ProgramTalentTier.Rebuilding);
		List<Player> stacked = ReturningAt(PlayerPosition.Shortstop, 74, 71, 68);
		List<Player> thin = ReturningAt(PlayerPosition.Shortstop, 52);

		double powerScore = RecruitingEvaluator.ScoreOffer(player, powerhouse, stacked, Offer("power", 250));
		double rebuildScore = RecruitingEvaluator.ScoreOffer(player, rebuild, thin, Offer("rebuild", 1200));
		Assert.True(powerScore > rebuildScore);
	}

	[Fact]
	public void Playing_time_seeker_takes_the_thinner_roster()
	{
		Player player = Incoming(62, RecruitingArchetype.PlayingTimeFirst);
		OrganizationState powerhouse = School("power", 86, 21, 1, ProgramTalentTier.Powerhouse);
		OrganizationState rebuild = School("rebuild", 32, 7, 7, ProgramTalentTier.Rebuilding);
		List<Player> stacked = ReturningAt(PlayerPosition.Shortstop, 76, 72, 69);
		List<Player> thin = ReturningAt(PlayerPosition.Shortstop, 50);

		double powerScore = RecruitingEvaluator.ScoreOffer(player, powerhouse, stacked, Offer("power", 450));
		double rebuildScore = RecruitingEvaluator.ScoreOffer(player, rebuild, thin, Offer("rebuild", 450));
		Assert.True(rebuildScore > powerScore);
	}

	[Fact]
	public void Money_is_visible_but_not_enough_to_buy_a_prestige_seeker()
	{
		double none = RecruitingEvaluator.FinancialScore(0);
		double mid = RecruitingEvaluator.FinancialScore(450);
		double high = RecruitingEvaluator.FinancialScore(1500);
		Assert.True(mid > none);
		Assert.True(high > mid);
		Assert.True(high - mid < mid - none);
	}

	[Fact]
	public void Incoming_target_is_between_seven_and_fifteen_until_the_roster_is_full()
	{
		Assert.Equal(15, RosterNeed.IncomingTarget(20));
		Assert.Equal(12, RosterNeed.IncomingTarget(28));
		Assert.Equal(7, RosterNeed.IncomingTarget(33));
		Assert.Equal(0, RosterNeed.IncomingTarget(40));
	}

	[Fact]
	public void Pending_offers_reserve_recruiting_budget()
	{
		var profile = new ProgramProfile { BaseballBudget = 40_000 };
		var offers = new List<RecruitingOffer>
		{
			new()
			{
				OrganizationId = "cascade",
				AnnualSponsorship = 900,
				Status = RecruitingOfferStatus.Pending,
			},
		};

		int available = ProgramBudget.RecruitingAvailable(profile, [], "cascade", offers);
		Assert.Equal(ProgramBudget.RecruitingPool(40_000) - 900, available);
	}

	[Fact]
	public void Operations_budget_pays_the_coach_salary()
	{
		var profile = new ProgramProfile { BaseballBudget = 40_000 };
		int salary = CoachSalary.From(78, 40_000);
		int left = ProgramBudget.Available(profile, [], "cascade", salary);
		Assert.Equal(40_000 - salary, left);
		Assert.Equal(ProgramBudget.RecruitingPool(40_000), ProgramBudget.RecruitingAvailable(profile, [], "cascade"));
	}

	[Fact]
	public void Fit_summary_calls_out_playing_time_for_a_thin_roster()
	{
		Player player = Incoming(62, RecruitingArchetype.PlayingTimeFirst);
		OrganizationState rebuild = School("rebuild", 32, 7, 7, ProgramTalentTier.Rebuilding);
		string summary = RecruitingEvaluator.FitSummary(
			player,
			rebuild,
			ReturningAt(PlayerPosition.Shortstop, 50),
			Offer("rebuild", 250));
		Assert.Contains("playing time", summary, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Visible_factors_cover_the_six_recruiting_weights()
	{
		Player player = Incoming(62, RecruitingArchetype.Balanced);
		OrganizationState school = School("labs", 50, 13, 4, ProgramTalentTier.Average);
		(string Label, int Weight, double Value)[] factors = RecruitingEvaluator.VisibleFactors(
			player,
			school,
			ReturningAt(PlayerPosition.Shortstop, 58),
			Offer("labs", 450));
		string[] labels = factors.Select(item => item.Label).ToArray();
		Assert.Contains("Playing time", labels);
		Assert.Contains("Prestige", labels);
		Assert.Contains("Roster opportunity", labels);
		Assert.Contains("Coaching", labels);
		Assert.Contains("Location / resources", labels);
		Assert.Contains("Sponsorship", labels);
	}

	[Fact]
	public void Better_coaching_reputation_moves_a_stability_seeker()
	{
		Player player = Incoming(60, RecruitingArchetype.StabilitySeeker);
		OrganizationState school = School("labs", 50, 13, 4, ProgramTalentTier.Average);
		List<Player> roster = ReturningAt(PlayerPosition.Shortstop, 58);
		double known = RecruitingEvaluator.ScoreOffer(player, school, roster, Offer("labs", 400), 82);
		double unknown = RecruitingEvaluator.ScoreOffer(player, school, roster, Offer("labs", 400), 28);
		Assert.True(known > unknown);
	}

	[Fact]
	public void Interest_copy_names_the_schools_on_a_player()
	{
		Assert.Equal("No schools on him yet.", RecruitingInterest.Format([]));
		Assert.Equal("Cascade is on him.", RecruitingInterest.Format(["Cascade"]));
		Assert.Equal("Cascade and Harbor are on him.", RecruitingInterest.Format(["Cascade", "Harbor"]));
		Assert.Equal("3 schools are on him.", RecruitingInterest.Format(["Cascade", "Harbor", "Ridge"]));
	}

	[Fact]
	public void Agreement_total_is_annual_times_term()
	{
		var agreement = new PlayerAgreement
		{
			AnnualSponsorship = 450,
			TermYears = 4,
			YearsRemaining = 4,
		};
		Assert.Equal(1800, agreement.TotalCommitment);
		Assert.Equal("$450", agreement.AnnualLabel);
	}

	[Fact]
	public void Coachable_player_leans_toward_better_facilities()
	{
		Player player = Incoming(60, RecruitingArchetype.Balanced);
		player.Personality = PlayerTemperament.Coachable;
		OrganizationState labs = School("labs", 50, 13, 4, ProgramTalentTier.Average);
		labs.Profile.Facilities = LetterGrade.A;
		OrganizationState worn = School("worn", 50, 13, 4, ProgramTalentTier.Average);
		worn.Profile.Facilities = LetterGrade.D;
		List<Player> roster = ReturningAt(PlayerPosition.Shortstop, 58);

		double labsScore = RecruitingEvaluator.ScoreOffer(player, labs, roster, Offer("labs", 400));
		double wornScore = RecruitingEvaluator.ScoreOffer(player, worn, roster, Offer("worn", 400));
		Assert.True(labsScore > wornScore);
	}

	private static Player Incoming(int overall, RecruitingArchetype archetype) =>
		new()
		{
			Id = Guid.NewGuid(),
			FirstName = "Test",
			LastName = "Prospect",
			PrimaryPosition = PlayerPosition.Shortstop,
			Grade = PlayerGrade.Freshman,
			OrganizationId = LeagueCatalog.UnsignedOrganizationId,
			TeamId = LeagueCatalog.OpenPlayTeamId("great-lakes"),
			Ratings = PlayerRatings.Clamp(overall, overall, overall, overall, overall, overall, 30, 30, 30, 30),
			Potential = overall + 10,
			Priorities = RecruitingPriorities.For(archetype),
		};

	private static OrganizationState School(
		string id,
		int reputation,
		int wins,
		int finish,
		ProgramTalentTier tier) =>
		new()
		{
			Identity = new OrganizationIdentity(id, id, id, 40),
			DistrictId = "great-lakes",
			TalentTier = tier,
			Profile = new ProgramProfile
			{
				Reputation = reputation,
				LastSeasonWins = wins,
				LastSeasonLosses = ProgramProfile.SeasonGames - wins,
				DistrictFinish = finish,
				Facilities = LetterGrade.B,
				BaseballBudget = 40_000,
			},
		};

	private static List<Player> ReturningAt(PlayerPosition position, params int[] overalls)
	{
		var list = new List<Player>();
		foreach (int overall in overalls)
		{
			list.Add(new Player
			{
				Id = Guid.NewGuid(),
				FirstName = "Ret",
				LastName = overall.ToString(),
				PrimaryPosition = position,
				OrganizationId = "school",
				TeamId = "school-varsity",
				TeamLevel = TeamLevel.Varsity,
				Ratings = PlayerRatings.Clamp(overall, overall, overall, overall, overall, overall, 30, 30, 30, 30),
			});
		}

		return list;
	}

	private static RecruitingOffer Offer(string orgId, int annual) =>
		new()
		{
			Id = Guid.NewGuid(),
			PlayerId = Guid.NewGuid(),
			OrganizationId = orgId,
			AnnualSponsorship = annual,
			TermYears = 4,
			Status = RecruitingOfferStatus.Pending,
		};
}
