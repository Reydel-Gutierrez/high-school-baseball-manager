using HSBM.Domain;
using HSBM.Simulation;

namespace HSBM.Simulation.Tests;

public class OpenPlayAndRecruitingTests
{
	[Fact]
	public void Open_play_builds_about_two_hundred_players_and_five_game_lines()
	{
		var rng = new SeededRandom(2026);
		List<Player> players = OpenPlayGenerator.GenerateDistrict("great-lakes", 2026, rng);
		Assert.Equal(LeagueCatalog.OpenPlayTargetPlayers, players.Count);
		Assert.All(players, p => Assert.True(p.IsUnsigned));
		Assert.Contains(players, p => p.Priorities.Archetype == RecruitingArchetype.PrestigeSeeker);
		Assert.Contains(players, p => p.Priorities.Archetype == RecruitingArchetype.PlayingTimeFirst);

		DistrictOpenPlay session = OpenPlaySimulator.Simulate("great-lakes", 2026, players, new SeededRandom(9));
		Assert.Equal(players.Count, session.Lines.Count);
		double avgGames = session.Lines.Average(line => Math.Max(line.Hitting.Games, line.Pitching?.Games ?? 0));
		Assert.InRange(avgGames, 4.0, 5.5);
		Assert.All(session.Lines, line => Assert.InRange(Math.Max(line.Hitting.Games, line.Pitching?.Games ?? 0), 3, 5));
	}

	[Fact]
	public void User_and_ai_use_the_same_market_and_ai_signs_inside_the_incoming_band()
	{
		LeagueCatalog catalog = LeagueCatalog.Standard;
		DistrictRecruitingSession session = DistrictRecruitingFactory.Create(
			catalog,
			"cascade",
			2026,
			new SeededRandom(77));
		RecruitingMarket market = session.Market;
		Assert.Equal(8, session.Schools.Count);
		Assert.InRange(session.Prospects.Count, 190, 210);

		var today = new DateOnly(2026, 1, 26);
		for (int i = 0; i < 7; i++)
		{
			market.AdvanceDay(today.AddDays(i), 2026, "cascade", new SeededRandom(100 + i));
		}

		int cascadeSigned = market.SignedCount("cascade");
		Assert.Equal(0, cascadeSigned);

		int otherSigned = 0;
		foreach (OrganizationState school in session.Schools)
		{
			if (school.Id == "cascade")
			{
				continue;
			}

			int signed = market.SignedCount(school.Id);
			otherSigned += signed;
			Assert.InRange(signed, 0, LeagueCatalog.TypicalIncomingMax);
		}

		Assert.True(otherSigned >= 20);
	}

	[Fact]
	public void From_career_reuses_the_saved_open_play_class()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 99, 2026);
		var state = new CareerState
		{
			Career = new Career
			{
				UserOrganizationId = "cascade",
				CurrentSeasonYear = 2026,
				CurrentDate = SeasonCalendar.OpenPlayStart(2026),
			},
			Organizations = world.Organizations,
			Teams = world.Teams,
			Players = world.Players,
			OpenPlay = world.OpenPlay,
		};

		DistrictRecruitingSession session = DistrictRecruitingFactory.FromCareer(state, "cascade");
		Assert.Equal(8, session.Schools.Count);
		Assert.Equal("great-lakes", session.DistrictId);
		Assert.Equal(LeagueCatalog.OpenPlayTargetPlayers, session.Prospects.Count);
		Assert.Equal(session.OpenPlay.PlayerIds.Count, session.Prospects.Count);
	}

	[Fact]
	public void Sponsorships_reduce_available_recruiting_budget()
	{
		var profile = new ProgramProfile { BaseballBudget = 40_000 };
		var agreements = new List<PlayerAgreement>
		{
			new()
			{
				OrganizationId = "cascade",
				AnnualSponsorship = 450,
				YearsRemaining = 4,
				TermYears = 4,
			},
			new()
			{
				OrganizationId = "cascade",
				AnnualSponsorship = 650,
				YearsRemaining = 3,
				TermYears = 3,
			},
		};

		int available = ProgramBudget.RecruitingAvailable(profile, agreements, "cascade");
		Assert.Equal(ProgramBudget.RecruitingPool(40_000) - 1_100, available);
	}

	[Fact]
	public void Open_play_days_put_ai_schools_on_players_before_offers()
	{
		DistrictRecruitingSession session = DistrictRecruitingFactory.Create(
			LeagueCatalog.Standard,
			"cascade",
			2026,
			new SeededRandom(41));
		RecruitingMarket market = session.Market;
		DateOnly open = SeasonCalendar.OpenPlayStart(2026);
		market.AdvanceDay(open, 2026, "cascade", new SeededRandom(3));
		Assert.True(market.Interest.Count >= 8);
		Assert.Empty(market.Offers);
		Assert.Contains(market.Interest, item => item.OrganizationId != "cascade");
	}
}
