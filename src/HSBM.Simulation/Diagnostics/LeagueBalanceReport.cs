using System.Globalization;
using System.Text;
using HSBM.Domain;

namespace HSBM.Simulation;

public sealed record DistributionBucket(string Label, int Count, double Share);

public sealed record ChampionshipCount(string OrganizationId, string Name, int Titles);

public sealed record TeamRecordLine(
	string TeamId,
	string OrganizationId,
	string Name,
	string DistrictId,
	int Rank,
	int Wins,
	int Losses,
	int RunsFor,
	int RunsAgainst);

public sealed class LeagueBalanceSnapshot
{
	public int ActivePlayers { get; init; }
	public int VarsityPlayers { get; init; }
	public double MeanOverall { get; init; }
	public double MeanContact { get; init; }
	public double MeanPower { get; init; }
	public double MeanSpeed { get; init; }
	public double MeanFielding { get; init; }
	public double MeanStuff { get; init; }
	public IReadOnlyList<DistributionBucket> GradeMix { get; init; } = [];
	public IReadOnlyList<DistributionBucket> PositionMix { get; init; } = [];
	public IReadOnlyList<DistributionBucket> OverallBands { get; init; } = [];
	public IReadOnlyList<DistributionBucket> TalentTiers { get; init; } = [];
}

public sealed class SeasonBalanceSnapshot
{
	public int SeasonYear { get; init; }
	public int GamesCompleted { get; init; }
	public double RunsPerTeamGame { get; init; }
	public double HitsPerTeamGame { get; init; }
	public string? StateChampion { get; init; }
	public string? StateChampionOrganizationId { get; init; }
	public IReadOnlyList<TeamRecordLine> Standings { get; init; } = [];
	public IReadOnlyList<ChampionshipCount> DistrictChampions { get; init; } = [];
}

public sealed record LeagueSimulationResult
{
	public int StartYear { get; init; }
	public int EndYear { get; init; }
	public int SeasonsCompleted { get; init; }
	public DateOnly Origin { get; init; }
	public DateOnly CurrentDate { get; init; }
	public int DaysAdvanced { get; init; }
	public int GamesPlayed { get; init; }
	public bool RolledOver { get; init; }
	public IReadOnlyList<string> IntegrityIssues { get; init; } = [];
	public IReadOnlyList<SeasonBalanceSnapshot> Seasons { get; init; } = [];
	public LeagueBalanceSnapshot League { get; init; } = new();

	public IReadOnlyList<ChampionshipCount> ChampionshipDistribution =>
		LeagueBalanceReport.Championships(Seasons);

	public string Format() => LeagueBalanceReport.Format(this);
}

public static class LeagueBalanceReport
{
	public static LeagueBalanceSnapshot SnapshotLeague(CareerState state)
	{
		List<Player> active = state.Players.Where(player => player.IsActive).ToList();
		List<Player> varsity = active.Where(player => player.TeamLevel == TeamLevel.Varsity).ToList();
		List<Player> sample = varsity.Count > 0 ? varsity : active;
		return new LeagueBalanceSnapshot
		{
			ActivePlayers = active.Count,
			VarsityPlayers = varsity.Count,
			MeanOverall = Mean(sample, player => player.Overall),
			MeanContact = Mean(sample, player => player.Ratings.Contact),
			MeanPower = Mean(sample, player => player.Ratings.Power),
			MeanSpeed = Mean(sample, player => player.Ratings.Speed),
			MeanFielding = Mean(sample, player => player.Ratings.Fielding),
			MeanStuff = Mean(sample.Where(player => player.IsPitcher).ToList(), player => player.Ratings.Stuff),
			GradeMix = Mix(active, player => player.Grade.ToAbbrev()),
			PositionMix = Mix(varsity.Count > 0 ? varsity : active, player => player.PrimaryPosition.ToCode()),
			OverallBands = Mix(sample, OverallBand),
			TalentTiers = Mix(
				state.Organizations.Where(org => !LeagueCatalog.IsUnsignedOrganization(org.Id)).ToList(),
				org => org.TalentTier.ToLabel()),
		};
	}

