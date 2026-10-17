namespace HSBM.Domain;

/// <summary>
/// One completed season on the coach's résumé: school, record, and
/// how deep the postseason run went.
/// </summary>
public sealed class CoachSeason
{
	public int SeasonYear { get; set; }
	public string OrganizationId { get; set; } = "";
	public string SchoolName { get; set; } = "";
	public int Wins { get; set; }
	public int Losses { get; set; }
	public int DistrictFinish { get; set; }
	public string Postseason { get; set; } = ProgramPostseason.None;
	public bool WonDistrict { get; set; }
	public bool WonRegional { get; set; }
	public bool WonState { get; set; }

	public string RecordLabel => $"{Wins}\u2013{Losses}";

	public string Line =>
		$"{SeasonYear}  {SchoolName}  {RecordLabel}  {PostseasonLabel}";

	public string PostseasonLabel =>
		string.IsNullOrWhiteSpace(Postseason) || Postseason == ProgramPostseason.None
			? SeasonReview.PostseasonLabel(WonDistrict, WonRegional, WonState, DistrictFinish)
			: Postseason;

	public CoachSeason Clone() => new()
	{
		SeasonYear = SeasonYear,
		OrganizationId = OrganizationId,
		SchoolName = SchoolName,
		Wins = Wins,
		Losses = Losses,
		DistrictFinish = DistrictFinish,
		Postseason = Postseason,
		WonDistrict = WonDistrict,
		WonRegional = WonRegional,
		WonState = WonState,
	};

	public static CoachSeason From(ProgramSeasonResult season, string schoolName) => new()
	{
		SeasonYear = season.SeasonYear,
		OrganizationId = season.OrganizationId,
		SchoolName = schoolName,
		Wins = season.Wins,
		Losses = season.Losses,
		DistrictFinish = season.DistrictFinish,
		Postseason = string.IsNullOrWhiteSpace(season.Postseason)
			? ProgramPostseason.HistoryLabel(
				season.WonDistrict,
				season.WonRegional,
				season.WonState,
				season.DistrictFinish)
			: season.Postseason,
		WonDistrict = season.WonDistrict,
		WonRegional = season.WonRegional,
		WonState = season.WonState,
	};
}
