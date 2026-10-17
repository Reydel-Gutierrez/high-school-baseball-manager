using HSBM.Domain;
using HSBM.Simulation;
using Xunit.Abstractions;

namespace HSBM.Simulation.Tests;

public class MultiSeasonSimulationTests
{
	private readonly ITestOutputHelper _output;

	public MultiSeasonSimulationTests(ITestOutputHelper output) => _output = output;

	[Fact(Timeout = 300000)]
	public async Task Ten_automated_seasons_stay_healthy()
	{
		(CareerState state, LeagueSimulationResult result) = await Task.Run(() =>
		{
			CareerState live = CareerWorldRunner.NewCareer(2026, name: "Decade");
			LeagueSimulationResult ran = CareerWorldRunner.RunSeasons(live, CareerWorldRunner.DefaultSeasons);
			return (live, ran);
		});
		_output.WriteLine(result.Format());

		Assert.Equal(10, result.SeasonsCompleted);
		Assert.Equal(10, result.Seasons.Count);
		Assert.Equal(2026 + 10, state.Career.CurrentSeasonYear);
		Assert.Equal(new DateOnly(2036, 1, 5), state.Career.CurrentDate);
		Assert.True(result.GamesPlayed > 5000, $"Expected a decade of games, found {result.GamesPlayed}.");
		Assert.True(result.IntegrityIssues.Count == 0, string.Join(Environment.NewLine, result.IntegrityIssues));
		Assert.All(result.Seasons, season =>
		{
			Assert.True(season.GamesCompleted >= 200, $"{season.SeasonYear} only completed {season.GamesCompleted} varsity counting games.");
			Assert.False(string.IsNullOrWhiteSpace(season.StateChampion));
			Assert.InRange(season.RunsPerTeamGame, 2.0, 12.0);
		});
		Assert.Equal(10, result.ChampionshipDistribution.Sum(row => row.Titles));
		Assert.True(result.League.ActivePlayers > 250, $"Returning rosters collapsed to {result.League.ActivePlayers}.");
		Assert.InRange(result.League.MeanOverall, 40, 85);
		Assert.Contains(state.Players, player => player.IsUnsigned && !player.Alumni);
	}
}

public class LeagueBalanceDiagnosticsTests
{
	private readonly ITestOutputHelper _output;

	public LeagueBalanceDiagnosticsTests(ITestOutputHelper output) => _output = output;

	[Fact]
	public void Generated_world_reports_averages_and_player_distributions()
	{
		CareerState state = CareerWorldRunner.NewCareer(12345);
		LeagueBalanceSnapshot league = LeagueBalanceReport.SnapshotLeague(state);
		string text = LeagueBalanceReport.FormatWorld(state);
		_output.WriteLine(text);

		Assert.Contains("League averages", text, StringComparison.Ordinal);
		Assert.Contains("grades:", text, StringComparison.Ordinal);
		Assert.Contains("positions:", text, StringComparison.Ordinal);
		Assert.True(league.ActivePlayers > 800);
		Assert.InRange(league.MeanOverall, 40, 85);
		Assert.InRange(league.MeanContact, 30, 80);
		Assert.True(league.GradeMix.Count >= 3);
		Assert.Contains(league.PositionMix, bucket => bucket.Label == "C");
		Assert.Contains(league.PositionMix, bucket => bucket.Label is "SP" or "RP");
		Assert.Contains(league.TalentTiers, bucket => bucket.Label == "Powerhouse");
		Assert.Contains(league.TalentTiers, bucket => bucket.Label == "Rebuilding");
	}

	[Fact]
	public void Game_batch_metrics_stay_in_believable_bands()
	{
		SimulationBalanceMetrics metrics = SimulationBalanceRunner.Run(400, 12345);
		_output.WriteLine(
			$"runs {metrics.RunsPerTeamGame:0.00}  hits {metrics.HitsPerTeamGame:0.00}  "
			+ $"BB {metrics.WalksPerTeamGame:0.00}  K {metrics.StrikeoutsPerTeamGame:0.00}  "
			+ $"HR {metrics.HomeRunsPerTeamGame:0.00}  AVG {metrics.BattingAverage:0.000}");
		Assert.InRange(metrics.RunsPerTeamGame, 2.0, 10.0);
		Assert.InRange(metrics.HitsPerTeamGame, 4.0, 14.0);
		Assert.InRange(metrics.BattingAverage, 0.180, 0.360);
	}
}
