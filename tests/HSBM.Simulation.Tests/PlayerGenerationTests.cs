using HSBM.Domain;
using HSBM.Simulation;

namespace HSBM.Simulation.Tests;

public class PlayerGenerationTests
{
	[Fact]
	public void Generated_players_carry_identity_ratings_potential_and_appearance()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 42, 2026);
		List<Player> rostered = world.Players.Where(p => !p.IsUnsigned).ToList();
		Assert.NotEmpty(rostered);
		Assert.All(rostered, player =>
		{
			Assert.False(string.IsNullOrWhiteSpace(player.FirstName));
			Assert.False(string.IsNullOrWhiteSpace(player.LastName));
			Assert.InRange(player.Age, 14, 19);
			Assert.InRange((int)player.Grade, (int)PlayerGrade.Freshman, (int)PlayerGrade.Senior);
			Assert.InRange(player.Ratings.Contact, PlayerRatings.Min, PlayerRatings.Max);
			Assert.InRange(player.Ratings.Speed, PlayerRatings.Min, PlayerRatings.Max);
			Assert.InRange(player.Ratings.Fielding, PlayerRatings.Min, PlayerRatings.Max);
			Assert.Equal(PlayerOverall.Calculate(player.PrimaryPosition, player.Ratings), player.Overall);
			Assert.True(player.Potential >= player.Overall);
			Assert.InRange(player.Physical.HeightInches, PlayerPhysical.MinHeightInches, PlayerPhysical.MaxHeightInches);
			Assert.InRange(player.Physical.WeightLbs, PlayerPhysical.MinWeightLbs, PlayerPhysical.MaxWeightLbs);
			Assert.InRange(player.Physical.Strength, PlayerRatings.Min, PlayerRatings.Max);
			Assert.NotEqual(0, player.Appearance.Seed);
			Assert.Equal(player.Physical.Build, player.Appearance.BodyBuild);
			Assert.Equal(OutfitStyle.BaseballJersey, player.Appearance.OutfitStyle);
			Assert.NotEqual(HairStyle.Bald, player.Appearance.HairStyle);
			Assert.NotEqual(HairStyle.Balding, player.Appearance.HairStyle);
			Assert.False(string.IsNullOrWhiteSpace(player.Personality.Label()));
			Assert.False(string.IsNullOrWhiteSpace(player.Priorities.Hook));
		});
	}

	[Fact]
	public void Open_play_players_are_unsigned_with_recruiting_priorities()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 42, 2026);
		List<Player> unsigned = world.Players.Where(p => p.IsUnsigned).ToList();
		Assert.True(unsigned.Count >= LeagueCatalog.DistrictCount * LeagueCatalog.OpenPlayTargetPlayers);
		Assert.All(unsigned, player =>
		{
			Assert.Equal(0, player.JerseyNumber);
			Assert.True(player.Potential >= player.Overall);
			Assert.NotEqual(0, player.Appearance.Seed);
			Assert.InRange(player.Physical.HeightInches, PlayerPhysical.MinHeightInches, PlayerPhysical.MaxHeightInches);
		});
	}

	[Fact]
	public void Same_seed_replays_appearance_and_personality()
	{
		GeneratedWorld a = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 4242, 2026);
		GeneratedWorld b = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 4242, 2026);
		Player firstA = a.Players.OrderBy(p => p.Id).First();
		Player firstB = b.Players.OrderBy(p => p.Id).First();
		Assert.Equal(firstA.Appearance, firstB.Appearance);
		Assert.Equal(firstA.Personality, firstB.Personality);
		Assert.Equal(firstA.Physical, firstB.Physical);
		Assert.Equal(firstA.Potential, firstB.Potential);
	}
}

public class DepthChartTests
{
	[Fact]
	public void Depth_chart_names_a_starter_at_every_field_spot_and_pitching_roles()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 77, 2026);
		TeamIdentity team = world.Teams.First(t => t.Level == TeamLevel.Varsity && t.RosterCap > 0);
		List<Player> roster = world.Players.Where(p => p.TeamId == team.Id).ToList();
		DepthChart depth = DepthChartBuilder.Build(roster);

		Assert.Equal(8, depth.Field.Count);
		Assert.All(depth.Field, slot => Assert.NotNull(slot.Starter));
		Assert.NotNull(depth.Ace);
		Assert.Contains(depth.Pitching, arm => arm.Role == PitchingRole.Ace);
		Assert.Contains(depth.Pitching, arm => arm.Role is PitchingRole.Reliever or PitchingRole.Closer);
		var starterIds = depth.Field.Select(slot => slot.Starter!.Id).ToHashSet();
		Assert.Equal(8, starterIds.Count);
	}

	[Fact]
	public void Shortstop_starter_is_the_best_listed_glove_not_a_random_bat()
	{
		var ss = PlayerAt("Avery", "Glove", PlayerPosition.Shortstop, fielding: 90, contact: 50);
		var bat = PlayerAt("Big", "Bat", PlayerPosition.FirstBase, fielding: 40, contact: 90);
		var depth = PlayerAt("Backup", "Middle", PlayerPosition.Shortstop, fielding: 70, contact: 55);
		DepthChart chart = DepthChartBuilder.Build([ss, bat, depth]);
		Assert.Equal(ss.Id, chart.StarterAt(PlayerPosition.Shortstop)?.Id);
		Assert.Contains(chart.Field.Single(slot => slot.Position == PlayerPosition.Shortstop).Players, p => p.Id == depth.Id);
	}

	private static Player PlayerAt(string first, string last, PlayerPosition position, int fielding, int contact) =>
		new()
		{
			Id = Guid.NewGuid(),
			FirstName = first,
			LastName = last,
			PrimaryPosition = position,
			Ratings = PlayerRatings.Clamp(contact, 50, 50, 60, fielding, 70, 30, 30, 30, 30),
		};
}
