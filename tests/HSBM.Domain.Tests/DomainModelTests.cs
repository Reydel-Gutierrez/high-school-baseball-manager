using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class PlayerOverallTests
{
	[Fact]
	public void Shortstop_weights_fielding_and_arm_over_first_base()
	{
		var glove = PlayerRatings.Clamp(50, 50, 50, 50, 90, 90, 30, 30, 30, 30);
		int ss = PlayerOverall.Calculate(PlayerPosition.Shortstop, glove);
		int first = PlayerOverall.Calculate(PlayerPosition.FirstBase, glove);
		Assert.True(ss > first);
	}

	[Fact]
	public void Pitcher_overall_uses_stuff_control_movement_stamina()
	{
		var ace = PlayerRatings.Clamp(30, 30, 30, 40, 40, 40, 90, 88, 86, 80);
		var weak = PlayerRatings.Clamp(30, 30, 30, 40, 40, 40, 40, 40, 40, 40);
		Assert.True(PlayerOverall.Calculate(PlayerPosition.StartingPitcher, ace) >
			PlayerOverall.Calculate(PlayerPosition.StartingPitcher, weak));
	}

	[Fact]
	public void Center_field_values_speed_and_fielding()
	{
		var burner = PlayerRatings.Clamp(50, 50, 50, 90, 88, 50, 30, 30, 30, 30);
		var statue = PlayerRatings.Clamp(50, 50, 50, 40, 40, 50, 30, 30, 30, 30);
		Assert.True(PlayerOverall.Calculate(PlayerPosition.CenterField, burner) >
			PlayerOverall.Calculate(PlayerPosition.CenterField, statue));
	}
}

public class SeasonStatsTests
{
	[Fact]
	public void Batting_derived_stats_come_from_counting_stats()
	{
		var stats = new BattingSeasonStats
		{
			AtBats = 10,
			Hits = 3,
			Doubles = 1,
			HomeRuns = 1,
			Walks = 2,
		};

		Assert.Equal(0.300, stats.Average, 3);
		Assert.Equal(5.0 / 12.0, stats.OnBasePercentage, 3);
		Assert.Equal(0.700, stats.Slugging, 3);
		Assert.Equal(stats.OnBasePercentage + stats.Slugging, stats.Ops);
	}

	[Fact]
	public void Pitching_stores_outs_and_derives_innings_era_and_whip()
	{
		var stats = new PitchingSeasonStats
		{
			OutsRecorded = 20,
			EarnedRuns = 4,
			HitsAllowed = 6,
			Walks = 2,
		};

		Assert.Equal("6.2", stats.InningsPitchedDisplay);
		Assert.Equal(4 * 27.0 / 20.0, stats.Era, 5);
		Assert.Equal(8 * 3.0 / 20.0, stats.Whip, 5);
	}

	[Fact]
	public void League_catalog_has_varsity_and_jv_for_every_school()
	{
		LeagueCatalog catalog = LeagueCatalog.Standard;
		Assert.Equal(32, catalog.Organizations.Count);
		Assert.Equal(64, catalog.Teams.Count);
		Assert.Equal(LeagueCatalog.DistrictCount, catalog.Districts.Count);
		Assert.Equal(CompetitionLevel.HighSchool, catalog.CompetitionLevel);
		foreach (DistrictIdentity district in catalog.Districts)
		{
			Assert.Equal(LeagueCatalog.SchoolsPerDistrict, catalog.OrganizationsInDistrict(district.Id).Count);
		}
		foreach (OrganizationIdentity org in catalog.Organizations)
		{
			Assert.NotNull(catalog.TeamFor(org.Id, TeamLevel.Varsity));
			Assert.NotNull(catalog.TeamFor(org.Id, TeamLevel.JuniorVarsity));
			Assert.Equal(20, catalog.TeamFor(org.Id, TeamLevel.Varsity).RosterCap);
		}
	}
}

public class SeededRandomTests
{
	[Fact]
	public void same_seed_replays_the_same_sequence()
	{
		var a = new SeededRandom(42);
		var b = new SeededRandom(42);
		Assert.Equal(a.Next(0, 1000), b.Next(0, 1000));
		Assert.Equal(a.NextDouble(), b.NextDouble());
		Assert.Equal(a.NextGuid(), b.NextGuid());
	}
}

