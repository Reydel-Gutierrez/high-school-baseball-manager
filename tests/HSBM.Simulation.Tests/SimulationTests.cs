using HSBM.Domain;
using HSBM.Simulation;

namespace HSBM.Simulation.Tests;

public class RosterGenerationTests
{
	[Fact]
	public void Same_seed_generates_identical_rosters()
	{
		var generator = new CareerWorldGenerator();
		GeneratedWorld a = generator.Generate(LeagueCatalog.Standard, 12345, 2026);
		GeneratedWorld b = generator.Generate(LeagueCatalog.Standard, 12345, 2026);
		Assert.Equal(Fingerprint(a), Fingerprint(b));
	}

	[Fact]
	public void Different_seeds_generate_different_rosters()
	{
		var generator = new CareerWorldGenerator();
		GeneratedWorld a = generator.Generate(LeagueCatalog.Standard, 12345, 2026);
		GeneratedWorld b = generator.Generate(LeagueCatalog.Standard, 99999, 2026);
		Assert.NotEqual(Fingerprint(a), Fingerprint(b));
	}

	[Fact]
	public void Every_team_has_playable_coverage_within_cap()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 777, 2026);
		foreach (TeamIdentity team in world.Teams)
		{
			if (team.RosterCap <= 0)
			{
				continue;
			}

			List<Player> roster = world.Players.Where(p => p.TeamId == team.Id).ToList();
			Assert.InRange(roster.Count, 12, team.RosterCap);
			Assert.Contains(roster, p => p.PrimaryPosition == PlayerPosition.Catcher);
			Assert.Contains(roster, p => p.PrimaryPosition == PlayerPosition.StartingPitcher);
			Assert.Contains(roster, p => p.IsPitcher);
			Assert.Contains(roster, p => p.PrimaryPosition.IsInfielder());
			Assert.Contains(roster, p => p.PrimaryPosition.IsOutfielder());
			Assert.All(roster, p => Assert.InRange((int)p.Grade, (int)PlayerGrade.Freshman, (int)PlayerGrade.Senior));
			Assert.Equal(roster.Count, roster.Select(p => p.JerseyNumber).Distinct().Count());
			Assert.All(roster, p => Assert.True(p.Id != Guid.Empty));
		}
	}

	[Fact]
	public void Varsity_skews_older_than_jv()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 42, 2026);
		double varsityUpper = world.Players
			.Where(p => p.TeamLevel == TeamLevel.Varsity)
			.Average(p => (int)p.Grade);
		double jvUpper = world.Players
			.Where(p => p.TeamLevel == TeamLevel.JuniorVarsity)
			.Average(p => (int)p.Grade);
		Assert.True(varsityUpper > jvUpper);
	}

	[Fact]
	public void Programs_are_not_identically_talented()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 42, 2026);
		Assert.Contains(world.Organizations, o => o.TalentTier == ProgramTalentTier.Powerhouse);
		Assert.Contains(world.Organizations, o => o.TalentTier == ProgramTalentTier.Rebuilding);
		double powerhouse = AverageOverall(world, ProgramTalentTier.Powerhouse);
		double rebuilding = AverageOverall(world, ProgramTalentTier.Rebuilding);
		Assert.True(powerhouse > rebuilding);
	}

	[Fact]
	public void Schedule_exists_for_both_levels()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 42, 2026);
		Assert.NotEmpty(world.Games);
		Assert.Contains(world.Games, g => g.TeamLevel == TeamLevel.Varsity);
		Assert.Contains(world.Games, g => g.TeamLevel == TeamLevel.JuniorVarsity);
		Assert.All(world.Games, g => Assert.Equal(GameStatus.Scheduled, g.Status));
	}

	[Fact]
	public void Generate_reports_world_creation_stages()
	{
		var labels = new List<string>();
		new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 7, 2026, progress => labels.Add(progress.Label));
		Assert.Equal(8, labels.Count);
		Assert.Equal("Creating 32 programs...", labels[0]);
		Assert.Equal("Generating program histories...", labels[1]);
		Assert.Equal("Generating returning varsity rosters...", labels[2]);
		Assert.Equal("Generating returning JV rosters...", labels[3]);
		Assert.Equal("Generating player identities...", labels[4]);
		Assert.Equal("Building District Open Play classes...", labels[5]);
		Assert.Equal("Simulating Open Play games...", labels[6]);
		Assert.Equal("Building season schedule...", labels[7]);
	}

	[Fact]
	public void Generated_world_is_a_high_school_circuit_with_seed_coaches_and_districts()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 42, 2026);
		Assert.Equal(42, world.Seed);
		Assert.Equal(CompetitionLevel.HighSchool, world.CompetitionLevel);
		Assert.Equal(LeagueCatalog.DistrictCount, world.Districts.Count);
		Assert.Equal(32, world.Organizations.Count(org => !LeagueCatalog.IsUnsignedOrganization(org.Id)));
		Assert.All(world.Districts, district =>
			Assert.Equal(
				LeagueCatalog.SchoolsPerDistrict,
				world.Organizations.Count(org => org.DistrictId == district.Id && !LeagueCatalog.IsUnsignedOrganization(org.Id))));
		Assert.Equal(32, world.Coaches.Count);
		Assert.All(world.Organizations.Where(org => !LeagueCatalog.IsUnsignedOrganization(org.Id)), org =>
		{
			Assert.Equal(CompetitionLevel.HighSchool, org.CompetitionLevel);
			Assert.NotNull(org.HeadCoach);
			Assert.False(string.IsNullOrWhiteSpace(org.HeadCoach!.FullName));
			Assert.InRange((int)org.Profile.BaseballResources, (int)LetterGrade.F, (int)LetterGrade.APlus);
		});
		GameWorld root = world.ToGameWorld(SeasonCalendar.TryoutSignupDay(2026));
		Assert.Equal(world.Seed, root.Seed);
		Assert.Equal(world.Players.Count, root.Players.Count);
		Assert.Equal(CompetitionLevel.HighSchool, root.CompetitionLevel);
	}

	[Fact]
	public void Program_histories_are_complete_and_seeded()
	{
		GeneratedWorld a = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 42, 2026);
		GeneratedWorld b = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 42, 2026);
		Assert.Equal(HistoryFingerprint(a), HistoryFingerprint(b));
		Assert.All(a.Organizations.Where(org => !LeagueCatalog.IsUnsignedOrganization(org.Id)), org =>
		{
			Assert.InRange(org.Profile.Reputation, 1, 99);
			Assert.Equal(ProgramProfile.SeasonGames, org.Profile.LastSeasonWins + org.Profile.LastSeasonLosses);
			Assert.InRange(org.Profile.DistrictFinish, 1, 8);
			Assert.True(org.Profile.TargetExpectation >= org.Profile.MinimumExpectation);
			Assert.InRange(org.Profile.BaseballBudget, 18_000, 95_000);
			Assert.InRange((int)org.Profile.BaseballResources, (int)LetterGrade.F, (int)LetterGrade.APlus);
			Assert.Equal(org.TalentTier, ProgramReputation.TalentBand(org.Profile.Reputation));
			Assert.Equal(ProgramHistoryGenerator.HistoryYears, org.SeasonHistory.Count);
			Assert.Equal(org.Profile.LastSeasonWins, org.SeasonHistory[^1].RegularWins);
			Assert.Equal(org.Profile.LastSeasonLosses, org.SeasonHistory[^1].RegularLosses);
			Assert.Equal(org.Profile.DistrictFinish, org.SeasonHistory[^1].DistrictFinish);
			Assert.Equal(org.SeasonHistory.Count, org.SeasonHistory.Select(row => row.SeasonYear).Distinct().Count());
			Assert.All(org.SeasonHistory, row =>
			{
				Assert.Equal(org.Id, row.OrganizationId);
				Assert.InRange(row.DistrictFinish, 1, 8);
				Assert.True(row.Wins + row.Losses >= ProgramProfile.SeasonGames);
				Assert.False(string.IsNullOrWhiteSpace(row.Postseason));
			});
			foreach (ProgramRecordCategory category in ProgramRecordBook.Categories)
			{
				List<ProgramRecordHolder> leaders = org.RecordBook.Where(row => row.Category == category).ToList();
				Assert.Equal(ProgramRecordBook.Slots, leaders.Count);
				Assert.Equal(Enumerable.Range(1, ProgramRecordBook.Slots), leaders.Select(row => row.Rank));
				Assert.True(leaders[0].Value >= leaders[^1].Value);
			}

			Assert.Equal(ProgramHistoryGenerator.HistoryYears, org.HallOfFame.Count);
			Assert.Equal(org.HallOfFame.Count, org.HallOfFame.Select(row => row.InductedYear).Distinct().Count());
			Assert.All(org.HallOfFame, row =>
			{
				Assert.Equal(org.Id, row.OrganizationId);
				Assert.False(string.IsNullOrWhiteSpace(row.PlayerName));
				Assert.False(string.IsNullOrWhiteSpace(row.Position));
				Assert.False(string.IsNullOrWhiteSpace(row.CareerLine));
				Assert.NotEmpty(row.HonorLines);
			});
		});

		foreach (IGrouping<string, OrganizationState> district in a.Organizations
			.Where(org => !LeagueCatalog.IsUnsignedOrganization(org.Id))
			.GroupBy(o => o.DistrictId))
		{
			List<int> finishes = district.Select(o => o.Profile.DistrictFinish).OrderBy(n => n).ToList();
			Assert.Equal(Enumerable.Range(1, finishes.Count).ToList(), finishes);
			foreach (int year in district.First().SeasonHistory.Select(row => row.SeasonYear))
			{
				List<int> yearFinishes = district
					.Select(org => org.SeasonHistory.Single(row => row.SeasonYear == year).DistrictFinish)
					.OrderBy(n => n)
					.ToList();
				Assert.Equal(Enumerable.Range(1, yearFinishes.Count).ToList(), yearFinishes);
			}
		}

		double powerhouse = a.Organizations.Where(o => o.TalentTier == ProgramTalentTier.Powerhouse).Average(o => o.Profile.Reputation);
		double rebuilding = a.Organizations.Where(o => o.TalentTier == ProgramTalentTier.Rebuilding).Average(o => o.Profile.Reputation);
		Assert.True(powerhouse > rebuilding);
	}

	[Fact]
	public void First_license_opens_two_or_three_non_powerhouse_vacancies()
	{
		GeneratedWorld a = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 42, 2026);
		GeneratedWorld b = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 42, 2026);
		List<OrganizationState> openingsA = a.Organizations.Where(o => o.Profile.HasOpening).ToList();
		List<OrganizationState> openingsB = b.Organizations.Where(o => o.Profile.HasOpening).ToList();
		Assert.Equal(openingsA.Select(o => o.Id), openingsB.Select(o => o.Id));
		Assert.InRange(openingsA.Count, OpeningGenerator.MinCount, OpeningGenerator.MaxCount);
		Assert.DoesNotContain(openingsA, o => ProgramReputation.IsPowerhouse(o.Profile.Reputation));
		Assert.All(openingsA, o => Assert.True(CoachReputation.CanReach(CoachReputation.Starting, o.Profile.Reputation)));
	}

	[Fact]
	public void Coaching_carousel_posts_two_or_three_seats_and_skips_the_sitting_coach()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 42, 2026);
		CoachingCarousel.Assign(world.Organizations, "cascade", new SeededRandom(99));
		List<OrganizationState> openings = world.Organizations.Where(o => o.Profile.HasOpening).ToList();
		Assert.InRange(openings.Count, CoachingCarousel.MinCount, CoachingCarousel.MaxCount);
		Assert.DoesNotContain(openings, o => o.Id == "cascade");
		Assert.DoesNotContain(openings, o => LeagueCatalog.IsUnsignedOrganization(o.Id));

		GeneratedWorld again = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 42, 2026);
		CoachingCarousel.Assign(again.Organizations, "cascade", new SeededRandom(99));
		Assert.Equal(
			openings.Select(o => o.Id),
			again.Organizations.Where(o => o.Profile.HasOpening).Select(o => o.Id));
	}

	[Fact]
	public void Graduation_day_archives_seniors_with_a_college_destination()
	{
		GeneratedWorld generated = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 11, 2026);
		DateOnly graduation = SeasonCalendar.Graduation(2026).Start;
		CareerState state = new()
		{
			Career = new Career { CurrentSeasonYear = 2026, CurrentDate = graduation, RandomSeed = 11 },
			World = generated.ToGameWorld(graduation),
		};
		int seniors = state.Players.Count(player => PlayerGraduation.ShouldGraduate(player));
		Assert.True(seniors > 0);

		WorldDayResult result = WorldDayProcessor.Run(state, graduation, new BaseballGameSimulator());
		Assert.True(result.Graduated);
		Assert.Equal(0, state.Players.Count(player => PlayerGraduation.ShouldGraduate(player)));
		List<Player> alumni = state.Players.Where(player => player.Alumni).ToList();
		Assert.Equal(seniors, alumni.Count);
		Assert.All(alumni, player =>
		{
			Assert.Equal(2026, player.GraduatedYear);
			Assert.True(player.HasCollegeDestination);
			Assert.Equal(CollegeCatalog.IdOf(player.CollegeSchool), player.CollegeId);
		});
		Assert.Contains(state.Players, player => !player.Alumni && player.Grade == PlayerGrade.Junior);
		Assert.Equal(seniors, state.Transactions.Count(entry => entry.Kind == PlayerTransactionKind.Graduation));
	}

	private static string Fingerprint(GeneratedWorld world) =>
		string.Join("|", world.Players
			.OrderBy(p => p.TeamId)
			.ThenBy(p => p.LastName)
			.ThenBy(p => p.FirstName)
			.ThenBy(p => p.Id)
			.Select(p => $"{p.Id:N}:{p.FullName}:{p.PrimaryPosition}:{p.Grade}:{p.Ratings.Contact}:{p.Ratings.Stuff}:{p.TeamId}"));

	private static string HistoryFingerprint(GeneratedWorld world) =>
		string.Join("|", world.Organizations
			.OrderBy(o => o.Id)
			.Select(o =>
				$"{o.Id}:{o.Profile.Reputation}:{o.Profile.LastSeasonWins}:{o.Profile.DistrictFinish}:{o.Profile.TargetExpectation}:{string.Join(",", o.SeasonHistory.Select(s => $"{s.SeasonYear}-{s.Wins}-{s.DistrictFinish}-{s.Postseason}"))}:{string.Join(",", o.RecordBook.Select(r => $"{r.Category}:{r.Rank}:{r.PlayerName}:{r.Value}"))}"));

	private static double AverageOverall(GeneratedWorld world, ProgramTalentTier tier)
	{
		HashSet<string> orgs = world.Organizations.Where(o => o.TalentTier == tier).Select(o => o.Id).ToHashSet();
		return world.Players.Where(p => orgs.Contains(p.OrganizationId) && p.TeamLevel == TeamLevel.Varsity).Average(p => p.Overall);
	}
}

