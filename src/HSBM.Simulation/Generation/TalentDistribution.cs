using HSBM.Domain;

namespace HSBM.Simulation;

public static class TalentDistribution
{
	/// <summary>
	/// Bell-curve-ish roll via Irwin-Hall (sum of uniforms), then a rare elite tail.
	/// </summary>
	public static int RollRating(ISeededRandom rng, double mean, double spread)
	{
		double unit = (rng.NextDouble() + rng.NextDouble() + rng.NextDouble() + rng.NextDouble()) / 4.0;
		double value = mean + (unit - 0.5) * 2.0 * spread;

		double tail = rng.NextDouble();
		if (tail > 0.992)
		{
			value += 12 + rng.NextDouble() * 10;
		}
		else if (tail > 0.97)
		{
			value += 6 + rng.NextDouble() * 6;
		}
		else if (tail < 0.04)
		{
			value -= 6 + rng.NextDouble() * 8;
		}

		return PlayerRatings.ClampValue((int)Math.Round(value, MidpointRounding.AwayFromZero));
	}

	public static ProgramTalentTier[] AssignTiers(int organizationCount, ISeededRandom rng)
	{
		var tiers = new List<ProgramTalentTier>(organizationCount);
		int powerhouses = Math.Max(2, organizationCount / 8);
		int rebuilding = powerhouses;
		int above = organizationCount / 4;
		int below = above;
		int average = organizationCount - powerhouses - rebuilding - above - below;
		if (average < 0)
		{
			average = 0;
			below = Math.Max(0, organizationCount - powerhouses - rebuilding - above);
		}

		Add(tiers, ProgramTalentTier.Powerhouse, powerhouses);
		Add(tiers, ProgramTalentTier.AboveAverage, above);
		Add(tiers, ProgramTalentTier.Average, average);
		Add(tiers, ProgramTalentTier.BelowAverage, below);
		Add(tiers, ProgramTalentTier.Rebuilding, rebuilding);
		while (tiers.Count < organizationCount)
		{
			tiers.Add(ProgramTalentTier.Average);
		}

		rng.Shuffle(tiers);
		return [.. tiers];
	}

	private static void Add(List<ProgramTalentTier> tiers, ProgramTalentTier tier, int count)
	{
		for (int i = 0; i < count; i++)
		{
			tiers.Add(tier);
		}
	}
}
