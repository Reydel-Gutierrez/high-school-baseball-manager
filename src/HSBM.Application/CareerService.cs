using HSBM.Domain;
using HSBM.Simulation;

namespace HSBM.Application;

public sealed class CareerService : IDisposable
{
	private readonly ICareerStore _store;
	private readonly LeagueCatalog _catalog;
	private readonly CareerWorldGenerator _generator;
	private readonly BaseballGameSimulator _simulator;

	public CareerService(
		ICareerStore store,
		LeagueCatalog? catalog = null,
		CareerWorldGenerator? generator = null,
		BaseballGameSimulator? simulator = null,
		ILineupProvider? lineups = null)
	{
		_store = store;
		_catalog = catalog ?? LeagueCatalog.Standard;
		_generator = generator ?? new CareerWorldGenerator();
		_simulator = simulator ?? new BaseballGameSimulator(BaseballSimulationConfig.Default, lineups);
	}

	public static CareerService Create(ICareerStore store) => new(store);

	public string DatabasePath => _store.DatabasePath;

	public CreateCareerResult CreateCareer(CreateCareerRequest request, Action<WorldGenerationProgress>? onProgress = null)
	{
		string userOrg = request.UserOrganizationId?.Trim() ?? "";
		if (!string.IsNullOrEmpty(userOrg))
		{
			_catalog.GetOrganization(userOrg);
		}

		int seasonYear = request.SeasonYear <= 0 ? LeagueCatalog.DefaultSeasonYear : request.SeasonYear;
		GeneratedWorld world = _generator.Generate(_catalog, request.Seed, seasonYear, onProgress);
		DateOnly start = SeasonCalendar.OpenPlayStart(seasonYear);
		GameWorld gameWorld = world.ToGameWorld(start);

		var state = new CareerState
		{
			Career = new Career
			{
				Id = Guid.NewGuid(),
				Name = string.IsNullOrWhiteSpace(request.Name) ? "New Career" : request.Name.Trim(),
				CreatedAt = DateTimeOffset.UtcNow,
				CurrentDate = start,
				CurrentSeasonYear = seasonYear,
				UserOrganizationId = userOrg,
				RandomSeed = request.Seed,
				CompetitionLevel = world.CompetitionLevel,
				SaveVersion = Career.CurrentSaveVersion,
				Coach = request.Coach?.Clone() ?? new CoachProfile(),
				Reputation = CoachReputation.Starting,
			},
			World = gameWorld,
		};
		state.TeamRecords = world.Teams
			.Where(team => team.RosterCap > 0)
			.Select(team => new TeamSeasonRecord
			{
				TeamId = team.Id,
				SeasonYear = seasonYear,
			}).ToList();
		state.BattingStats = [];
		state.PitchingStats = [];
		state.FieldingStats = [];
		state.Lineups = [];
		state.Rotations = [];
		state.BoxScores = [];
		state.Offers = [];
		state.Agreements = [];
		state.SyncClockFromCareer();

		NewsWire.Publish(state);
		_store.ReplaceAll(state);
		return new CreateCareerResult
		{
			CareerId = state.Career.Id,
			Name = state.Career.Name,
			Seed = state.Career.RandomSeed,
			CurrentDate = state.Career.CurrentDate,
			PlayerCount = state.Players.Count,
			GameCount = state.Games.Count,
			UserOrganizationId = state.Career.UserOrganizationId,
		};
	}

	public CareerState LoadCareer() => _store.Load();

	public IReadOnlyList<JobListing> GetJobBoard() => JobBoard.From(_store.Load());

	public SignContractResult SignContract(string organizationId)
	{
		if (string.IsNullOrWhiteSpace(organizationId))
		{
			throw new ArgumentException("Organization is required.", nameof(organizationId));
		}

		CareerState state = _store.Load();
		if (state.Career.HasSignedContract)
		{
			throw new InvalidOperationException("A contract is already signed.");
		}

		OrganizationState org = RequireOpening(state, organizationId);
		BindToSchool(state, org, alreadySigned: false, CoachContract.For(org, state.Career.CurrentSeasonYear));
		state.Career.SeasonsCoached = 1;
		CloseAllOpenings(state);
		_store.ReplaceAll(state);
		return ToSignResult(org);
	}