	public static SeasonBalanceSnapshot SnapshotSeason(CareerState state, int seasonYear)
	{
		List<ScheduledGame> completed = state.Games
			.Where(game =>
				game.SeasonYear == seasonYear
				&& game.Status == GameStatus.Completed
				&& game.TeamLevel == TeamLevel.Varsity
				&& game.GameType.CountsInStandings())
			.ToList();
		int teamGames = completed.Count * 2;
		int runs = completed.Sum(game => (game.HomeScore ?? 0) + (game.AwayScore ?? 0));
		HistoricalSeason? archive = SeasonArchive.ForYear(state.World, seasonYear);
		List<StandingEntry> ranked = archive != null && archive.Standings.Count > 0
			? archive.Standings.Select(row => new StandingEntry(
				row.TeamId,
				row.OrganizationId,
				row.Name,
				row.DistrictId,
				row.Wins,
				row.Losses,
				0,
				0,
				row.RunsFor,
				row.RunsAgainst,
				row.Rank,
				row.Qualifies)).ToList()
			: LeagueStandings.FromWorld(state.World, seasonYear);
		HistoricalChampion? stateTitle = archive?.Champions.FirstOrDefault(row => row.Title == "State");
		var districtTitles = new List<ChampionshipCount>();
		if (archive != null)
		{
			foreach (HistoricalChampion row in archive.Champions.Where(item =>
				item.Title.Contains("District", StringComparison.Ordinal)))
			{
				districtTitles.Add(new ChampionshipCount(row.OrganizationId, row.Name, 1));
			}
		}

		HashSet<string> varsityIds = state.Teams
			.Where(team => team.Level == TeamLevel.Varsity && team.RosterCap > 0)
			.Select(team => team.Id)
			.ToHashSet(StringComparer.Ordinal);
		long hits = 0;
		foreach (BattingSeasonStats line in state.BattingStats)
		{
			if (line.SeasonYear == seasonYear && varsityIds.Contains(line.TeamId))
			{
				hits += line.Hits;
			}
		}

		return new SeasonBalanceSnapshot
		{
			SeasonYear = seasonYear,
			GamesCompleted = completed.Count,
			RunsPerTeamGame = teamGames == 0 ? 0 : runs / (double)teamGames,
			HitsPerTeamGame = teamGames == 0 ? 0 : hits / (double)teamGames,
			StateChampion = stateTitle?.Name,
			StateChampionOrganizationId = stateTitle?.OrganizationId,
			Standings = ranked.Select(row => new TeamRecordLine(
				row.TeamId,
				row.OrganizationId,
				row.Name,
				row.DistrictId,
				row.Rank,
				row.Wins,
				row.Losses,
				row.RunsFor,
				row.RunsAgainst)).ToList(),
			DistrictChampions = districtTitles,
		};
	}

	public static IReadOnlyList<ChampionshipCount> Championships(IEnumerable<SeasonBalanceSnapshot> seasons)
	{
		var counts = new Dictionary<string, ChampionshipCount>(StringComparer.Ordinal);
		foreach (SeasonBalanceSnapshot season in seasons)
		{
			if (string.IsNullOrWhiteSpace(season.StateChampionOrganizationId))
			{
				continue;
			}

			if (!counts.TryGetValue(season.StateChampionOrganizationId, out ChampionshipCount? existing))
			{
				counts[season.StateChampionOrganizationId] = new ChampionshipCount(
					season.StateChampionOrganizationId,
					season.StateChampion ?? season.StateChampionOrganizationId,
					1);
				continue;
			}

			counts[season.StateChampionOrganizationId] = existing with { Titles = existing.Titles + 1 };
		}

		return counts.Values.OrderByDescending(row => row.Titles).ThenBy(row => row.Name, StringComparer.Ordinal).ToList();
	}

