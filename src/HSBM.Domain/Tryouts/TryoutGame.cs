namespace HSBM.Domain;

/// <summary>
/// One of the three camp scrimmages. Team Blue vs Team White is an
/// evaluation event, not a Cascade varsity result.
/// </summary>
public sealed class TryoutGame
{
	public const string BlueId = "tryout-blue";
	public const string WhiteId = "tryout-white";
	public const string BlueName = "Team Blue";
	public const string WhiteName = "Team White";
	public const int GameCount = 3;
	public const int Innings = 7;

	public TryoutGame(
		int number,
		TryoutDay day,
		DateOnly date,
		int blueRuns,
		int whiteRuns,
		int blueHits,
		int whiteHits,
		int blueErrors,
		int whiteErrors,
		int[] blueInningRuns,
		int[] whiteInningRuns,
		IReadOnlyList<TryoutGameBatter> blueBatters,
		IReadOnlyList<TryoutGameBatter> whiteBatters,
		IReadOnlyList<TryoutGamePitcher> bluePitchers,
		IReadOnlyList<TryoutGamePitcher> whitePitchers,
		IReadOnlyList<TryoutStandout> standouts,
		string winningPitcher,
		string losingPitcher,
		string? save,
		IReadOnlyList<string> fieldingPlays)
	{
		Number = number;
		Day = day;
		Date = date;
		BlueRuns = blueRuns;
		WhiteRuns = whiteRuns;
		BlueHits = blueHits;
		WhiteHits = whiteHits;
		BlueErrors = blueErrors;
		WhiteErrors = whiteErrors;
		BlueInningRuns = blueInningRuns;
		WhiteInningRuns = whiteInningRuns;
		BlueBatters = blueBatters;
		WhiteBatters = whiteBatters;
		BluePitchers = bluePitchers;
		WhitePitchers = whitePitchers;
		Standouts = standouts;
		WinningPitcher = winningPitcher;
		LosingPitcher = losingPitcher;
		Save = save;
		FieldingPlays = fieldingPlays;
	}

	public int Number { get; }

	public TryoutDay Day { get; }

	public DateOnly Date { get; }

	public int BlueRuns { get; }

	public int WhiteRuns { get; }

	public int BlueHits { get; }

	public int WhiteHits { get; }

	public int BlueErrors { get; }

	public int WhiteErrors { get; }

	public int[] BlueInningRuns { get; }

	public int[] WhiteInningRuns { get; }

	public IReadOnlyList<TryoutGameBatter> BlueBatters { get; }

	public IReadOnlyList<TryoutGameBatter> WhiteBatters { get; }

	public IReadOnlyList<TryoutGamePitcher> BluePitchers { get; }

	public IReadOnlyList<TryoutGamePitcher> WhitePitchers { get; }

	public IReadOnlyList<TryoutStandout> Standouts { get; }

	public string WinningPitcher { get; }

	public string LosingPitcher { get; }

	public string? Save { get; }

	public IReadOnlyList<string> FieldingPlays { get; }

	public string Title => $"TRYOUT GAME {Number} OF {GameCount}";

	public const string Time = "10:00 AM";
}

public sealed record TryoutGameBatter(
	string Name,
	string LastName,
	string Position,
	int AtBats,
	int Runs,
	int Hits,
	int HomeRuns,
	int Rbi);

public sealed record TryoutGamePitcher(
	string Name,
	string LastName,
	string Innings,
	int Hits,
	int Walks,
	int Strikeouts);

public sealed record TryoutStandout(string Name, string Line);
