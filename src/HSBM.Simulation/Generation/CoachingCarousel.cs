using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// Offseason market: two or three chairs open after boards fire, retire,
/// or reassign sitting staff. The user's current school stays off the list.
/// </summary>
public static class CoachingCarousel
{
	public const int MinCount = 2;
	public const int MaxCount = 3;

	public static void Assign(
		IReadOnlyList<OrganizationState> organizations,
		string? userOrganizationId,
		ISeededRandom rng)
	{
		foreach (OrganizationState org in organizations)
		{
			org.Profile.HasOpening = false;
		}

		int count = rng.Next(MinCount, MaxCount + 1);
		var pool = organizations
			.Where(org => !LeagueCatalog.IsUnsignedOrganization(org.Id)
				&& !IsUserSchool(org.Id, userOrganizationId))
			.ToList();
		if (pool.Count == 0)
		{
			return;
		}

		var picked = new List<OrganizationState>(count);
		while (picked.Count < count && pool.Count > 0)
		{
			int index = WeightedIndex(pool, picked, rng);
			OrganizationState org = pool[index];
			pool.RemoveAt(index);
			org.Profile.HasOpening = true;
			org.Profile.VacancyReason = ReasonFor(org, rng);
			picked.Add(org);
		}
	}

	private static bool IsUserSchool(string organizationId, string? userOrganizationId) =>
		!string.IsNullOrWhiteSpace(userOrganizationId)
		&& string.Equals(organizationId, userOrganizationId, StringComparison.Ordinal);

	private static int WeightedIndex(
		IReadOnlyList<OrganizationState> pool,
		IReadOnlyList<OrganizationState> alreadyPicked,
		ISeededRandom rng)
	{
		var weights = new double[pool.Count];
		double total = 0;
		for (int i = 0; i < pool.Count; i++)
		{
			double weight = Weight(pool[i]);
			if (alreadyPicked.Any(picked => picked.DistrictId == pool[i].DistrictId))
			{
				weight *= 0.4;
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

	private static double Weight(OrganizationState org)
	{
		ProgramProfile profile = org.Profile;
		bool losing = profile.LastSeasonWins < profile.LastSeasonLosses;
		bool missed = !SeasonReview.TargetMet(
			profile.TargetExpectation,
			profile.LastSeasonWins,
			profile.LastSeasonLosses,
			profile.DistrictFinish,
			profile.DistrictFinish == 1,
			false,
			false);

		double weight = ProgramReputation.TalentBand(org.Profile.Reputation) switch
		{
			ProgramTalentTier.Rebuilding => 8,
			ProgramTalentTier.BelowAverage => 6,
			ProgramTalentTier.Average => 3.5,
			ProgramTalentTier.AboveAverage => 2,
			ProgramTalentTier.Powerhouse => 0.8,
			_ => 2,
		};

		if (losing)
		{
			weight *= 2.1;
		}

		if (missed)
		{
			weight *= 2.4;
		}

		if (profile.DistrictFinish >= 7)
		{
			weight *= 1.35;
		}

		return weight;
	}

	private static VacancyReason ReasonFor(OrganizationState org, ISeededRandom rng)
	{
		ProgramProfile profile = org.Profile;
		bool missed = !SeasonReview.TargetMet(
			profile.TargetExpectation,
			profile.LastSeasonWins,
			profile.LastSeasonLosses,
			profile.DistrictFinish,
			profile.DistrictFinish == 1,
			false,
			false);
		if (missed && rng.NextDouble() < 0.72)
		{
			return VacancyReason.Dismissal;
		}

		if (profile.LastSeasonWins < profile.LastSeasonLosses && rng.NextDouble() < 0.45)
		{
			return VacancyReason.NonRenewal;
		}

		VacancyReason[] rest =
		[
			VacancyReason.Retirement,
			VacancyReason.Resignation,
			VacancyReason.Reassignment,
		];
		return rest[rng.Next(0, rest.Length)];
	}
}
