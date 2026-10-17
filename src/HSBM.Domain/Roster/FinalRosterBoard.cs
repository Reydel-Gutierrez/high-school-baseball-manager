namespace HSBM.Domain;

/// <summary>
/// Organization-wide assignment board used during Final Roster Week.
/// Players sit on Varsity, JV, or Unassigned until released into the pool.
/// </summary>
public sealed class FinalRosterBoard
{
	private readonly List<FinalRosterPlayer> _players = [];

	public IReadOnlyList<FinalRosterPlayer> Players => _players;

	public int RegisteredCount => _players.Count;

	public int Count(RosterAssignment assignment)
	{
		int count = 0;
		foreach (FinalRosterPlayer player in _players)
		{
			if (player.Assignment == assignment)
			{
				count++;
			}
		}

		return count;
	}

	public int VarsityCount => Count(RosterAssignment.Varsity);

	public int JuniorVarsityCount => Count(RosterAssignment.JuniorVarsity);

	public int UnassignedCount => Count(RosterAssignment.Unassigned);

	public bool CanAdd(DateOnly today, int seasonYear) =>
		RegisteredCount < ProgramRosterRules.RegisteredCap(today, seasonYear);

	public bool IsOverLimit(DateOnly today, int seasonYear) =>
		RegisteredCount > ProgramRosterRules.RegisteredCap(today, seasonYear);

	public FinalRosterPlayer? Find(Guid id)
	{
		foreach (FinalRosterPlayer player in _players)
		{
			if (player.Id == id)
			{
				return player;
			}
		}

		return null;
	}

	public FinalRosterPlayer? FindByName(string firstName, string lastName)
	{
		foreach (FinalRosterPlayer player in _players)
		{
			if (string.Equals(player.FirstName, firstName, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(player.LastName, lastName, StringComparison.OrdinalIgnoreCase))
			{
				return player;
			}
		}

		return null;
	}

	public void Seed(IEnumerable<FinalRosterPlayer> players)
	{
		_players.Clear();
		foreach (FinalRosterPlayer player in players)
		{
			_players.Add(player);
		}

		NormalizeAssignments();
	}

	public bool TryAdd(FinalRosterPlayer player, DateOnly today, int seasonYear)
	{
		if (Find(player.Id) != null || FindByName(player.FirstName, player.LastName) != null)
		{
			return true;
		}

		if (!CanAdd(today, seasonYear))
		{
			return false;
		}

		if (player.Assignment == RosterAssignment.Varsity && VarsityCount >= ProgramRosterRules.VarsityCap)
		{
			player.Assignment = RosterAssignment.Unassigned;
		}
		else if (player.Assignment == RosterAssignment.JuniorVarsity
			&& JuniorVarsityCount >= ProgramRosterRules.JuniorVarsityCap)
		{
			player.Assignment = RosterAssignment.Unassigned;
		}

		if (player.Assignment == RosterAssignment.Unassigned
			&& UnassignedCount >= ProgramRosterRules.UnassignedCap(today, seasonYear))
		{
			if (VarsityCount < ProgramRosterRules.VarsityCap)
			{
				player.Assignment = RosterAssignment.Varsity;
			}
			else if (JuniorVarsityCount < ProgramRosterRules.JuniorVarsityCap)
			{
				player.Assignment = RosterAssignment.JuniorVarsity;
			}
			else
			{
				return false;
			}
		}

		_players.Add(player);
		return true;
	}

	public bool TryAssign(Guid id, RosterAssignment assignment, DateOnly today, int seasonYear)
	{
		FinalRosterPlayer? player = Find(id);
		if (player == null)
		{
			return false;
		}

		if (player.Assignment == assignment)
		{
			return true;
		}

		if (!HasRoom(assignment, today, seasonYear, except: player))
		{
			return false;
		}

		player.Assignment = assignment;
		return true;
	}

	public bool TryRemove(Guid id, out FinalRosterPlayer? removed)
	{
		for (int i = 0; i < _players.Count; i++)
		{
			if (_players[i].Id == id)
			{
				removed = _players[i];
				_players.RemoveAt(i);
				return true;
			}
		}

		removed = null;
		return false;
	}

	public bool HasRoom(RosterAssignment assignment, DateOnly today, int seasonYear, FinalRosterPlayer? except = null)
	{
		int count = 0;
		foreach (FinalRosterPlayer player in _players)
		{
			if (except != null && player.Id == except.Id)
			{
				continue;
			}

			if (player.Assignment == assignment)
			{
				count++;
			}
		}

		int cap = assignment switch
		{
			RosterAssignment.Varsity => ProgramRosterRules.VarsityCap,
			RosterAssignment.JuniorVarsity => ProgramRosterRules.JuniorVarsityCap,
			_ => ProgramRosterRules.UnassignedCap(today, seasonYear),
		};

		return count < cap;
	}

	private void NormalizeAssignments()
	{
		int varsity = 0;
		int jv = 0;
		foreach (FinalRosterPlayer player in _players)
		{
			if (player.Assignment == RosterAssignment.Varsity)
			{
				varsity++;
				if (varsity > ProgramRosterRules.VarsityCap)
				{
					player.Assignment = RosterAssignment.Unassigned;
					varsity--;
				}
			}
			else if (player.Assignment == RosterAssignment.JuniorVarsity)
			{
				jv++;
				if (jv > ProgramRosterRules.JuniorVarsityCap)
				{
					player.Assignment = RosterAssignment.Unassigned;
					jv--;
				}
			}
		}
	}
}

public sealed class FinalRosterPlayer
{
	public required Guid Id { get; init; }
	public required string FirstName { get; init; }
	public required string LastName { get; init; }
	public required PlayerPosition Position { get; init; }
	public required ThrowingHand Throws { get; init; }
	public BattingSide Bats { get; init; } = BattingSide.Right;
	public required PlayerGrade Year { get; init; }
	public required int Overall { get; init; }
	public required int Potential { get; init; }
	public int Jersey { get; init; }
	public bool FromTryout { get; init; }
	public RosterAssignment Assignment { get; set; }

	public string FullName => $"{FirstName} {LastName}";

	public string PositionLabel => Position.ToDisplayLabel(Throws);
}
