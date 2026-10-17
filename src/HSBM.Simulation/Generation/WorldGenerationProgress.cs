namespace HSBM.Simulation;

public sealed record WorldGenerationProgress(string Label, int StepIndex, int StepCount);

public static class WorldGenerationSteps
{
	public const int Count = 8;
	public const string Histories = "Generating program histories...";
	public const string Varsity = "Generating returning varsity rosters...";
	public const string JuniorVarsity = "Generating returning JV rosters...";
	public const string Identities = "Generating player identities...";
	public const string OpenPlay = "Building District Open Play classes...";
	public const string OpenPlayGames = "Simulating Open Play games...";
	public const string Schedule = "Building season schedule...";

	public static string Programs(int count) => $"Creating {count} programs...";
}
