namespace HSBM.Simulation;

public sealed record WorldGenerationProgress(string Label, int StepIndex, int StepCount);

public static class WorldGenerationSteps
{
	public const int Count = 7;
	public const string Histories = "Generating program histories...";
	public const string Varsity = "Generating returning varsity rosters...";
	public const string JuniorVarsity = "Generating returning JV rosters...";
	public const string Identities = "Generating player identities...";
	public const string Tryouts = "Posting tryout sign-up sheets...";
	public const string Schedule = "Building season schedule...";

	public static string Programs(int count) => $"Creating {count} programs...";
}
