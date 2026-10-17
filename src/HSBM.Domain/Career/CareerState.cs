namespace HSBM.Domain;

/// <summary>
/// In-memory snapshot of a career save. The world is the persistent
/// root; the career is the player's coach inside that world.
/// </summary>
public sealed class CareerState
{
	public Career Career { get; set; } = new();
	public GameWorld World { get; set; } = new();

	public List<DistrictIdentity> Districts
	{
		get => World.Districts;
		set => World.Districts = value;
	}

	public List<OrganizationState> Organizations
	{
		get => World.Organizations;
		set => World.Organizations = value;
	}

	public List<TeamIdentity> Teams
	{
		get => World.Teams;
		set => World.Teams = value;
	}

	public List<Player> Players
	{
		get => World.Players;
		set => World.Players = value;
	}

	public List<ProgramCoach> Coaches
	{
		get => World.Coaches;
		set => World.Coaches = value;
	}

	public List<ScheduledGame> Games
	{
		get => World.Games;
		set => World.Games = value;
	}

	public List<TeamSeasonRecord> TeamRecords
	{
		get => World.TeamRecords;
		set => World.TeamRecords = value;
	}

	public List<BattingSeasonStats> BattingStats
	{
		get => World.BattingStats;
		set => World.BattingStats = value;
	}

	public List<PitchingSeasonStats> PitchingStats
	{
		get => World.PitchingStats;
		set => World.PitchingStats = value;
	}

	public List<FieldingSeasonStats> FieldingStats
	{
		get => World.FieldingStats;
		set => World.FieldingStats = value;
	}

	public List<TeamLineup> Lineups
	{
		get => World.Lineups;
		set => World.Lineups = value;
	}

	public List<TeamRotation> Rotations
	{
		get => World.Rotations;
		set => World.Rotations = value;
	}

	public List<GameBoxScore> BoxScores
	{
		get => World.BoxScores;
		set => World.BoxScores = value;
	}

	public List<DistrictOpenPlay> OpenPlay
	{
		get => World.OpenPlay;
		set => World.OpenPlay = value;
	}

	public List<RecruitingOffer> Offers
	{
		get => World.Offers;
		set => World.Offers = value;
	}

	public List<PlayerAgreement> Agreements
	{
		get => World.Agreements;
		set => World.Agreements = value;
	}

	public List<RecruitingInterest> RecruitingInterest
	{
		get => World.RecruitingInterest;
		set => World.RecruitingInterest = value;
	}

	public List<RecruitingTarget> RecruitingTargets
	{
		get => World.RecruitingTargets;
		set => World.RecruitingTargets = value;
	}

	public List<NewsStory> News
	{
		get => World.News;
		set => World.News = value;
	}

	public List<PlayerTransaction> Transactions
	{
		get => World.Transactions;
		set => World.Transactions = value;
	}

	public PostseasonState? Postseason
	{
		get => World.Postseason;
		set => World.Postseason = value;
	}

	public List<SeasonAwards> Awards
	{
		get => World.Awards;
		set => World.Awards = value;
	}

	public List<HistoricalSeason> HistoricalSeasons
	{
		get => World.HistoricalSeasons;
		set => World.HistoricalSeasons = value;
	}

	public List<string> SeasonProfileCommits
	{
		get => World.SeasonProfileCommits;
		set => World.SeasonProfileCommits = value;
	}

	public CompetitionLevel CompetitionLevel
	{
		get => World.CompetitionLevel;
		set => World.CompetitionLevel = value;
	}

	public void SyncClockFromCareer()
	{
		World.Seed = Career.RandomSeed;
		World.CurrentDate = Career.CurrentDate;
		World.CurrentSeasonYear = Career.CurrentSeasonYear;
		World.CompetitionLevel = Career.CompetitionLevel;
	}

	public void SyncClockToCareer()
	{
		Career.RandomSeed = World.Seed;
		Career.CurrentDate = World.CurrentDate;
		Career.CurrentSeasonYear = World.CurrentSeasonYear;
		Career.CompetitionLevel = World.CompetitionLevel;
	}

	public IEnumerable<Player> RosterFor(string teamId)
	{
		foreach (Player player in Players)
		{
			if (player.TeamId == teamId && !player.Alumni)
			{
				yield return player;
			}
		}
	}

	public TeamSeasonRecord RecordFor(string teamId)
	{
		foreach (TeamSeasonRecord record in TeamRecords)
		{
			if (record.TeamId == teamId && record.SeasonYear == Career.CurrentSeasonYear)
			{
				return record;
			}
		}

		var created = new TeamSeasonRecord
		{
			TeamId = teamId,
			SeasonYear = Career.CurrentSeasonYear,
		};
		TeamRecords.Add(created);
		return created;
	}

	public BattingSeasonStats BattingFor(Guid playerId, string teamId)
	{
		foreach (BattingSeasonStats stats in BattingStats)
		{
			if (stats.PlayerId == playerId && stats.SeasonYear == Career.CurrentSeasonYear)
			{
				return stats;
			}
		}

		var created = new BattingSeasonStats
		{
			PlayerId = playerId,
			SeasonYear = Career.CurrentSeasonYear,
			TeamId = teamId,
		};
		BattingStats.Add(created);
		return created;
	}