public class SeasonCalendarTests
{
	[Fact]
	public void Opening_day_and_roster_freeze_match_the_baseball_year()
	{
		Assert.Equal(new DateOnly(2026, 2, 16), SeasonCalendar.OpeningDay(2026));
		Assert.Equal(new DateOnly(2026, 2, 1), SeasonCalendar.SigningDeadline(2026));
		Assert.Equal(new DateOnly(2026, 2, 8), SeasonCalendar.InitialRosterCut(2026));
		Assert.Equal(new DateOnly(2026, 4, 24), SeasonCalendar.RosterFreeze(2026));
		Assert.Equal(new DateOnly(2026, 5, 3), SeasonCalendar.RegularSeasonEnd(2026));
		Assert.Equal(SeasonCalendar.OpeningDay(2026), LeagueCatalog.Standard.OpeningDay(2026));
	}

	[Fact]
	public void January_fifth_is_new_year_and_preliminary_scouting()
	{
		IReadOnlyList<SeasonEvent> events = SeasonCalendar.On(2026, new DateOnly(2026, 1, 5));
		Assert.Contains(events, e => e.Id == "new-year");
		Assert.Contains(events, e => e.Id == "prelim-scouting");
	}

	[Fact]
	public void District_open_play_covers_two_january_weeks()
	{
		SeasonEvent openPlay = SeasonCalendar.ForSeason(2026).Single(e => e.Id == "open-play");
		Assert.True(openPlay.Featured);
		Assert.Equal(new DateOnly(2026, 1, 12), openPlay.Start);
		Assert.Equal(new DateOnly(2026, 1, 25), openPlay.End);
		Assert.True(openPlay.OccursOn(new DateOnly(2026, 1, 12)));
		Assert.True(openPlay.OccursOn(new DateOnly(2026, 1, 25)));
		Assert.False(openPlay.OccursOn(new DateOnly(2026, 1, 11)));
		Assert.False(openPlay.OccursOn(new DateOnly(2026, 1, 26)));
	}

	[Fact]
	public void Winter_break_spans_december_into_january()
	{
		IReadOnlyList<SeasonEvent> newYears = SeasonCalendar.On(2026, new DateOnly(2026, 1, 2));
		Assert.Contains(newYears, e => e.Name == "Winter Break");

		IReadOnlyList<SeasonEvent> lateDecember = SeasonCalendar.On(2026, new DateOnly(2026, 12, 22));
		Assert.Contains(lateDecember, e => e.Name == "Winter Break");
	}

	[Fact]
	public void Incoming_class_and_graduation_are_featured_milestones()
	{
		SeasonEvent incoming = SeasonCalendar.IncomingClass(2026);
		Assert.True(incoming.Featured);
		Assert.Equal(new DateOnly(2026, 2, 2), incoming.Start);
		Assert.Equal("incoming-class", incoming.Id);

		SeasonEvent graduation = SeasonCalendar.Graduation(2026);
		Assert.True(graduation.Featured);
		Assert.Equal(new DateOnly(2026, 6, 1), graduation.Start);
		Assert.Equal("graduation", graduation.Id);

		SeasonEvent hall = SeasonCalendar.HallOfFame(2026);
		Assert.True(hall.Featured);
		Assert.Equal(new DateOnly(2026, 6, 6), hall.Start);
		Assert.Equal("hall-of-fame", hall.Id);
		Assert.True(SeasonCalendar.HallOfFameOn(2026, hall.Start));
	}

