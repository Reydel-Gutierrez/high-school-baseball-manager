using HSBM.Domain;

namespace HSBM.Simulation;

public static class OpenPlayGenerator
{
	private static readonly PlayerPosition[] SquadSlots =
	[
		PlayerPosition.Catcher,
		PlayerPosition.FirstBase,
		PlayerPosition.SecondBase,
		PlayerPosition.ThirdBase,
		PlayerPosition.Shortstop,
		PlayerPosition.LeftField,
		PlayerPosition.CenterField,
		PlayerPosition.RightField,
		PlayerPosition.StartingPitcher,
		PlayerPosition.ReliefPitcher,
	];

	public static List<Player> GenerateDistrict(
		string districtId,
		int seasonYear,
		ISeededRandom rng,
		HashSet<string>? usedNames = null)
	{
		usedNames ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var unsignedTeam = new TeamIdentity(
			LeagueCatalog.OpenPlayTeamId(districtId),
			LeagueCatalog.UnsignedOrganizationId,
			TeamLevel.JuniorVarsity,
			0);
		int target = LeagueCatalog.OpenPlayTargetPlayers;
		var players = new List<Player>(target);
		int slot = 0;
		while (players.Count < target)
		{
			players.Add(RosterGenerator.CreateIncoming(
				unsignedTeam,
				SquadSlots[slot % SquadSlots.Length],
				seasonYear,
				rng,
				usedNames));
			slot++;
		}

		return players;
	}
}
