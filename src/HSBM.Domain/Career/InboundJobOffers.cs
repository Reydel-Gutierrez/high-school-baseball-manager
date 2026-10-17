namespace HSBM.Domain;

/// <summary>
/// Offseason inbound calls. Posted chairs the coach can reach may put
/// term and salary on paper instead of waiting for an application.
/// </summary>
public static class InboundJobOffers
{
	public const int MaxCount = 2;

	public static List<CoachingJobOffer> Draft(
		IReadOnlyList<OrganizationState> organizations,
		Career career,
		ISeededRandom rng)
	{
		if (!career.HasSignedContract)
		{
			return [];
		}

		var pool = organizations
			.Where(org => org.Profile.HasOpening
				&& !LeagueCatalog.IsUnsignedOrganization(org.Id)
				&& !string.Equals(org.Id, career.UserOrganizationId, StringComparison.Ordinal)
				&& AttractsOffer(career, org.Profile))
			.ToList();
		if (pool.Count == 0)
		{
			return [];
		}

		int want = pool.Count >= 2 && career.Reputation >= 55 && rng.NextDouble() < 0.42
			? 2
			: 1;
		want = Math.Min(want, Math.Min(MaxCount, pool.Count));
		int currentRep = CurrentReputation(organizations, career.UserOrganizationId);
		var picked = new List<CoachingJobOffer>(want);
		while (picked.Count < want && pool.Count > 0)
		{
			int index = WeightedIndex(pool, career, currentRep, rng);
			OrganizationState org = pool[index];
			pool.RemoveAt(index);
			picked.Add(From(org));
		}

		return picked
			.OrderByDescending(offer => offer.Reputation)
			.ToList();
	}

	public static CoachingJobOffer From(OrganizationState org)
	{
		ProgramProfile profile = org.Profile;
		return new CoachingJobOffer
		{
			Id = Guid.NewGuid(),
			OrganizationId = org.Id,
			SchoolName = org.DisplayName,
			SchoolFullName = org.Name,
			Reputation = profile.Reputation,
			Facilities = profile.Facilities,
			TargetExpectation = profile.TargetExpectation,
			TermYears = CoachSalary.TermYears(profile),
			AnnualSalary = CoachSalary.From(profile),
			Status = CoachingJobOfferStatus.Pending,
		};
	}

	private static int CurrentReputation(IReadOnlyList<OrganizationState> organizations, string userOrgId)
	{
		foreach (OrganizationState org in organizations)
		{
			if (string.Equals(org.Id, userOrgId, StringComparison.Ordinal))
			{
				return org.Profile.Reputation;
			}
		}

		return 50;
	}

	public static bool AttractsOffer(Career career, ProgramProfile program)
	{
		if (!CoachReputation.CanReach(career.Reputation, program.Reputation))
		{
			return false;
		}

		if (!ProgramReputation.IsPowerhouse(program.Reputation))
		{
			return true;
		}

		if (career.StateTitles > 0 || career.RegionalTitles > 0 || career.Reputation >= 80)
		{
			return true;
		}

		if (career.CareerGames == 0)
		{
			return true;
		}

		return career.WinningPercentage >= 0.55;
	}

	private static int WeightedIndex(
		IReadOnlyList<OrganizationState> pool,
		Career career,
		int currentRep,
		ISeededRandom rng)
	{
		var weights = new double[pool.Count];
		double total = 0;
		for (int i = 0; i < pool.Count; i++)
		{
			int rep = pool[i].Profile.Reputation;
			double weight = 1.2;
			if (rep > currentRep)
			{
				weight += 1.6;
			}

			if (rep >= currentRep + 8)
			{
				weight += 0.8;
			}

			weight *= PerformanceWeight(career, rep > currentRep);
			weights[i] = weight;
			total += weight;
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

	private static double PerformanceWeight(Career career, bool stepUp)
	{
		double weight = 1.0;
		if (career.CareerGames == 0)
		{
			return weight;
		}

		if (career.WinningPercentage >= 0.6 && stepUp)
		{
			weight += 0.7;
		}

		if (career.StateTitles > 0)
		{
			weight += 1.1;
		}
		else if (career.RegionalTitles > 0)
		{
			weight += 0.55;
		}
		else if (career.DistrictTitles > 0)
		{
			weight += 0.25;
		}

		return weight;
	}
}
