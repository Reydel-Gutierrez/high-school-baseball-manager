namespace HSBM.Domain;

/// <summary>A coach's decision on a tryout candidate.</summary>
public enum TryoutCall
{
	Pending = 0,
	Varsity = 1,
	JuniorVarsity = 2,
	Cut = 3,
}

public static class TryoutCallExtensions
{
	public static string ToLabel(this TryoutCall call) => call switch
	{
		TryoutCall.Varsity => "Varsity",
		TryoutCall.JuniorVarsity => "JV",
		TryoutCall.Cut => "Cut",
		_ => "Undecided",
	};
}
