namespace HSBM.Domain;

public sealed class GameBoxScore
{
	public Guid GameId { get; set; }
	public int[] AwayInningRuns { get; set; } = [];
	public int[] HomeInningRuns { get; set; } = [];
	public int AwayHits { get; set; }
	public int HomeHits { get; set; }
	public int AwayErrors { get; set; }
	public int HomeErrors { get; set; }
	public bool HomeSkippedLast { get; set; }
	public Guid? SavePitcherId { get; set; }
	public List<BoxScoreBattingLine> AwayBatting { get; set; } = [];
	public List<BoxScoreBattingLine> HomeBatting { get; set; } = [];
	public List<BoxScorePitchingLine> AwayPitching { get; set; } = [];
	public List<BoxScorePitchingLine> HomePitching { get; set; } = [];
	public List<string> FieldingPlays { get; set; } = [];
}

public sealed class BoxScoreBattingLine
{
	public Guid PlayerId { get; set; }
	public string Name { get; set; } = "";
	public string LastName { get; set; } = "";
	public string Position { get; set; } = "";
	public int AtBats { get; set; }
	public int Runs { get; set; }
	public int Hits { get; set; }
	public int HomeRuns { get; set; }
	public int RunsBattedIn { get; set; }
}

public sealed class BoxScorePitchingLine
{
	public Guid PlayerId { get; set; }
	public string Name { get; set; } = "";
	public string LastName { get; set; } = "";
	public int OutsRecorded { get; set; }
	public int Hits { get; set; }
	public int Walks { get; set; }
	public int Strikeouts { get; set; }

	public string InningsDisplay => $"{OutsRecorded / 3}.{OutsRecorded % 3}";
}