	public PitchingSeasonStats PitchingFor(Guid playerId, string teamId)
	{
		foreach (PitchingSeasonStats stats in PitchingStats)
		{
			if (stats.PlayerId == playerId && stats.SeasonYear == Career.CurrentSeasonYear)
			{
				return stats;
			}
		}

		var created = new PitchingSeasonStats
		{
			PlayerId = playerId,
			SeasonYear = Career.CurrentSeasonYear,
			TeamId = teamId,
		};
		PitchingStats.Add(created);
		return created;
	}

	public FieldingSeasonStats FieldingFor(Guid playerId, string teamId)
	{
		foreach (FieldingSeasonStats stats in FieldingStats)
		{
			if (stats.PlayerId == playerId && stats.SeasonYear == Career.CurrentSeasonYear)
			{
				return stats;
			}
		}

		var created = new FieldingSeasonStats
		{
			PlayerId = playerId,
			SeasonYear = Career.CurrentSeasonYear,
			TeamId = teamId,
		};
		FieldingStats.Add(created);
		return created;
	}

	public TeamLineup? LineupFor(string teamId)
	{
		foreach (TeamLineup lineup in Lineups)
		{
			if (lineup.TeamId == teamId)
			{
				return lineup;
			}
		}

		return null;
	}

	public TeamRotation? RotationFor(string teamId)
	{
		foreach (TeamRotation rotation in Rotations)
		{
			if (rotation.TeamId == teamId)
			{
				return rotation;
			}
		}

		return null;
	}

	public GameBoxScore? BoxScoreFor(Guid gameId)
	{
		foreach (GameBoxScore sheet in BoxScores)
		{
			if (sheet.GameId == gameId)
			{
				return sheet;
			}
		}

		return null;
	}

	public int CompletedStarts(string teamId)
	{
		int count = 0;
		foreach (ScheduledGame game in Games)
		{
			if (game.SeasonYear == Career.CurrentSeasonYear
				&& game.Status == GameStatus.Completed
				&& !game.GameType.IsExhibition()
				&& game.Involves(teamId))
			{
				count++;
			}
		}

		return count;
	}

	public void UpsertLineup(TeamLineup lineup)
	{
		for (int i = 0; i < Lineups.Count; i++)
		{
			if (Lineups[i].TeamId == lineup.TeamId)
			{
				Lineups[i] = lineup;
				return;
			}
		}

		Lineups.Add(lineup);
	}

	public void UpsertRotation(TeamRotation rotation)
	{
		for (int i = 0; i < Rotations.Count; i++)
		{
			if (Rotations[i].TeamId == rotation.TeamId)
			{
				Rotations[i] = rotation;
				return;
			}
		}

		Rotations.Add(rotation);
	}

	public void UpsertBoxScore(GameBoxScore sheet)
	{
		for (int i = 0; i < BoxScores.Count; i++)
		{
			if (BoxScores[i].GameId == sheet.GameId)
			{
				BoxScores[i] = sheet;
				return;
			}
		}

		BoxScores.Add(sheet);
	}

	public OrganizationState OrganizationForTeam(string teamId)
	{
		TeamIdentity team = GetTeam(teamId);
		foreach (OrganizationState org in Organizations)
		{
			if (org.Id == team.OrganizationId)
			{
				return org;
			}
		}

		throw new KeyNotFoundException($"Organization for team '{teamId}' was not found.");
	}

	public TeamIdentity GetTeam(string teamId)
	{
		foreach (TeamIdentity team in Teams)
		{
			if (team.Id == teamId)
			{
				return team;
			}
		}

		throw new KeyNotFoundException($"Team '{teamId}' was not found.");
	}

	public bool SameDistrict(string teamIdA, string teamIdB)
	{
		string orgA = GetTeam(teamIdA).OrganizationId;
		string orgB = GetTeam(teamIdB).OrganizationId;
		string? districtA = null;
		string? districtB = null;
		foreach (OrganizationState org in Organizations)
		{
			if (org.Id == orgA)
			{
				districtA = org.DistrictId;
			}

			if (org.Id == orgB)
			{
				districtB = org.DistrictId;
			}
		}

		return districtA != null && districtA == districtB;
	}

	public IEnumerable<Player> ReturningFor(string organizationId)
	{
		foreach (Player player in Players)
		{
			if (!player.Alumni && !player.IsUnsigned && player.OrganizationId == organizationId)
			{
				yield return player;
			}
		}
	}

	public IEnumerable<Player> UnsignedInDistrict(string districtId)
	{
		foreach (Player player in Players)
		{
			if (!player.IsUnsigned)
			{
				continue;
			}

			if (player.TeamId == LeagueCatalog.OpenPlayTeamId(districtId))
			{
				yield return player;
			}
		}
	}

	public Player? PlayerById(Guid id)
	{
		foreach (Player player in Players)
		{
			if (player.Id == id)
			{
				return player;
			}
		}

		return null;
	}

	public OrganizationState OrganizationById(string organizationId)
	{
		foreach (OrganizationState org in Organizations)
		{
			if (org.Id == organizationId)
			{
				return org;
			}
		}

		throw new KeyNotFoundException($"Organization '{organizationId}' was not found.");
	}

	public DistrictOpenPlay? OpenPlayFor(string districtId)
	{
		foreach (DistrictOpenPlay session in OpenPlay)
		{
			if (session.DistrictId == districtId)
			{
				return session;
			}
		}

		return null;
	}

	public PlayerAgreement? AgreementFor(Guid playerId)
	{
		foreach (PlayerAgreement agreement in Agreements)
		{
			if (agreement.PlayerId == playerId && agreement.YearsRemaining > 0)
			{
				return agreement;
			}
		}

		return null;
	}
}
