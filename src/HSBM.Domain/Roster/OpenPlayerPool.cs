namespace HSBM.Domain;

public enum OpenPoolSource
{
	MissedDeadline = 0,
	TryoutCut = 1,
	Release = 2,
}

/// <summary>
/// District Open Pool: unsigned or released players any program can draft
/// during the season, subject to roster caps.
/// </summary>
public sealed class OpenPlayerPool
{
	private readonly List<OpenPoolPlayer> _players = [];

	public IReadOnlyList<OpenPoolPlayer> Players => _players;

	public int Count => _players.Count;

	public OpenPoolPlayer? Find(Guid id)
	{
		foreach (OpenPoolPlayer player in _players)
		{
			if (player.Id == id)
			{
				return player;
			}
		}

		return null;
	}

	public bool Contains(Guid id) => Find(id) != null;

	public bool TryAdd(OpenPoolPlayer player)
	{
		if (Find(player.Id) != null)
		{
			return false;
		}

		_players.Add(player);
		return true;
	}

	public OpenPoolPlayer? Take(Guid id)
	{
		for (int i = 0; i < _players.Count; i++)
		{
			if (_players[i].Id == id)
			{
				OpenPoolPlayer player = _players[i];
				_players.RemoveAt(i);
				return player;
			}
		}

		return null;
	}

	public bool TryRemove(Guid id) => Take(id) != null;

	public void Seed(IEnumerable<OpenPoolPlayer> players)
	{
		_players.Clear();
		foreach (OpenPoolPlayer player in players)
		{
			TryAdd(player);
		}
	}
}

public sealed class OpenPoolPlayer
{
	public required Guid Id { get; init; }
	public required string FirstName { get; init; }
	public required string LastName { get; init; }
	public required PlayerPosition Position { get; init; }
	public required ThrowingHand Throws { get; init; }
	public BattingSide Bats { get; init; } = BattingSide.Right;
	public required PlayerGrade Year { get; init; }
	public required int TrueOverall { get; init; }
	public required int TruePotential { get; init; }
	public required OpenPoolSource Source { get; init; }

	public string FullName => $"{FirstName} {LastName}";

	public string PositionLabel => Position.ToDisplayLabel(Throws);

	public static OpenPoolPlayer FromIncoming(Player player) =>
		new()
		{
			Id = player.Id,
			FirstName = player.FirstName,
			LastName = player.LastName,
			Position = player.PrimaryPosition,
			Throws = player.Throws,
			Bats = player.Bats,
			Year = player.Grade,
			TrueOverall = player.Overall,
			TruePotential = player.Potential,
			Source = OpenPoolSource.MissedDeadline,
		};

	public static OpenPoolPlayer FromProspect(TryoutProspect prospect, OpenPoolSource source) =>
		new()
		{
			Id = prospect.Id,
			FirstName = prospect.FirstName,
			LastName = prospect.LastName,
			Position = prospect.Position,
			Throws = prospect.Throws,
			Bats = prospect.Bats,
			Year = prospect.Year,
			TrueOverall = prospect.TrueOverall,
			TruePotential = prospect.TruePotential,
			Source = source,
		};

	public static OpenPoolPlayer FromRoster(FinalRosterPlayer player) =>
		new()
		{
			Id = player.Id,
			FirstName = player.FirstName,
			LastName = player.LastName,
			Position = player.Position,
			Throws = player.Throws,
			Bats = player.Bats,
			Year = player.Year,
			TrueOverall = player.Overall,
			TruePotential = player.Potential,
			Source = OpenPoolSource.Release,
		};

	public FinalRosterPlayer ToUnassigned() =>
		new()
		{
			Id = Id,
			FirstName = FirstName,
			LastName = LastName,
			Position = Position,
			Throws = Throws,
			Bats = Bats,
			Year = Year,
			Overall = TrueOverall,
			Potential = TruePotential,
			FromTryout = Source != OpenPoolSource.Release,
			Assignment = RosterAssignment.Unassigned,
		};
}
