using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class WorldRulesTests
{
	[Fact]
	public void Standard_league_is_thirty_two_schools_in_four_districts()
	{
		LeagueCatalog catalog = LeagueCatalog.Standard;
		Assert.Equal(32, catalog.Organizations.Count);
		Assert.Equal(64, catalog.Teams.Count);
		Assert.Equal(4, catalog.Districts.Count);
		Assert.Equal(8, LeagueCatalog.SchoolsPerDistrict);
		Assert.Equal(CompetitionLevel.HighSchool, catalog.CompetitionLevel);
		Assert.All(catalog.Districts, district =>
			Assert.Equal(8, catalog.OrganizationsInDistrict(district.Id).Count));
	}

	[Fact]
	public void Every_school_has_varsity_and_jv_capped_at_twenty()
	{
		LeagueCatalog catalog = LeagueCatalog.Standard;
		Assert.Equal(40, LeagueCatalog.OrganizationRosterCap);
		Assert.Equal(42, LeagueCatalog.TemporaryRosterCap);
		Assert.Equal(20, LeagueCatalog.TeamRosterCap);
		foreach (OrganizationIdentity org in catalog.Organizations)
		{
			TeamIdentity varsity = catalog.TeamFor(org.Id, TeamLevel.Varsity);
			TeamIdentity jv = catalog.TeamFor(org.Id, TeamLevel.JuniorVarsity);
			Assert.Equal(LeagueCatalog.TeamIdFor(org.Id, TeamLevel.Varsity), varsity.Id);
			Assert.Equal(LeagueCatalog.TeamIdFor(org.Id, TeamLevel.JuniorVarsity), jv.Id);
			Assert.Equal(20, varsity.RosterCap);
			Assert.Equal(20, jv.RosterCap);
			Assert.Equal(org.Id, varsity.OrganizationId);
			Assert.Equal(org.Id, jv.OrganizationId);
		}
	}

	[Fact]
	public void District_assignment_is_stable_and_east_west_split()
	{
		LeagueCatalog catalog = LeagueCatalog.Standard;
		Assert.Equal("great-lakes", catalog.DistrictIdFor("cascade"));
		Assert.Equal("north-shore", catalog.DistrictIdFor("port-clinton"));
		Assert.Equal("capital-valley", catalog.DistrictIdFor("springfield"));
		Assert.Equal("iron-range", catalog.DistrictIdFor("hibbing"));
		Assert.True(catalog.SameDistrict("cascade", "lakeside"));
		Assert.False(catalog.SameDistrict("cascade", "hibbing"));
		Assert.True(catalog.SameDistrictTeams("cascade-varsity", "pinecrest-jv"));
		Assert.Equal("East", catalog.Districts.Single(d => d.Id == "great-lakes").Region);
		Assert.Equal("West", catalog.Districts.Single(d => d.Id == "iron-range").Region);
	}

	[Fact]
	public void Unsigned_pool_is_not_a_playable_school()
	{
		Assert.True(LeagueCatalog.IsUnsignedOrganization(null));
		Assert.True(LeagueCatalog.IsUnsignedOrganization(""));
		Assert.True(LeagueCatalog.IsUnsignedOrganization(LeagueCatalog.UnsignedOrganizationId));
		Assert.False(LeagueCatalog.IsUnsignedOrganization("cascade"));
		Assert.Equal("tryouts-cascade", LeagueCatalog.TryoutTeamId("cascade"));
		Assert.True(LeagueCatalog.IsTryoutTeam("tryouts-cascade"));
		Assert.False(LeagueCatalog.IsTryoutTeam("cascade-varsity"));
		Assert.Equal("cascade", LeagueCatalog.SchoolForTryoutTeam("tryouts-cascade"));
	}
}

