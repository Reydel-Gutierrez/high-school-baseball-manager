using HSBM.Domain;
using HSBM.Simulation;

namespace HSBM.Simulation.Tests;

public class PersonAppearanceGeneratorTests
{
	[Fact]
	public void Same_seed_produces_identical_appearance()
	{
		PersonAppearance a = PersonAppearanceGenerator.Generate(12345);
		PersonAppearance b = PersonAppearanceGenerator.Generate(12345);
		Assert.Equal(a, b);
		Assert.Equal(12345, a.Seed);
	}

	[Fact]
	public void Different_seeds_normally_differ()
	{
		PersonAppearance a = PersonAppearanceGenerator.Generate(12345);
		PersonAppearance b = PersonAppearanceGenerator.Generate(67890);
		Assert.NotEqual(a, b);
	}

	[Fact]
	public void Coach_generation_is_deterministic_and_neutral_outfit()
	{
		PersonAppearance a = PersonAppearanceGenerator.GenerateCoach(847291);
		PersonAppearance b = PersonAppearanceGenerator.GenerateCoach(847291);
		Assert.Equal(a, b);
		Assert.Equal(OutfitColorway.Neutral, a.OutfitColorway);
		Assert.Equal(0, a.TeamPrimaryArgb);
	}

	[Fact]
	public void GenerateFrom_uses_the_seeded_rng_not_shared_random()
	{
		var rngA = new SeededRandom(42);
		var rngB = new SeededRandom(42);
		PersonAppearance a = PersonAppearanceGenerator.GenerateFrom(rngA);
		PersonAppearance b = PersonAppearanceGenerator.GenerateFrom(rngB);
		Assert.Equal(a, b);
	}
}
