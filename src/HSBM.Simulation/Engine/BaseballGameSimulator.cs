using HSBM.Domain;

namespace HSBM.Simulation;

public sealed class PlayerGameBatting
{
	public required Guid PlayerId { get; init; }
	public string Position { get; set; } = "";
	public int PlateAppearances { get; set; }
	public int AtBats { get; set; }
	public int Runs { get; set; }
	public int Hits { get; set; }
	public int Doubles { get; set; }
	public int Triples { get; set; }
	public int HomeRuns { get; set; }
	public int RunsBattedIn { get; set; }
	public int Walks { get; set; }
	public int Strikeouts { get; set; }
	public int StolenBases { get; set; }
}

public sealed class PlayerGamePitching
{
	public required Guid PlayerId { get; init; }
	public int GamesStarted { get; set; }
	public int OutsRecorded { get; set; }
	public int HitsAllowed { get; set; }
	public int RunsAllowed { get; set; }
	public int EarnedRuns { get; set; }
	public int Walks { get; set; }
	public int Strikeouts { get; set; }
	public int HomeRunsAllowed { get; set; }
	public bool Win { get; set; }
	public bool Loss { get; set; }
	public bool Save { get; set; }
}

public sealed class PlayerGameFielding
{
	public required Guid PlayerId { get; init; }
	public int Putouts { get; set; }
	public int Assists { get; set; }
	public int Errors { get; set; }
}

public sealed class GameSimulationResult
{
	public required Guid GameId { get; init; }
	public required string HomeTeamId { get; init; }
	public required string AwayTeamId { get; init; }
	public int HomeScore { get; init; }
	public int AwayScore { get; init; }
	public int InningsPlayed { get; init; }
	public int HomeHits { get; init; }
	public int AwayHits { get; init; }
	public int HomeWalks { get; init; }
	public int AwayWalks { get; init; }
	public int HomeStrikeouts { get; init; }
	public int AwayStrikeouts { get; init; }
	public int HomeHomeRuns { get; init; }
	public int AwayHomeRuns { get; init; }
	public int HomeErrors { get; init; }
	public int AwayErrors { get; init; }
	public bool ExtraInnings { get; init; }
	public bool HomeSkippedLast { get; init; }
	public required Guid WinningPitcherId { get; init; }
	public required Guid LosingPitcherId { get; init; }
	public Guid? SavePitcherId { get; init; }
	public required int[] HomeInningRuns { get; init; }
	public required int[] AwayInningRuns { get; init; }
	public required IReadOnlyList<PlayerGameBatting> Batting { get; init; }
	public required IReadOnlyList<PlayerGamePitching> Pitching { get; init; }
	public required IReadOnlyList<PlayerGameFielding> Fielding { get; init; }
	public required IReadOnlyList<string> FieldingPlays { get; init; }
	public required GameBoxScore BoxScore { get; init; }

	public string WinningTeamId => HomeScore > AwayScore ? HomeTeamId : AwayTeamId;
	public string LosingTeamId => HomeScore > AwayScore ? AwayTeamId : HomeTeamId;
}

public sealed class BaseballGameSimulator
{
	private readonly BaseballSimulationConfig _config;
	private readonly ILineupProvider _lineups;
	private readonly Dictionary<Guid, GameSimulationResult> _preplayed = new();

	public BaseballGameSimulator(
		BaseballSimulationConfig? config = null,
		ILineupProvider? lineups = null)
	{
		_config = config ?? BaseballSimulationConfig.Default;
		_lineups = lineups ?? new AutomaticLineupProvider();
	}

	public BaseballSimulationConfig Config => _config;

	/// <summary>
	/// Register a result the user already played live. The next time the
	/// day processor reaches that game it is applied as-is instead of being
	/// simulated, so standings, stats, injuries, and news treat it like any
	/// other game.
	/// </summary>
	public void Preload(GameSimulationResult result) => _preplayed[result.GameId] = result;

	public GameSimulationResult Simulate(
		ScheduledGame game,
		IReadOnlyList<Player> homeRoster,
		IReadOnlyList<Player> awayRoster,
		ISeededRandom rng)
	{
		return Simulate(
			game,
			_lineups.Create(homeRoster),
			_lineups.Create(awayRoster),
			rng);
	}

	public GameSimulationResult Simulate(
		ScheduledGame game,
		TeamGamePlan home,
		TeamGamePlan away,
		ISeededRandom rng)
	{
		if (_preplayed.Remove(game.Id, out GameSimulationResult? played))
		{
			return played;
		}

		return new LiveGame(game, home, away, rng, _config).Finish();
	}

	/// <summary>Start a game that will be played one plate appearance at a time.</summary>
	public LiveGame StartLive(
		ScheduledGame game,
		TeamGamePlan home,
		TeamGamePlan away,
		ISeededRandom rng,
		IReadOnlyList<Player>? homeBench = null,
		IReadOnlyList<Player>? awayBench = null) =>
		new(game, home, away, rng, _config, homeBench, awayBench);
}
