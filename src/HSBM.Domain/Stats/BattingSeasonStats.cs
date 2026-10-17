namespace HSBM.Domain;

public sealed class BattingSeasonStats
{
	public Guid PlayerId { get; set; }
	public int SeasonYear { get; set; }
	public string TeamId { get; set; } = "";
	public int Games { get; set; }
	public int PlateAppearances { get; set; }
	public int AtBats { get; set; }
	public int Runs { get; set; }
	public int Hits { get; set; }
	public int Doubles { get; set; }
	public int Triples { get; set; }
	public int HomeRuns { get; set; }
	public int RunsBattedIn { get; set; }
	public int Walks { get; set; }
	public int Strikeouts { get; set; }
	public int StolenBases { get; set; }

	public int Singles => Math.Max(0, Hits - Doubles - Triples - HomeRuns);

	public double Average => AtBats == 0 ? 0 : (double)Hits / AtBats;

	public double OnBasePercentage
	{
		get
		{
			int denom = AtBats + Walks;
			return denom == 0 ? 0 : (double)(Hits + Walks) / denom;
		}
	}

	public double Slugging
	{
		get
		{
			if (AtBats == 0)
			{
				return 0;
			}

			int totalBases = Singles + (2 * Doubles) + (3 * Triples) + (4 * HomeRuns);
			return (double)totalBases / AtBats;
		}
	}

	public double Ops => OnBasePercentage + Slugging;

	public void Add(BattingSeasonStats other)
	{
		Games += other.Games;
		PlateAppearances += other.PlateAppearances;
		AtBats += other.AtBats;
		Runs += other.Runs;
		Hits += other.Hits;
		Doubles += other.Doubles;
		Triples += other.Triples;
		HomeRuns += other.HomeRuns;
		RunsBattedIn += other.RunsBattedIn;
		Walks += other.Walks;
		Strikeouts += other.Strikeouts;
		StolenBases += other.StolenBases;
	}
}
