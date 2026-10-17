using HSBM.Domain;

/// <summary>
/// Inbound calls after the carousel. Higher priority than the job market:
/// another school wants this coach, with term and salary on paper.
/// </summary>
public static class JobOfferDesk
{
	public static bool HasPending(GameSession session) =>
		session.HasSignedContract && session.PendingJobOffers.Count > 0;

	public static CoachingJobOffer? Next(GameSession session) =>
		HasPending(session) ? session.PendingJobOffers[0] : null;

	public static bool PendingOn(GameSession session) =>
		HasPending(session) && !session.JobOfferSeen;

	public static bool BannerOn(GameSession session) =>
		HasPending(session) && session.JobOfferSeen;
}
