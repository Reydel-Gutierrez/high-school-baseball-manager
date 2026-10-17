namespace HSBM.Simulation;

/// <summary>
/// Tunable probabilities and rating weights for the plate-appearance model.
/// Keep numbers here so balance can be adjusted without hunting through the engine.
/// </summary>
public sealed class BaseballSimulationConfig
{
	public static BaseballSimulationConfig Default { get; } = new();

	public int RegulationInnings { get; init; } = 9;
	public int MaxInnings { get; init; } = 24;

	public double BaseStrikeoutRate { get; init; } = 0.195;
	public double StrikeoutRatingWeight { get; init; } = 0.17;

	public double BaseWalkRate { get; init; } = 0.085;
	public double WalkRatingWeight { get; init; } = 0.13;

	public double BaseHomeRunRate { get; init; } = 0.018;
	public double HomeRunRatingWeight { get; init; } = 0.035;

	public double BaseBabip { get; init; } = 0.315;
	public double BabipRatingWeight { get; init; } = 0.11;

	public double DoubleShare { get; init; } = 0.20;
	public double TripleShare { get; init; } = 0.025;

	public double SingleRunnerFromSecondScores { get; init; } = 0.80;
	public double SingleRunnerFromFirstToThird { get; init; } = 0.22;
	public double DoubleRunnerFromFirstScores { get; init; } = 0.65;

	public double MinOutcomeRate { get; init; } = 0.004;
	public double MaxStrikeoutRate { get; init; } = 0.40;
	public double MaxWalkRate { get; init; } = 0.20;
	public double MaxHomeRunRate { get; init; } = 0.075;

	public double StarterPitchBase { get; init; } = 68;
	public double StarterPitchPerStamina { get; init; } = 0.52;
	public double RelieverPitchBase { get; init; } = 24;
	public double RelieverPitchPerStamina { get; init; } = 0.18;
	public double FatiguePerPitch { get; init; } = 0.0016;
	public double MaxFatigue { get; init; } = 0.16;
	public double ErrorBaseRate { get; init; } = 0.026;
	public double ErrorFieldingWeight { get; init; } = 0.038;
	public double StealAttemptBase { get; init; } = 0.055;
	public double StealRatingWeight { get; init; } = 0.16;
	public int StarterWinOuts { get; init; } = 12;
}
