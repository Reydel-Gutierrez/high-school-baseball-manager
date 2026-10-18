namespace HSBM.Domain;

public enum SeasonPhase
{
	Offseason = 0,
	/// <summary>Jan 12–25: sign-ups and open gym before tryouts.</summary>
	WinterWorkouts = 1,

	/// <summary>Tryout Week through Decision Day.</summary>
	Tryouts = 2,
	Preseason = 3,
	RegularSeason = 4,
	Postseason = 5,
	SeasonRollover = 6,
}

public static class SeasonPhaseExtensions
{
	public static string ToLabel(this SeasonPhase phase) => phase switch
	{
		SeasonPhase.WinterWorkouts => "Winter Workouts",
		SeasonPhase.Tryouts => "Tryouts",
		SeasonPhase.Preseason => "Preseason",
		SeasonPhase.RegularSeason => "Regular Season",
		SeasonPhase.Postseason => "Postseason",
		SeasonPhase.SeasonRollover => "New Baseball Year",
		_ => "Offseason",
	};
}