public class GameSimulationTests
{
	[Fact]
	public void Games_do_not_end_tied_and_last_at_least_regulation()
	{
		(GeneratedWorld world, BaseballGameSimulator sim) = World();
		for (int i = 0; i < 40; i++)
		{
			GameSimulationResult result = Play(world, sim, i);
			Assert.NotEqual(result.HomeScore, result.AwayScore);
			Assert.True(result.InningsPlayed >= BaseballSimulationConfig.Default.RegulationInnings);
			if (result.ExtraInnings)
			{
				Assert.True(result.InningsPlayed > BaseballSimulationConfig.Default.RegulationInnings);
			}
		}
	}

	[Fact]
	public void Extra_innings_happen_when_low_scoring_games_stay_tied()
	{
		var config = new BaseballSimulationConfig
		{
			BaseStrikeoutRate = 0.78,
			MaxStrikeoutRate = 0.88,
			BaseWalkRate = 0.03,
			MaxWalkRate = 0.06,
			BaseHomeRunRate = 0.004,
			MaxHomeRunRate = 0.01,
			BaseBabip = 0.18,
		};
		(GeneratedWorld world, BaseballGameSimulator sim) = World(config, seed: 3);
		int extras = 0;
		for (int i = 0; i < 80; i++)
		{
			GameSimulationResult result = Play(world, sim, i);
			Assert.NotEqual(result.HomeScore, result.AwayScore);
			if (result.ExtraInnings)
			{
				extras++;
				Assert.True(result.InningsPlayed > BaseballSimulationConfig.Default.RegulationInnings);
			}
		}

		Assert.True(extras > 0);
	}

