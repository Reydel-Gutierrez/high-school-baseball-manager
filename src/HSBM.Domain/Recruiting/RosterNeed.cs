namespace HSBM.Domain;

/// <summary>
/// How crowded a returning roster is at a position, and whether an incoming
/// player would see varsity this spring.
/// </summary>
public static class RosterNeed
{
	public static int ReturningCount(IReadOnlyList<Player> roster, PlayerPosition position)
	{
		int count = 0;
		foreach (Player player in roster)
		{
			if (player.IsUnsigned)
			{
				continue;
			}

			if (SameRole(player.PrimaryPosition, position))
			{
				count++;
			}
		}

		return count;
	}

	public static int BetterOrEqualCount(
		IReadOnlyList<Player> roster,
		PlayerPosition position,
		int incomingOverall)
	{
		int count = 0;
		foreach (Player player in roster)
		{
			if (player.IsUnsigned || !SameRole(player.PrimaryPosition, position))
			{
				continue;
			}

			if (player.Overall >= incomingOverall - 2)
			{
				count++;
			}
		}

		return count;
	}

	public static double PlayingTimeScore(
		IReadOnlyList<Player> roster,
		PlayerPosition position,
		int incomingOverall)
	{
		int depth = ReturningCount(roster, position);
		int ahead = BetterOrEqualCount(roster, position, incomingOverall);
		double depthScore = depth switch
		{
			0 => 1.0,
			1 => 0.78,
			2 => 0.52,
			3 => 0.32,
			_ => 0.16,
		};
		double aheadScore = ahead switch
		{
			0 => 1.0,
			1 => 0.62,
			2 => 0.38,
			_ => 0.18,
		};
		return Math.Clamp((depthScore * 0.45) + (aheadScore * 0.55), 0.05, 1.0);
	}

	public static double VarsityOpportunityScore(
		IReadOnlyList<Player> roster,
		PlayerPosition position,
		int incomingOverall)
	{
		int varsityAhead = 0;
		int varsityAtPos = 0;
		foreach (Player player in roster)
		{
			if (player.IsUnsigned || player.TeamLevel != TeamLevel.Varsity)
			{
				continue;
			}

			if (!SameRole(player.PrimaryPosition, position))
			{
				continue;
			}

			varsityAtPos++;
			if (player.Overall >= incomingOverall - 1)
			{
				varsityAhead++;
			}
		}

		if (varsityAhead == 0)
		{
			return 0.96;
		}

		if (varsityAhead == 1 && incomingOverall >= 62)
		{
			return 0.72;
		}

		if (varsityAtPos <= 2 && incomingOverall >= 58)
		{
			return 0.48;
		}

		return incomingOverall >= 68 ? 0.34 : 0.18;
	}

	public static int IncomingTarget(int returningCount)
	{
		int holes = LeagueCatalog.OrganizationRosterCap - returningCount;
		if (holes <= 0)
		{
			return 0;
		}

		return Math.Clamp(holes, LeagueCatalog.TypicalIncomingMin, LeagueCatalog.TypicalIncomingMax);
	}

	public static bool SameRole(PlayerPosition left, PlayerPosition right)
	{
		if (left == right)
		{
			return true;
		}

		if (left.IsPitcher() && right.IsPitcher())
		{
			return true;
		}

		if (left.IsOutfielder() && right.IsOutfielder())
		{
			return true;
		}

		if ((left == PlayerPosition.Shortstop && right == PlayerPosition.SecondBase)
			|| (left == PlayerPosition.SecondBase && right == PlayerPosition.Shortstop))
		{
			return true;
		}

		return false;
	}

	public static TeamLevel PlaceIncoming(IReadOnlyList<Player> roster, Player player)
	{
		double varsity = VarsityOpportunityScore(roster, player.PrimaryPosition, player.Overall);
		if (player.Grade == PlayerGrade.Senior || varsity >= 0.7 || player.Overall >= 68)
		{
			return TeamLevel.Varsity;
		}

		return TeamLevel.JuniorVarsity;
	}

	public static TeamLevel? FitIncoming(
		IReadOnlyList<Player> roster,
		Player player,
		int teamCap = LeagueCatalog.TeamRosterCap)
	{
		int varsity = 0;
		int jv = 0;
		foreach (Player member in roster)
		{
			if (member.IsUnsigned || member.Id == player.Id)
			{
				continue;
			}

			if (member.TeamLevel == TeamLevel.Varsity)
			{
				varsity++;
			}
			else
			{
				jv++;
			}
		}

		TeamLevel preferred = PlaceIncoming(roster, player);
		if (preferred == TeamLevel.Varsity && varsity < teamCap)
		{
			return TeamLevel.Varsity;
		}

		if (jv < teamCap)
		{
			return TeamLevel.JuniorVarsity;
		}

		if (varsity < teamCap)
		{
			return TeamLevel.Varsity;
		}

		return null;
	}
}
