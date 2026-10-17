using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// First-license market: two or three posted vacancies a newly credentialed
/// coach can actually get in front of. Powerhouses keep their sitting staff.
/// </summary>
public static class OpeningGenerator
{
	public const int MinCount = 2;
	public const int MaxCount = 3;

	public static void Assign(IReadOnlyList<OrganizationState> organizations, ISeededRandom rng)
	{
		int count = rng.Next(MinCount, MaxCount + 1);
		var pool = organizations
			.Where(org => !LeagueCatalog.IsUnsignedOrganization(org.Id)
				&& !ProgramReputation.IsPowerhouse(org.Profile.Reputation)
				&& CoachReputation.CanReach(CoachReputation.Starting, org.Profile.Reputation))
			.ToList();
		if (pool.Count < count)
		{
			pool = organizations
				.Where(org => !LeagueCatalog.IsUnsignedOrganization(org.Id)
					&& !ProgramReputation.IsPowerhouse(org.Profile.Reputation))
				.ToList();
		}

		var picked = new List<OrganizationState>(count);
		while (picked.Count < count && pool.Count > 0)
		{
			int index = WeightedIndex(pool, picked, rng);
			OrganizationState org = pool[index];
			pool.RemoveAt(index);
			org.Profile.HasOpening = true;
			org.Profile.VacancyReason = PickReason(rng);
			picked.Add(org);
		}
	}

	private static int WeightedIndex(
		IReadOnlyList<OrganizationState> pool,
		IReadOnlyList<OrganizationState> alreadyPicked,
		ISeededRandom rng)
	{
		var weights = new double[pool.Count];
		double total = 0;
		for (int i = 0; i < pool.Count; i++)
		{
			double weight = Weight(pool[i].Profile.Reputation);
			if (alreadyPicked.Any(picked => picked.DistrictId == pool[i].DistrictId))
			{
				weight *= 0.35;
			}

			weights[i] = weight;
			total += weight;
		}

		if (total <= 0)
		{
			return rng.Next(0, pool.Count);
		}

		double roll = rng.NextDouble() * total;
		double cursor = 0;
		for (int i = 0; i < weights.Length; i++)
		{
			cursor += weights[i];
			if (roll <= cursor)
			{
				return i;
			}
		}

		return pool.Count - 1;
	}

	private static double Weight(int reputation) => ProgramReputation.TalentBand(reputation) switch
	{
		ProgramTalentTier.Rebuilding => 10,
		ProgramTalentTier.BelowAverage => 6,
		ProgramTalentTier.Average => 3,
		ProgramTalentTier.AboveAverage => 1,
		_ => 0,
	};

	private static VacancyReason PickReason(ISeededRandom rng)
	{
		VacancyReason[] reasons = Enum.GetValues<VacancyReason>();
		return reasons[rng.Next(0, reasons.Length)];
	}
}
