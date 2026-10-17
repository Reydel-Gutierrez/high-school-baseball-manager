namespace HSBM.Domain;

public sealed class HistoricalStanding
{
	public string TeamId { get; set; } = "";
	public string OrganizationId { get; set; } = "";
	public string Name { get; set; } = "";
	public string DistrictId { get; set; } = "";
	public int Rank { get; set; }
	public int Wins { get; set; }
	public int Losses { get; set; }
	public int RunsFor { get; set; }
	public int RunsAgainst { get; set; }
	public bool Qualifies { get; set; }
}

public sealed class HistoricalChampion
{
	public string Title { get; set; } = "";
	public string TeamId { get; set; } = "";
	public string OrganizationId { get; set; } = "";
	public string Name { get; set; } = "";
}

public sealed class HistoricalLeader
{
	public string Category { get; set; } = "";
	public string Stat { get; set; } = "";
	public Guid PlayerId { get; set; }
	public string PlayerName { get; set; } = "";
	public string TeamShort { get; set; } = "";
	public string Display { get; set; } = "";
}

/// <summary>
/// Frozen league snapshot after a baseball year closes: standings,
/// champions, awards, and statistical leaders.
/// </summary>
public sealed class HistoricalSeason
{
	public int SeasonYear { get; set; }
	public List<HistoricalStanding> Standings { get; set; } = [];
	public List<HistoricalChampion> Champions { get; set; } = [];
	public SeasonAwards? DistrictAwards { get; set; }
	public SeasonAwards? StateAwards { get; set; }
	public List<HistoricalLeader> Leaders { get; set; } = [];
}

/// <summary>
/// Writes program history, title flags, and the league archive from
/// live standings and the postseason bracket.
/// </summary>
public static class SeasonArchive
{
	public static void WriteHistory(CareerState state)
	{
		int year = state.Career.CurrentSeasonYear;
		List<StandingEntry> standings = LeagueStandings.FromWorld(state.World, year);
		Dictionary<string, int> places = LeagueStandings.PlacesByOrganization(standings);
		PostseasonState? bracket = state.World.Postseason;
		if (bracket != null && bracket.SeasonYear != year)
		{
			bracket = null;
		}

		foreach (TeamIdentity team in state.Teams)
		{
			if (team.Level != TeamLevel.Varsity || team.RosterCap <= 0)
			{
				continue;
			}

			OrganizationState org = state.OrganizationForTeam(team.Id);
			if (LeagueCatalog.IsUnsignedOrganization(org.Id))
			{
				continue;
			}

			TeamSeasonRecord record = state.RecordFor(team.Id);
			(int postWins, int postLosses) = PostseasonRecord(state, team.Id);
			if (record.GamesPlayed == 0 && postWins == 0 && postLosses == 0)
			{
				continue;
			}
			int finish = places.GetValueOrDefault(org.Id, 8);
			bool qualified = bracket?.Qualified(team.Id) == true || finish <= LeagueStandings.QualifiersPerDistrict;
			bool wonDistrict = finish == 1;
			bool wonSemi = bracket?.WonRegionalSemi(team.Id) == true;
			bool wonRegional = bracket?.WonRegional(team.Id) == true;
			bool wonState = bracket?.WonState(team.Id) == true;
			ProgramSeasonResult season = new()
			{
				OrganizationId = org.Id,
				SeasonYear = year,
				Wins = record.Wins + postWins,
				Losses = record.Losses + postLosses,
				RegularWins = record.Wins,
				RegularLosses = record.Losses,
				DistrictFinish = finish,
				Postseason = ProgramPostseason.FromPlayoff(wonState, wonRegional, wonSemi, qualified),
				WonDistrict = wonDistrict,
				WonRegional = wonRegional,
				WonState = wonState,
			};
			ProgramRecordBook.ReplaceYear(org.SeasonHistory, season);
			org.Profile.LastSeasonWins = season.RegularWins;
			org.Profile.LastSeasonLosses = season.RegularLosses;
			org.Profile.DistrictFinish = season.DistrictFinish;
		}

		if (bracket != null)
		{
			bracket.HistoryWritten = true;
		}

		UpsertHistoricalSeason(state, year, standings, bracket);
	}

	public static string CommitKey(string organizationId, int year) => $"{organizationId}:{year}";

	public static bool IsCommitted(CareerState state, string organizationId, int year)
	{
		string key = CommitKey(organizationId, year);
		foreach (string existing in state.World.SeasonProfileCommits)
		{
			if (existing == key)
			{
				return true;
			}
		}

		return false;
	}

