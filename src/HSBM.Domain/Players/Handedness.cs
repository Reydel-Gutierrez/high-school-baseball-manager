namespace HSBM.Domain;

public enum BattingSide
{
	Right = 0,
	Left = 1,
	Switch = 2,
}

public enum ThrowingHand
{
	Right = 0,
	Left = 1,
}

public static class HandednessExtensions
{
	public static string ToAbbrev(this BattingSide bats) => bats switch
	{
		BattingSide.Left => "L",
		BattingSide.Switch => "S",
		_ => "R",
	};

	public static string ToAbbrev(this ThrowingHand throws) =>
		throws == ThrowingHand.Left ? "L" : "R";

	public static string ToBatsThrowsLabel(this BattingSide bats, ThrowingHand throws) =>
		$"{bats.ToAbbrev()}/{throws.ToAbbrev()}";

	public static void ParseBatsThrows(string? label, out BattingSide bats, out ThrowingHand throws)
	{
		string[] parts = (label ?? "R/R").Split('/');
		bats = parts[0].Trim().ToUpperInvariant() switch
		{
			"L" => BattingSide.Left,
			"S" => BattingSide.Switch,
			_ => BattingSide.Right,
		};
		throws = parts.Length > 1 && parts[1].Trim().Equals("L", StringComparison.OrdinalIgnoreCase)
			? ThrowingHand.Left
			: ThrowingHand.Right;
	}
}