	public static string Format(LeagueSimulationResult result)
	{
		var text = new StringBuilder();
		text.AppendLine(CultureInfo.InvariantCulture,
			$"Multi-season simulation: {result.SeasonsCompleted} seasons, {result.DaysAdvanced} days, {result.GamesPlayed} games");
		text.AppendLine(CultureInfo.InvariantCulture,
			$"  {result.Origin:yyyy-MM-dd} -> {result.CurrentDate:yyyy-MM-dd}  (year {result.StartYear} -> {result.EndYear})");
		if (result.IntegrityIssues.Count == 0)
		{
			text.AppendLine("  integrity: OK");
		}
		else
		{
			text.AppendLine(CultureInfo.InvariantCulture, $"  integrity: {result.IntegrityIssues.Count} issue(s)");
			foreach (string issue in result.IntegrityIssues)
			{
				text.AppendLine(CultureInfo.InvariantCulture, $"    - {issue}");
			}
		}

		text.AppendLine();
		text.Append(FormatLeague(result.League));
		text.AppendLine();
		text.AppendLine("Scoring by season (varsity, counting games)");
		foreach (SeasonBalanceSnapshot season in result.Seasons)
		{
			text.AppendLine(CultureInfo.InvariantCulture,
				$"  {season.SeasonYear}: {season.GamesCompleted,3} games  {season.RunsPerTeamGame,4:0.00} R/G  {season.HitsPerTeamGame,4:0.00} H/G  champ {season.StateChampion ?? "—"}");
		}

		text.AppendLine();
		text.AppendLine("Championship distribution (state titles)");
		IReadOnlyList<ChampionshipCount> titles = result.ChampionshipDistribution;
		if (titles.Count == 0)
		{
			text.AppendLine("  (none)");
		}
		else
		{
			foreach (ChampionshipCount row in titles)
			{
				text.AppendLine(CultureInfo.InvariantCulture, $"  {row.Name,-16} {row.Titles,2}");
			}
		}

		if (result.Seasons.Count > 0)
		{
			text.AppendLine();
			text.Append(FormatStandings(result.Seasons[^1]));
		}

		return text.ToString();
	}

	public static string FormatLeague(LeagueBalanceSnapshot league)
	{
		var text = new StringBuilder();
		text.AppendLine("League averages (active varsity unless noted)");
		text.AppendLine(CultureInfo.InvariantCulture,
			$"  players: {league.ActivePlayers} active / {league.VarsityPlayers} varsity");
		text.AppendLine(CultureInfo.InvariantCulture,
			$"  OVR {league.MeanOverall,5:0.0}  CON {league.MeanContact,5:0.0}  POW {league.MeanPower,5:0.0}  SPD {league.MeanSpeed,5:0.0}  FLD {league.MeanFielding,5:0.0}  STU {league.MeanStuff,5:0.0}");
		text.AppendLine("  grades:     " + FormatMix(league.GradeMix));
		text.AppendLine("  positions:  " + FormatMix(league.PositionMix));
		text.AppendLine("  overall:    " + FormatMix(league.OverallBands));
		text.AppendLine("  programs:   " + FormatMix(league.TalentTiers));
		return text.ToString();
	}

	public static string FormatWorld(CareerState state) => FormatLeague(SnapshotLeague(state));

	private static string FormatStandings(SeasonBalanceSnapshot season)
	{
		var text = new StringBuilder();
		text.AppendLine(CultureInfo.InvariantCulture, $"Team records ({season.SeasonYear} varsity)");
		string district = "";
		foreach (TeamRecordLine row in season.Standings)
		{
			if (row.DistrictId != district)
			{
				district = row.DistrictId;
				text.AppendLine();
				text.AppendLine(CultureInfo.InvariantCulture, $"  {district.ToUpperInvariant()}");
				text.AppendLine(CultureInfo.InvariantCulture, $"  {"Rk",-3} {"Team",-16} {"W",3} {"L",3} {"RF",4} {"RA",4}");
			}

			text.AppendLine(CultureInfo.InvariantCulture,
				$"  {row.Rank,-3} {row.Name,-16} {row.Wins,3} {row.Losses,3} {row.RunsFor,4} {row.RunsAgainst,4}");
		}

		return text.ToString();
	}

	private static string FormatMix(IReadOnlyList<DistributionBucket> buckets) =>
		string.Join("  ", buckets.Select(bucket =>
			$"{bucket.Label} {bucket.Share.ToString("0%", CultureInfo.InvariantCulture)}"));

	private static List<DistributionBucket> Mix<T>(IReadOnlyList<T> items, Func<T, string> key)
	{
		int total = items.Count;
		return items
			.GroupBy(key, StringComparer.Ordinal)
			.OrderBy(group => group.Key, StringComparer.Ordinal)
			.Select(group => new DistributionBucket(
				group.Key,
				group.Count(),
				total == 0 ? 0 : group.Count() / (double)total))
			.ToList();
	}

	private static string OverallBand(Player player) => player.Overall switch
	{
		>= 86 => "86-100",
		>= 71 => "71-85",
		>= 56 => "56-70",
		>= 41 => "41-55",
		_ => "1-40",
	};

	private static double Mean(IReadOnlyList<Player> players, Func<Player, int> value)
	{
		if (players.Count == 0)
		{
			return 0;
		}

		return players.Average(value);
	}
}