	public SignContractResult ChangeJob(string organizationId)
	{
		if (string.IsNullOrWhiteSpace(organizationId))
		{
			throw new ArgumentException("Organization is required.", nameof(organizationId));
		}

		CareerState state = _store.Load();
		if (!state.Career.HasSignedContract)
		{
			return SignContract(organizationId);
		}

		if (string.Equals(state.Career.UserOrganizationId, organizationId, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("Already coaching that program.");
		}

		OrganizationState dest = RequireOpening(state, organizationId);
		if (!CoachReputation.CanReach(state.Career.Reputation, dest.Profile.Reputation))
		{
			throw new InvalidOperationException($"Reputation does not reach '{dest.DisplayName}'.");
		}

		BindToSchool(state, dest, alreadySigned: true, CoachContract.For(dest, state.Career.CurrentSeasonYear));
		_store.ReplaceAll(state);
		return ToSignResult(dest);
	}

	public SignContractResult AcceptJobOffer(Guid offerId)
	{
		CareerState state = _store.Load();
		CoachingJobOffer offer = state.Career.JobOffers.FirstOrDefault(item => item.Id == offerId && item.IsPending)
			?? throw new KeyNotFoundException("That offer is no longer on the table.");
		if (!state.Career.HasSignedContract)
		{
			return SignContract(offer.OrganizationId);
		}

		OrganizationState dest = RequireOpening(state, offer.OrganizationId);
		BindToSchool(state, dest, alreadySigned: true, offer.ToContract(state.Career.CurrentSeasonYear));
		_store.ReplaceAll(state);
		return ToSignResult(dest);
	}

	public bool DeclineJobOffer(Guid offerId)
	{
		CareerState state = _store.Load();
		CoachingJobOffer? offer = state.Career.JobOffers.FirstOrDefault(item => item.Id == offerId);
		if (offer == null || !offer.IsPending)
		{
			return false;
		}

		offer.Status = CoachingJobOfferStatus.Declined;
		_store.ReplaceAll(state);
		return true;
	}

	public IReadOnlyList<CoachingJobOffer> GetPendingJobOffers() =>
		_store.Load().Career.PendingJobOffers;

	public AdvanceDayResult AdvanceDay()
	{
		CareerState state = _store.Load();
		CareerTickResult tick = CareerClock.Tick(state, _simulator, _catalog);
		_store.ReplaceAll(state);
		return ToAdvanceResult(tick);
	}

	/// <summary>
	/// Prepare one of today's scheduled games for live play. The returned
	/// game is detached from storage; finish it, then pass its result to
	/// <see cref="AdvanceDay(GameSimulationResult)"/>.
	/// </summary>
	public LiveGame? StartLiveGame(Guid gameId)
	{
		CareerState state = _store.Load();
		ScheduledGame? scheduled = state.Games.FirstOrDefault(game => game.Id == gameId && game.Status == GameStatus.Scheduled);
		return scheduled == null ? null : WorldDayProcessor.PrepareLive(state, scheduled, _simulator);
	}

	/// <summary>Advance the day, applying a game the user already played live.</summary>
	public AdvanceDayResult AdvanceDay(GameSimulationResult preplayed)
	{
		_simulator.Preload(preplayed);
		return AdvanceDay();
	}

	public bool TryPlaceRecruitingOffer(
		string organizationId,
		Guid playerId,
		int annualSponsorship,
		int termYears)
	{
		return TryPlaceRecruitingOffer(organizationId, playerId, annualSponsorship, termYears, out _);
	}

	public bool TryPlaceRecruitingOffer(
		string organizationId,
		Guid playerId,
		int annualSponsorship,
		int termYears,
		out string error)
	{
		CareerState state = _store.Load();
		RecruitingMarket market = MarketFrom(state);
		if (!market.TryPlaceOffer(
			organizationId,
			playerId,
			annualSponsorship,
			termYears,
			state.Career.CurrentDate,
			state.Career.CurrentSeasonYear,
			out error))
		{
			return false;
		}

		ApplyMarket(state, market);
		_store.ReplaceAll(state);
		return true;
	}

	public void ApplyRecruiting(
		RecruitingMarket market,
		DateOnly? currentDate = null,
		IReadOnlyList<RecruitingTarget>? targets = null)
	{
		CareerState state = _store.Load();
		ApplyMarket(state, market);
		if (targets != null)
		{
			state.RecruitingTargets = targets.ToList();
		}

		if (currentDate.HasValue)
		{
			state.Career.CurrentDate = currentDate.Value;
		}

		_store.ReplaceAll(state);
	}

	public void SetCurrentDate(DateOnly date)
	{
		CareerState state = _store.Load();
		state.Career.CurrentDate = date;
		state.World.CurrentDate = date;
		state.SyncClockFromCareer();
		PostseasonDesk.Tick(state, date);
		NewsWire.Publish(state);
		_store.ReplaceAll(state);
	}

	public bool TryWithdrawRecruitingOffer(string organizationId, Guid playerId)
	{
		CareerState state = _store.Load();
		RecruitingMarket market = MarketFrom(state);
		if (!market.TryWithdraw(organizationId, playerId))
		{
			return false;
		}

		WriteMarket(state, market);
		_store.ReplaceAll(state);
		return true;
	}

	public AdvanceDayResult AdvanceDays(int days)
	{
		if (days < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(days), "Day count must be at least 1.");
		}

		AdvanceDayResult last = AdvanceDay();
		DateOnly previous = last.PreviousDate;
		var games = last.GamesPlayed.ToList();
		var stories = last.StoriesPublished.ToList();
		var transactions = last.TransactionsPosted.ToList();
		for (int i = 1; i < days; i++)
		{
			last = AdvanceDay();
			games.AddRange(last.GamesPlayed);
			stories.AddRange(last.StoriesPublished);
			transactions.AddRange(last.TransactionsPosted);
		}

		return new AdvanceDayResult
		{
			PreviousDate = previous,
			NewDate = last.NewDate,
			GamesPlayed = games,
			StoriesPublished = stories,
			TransactionsPosted = transactions,
			Phase = last.Phase,
			RolledOver = last.RolledOver,
		};
	}