	[Fact]
	public void Team_runs_and_counting_stats_reconcile()
	{
		(GeneratedWorld world, BaseballGameSimulator sim) = World();
		for (int i = 0; i < 25; i++)
		{
			ScheduledGame game = world.Games[i];
			List<Player> home = world.Players.Where(p => p.TeamId == game.HomeTeamId).ToList();
			List<Player> away = world.Players.Where(p => p.TeamId == game.AwayTeamId).ToList();
			GameSimulationResult result = sim.Simulate(game, home, away, new SeededRandom(1000 + i));

			int homeRuns = SumRuns(result, home);
			int awayRuns = SumRuns(result, away);
			Assert.Equal(result.HomeScore, homeRuns);
			Assert.Equal(result.AwayScore, awayRuns);
			Assert.Equal(result.HomeHits, SumHits(result, home));
			Assert.Equal(result.AwayHits, SumHits(result, away));
		}
	}

	[Fact]
	public void Hits_walks_and_strikeouts_match_opposing_pitcher()
	{
		(GeneratedWorld world, BaseballGameSimulator sim) = World();
		for (int i = 0; i < 20; i++)
		{
			ScheduledGame game = world.Games[i];
			List<Player> home = world.Players.Where(p => p.TeamId == game.HomeTeamId).ToList();
			List<Player> away = world.Players.Where(p => p.TeamId == game.AwayTeamId).ToList();
			GameSimulationResult result = sim.Simulate(game, home, away, new SeededRandom(50 + i));
			PlayerGamePitching homePitching = SumPitching(result, home);
			PlayerGamePitching awayPitching = SumPitching(result, away);
			Assert.Equal(result.AwayHits, homePitching.HitsAllowed);
			Assert.Equal(result.HomeHits, awayPitching.HitsAllowed);
			Assert.Equal(result.AwayWalks, homePitching.Walks);
			Assert.Equal(result.HomeWalks, awayPitching.Walks);
			Assert.Equal(result.AwayStrikeouts, homePitching.Strikeouts);
			Assert.Equal(result.HomeStrikeouts, awayPitching.Strikeouts);
		}
	}

