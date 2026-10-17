namespace HSBM.Domain;

public enum PlayerPosition
{
	Catcher = 0,
	FirstBase = 1,
	SecondBase = 2,
	ThirdBase = 3,
	Shortstop = 4,
	LeftField = 5,
	CenterField = 6,
	RightField = 7,
	StartingPitcher = 8,
	ReliefPitcher = 9,
}

public static class PlayerPositionExtensions
{
	public static bool IsPitcher(this PlayerPosition position) =>
		position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher;

	public static bool IsInfielder(this PlayerPosition position) =>
		position is PlayerPosition.FirstBase
			or PlayerPosition.SecondBase
			or PlayerPosition.ThirdBase
			or PlayerPosition.Shortstop;

	public static bool IsOutfielder(this PlayerPosition position) =>
		position is PlayerPosition.LeftField
			or PlayerPosition.CenterField
			or PlayerPosition.RightField;

	public static string ToCode(this PlayerPosition position) => position switch
	{
		PlayerPosition.Catcher => "C",
		PlayerPosition.FirstBase => "1B",
		PlayerPosition.SecondBase => "2B",
		PlayerPosition.ThirdBase => "3B",
		PlayerPosition.Shortstop => "SS",
		PlayerPosition.LeftField => "LF",
		PlayerPosition.CenterField => "CF",
		PlayerPosition.RightField => "RF",
		PlayerPosition.StartingPitcher => "SP",
		PlayerPosition.ReliefPitcher => "RP",
		_ => "UTIL",
	};

	/// <summary>
	/// Presentation label compatible with the current UI (RHP/LHP instead of SP/RP).
	/// </summary>
	public static string ToDisplayLabel(this PlayerPosition position, ThrowingHand throws)
	{
		if (position.IsPitcher())
		{
			return throws == ThrowingHand.Left ? "LHP" : "RHP";
		}

		return position.ToCode();
	}

	public static PlayerPosition ParseLabel(string? label)
	{
		string code = (label ?? string.Empty).Split('/')[0].Trim().ToUpperInvariant();
		return code switch
		{
			"C" => PlayerPosition.Catcher,
			"1B" => PlayerPosition.FirstBase,
			"2B" => PlayerPosition.SecondBase,
			"3B" => PlayerPosition.ThirdBase,
			"SS" => PlayerPosition.Shortstop,
			"LF" => PlayerPosition.LeftField,
			"CF" => PlayerPosition.CenterField,
			"RF" => PlayerPosition.RightField,
			"OF" => PlayerPosition.CenterField,
			"INF" or "UTIL" or "DH" => PlayerPosition.SecondBase,
			"RP" => PlayerPosition.ReliefPitcher,
			"LHP" or "RHP" or "P" or "SP" => PlayerPosition.StartingPitcher,
			_ => PlayerPosition.SecondBase,
		};
	}
}