	[Fact]
	public void Championship_celebrations_land_after_each_title()
	{
		SeasonEvent district = SeasonCalendar.DistrictCelebration(2026);
		Assert.True(district.Featured);
		Assert.Equal(new DateOnly(2026, 5, 3), district.Start);
		Assert.Equal(ChampionshipTitle.District, SeasonCalendar.ChampionshipOn(2026, district.Start));

		SeasonEvent regional = SeasonCalendar.RegionalCelebration(2026);
		Assert.True(regional.Featured);
		Assert.Equal(new DateOnly(2026, 5, 16), regional.Start);
		Assert.Equal(ChampionshipTitle.Regional, SeasonCalendar.ChampionshipOn(2026, regional.Start));

		SeasonEvent state = SeasonCalendar.ChampionCelebration(2026);
		Assert.True(state.Featured);
		Assert.Equal(new DateOnly(2026, 5, 23), state.Start);
		Assert.Equal(ChampionshipTitle.State, SeasonCalendar.ChampionshipOn(2026, state.Start));
		Assert.Equal(ChampionshipTitle.State, SeasonCalendar.ChampionshipForEvent("champion-celebration"));
		Assert.Null(SeasonCalendar.ChampionshipOn(2026, new DateOnly(2026, 5, 4)));
	}

	[Fact]
	public void Award_ceremonies_follow_district_and_state_titles()
	{
		SeasonEvent district = SeasonCalendar.DistrictAwards(2026);
		Assert.True(district.Featured);
		Assert.Equal(new DateOnly(2026, 5, 4), district.Start);
		Assert.Equal("district-awards", district.Id);
		Assert.Equal(AwardScope.District, SeasonCalendar.AwardCeremonyOn(2026, district.Start));
		Assert.Equal(AwardScope.District, SeasonCalendar.AwardCeremonyForEvent("district-awards"));

		SeasonEvent state = SeasonCalendar.StateAwards(2026);
		Assert.True(state.Featured);
		Assert.Equal(new DateOnly(2026, 5, 24), state.Start);
		Assert.Equal(new DateOnly(2026, 5, 27), state.End);
		Assert.Equal("state-awards", state.Id);
		Assert.Equal(AwardScope.State, SeasonCalendar.AwardCeremonyOn(2026, state.Start));
		Assert.Equal(AwardScope.State, SeasonCalendar.AwardCeremonyOn(2026, state.End));
		Assert.Equal(AwardScope.State, SeasonCalendar.AwardCeremonyForEvent("state-awards"));
		Assert.Null(SeasonCalendar.AwardCeremonyOn(2026, new DateOnly(2026, 5, 23)));
	}

	[Fact]
	public void April_lists_roster_freeze_in_the_month()
	{
		IReadOnlyList<SeasonEvent> april = SeasonCalendar.InMonth(2026, 2026, 4);
		Assert.Contains(april, e => e.Id == "roster-freeze");
		Assert.Contains(april, e => e.Id == "transfer-deadline");
	}
}

public class CollegeCatalogTests
{
	[Fact]
	public void High_overall_commits_to_a_division_i_power()
	{
		CollegeCommit commit = CollegeCatalog.Place("Chris Morgan", 88, 92);
		Assert.Equal("Division I", commit.Path);
		Assert.Contains(commit.School, new[]
		{
			"Lakeshore State",
			"Northbridge University",
			"Gulf Coast University",
			"Iron Range State",
			"Capital Valley University",
		});
	}

	[Fact]
	public void Low_overall_commits_to_junior_college()
	{
		CollegeCommit commit = CollegeCatalog.Place("Quiet Role", 48, 50);
		Assert.Equal("Junior college", commit.Path);
		Assert.Contains(commit.School, new[]
		{
			"Riverbend College",
			"Westshore JC",
			"Mesabi Valley College",
			"Harbor Point JC",
		});
	}

	[Fact]
	public void Same_name_and_talent_is_stable()
	{
		CollegeCommit a = CollegeCatalog.Place("Anthony Marini", 78, 80);
		CollegeCommit b = CollegeCatalog.Place("Anthony Marini", 78, 80);
		Assert.Equal(a.School, b.School);
		Assert.Equal(a.Path, b.Path);
		Assert.Equal(a.CollegeId, b.CollegeId);
		Assert.False(string.IsNullOrWhiteSpace(a.CollegeId));
		Assert.Equal(CompetitionLevel.College, a.CompetitionLevel);
	}

	[Fact]
	public void College_id_is_a_stable_slug_for_later_catalogs()
	{
		Assert.Equal("lakeshore-state", CollegeCatalog.IdOf("Lakeshore State"));
		Assert.Equal("westshore-jc", CollegeCatalog.IdOf("Westshore JC"));
	}
}
