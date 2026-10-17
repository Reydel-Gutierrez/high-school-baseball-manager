namespace HSBM.Domain;

public enum ProgramRecordCategory
{
	Hits = 0,
	HomeRuns = 1,
	RunsBattedIn = 2,
	Doubles = 3,
	Wins = 4,
	Strikeouts = 5,
}

public sealed class ProgramRecordHolder
{
	public string OrganizationId { get; set; } = "";
	public ProgramRecordCategory Category { get; set; }
	public int Rank { get; set; }
	public string PlayerName { get; set; } = "";
	public Guid? PlayerId { get; set; }
	public int Value { get; set; }
}

public static class ProgramRecordCategoryExtensions
{
	public static string ToLabel(this ProgramRecordCategory category) => category switch
	{
		ProgramRecordCategory.Hits => "Hits",
		ProgramRecordCategory.HomeRuns => "Home Runs",
		ProgramRecordCategory.RunsBattedIn => "RBI",
		ProgramRecordCategory.Doubles => "Doubles",
		ProgramRecordCategory.Wins => "Wins",
		ProgramRecordCategory.Strikeouts => "Strikeouts",
		_ => category.ToString(),
	};

	public static bool IsPitching(this ProgramRecordCategory category) =>
		category is ProgramRecordCategory.Wins or ProgramRecordCategory.Strikeouts;
}

/// <summary>
/// Career program records: generated alumni plus anyone who has posted
/// varsity stats for the school.
/// </summary>
public static class ProgramRecordBook
{
	public const int Slots = 5;

	public static readonly ProgramRecordCategory[] Categories =
	[
		ProgramRecordCategory.Hits,
		ProgramRecordCategory.HomeRuns,
		ProgramRecordCategory.RunsBattedIn,
		ProgramRecordCategory.Doubles,
		ProgramRecordCategory.Wins,
		ProgramRecordCategory.Strikeouts,
	];

	public static List<ProgramRecordHolder> Compile(
		string organizationId,
		IReadOnlyList<ProgramRecordHolder> existing,
		IReadOnlyDictionary<Guid, string> playerNames,
		IReadOnlyList<BattingSeasonStats> batting,
		IReadOnlyList<PitchingSeasonStats> pitching,
		IReadOnlySet<string> varsityTeamIds)
	{
		var alumni = new List<ProgramRecordHolder>();
		foreach (ProgramRecordHolder holder in existing)
		{
			if (holder.OrganizationId != organizationId)
			{
				continue;
			}

			if (holder.PlayerId is Guid liveId && playerNames.ContainsKey(liveId))
			{
				continue;
			}

			alumni.Add(Clone(holder));
		}

		var live = new List<ProgramRecordHolder>();
		foreach (IGrouping<Guid, BattingSeasonStats> group in batting
			.Where(row => varsityTeamIds.Contains(row.TeamId))
			.GroupBy(row => row.PlayerId))
		{
			if (!playerNames.TryGetValue(group.Key, out string? name) || string.IsNullOrWhiteSpace(name))
			{
				continue;
			}

			int hits = 0;
			int homeRuns = 0;
			int rbi = 0;
			int doubles = 0;
			foreach (BattingSeasonStats row in group)
			{
				hits += row.Hits;
				homeRuns += row.HomeRuns;
				rbi += row.RunsBattedIn;
				doubles += row.Doubles;
			}

			AddLive(live, organizationId, group.Key, name, ProgramRecordCategory.Hits, hits);
			AddLive(live, organizationId, group.Key, name, ProgramRecordCategory.HomeRuns, homeRuns);
			AddLive(live, organizationId, group.Key, name, ProgramRecordCategory.RunsBattedIn, rbi);
			AddLive(live, organizationId, group.Key, name, ProgramRecordCategory.Doubles, doubles);
		}

		foreach (IGrouping<Guid, PitchingSeasonStats> group in pitching
			.Where(row => varsityTeamIds.Contains(row.TeamId))
			.GroupBy(row => row.PlayerId))
		{
			if (!playerNames.TryGetValue(group.Key, out string? name) || string.IsNullOrWhiteSpace(name))
			{
				continue;
			}

			int wins = 0;
			int strikeouts = 0;
			foreach (PitchingSeasonStats row in group)
			{
				wins += row.Wins;
				strikeouts += row.Strikeouts;
			}

			AddLive(live, organizationId, group.Key, name, ProgramRecordCategory.Wins, wins);
			AddLive(live, organizationId, group.Key, name, ProgramRecordCategory.Strikeouts, strikeouts);
		}

		return Rank(organizationId, alumni.Concat(live));
	}

	public static List<ProgramRecordHolder> Rank(
		string organizationId,
		IEnumerable<ProgramRecordHolder> holders)
	{
		var ranked = new List<ProgramRecordHolder>();
		foreach (ProgramRecordCategory category in Categories)
		{
			List<ProgramRecordHolder> top = holders
				.Where(holder => holder.Category == category && holder.Value > 0)
				.OrderByDescending(holder => holder.Value)
				.ThenBy(holder => holder.PlayerName, StringComparer.OrdinalIgnoreCase)
				.Take(Slots)
				.ToList();
			for (int i = 0; i < top.Count; i++)
			{
				ProgramRecordHolder holder = Clone(top[i]);
				holder.OrganizationId = organizationId;
				holder.Rank = i + 1;
				ranked.Add(holder);
			}
		}

		return ranked;
	}

	public static void ReplaceYear(List<ProgramSeasonResult> history, ProgramSeasonResult season)
	{
		history.RemoveAll(row =>
			row.OrganizationId == season.OrganizationId && row.SeasonYear == season.SeasonYear);
		history.Add(season);
		history.Sort((a, b) => a.SeasonYear.CompareTo(b.SeasonYear));
	}

	private static void AddLive(
		List<ProgramRecordHolder> live,
		string organizationId,
		Guid playerId,
		string name,
		ProgramRecordCategory category,
		int value)
	{
		if (value <= 0)
		{
			return;
		}

		live.Add(new ProgramRecordHolder
		{
			OrganizationId = organizationId,
			Category = category,
			PlayerId = playerId,
			PlayerName = name,
			Value = value,
		});
	}

	private static ProgramRecordHolder Clone(ProgramRecordHolder holder) => new()
	{
		OrganizationId = holder.OrganizationId,
		Category = holder.Category,
		Rank = holder.Rank,
		PlayerName = holder.PlayerName,
		PlayerId = holder.PlayerId,
		Value = holder.Value,
	};
}
