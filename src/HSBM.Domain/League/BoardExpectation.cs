namespace HSBM.Domain;

/// <summary>
/// What the athletic board will accept this season, and what they actually want.
/// </summary>
public enum BoardExpectation
{
	Rebuild = 0,
	WinningSeason = 1,
	Compete = 2,
	DistrictPlayoffs = 3,
	WinDistrict = 4,
	StateTitle = 5,
}

public static class BoardExpectationExtensions
{
	public static string ToLabel(this BoardExpectation expectation) => expectation switch
	{
		BoardExpectation.Rebuild => "Rebuild",
		BoardExpectation.WinningSeason => "Winning Season",
		BoardExpectation.Compete => "Compete",
		BoardExpectation.DistrictPlayoffs => "District Playoffs",
		BoardExpectation.WinDistrict => "Win District",
		BoardExpectation.StateTitle => "State Title",
		_ => "Compete",
	};

	public static string ToShortLabel(this BoardExpectation expectation) => expectation switch
	{
		BoardExpectation.DistrictPlayoffs => "Playoffs",
		_ => expectation.ToLabel(),
	};

	public static BoardExpectation StepDown(this BoardExpectation expectation)
	{
		int next = Math.Max((int)BoardExpectation.Rebuild, (int)expectation - 1);
		return (BoardExpectation)next;
	}
}