public class PlayerRulesTests
{
	[Fact]
	public void Ratings_clamp_to_one_through_one_hundred()
	{
		PlayerRatings low = PlayerRatings.Clamp(0, -4, 50, 50, 50, 50, 50, 50, 50, 50);
		PlayerRatings high = PlayerRatings.Clamp(140, 50, 50, 50, 50, 50, 50, 50, 50, 50);
		Assert.Equal(PlayerRatings.Min, low.Contact);
		Assert.Equal(PlayerRatings.Min, low.Power);
		Assert.Equal(PlayerRatings.Max, high.Contact);
	}

	[Fact]
	public void Grade_advances_until_senior_year_then_eligibility_ends()
	{
		Assert.Equal(PlayerGrade.Sophomore, PlayerGrade.Freshman.Advance());
		Assert.Equal(PlayerGrade.Junior, PlayerGrade.Sophomore.Advance());
		Assert.Equal(PlayerGrade.Senior, PlayerGrade.Junior.Advance());
		Assert.Null(PlayerGrade.Senior.Advance());
		Assert.Equal(4, PlayerGrade.Freshman.RemainingEligibility());
		Assert.Equal(1, PlayerGrade.Senior.RemainingEligibility());
		Assert.Equal("FR", PlayerGrade.Freshman.ToAbbrev());
		Assert.Equal(PlayerGrade.Junior, PlayerGradeExtensions.FromAbbrev("JR"));
	}

	[Fact]
	public void Unsigned_alumni_and_injury_rules_gate_who_can_play()
	{
		var player = new Player
		{
			FirstName = "Alex",
			LastName = "Rivera",
			OrganizationId = "cascade",
			TeamId = "cascade-varsity",
			Grade = PlayerGrade.Junior,
			PrimaryPosition = PlayerPosition.Shortstop,
			Ratings = PlayerRatings.Clamp(60, 50, 55, 70, 80, 75, 30, 30, 30, 30),
		};
		Assert.True(player.IsActive);
		Assert.False(player.IsUnsigned);
		Assert.True(player.CanPlay(new DateOnly(2026, 3, 1)));

		player.Injury = new PlayerInjury
		{
			Kind = "Hamstring",
			Start = new DateOnly(2026, 3, 1),
			AvailableOn = new DateOnly(2026, 3, 10),
		};
		Assert.True(player.IsInjured);
		Assert.False(player.CanPlay(new DateOnly(2026, 3, 5)));
		Assert.True(player.CanPlay(new DateOnly(2026, 3, 10)));
		player.Injury.DayToDay = true;
		Assert.True(player.CanPlay(new DateOnly(2026, 3, 5)));

		RosterMovement.Release(player);
		Assert.True(player.IsUnsigned);
		Assert.False(player.IsActive);
		Assert.Equal(0, player.JerseyNumber);

		player.Graduate(2026, CollegeCatalog.Place(player.FullName, player.Overall, 80));
		Assert.True(player.Alumni);
		Assert.True(player.HasCollegeDestination);
		Assert.Equal(2026, player.GraduatedYear);
	}

	[Fact]
	public void Position_groups_match_the_diamond()
	{
		Assert.True(PlayerPosition.StartingPitcher.IsPitcher());
		Assert.True(PlayerPosition.ReliefPitcher.IsPitcher());
		Assert.False(PlayerPosition.Catcher.IsPitcher());
		Assert.True(PlayerPosition.Shortstop.IsInfielder());
		Assert.True(PlayerPosition.CenterField.IsOutfielder());
		Assert.Equal("SS", PlayerPosition.Shortstop.ToCode());
		Assert.Equal("LHP", PlayerPosition.StartingPitcher.ToDisplayLabel(ThrowingHand.Left));
		Assert.Equal(PlayerPosition.Catcher, PlayerPositionExtensions.ParseLabel("C"));
		Assert.Equal(PlayerPosition.StartingPitcher, PlayerPositionExtensions.ParseLabel("RHP"));
	}

