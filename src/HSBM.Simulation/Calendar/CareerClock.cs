using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// One calendar tick for a live career: simulate today's games, then
/// roll the baseball year if the next morning is January 5.
/// </summary>
public sealed class CareerTickResult
{
	public required DateOnly PreviousDate { get; init; }
	public required DateOnly NewDate { get; init; }
	public required WorldDayResult Day { get; init; }
	public IReadOnlyList<NewsStory> StoriesPublished { get; init; } = [];
	public required SeasonPhase Phase { get; init; }
	public bool RolledOver { get; init; }
}

public static class CareerClock
{
	public static CareerTickResult Tick(
		CareerState state,
		BaseballGameSimulator simulator,
		LeagueCatalog? catalog = null)
	{
		LeagueCatalog league = catalog ?? LeagueCatalog.Standard;
		DateOnly previous = state.World.CurrentDate != default
			? state.World.CurrentDate
			: state.Career.CurrentDate;
		int year = state.Career.CurrentSeasonYear;
		WorldDayResult day = WorldDayProcessor.Run(state, previous, simulator);

		DateOnly next = previous.AddDays(1);
		bool rolled = CareerCalendar.IsRolloverDay(year, next);
		if (rolled)
		{
			ISeededRandom rng = new SeededRandom(
				SeededRandom.Mix(state.Career.RandomSeed, next.DayNumber ^ unchecked((int)0x5EED)));
			SeasonRollover.Apply(state, league, rng);
		}

		state.World.CurrentDate = next;
		state.Career.CurrentDate = next;
		state.SyncClockFromCareer();
		PostseasonDesk.Tick(state, next);
		IReadOnlyList<NewsStory> wire = NewsWire.Publish(state);
		return new CareerTickResult
		{
			PreviousDate = previous,
			NewDate = state.World.CurrentDate,
			Day = day,
			StoriesPublished = wire,
			Phase = CareerCalendar.PhaseOn(state.Career.CurrentSeasonYear, state.World.CurrentDate),
			RolledOver = rolled,
		};
	}
}