	public static void MarkCommitted(CareerState state, string organizationId, int year)
	{
		if (IsCommitted(state, organizationId, year))
		{
			return;
		}

		state.World.SeasonProfileCommits.Add(CommitKey(organizationId, year));
	}

	public static void CommitProfiles(CareerState state, bool includeUser)
	{
		int year = state.Career.CurrentSeasonYear;
		string userOrg = state.Career.UserOrganizationId;
		foreach (OrganizationState org in state.Organizations)
		{
			if (LeagueCatalog.IsUnsignedOrganization(org.Id))
			{
				continue;
			}

			if (!includeUser && org.Id == userOrg)
			{
				continue;
			}

			if (IsCommitted(state, org.Id, year))
			{
				continue;
			}

			ProgramSeasonResult? season = org.SeasonHistory.Find(row => row.SeasonYear == year);
			if (season == null)
			{
				continue;
			}

			ProgramReputation.ApplySeason(org, season);
			MarkCommitted(state, org.Id, year);
		}
	}

	public static void EnsureCommitted(CareerState state)
	{
		WriteHistory(state);
		CommitProfiles(state, includeUser: true);
		RefreshRecordBooks(state);
	}

	public static HistoricalSeason? ForYear(GameWorld world, int seasonYear)
	{
		foreach (HistoricalSeason season in world.HistoricalSeasons)
		{
			if (season.SeasonYear == seasonYear)
			{
				return season;
			}
		}

		return null;
	}

	private static (int Wins, int Losses) PostseasonRecord(CareerState state, string teamId)
	{
		int wins = 0;
		int losses = 0;
		int year = state.Career.CurrentSeasonYear;
		foreach (ScheduledGame game in state.Games)
		{
			if (game.SeasonYear != year
				|| game.Status != GameStatus.Completed
				|| !game.GameType.IsPostseason()
				|| !game.Involves(teamId))
			{
				continue;
			}

			if (game.WinningTeamId == teamId)
			{
				wins++;
			}
			else
			{
				losses++;
			}
		}

		return (wins, losses);
	}

	private static void UpsertHistoricalSeason(
		CareerState state,
		int year,
		IReadOnlyList<StandingEntry> standings,
		PostseasonState? bracket)
	{
		var snapshot = new HistoricalSeason
		{
			SeasonYear = year,
			Standings = standings.Select(row => new HistoricalStanding
			{
				TeamId = row.TeamId,
				OrganizationId = row.OrganizationId,
				Name = row.Name,
				DistrictId = row.DistrictId,
				Rank = row.Rank,
				Wins = row.Wins,
				Losses = row.Losses,
				RunsFor = row.RunsFor,
				RunsAgainst = row.RunsAgainst,
				Qualifies = row.Qualifies,
			}).ToList(),
			Champions = Champions(state, standings, bracket),
			DistrictAwards = AwardsEngine.Find(state.World.Awards, year, AwardScope.District),
			StateAwards = AwardsEngine.Find(state.World.Awards, year, AwardScope.State),
			Leaders = Leaders(state, year),
		};

		List<HistoricalSeason> archive = state.World.HistoricalSeasons;
		for (int i = 0; i < archive.Count; i++)
		{
			if (archive[i].SeasonYear == year)
			{
				archive[i] = snapshot;
				return;
			}
		}

		archive.Add(snapshot);
		archive.Sort((a, b) => a.SeasonYear.CompareTo(b.SeasonYear));
	}

	private static List<HistoricalChampion> Champions(
		CareerState state,
		IReadOnlyList<StandingEntry> standings,
		PostseasonState? bracket)
	{
		var list = new List<HistoricalChampion>();
		foreach (DistrictIdentity district in state.Districts)
		{
			StandingEntry? first = null;
			foreach (StandingEntry row in standings)
			{
				if (row.DistrictId == district.Id && row.Rank == 1)
				{
					first = row;
					break;
				}
			}

			if (first != null)
			{
				list.Add(new HistoricalChampion
				{
					Title = $"{district.Name} District",
					TeamId = first.TeamId,
					OrganizationId = first.OrganizationId,
					Name = first.Name,
				});
			}
		}

		if (bracket != null)
		{
			AddChampion(list, state, "East Regional", bracket.EastChampionTeamId);
			AddChampion(list, state, "West Regional", bracket.WestChampionTeamId);
			AddChampion(list, state, "State", bracket.StateChampionTeamId);
		}

		return list;
	}

