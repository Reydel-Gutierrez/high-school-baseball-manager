using System.Globalization;

namespace HSBM.Domain;

public enum AdvanceDayInterruptKind
{
	PlayoffGame = 0,
	JobOffer = 1,
	SigningDeadline = 2,
	TryoutDecision = 3,
	RosterDeadline = 4,
}

/// <summary>
/// A hard stop the calendar cannot skip. Simulate Week must land on
/// this day and wait — roster cuts, tryout decisions, playoff games,
/// job offers, and the signing deadline.
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
	bool SigningIncomplete,
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

		DateOnly signing = SeasonCalendar.SigningDeadline(state.SeasonYear);
		if (state.Today == signing)
		{
			return new AdvanceDayInterrupt(
				AdvanceDayInterruptKind.SigningDeadline,
				signing,
				MustComplete("Signing", signing),
				"GO TO SIGNING");
		}

		DateOnly tryouts = SeasonCalendar.TryoutDecisionDeadline(state.SeasonYear);
		if (state.Today == tryouts)
		{
			return new AdvanceDayInterrupt(
				AdvanceDayInterruptKind.TryoutDecision,
				tryouts,
				MustComplete("Tryout decisions", tryouts),
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
