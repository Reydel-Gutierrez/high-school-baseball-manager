using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class FinalRosterBoardTests
{
	private static readonly DateOnly BeforeLock = new(2026, 2, 3);
	private static readonly DateOnly AfterLock = new(2026, 2, 8);

	[Fact]
	public void Caps_match_the_program_shape()
	{
		Assert.Equal(40, ProgramRosterRules.OrganizationCap);
		Assert.Equal(42, ProgramRosterRules.TemporaryCap);
		Assert.Equal(20, ProgramRosterRules.VarsityCap);
		Assert.Equal(2, ProgramRosterRules.UnassignedSlots);
		Assert.Equal(4, ProgramRosterRules.UnassignedCap(BeforeLock, 2026));
		Assert.Equal(2, ProgramRosterRules.UnassignedCap(AfterLock, 2026));
		Assert.False(ProgramRosterRules.ExtrasAllowed(AfterLock, 2026));
	}

	[Fact]
	public void Varsity_stops_at_twenty()
	{
		FinalRosterBoard board = Seeded(20, 18, 2);
		FinalRosterPlayer extra = Player("Extra", RosterAssignment.Unassigned);
		Assert.True(board.TryAdd(extra, BeforeLock, 2026));
		Assert.False(board.TryAssign(extra.Id, RosterAssignment.Varsity, BeforeLock, 2026));
		Assert.True(board.TryAssign(extra.Id, RosterAssignment.JuniorVarsity, BeforeLock, 2026));
		Assert.Equal(19, board.JuniorVarsityCount);
	}

	[Fact]
	public void Temporary_extras_are_unassigned_before_the_cut()
	{
		FinalRosterBoard board = Seeded(20, 18, 2);
		Assert.True(board.TryAdd(Player("PlusOne", RosterAssignment.Unassigned), BeforeLock, 2026));
		Assert.True(board.TryAdd(Player("PlusTwo", RosterAssignment.Unassigned), BeforeLock, 2026));
		Assert.False(board.TryAdd(Player("Overflow", RosterAssignment.Unassigned), BeforeLock, 2026));
		Assert.Equal(42, board.RegisteredCount);
		Assert.Equal(4, board.UnassignedCount);
		Assert.False(board.IsOverLimit(BeforeLock, 2026));
		Assert.True(board.IsOverLimit(AfterLock, 2026));
	}

	[Fact]
	public void After_the_cut_the_program_locks_at_forty()
	{
		FinalRosterBoard board = Seeded(20, 18, 2);
		Assert.False(board.IsOverLimit(AfterLock, 2026));
		Assert.False(board.TryAdd(Player("Late", RosterAssignment.Unassigned), AfterLock, 2026));
		Assert.Equal(40, board.RegisteredCount);
		Assert.Equal(2, board.UnassignedCount);
	}

	[Fact]
	public void Release_opens_a_roster_spot()
	{
		FinalRosterBoard board = Seeded(20, 18, 2);
		Guid id = board.Players[0].Id;
		Assert.True(board.TryRemove(id, out FinalRosterPlayer? removed));
		Assert.NotNull(removed);
		Assert.Equal(39, board.RegisteredCount);
		Assert.True(board.TryAdd(Player("Callup", RosterAssignment.Unassigned), AfterLock, 2026));
		Assert.Equal(40, board.RegisteredCount);
	}

	[Fact]
	public void Pool_take_moves_a_player_out()
	{
		var pool = new OpenPlayerPool();
		OpenPoolPlayer player = OpenPoolPlayer.FromRoster(Player("Webb", RosterAssignment.JuniorVarsity));
		Assert.True(pool.TryAdd(player));
		Assert.False(pool.TryAdd(player));
		Assert.Equal(player.Id, pool.Take(player.Id)?.Id);
		Assert.Null(pool.Find(player.Id));
	}

	private static FinalRosterBoard Seeded(int varsity, int jv, int unassigned)
	{
		var board = new FinalRosterBoard();
		var players = new List<FinalRosterPlayer>();
		for (int i = 0; i < varsity; i++)
		{
			players.Add(Player($"V{i:00}", RosterAssignment.Varsity));
		}

		for (int i = 0; i < jv; i++)
		{
			players.Add(Player($"J{i:00}", RosterAssignment.JuniorVarsity));
		}

		for (int i = 0; i < unassigned; i++)
		{
			players.Add(Player($"U{i:00}", RosterAssignment.Unassigned));
		}

		board.Seed(players);
		return board;
	}

	private static FinalRosterPlayer Player(string lastName, RosterAssignment assignment) =>
		new()
		{
			Id = Guid.NewGuid(),
			FirstName = "Test",
			LastName = lastName,
			Position = PlayerPosition.Shortstop,
			Throws = ThrowingHand.Right,
			Year = PlayerGrade.Sophomore,
			Overall = 60,
			Potential = 70,
			Assignment = assignment,
		};
}
