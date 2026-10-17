using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class CoachCareerTests
{
	[Fact]
	public void Starting_reputation_is_rising_and_reaches_rebuilding_chairs()
	{
		Assert.Equal("Rising", CoachReputation.ToLabel(CoachReputation.Starting));
		Assert.True(CoachReputation.CanReach(CoachReputation.Starting, 50));
		Assert.False(CoachReputation.CanReach(CoachReputation.Starting, 80));
	}

	[Fact]
	public void Elite_coaches_reach_powerhouse_boards()
	{
		Assert.Equal("Elite", CoachReputation.ToLabel(90));
		Assert.True(CoachReputation.CanReach(90, 99));
	}

	[Fact]
	public void Career_record_and_history_match_the_dossier()
	{
		var career = new Career
		{
			CurrentSeasonYear = 2030,
			Reputation = CoachReputation.Starting,
		};
		career.SeasonsCoached = 1;
		career.OpenStint("cascade", "Cascade Regional", 2026);
		career.AddSeason(22, 8, true, false, false, true);
		career.OpenStint("great-lakes", "Great Lakes", 2030);

		Assert.Equal("22–8", career.RecordLabel);
		Assert.Equal(".733", career.WinPctLabel);
		Assert.Equal(1, career.DistrictTitles);
		Assert.Equal(0, career.StateTitles);
		Assert.Equal(2, career.SeasonsCoached);
		Assert.Equal("2ND SEASON", career.SeasonLabel);
		Assert.Equal(2, career.Stints.Count);
		Assert.Equal("2026–2029", career.Stints[0].YearsLabel);
		Assert.Equal("2030–", career.Stints[1].YearsLabel);
		Assert.True(career.Reputation > CoachReputation.Starting);
		Assert.Equal(2031, career.NextStintStartYear());
	}

	[Fact]
	public void Unsigned_career_reads_as_seeking_a_seat()
	{
		var career = new Career();
		Assert.False(career.HasSignedContract);
		Assert.Equal("SEEKING A SEAT", career.SeasonLabel);
		Assert.Equal("—", career.WinPctLabel);
	}

	[Fact]
	public void Signed_career_without_stints_gets_the_current_school_not_a_prototype()
	{
		var career = new Career
		{
			UserOrganizationId = "delaware-valley",
			CurrentSeasonYear = 2026,
			Contract = new CoachContract { SchoolName = "Delaware Valley" },
		};

		Assert.True(career.EnsureCurrentStint("Delaware Valley"));
		Assert.False(career.EnsureCurrentStint("Delaware Valley"));
		Assert.Single(career.Stints);
		Assert.Equal("delaware-valley", career.Stints[0].OrganizationId);
		Assert.Equal("Delaware Valley", career.Stints[0].SchoolName);
		Assert.Equal(2026, career.Stints[0].StartYear);
		Assert.Equal("2026–", career.Stints[0].YearsLabel);
	}

	[Fact]
	public void Salary_from_a_forty_thousand_budget_stays_in_the_coach_band()
	{
		var profile = new ProgramProfile
		{
			Reputation = 78,
			BaseballBudget = 40_000,
			TargetExpectation = BoardExpectation.WinDistrict,
		};

		int salary = CoachSalary.From(profile);
		Assert.InRange(salary, CoachSalary.MinAnnual, CoachSalary.MaxAnnual);
		Assert.Equal(6_000, salary);
		Assert.Equal(0, salary % 500);
	}

	[Fact]
	public void Win_district_boards_offer_three_seasons()
	{
		Assert.Equal(3, CoachSalary.TermYears(BoardExpectation.WinDistrict));
		Assert.Equal("3 Seasons", CoachSalary.TermLabel(3));
	}

	[Fact]
	public void Inbound_offers_only_come_from_in_reach_openings()
	{
		var sitting = School("cascade", 70, opening: false);
		var reach = School("great-lakes", 78, opening: true);
		reach.Profile.Facilities = LetterGrade.A;
		reach.Profile.TargetExpectation = BoardExpectation.WinDistrict;
		var outOfReach = School("capital-city", 99, opening: true);
		var career = new Career
		{
			UserOrganizationId = sitting.Id,
			Reputation = 70,
		};

		List<CoachingJobOffer> offers = InboundJobOffers.Draft(
			[sitting, reach, outOfReach],
			career,
			new SeededRandom(11));

		Assert.NotEmpty(offers);
		Assert.All(offers, offer => Assert.Equal(reach.Id, offer.OrganizationId));
		Assert.DoesNotContain(offers, offer => offer.OrganizationId == sitting.Id);
		Assert.DoesNotContain(offers, offer => offer.OrganizationId == outOfReach.Id);
		Assert.Equal(3, offers[0].TermYears);
		Assert.Equal(CoachSalary.From(reach.Profile), offers[0].AnnualSalary);
	}

	[Fact]
	public void Powerhouses_do_not_call_a_thin_resume()
	{
		var sitting = School("cascade", 70, opening: false);
		var power = School("capital-city", 80, opening: true);
		var career = new Career
		{
			UserOrganizationId = sitting.Id,
			Reputation = 70,
			CareerWins = 8,
			CareerLosses = 18,
		};

		Assert.False(InboundJobOffers.AttractsOffer(career, power.Profile));
		Assert.Empty(InboundJobOffers.Draft([sitting, power], career, new SeededRandom(4)));
	}

	[Fact]
	public void Championship_resume_reaches_a_powerhouse_in_range()
	{
		var sitting = School("cascade", 70, opening: false);
		var power = School("capital-city", 82, opening: true);
		var career = new Career
		{
			UserOrganizationId = sitting.Id,
			Reputation = 78,
			CareerWins = 24,
			CareerLosses = 8,
			StateTitles = 1,
		};

		Assert.True(InboundJobOffers.AttractsOffer(career, power.Profile));
		List<CoachingJobOffer> offers = InboundJobOffers.Draft(
			[sitting, power],
			career,
			new SeededRandom(4));
		Assert.Contains(offers, offer => offer.OrganizationId == power.Id);
	}

	[Fact]
	public void College_boards_wait_for_a_high_school_resume()
	{
		var career = new Career { Reputation = 70, SeasonsCoached = 1 };
		CollegeCoachDossier early = CollegeCoachEligibility.Evaluate(career);
		Assert.False(early.Eligible);
		Assert.False(CollegeCoachEligibility.WouldConsider(career, 60));
		Assert.Contains(early.Notes, note => note.Contains("high-school season", StringComparison.OrdinalIgnoreCase));

		career.SeasonsCoached = 5;
		career.CareerWins = 90;
		career.CareerLosses = 40;
		career.StateTitles = 1;
		career.Reputation = 80;
		CollegeCoachDossier ready = CollegeCoachEligibility.Evaluate(career);
		Assert.True(ready.Eligible);
		Assert.Equal(CompetitionLevel.HighSchool, ready.HighestLevel);
		Assert.True(CollegeCoachEligibility.WouldConsider(career, 70));

		career.SeasonsCoached = 3;
		career.CareerWins = 30;
		career.CareerLosses = 40;
		career.StateTitles = 0;
		career.Reputation = 55;
		Assert.True(CollegeCoachEligibility.Evaluate(career).Eligible);
		Assert.False(CollegeCoachEligibility.WouldConsider(career, 90));
	}

	[Fact]
	public void Unsigned_coaches_do_not_get_inbound_calls()
	{
		var opening = School("great-lakes", 50, opening: true);
		List<CoachingJobOffer> offers = InboundJobOffers.Draft(
			[opening],
			new Career { Reputation = 70 },
			new SeededRandom(3));
		Assert.Empty(offers);
	}

	private static OrganizationState School(string id, int reputation, bool opening) => new()
	{
		Identity = new OrganizationIdentity(id, $"{id} High School", id, 40),
		Profile = new ProgramProfile
		{
			Reputation = reputation,
			HasOpening = opening,
			BaseballBudget = 40_000,
			Facilities = LetterGrade.B,
			TargetExpectation = BoardExpectation.WinDistrict,
		},
	};
}
