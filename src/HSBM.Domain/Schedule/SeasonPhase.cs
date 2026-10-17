namespace HSBM.Domain;

public enum SeasonPhase
{
	Offseason = 0,
	OpenPlay = 1,
	SigningPeriod = 2,
	Preseason = 3,
	RegularSeason = 4,
	Postseason = 5,
	SeasonRollover = 6,
}

public static class SeasonPhaseExtensions
{
	public static string ToLabel(this SeasonPhase phase) => phase switch
	{
		SeasonPhase.OpenPlay => "Open Play",
		SeasonPhase.SigningPeriod => "Signing Period",
		SeasonPhase.Preseason => "Preseason",
		SeasonPhase.RegularSeason => "Regular Season",
		SeasonPhase.Postseason => "Postseason",
		SeasonPhase.SeasonRollover => "New Baseball Year",
		_ => "Offseason",
	};
}
