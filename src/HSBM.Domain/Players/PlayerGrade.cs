namespace HSBM.Domain;

public enum PlayerGrade
{
	Freshman = 9,
	Sophomore = 10,
	Junior = 11,
	Senior = 12,
}

public static class PlayerGradeExtensions
{
	public static string ToAbbrev(this PlayerGrade grade) => grade switch
	{
		PlayerGrade.Freshman => "FR",
		PlayerGrade.Sophomore => "SO",
		PlayerGrade.Junior => "JR",
		_ => "SR",
	};

	public static PlayerGrade FromAbbrev(string? year)
	{
		return (year ?? string.Empty).Trim().ToUpperInvariant() switch
		{
			"FR" or "9" => PlayerGrade.Freshman,
			"SO" or "10" => PlayerGrade.Sophomore,
			"JR" or "11" => PlayerGrade.Junior,
			_ => PlayerGrade.Senior,
		};
	}

	public static int TypicalAge(this PlayerGrade grade) => grade switch
	{
		PlayerGrade.Freshman => 15,
		PlayerGrade.Sophomore => 16,
		PlayerGrade.Junior => 17,
		_ => 18,
	};

	public static int RemainingEligibility(this PlayerGrade grade) => grade switch
	{
		PlayerGrade.Freshman => 4,
		PlayerGrade.Sophomore => 3,
		PlayerGrade.Junior => 2,
		_ => 1,
	};

	public static PlayerGrade? Advance(this PlayerGrade grade) => grade switch
	{
		PlayerGrade.Freshman => PlayerGrade.Sophomore,
		PlayerGrade.Sophomore => PlayerGrade.Junior,
		PlayerGrade.Junior => PlayerGrade.Senior,
		_ => null,
	};
}
