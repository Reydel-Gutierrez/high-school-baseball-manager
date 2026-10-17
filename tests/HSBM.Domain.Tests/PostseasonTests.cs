using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class PostseasonTests
{
	[Fact]
	public void District_standings_mark_the_top_two_as_qualifiers()
	{
		List<StandingEntry> ranked = LeagueStandings.Rank(
			[
				Entry("a-varsity", "a", "A", 18, 4),
				Entry("b-varsity", "b", "B", 16, 6),
				Entry("c-varsity", "c", "C", 16, 6, runsFor: 80, runsAgainst: 90),
				Entry("d-varsity", "d", "D", 10, 12),
			],
			markQualifiers: true);

		Assert.Equal("a-varsity", ranked[0].TeamId);
		Assert.True(ranked[0].Qualifies);
		Assert.Equal("b-varsity", ranked[1].TeamId);
		Assert.True(ranked[1].Qualifies);
		Assert.False(ranked[2].Qualifies);
		Assert.Equal(2, ranked.Count(row => row.Qualifies));
	}

	[Fact]
	public void Regional_field_pairs_district_ones_against_the_other_district_two()
	{
		LeagueCatalog catalog = LeagueCatalog.Standard;
		var standings = new List<StandingEntry>();
		foreach (DistrictIdentity district in catalog.Districts)
		{
			IReadOnlyList<OrganizationIdentity> schools = catalog.OrganizationsInDistrict(district.Id);
			standings.Add(Entry(
				$"{schools[0].Id}-varsity",
				schools[0].Id,
				schools[0].ShortName,
				20,
				2,
				district.Id,
				rank: 1));
			standings.Add(Entry(
				$"{schools[1].Id}-varsity",
				schools[1].Id,
				schools[1].ShortName,
				18,
				4,
				district.Id,
				rank: 2));
		}

		PostseasonState bracket = PostseasonField.Seed(2026, catalog.Districts, standings);

		Assert.Equal(8, bracket.Qualifiers.Count);
		IReadOnlyList<PostseasonSeries> east = bracket.SeriesIn(PostseasonRound.RegionalSemifinal, "East");
		Assert.Equal(2, east.Count);
		Assert.Contains(east, series =>
			series.HomeTeamId == "cascade-varsity" && series.AwayTeamId == "sandusky-varsity");
		Assert.Contains(east, series =>
			series.HomeTeamId == "port-clinton-varsity" && series.AwayTeamId == "great-lakes-varsity");
		Assert.Equal(2, bracket.SeriesIn(PostseasonRound.RegionalSemifinal, "West").Count);
	}

	[Fact]
	public void Live_playoff_labels_follow_the_high_school_path()
	{
		Assert.Equal(ProgramPostseason.StateChampion, ProgramPostseason.FromPlayoff(true, true, true, true));
		Assert.Equal(ProgramPostseason.StateFinal, ProgramPostseason.FromPlayoff(false, true, true, true));
		Assert.Equal(ProgramPostseason.RegionalFinal, ProgramPostseason.FromPlayoff(false, false, true, true));
		Assert.Equal(ProgramPostseason.RegionalSemifinal, ProgramPostseason.FromPlayoff(false, false, false, true));
		Assert.Equal(ProgramPostseason.None, ProgramPostseason.FromPlayoff(false, false, false, false));
	}

	[Fact]
	public void Awards_engine_gives_mvp_to_the_best_hitter()
	{
		Guid starId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee1");
		Guid roleId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeee2");
		var world = new GameWorld
		{
			Districts = [new("great-lakes", "Great Lakes", "GL", "East")],
			Organizations =
			[
				new()
				{
					Identity = new OrganizationIdentity("cascade", "Cascade Regional High School", "Cascade", 40),
					DistrictId = "great-lakes",
				},
			],
			Teams = [new TeamIdentity("cascade-varsity", "cascade", TeamLevel.Varsity, 20)],
			Players =
			[
				Hitter(starId, "Miguel", "Alvarez", 70),
				Hitter(roleId, "Luis", "Torres", 58),
			],
			BattingStats =
			[
				new BattingSeasonStats
				{
					PlayerId = starId,
					SeasonYear = 2026,
					TeamId = "cascade-varsity",
					AtBats = 80,
					Hits = 36,
					Doubles = 8,
					HomeRuns = 9,
					RunsBattedIn = 28,
					Walks = 12,
				},
				new BattingSeasonStats
				{
					PlayerId = roleId,
					SeasonYear = 2026,
					TeamId = "cascade-varsity",
					AtBats = 70,
					Hits = 18,
					HomeRuns = 1,
					RunsBattedIn = 8,
				},
			],
			TeamRecords =
			[
				new TeamSeasonRecord { TeamId = "cascade-varsity", SeasonYear = 2026, Wins = 18, Losses = 4 },
			],
		};

		SeasonAwards slate = AwardsEngine.Compile(world, AwardScope.District, 2026, "cascade", "Daniel Hart", official: true);
		SeasonHonor? mvp = slate.Honors.FirstOrDefault(honor => honor.Id == "district-mvp");

		Assert.NotNull(mvp);
		Assert.Equal("Miguel Alvarez", mvp.Winner?.Name);
	}

	[Fact]
	public void Championship_story_runs_the_morning_after_the_series_ends()
	{
		const string winnerTeam = "cascade-varsity";
		Guid seriesId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
		Guid gameId = Guid.Parse("cccccccc-dddd-eeee-ffff-aaaaaaaaaaaa");
		var lastGame = new DateOnly(2026, 5, 15);
		CareerState state = new()
		{
			Career = new Career
			{
				CurrentSeasonYear = 2026,
				CurrentDate = lastGame.AddDays(1),
				UserOrganizationId = "cascade",
			},
			World = new GameWorld
			{
				Districts = [new("great-lakes", "Great Lakes", "GL", "East")],
				Organizations =
				[
					new()
					{
						Identity = new OrganizationIdentity("cascade", "Cascade Regional High School", "Cascade", 40),
						DistrictId = "great-lakes",
					},
					new()
					{
						Identity = new OrganizationIdentity("port-clinton", "Port Clinton High School", "Port Clinton", 40),
						DistrictId = "north-shore",
					},
				],
				Teams =
				[
					new TeamIdentity(winnerTeam, "cascade", TeamLevel.Varsity, 20),
					new TeamIdentity("port-clinton-varsity", "port-clinton", TeamLevel.Varsity, 20),
				],
				Games =
				[
					new ScheduledGame
					{
						Id = gameId,
						SeasonYear = 2026,
						Date = lastGame,
						HomeTeamId = winnerTeam,
						AwayTeamId = "port-clinton-varsity",
						TeamLevel = TeamLevel.Varsity,
						GameType = GameType.RegionalTournament,
						Status = GameStatus.Completed,
						HomeScore = 5,
						AwayScore = 1,
						WinningTeamId = winnerTeam,
						LosingTeamId = "port-clinton-varsity",
					},
				],
				Postseason = new PostseasonState
				{
					SeasonYear = 2026,
					EastChampionTeamId = winnerTeam,
					Series =
					[
						new PostseasonSeries
						{
							Id = seriesId,
							Round = PostseasonRound.RegionalFinal,
							Region = "East",
							HomeTeamId = winnerTeam,
							AwayTeamId = "port-clinton-varsity",
							HomeWins = 2,
							AwayWins = 0,
							WinnerTeamId = winnerTeam,
							GameIds = [gameId],
						},
					],
				},
			},
		};

		IReadOnlyList<NewsStory> stories = NewsWire.Publish(state);
		Assert.Contains(stories, story =>
			story.Tag == NewsTag.Playoffs
			&& story.Headline.Contains("Cascade wins the East Regional", StringComparison.Ordinal));
	}

	private static StandingEntry Entry(
		string teamId,
		string orgId,
		string name,
		int wins,
		int losses,
		string districtId = "great-lakes",
		int rank = 0,
		int runsFor = 100,
		int runsAgainst = 70) =>
		new(teamId, orgId, name, districtId, wins, losses, wins, losses, runsFor, runsAgainst, rank);

	private static Player Hitter(Guid id, string first, string last, int overall) =>
		new()
		{
			Id = id,
			FirstName = first,
			LastName = last,
			PrimaryPosition = PlayerPosition.Shortstop,
			Grade = PlayerGrade.Junior,
			OrganizationId = "cascade",
			TeamId = "cascade-varsity",
			TeamLevel = TeamLevel.Varsity,
			Ratings = PlayerRatings.Clamp(overall, overall, overall, overall, overall, overall, 30, 30, 30, 30),
		};
}
