namespace HSBM.Domain;

/// <summary>
/// One completed varsity season for a program. Regular-season place is
/// <see cref="DistrictFinish"/>; <see cref="Wins"/> / <see cref="Losses"/>
/// include postseason games when they were played.
/// </summary>
public sealed class ProgramSeasonResult
{
	public string OrganizationId { get; set; } = "";
	public int SeasonYear { get; set; }
	public int Wins { get; set; }
	public int Losses { get; set; }
	public int RegularWins { get; set; }
	public int RegularLosses { get; set; }
	public int DistrictFinish { get; set; }
	public string Postseason { get; set; } = ProgramPostseason.None;
	public bool WonDistrict { get; set; }
	public bool WonRegional { get; set; }
	public bool WonState { get; set; }

	public string RecordLabel => $"{Wins}\u2013{Losses}";

	public string DistrictFinishLabel => Ordinal.Format(DistrictFinish);
}
