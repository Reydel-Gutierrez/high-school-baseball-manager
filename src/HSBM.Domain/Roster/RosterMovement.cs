namespace HSBM.Domain;

/// <summary>
/// Moves a player onto a varsity or JV team inside one organization.
/// Caps come from the destination team, not from the assignment board.
/// </summary>
public static class RosterMovement
{
	public static bool TryAssign(
		Player player,
		TeamIdentity destination,
		IReadOnlyList<Player> roster,
		out string? error)
	{
		if (LeagueCatalog.IsUnsignedOrganization(destination.OrganizationId))
		{
			error = "Cannot assign a player to a holding list.";
			return false;
		}

		if (!player.IsUnsigned && player.OrganizationId != destination.OrganizationId)
		{
			error = "Player belongs to a different program.";
			return false;
		}

		if (player.TeamId == destination.Id && player.TeamLevel == destination.Level)
		{
			error = null;
			return true;
		}

		int cap = destination.RosterCap > 0 ? destination.RosterCap : LeagueCatalog.TeamRosterCap;
		int count = 0;
		foreach (Player other in roster)
		{
			if (other.Id == player.Id || other.IsUnsigned)
			{
				continue;
			}

			if (other.TeamId == destination.Id)
			{
				count++;
			}
		}

		if (count >= cap)
		{
			error = $"{destination.Level} is at capacity.";
			return false;
		}

		ForceAssign(player, destination, roster);
		error = null;
		return true;
	}

	public static void ForceAssign(Player player, TeamIdentity destination, IReadOnlyList<Player> roster)
	{
		player.OrganizationId = destination.OrganizationId;
		player.TeamId = destination.Id;
		player.TeamLevel = destination.Level;
		if (player.JerseyNumber <= 0 || JerseyTaken(roster, destination.Id, player))
		{
			player.JerseyNumber = NextJersey(roster, destination.Id, player.Id);
		}
	}

	/// <summary>Take a player off the team. He stays in school; he just isn't on a roster.</summary>
	public static void Release(Player player)
	{
		player.OrganizationId = LeagueCatalog.UnsignedOrganizationId;
		player.TeamId = LeagueCatalog.InactiveTeamId;
		player.TeamLevel = TeamLevel.JuniorVarsity;
		player.JerseyNumber = 0;
	}

	private static bool JerseyTaken(IReadOnlyList<Player> roster, string teamId, Player player)
	{
		foreach (Player other in roster)
		{
			if (other.Id != player.Id && other.TeamId == teamId && other.JerseyNumber == player.JerseyNumber)
			{
				return true;
			}
		}

		return false;
	}

	private static int NextJersey(IReadOnlyList<Player> roster, string teamId, Guid selfId)
	{
		var used = new HashSet<int>();
		foreach (Player other in roster)
		{
			if (other.Id != selfId && other.TeamId == teamId)
			{
				used.Add(other.JerseyNumber);
			}
		}

		for (int n = 1; n <= 99; n++)
		{
			if (used.Add(n))
			{
				return n;
			}
		}

		return 99;
	}
}
