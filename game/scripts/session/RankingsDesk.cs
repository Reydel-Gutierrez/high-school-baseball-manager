using System;
using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// Statewide power poll for District Hub. Uses hub records and school
/// reputation until CareerService standings and schedules feed the same
/// formula.
/// </summary>
public static class RankingsDesk
{
	public static IReadOnlyList<PowerRankingRow> For(GameSession session)
	{
		TeamLevel level = session.ActiveTeam.Level;
		var entries = new List<PowerRankingEntry>(DistrictHubData.Teams.Count);
		foreach (HubTeam team in DistrictHubData.Teams)
		{
			HubDistrict district = DistrictHubData.GetDistrict(team.DistrictId);
			SchoolSnapshot school = SchoolProfiles.For(team.Id, level, session);
			entries.Add(new PowerRankingEntry(
				team.Id,
				team.Name,
				district.Id,
				district.Name,
				team.Wins(level),
				team.Losses(level),
				team.RunsFor,
				team.RunsAgainst,
				school.Reputation,
				ParseStreak(team.Streak(level)),
				OpponentsFor(team)));
		}

		return PowerRankings.Rank(entries);
	}

	private static List<string> OpponentsFor(HubTeam team)
	{
		var ids = new List<string>();
		var outsiders = new List<HubTeam>();
		foreach (HubTeam other in DistrictHubData.Teams)
		{
			if (other.Id == team.Id)
			{
				continue;
			}

			if (other.DistrictId == team.DistrictId)
			{
				ids.Add(other.Id);
				ids.Add(other.Id);
				continue;
			}

			outsiders.Add(other);
		}

		outsiders.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
		int seed = 17;
		foreach (char c in team.Id)
		{
			seed = (seed * 31) + c;
		}

		seed = Math.Abs(seed);
		var used = new HashSet<string>(StringComparer.Ordinal);
		int added = 0;
		for (int i = 0; added < PowerRankings.NonDistrictGames && outsiders.Count > 0 && i < outsiders.Count * 4; i++)
		{
			HubTeam pick = outsiders[Math.Abs(seed + (i * 7)) % outsiders.Count];
			if (!used.Add(pick.Id))
			{
				continue;
			}

			ids.Add(pick.Id);
			added++;
		}

		return ids;
	}

	private static int ParseStreak(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
		{
			return 0;
		}

		char kind = char.ToUpperInvariant(value[0]);
		if (kind is not ('W' or 'L'))
		{
			return 0;
		}

		if (!int.TryParse(value.AsSpan(1), out int length) || length <= 0)
		{
			return 0;
		}

		return kind == 'W' ? length : -length;
	}
}
