using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class PlayerPhysicalTests
{
	[Fact]
	public void Height_label_uses_feet_and_inches()
	{
		var physical = new PlayerPhysical(73, 190, 62, BodyBuild.Athletic);
		Assert.Equal("6'1\"", physical.HeightLabel);
	}

	[Fact]
	public void Typical_first_baseman_is_taller_than_a_middle_infielder()
	{
		Assert.True(PlayerPhysical.Typical(PlayerPosition.FirstBase).HeightInches
			> PlayerPhysical.Typical(PlayerPosition.SecondBase).HeightInches);
	}
}

public class PlayerOverallFormulaTests
{
	[Fact]
	public void Overall_is_derived_and_not_an_independent_field()
	{
		var ratings = PlayerRatings.Clamp(70, 40, 60, 80, 85, 82, 30, 30, 30, 30);
		var player = new Player
		{
			PrimaryPosition = PlayerPosition.Shortstop,
			Ratings = ratings,
		};

		Assert.Equal(PlayerOverall.Calculate(PlayerPosition.Shortstop, ratings), player.Overall);
		Assert.NotEqual(70, player.Overall);
	}
}

public class RosterMovementTests
{
	[Fact]
	public void Promote_moves_the_player_onto_the_varsity_team()
	{
		var varsity = new TeamIdentity("cascade-varsity", "cascade", TeamLevel.Varsity, 20);
		var jv = new TeamIdentity("cascade-jv", "cascade", TeamLevel.JuniorVarsity, 20);
		Player player = SquadPlayer("Jordan", "Reed", jv, 64);
		List<Player> roster = [player];

		Assert.True(RosterMovement.TryAssign(player, varsity, roster, out string? error));
		Assert.Null(error);
		Assert.Equal(varsity.Id, player.TeamId);
		Assert.Equal(TeamLevel.Varsity, player.TeamLevel);
		Assert.True(player.JerseyNumber > 0);
	}

	[Fact]
	public void Promote_fails_when_varsity_is_full()
	{
		var varsity = new TeamIdentity("cascade-varsity", "cascade", TeamLevel.Varsity, 2);
		var jv = new TeamIdentity("cascade-jv", "cascade", TeamLevel.JuniorVarsity, 20);
		var roster = new List<Player>
		{
			SquadPlayer("A", "One", varsity, 70),
			SquadPlayer("B", "Two", varsity, 68),
			SquadPlayer("C", "Three", jv, 72),
		};

		Assert.False(RosterMovement.TryAssign(roster[2], varsity, roster, out string? error));
		Assert.Contains("capacity", error, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(jv.Id, roster[2].TeamId);
	}

	[Fact]
	public void Release_sends_the_player_to_the_unsigned_pool()
	{
		var varsity = new TeamIdentity("cascade-varsity", "cascade", TeamLevel.Varsity, 20);
		Player player = SquadPlayer("Nico", "Vale", varsity, 55);
		RosterMovement.ReleaseToPool(player, "great-lakes");
		Assert.True(player.IsUnsigned);
		Assert.Equal(LeagueCatalog.OpenPlayTeamId("great-lakes"), player.TeamId);
		Assert.Equal(0, player.JerseyNumber);
	}

	private static Player SquadPlayer(string first, string last, TeamIdentity team, int overall) =>
		new()
		{
			Id = Guid.NewGuid(),
			FirstName = first,
			LastName = last,
			PrimaryPosition = PlayerPosition.Shortstop,
			OrganizationId = team.OrganizationId,
			TeamId = team.Id,
			TeamLevel = team.Level,
			JerseyNumber = 12,
			Ratings = PlayerRatings.Clamp(overall, overall, overall, overall, overall, overall, 30, 30, 30, 30),
		};
}
