using HSBM.Domain;
using HSBM.Simulation;
using HSBM.Simulation.Tests.Reference;

namespace HSBM.Simulation.Tests;

public class LiveGameTests
{
	private static readonly GeneratedWorld World = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 2468, 2026);

	private static (ScheduledGame Game, TeamGamePlan Home, TeamGamePlan Away, List<Player> HomeRoster, List<Player> AwayRoster) Matchup(int index)
	{
		List<ScheduledGame> games = World.Games.Where(g => g.TeamLevel == TeamLevel.Varsity).ToList();
		ScheduledGame game = games[index % games.Count];
		List<Player> home = World.Players.Where(p => p.TeamId == game.HomeTeamId).ToList();
		List<Player> away = World.Players.Where(p => p.TeamId == game.AwayTeamId).ToList();
		var lineups = new AutomaticLineupProvider();
		return (game, lineups.Create(home), lineups.Create(away), home, away);
	}

	[Fact]
	public void Untouched_live_game_matches_the_original_simulator_exactly()
	{
		var legacy = new LegacyBaseballGameSimulator();
		var current = new BaseballGameSimulator();
		for (int i = 0; i < 150; i++)
		{
			var (game, home, away, _, _) = Matchup(i);
			GameSimulationResult expected = legacy.Simulate(game, home, away, new SeededRandom(9000 + i));
			GameSimulationResult actual = current.Simulate(game, home, away, new SeededRandom(9000 + i));

			Assert.Equal(expected.HomeScore, actual.HomeScore);
			Assert.Equal(expected.AwayScore, actual.AwayScore);
			Assert.Equal(expected.InningsPlayed, actual.InningsPlayed);
			Assert.Equal(expected.HomeInningRuns, actual.HomeInningRuns);
			Assert.Equal(expected.AwayInningRuns, actual.AwayInningRuns);
			Assert.Equal(expected.HomeHits, actual.HomeHits);
			Assert.Equal(expected.AwayHits, actual.AwayHits);
			Assert.Equal(expected.HomeSkippedLast, actual.HomeSkippedLast);
			Assert.Equal(expected.WinningPitcherId, actual.WinningPitcherId);
			Assert.Equal(expected.LosingPitcherId, actual.LosingPitcherId);
			Assert.Equal(expected.SavePitcherId, actual.SavePitcherId);
			Assert.Equal(
				expected.Pitching.Select(p => (p.PlayerId, p.OutsRecorded, p.HitsAllowed, p.EarnedRuns, p.Strikeouts)),
				actual.Pitching.Select(p => (p.PlayerId, p.OutsRecorded, p.HitsAllowed, p.EarnedRuns, p.Strikeouts)));
			Assert.Equal(
				expected.Batting.OrderBy(b => b.PlayerId).Select(b => (b.PlayerId, b.AtBats, b.Hits, b.HomeRuns, b.RunsBattedIn, b.StolenBases)),
				actual.Batting.OrderBy(b => b.PlayerId).Select(b => (b.PlayerId, b.AtBats, b.Hits, b.HomeRuns, b.RunsBattedIn, b.StolenBases)));
		}
	}

	[Fact]
	public void Stepping_one_plate_appearance_at_a_time_equals_finishing_at_once()
	{
		var simulator = new BaseballGameSimulator();
		for (int i = 0; i < 25; i++)
		{
			var (game, home, away, _, _) = Matchup(i);
			LiveGame stepped = simulator.StartLive(game, home, away, new SeededRandom(i));
			int guard = 0;
			while (!stepped.IsFinal && guard++ < 1000)
			{
				stepped.Step();
			}

			GameSimulationResult whole = simulator.Simulate(game, home, away, new SeededRandom(i));
			GameSimulationResult parts = stepped.Finish();
			Assert.Equal(whole.HomeScore, parts.HomeScore);
			Assert.Equal(whole.AwayScore, parts.AwayScore);
			Assert.Equal(whole.HomeInningRuns, parts.HomeInningRuns);
		}
	}

	[Fact]
	public void Play_by_play_scoreboard_matches_the_final_score()
	{
		var (game, home, away, _, _) = Matchup(3);
		LiveGame live = new BaseballGameSimulator().StartLive(game, home, away, new SeededRandom(77));
		GameSimulationResult result = live.Finish();
		LivePlay last = live.Log[^1];

		Assert.Equal(LivePlayKind.Final, last.Kind);
		Assert.Equal(result.HomeScore, last.HomeScore);
		Assert.Equal(result.AwayScore, last.AwayScore);
		Assert.Contains(live.Log, play => play.Kind == LivePlayKind.HalfInning);
		Assert.All(live.Log, play => Assert.False(string.IsNullOrWhiteSpace(play.Text)));
	}

	[Fact]
	public void Manager_can_change_pitchers_and_reliever_gets_a_line()
	{
		var (game, home, away, _, _) = Matchup(5);
		LiveGame live = new BaseballGameSimulator().StartLive(game, home, away, new SeededRandom(5));
		live.AutoManageHome = false;
		live.Step();
		IReadOnlyList<Player> pen = live.AvailableRelievers(home: true);
		Assert.NotEmpty(pen);
		Player reliever = pen[0];

		Assert.True(live.ChangePitcher(home: true, reliever));
		Assert.Equal(reliever.Id, live.Pitcher(home: true).Id);
		Assert.DoesNotContain(live.AvailableRelievers(home: true), p => p.Id == reliever.Id);

		GameSimulationResult result = live.Finish();
		Assert.Contains(result.Pitching, line => line.PlayerId == reliever.Id && line.OutsRecorded > 0);
	}

	[Fact]
	public void Without_auto_management_the_starter_is_never_pulled()
	{
		var (game, home, away, _, _) = Matchup(8);
		LiveGame live = new BaseballGameSimulator().StartLive(game, home, away, new SeededRandom(8));
		live.AutoManageHome = false;
		GameSimulationResult result = live.Finish();
		int homePitchers = result.Pitching.Count(line => home.StartingPitcher.Id == line.PlayerId
			|| home.Bullpen.Any(p => p.Id == line.PlayerId)
			|| home.Closer?.Id == line.PlayerId);
		Assert.Equal(1, homePitchers);
	}

	[Fact]
	public void Pinch_hitter_takes_the_slot_and_shows_in_the_box_score()
	{
		var (game, home, away, homeRoster, awayRoster) = Matchup(11);
		LiveGame live = new BaseballGameSimulator().StartLive(game, home, away, new SeededRandom(11), homeRoster, awayRoster);
		IReadOnlyList<Player> bench = live.AvailableBench(home: false);
		if (bench.Count == 0)
		{
			return; // roster exactly nine position players; nothing to test
		}

		Player sub = bench[0];
		Assert.True(live.PinchHit(sub));
		Assert.Equal(sub.Id, live.NextBatter.Id);
		GameSimulationResult result = live.Finish();
		Assert.Contains(result.BoxScore.AwayBatting, line => line.PlayerId == sub.Id);
		Assert.Contains(result.Batting, line => line.PlayerId == sub.Id && line.PlateAppearances > 0);
	}

	[Fact]
	public void Intentional_walk_puts_the_batter_on_first()
	{
		var (game, home, away, _, _) = Matchup(13);
		LiveGame live = new BaseballGameSimulator().StartLive(game, home, away, new SeededRandom(13));
		Player batter = live.NextBatter;
		live.QueueIntentionalWalk();
		IReadOnlyList<LivePlay> plays = live.Step();

		Assert.Contains(plays, p => p.Kind == LivePlayKind.IntentionalWalk);
		Assert.Equal(batter.Id, live.OnFirst?.Id);
		Assert.Equal(1, live.BattingLine(batter.Id)!.Walks);
	}

	[Fact]
	public void Bunt_and_steal_calls_resolve_and_keep_the_game_consistent()
	{
		var (game, home, away, _, _) = Matchup(17);
		for (int seed = 0; seed < 40; seed++)
		{
			LiveGame live = new BaseballGameSimulator().StartLive(game, home, away, new SeededRandom(seed));
			live.AutoManageAway = false;
			int guard = 0;
			while (!live.IsFinal && guard++ < 1000)
			{
				if (live.TopHalf && live.CanSteal && guard % 3 == 0)
				{
					live.Steal();
					continue;
				}

				if (live.TopHalf && live.CanBunt && guard % 5 == 0)
				{
					live.QueueBunt();
				}

				live.Step();
			}

			GameSimulationResult result = live.Finish();
			Assert.Equal(result.AwayInningRuns.Sum(), result.AwayScore);
			Assert.Equal(result.HomeInningRuns.Sum(), result.HomeScore);
			Assert.NotEqual(result.HomeScore, result.AwayScore);
			Assert.InRange(live.Outs, 0, 3);
		}
	}

	[Fact]
	public void Preloaded_result_is_returned_instead_of_simulating()
	{
		var (game, home, away, _, _) = Matchup(21);
		var simulator = new BaseballGameSimulator();
		LiveGame live = simulator.StartLive(game, home, away, new SeededRandom(1));
		live.AutoManageHome = false;
		GameSimulationResult played = live.Finish();

		simulator.Preload(played);
		GameSimulationResult applied = simulator.Simulate(game, home, away, new SeededRandom(999));
		Assert.Same(played, applied);

		GameSimulationResult again = simulator.Simulate(game, home, away, new SeededRandom(999));
		Assert.NotSame(played, again);
	}
}
