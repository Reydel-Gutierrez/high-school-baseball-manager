using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class AdvanceDayGateTests
{
	[Fact]
	public void Simulate_week_can_run_before_tryout_decisions_are_due()
	{
		AdvanceDayInterrupt? stop = AdvanceDayGate.Evaluate(Clear(new DateOnly(2026, 1, 20)) with
		{
			TryoutDecisionsPending = true,
		});
		Assert.Null(stop);
	}

	[Fact]
	public void Tryout_decisions_stop_simulation_on_the_deadline()
	{
		DateOnly deadline = SeasonCalendar.TryoutDecisionDay(2026);
		AdvanceDayInterrupt? stop = AdvanceDayGate.Evaluate(Clear(deadline) with { TryoutDecisionsPending = true });
		Assert.NotNull(stop);
		Assert.Equal(AdvanceDayInterruptKind.TryoutDecision, stop.Kind);
		Assert.Equal("GO TO TRYOUTS", stop.ActionLabel);
		Assert.Contains("Tryout decisions are due today", stop.Body);
		Assert.Null(AdvanceDayGate.Evaluate(Clear(deadline)));
	}

	[Fact]
	public void The_day_after_tryouts_does_not_keep_blocking()
	{
		AdvanceDayInterrupt? stop = AdvanceDayGate.Evaluate(
			Clear(SeasonCalendar.TryoutDecisionDay(2026).AddDays(1)) with
			{
				TryoutDecisionsPending = true,
			});
		Assert.Null(stop);
	}

	[Fact]
	public void Roster_cut_stops_when_the_program_is_over_the_limit()
	{
		AdvanceDayInterrupt? stop = AdvanceDayGate.Evaluate(Clear(SeasonCalendar.InitialRosterCut(2026)) with
		{
			RosterOverLimit = true,
		});
		Assert.NotNull(stop);
		Assert.Equal(AdvanceDayInterruptKind.RosterDeadline, stop.Kind);
		Assert.Equal("GO TO ROSTER", stop.ActionLabel);
		Assert.Contains("before February 8", stop.Body);
	}

	[Fact]
	public void Under_limit_roster_can_pass_the_cut()
	{
		AdvanceDayInterrupt? stop = AdvanceDayGate.Evaluate(Clear(SeasonCalendar.InitialRosterCut(2026)));
		Assert.Null(stop);
	}

	[Fact]
	public void Playoff_game_day_cannot_be_skipped()
	{
		AdvanceDayInterrupt? stop = AdvanceDayGate.Evaluate(Clear(new DateOnly(2026, 5, 7)) with
		{
			PlayoffGameToday = true,
		});
		Assert.NotNull(stop);
		Assert.Equal(AdvanceDayInterruptKind.PlayoffGame, stop.Kind);
		Assert.Equal("GO TO GAME", stop.ActionLabel);
	}

	[Fact]
	public void Job_offer_stops_before_the_calendar_moves()
	{
		AdvanceDayInterrupt? stop = AdvanceDayGate.Evaluate(Clear(new DateOnly(2026, 6, 10)) with
		{
			JobOfferPending = true,
		});
		Assert.NotNull(stop);
		Assert.Equal(AdvanceDayInterruptKind.JobOffer, stop.Kind);
		Assert.Equal("READ JOB OFFER", stop.ActionLabel);
	}

	[Fact]
	public void Playoff_game_outranks_a_job_offer_on_the_same_day()
	{
		AdvanceDayInterrupt? stop = AdvanceDayGate.Evaluate(Clear(new DateOnly(2026, 5, 12)) with
		{
			PlayoffGameToday = true,
			JobOfferPending = true,
		});
		Assert.Equal(AdvanceDayInterruptKind.PlayoffGame, stop!.Kind);
	}

	[Fact]
	public void Playoff_windows_cover_regionals_and_state()
	{
		Assert.True(SeasonCalendar.IsPlayoffWindow(2026, new DateOnly(2026, 5, 7)));
		Assert.True(SeasonCalendar.IsPlayoffWindow(2026, new DateOnly(2026, 5, 12)));
		Assert.True(SeasonCalendar.IsPlayoffWindow(2026, new DateOnly(2026, 5, 18)));
		Assert.False(SeasonCalendar.IsPlayoffWindow(2026, new DateOnly(2026, 5, 3)));
	}

	private static AdvanceDayGateState Clear(DateOnly today) =>
		new(
			today,
			2026,
			TryoutDecisionsPending: false,
			RosterOverLimit: false,
			PlayoffGameToday: false,
			JobOfferPending: false);
}
