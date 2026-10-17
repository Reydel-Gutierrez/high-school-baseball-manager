namespace HSBM.Domain;

public enum PitchingRole
{
	Ace = 0,
	Starter = 1,
	Reliever = 2,
	Closer = 3,
}

public sealed record DepthChartSlot(PlayerPosition Position, IReadOnlyList<Player> Players)
{
	public Player? Starter => Players.Count > 0 ? Players[0] : null;
}

public sealed record PitchingAssignment(Player Player, PitchingRole Role, int Order);

/// <summary>
/// Starters, bench, and pitching roles for one team roster. Built from
/// current ratings and listed positions, not from saved lineup cards.
/// </summary>
public sealed class DepthChart
{
	public IReadOnlyList<DepthChartSlot> Field { get; init; } = [];
	public IReadOnlyList<PitchingAssignment> Pitching { get; init; } = [];
	public IReadOnlyList<Player> Bench { get; init; } = [];

	public Player? StarterAt(PlayerPosition position)
	{
		foreach (DepthChartSlot slot in Field)
		{
			if (slot.Position == position)
			{
				return slot.Starter;
			}
		}

		return null;
	}

	public Player? Ace
	{
		get
		{
			foreach (PitchingAssignment arm in Pitching)
			{
				if (arm.Role == PitchingRole.Ace)
				{
					return arm.Player;
				}
			}

			foreach (PitchingAssignment arm in Pitching)
			{
				if (arm.Role == PitchingRole.Starter)
				{
					return arm.Player;
				}
			}

			return null;
		}
	}

	public IEnumerable<Player> Rotation
	{
		get
		{
			foreach (PitchingAssignment arm in Pitching)
			{
				if (arm.Role is PitchingRole.Ace or PitchingRole.Starter)
				{
					yield return arm.Player;
				}
			}
		}
	}
}
