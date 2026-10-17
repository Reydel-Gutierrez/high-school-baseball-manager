namespace HSBM.Domain;

/// <summary>
/// One stop on the coach's résumé. A null end year means the seat is current.
/// </summary>
public sealed class CoachStint
{
	public string OrganizationId { get; set; } = "";
	public string SchoolName { get; set; } = "";
	public int StartYear { get; set; }
	public int? EndYear { get; set; }
	public int Wins { get; set; }
	public int Losses { get; set; }
	public int DistrictTitles { get; set; }
	public int RegionalTitles { get; set; }
	public int StateTitles { get; set; }

	public string YearsLabel => EndYear is null or 0
		? $"{StartYear}\u2013"
		: $"{StartYear}\u2013{EndYear}";

	public string RecordLabel => $"{Wins}\u2013{Losses}";

	public int Games => Wins + Losses;

	public bool IsCurrent => EndYear is null or 0;

	public int SeasonsThrough(int currentYear)
	{
		int last = EndYear is null or 0 ? currentYear : EndYear.Value;
		int span = last - StartYear + 1;
		return span < 1 ? 1 : span;
	}

	public void AddSeason(ProgramSeasonResult season)
	{
		Wins += Math.Max(0, season.Wins);
		Losses += Math.Max(0, season.Losses);
		if (season.WonDistrict)
		{
			DistrictTitles++;
		}

		if (season.WonRegional)
		{
			RegionalTitles++;
		}

		if (season.WonState)
		{
			StateTitles++;
		}
	}

	public CoachStint Clone() => new()
	{
		OrganizationId = OrganizationId,
		SchoolName = SchoolName,
		StartYear = StartYear,
		EndYear = EndYear,
		Wins = Wins,
		Losses = Losses,
		DistrictTitles = DistrictTitles,
		RegionalTitles = RegionalTitles,
		StateTitles = StateTitles,
	};
}
