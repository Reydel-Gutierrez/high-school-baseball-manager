namespace HSBM.Domain;

public sealed class PitchingSeasonStats
{
	public Guid PlayerId { get; set; }
	public int SeasonYear { get; set; }
	public string TeamId { get; set; } = "";
	public int Games { get; set; }
	public int GamesStarted { get; set; }
	public int Wins { get; set; }
	public int Losses { get; set; }
	public int OutsRecorded { get; set; }
	public int HitsAllowed { get; set; }
	public int RunsAllowed { get; set; }
	public int EarnedRuns { get; set; }
	public int Walks { get; set; }
	public int Strikeouts { get; set; }
	public int HomeRunsAllowed { get; set; }
	public int Saves { get; set; }

	public double InningsPitched => OutsRecorded / 3.0;

	public string InningsPitchedDisplay => $"{OutsRecorded / 3}.{OutsRecorded % 3}";

	/// <summary>High school ERA: earned runs per seven innings (21 outs).</summary>
	public double Era => OutsRecorded == 0 ? 0 : EarnedRuns * 21.0 / OutsRecorded;

	public double Whip => OutsRecorded == 0 ? 0 : (Walks + HitsAllowed) * 3.0 / OutsRecorded;

	public void Add(PitchingSeasonStats other)
	{
		Games += other.Games;
		GamesStarted += other.GamesStarted;
		Wins += other.Wins;
		Losses += other.Losses;
		OutsRecorded += other.OutsRecorded;
		HitsAllowed += other.HitsAllowed;
		RunsAllowed += other.RunsAllowed;
		EarnedRuns += other.EarnedRuns;
		Walks += other.Walks;
		Strikeouts += other.Strikeouts;
		HomeRunsAllowed += other.HomeRunsAllowed;
		Saves += other.Saves;
	}
}