	public AdvanceDayResult SimulateUntilNextEvent(string? userTeamId, int maxDays = 120)
	{
		if (maxDays < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(maxDays), "Day count must be at least 1.");
		}

		CareerState start = _store.Load();
		DateOnly origin = start.Career.CurrentDate;
		if (HasUserGame(start, userTeamId))
		{
			return new AdvanceDayResult
			{
				PreviousDate = origin,
				NewDate = origin,
				GamesPlayed = [],
				Phase = CareerCalendar.PhaseOn(start.Career.CurrentSeasonYear, origin),
			};
		}

		AdvanceDayResult last = AdvanceDay();
		var games = last.GamesPlayed.ToList();
		var stories = last.StoriesPublished.ToList();
		var transactions = last.TransactionsPosted.ToList();
		int advanced = 1;
		while (advanced < maxDays)
		{
			CareerState state = _store.Load();
			if (HasUserGame(state, userTeamId) || CareerCalendar.IsSimStop(state.Career.CurrentSeasonYear, state.Career.CurrentDate))
			{
				break;
			}

			last = AdvanceDay();
			games.AddRange(last.GamesPlayed);
			stories.AddRange(last.StoriesPublished);
			transactions.AddRange(last.TransactionsPosted);
			advanced++;
		}

		return new AdvanceDayResult
		{
			PreviousDate = origin,
			NewDate = last.NewDate,
			GamesPlayed = games,
			StoriesPublished = stories,
			TransactionsPosted = transactions,
			Phase = last.Phase,
			RolledOver = last.RolledOver,
		};
	}

	private static bool HasUserGame(CareerState state, string? userTeamId) =>
		!string.IsNullOrWhiteSpace(userTeamId)
		&& CareerCalendar.HasScheduledGame(state.World, userTeamId, state.Career.CurrentDate);

	public AdvanceDayResult SimulateRemainingSeason()
	{
		CareerState state = _store.Load();
		int year = state.Career.CurrentSeasonYear;
		LeagueSimulationResult simulated = CareerWorldRunner.AdvanceUntil(
			state,
			(live, tick) => tick.RolledOver || live.Career.CurrentSeasonYear > year,
			_simulator,
			_catalog);
		_store.ReplaceAll(state);
		return new AdvanceDayResult
		{
			PreviousDate = simulated.Origin,
			NewDate = simulated.CurrentDate,
			GamesPlayed = [],
			Phase = CareerCalendar.PhaseOn(state.Career.CurrentSeasonYear, state.Career.CurrentDate),
			RolledOver = simulated.RolledOver,
		};
	}

	public LeagueSimulationResult SimulateSeasons(int seasons)
	{
		CareerState state = _store.Load();
		LeagueSimulationResult result = CareerWorldRunner.RunSeasons(state, seasons, _simulator, _catalog);
		_store.ReplaceAll(state);
		return result;
	}

	public IReadOnlyList<Player> GetRoster(string teamId)
	{
		CareerState state = _store.Load();
		return state.Players.Where(p => p.TeamId == teamId && !p.Alumni).ToList();
	}

	public IReadOnlyList<ProgramGraduate> GetGraduates(string? organizationId = null, int? classYear = null)
	{
		CareerState state = _store.Load();
		string org = string.IsNullOrWhiteSpace(organizationId)
			? state.Career.UserOrganizationId
			: organizationId;
		return GraduateHistory.For(state.Players, state.BattingStats, state.PitchingStats, org, classYear);
	}

	public IReadOnlyList<ScheduledGame> GetSchedule(string? teamId = null)
	{
		CareerState state = _store.Load();
		IEnumerable<ScheduledGame> games = state.Games;
		if (!string.IsNullOrWhiteSpace(teamId))
		{
			games = games.Where(game => game.Involves(teamId));
		}

		return games.OrderBy(game => game.Date).ThenBy(game => game.TeamLevel).ToList();
	}

	public void ArchiveCompletedSeason(
		ProgramSeasonResult userSeason,
		int? reputation = null,
		int? baseballBudget = null,
		ProgramHallOfFameInductee? userInductee = null)
	{
		if (string.IsNullOrWhiteSpace(userSeason.OrganizationId))
		{
			throw new ArgumentException("Organization is required.", nameof(userSeason));
		}

		CareerState state = _store.Load();
		SeasonArchive.WriteHistory(state);
		ProgramRecordBook.ReplaceYear(
			state.OrganizationById(userSeason.OrganizationId).SeasonHistory,
			userSeason);
		SeasonArchive.CommitProfiles(state, includeUser: false);
		ApplyUserSeasonProfile(state, userSeason, reputation, baseballBudget);
		SeasonArchive.MarkCommitted(state, userSeason.OrganizationId, userSeason.SeasonYear);
		ApplyCoachSeason(state, userSeason);
		RefreshRecordBooks(state);
		InductHallOfFame(state, userSeason, userInductee);
		ISeededRandom carouselRng = new SeededRandom(
			SeededRandom.Mix(state.Career.RandomSeed, userSeason.SeasonYear ^ unchecked((int)0xC0A7C4)));
		CoachingCarousel.Assign(state.Organizations, state.Career.UserOrganizationId, carouselRng);
		state.Career.JobOffers = InboundJobOffers.Draft(state.Organizations, state.Career, carouselRng.Derive(5));
		_store.ReplaceAll(state);
	}

	public IReadOnlyList<StandingRow> GetStandings(string? districtId = null, TeamLevel level = TeamLevel.Varsity)
	{
		CareerState state = _store.Load();
		var rows = new List<StandingRow>();
		foreach (TeamIdentity team in state.Teams.Where(t => t.Level == level && t.RosterCap > 0))
		{
			OrganizationState org = state.Organizations.First(o => o.Id == team.OrganizationId);
			if (LeagueCatalog.IsUnsignedOrganization(org.Id))
			{
				continue;
			}

			if (districtId != null && org.DistrictId != districtId)
			{
				continue;
			}

			TeamSeasonRecord record = state.RecordFor(team.Id);
			rows.Add(new StandingRow
			{
				Rank = 0,
				TeamId = team.Id,
				OrganizationId = org.Id,
				Name = org.ShortName,
				DistrictId = org.DistrictId,
				Wins = record.Wins,
				Losses = record.Losses,
				DistrictWins = record.DistrictWins,
				DistrictLosses = record.DistrictLosses,
				RunsFor = record.RunsFor,
				RunsAgainst = record.RunsAgainst,
				WinningPercentage = record.WinningPercentage,
			});
		}

		rows.Sort((a, b) =>
		{
			int win = b.Wins.CompareTo(a.Wins);
			if (win != 0)
			{
				return win;
			}

			int loss = a.Losses.CompareTo(b.Losses);
			if (loss != 0)
			{
				return loss;
			}

			int runDiff = (b.RunsFor - b.RunsAgainst).CompareTo(a.RunsFor - a.RunsAgainst);
			return runDiff != 0 ? runDiff : string.CompareOrdinal(a.Name, b.Name);
		});

		for (int i = 0; i < rows.Count; i++)
		{
			rows[i] = rows[i] with { Rank = i + 1 };
		}

		return rows;
	}

	public IReadOnlyList<BattingLeaderRow> GetBattingLeaders(int take = 15)
	{
		CareerState state = _store.Load();
		var players = state.Players.ToDictionary(p => p.Id);
		return state.BattingStats
			.Where(s => s.AtBats >= 1)
			.OrderByDescending(s => s.Average)
			.ThenByDescending(s => s.Hits)
			.Take(take)
			.Select(stats =>
			{
				Player player = players[stats.PlayerId];
				return new BattingLeaderRow
				{
					PlayerId = player.Id,
					Name = player.FullName,
					TeamId = stats.TeamId,
					Position = player.DisplayPosition,
					Stats = stats,
				};
			})
			.ToList();
	}

	public IReadOnlyList<PitchingLeaderRow> GetPitchingLeaders(int take = 15)
	{
		CareerState state = _store.Load();
		var players = state.Players.ToDictionary(p => p.Id);
		return state.PitchingStats
			.Where(s => s.OutsRecorded >= 3)
			.OrderBy(s => s.Era)
			.ThenByDescending(s => s.Strikeouts)
			.Take(take)
			.Select(stats =>
			{
				Player player = players[stats.PlayerId];
				return new PitchingLeaderRow
				{
					PlayerId = player.Id,
					Name = player.FullName,
					TeamId = stats.TeamId,
					Position = player.DisplayPosition,
					Stats = stats,
				};
			})
			.ToList();
	}

	public IReadOnlyList<NewsStory> GetNews()
	{
		CareerState state = _store.Load();
		return state.News
			.OrderByDescending(story => story.Date)
			.ThenByDescending(story => story.Id)
			.ToList();
	}

	public IReadOnlyList<PlayerTransaction> GetTransactions()
	{
		CareerState state = _store.Load();
		var newestFirst = new List<PlayerTransaction>(state.Transactions.Count);
		for (int i = state.Transactions.Count - 1; i >= 0; i--)
		{
			newestFirst.Add(state.Transactions[i]);
		}

		return newestFirst;
	}

	public void RecordTransaction(PlayerTransaction entry)
	{
		if (string.IsNullOrWhiteSpace(entry.Headline))
		{
			return;
		}

		CareerState state = _store.Load();
		if (!string.IsNullOrWhiteSpace(entry.DedupKey))
		{
			foreach (PlayerTransaction existing in state.Transactions)
			{
				if (string.Equals(existing.DedupKey, entry.DedupKey, StringComparison.Ordinal))
				{
					return;
				}
			}
		}

		state.Transactions.Add(entry);
		_store.ReplaceAll(state);
	}

	public bool TryAssignPlayerLevel(Guid playerId, TeamLevel level, out string? error)
	{
		CareerState state = _store.Load();
		Player? player = state.PlayerById(playerId);
		if (player == null)
		{
			error = "Player not found.";
			return false;
		}

		string orgId = player.IsUnsigned ? state.Career.UserOrganizationId : player.OrganizationId;
		TeamIdentity? dest = null;
		foreach (TeamIdentity team in state.Teams)
		{
			if (team.OrganizationId == orgId && team.Level == level && team.RosterCap > 0)
			{
				dest = team;
				break;
			}
		}

		if (dest == null)
		{
			error = "Destination team not found.";
			return false;
		}

		List<Player> roster = [.. state.ReturningFor(dest.OrganizationId)];
		if (!RosterMovement.TryAssign(player, dest, roster, out error))
		{
			return false;
		}

		_store.ReplaceAll(state);
		return true;
	}

	public bool TryReleasePlayer(Guid playerId, out Player? released)
	{
		CareerState state = _store.Load();
		Player? player = state.PlayerById(playerId);
		if (player == null || player.IsUnsigned)
		{
			released = null;
			return false;
		}

		OrganizationState school = state.OrganizationById(player.OrganizationId);
		RosterMovement.ReleaseToPool(player, school.DistrictId);
		foreach (PlayerAgreement agreement in state.Agreements)
		{
			if (agreement.PlayerId == playerId)
			{
				agreement.YearsRemaining = 0;
			}
		}

		_store.ReplaceAll(state);
		released = player;
		return true;
	}

	public void ReplacePlayers(IReadOnlyList<Player> players)
	{
		CareerState state = _store.Load();
		state.Players = [.. players];
		_store.ReplaceAll(state);
	}

	public void SaveClubhouse(GameWorld world)
	{
		CareerState state = _store.Load();
		state.Players = world.Players;
		state.Lineups = world.Lineups;
		state.Rotations = world.Rotations;
		state.BoxScores = world.BoxScores;
		state.FieldingStats = world.FieldingStats;
		_store.ReplaceAll(state);
	}

	public void MarkNewsRead()
	{
		CareerState state = _store.Load();
		bool dirty = false;
		foreach (NewsStory story in state.News)
		{
			if (!story.Read)
			{
				story.Read = true;
				dirty = true;
			}
		}

		if (dirty)
		{
			_store.ReplaceAll(state);
		}
	}

	public void Dispose() => _store.Dispose();

	private static AdvanceDayResult ToAdvanceResult(CareerTickResult tick) =>
		new()
		{
			PreviousDate = tick.PreviousDate,
			NewDate = tick.NewDate,
			GamesPlayed = tick.Day.GamesPlayed.Select(ToSummary).ToList(),
			StoriesPublished = tick.StoriesPublished,
			TransactionsPosted = tick.Day.Transactions,
			Phase = tick.Phase,
			RolledOver = tick.RolledOver,
		};

	private static GameSummary ToSummary(SimulatedGame game) =>
		new()
		{
			GameId = game.GameId,
			Date = game.Date,
			AwayTeamId = game.AwayTeamId,
			HomeTeamId = game.HomeTeamId,
			AwayScore = game.AwayScore,
			HomeScore = game.HomeScore,
			InningsPlayed = game.InningsPlayed,
			TeamLevel = game.TeamLevel,
			GameType = game.GameType,
			WinningTeamId = game.WinningTeamId,
		};

	private static RecruitingDayResult RunRecruitingDay(CareerState state, DateOnly today)
	{
		RecruitingMarket market = MarketFrom(state);
		ISeededRandom rng = new SeededRandom(SeededRandom.Mix(state.Career.RandomSeed, today.DayNumber));
		RecruitingDayResult result = market.AdvanceDay(
			today,
			state.Career.CurrentSeasonYear,
			state.Career.UserOrganizationId,
			rng);
		WriteMarket(state, market);
		return result;
	}

	private static List<PlayerTransaction> LogSignings(
		CareerState state,
		RecruitingDayResult recruiting,
		DateOnly today)
	{
		var posted = new List<PlayerTransaction>();
		if (recruiting.Signed.Count == 0)
		{
			return posted;
		}

		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (PlayerTransaction existing in state.Transactions)
		{
			if (!string.IsNullOrWhiteSpace(existing.DedupKey))
			{
				seen.Add(existing.DedupKey);
			}
		}

		foreach (PlayerAgreement agreement in recruiting.Signed)
		{
			Player? player = state.PlayerById(agreement.PlayerId);
			if (player == null)
			{
				continue;
			}

			OrganizationState school = state.OrganizationById(agreement.OrganizationId);
			PlayerTransaction entry = PlayerTransaction.FromSigning(player, school.ShortName, school.Id, today);
			if (string.IsNullOrWhiteSpace(entry.DedupKey) || !seen.Add(entry.DedupKey))
			{
				continue;
			}

			posted.Add(entry);
		}

		if (posted.Count > 0)
		{
			state.Transactions.AddRange(posted);
		}

		return posted;
	}

	private static RecruitingMarket MarketFrom(CareerState state) =>
		new(
			state.Organizations,
			state.Players.Where(p => !p.IsUnsigned && state.AgreementFor(p.Id) == null).ToList(),
			state.Players.Where(p => p.IsUnsigned || state.AgreementFor(p.Id) != null).ToList(),
			state.Teams,
			state.Offers,
			state.Agreements,
			state.RecruitingInterest);

	private static void ApplyMarket(CareerState state, RecruitingMarket market)
	{
		foreach (Player prospect in market.Prospects)
		{
			Player? existing = state.PlayerById(prospect.Id);
			if (existing == null)
			{
				continue;
			}

			existing.OrganizationId = prospect.OrganizationId;
			existing.TeamId = prospect.TeamId;
			existing.TeamLevel = prospect.TeamLevel;
			existing.JerseyNumber = prospect.JerseyNumber;
		}

		WriteMarket(state, market);
	}

	private static void ApplyUserSeasonProfile(
		CareerState state,
		ProgramSeasonResult userSeason,
		int? reputation,
		int? baseballBudget)
	{
		OrganizationState org = state.OrganizationById(userSeason.OrganizationId);
		ProgramProfile profile = org.Profile;
		profile.LastSeasonWins = userSeason.RegularWins;
		profile.LastSeasonLosses = userSeason.RegularLosses;
		profile.DistrictFinish = userSeason.DistrictFinish;
		if (!SeasonArchive.IsCommitted(state, userSeason.OrganizationId, userSeason.SeasonYear))
		{
			if (userSeason.WonDistrict)
			{
				profile.DistrictTitles++;
			}

			if (userSeason.WonRegional)
			{
				profile.RegionalTitles++;
			}

			if (userSeason.WonState)
			{
				profile.StateTitles++;
			}
		}

		if (reputation.HasValue)
		{
			profile.Reputation = reputation.Value;
		}

		if (baseballBudget.HasValue)
		{
			profile.BaseballBudget = Math.Max(0, baseballBudget.Value);
		}

		org.RefreshTalentFromReputation();
		profile.TargetExpectation = ProgramReputation.TargetExpectation(profile.Reputation, userSeason.RegularWins);
		profile.MinimumExpectation = profile.TargetExpectation.StepDown();
	}

	private static void ApplyCoachSeason(CareerState state, ProgramSeasonResult userSeason)
	{
		OrganizationState org = state.OrganizationById(userSeason.OrganizationId);
		bool targetMet = SeasonReview.TargetMet(
			org.Profile.TargetExpectation,
			userSeason.Wins,
			userSeason.Losses,
			userSeason.DistrictFinish,
			userSeason.WonDistrict,
			userSeason.WonRegional,
			userSeason.WonState);
		state.Career.AddSeason(
			userSeason.Wins,
			userSeason.Losses,
			userSeason.WonDistrict,
			userSeason.WonRegional,
			userSeason.WonState,
			targetMet);
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

	private static void InductHallOfFame(
		CareerState state,
		ProgramSeasonResult userSeason,
		ProgramHallOfFameInductee? userInductee)
	{
		int year = userSeason.SeasonYear;
		foreach (OrganizationState org in state.Organizations)
		{
			if (LeagueCatalog.IsUnsignedOrganization(org.Id))
			{
				continue;
			}

			ProgramHallOfFameInductee? inductee = org.Id == userSeason.OrganizationId && userInductee != null
				? userInductee
				: SelectSeniorInductee(state, org, year);
			if (inductee == null)
			{
				continue;
			}

			inductee.OrganizationId = org.Id;
			inductee.InductedYear = year;
			ProgramHallOfFame.ReplaceYear(org.HallOfFame, inductee);
		}
	}

	private static ProgramHallOfFameInductee? SelectSeniorInductee(
		CareerState state,
		OrganizationState org,
		int year)
	{
		Player? best = null;
		foreach (Player player in state.Players)
		{
			if (player.OrganizationId != org.Id
				|| player.TeamLevel != TeamLevel.Varsity
				|| player.Grade != PlayerGrade.Senior)
			{
				continue;
			}

			if (best == null || player.Overall > best.Overall)
			{
				best = player;
			}
		}

		if (best == null)
		{
			foreach (Player player in state.Players)
			{
				if (player.OrganizationId != org.Id || player.TeamLevel != TeamLevel.Varsity)
				{
					continue;
				}

				if (best == null || player.Overall > best.Overall)
				{
					best = player;
				}
			}
		}

		if (best == null)
		{
			return null;
		}

		HashSet<string> varsityIds = state.Teams
			.Where(team => team.OrganizationId == org.Id && team.Level == TeamLevel.Varsity)
			.Select(team => team.Id)
			.ToHashSet(StringComparer.Ordinal);
		int years = ProgramHallOfFame.YearsFromGrade(best.Grade, best.GeneratedSeasonYear, year);
		bool pitcher = best.IsPitcher;
		string careerLine = pitcher
			? PitchingPlaque(state, best.Id, varsityIds)
			: BattingPlaque(state, best.Id, varsityIds);
		var honors = new List<string> { "All-District" };
		ProgramSeasonResult? season = org.SeasonHistory.Find(row => row.SeasonYear == year);
		if (season?.WonState == true)
		{
			honors.Insert(0, "State Champion");
		}
		else if (season?.WonDistrict == true)
		{
			honors.Insert(0, "District Champion");
		}

		return new ProgramHallOfFameInductee
		{
			OrganizationId = org.Id,
			InductedYear = year,
			PlayerId = best.Id,
			PlayerName = best.FullName,
			Position = best.PrimaryPosition.ToCode(),
			FirstSeasonYear = ProgramHallOfFame.FirstSeasonYear(year, years),
			LastSeasonYear = year,
			CareerLine = careerLine,
			Honors = ProgramHallOfFame.JoinHonors(honors),
		};
	}

	private static string BattingPlaque(CareerState state, Guid playerId, IReadOnlySet<string> varsityIds)
	{
		int hits = 0;
		int atBats = 0;
		foreach (BattingSeasonStats row in state.BattingStats)
		{
			if (row.PlayerId == playerId && varsityIds.Contains(row.TeamId))
			{
				hits += row.Hits;
				atBats += row.AtBats;
			}
		}

		double average = atBats == 0 ? 0.280 : (double)hits / atBats;
		return ProgramHallOfFame.BattingLine(average);
	}

	private static string PitchingPlaque(CareerState state, Guid playerId, IReadOnlySet<string> varsityIds)
	{
		int wins = 0;
		int losses = 0;
		int earned = 0;
		int outs = 0;
		foreach (PitchingSeasonStats row in state.PitchingStats)
		{
			if (row.PlayerId == playerId && varsityIds.Contains(row.TeamId))
			{
				wins += row.Wins;
				losses += row.Losses;
				earned += row.EarnedRuns;
				outs += row.OutsRecorded;
			}
		}

		double era = outs == 0 ? 2.80 : earned * 27.0 / outs;
		return ProgramHallOfFame.PitchingLine(Math.Max(wins, 8), Math.Max(losses, 2), era);
	}

	private static void BindToSchool(
		CareerState state,
		OrganizationState dest,
		bool alreadySigned,
		CoachContract contract)
	{
		if (alreadySigned)
		{
			OrganizationState? leaving = state.Organizations.FirstOrDefault(item =>
				item.Id == state.Career.UserOrganizationId);
			if (leaving != null && leaving.Id != dest.Id)
			{
				leaving.Profile.HasOpening = true;
				leaving.Profile.VacancyReason = VacancyReason.Resignation;
			}
		}

		int startYear = alreadySigned
			? state.Career.NextStintStartYear()
			: state.Career.CurrentSeasonYear;
		state.Career.UserOrganizationId = dest.Id;
		state.Career.OpenStint(dest.Id, dest.DisplayName, startYear);
		state.Career.Contract = contract;
		state.Career.JobOffers.Clear();
		dest.Profile.HasOpening = false;
	}

	private static OrganizationState RequireOpening(CareerState state, string organizationId)
	{
		OrganizationState org = state.Organizations.FirstOrDefault(item => item.Id == organizationId)
			?? throw new KeyNotFoundException($"Unknown organization '{organizationId}'.");
		if (!org.Profile.HasOpening)
		{
			throw new InvalidOperationException($"'{org.DisplayName}' is not posting a vacancy.");
		}

		return org;
	}

	private static void CloseAllOpenings(CareerState state)
	{
		foreach (OrganizationState org in state.Organizations)
		{
			org.Profile.HasOpening = false;
		}
	}

	private static SignContractResult ToSignResult(OrganizationState org) => new()
	{
		OrganizationId = org.Id,
		Name = org.Name,
		ShortName = org.ShortName,
		DisplayName = org.DisplayName,
	};

	private static void WriteMarket(CareerState state, RecruitingMarket market)
	{
		state.Offers = market.Offers.ToList();
		state.Agreements = market.Agreements.ToList();
		state.RecruitingInterest = market.Interest.ToList();
	}

	private static void ApplyResult(CareerState state, ScheduledGame scheduled, GameSimulationResult result)
	{
		scheduled.Status = GameStatus.Completed;
		scheduled.HomeScore = result.HomeScore;
		scheduled.AwayScore = result.AwayScore;
		scheduled.InningsPlayed = result.InningsPlayed;
		scheduled.WinningTeamId = result.WinningTeamId;
		scheduled.LosingTeamId = result.LosingTeamId;
		scheduled.WinningPitcherId = result.WinningPitcherId;
		scheduled.LosingPitcherId = result.LosingPitcherId;

		bool district = scheduled.GameType == GameType.District;
		state.RecordFor(scheduled.HomeTeamId).ApplyGame(
			result.HomeScore,
			result.AwayScore,
			result.HomeScore > result.AwayScore,
			district);
		state.RecordFor(scheduled.AwayTeamId).ApplyGame(
			result.AwayScore,
			result.HomeScore,
			result.AwayScore > result.HomeScore,
			district);

		foreach (PlayerGameBatting line in result.Batting)
		{
			Player player = state.Players.First(p => p.Id == line.PlayerId);
			BattingSeasonStats stats = state.BattingFor(player.Id, player.TeamId);
			stats.Games++;
			stats.PlateAppearances += line.PlateAppearances;
			stats.AtBats += line.AtBats;
			stats.Runs += line.Runs;
			stats.Hits += line.Hits;
			stats.Doubles += line.Doubles;
			stats.Triples += line.Triples;
			stats.HomeRuns += line.HomeRuns;
			stats.RunsBattedIn += line.RunsBattedIn;
			stats.Walks += line.Walks;
			stats.Strikeouts += line.Strikeouts;
		}

		foreach (PlayerGamePitching line in result.Pitching)
		{
			Player player = state.Players.First(p => p.Id == line.PlayerId);
			PitchingSeasonStats stats = state.PitchingFor(player.Id, player.TeamId);
			stats.Games++;
			stats.GamesStarted += line.GamesStarted;
			stats.OutsRecorded += line.OutsRecorded;
			stats.HitsAllowed += line.HitsAllowed;
			stats.RunsAllowed += line.RunsAllowed;
			stats.EarnedRuns += line.EarnedRuns;
			stats.Walks += line.Walks;
			stats.Strikeouts += line.Strikeouts;
			stats.HomeRunsAllowed += line.HomeRunsAllowed;
			if (line.Win)
			{
				stats.Wins++;
			}

			if (line.Loss)
			{
				stats.Losses++;
			}
		}
	}
}
