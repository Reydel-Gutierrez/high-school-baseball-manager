namespace HSBM.Domain;

/// <summary>
/// Canonical varsity ranking: wins, then losses, then run differential.
/// Top two in each eight-school district qualify for the regional.
/// </summary>
public sealed record StandingEntry(
	string TeamId,
	string OrganizationId,
	string Name,
	string DistrictId,
	int Wins,
	int Losses,
	int DistrictWins,
	int DistrictLosses,
	int RunsFor,
	int RunsAgainst,
	int Rank = 0,
	bool Qualifies = false)
{
	public int Games => Wins + Losses;

	public int RunDifferential => RunsFor - RunsAgainst;

	public double WinningPercentage => Games == 0 ? 0 : (double)Wins / Games;
}

public static class LeagueStandings
{
	public const int QualifiersPerDistrict = 2;

	public static int Compare(StandingEntry a, StandingEntry b)
	{
		int wins = b.Wins.CompareTo(a.Wins);
		if (wins != 0)
		{
			return wins;
		}

		int losses = a.Losses.CompareTo(b.Losses);
		if (losses != 0)
		{
			return losses;
		}

		int diff = b.RunDifferential.CompareTo(a.RunDifferential);
		return diff != 0 ? diff : string.CompareOrdinal(a.Name, b.Name);
	}

	public static List<StandingEntry> Rank(IEnumerable<StandingEntry> entries, bool markQualifiers = false)
	{
		var ranked = entries.ToList();
		ranked.Sort(Compare);
		for (int i = 0; i < ranked.Count; i++)
		{
			ranked[i] = ranked[i] with
			{
				Rank = i + 1,
				Qualifies = markQualifiers && i < QualifiersPerDistrict,
			};
		}

		return ranked;
	}

	public static List<StandingEntry> RankDistricts(IEnumerable<StandingEntry> entries)
	{
		var result = new List<StandingEntry>();
		foreach (IGrouping<string, StandingEntry> district in entries.GroupBy(row => row.DistrictId))
		{
			result.AddRange(Rank(district, markQualifiers: true));
		}

		result.Sort((a, b) =>
		{
			int district = string.CompareOrdinal(a.DistrictId, b.DistrictId);
			return district != 0 ? district : a.Rank.CompareTo(b.Rank);
		});
		return result;
	}

	public static List<StandingEntry> FromWorld(GameWorld world, int seasonYear, TeamLevel level = TeamLevel.Varsity)
	{
		var records = new Dictionary<string, TeamSeasonRecord>(StringComparer.Ordinal);
		foreach (TeamSeasonRecord record in world.TeamRecords)
		{
			if (record.SeasonYear == seasonYear)
			{
				records[record.TeamId] = record;
			}
		}

		var orgs = new Dictionary<string, OrganizationState>(StringComparer.Ordinal);
		foreach (OrganizationState org in world.Organizations)
		{
			orgs[org.Id] = org;
		}

		var entries = new List<StandingEntry>();
		foreach (TeamIdentity team in world.Teams)
		{
			if (team.Level != level || team.RosterCap <= 0)
			{
				continue;
			}

			if (!orgs.TryGetValue(team.OrganizationId, out OrganizationState? org)
				|| LeagueCatalog.IsUnsignedOrganization(org.Id))
			{
				continue;
			}

			records.TryGetValue(team.Id, out TeamSeasonRecord? record);
			entries.Add(new StandingEntry(
				team.Id,
				org.Id,
				org.ShortName,
				org.DistrictId,
				record?.Wins ?? 0,
				record?.Losses ?? 0,
				record?.DistrictWins ?? 0,
				record?.DistrictLosses ?? 0,
				record?.RunsFor ?? 0,
				record?.RunsAgainst ?? 0));
		}

		return RankDistricts(entries);
	}

	public static Dictionary<string, int> PlacesByOrganization(IReadOnlyList<StandingEntry> ranked)
	{
		var places = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (StandingEntry row in ranked)
		{
			places[row.OrganizationId] = row.Rank;
		}

		return places;
	}

	public static StandingEntry? FindTeam(IReadOnlyList<StandingEntry> ranked, string teamId)
	{
		foreach (StandingEntry row in ranked)
		{
			if (row.TeamId == teamId)
			{
				return row;
			}
		}

		return null;
	}

	public static StandingEntry? FindOrganization(IReadOnlyList<StandingEntry> ranked, string organizationId)
	{
		foreach (StandingEntry row in ranked)
		{
			if (row.OrganizationId == organizationId)
			{
				return row;
			}
		}

		return null;
	}
}
