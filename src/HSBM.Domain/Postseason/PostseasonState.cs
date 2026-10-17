namespace HSBM.Domain;

public enum PostseasonRound
{
	RegionalSemifinal = 0,
	RegionalFinal = 1,
	StateChampionship = 2,
}

/// <summary>
/// One best-of-three series in the high-school postseason.
/// Regional semis pair district 1-seeds against the other district's 2-seed.
/// </summary>
public sealed class PostseasonSeries
{
	public const int WinsNeeded = 2;

	public Guid Id { get; set; }
	public PostseasonRound Round { get; set; }
	public string Region { get; set; } = "";
	public string HomeTeamId { get; set; } = "";
	public string AwayTeamId { get; set; } = "";
	public int HomeWins { get; set; }
	public int AwayWins { get; set; }
	public string? WinnerTeamId { get; set; }
	public List<Guid> GameIds { get; set; } = [];

	public int GamesPlayed => HomeWins + AwayWins;

	public bool IsComplete =>
		!string.IsNullOrEmpty(WinnerTeamId)
		|| HomeWins >= WinsNeeded
		|| AwayWins >= WinsNeeded;

	public bool Involves(string teamId) =>
		HomeTeamId == teamId || AwayTeamId == teamId;

	public string OpponentOf(string teamId) =>
		teamId == HomeTeamId ? AwayTeamId : HomeTeamId;
}

public sealed class PostseasonSeed
{
	public string TeamId { get; set; } = "";
	public string OrganizationId { get; set; } = "";
	public string Name { get; set; } = "";
	public string DistrictId { get; set; } = "";
	public string Region { get; set; } = "";
	public int Seed { get; set; }
	public int DistrictFinish { get; set; }
}

/// <summary>
/// Live bracket for one baseball year: eight qualifiers, two regionals,
/// one state final.
/// </summary>
public sealed class PostseasonState
{
	public int SeasonYear { get; set; }
	public List<PostseasonSeed> Qualifiers { get; set; } = [];
	public List<PostseasonSeries> Series { get; set; } = [];
	public string? EastChampionTeamId { get; set; }
	public string? WestChampionTeamId { get; set; }
	public string? StateChampionTeamId { get; set; }
	public bool HistoryWritten { get; set; }

	public bool Qualified(string teamId)
	{
		foreach (PostseasonSeed seed in Qualifiers)
		{
			if (seed.TeamId == teamId)
			{
				return true;
			}
		}

		return false;
	}

	public PostseasonSeed? SeedFor(string teamId)
	{
		foreach (PostseasonSeed seed in Qualifiers)
		{
			if (seed.TeamId == teamId)
			{
				return seed;
			}
		}

		return null;
	}

	public bool WonDistrict(string teamId)
	{
		PostseasonSeed? seed = SeedFor(teamId);
		return seed != null && seed.DistrictFinish == 1;
	}

	public bool WonRegionalSemi(string teamId)
	{
		foreach (PostseasonSeries series in Series)
		{
			if (series.Round == PostseasonRound.RegionalSemifinal
				&& series.WinnerTeamId == teamId)
			{
				return true;
			}
		}

		return false;
	}

	public bool WonRegional(string teamId) =>
		teamId == EastChampionTeamId || teamId == WestChampionTeamId;

	public bool WonState(string teamId) => teamId == StateChampionTeamId;

	public PostseasonSeries? SeriesById(Guid id)
	{
		foreach (PostseasonSeries series in Series)
		{
			if (series.Id == id)
			{
				return series;
			}
		}

		return null;
	}

	public PostseasonSeries? SeriesForGame(Guid gameId)
	{
		foreach (PostseasonSeries series in Series)
		{
			foreach (Guid id in series.GameIds)
			{
				if (id == gameId)
				{
					return series;
				}
			}
		}

		return null;
	}

