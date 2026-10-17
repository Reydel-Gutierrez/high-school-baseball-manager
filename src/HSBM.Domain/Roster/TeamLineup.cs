namespace HSBM.Domain;

public sealed class TeamLineupSlot
{
	public Guid? PlayerId { get; set; }
	public string Position { get; set; } = "";
}

/// <summary>
/// Saved batting order and defensive positions for one team. Empty slots
/// fall back to the automatic depth chart on game day.
/// </summary>
public sealed class TeamLineup
{
	public string TeamId { get; set; } = "";
	public List<TeamLineupSlot> Slots { get; set; } = [];

	public int FilledCount
	{
		get
		{
			int count = 0;
			foreach (TeamLineupSlot slot in Slots)
			{
				if (slot.PlayerId.HasValue)
				{
					count++;
				}
			}

			return count;
		}
	}
}

public sealed class TeamRotation
{
	public string TeamId { get; set; } = "";
	public List<Guid?> Starters { get; set; } = [null, null, null, null, null];
	public Guid? Closer { get; set; }
	public Guid? Setup { get; set; }
	public Guid? Middle { get; set; }
	public Guid? LongRelief { get; set; }

	public IEnumerable<Guid> AssignedPitcherIds
	{
		get
		{
			foreach (Guid? starter in Starters)
			{
				if (starter.HasValue)
				{
					yield return starter.Value;
				}
			}

			if (Closer.HasValue)
			{
				yield return Closer.Value;
			}

			if (Setup.HasValue)
			{
				yield return Setup.Value;
			}

			if (Middle.HasValue)
			{
				yield return Middle.Value;
			}

			if (LongRelief.HasValue)
			{
				yield return LongRelief.Value;
			}
		}
	}
}
