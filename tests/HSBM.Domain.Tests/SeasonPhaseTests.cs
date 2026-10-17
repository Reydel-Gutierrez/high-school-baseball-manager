using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class SeasonPhaseTests
{
	[Fact]
	public void Baseball_year_maps_onto_the_career_phases()
	{
		Assert.Equal(SeasonPhase.SeasonRollover, CareerCalendar.PhaseOn(2026, new DateOnly(2026, 1, 5)));
		Assert.Equal(SeasonPhase.OpenPlay, CareerCalendar.PhaseOn(2026, new DateOnly(2026, 1, 12)));
		Assert.Equal(SeasonPhase.OpenPlay, CareerCalendar.PhaseOn(2026, new DateOnly(2026, 1, 25)));
		Assert.Equal(SeasonPhase.SigningPeriod, CareerCalendar.PhaseOn(2026, new DateOnly(2026, 1, 26)));
		Assert.Equal(SeasonPhase.SigningPeriod, CareerCalendar.PhaseOn(2026, new DateOnly(2026, 2, 1)));
		Assert.Equal(SeasonPhase.Preseason, CareerCalendar.PhaseOn(2026, new DateOnly(2026, 2, 2)));
		Assert.Equal(SeasonPhase.Preseason, CareerCalendar.PhaseOn(2026, new DateOnly(2026, 2, 12)));
		Assert.Equal(SeasonPhase.RegularSeason, CareerCalendar.PhaseOn(2026, new DateOnly(2026, 2, 16)));
		Assert.Equal(SeasonPhase.RegularSeason, CareerCalendar.PhaseOn(2026, new DateOnly(2026, 5, 3)));
		Assert.Equal(SeasonPhase.Postseason, CareerCalendar.PhaseOn(2026, new DateOnly(2026, 5, 7)));
		Assert.Equal(SeasonPhase.Postseason, CareerCalendar.PhaseOn(2026, new DateOnly(2026, 5, 22)));
		Assert.Equal(SeasonPhase.Offseason, CareerCalendar.PhaseOn(2026, new DateOnly(2026, 6, 15)));
		Assert.Equal(SeasonPhase.Offseason, CareerCalendar.PhaseOn(2026, new DateOnly(2026, 12, 1)));
		Assert.Equal(SeasonPhase.SeasonRollover, CareerCalendar.PhaseOn(2026, new DateOnly(2027, 1, 5)));
	}

	[Fact]
	public void Important_dates_include_featured_milestones()
	{
		IReadOnlyList<SeasonEvent> dates = CareerCalendar.ImportantDates(2026);
		Assert.Contains(dates, item => item.Id == "opening-day");
		Assert.Contains(dates, item => item.Id == "signing-deadline");
		Assert.Contains(dates, item => item.Id == "open-play");
		Assert.True(CareerCalendar.IsSimStop(2026, new DateOnly(2026, 2, 16)));
		Assert.False(CareerCalendar.IsSimStop(2026, new DateOnly(2026, 1, 20)));
	}

	[Fact]
	public void Next_important_date_skips_the_past()
	{
		SeasonEvent? next = CareerCalendar.NextImportant(2026, new DateOnly(2026, 2, 10));
		Assert.NotNull(next);
		Assert.Equal("opening-day", next!.Id);
	}
}
