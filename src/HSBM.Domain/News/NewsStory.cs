using System.Globalization;

namespace HSBM.Domain;

public static class NewsTag
{
	public const string Rankings = "RANKINGS";
	public const string District = "DISTRICT";
	public const string Freshman = "FRESHMAN";
	public const string Coach = "COACH";
	public const string Playoffs = "PLAYOFFS";
	public const string Pitching = "PITCHING";
	public const string Hitting = "HITTING";
	public const string Rivalry = "RIVALRY";
	public const string Calendar = "CALENDAR";
	public const string Tryouts = "TRYOUTS";
	public const string Results = "RESULTS";
	public const string Note = "NOTE";
}

/// <summary>
/// A wire story generated from league conditions, not authored copy.
/// </summary>
public sealed class NewsStory
{
	public Guid Id { get; set; }
	public DateOnly Date { get; set; }
	public string Tag { get; set; } = NewsTag.Note;
	public string Headline { get; set; } = "";
	public string Body { get; set; } = "";
	public string Pull { get; set; } = "";
	public string DedupKey { get; set; } = "";
	public bool Read { get; set; }
	public string? SubjectOrganizationId { get; set; }
	public string? SubjectPlayerName { get; set; }

	public string DateLabel => Date.ToString("MMM d", CultureInfo.InvariantCulture).ToUpperInvariant();
}

public sealed record NewsTeamSnapshot(
	string TeamId,
	string OrganizationId,
	string ShortName,
	string DistrictId,
	string DistrictName,
	string Region,
	int Wins,
	int Losses,
	int RunsFor,
	int RunsAgainst,
	int Streak)
{
	public int Games => Wins + Losses;

	public double WinningPercentage => Games == 0 ? 0 : (double)Wins / Games;

	public string RecordLabel => $"{Wins}\u2013{Losses}";

	public int RunDifferential => RunsFor - RunsAgainst;
}

public sealed record NewsPlayerSnapshot(
	Guid PlayerId,
	string FullName,
	string OrganizationId,
	string TeamShortName,
	string DistrictName,
	PlayerGrade Grade,
	string Position,
	int AtBats,
	double Average,
	int HomeRuns,
	int Rbi,
	double Innings,
	double Era,
	int Strikeouts);

public sealed class NewsWorld
{
	public DateOnly Date { get; init; }
	public int SeasonYear { get; init; }
	public IReadOnlyList<NewsTeamSnapshot> Teams { get; init; } = [];
	public IReadOnlyList<NewsPlayerSnapshot> Players { get; init; } = [];
	public IReadOnlyList<DistrictIdentity> Districts { get; init; } = [];

	public static NewsWorld FromCareer(CareerState state)
	{
		var districts = state.Districts.ToDictionary(d => d.Id, StringComparer.Ordinal);
		var teamsById = state.Teams.ToDictionary(t => t.Id, StringComparer.Ordinal);
		var orgsById = state.Organizations.ToDictionary(o => o.Id, StringComparer.Ordinal);
		var records = new Dictionary<string, TeamSeasonRecord>(StringComparer.Ordinal);
		foreach (TeamSeasonRecord record in state.TeamRecords)
		{
			if (record.SeasonYear == state.Career.CurrentSeasonYear)
			{
				records[record.TeamId] = record;
			}
		}

		Dictionary<string, int> streaks = StreaksFrom(state);
		var teams = new List<NewsTeamSnapshot>();
		foreach (TeamIdentity team in state.Teams)
		{
			if (team.Level != TeamLevel.Varsity || team.RosterCap <= 0)
			{
				continue;
			}

			if (!orgsById.TryGetValue(team.OrganizationId, out OrganizationState? org)
				|| LeagueCatalog.IsUnsignedOrganization(org.Id))
			{
				continue;
			}

			if (!districts.TryGetValue(org.DistrictId, out DistrictIdentity? district))
			{
				continue;
			}

			records.TryGetValue(team.Id, out TeamSeasonRecord? record);
			int wins = record?.Wins ?? 0;
			int losses = record?.Losses ?? 0;
			streaks.TryGetValue(team.Id, out int streak);
			teams.Add(new NewsTeamSnapshot(
				team.Id,
				org.Id,
				org.ShortName,
				org.DistrictId,
				district.Name,
				district.Region,
				wins,
				losses,
				record?.RunsFor ?? 0,
				record?.RunsAgainst ?? 0,
				streak));
		}

		var batting = state.BattingStats
			.Where(s => s.SeasonYear == state.Career.CurrentSeasonYear)
			.ToDictionary(s => s.PlayerId);
		var pitching = state.PitchingStats
			.Where(s => s.SeasonYear == state.Career.CurrentSeasonYear)
			.ToDictionary(s => s.PlayerId);
		var players = new List<NewsPlayerSnapshot>();
		foreach (Player player in state.Players)
		{
			if (player.IsUnsigned || player.TeamLevel != TeamLevel.Varsity)
			{
				continue;
			}

			if (!teamsById.TryGetValue(player.TeamId, out TeamIdentity? team)
				|| !orgsById.TryGetValue(team.OrganizationId, out OrganizationState? org)
				|| !districts.TryGetValue(org.DistrictId, out DistrictIdentity? district))
			{
				continue;
			}

			batting.TryGetValue(player.Id, out BattingSeasonStats? hit);
			pitching.TryGetValue(player.Id, out PitchingSeasonStats? arm);
			if ((hit == null || hit.AtBats == 0) && (arm == null || arm.OutsRecorded == 0))
			{
				continue;
			}

			players.Add(new NewsPlayerSnapshot(
				player.Id,
				player.FullName,
				org.Id,
				org.ShortName,
				district.Name,
				player.Grade,
				player.DisplayPosition,
				hit?.AtBats ?? 0,
				hit?.Average ?? 0,
				hit?.HomeRuns ?? 0,
				hit?.RunsBattedIn ?? 0,
				arm?.InningsPitched ?? 0,
				arm?.Era ?? 0,
				arm?.Strikeouts ?? 0));
		}

		return new NewsWorld
		{
			Date = state.Career.CurrentDate,
			SeasonYear = state.Career.CurrentSeasonYear,
			Teams = teams,
			Players = players,
			Districts = state.Districts,
		};
	}

	private static Dictionary<string, int> StreaksFrom(CareerState state)
	{
		var streaks = new Dictionary<string, int>(StringComparer.Ordinal);
		IEnumerable<IGrouping<string, ScheduledGame>> byTeam = state.Games
			.Where(game => game.Status == GameStatus.Completed
				&& game.TeamLevel == TeamLevel.Varsity
				&& !string.IsNullOrEmpty(game.WinningTeamId))
			.SelectMany(game => new[]
			{
				(TeamId: game.HomeTeamId, Game: game),
				(TeamId: game.AwayTeamId, Game: game),
			})
			.GroupBy(item => item.TeamId, item => item.Game);

		foreach (IGrouping<string, ScheduledGame> group in byTeam)
		{
			List<ScheduledGame> ordered = group
				.OrderByDescending(game => game.Date)
				.ThenByDescending(game => game.Id)
				.ToList();
			if (ordered.Count == 0)
			{
				continue;
			}

			bool wonFirst = string.Equals(ordered[0].WinningTeamId, group.Key, StringComparison.Ordinal);
			int length = 0;
			foreach (ScheduledGame game in ordered)
			{
				bool won = string.Equals(game.WinningTeamId, group.Key, StringComparison.Ordinal);
				if (won != wonFirst)
				{
					break;
				}

				length++;
			}

			streaks[group.Key] = wonFirst ? length : -length;
		}

		return streaks;
	}
}
