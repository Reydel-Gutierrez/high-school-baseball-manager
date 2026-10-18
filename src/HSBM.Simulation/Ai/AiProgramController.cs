using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// Every non-user program: roster moves, and game-day lineups.
/// User schools are left for the player.
/// </summary>
public static class AiProgramController
{
	private static readonly AutomaticLineupProvider Lineups = new();

	public static List<PlayerTransaction> AdvanceDay(
		CareerState state,
		DateOnly today,
		ISeededRandom rng) =>
		WorldRosterDesk.AdvanceDay(state, today, rng);

	public static TeamGamePlan BuildLineup(IReadOnlyList<Player> roster) =>
		Lineups.Create(roster);
}
