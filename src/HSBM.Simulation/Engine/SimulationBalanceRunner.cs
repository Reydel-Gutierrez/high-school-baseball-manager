using HSBM.Domain;

namespace HSBM.Simulation;

public sealed class SimulationBalanceMetrics
{
	public int Games { get; init; }
	public double RunsPerTeamGame { get; init; }
	public double HitsPerTeamGame { get; init; }
	public double WalksPerTeamGame { get; init; }
	public double StrikeoutsPerTeamGame { get; init; }
	public double HomeRunsPerTeamGame { get; init; }
	public double ExtraInningRate { get; init; }
	public double BattingAverage { get; init; }
}

public static class SimulationBalanceRunner
{
	public static SimulationBalanceMetrics Run(int games, int seed, BaseballSimulationConfig? config = null)
	{
		var catalog = LeagueCatalog.Standard;
		var world = new CareerWorldGenerator().Generate(catalog, seed, LeagueCatalog.DefaultSeasonYear);
		var simulator = new BaseballGameSimulator(config ?? BaseballSimulationConfig.Default);
		ISeededRandom rng = new SeededRandom(seed).Derive(9001);

		int teamGames = 0;
		long runs = 0;
		long hits = 0;
		long walks = 0;
		long strikeouts = 0;
		long homeRuns = 0;
		int extras = 0;
		long atBats = 0;
		long batterHits = 0;

		int limit = Math.Min(games, world.Games.Count);
		for (int i = 0; i < limit; i++)
		{
			ScheduledGame scheduled = world.Games[i];
			List<Player> home = world.Players.Where(p => p.TeamId == scheduled.HomeTeamId).ToList();
			List<Player> away = world.Players.Where(p => p.TeamId == scheduled.AwayTeamId).ToList();
			GameSimulationResult result = simulator.Simulate(scheduled, home, away, rng.Derive(i + 1));
			teamGames += 2;
			runs += result.HomeScore + result.AwayScore;
			hits += result.HomeHits + result.AwayHits;
			walks += result.HomeWalks + result.AwayWalks;
			strikeouts += result.HomeStrikeouts + result.AwayStrikeouts;
			homeRuns += result.HomeHomeRuns + result.AwayHomeRuns;
			if (result.ExtraInnings)
			{
				extras++;
			}

			foreach (PlayerGameBatting line in result.Batting)
			{
				atBats += line.AtBats;
				batterHits += line.Hits;
			}
		}

		return new SimulationBalanceMetrics
		{
			Games = limit,
			RunsPerTeamGame = teamGames == 0 ? 0 : runs / (double)teamGames,
			HitsPerTeamGame = teamGames == 0 ? 0 : hits / (double)teamGames,
			WalksPerTeamGame = teamGames == 0 ? 0 : walks / (double)teamGames,
			StrikeoutsPerTeamGame = teamGames == 0 ? 0 : strikeouts / (double)teamGames,
			HomeRunsPerTeamGame = teamGames == 0 ? 0 : homeRuns / (double)teamGames,
			ExtraInningRate = limit == 0 ? 0 : extras / (double)limit,
			BattingAverage = atBats == 0 ? 0 : batterHits / (double)atBats,
		};
	}
}
