namespace HSBM.Domain;

public sealed class FieldingSeasonStats
{
	public Guid PlayerId { get; set; }
	public int SeasonYear { get; set; }
	public string TeamId { get; set; } = "";
	public int Games { get; set; }
	public int Putouts { get; set; }
	public int Assists { get; set; }
	public int Errors { get; set; }

	public int Chances => Putouts + Assists + Errors;

	public double FieldingPercentage => Chances == 0 ? 0 : (double)(Putouts + Assists) / Chances;

	public void Add(FieldingSeasonStats other)
	{
		Games += other.Games;
		Putouts += other.Putouts;
		Assists += other.Assists;
		Errors += other.Errors;
	}
}
