using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class ProgramReputationTests
{
	[Fact]
	public void Season_results_move_prestige_instead_of_locking_a_power_ranking()
	{
		var school = new OrganizationState
		{
			Identity = new OrganizationIdentity("cascade", "Cascade Regional High School", "Cascade", 40),
			DistrictId = "great-lakes",
			TalentTier = ProgramTalentTier.Average,
			Profile = new ProgramProfile
			{
				Reputation = 70,
				TargetExpectation = BoardExpectation.WinningSeason,
				BaseballBudget = 40_000,
			},
		};

		ProgramReputation.ApplySeason(school, new ProgramSeasonResult
		{
			OrganizationId = "cascade",
			SeasonYear = 2026,
			Wins = 24,
			Losses = 4,
			RegularWins = 20,
			RegularLosses = 6,
			DistrictFinish = 1,
			WonDistrict = true,
			WonRegional = true,
			WonState = true,
		});

		Assert.True(school.Profile.Reputation > 70);
		Assert.Equal(ProgramTalentTier.Powerhouse, school.TalentTier);
		Assert.True(school.Profile.BaseballBudget > 40_000);
		Assert.Equal(1, school.Profile.DistrictTitles);
		Assert.Equal(1, school.Profile.StateTitles);
	}

	[Fact]
	public void Missed_targets_drop_a_powerhouse_out_of_the_top_band()
	{
		var school = new OrganizationState
		{
			Identity = new OrganizationIdentity("capital-city", "Capital City High School", "Capital City", 40),
			Profile = new ProgramProfile
			{
				Reputation = 80,
				TargetExpectation = BoardExpectation.StateTitle,
				LastSeasonWins = 8,
			},
		};
		school.RefreshTalentFromReputation();
		Assert.Equal(ProgramTalentTier.Powerhouse, school.TalentTier);

		int next = ProgramReputation.AfterSeason(
			school.Profile.Reputation,
			8,
			18,
			8,
			wonDistrict: false,
			wonRegional: false,
			wonState: false,
			targetMet: false);
		Assert.True(next < 80);
		Assert.Equal(ProgramTalentTier.AboveAverage, ProgramReputation.TalentBand(next));
	}

	[Fact]
	public void College_is_a_reserved_competition_level()
	{
		Assert.Equal("High School", CompetitionLevel.HighSchool.ToLabel());
		Assert.Equal("College", CompetitionLevel.College.ToLabel());
	}
}