	[Fact]
	public void Walk_forces_runners_and_home_run_clears_the_bases()
	{
		var bases = new BasePaths();
		var first = Player("a");
		var second = Player("b");
		var third = Player("c");
		var batter = Player("d");
		bases.First = first;
		bases.Second = second;
		bases.Third = third;
		BaserunningResult walk = BaseRunnerEngine.Apply(
			bases, batter, PlateAppearanceOutcome.Walk, new SeededRandom(1), BaseballSimulationConfig.Default);
		Assert.Equal(1, walk.Runs);
		Assert.Equal(batter, bases.First);
		Assert.Equal(first, bases.Second);
		Assert.Equal(second, bases.Third);

		BaserunningResult homer = BaseRunnerEngine.Apply(
			bases, Player("e"), PlateAppearanceOutcome.HomeRun, new SeededRandom(1), BaseballSimulationConfig.Default);
		Assert.Equal(4, homer.Runs);
		Assert.Null(bases.First);
		Assert.Null(bases.Second);
		Assert.Null(bases.Third);
	}

	[Fact]
	public void Automatic_lineup_has_nine_batters_and_a_starter()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 5, 2026);
		List<Player> roster = world.Players.Where(p => p.TeamId == world.Teams[0].Id).ToList();
		TeamGamePlan plan = new AutomaticLineupProvider().Create(roster);
		Assert.Equal(9, plan.BattingOrder.Count);
		Assert.True(plan.StartingPitcher.IsPitcher);
		Assert.Equal(9, plan.BattingOrder.Select(b => b.Player.Id).Distinct().Count());
	}

	[Fact]
	public void Saved_lineup_sets_batting_order_and_box_score()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 8, 2026);
		ScheduledGame game = world.Games[0];
		List<Player> home = world.Players.Where(p => p.TeamId == game.HomeTeamId).ToList();
		List<Player> away = world.Players.Where(p => p.TeamId == game.AwayTeamId).ToList();
		TeamGamePlan automatic = new AutomaticLineupProvider().Create(home);
		var slots = new List<TeamLineupSlot>();
		for (int i = automatic.BattingOrder.Count - 1; i >= 0; i--)
		{
			LineupBatter slot = automatic.BattingOrder[i];
			slots.Add(new TeamLineupSlot
			{
				PlayerId = slot.Player.Id,
				Position = slot.FieldPosition.ToCode(),
			});
		}

		TeamGamePlan planned = CareerGamePlanProvider.Create(home, new GamePlanContext
		{
			TeamId = game.HomeTeamId,
			Lineup = new TeamLineup { TeamId = game.HomeTeamId, Slots = slots },
		});
		Assert.Equal(automatic.BattingOrder[^1].Player.Id, planned.BattingOrder[0].Player.Id);

		GameSimulationResult result = new BaseballGameSimulator().Simulate(
			game,
			planned,
			new AutomaticLineupProvider().Create(away),
			new SeededRandom(11));
		Assert.Equal(planned.BattingOrder[0].Player.Id, result.BoxScore.HomeBatting[0].PlayerId);
		Assert.Equal(result.HomeScore + result.AwayScore, result.BoxScore.HomeInningRuns.Sum() + result.BoxScore.AwayInningRuns.Sum());
		Assert.True(result.BoxScore.HomeInningRuns.Length >= BaseballSimulationConfig.Default.RegulationInnings);
		Assert.NotEmpty(result.Pitching);
	}

	[Fact]
	public void Rotation_starter_cycles_with_completed_starts()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 9, 2026);
		List<Player> roster = world.Players.Where(p => p.TeamId == world.Teams[0].Id && p.IsPitcher).ToList();
		Assert.True(roster.Count >= 3);
		var rotation = new TeamRotation
		{
			TeamId = world.Teams[0].Id,
			Starters = [roster[0].Id, roster[1].Id, roster[2].Id, null, null],
		};
		TeamGamePlan first = CareerGamePlanProvider.Create(roster, new GamePlanContext
		{
			TeamId = world.Teams[0].Id,
			Rotation = rotation,
			CompletedStarts = 0,
		});
		TeamGamePlan second = CareerGamePlanProvider.Create(roster, new GamePlanContext
		{
			TeamId = world.Teams[0].Id,
			Rotation = rotation,
			CompletedStarts = 1,
		});
		TeamGamePlan fifth = CareerGamePlanProvider.Create(roster, new GamePlanContext
		{
			TeamId = world.Teams[0].Id,
			Rotation = rotation,
			CompletedStarts = 5,
		});
		Assert.Equal(roster[0].Id, first.StartingPitcher.Id);
		Assert.Equal(roster[1].Id, second.StartingPitcher.Id);
		Assert.Equal(roster[2].Id, fifth.StartingPitcher.Id);
	}

	[Fact]
	public void Apply_result_accumulates_counting_stats_and_box_score()
	{
		GeneratedWorld generated = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 11, 2026);
		CareerState state = new()
		{
			Career = new Career { CurrentSeasonYear = 2026, CurrentDate = generated.Games[0].Date },
			World = generated.ToGameWorld(generated.Games[0].Date),
		};
		state.TeamRecords = generated.Teams
			.Where(team => team.RosterCap > 0)
			.Select(team => new TeamSeasonRecord { TeamId = team.Id, SeasonYear = 2026 })
			.ToList();
		ScheduledGame game = state.Games.First(g => !g.GameType.IsExhibition());
		List<Player> home = state.Players.Where(p => p.TeamId == game.HomeTeamId).ToList();
		List<Player> away = state.Players.Where(p => p.TeamId == game.AwayTeamId).ToList();
		GameSimulationResult result = new BaseballGameSimulator().Simulate(game, home, away, new SeededRandom(44));
		WorldDayProcessor.ApplyResult(state, game, result);
		Assert.Equal(GameStatus.Completed, game.Status);
		Assert.NotNull(state.BoxScoreFor(game.Id));
		Assert.Contains(state.BattingStats, s => s.PlateAppearances > 0);
		Assert.Contains(state.PitchingStats, s => s.OutsRecorded > 0);
		Assert.Contains(state.FieldingStats, s => s.Games > 0);
		Assert.Equal(1, state.RecordFor(result.WinningTeamId).Wins);
		Assert.Equal(1, state.RecordFor(result.LosingTeamId).Losses);
		Assert.Equal(result.Batting.Sum(line => line.StolenBases), state.BattingStats.Sum(s => s.StolenBases));
		Assert.Equal(result.Pitching.Count(line => line.Save), state.PitchingStats.Sum(s => s.Saves));
	}

	private static (GeneratedWorld World, BaseballGameSimulator Sim) World(
		BaseballSimulationConfig? config = null,
		int seed = 12345)
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, seed, 2026);
		return (world, new BaseballGameSimulator(config ?? BaseballSimulationConfig.Default));
	}

	private static GameSimulationResult Play(GeneratedWorld world, BaseballGameSimulator sim, int index)
	{
		ScheduledGame game = world.Games[index];
		List<Player> home = world.Players.Where(p => p.TeamId == game.HomeTeamId).ToList();
		List<Player> away = world.Players.Where(p => p.TeamId == game.AwayTeamId).ToList();
		return sim.Simulate(game, home, away, new SeededRandom(2000 + index));
	}

	private static int SumRuns(GameSimulationResult result, List<Player> team)
	{
		var ids = team.Select(p => p.Id).ToHashSet();
		return result.Batting.Where(b => ids.Contains(b.PlayerId)).Sum(b => b.Runs);
	}

	private static int SumHits(GameSimulationResult result, List<Player> team)
	{
		var ids = team.Select(p => p.Id).ToHashSet();
		return result.Batting.Where(b => ids.Contains(b.PlayerId)).Sum(b => b.Hits);
	}

	private static PlayerGamePitching SumPitching(GameSimulationResult result, List<Player> team)
	{
		var ids = team.Select(p => p.Id).ToHashSet();
		var total = new PlayerGamePitching { PlayerId = Guid.Empty };
		foreach (PlayerGamePitching line in result.Pitching)
		{
			if (!ids.Contains(line.PlayerId))
			{
				continue;
			}

			total.HitsAllowed += line.HitsAllowed;
			total.Walks += line.Walks;
			total.Strikeouts += line.Strikeouts;
			total.OutsRecorded += line.OutsRecorded;
		}

		return total;
	}

	private static Player Player(string last) => new()
	{
		Id = Guid.NewGuid(),
		FirstName = "Test",
		LastName = last,
		PrimaryPosition = PlayerPosition.CenterField,
		Ratings = PlayerRatings.Clamp(50, 50, 50, 50, 50, 50, 40, 40, 40, 40),
	};
}

public class SimulationBalanceTests
{
	[Fact]
	public void Batch_metrics_are_in_believable_bands()
	{
		SimulationBalanceMetrics metrics = SimulationBalanceRunner.Run(400, 12345);
		Assert.InRange(metrics.RunsPerTeamGame, 2.0, 10.0);
		Assert.InRange(metrics.HitsPerTeamGame, 4.0, 14.0);
		Assert.InRange(metrics.WalksPerTeamGame, 1.0, 10.0);
		Assert.InRange(metrics.StrikeoutsPerTeamGame, 2.0, 14.0);
		Assert.InRange(metrics.HomeRunsPerTeamGame, 0.0, 2.5);
		Assert.InRange(metrics.BattingAverage, 0.180, 0.360);
		Assert.InRange(metrics.ExtraInningRate, 0.0, 0.35);
		Assert.True(metrics.RunsPerTeamGame < 12, "Offense is wildly overtuned.");
		Assert.True(metrics.StrikeoutsPerTeamGame < 18, "Strikeouts are wildly overtuned.");
	}
}