	private static void AddChampion(
		List<HistoricalChampion> list,
		CareerState state,
		string title,
		string? teamId)
	{
		if (string.IsNullOrEmpty(teamId))
		{
			return;
		}

		OrganizationState org = state.OrganizationForTeam(teamId);
		list.Add(new HistoricalChampion
		{
			Title = title,
			TeamId = teamId,
			OrganizationId = org.Id,
			Name = org.ShortName,
		});
	}

	private static List<HistoricalLeader> Leaders(CareerState state, int year)
	{
		var list = new List<HistoricalLeader>();
		BattingSeasonStats? avg = null;
		BattingSeasonStats? hr = null;
		PitchingSeasonStats? era = null;
		PitchingSeasonStats? strikeouts = null;
		foreach (BattingSeasonStats row in state.BattingStats)
		{
			if (row.SeasonYear != year || row.AtBats < AwardsEngine.MinAtBats)
			{
				continue;
			}

			if (avg == null || row.Average > avg.Average || (row.Average == avg.Average && row.Hits > avg.Hits))
			{
				avg = row;
			}

			if (hr == null || row.HomeRuns > hr.HomeRuns)
			{
				hr = row;
			}
		}

		foreach (PitchingSeasonStats row in state.PitchingStats)
		{
			if (row.SeasonYear != year || row.InningsPitched < AwardsEngine.MinInnings)
			{
				continue;
			}

			if (era == null || row.Era < era.Era || (row.Era == era.Era && row.Strikeouts > era.Strikeouts))
			{
				era = row;
			}

			if (strikeouts == null || row.Strikeouts > strikeouts.Strikeouts)
			{
				strikeouts = row;
			}
		}

		AddBatter(list, state, "Batting", "AVG", avg, avg == null ? "" : FormatRate(avg.Average));
		AddBatter(list, state, "Batting", "HR", hr, hr == null ? "" : hr.HomeRuns.ToString());
		AddPitcher(list, state, "Pitching", "ERA", era, era == null ? "" : era.Era.ToString("0.00"));
		AddPitcher(list, state, "Pitching", "SO", strikeouts, strikeouts == null ? "" : strikeouts.Strikeouts.ToString());
		return list;
	}

	private static void AddBatter(
		List<HistoricalLeader> list,
		CareerState state,
		string category,
		string stat,
		BattingSeasonStats? row,
		string display)
	{
		if (row == null)
		{
			return;
		}

		Player? player = state.PlayerById(row.PlayerId);
		if (player == null)
		{
			return;
		}

		list.Add(new HistoricalLeader
		{
			Category = category,
			Stat = stat,
			PlayerId = player.Id,
			PlayerName = player.FullName,
			TeamShort = SchoolShort(state, player.OrganizationId),
			Display = display,
		});
	}

	private static void AddPitcher(
		List<HistoricalLeader> list,
		CareerState state,
		string category,
		string stat,
		PitchingSeasonStats? row,
		string display)
	{
		if (row == null)
		{
			return;
		}

		Player? player = state.PlayerById(row.PlayerId);
		if (player == null)
		{
			return;
		}

		list.Add(new HistoricalLeader
		{
			Category = category,
			Stat = stat,
			PlayerId = player.Id,
			PlayerName = player.FullName,
			TeamShort = SchoolShort(state, player.OrganizationId),
			Display = display,
		});
	}

	private static string SchoolShort(CareerState state, string organizationId)
	{
		try
		{
			return state.OrganizationById(organizationId).ShortName;
		}
		catch (KeyNotFoundException)
		{
			return "";
		}
	}

	private static string FormatRate(double value)
	{
		string raw = value.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
		return raw.StartsWith('0') ? raw[1..] : raw;
	}

	private static void RefreshRecordBooks(CareerState state)
	{
		var names = new Dictionary<Guid, string>();
		foreach (Player player in state.Players)
		{
			names[player.Id] = player.FullName;
		}

		foreach (OrganizationState org in state.Organizations)
		{
			if (LeagueCatalog.IsUnsignedOrganization(org.Id))
			{
				continue;
			}

			HashSet<string> varsityIds = state.Teams
				.Where(team => team.OrganizationId == org.Id && team.Level == TeamLevel.Varsity)
				.Select(team => team.Id)
				.ToHashSet(StringComparer.Ordinal);
			org.RecordBook = ProgramRecordBook.Compile(
				org.Id,
				org.RecordBook,
				names,
				state.BattingStats,
				state.PitchingStats,
				varsityIds);
		}
	}
}
