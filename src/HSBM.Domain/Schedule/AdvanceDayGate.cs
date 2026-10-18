using System.Globalization;

namespace HSBM.Domain;

public enum AdvanceDayInterruptKind
{
	PlayoffGame = 0,
	JobOffer = 1,
	TryoutDecision = 3,
	RosterDeadline = 4,
}

/// <summary>
/// A hard stop the calendar cannot skip. Simulate Week must land on
/// this day and wait — roster cuts, tryout decisions, playoff games,
/// and job offers.
/// </summary>
public sealed record AdvanceDayInterrupt(
	AdvanceDayInterruptKind Kind,
	DateOnly Deadline,
	string Body,
	string ActionLabel);

public sealed record AdvanceDayGateState(
	DateOnly Today,
	int SeasonYear,
	bool TryoutDecisionsPending,
	bool RosterOverLimit,
	bool PlayoffGameToday,
	bool JobOfferPending);

public static class AdvanceDayGate
{
	public static AdvanceDayInterrupt? Evaluate(AdvanceDayGateState state)
	{
		if (state.PlayoffGameToday)
		{
			return new AdvanceDayInterrupt(
				AdvanceDayInterruptKind.PlayoffGame,
				state.Today,
				"Today's playoff game cannot be skipped.",
				"GO TO GAME");
		}

		if (state.JobOfferPending)
		{
			return new AdvanceDayInterrupt(
				AdvanceDayInterruptKind.JobOffer,
				state.Today,
				"A job offer must be accepted or declined\nbefore you can continue.",
				"READ JOB OFFER");
		}

		DateOnly tryouts = SeasonCalendar.TryoutDecisionDay(state.SeasonYear);
		if (state.Today == tryouts && state.TryoutDecisionsPending)
		{
			return new AdvanceDayInterrupt(
				AdvanceDayInterruptKind.TryoutDecision,
				tryouts,
				"Tryout decisions are due today.\nPost your list (or let your assistant decide).",
				"GO TO TRYOUTS");
		}

		DateOnly roster = SeasonCalendar.InitialRosterCut(state.SeasonYear);
		if (state.RosterOverLimit && state.Today >= roster)
		{
			return new AdvanceDayInterrupt(
				AdvanceDayInterruptKind.RosterDeadline,
				roster,
				MustComplete("Roster cuts", roster),
				"GO TO ROSTER");
		}

		return null;
	}

	private static string MustComplete(string action, DateOnly deadline) =>
		$"{action} must be completed\nbefore {deadline.ToString("MMMM d", CultureInfo.InvariantCulture)}.";
}