	public IReadOnlyList<PostseasonSeries> SeriesIn(PostseasonRound round, string? region = null)
	{
		var list = new List<PostseasonSeries>();
		foreach (PostseasonSeries series in Series)
		{
			if (series.Round != round)
			{
				continue;
			}

			if (region != null && !string.Equals(series.Region, region, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			list.Add(series);
		}

		return list;
	}
}

/// <summary>
/// Seeds the eight-team regional field from locked district standings.
/// </summary>
public static class PostseasonField
{
	public static PostseasonState Seed(
		int seasonYear,
		IReadOnlyList<DistrictIdentity> districts,
		IReadOnlyList<StandingEntry> standings,
		ISeededRandom? rng = null)
	{
		var bracket = new PostseasonState { SeasonYear = seasonYear };
		var byDistrict = new Dictionary<string, List<StandingEntry>>(StringComparer.Ordinal);
		foreach (StandingEntry row in standings)
		{
			if (!byDistrict.TryGetValue(row.DistrictId, out List<StandingEntry>? list))
			{
				list = [];
				byDistrict[row.DistrictId] = list;
			}

			list.Add(row);
		}

		foreach (List<StandingEntry> list in byDistrict.Values)
		{
			list.Sort((a, b) => a.Rank.CompareTo(b.Rank));
		}

		var regionDistricts = new Dictionary<string, List<DistrictIdentity>>(StringComparer.OrdinalIgnoreCase);
		foreach (DistrictIdentity district in districts)
		{
			if (!regionDistricts.TryGetValue(district.Region, out List<DistrictIdentity>? list))
			{
				list = [];
				regionDistricts[district.Region] = list;
			}

			list.Add(district);
		}

		foreach (KeyValuePair<string, List<DistrictIdentity>> pair in regionDistricts)
		{
			if (pair.Value.Count < 2)
			{
				continue;
			}

			DistrictIdentity left = pair.Value[0];
			DistrictIdentity right = pair.Value[1];
			PostseasonSeed? left1 = Qualifier(byDistrict, left, 1, pair.Key);
			PostseasonSeed? left2 = Qualifier(byDistrict, left, 2, pair.Key);
			PostseasonSeed? right1 = Qualifier(byDistrict, right, 1, pair.Key);
			PostseasonSeed? right2 = Qualifier(byDistrict, right, 2, pair.Key);
			AddSeed(bracket, left1);
			AddSeed(bracket, left2);
			AddSeed(bracket, right1);
			AddSeed(bracket, right2);
			if (left1 != null && right2 != null)
			{
				bracket.Series.Add(CreateSeries(
					PostseasonRound.RegionalSemifinal,
					pair.Key,
					left1.TeamId,
					right2.TeamId,
					rng));
			}

			if (right1 != null && left2 != null)
			{
				bracket.Series.Add(CreateSeries(
					PostseasonRound.RegionalSemifinal,
					pair.Key,
					right1.TeamId,
					left2.TeamId,
					rng));
			}
		}

		return bracket;
	}

	public static PostseasonSeries CreateSeries(
		PostseasonRound round,
		string region,
		string homeTeamId,
		string awayTeamId,
		ISeededRandom? rng = null) =>
		new()
		{
			Id = rng?.NextGuid() ?? Guid.NewGuid(),
			Round = round,
			Region = region,
			HomeTeamId = homeTeamId,
			AwayTeamId = awayTeamId,
		};

	public static string ChampionRegion(string region) =>
		string.Equals(region, "West", StringComparison.OrdinalIgnoreCase) ? "West" : "East";

	private static void AddSeed(PostseasonState bracket, PostseasonSeed? seed)
	{
		if (seed != null)
		{
			bracket.Qualifiers.Add(seed);
		}
	}

	private static PostseasonSeed? Qualifier(
		Dictionary<string, List<StandingEntry>> byDistrict,
		DistrictIdentity district,
		int seed,
		string region)
	{
		if (!byDistrict.TryGetValue(district.Id, out List<StandingEntry>? ranked) || ranked.Count < seed)
		{
			return null;
		}

		StandingEntry row = ranked[seed - 1];
		return new PostseasonSeed
		{
			TeamId = row.TeamId,
			OrganizationId = row.OrganizationId,
			Name = row.Name,
			DistrictId = district.Id,
			Region = region,
			Seed = seed,
			DistrictFinish = row.Rank,
		};
	}
}