	[Fact]
	public void Seniors_on_a_high_school_roster_are_the_graduating_class()
	{
		var senior = new Player
		{
			OrganizationId = "cascade",
			Grade = PlayerGrade.Senior,
		};
		var junior = new Player
		{
			OrganizationId = "cascade",
			Grade = PlayerGrade.Junior,
		};
		var unsigned = new Player
		{
			OrganizationId = LeagueCatalog.UnsignedOrganizationId,
			Grade = PlayerGrade.Senior,
		};
		Assert.True(PlayerGraduation.ShouldGraduate(senior));
		Assert.False(PlayerGraduation.ShouldGraduate(junior));
		Assert.False(PlayerGraduation.ShouldGraduate(unsigned));
	}
}

public class TeamRulesTests
{
	[Fact]
	public void Counting_games_are_district_and_non_district_only()
	{
		Assert.True(GameType.District.CountsInStandings());
		Assert.True(GameType.NonDistrict.CountsInStandings());
		Assert.False(GameType.Exhibition.CountsInStandings());
		Assert.False(GameType.RegionalTournament.CountsInStandings());
		Assert.True(GameType.StateTournament.IsPostseason());
		Assert.True(GameType.Exhibition.IsExhibition());
	}

	[Fact]
	public void Season_record_tracks_overall_and_district_splits()
	{
		var record = new TeamSeasonRecord { TeamId = "cascade-varsity", SeasonYear = 2026 };
		record.ApplyGame(5, 2, won: true, district: true);
		record.ApplyGame(1, 4, won: false, district: false);
		Assert.Equal(1, record.Wins);
		Assert.Equal(1, record.Losses);
		Assert.Equal(1, record.DistrictWins);
		Assert.Equal(0, record.DistrictLosses);
		Assert.Equal(6, record.RunsFor);
		Assert.Equal(6, record.RunsAgainst);
		Assert.Equal(2, record.GamesPlayed);
		Assert.Equal(0.5, record.WinningPercentage);
	}

	[Fact]
	public void Standings_rank_by_wins_then_losses_then_run_differential()
	{
		List<StandingEntry> ranked = LeagueStandings.RankDistricts(
		[
			new("a-varsity", "a", "A", "east", 16, 6, 10, 4, 80, 50),
			new("b-varsity", "b", "B", "east", 16, 6, 9, 5, 70, 55),
			new("c-varsity", "c", "C", "east", 14, 8, 8, 6, 60, 70),
			new("d-varsity", "d", "D", "west", 20, 2, 12, 2, 90, 40),
			new("e-varsity", "e", "E", "west", 18, 4, 11, 3, 85, 50),
			new("f-varsity", "f", "F", "west", 10, 12, 4, 10, 40, 80),
		]);

		StandingEntry? a = LeagueStandings.FindOrganization(ranked, "a");
		StandingEntry? b = LeagueStandings.FindOrganization(ranked, "b");
		Assert.NotNull(a);
		Assert.NotNull(b);
		Assert.Equal(1, a!.Rank);
		Assert.Equal(2, b!.Rank);
		Assert.True(a.Qualifies);
		Assert.True(b.Qualifies);
		Assert.False(LeagueStandings.FindOrganization(ranked, "c")!.Qualifies);
		Assert.Equal(1, LeagueStandings.FindOrganization(ranked, "d")!.Rank);
		Assert.Equal(LeagueStandings.QualifiersPerDistrict, 2);
	}

	[Fact]
	public void Roster_rules_open_two_extra_spots_before_the_cut()
	{
		DateOnly before = new(2026, 2, 1);
		DateOnly cut = SeasonCalendar.InitialRosterCut(2026);
		Assert.True(ProgramRosterRules.ExtrasAllowed(before, 2026));
		Assert.Equal(42, ProgramRosterRules.RegisteredCap(before, 2026));
		Assert.Equal(4, ProgramRosterRules.UnassignedCap(before, 2026));
		Assert.False(ProgramRosterRules.ExtrasAllowed(cut, 2026));
		Assert.Equal(40, ProgramRosterRules.RegisteredCap(cut, 2026));
		Assert.Equal("VARSITY", ProgramRosterRules.AssignmentLabel(RosterAssignment.Varsity));
		Assert.Equal("UNASSIGNED", ProgramRosterRules.AssignmentLabel(RosterAssignment.Unassigned));
	}
}
