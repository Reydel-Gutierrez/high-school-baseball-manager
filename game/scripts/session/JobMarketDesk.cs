using System;
using HSBM.Domain;

/// <summary>
/// Offseason coaching carousel. Posted after board review; the desk
/// stays optional — the coach can keep the current seat.
/// </summary>
public static class JobMarketDesk
{
	public static bool PendingOn(GameSession session) =>
		session.HasSignedContract
		&& session.SeasonReviewAcknowledged
		&& !session.JobMarketAcknowledged
		&& SeasonCalendar.CoachingCarouselOn(GameSession.SeasonYear, session.CurrentDate)
		&& session.AvailableJobs.Count > 0;
}
