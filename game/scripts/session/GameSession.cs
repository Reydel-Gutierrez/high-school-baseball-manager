using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Godot;
using HSBM.Application;
using HSBM.Domain;
using HSBM.Persistence;
using HSBM.Simulation;

/// <summary>
/// Autoload that holds the coach's organization and which of its teams is
/// currently active (Varsity or JV). Screens should bind to ActiveTeam so
/// roster, schedule, and other pages all populate from the same selection.
/// </summary>
[GlobalClass]
public partial class GameSession : Node
{
	public const int DefaultSeasonYear = 2026;

	public DateOnly CurrentDate { get; private set; } = SeasonCalendar.OpenPlayStart(DefaultSeasonYear);

	public string LastRecruitingError { get; private set; } = "";

	public string CurrentDateLabel =>
		CurrentDate.ToString("MMM d", CultureInfo.InvariantCulture).ToUpperInvariant();

	public static GameSession Current { get; private set; } = null!;

	private int _seasonYear = DefaultSeasonYear;

	public static int SeasonYear => Current?._seasonYear ?? DefaultSeasonYear;

	public OrganizationIdentity Organization { get; private set; } = null!;
	public TeamIdentity VarsityTeam { get; private set; } = null!;
	public TeamIdentity JuniorVarsityTeam { get; private set; } = null!;
	public TeamIdentity ActiveTeam { get; private set; } = null!;

	/// <summary>
	/// 1-based year on the current deal (1/4 during the first season).
	/// </summary>
	public int ContractYear { get; private set; } = 1;

	public int ContractLength { get; private set; } = 4;

	/// <summary>
	/// Starts at Average (62). Later seasons raise or lower this from
	/// met and missed board goals; the band and job security are derived.
	/// </summary>
	private int _boardSatisfactionScore = SeasonReview.OpeningSatisfaction(HSBM.Domain.BoardSatisfaction.Average);

	public int BoardSatisfactionScore => _boardSatisfactionScore;

	public BoardSatisfaction BoardSatisfaction => SeasonReview.ToSatisfaction(_boardSatisfactionScore);

	public JobSecurity JobSecurity => BoardSatisfaction.ToJobSecurity();

	private readonly List<PlayerTransaction> _transactions = new();
	private readonly List<ScoutTarget> _scoutTargets = new();
	private TryoutCamp _tryouts = null!;
	private FinalRosterBoard _rosterBoard = null!;
	private OpenPlayerPool _openPool = null!;
	private DistrictRecruitingSession _recruiting = null!;
	private readonly List<OrganizationState> _leaguePrograms = new();

	public IReadOnlyList<PlayerTransaction> Transactions => _transactions;

	public IReadOnlyList<ScoutTarget> ScoutTargets => _scoutTargets;

	public TryoutCamp Tryouts => _tryouts;

	public FinalRosterBoard RosterBoard => _rosterBoard;

	public OpenPlayerPool OpenPool => _openPool;

	public DistrictRecruitingSession Recruiting => _recruiting;

	public RecruitingMarket RecruitingMarket => _recruiting.Market;

	public DistrictOpenPlay OpenPlay => _recruiting.OpenPlay;

	public string CatalogOrganizationId =>
		Organization.Id is "org-cascade-regional" or "cascade"
			? LeagueCatalog.DefaultUserOrganizationId
			: Organization.Id;

	public IReadOnlyList<CalendarContest> ContestsOn(DateOnly date)
	{
		if (World != null && ActiveTeam != null)
		{
			return ToContests(CareerCalendar.GamesOn(World, date, ActiveTeam.Id));
		}

		return ClubhouseCalendar.On(date);
	}

	public IReadOnlyList<CalendarContest> ContestsInMonth(int year, int month)
	{
		if (World != null && ActiveTeam != null)
		{
			return ToContests(CareerCalendar.GamesInMonth(World, year, month, ActiveTeam.Id));
		}

		return ClubhouseCalendar.InMonth(year, month);
	}

	public IReadOnlyList<CalendarContest> SeasonContests(TeamLevel level)
	{
		if (World != null && ActiveTeam != null)
		{
			string teamId = GetTeam(level).Id;
			return ToContests(CareerCalendar.SeasonGames(World, teamId));
		}

		var matches = new List<CalendarContest>();
		foreach (CalendarContest contest in ClubhouseCalendar.Contests)
		{
			if (contest.Level == level)
			{
				matches.Add(contest);
			}
		}

		return matches;
	}

	private List<CalendarContest> ToContests(IReadOnlyList<ScheduledGame> games)
	{
		var matches = new List<CalendarContest>(games.Count);
		if (World == null)
		{
			return matches;
		}

		string userTeam = ActiveTeam.Id;
		foreach (ScheduledGame game in games)
		{
			matches.Add(ClubhouseCalendar.FromScheduled(game, userTeam, World));
		}

		return matches;
	}


	public event Action<TeamIdentity>? ActiveTeamChanged;
	public event Action? TransactionsChanged;
	public event Action? ScoutTargetsChanged;
	public event Action? CareerChanged;
	public event Action? ContractStateChanged;
	public event Action? TryoutsChanged;
	public event Action? RosterBoardChanged;

	public string CoachName { get; private set; } = DistrictHubData.UserCoachName;

	public CoachProfile Coach { get; private set; } = PrototypeCoach();

	public int CoachReputationScore { get; private set; } = 48;

	public int CareerWins { get; private set; } = 35;

	public int CareerLosses { get; private set; } = 21;

	public int CareerDistrictTitles { get; private set; } = 1;

	public int CareerRegionalTitles { get; private set; }

	public int CareerStateTitles { get; private set; }

	public int SeasonsCoached { get; private set; } = 1;

	public IReadOnlyList<CoachStint> CareerStints { get; private set; } = PrototypeStints();

	public int SeasonNumber { get; private set; } = 1;

	public int Wins { get; private set; } = 18;

	public int Losses { get; private set; } = 4;

	public string SeasonStatus { get; private set; } = "Regular Season";

	/// <summary>
	/// Live careers take titles from district standings and the
	/// postseason bracket. Prototype mode keeps them on so celebration
	/// screens can be previewed.
	/// </summary>
	public bool WonDistrict { get; private set; } = true;

	public bool WonRegional { get; private set; } = true;

	public bool WonState { get; private set; } = true;

	private readonly HashSet<ChampionshipTitle> _acknowledgedCelebrations = new();
	private readonly HashSet<AwardScope> _acknowledgedAwards = new();
	private bool _seasonReviewAcknowledged;
	private bool _hallOfFameAcknowledged;
	private bool _jobMarketAcknowledged;
	private bool _jobOfferSeen;
	private readonly HashSet<string> _readMessages = new();
	private readonly List<NewsStory> _news = new();
	private bool _seasonRolloverAcknowledged;
	private bool _advancingDay;

	public bool SeasonReviewAcknowledged => _seasonReviewAcknowledged;

	public bool SeasonRolloverAcknowledged => _seasonRolloverAcknowledged;

	public bool HallOfFameAcknowledged => _hallOfFameAcknowledged;

	public bool JobMarketAcknowledged => _jobMarketAcknowledged;

	public bool JobOfferSeen => _jobOfferSeen;

	public IReadOnlyList<NewsStory> News => _news;

	public int UnreadNewsCount
	{
		get
		{
			int count = 0;
			foreach (NewsStory story in _news)
			{
				if (!story.Read)
				{
					count++;
				}
			}

			return count;
		}
	}

	public string? ActiveSaveId { get; private set; }

	public int WorldSeed { get; private set; }

	public CompetitionLevel WorldCompetitionLevel { get; private set; } = CompetitionLevel.HighSchool;

	public GameWorld? World { get; private set; }

	public bool HasSignedContract { get; private set; } = true;

	public IReadOnlyList<JobListing> AvailableJobs { get; private set; } = [];

	public IReadOnlyList<CoachingJobOffer> PendingJobOffers { get; private set; } = [];

	public int AnnualSalary { get; private set; } = 8_000;

	public bool LicenseNoticeDismissed { get; private set; }

	public void DismissLicenseNotice() => LicenseNoticeDismissed = true;

	public string RecordLabel => $"{Wins}\u2013{Losses}";

	public string RecordLabelFor(TeamLevel level)
	{
		TeamSeasonRecord? record = RecordFor(level);
		if (record != null)
		{
			return $"{record.Wins}\u2013{record.Losses}";
		}

		if (level == TeamLevel.Varsity)
		{
			return RecordLabel;
		}

		HubTeam team = DistrictHubData.GetTeam(CatalogOrganizationId);
		return $"{team.Wins(level)}\u2013{team.Losses(level)}";
	}

	public string DistrictPlaceLabel(TeamLevel level)
	{
		if (World != null && GetTeam(level) != null)
		{
			StandingEntry? place = LeagueStandings.FindTeam(
				LeagueStandings.FromWorld(World, SeasonYear, level),
				GetTeam(level).Id);
			if (place != null)
			{
				return Ordinal.Format(place.Rank).ToUpperInvariant();
			}
		}

		return Ordinal.Format(SchoolProfiles.For(CatalogOrganizationId, level, this).DistrictPlace)
			.ToUpperInvariant();
	}

	public TeamSeasonRecord? RecordFor(TeamLevel level)
	{
		if (World == null || GetTeam(level) == null)
		{
			return null;
		}

		string teamId = GetTeam(level).Id;
		foreach (TeamSeasonRecord record in World.TeamRecords)
		{
			if (record.TeamId == teamId && record.SeasonYear == SeasonYear)
			{
				return record;
			}
		}

		return null;
	}

	public CalendarContest? NextContest(TeamLevel level)
	{
		CalendarContest? today = null;
		foreach (CalendarContest game in ContestsOn(CurrentDate))
		{
			if (game.Level == level && !game.Completed)
			{
				return game;
			}

			today ??= game.Level == level ? game : today;
		}

		foreach (CalendarContest game in SeasonContests(level))
		{
			if (game.Level == level && !game.Completed && game.Date >= CurrentDate)
			{
				return game;
			}
		}

		return today;
	}

	public int DisplayCareerWins =>
		CareerWins + (SeasonReviewAcknowledged || !HasSignedContract ? 0 : Wins);

	public int DisplayCareerLosses =>
		CareerLosses + (SeasonReviewAcknowledged || !HasSignedContract ? 0 : Losses);

	public int DisplayCareerGames => DisplayCareerWins + DisplayCareerLosses;

	public string CareerRecordLabel => $"{DisplayCareerWins}\u2013{DisplayCareerLosses}";

	public string CareerWinPctLabel => DisplayCareerGames == 0
		? "\u2014"
		: (DisplayCareerWins / (double)DisplayCareerGames).ToString(".000", CultureInfo.InvariantCulture);

	public string SeasonOrdinalLabel => SeasonsCoached <= 0
		? "SEEKING A SEAT"
		: $"{Ordinal.Format(SeasonsCoached)} SEASON";

	public static string CareerDatabasePath(string slotId) =>
		CareerLibrary.DatabasePath(slotId);

	public override void _EnterTree()
	{
		Current = this;
		LoadPrototypeProgram();
	}

	public TeamIdentity GetTeam(TeamLevel level) =>
		level == TeamLevel.JuniorVarsity ? JuniorVarsityTeam : VarsityTeam;

	public void SetActiveLevel(TeamLevel level)
	{
		TeamIdentity next = GetTeam(level);
		if (ActiveTeam != null && ActiveTeam.Id == next.Id)
		{
			return;
		}

		ActiveTeam = next;
		ActiveTeamChanged?.Invoke(ActiveTeam);
	}

	public void ToggleActiveLevel()
	{
		SetActiveLevel(ActiveTeam.Level == TeamLevel.Varsity
			? TeamLevel.JuniorVarsity
			: TeamLevel.Varsity);
	}

	public CareerSaveSlot CaptureCareerSlot()
	{
		CareerSaveSlot? existing = string.IsNullOrEmpty(ActiveSaveId) ? null : CareerLibrary.Find(ActiveSaveId);
		return new CareerSaveSlot
		{
			Id = ActiveSaveId ?? Guid.NewGuid().ToString("N"),
			CoachName = CoachName,
			FileLabel = existing?.FileLabel ?? "",
			SchoolName = HasSignedContract ? ProgramDisplayName : "Unsigned",
			SchoolShortName = HasSignedContract ? Organization.ShortName : "",
			OrganizationId = HasSignedContract ? Organization.Id : "",
			SeasonNumber = SeasonNumber,
			SeasonYear = SeasonYear,
			Wins = HasSignedContract ? Wins : 0,
			Losses = HasSignedContract ? Losses : 0,
			WorldSeed = WorldSeed,
			CompetitionLevel = WorldCompetitionLevel.ToLabel(),
			Status = HasSignedContract ? SeasonStatus : "Job Market",
			LastPlayedUtc = DateTime.UtcNow,
		};
	}

	public CareerSaveSlot SaveCareer()
	{
		CareerSaveSlot saved = CareerLibrary.Upsert(CaptureCareerSlot());
		ActiveSaveId = saved.Id;
		return saved;
	}

	public CareerSaveSlot SaveCareerAs(string fileLabel)
	{
		CareerSaveSlot current = CaptureCareerSlot();
		if (!string.IsNullOrEmpty(ActiveSaveId))
		{
			CareerLibrary.Upsert(current);
		}

		string newId = Guid.NewGuid().ToString("N");
		if (!string.IsNullOrEmpty(current.Id))
		{
			CareerLibrary.CopyDatabase(current.Id, newId);
		}

		var copy = new CareerSaveSlot
		{
			Id = newId,
			CoachName = current.CoachName,
			FileLabel = fileLabel.Trim(),
			SchoolName = current.SchoolName,
			SchoolShortName = current.SchoolShortName,
			OrganizationId = current.OrganizationId,
			SeasonNumber = current.SeasonNumber,
			SeasonYear = current.SeasonYear,
			Wins = current.Wins,
			Losses = current.Losses,
			WorldSeed = current.WorldSeed,
			CompetitionLevel = current.CompetitionLevel,
			Status = current.Status,
		};
		ActiveSaveId = newId;
		return CareerLibrary.Upsert(copy);
	}

	public bool TryAutosave(params AutosaveTrigger[] triggers)
	{
		if (triggers.Length == 0 || !GameSettings.Autosave.ShouldSaveAny(triggers))
		{
			return false;
		}

		SaveCareer();
		return true;
	}

	public void ApplyCareerSlot(CareerSaveSlot slot, bool announce = true)
	{
		if (ActiveSaveId != slot.Id)
		{
			LicenseNoticeDismissed = false;
		}

		ActiveSaveId = slot.Id;
		CoachName = slot.CoachName;
		SeasonNumber = slot.SeasonNumber;
		SeasonsCoached = slot.SeasonNumber;
		Wins = slot.Wins;
		Losses = slot.Losses;
		WorldSeed = slot.WorldSeed;
		SeasonStatus = slot.Status;
		string schoolName = slot.SchoolName.Contains("High School", StringComparison.OrdinalIgnoreCase)
			? slot.SchoolName
			: $"{slot.SchoolName} High School";
		if (!string.IsNullOrEmpty(slot.OrganizationId))
		{
			Organization = new OrganizationIdentity(
				slot.OrganizationId,
				schoolName,
				string.IsNullOrEmpty(slot.SchoolShortName) ? slot.SchoolName : slot.SchoolShortName,
				Organization.RosterCap);
		}

		BindGeneratedCareer(slot.Id);
		if (HasCareerDatabase)
		{
			ClearChampionshipFlags();
		}
		else
		{
			ResetChampionshipFlags();
		}
		if (announce)
		{
			CareerChanged?.Invoke();
			ActiveTeamChanged?.Invoke(ActiveTeam);
		}
	}

	public void SetCoachName(string name)
	{
		string trimmed = name.Trim();
		if (string.IsNullOrEmpty(trimmed) || CoachName == trimmed)
		{
			return;
		}

		CoachName = trimmed;
		string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 1)
		{
			Coach.FirstName = parts[0];
		}
		else if (parts.Length > 1)
		{
			Coach.FirstName = parts[0];
			Coach.LastName = parts[^1];
		}

		CareerChanged?.Invoke();
	}

	public void ClearActiveSave()
	{
		ActiveSaveId = null;
		CareerLibrary.ClearActive();
	}

	public void ResetToPrototypeCareer()
	{
		ActiveSaveId = null;
		CoachName = DistrictHubData.UserCoachName;
		Coach = PrototypeCoach();
		CoachReputationScore = 48;
		CareerWins = 35;
		CareerLosses = 21;
		CareerDistrictTitles = 1;
		CareerRegionalTitles = 0;
		CareerStateTitles = 0;
		SeasonsCoached = 1;
		CareerStints = PrototypeStints();
		SeasonNumber = 1;
		Wins = 18;
		Losses = 4;
		SeasonStatus = "Regular Season";
		Organization = new OrganizationIdentity(
			"org-cascade-regional",
			"Cascade Regional High School",
			"Cascade",
			40);
		CareerLibrary.ClearActive();
		HasSignedContract = true;
		AvailableJobs = [];
		PendingJobOffers = [];
		AnnualSalary = 8_000;
		ContractYear = 1;
		ContractLength = 4;
		LicenseNoticeDismissed = true;
		_boardSatisfactionScore = SeasonReview.OpeningSatisfaction(HSBM.Domain.BoardSatisfaction.Average);
		ResetChampionshipFlags();
		_news.Clear();
		RefreshNews();
		CareerChanged?.Invoke();
		ContractStateChanged?.Invoke();
		ActiveTeamChanged?.Invoke(ActiveTeam);
	}

	/// <summary>
	/// Prototype Cascade program. Replace with persisted organization + team
	/// records when career/save data is wired up; keep ActiveTeam as the
	/// single source of truth for UI.
	/// TODO(career-backend): Load from HSBM.Application CareerService / SQLite
	/// save instead of this prototype. Do not regenerate rosters on load.
	/// </summary>
	private void LoadPrototypeProgram()
	{
		Organization = new OrganizationIdentity(
			"org-cascade-regional",
			"Cascade Regional High School",
			"Cascade",
			40);

		VarsityTeam = new TeamIdentity(
			"team-cascade-varsity",
			Organization.Id,
			TeamLevel.Varsity,
			20);

		JuniorVarsityTeam = new TeamIdentity(
			"team-cascade-jv",
			Organization.Id,
			TeamLevel.JuniorVarsity,
			20);

		ActiveTeam = VarsityTeam;
		SeedPrototypeTransactions();
		_rosterBoard = ProgramRosterFactory.CreateReturningBoard();
		_tryouts = TryoutCampGenerator.Generate(SeasonYear, new SeededRandom(SeasonYear * 37 + 19));
		List<Player> userReturning = ProgramRosterFactory.ToPlayers(
			_rosterBoard,
			LeagueCatalog.DefaultUserOrganizationId,
			VarsityTeam,
			JuniorVarsityTeam);
		_recruiting = DistrictRecruitingFactory.Create(
			LeagueCatalog.Standard,
			LeagueCatalog.DefaultUserOrganizationId,
			SeasonYear,
			new SeededRandom(SeasonYear * 37 + 41),
			userReturning);
		CurrentDate = SeasonCalendar.OpenPlayStart(SeasonYear);
		_openPool = new OpenPlayerPool();
		SeedLeaguePrograms(null);
		ResetChampionshipFlags();
		RefreshNews();
	}

	public bool Won(ChampionshipTitle title) =>
		title switch
		{
			ChampionshipTitle.District => WonDistrict,
			ChampionshipTitle.Regional => WonRegional,
			ChampionshipTitle.State => WonState,
			_ => false,
		};

	public bool ChampionshipAcknowledged(ChampionshipTitle title) =>
		_acknowledgedCelebrations.Contains(title);

	public void AcknowledgeChampionship(ChampionshipTitle title)
	{
		if (SeasonCalendar.ChampionshipOn(SeasonYear, CurrentDate) == title)
		{
			_acknowledgedCelebrations.Add(title);
			SeasonStatus = title switch
			{
				ChampionshipTitle.District => "District Champions",
				ChampionshipTitle.Regional => "Regional Champions",
				ChampionshipTitle.State => "State Champions",
				_ => SeasonStatus,
			};
			TryAutosave(AutosaveTrigger.PostseasonRound);
		}
	}

	public bool AwardsAcknowledged(AwardScope scope) =>
		_acknowledgedAwards.Contains(scope);

	public void AcknowledgeAwards(AwardScope scope) =>
		_acknowledgedAwards.Add(scope);

	public void AcknowledgeHallOfFame() => _hallOfFameAcknowledged = true;

	public void AcknowledgeJobMarket() => _jobMarketAcknowledged = true;

	public void AcknowledgeJobOffer() => _jobOfferSeen = true;

	public void AcknowledgeSeasonRollover() => _seasonRolloverAcknowledged = true;

	public bool MessageRead(string id) =>
		!string.IsNullOrEmpty(id) && _readMessages.Contains(id);

	public void AcknowledgeMessage(string id)
	{
		if (string.IsNullOrEmpty(id) || !_readMessages.Add(id))
		{
			return;
		}

		CareerChanged?.Invoke();
	}

	public void ApplySeasonReview(SeasonReviewSheet sheet)
	{
		if (_seasonReviewAcknowledged)
		{
			return;
		}

		_seasonReviewAcknowledged = true;
		_boardSatisfactionScore = sheet.SatisfactionAfter;
		OrganizationState? school = RecruitingSchool(CatalogOrganizationId);
		if (school != null)
		{
			ProgramProfile profile = school.Profile;
			profile.Reputation = sheet.ReputationAfter;
			profile.LastSeasonWins = Wins;
			profile.LastSeasonLosses = Losses;
			profile.DistrictFinish = sheet.DistrictPlace;
			profile.BaseballBudget = Math.Max(0, profile.BaseballBudget + sheet.FinanceDelta);
			if (WonDistrict)
			{
				profile.DistrictTitles++;
			}

			if (WonRegional)
			{
				profile.RegionalTitles++;
			}

			if (WonState)
			{
				profile.StateTitles++;
			}

			ProgramSeasonResult season = new()
			{
				OrganizationId = school.Id,
				SeasonYear = sheet.SeasonYear,
				Wins = Wins,
				Losses = Losses,
				RegularWins = Wins,
				RegularLosses = Losses,
				DistrictFinish = sheet.DistrictPlace,
				Postseason = ProgramPostseason.HistoryLabel(
					WonDistrict,
					WonRegional,
					WonState,
					sheet.DistrictPlace),
				WonDistrict = WonDistrict,
				WonRegional = WonRegional,
				WonState = WonState,
			};
			ProgramRecordBook.ReplaceYear(school.SeasonHistory, season);
			ProgramHallOfFameInductee? inductee = HallOfFameDesk.Select(this);
			if (inductee != null)
			{
				ProgramHallOfFame.ReplaceYear(school.HallOfFame, inductee);
			}

			ArchiveCareerSeason(season, sheet.ReputationAfter, school.Profile.BaseballBudget, inductee);
		}

		SeasonStatus = "Offseason";
		TryAutosave(AutosaveTrigger.OffseasonTransition);
		CareerChanged?.Invoke();
		ContractStateChanged?.Invoke();
	}

	private void ClearChampionshipFlags()
	{
		WonDistrict = false;
		WonRegional = false;
		WonState = false;
		_acknowledgedCelebrations.Clear();
		_acknowledgedAwards.Clear();
		_seasonReviewAcknowledged = false;
		_hallOfFameAcknowledged = false;
		_jobMarketAcknowledged = false;
		_jobOfferSeen = false;
		_seasonRolloverAcknowledged = false;
	}

	private void ResetChampionshipFlags()
	{
		WonDistrict = true;
		WonRegional = true;
		WonState = true;
		_acknowledgedCelebrations.Clear();
		_acknowledgedAwards.Clear();
		_seasonReviewAcknowledged = false;
		_hallOfFameAcknowledged = false;
		_jobMarketAcknowledged = false;
		_jobOfferSeen = false;
		_seasonRolloverAcknowledged = false;
	}

	private bool HasCareerDatabase =>
		!string.IsNullOrEmpty(ActiveSaveId) && File.Exists(CareerDatabasePath(ActiveSaveId));

	public bool TryPlaceRecruitingOffer(Guid playerId, int annualSponsorship, int? termYears = null)
	{
		LastRecruitingError = "";
		Player? player = FindProspect(playerId);
		if (player == null)
		{
			LastRecruitingError = "That player is no longer available.";
			return false;
		}

		int term = termYears ?? player.RemainingEligibility;
		bool placed = _recruiting.Market.TryPlaceOffer(
			CatalogOrganizationId,
			playerId,
			annualSponsorship,
			term,
			CurrentDate,
			SeasonYear,
			out string error);
		if (!placed)
		{
			LastRecruitingError = error;
			return false;
		}

		PersistRecruiting();
		NotifyRosterChanged();
		return true;
	}

	public bool TryWithdrawRecruitingOffer(Guid playerId)
	{
		LastRecruitingError = "";
		bool withdrawn = _recruiting.Market.TryWithdraw(CatalogOrganizationId, playerId);
		if (withdrawn)
		{
			PersistRecruiting();
			NotifyRosterChanged();
		}

		return withdrawn;
	}

	public bool AdvanceSigningDay() => AdvanceClubhouseDay();

	public bool ContinueToSigningWeek()
	{
		DateOnly signing = SeasonCalendar.SigningWeek(SeasonYear).Start;
		if (CurrentDate >= signing)
		{
			return AdvanceClubhouseDay();
		}

		CurrentDate = signing;
		if (HasCareerDatabase)
		{
			try
			{
				using SqliteCareerStore store = new(CareerDatabasePath(ActiveSaveId!));
				using CareerService service = CareerService.Create(store);
				service.SetCurrentDate(CurrentDate);
			}
			catch (Exception ex)
			{
				GD.PushError($"Failed to skip to Signing Week: {ex.Message}");
			}
		}

		NotifyRosterChanged();
		RefreshNews();
		CareerChanged?.Invoke();
		return true;
	}

	public SimulationRun SimulateDays(int days)
	{
		if (days < 1)
		{
			return new SimulationRun(0, null);
		}

		var signals = new DayAdvanceSignals { StartedOn = CurrentDate };
		int advanced = 0;
		for (int i = 0; i < days; i++)
		{
			AdvanceDayInterrupt? interrupt = SimulationDesk.InterruptOn(this);
			if (interrupt != null)
			{
				AutosaveFrom(signals, advanced);
				return FinishRun(advanced, interrupt, signals);
			}

			if (SimulationDesk.HasCeremony(this))
			{
				AutosaveFrom(signals, advanced);
				return FinishRun(advanced, null, signals);
			}

			if (!AdvanceCalendarDay(signals))
			{
				AutosaveFrom(signals, advanced);
				return FinishRun(advanced, null, signals);
			}

			advanced++;
			interrupt = SimulationDesk.InterruptOn(this);
			if (interrupt != null)
			{
				AutosaveFrom(signals, advanced);
				return FinishRun(advanced, interrupt, signals);
			}

			if (SimulationDesk.HasCeremony(this))
			{
				AutosaveFrom(signals, advanced);
				return FinishRun(advanced, null, signals);
			}
		}

		AutosaveFrom(signals, advanced);
		return FinishRun(advanced, null, signals);
	}

	public SimulationRun SimulateUntilNextEvent(int maxDays = 120)
	{
		if (maxDays < 1)
		{
			return new SimulationRun(0, null);
		}

		var signals = new DayAdvanceSignals { StartedOn = CurrentDate };
		int advanced = 0;
		for (int i = 0; i < maxDays; i++)
		{
			AdvanceDayInterrupt? interrupt = SimulationDesk.InterruptOn(this);
			if (interrupt != null)
			{
				AutosaveFrom(signals, advanced);
				return FinishRun(advanced, interrupt, signals);
			}

			if (SimulationDesk.HasCeremony(this))
			{
				AutosaveFrom(signals, advanced);
				return FinishRun(advanced, null, signals);
			}

			bool userGame = World != null
				&& ActiveTeam != null
				&& CareerCalendar.HasScheduledGame(World, ActiveTeam.Id, CurrentDate);
			if (userGame || (advanced > 0 && CareerCalendar.IsSimStop(SeasonYear, CurrentDate)))
			{
				AutosaveFrom(signals, advanced);
				return FinishRun(advanced, null, signals);
			}

			if (!AdvanceCalendarDay(signals))
			{
				AutosaveFrom(signals, advanced);
				return FinishRun(advanced, null, signals);
			}

			advanced++;
			interrupt = SimulationDesk.InterruptOn(this);
			if (interrupt != null)
			{
				AutosaveFrom(signals, advanced);
				return FinishRun(advanced, interrupt, signals);
			}

			if (SimulationDesk.HasCeremony(this)
				|| (World != null && ActiveTeam != null && CareerCalendar.HasScheduledGame(World, ActiveTeam.Id, CurrentDate))
				|| CareerCalendar.IsSimStop(SeasonYear, CurrentDate))
			{
				AutosaveFrom(signals, advanced);
				return FinishRun(advanced, null, signals);
			}
		}

		AutosaveFrom(signals, advanced);
		return FinishRun(advanced, null, signals);
	}

	private SimulationRun FinishRun(int advanced, AdvanceDayInterrupt? interrupt, DayAdvanceSignals signals)
	{
		SimulationRecap recap = SimulationDesk.BuildRecap(this, signals, advanced);
		return new SimulationRun(advanced, interrupt, recap);
	}

	public bool AdvanceClubhouseDay()
	{
		return AdvanceCalendarDay();
	}

	private void AutosaveFrom(DayAdvanceSignals signals, int advanced)
	{
		if (advanced <= 0 && !signals.HadGame && !signals.HadMajorTransaction && !signals.HadPostseason)
		{
			return;
		}

		var triggers = new List<AutosaveTrigger>();
		if (signals.HadGame)
		{
			triggers.Add(AutosaveTrigger.Game);
		}

		if (signals.HadMajorTransaction)
		{
			triggers.Add(AutosaveTrigger.MajorTransaction);
		}

		if (advanced >= 7)
		{
			triggers.Add(AutosaveTrigger.WeekAdvance);
		}

		if (signals.HadPostseason)
		{
			triggers.Add(AutosaveTrigger.PostseasonRound);
		}

		if (triggers.Count > 0)
		{
			TryAutosave(triggers.ToArray());
		}
	}

	private bool AdvanceCalendarDay(DayAdvanceSignals? signals = null)
	{
		_advancingDay = true;
		try
		{
			if (HasCareerDatabase)
			{
				return AdvanceSavedDay(signals);
			}

			DateOnly deadline = SeasonCalendar.SigningDeadline(SeasonYear);
			bool signing = SeasonCalendar.SigningWeek(SeasonYear).OccursOn(CurrentDate)
				|| CurrentDate == deadline;
			if (signing)
			{
				RecruitingDayResult result = _recruiting.Market.AdvanceDay(
					CurrentDate,
					SeasonYear,
					CatalogOrganizationId,
					new SeededRandom(SeasonYear * 37 + CurrentDate.DayNumber));
				foreach (PlayerAgreement agreement in result.Signed)
				{
					OnPlayerSigned(agreement);
				}
			}

			if (CurrentDate == deadline)
			{
				foreach (Player leftover in _recruiting.Market.ReleaseUnsignedToPool())
				{
					_openPool.TryAdd(OpenPoolPlayer.FromIncoming(leftover));
				}
			}

			CurrentDate = CurrentDate.AddDays(1);
			NotifyRosterChanged();
			RefreshNews();
			CareerChanged?.Invoke();
			return true;
		}
		finally
		{
			_advancingDay = false;
		}
	}

	private bool AdvanceSavedDay(DayAdvanceSignals? signals)
	{
		try
		{
			HashSet<Guid> signedBefore = UserSignedIds();
			using SqliteCareerStore store = new(CareerDatabasePath(ActiveSaveId!));
			using CareerService service = CareerService.Create(store);
			AdvanceDayResult advanced = service.AdvanceDay();
			CareerState state = service.LoadCareer();
			BindWorld(state);
			BindRecruiting(state);
			CurrentDate = state.Career.CurrentDate;
			SyncChampionshipsFromWorld();
			ApplyPlayedGames(advanced, signals);
			foreach (PlayerAgreement agreement in _recruiting.Market.Agreements)
			{
				if (agreement.OrganizationId == CatalogOrganizationId && signedBefore.Add(agreement.PlayerId))
				{
					OnPlayerSigned(agreement);
				}
			}

			DateOnly deadline = SeasonCalendar.SigningDeadline(SeasonYear);
			if (advanced.PreviousDate >= deadline)
			{
				foreach (Player leftover in _recruiting.Market.ReleaseUnsignedToPool())
				{
					_openPool.TryAdd(OpenPoolPlayer.FromIncoming(leftover));
				}

				service.ApplyRecruiting(_recruiting.Market, CurrentDate, ToRecruitingTargets());
			}

			if (signals != null)
			{
				foreach (PlayerTransaction posted in advanced.TransactionsPosted)
				{
					if (AutosavePreferences.IsMajorTransaction(posted.Kind))
					{
						signals.HadMajorTransaction = true;
						break;
					}
				}
			}

			if (SeasonCalendar.IsPlayoffWindow(SeasonYear, advanced.PreviousDate))
			{
				SeasonStatus = SeasonPhase.Postseason.ToLabel();
			}
			else
			{
				SeasonStatus = CareerCalendar.PhaseOn(SeasonYear, CurrentDate).ToLabel();
			}

			if (advanced.RolledOver)
			{
				ClearChampionshipFlags();
				SeasonStatus = SeasonPhase.SeasonRollover.ToLabel();
			}

			SyncRecordFromWorld();
			NotifyRosterChanged();
			RefreshNews();
			BindTransactions(state);
			CareerChanged?.Invoke();
			return true;
		}
		catch (Exception ex)
		{
			GD.PushError($"Failed to advance recruiting day: {ex.Message}");
			return false;
		}
	}

	private void ApplyPlayedGames(AdvanceDayResult advanced, DayAdvanceSignals? signals)
	{
		if (signals != null)
		{
			foreach (GameSummary game in advanced.GamesPlayed)
			{
				signals.Games.Add(game);
			}

			foreach (PlayerTransaction posted in advanced.TransactionsPosted)
			{
				signals.Transactions.Add(posted);
			}

			if (advanced.RolledOver)
			{
				signals.RolledOver = true;
			}
		}

		if (advanced.GamesPlayed.Count == 0 || VarsityTeam == null)
		{
			SyncRecordFromWorld();
			return;
		}

		string teamId = VarsityTeam.Id;
		foreach (GameSummary game in advanced.GamesPlayed)
		{
			if (signals != null)
			{
				signals.HadGame = true;
				if (game.GameType.IsPostseason())
				{
					signals.HadPostseason = true;
				}
			}

			if (game.TeamLevel != TeamLevel.Varsity || !game.Involves(teamId) || game.GameType.IsExhibition())
			{
				continue;
			}

			if (game.WinningTeamId == teamId)
			{
				Wins++;
			}
			else
			{
				Losses++;
			}
		}

		SyncRecordFromWorld();
	}

	private void SyncRecordFromWorld()
	{
		TeamSeasonRecord? varsity = RecordFor(TeamLevel.Varsity);
		if (varsity == null)
		{
			return;
		}

		Wins = varsity.Wins;
		Losses = varsity.Losses;
	}

	private HashSet<Guid> UserSignedIds()
	{
		var ids = new HashSet<Guid>();
		foreach (PlayerAgreement agreement in _recruiting.Market.Agreements)
		{
			if (agreement.OrganizationId == CatalogOrganizationId)
			{
				ids.Add(agreement.PlayerId);
			}
		}

		return ids;
	}

	private void PersistRecruiting()
	{
		if (!HasCareerDatabase)
		{
			return;
		}

		try
		{
			using SqliteCareerStore store = new(CareerDatabasePath(ActiveSaveId!));
			using CareerService service = CareerService.Create(store);
			service.ApplyRecruiting(_recruiting.Market, CurrentDate, ToRecruitingTargets());
		}
		catch (Exception ex)
		{
			GD.PushError($"Failed to persist recruiting: {ex.Message}");
		}
	}

	private void BindRecruiting(CareerState state)
	{
		_recruiting = DistrictRecruitingFactory.FromCareer(state, state.Career.UserOrganizationId);
		List<Player> returning = [];
		foreach (Player player in state.ReturningFor(state.Career.UserOrganizationId))
		{
			if (!_recruiting.OpenPlay.Contains(player.Id))
			{
				returning.Add(player);
			}
		}

		_rosterBoard = ProgramRosterFactory.FromPlayers(returning, _recruiting.Market, CatalogOrganizationId);
		BindScoutTargets(state.RecruitingTargets);
	}

	private void OnPlayerSigned(PlayerAgreement agreement)
	{
		Player? player = FindProspect(agreement.PlayerId);
		if (player == null)
		{
			return;
		}

		if (agreement.OrganizationId == CatalogOrganizationId)
		{
			RosterAssignment slot = player.TeamLevel == TeamLevel.Varsity
				? RosterAssignment.Varsity
				: RosterAssignment.JuniorVarsity;
			_rosterBoard.TryAdd(ProgramRosterFactory.FromIncoming(player, slot), CurrentDate, SeasonYear);
		}

		if (!HasCareerDatabase)
		{
			RecordTransaction(
				PlayerTransaction.FromSigning(
					player,
					TransactionSchoolName(agreement.OrganizationId),
					agreement.OrganizationId,
					CurrentDate),
				persist: false);
		}
	}

	private void BindTransactions(CareerState state)
	{
		_transactions.Clear();
		for (int i = state.Transactions.Count - 1; i >= 0; i--)
		{
			_transactions.Add(state.Transactions[i]);
		}

		TransactionsChanged?.Invoke();
	}

	private void RecordTransaction(PlayerTransaction entry, bool persist = true)
	{
		_transactions.Insert(0, entry);
		if (persist && HasCareerDatabase)
		{
			try
			{
				using SqliteCareerStore store = new(CareerDatabasePath(ActiveSaveId!));
				using CareerService service = CareerService.Create(store);
				service.RecordTransaction(entry);
			}
			catch (Exception ex)
			{
				GD.PushError($"Failed to persist transaction: {ex.Message}");
			}
		}

		TransactionsChanged?.Invoke();
		if (!_advancingDay && AutosavePreferences.IsMajorTransaction(entry.Kind))
		{
			TryAutosave(AutosaveTrigger.MajorTransaction);
		}
	}

	private string TransactionSchoolName(string organizationId)
	{
		OrganizationState? school = LeagueProgram(organizationId);
		return school != null ? school.ShortName : Organization.ShortName;
	}

	public Player? FindProspect(Guid playerId)
	{
		Player? world = FindWorldPlayer(playerId);
		if (world != null)
		{
			return world;
		}

		foreach (Player player in _recruiting.Prospects)
		{
			if (player.Id == playerId)
			{
				return player;
			}
		}

		return null;
	}

	public Player? FindWorldPlayer(Guid playerId)
	{
		if (World == null)
		{
			return null;
		}

		foreach (Player player in World.Players)
		{
			if (player.Id == playerId)
			{
				return player;
			}
		}

		return null;
	}

	public Player? FindWorldPlayer(string teamId, string playerName, TeamLevel preferred)
	{
		if (World == null || string.IsNullOrWhiteSpace(playerName))
		{
			return null;
		}

		Player? match = MatchWorldPlayer(teamId, playerName, preferred);
		if (match != null)
		{
			return match;
		}

		TeamLevel other = preferred == TeamLevel.Varsity ? TeamLevel.JuniorVarsity : TeamLevel.Varsity;
		return MatchWorldPlayer(teamId, playerName, other)
			?? MatchWorldPlayer(teamId, playerName, preferred, ignoreLevel: true);
	}

	public List<Player> OrganizationRoster()
	{
		var list = new List<Player>();
		if (World == null)
		{
			return list;
		}

		string org = CatalogOrganizationId;
		foreach (Player player in World.Players)
		{
			if (!player.Alumni && !player.IsUnsigned && player.OrganizationId == org)
			{
				list.Add(player);
			}
		}

		return list;
	}

	public List<Player> RosterFor(string? teamId = null)
	{
		var list = new List<Player>();
		if (World == null)
		{
			return list;
		}

		string id = string.IsNullOrWhiteSpace(teamId) ? ActiveTeam.Id : teamId;
		foreach (Player player in World.Players)
		{
			if (player.TeamId == id && !player.Alumni)
			{
				list.Add(player);
			}
		}

		return list;
	}

	public DepthChart DepthChartFor(string? teamId = null) =>
		DepthChartBuilder.Build(RosterFor(teamId));

	private Player? MatchWorldPlayer(string teamId, string playerName, TeamLevel level, bool ignoreLevel = false)
	{
		if (World == null)
		{
			return null;
		}

		string id = string.IsNullOrWhiteSpace(teamId) ? string.Empty : SchoolProfiles.NormalizeId(teamId);
		foreach (Player player in World.Players)
		{
			if (!ignoreLevel && player.TeamLevel != level)
			{
				continue;
			}

			if (id.Length > 0 && !SameProgram(player, id))
			{
				continue;
			}

			if (NamesMatch(player, playerName))
			{
				return player;
			}
		}

		return null;
	}

	private static bool SameProgram(Player player, string teamId)
	{
		return player.TeamId == teamId
			|| player.OrganizationId == teamId
			|| player.TeamId.StartsWith(teamId + "-", StringComparison.OrdinalIgnoreCase);
	}

	private static bool NamesMatch(Player player, string query)
	{
		string needle = query.Trim();
		if (needle.Length == 0)
		{
			return false;
		}

		if (string.Equals(player.FullName, needle, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(player.LastName, needle, StringComparison.OrdinalIgnoreCase)
			|| string.Equals($"{player.LastName}, {player.FirstName}", needle, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if (player.FirstName.Length == 0)
		{
			return false;
		}

		string compact = needle.Replace(".", string.Empty, StringComparison.Ordinal);
		return string.Equals($"{player.FirstName[0]} {player.LastName}", compact, StringComparison.OrdinalIgnoreCase)
			|| string.Equals($"{player.FirstName[0]}. {player.LastName}", needle, StringComparison.OrdinalIgnoreCase);
	}

	private void PersistPlayers() => PersistClubhouse();

	public void PersistClubhouse()
	{
		if (!HasCareerDatabase || World == null)
		{
			return;
		}

		try
		{
			using SqliteCareerStore store = new(CareerDatabasePath(ActiveSaveId!));
			using CareerService service = CareerService.Create(store);
			service.SaveClubhouse(World);
		}
		catch (Exception ex)
		{
			GD.PushError($"Failed to persist clubhouse: {ex.Message}");
		}
	}

	public OrganizationState? RecruitingSchool(string organizationId)
	{
		return FindProgram(organizationId, _recruiting?.Schools);
	}

	public OrganizationState? LeagueProgram(string organizationId)
	{
		return FindProgram(organizationId, _leaguePrograms) ?? RecruitingSchool(organizationId);
	}

	private static OrganizationState? FindProgram(string organizationId, IReadOnlyList<OrganizationState>? schools)
	{
		if (schools == null || string.IsNullOrWhiteSpace(organizationId))
		{
			return null;
		}

		string id = SchoolProfiles.NormalizeId(organizationId);
		foreach (OrganizationState school in schools)
		{
			if (school.Id == id)
			{
				return school;
			}
		}

		return null;
	}

	public bool TryOfferTryoutSpot(Guid prospectId)
	{
		TryoutProspect? prospect = _tryouts.Find(prospectId);
		if (prospect == null)
		{
			return false;
		}

		if (prospect.Decision != TryoutDecision.Offered
			&& !_rosterBoard.CanAdd(CurrentDate, SeasonYear))
		{
			return false;
		}

		if (!_tryouts.TryOffer(prospectId))
		{
			return false;
		}

		_openPool.TryRemove(prospectId);
		_rosterBoard.TryAdd(
			ProgramRosterFactory.FromProspect(prospect, RosterAssignment.Unassigned),
			CurrentDate,
			SeasonYear);
		NotifyRosterChanged();
		return true;
	}

	public void CutTryoutProspect(Guid prospectId)
	{
		TryoutProspect? prospect = _tryouts.Find(prospectId);
		_tryouts.Cut(prospectId);
		if (_rosterBoard.TryRemove(prospectId, out FinalRosterPlayer? removed) && removed != null)
		{
			_openPool.TryAdd(OpenPoolPlayer.FromRoster(removed));
			InsertReleaseLog(removed.FirstName, removed.LastName, removed.PositionLabel, removed.Year.ToAbbrev(), "Tryout");
		}
		else if (prospect != null)
		{
			_openPool.TryAdd(OpenPoolPlayer.FromProspect(prospect, OpenPoolSource.TryoutCut));
		}

		NotifyRosterChanged();
	}

	public void HoldTryoutProspect(Guid prospectId)
	{
		_tryouts.Hold(prospectId);
		if (_rosterBoard.TryRemove(prospectId, out _))
		{
			_openPool.TryRemove(prospectId);
		}

		NotifyRosterChanged();
	}

	public bool TryAssignRoster(Guid playerId, RosterAssignment assignment)
	{
		if (assignment is RosterAssignment.Varsity or RosterAssignment.JuniorVarsity
			&& World != null)
		{
			Player? worldPlayer = FindWorldPlayer(playerId);
			if (worldPlayer != null)
			{
				TeamIdentity dest = assignment == RosterAssignment.Varsity ? VarsityTeam : JuniorVarsityTeam;
				TeamLevel previousLevel = worldPlayer.TeamLevel;
				if (!RosterMovement.TryAssign(worldPlayer, dest, OrganizationRoster(), out _))
				{
					return false;
				}

				_rosterBoard.TryAssign(playerId, assignment, CurrentDate, SeasonYear);
				PersistPlayers();
				if (assignment == RosterAssignment.Varsity && previousLevel != TeamLevel.Varsity)
				{
					RecordTransaction(PlayerTransaction.Promote(
						worldPlayer.FullName,
						worldPlayer.DisplayPosition,
						worldPlayer.Grade.ToAbbrev(),
						Organization.ShortName,
						CurrentDateLabel));
				}

				NotifyRosterChanged();
				return true;
			}
		}
		if (_rosterBoard.Find(playerId) == null)
		{
			TryoutProspect? prospect = _tryouts.Find(playerId);
			if (prospect == null || prospect.Decision == TryoutDecision.Cut)
			{
				return false;
			}

			if (!_rosterBoard.CanAdd(CurrentDate, SeasonYear)
				|| !_rosterBoard.HasRoom(assignment, CurrentDate, SeasonYear))
			{
				return false;
			}

			if (!_tryouts.TryOffer(playerId))
			{
				return false;
			}

			_openPool.TryRemove(playerId);
			if (!_rosterBoard.TryAdd(ProgramRosterFactory.FromProspect(prospect, assignment), CurrentDate, SeasonYear))
			{
				return false;
			}

			if (!_rosterBoard.TryAssign(playerId, assignment, CurrentDate, SeasonYear))
			{
				return false;
			}

			NotifyRosterChanged();
			return true;
		}

		FinalRosterPlayer? existing = _rosterBoard.Find(playerId);
		RosterAssignment before = existing?.Assignment ?? RosterAssignment.Unassigned;
		bool assigned = _rosterBoard.TryAssign(playerId, assignment, CurrentDate, SeasonYear);
		if (assigned)
		{
			if (assignment == RosterAssignment.Varsity && before != RosterAssignment.Varsity)
			{
				FinalRosterPlayer? promoted = _rosterBoard.Find(playerId);
				if (promoted != null)
				{
					RecordTransaction(PlayerTransaction.Promote(
						promoted.FullName,
						promoted.PositionLabel,
						promoted.Year.ToAbbrev(),
						Organization.ShortName,
						CurrentDateLabel));
				}
			}

			NotifyRosterChanged();
		}

		return assigned;
	}

	public bool TryReleaseRoster(Guid playerId)
	{
		if (World != null)
		{
			Player? worldPlayer = FindWorldPlayer(playerId);
			if (worldPlayer != null && !worldPlayer.IsUnsigned)
			{
				OrganizationState? school = LeagueProgram(worldPlayer.OrganizationId);
				string district = school?.DistrictId ?? "";
				if (district.Length > 0)
				{
					RosterMovement.ReleaseToPool(worldPlayer, district);
					PersistPlayers();
				}

				_rosterBoard.TryRemove(playerId, out FinalRosterPlayer? boardPlayer);
				if (boardPlayer != null)
				{
					_openPool.TryAdd(OpenPoolPlayer.FromRoster(boardPlayer));
					InsertReleaseLog(
						boardPlayer.FirstName,
						boardPlayer.LastName,
						boardPlayer.PositionLabel,
						boardPlayer.Year.ToAbbrev(),
						ProgramRosterRules.AssignmentLabel(boardPlayer.Assignment));
				}
				else
				{
					InsertReleaseLog(
						worldPlayer.FirstName,
						worldPlayer.LastName,
						worldPlayer.DisplayPosition,
						worldPlayer.Grade.ToAbbrev(),
						worldPlayer.TeamLevel.ToLabel());
				}

				NotifyRosterChanged();
				return true;
			}
		}
		TryoutProspect? prospect = _tryouts.Find(playerId);
		if (_rosterBoard.TryRemove(playerId, out FinalRosterPlayer? removed) && removed != null)
		{
			if (prospect != null)
			{
				_tryouts.Cut(playerId);
			}

			_openPool.TryAdd(OpenPoolPlayer.FromRoster(removed));
			InsertReleaseLog(
				removed.FirstName,
				removed.LastName,
				removed.PositionLabel,
				removed.Year.ToAbbrev(),
				ProgramRosterRules.AssignmentLabel(removed.Assignment));
			NotifyRosterChanged();
			return true;
		}

		if (prospect != null && prospect.Decision != TryoutDecision.Cut)
		{
			CutTryoutProspect(playerId);
			return true;
		}

		return false;
	}

	public bool TryDraftFromPool(Guid playerId)
	{
		if (!_rosterBoard.CanAdd(CurrentDate, SeasonYear))
		{
			return false;
		}

		OpenPoolPlayer? pooled = _openPool.Take(playerId);
		if (pooled == null)
		{
			return false;
		}

		if (!_rosterBoard.TryAdd(pooled.ToUnassigned(), CurrentDate, SeasonYear))
		{
			_openPool.TryAdd(pooled);
			return false;
		}

		TryoutProspect? prospect = _tryouts.Find(playerId);
		if (prospect != null)
		{
			_tryouts.TryOffer(playerId);
		}

		RecordTransaction(PlayerTransaction.SignedFromOpenPool(
			pooled.FullName,
			pooled.PositionLabel,
			pooled.Year.ToAbbrev(),
			"Unassigned",
			Organization.ShortName,
			CurrentDateLabel));
		NotifyRosterChanged();
		return true;
	}

	public void EnsureNews()
	{
		if (_news.Count > 0)
		{
			return;
		}

		RefreshNews();
	}

	public void RefreshNews()
	{
		if (HasCareerDatabase)
		{
			try
			{
				using SqliteCareerStore store = new(CareerDatabasePath(ActiveSaveId!));
				using CareerService service = CareerService.Create(store);
				_news.Clear();
				_news.AddRange(service.GetNews());
				return;
			}
			catch (Exception ex)
			{
				GD.PushError($"Failed to load news: {ex.Message}");
			}
		}

		List<NewsStory> fresh = NewsWire.Discover(NewsDesk.FromHub(this), _news);
		_news.AddRange(fresh);

		_news.Sort((a, b) =>
		{
			int date = b.Date.CompareTo(a.Date);
			return date != 0 ? date : b.Id.CompareTo(a.Id);
		});
	}

	public void AcknowledgeNews()
	{
		EnsureNews();
		bool dirty = false;
		foreach (NewsStory story in _news)
		{
			if (!story.Read)
			{
				story.Read = true;
				dirty = true;
			}
		}

		if (!dirty)
		{
			return;
		}

		if (HasCareerDatabase)
		{
			try
			{
				using SqliteCareerStore store = new(CareerDatabasePath(ActiveSaveId!));
				using CareerService service = CareerService.Create(store);
				service.MarkNewsRead();
			}
			catch (Exception ex)
			{
				GD.PushError($"Failed to mark news read: {ex.Message}");
			}
		}

		CareerChanged?.Invoke();
	}

	private void NotifyRosterChanged()
	{
		TryoutsChanged?.Invoke();
		RosterBoardChanged?.Invoke();
	}

	public bool TrySignContract(string organizationId) => TryTakeJob(organizationId);

	public bool TryTakeJob(string organizationId)
	{
		if (string.IsNullOrEmpty(ActiveSaveId))
		{
			return false;
		}

		string path = CareerDatabasePath(ActiveSaveId);
		if (!File.Exists(path))
		{
			return false;
		}

		try
		{
			using SqliteCareerStore store = new(path);
			using CareerService service = CareerService.Create(store);
			if (HasSignedContract)
			{
				service.ChangeJob(organizationId);
			}
			else
			{
				service.SignContract(organizationId);
			}

			CareerState state = service.LoadCareer();
			HasSignedContract = true;
			AvailableJobs = service.GetJobBoard();
			ApplyCoachFrom(state.Career);
			ApplySignedOrganization(state);
			if (state.Career.Stints.Count > 1)
			{
				Wins = 0;
				Losses = 0;
				SeasonStatus = "Offseason";
			}
			else
			{
				Wins = 0;
				Losses = 0;
				SeasonStatus = "Regular Season";
				SeasonNumber = state.Career.SeasonsCoached;
			}

			SeasonsCoached = state.Career.SeasonsCoached;
			_jobMarketAcknowledged = true;
			_jobOfferSeen = true;
			CareerLibrary.Upsert(CaptureCareerSlot());
			ContractStateChanged?.Invoke();
			CareerChanged?.Invoke();
			ActiveTeamChanged?.Invoke(ActiveTeam);
			return true;
		}
		catch (Exception ex)
		{
			GD.PushError($"Failed to take job at '{organizationId}': {ex.Message}");
			return false;
		}
	}

	public bool TryAcceptJobOffer(Guid offerId)
	{
		if (string.IsNullOrEmpty(ActiveSaveId))
		{
			return false;
		}

		string path = CareerDatabasePath(ActiveSaveId);
		if (!File.Exists(path))
		{
			return false;
		}

		try
		{
			using SqliteCareerStore store = new(path);
			using CareerService service = CareerService.Create(store);
			service.AcceptJobOffer(offerId);
			CareerState state = service.LoadCareer();
			HasSignedContract = true;
			AvailableJobs = service.GetJobBoard();
			ApplyCoachFrom(state.Career);
			ApplySignedOrganization(state);
			Wins = 0;
			Losses = 0;
			SeasonStatus = "Offseason";
			SeasonsCoached = state.Career.SeasonsCoached;
			_jobMarketAcknowledged = true;
			_jobOfferSeen = true;
			CareerLibrary.Upsert(CaptureCareerSlot());
			ContractStateChanged?.Invoke();
			CareerChanged?.Invoke();
			ActiveTeamChanged?.Invoke(ActiveTeam);
			return true;
		}
		catch (Exception ex)
		{
			GD.PushError($"Failed to accept job offer '{offerId}': {ex.Message}");
			return false;
		}
	}

	public bool TryDeclineJobOffer(Guid offerId)
	{
		if (string.IsNullOrEmpty(ActiveSaveId))
		{
			return false;
		}

		string path = CareerDatabasePath(ActiveSaveId);
		if (!File.Exists(path))
		{
			return false;
		}

		try
		{
			using SqliteCareerStore store = new(path);
			using CareerService service = CareerService.Create(store);
			if (!service.DeclineJobOffer(offerId))
			{
				return false;
			}

			CareerState state = service.LoadCareer();
			AvailableJobs = service.GetJobBoard();
			BindWorld(state);
			ApplyCoachFrom(state.Career);
			CareerLibrary.Upsert(CaptureCareerSlot());
			CareerChanged?.Invoke();
			return true;
		}
		catch (Exception ex)
		{
			GD.PushError($"Failed to decline job offer '{offerId}': {ex.Message}");
			return false;
		}
	}

	private void BindGeneratedCareer(string? slotId)
	{
		bool wasSigned = HasSignedContract;
		AvailableJobs = [];
		PendingJobOffers = [];
		HasSignedContract = true;
		if (string.IsNullOrEmpty(slotId))
		{
			NotifyContractState(wasSigned);
			return;
		}

		string path = CareerDatabasePath(slotId);
		if (!File.Exists(path))
		{
			NotifyContractState(wasSigned);
			return;
		}

		try
		{
			using SqliteCareerStore store = new(path);
			using CareerService service = CareerService.Create(store);
			CareerState state = service.LoadCareer();
			AvailableJobs = service.GetJobBoard();
			HasSignedContract = state.Career.HasSignedContract;
			BindWorld(state);
			ApplyCoachFrom(state.Career);
			SeedLeaguePrograms(state.Organizations);
			if (HasSignedContract)
			{
				ApplySignedOrganization(state);
			}

			RefreshNews();
		}
		catch (Exception ex)
		{
			GD.PushError($"Failed to bind career save '{slotId}': {ex.Message}");
			HasSignedContract = true;
			AvailableJobs = [];
			PendingJobOffers = [];
		}

		NotifyContractState(wasSigned);
	}

	private void BindWorld(CareerState state)
	{
		World = state.World;
		WorldSeed = state.Career.RandomSeed;
		WorldCompetitionLevel = state.Career.CompetitionLevel;
		_seasonYear = state.Career.CurrentSeasonYear <= 0
			? DefaultSeasonYear
			: state.Career.CurrentSeasonYear;
		CurrentDate = state.Career.CurrentDate;
		SeasonStatus = CareerCalendar.PhaseOn(_seasonYear, CurrentDate).ToLabel();
	}

	private void SyncChampionshipsFromWorld()
	{
		if (World == null || VarsityTeam == null)
		{
			return;
		}

		PostseasonDesk.EnsureBracket(new CareerState
		{
			Career = new Career
			{
				CurrentSeasonYear = SeasonYear,
				CurrentDate = CurrentDate,
				RandomSeed = WorldSeed,
				UserOrganizationId = CatalogOrganizationId,
			},
			World = World,
		}, CurrentDate);

		StandingEntry? place = LeagueStandings.FindTeam(
			LeagueStandings.FromWorld(World, SeasonYear),
			VarsityTeam.Id);
		WonDistrict = place?.Rank == 1;
		PostseasonState? bracket = World.Postseason;
		WonRegional = bracket?.WonRegional(VarsityTeam.Id) == true;
		WonState = bracket?.WonState(VarsityTeam.Id) == true;
	}

	private void SeedLeaguePrograms(IReadOnlyList<OrganizationState>? fromCareer)
	{
		_leaguePrograms.Clear();
		if (fromCareer != null)
		{
			foreach (OrganizationState org in fromCareer)
			{
				if (!LeagueCatalog.IsUnsignedOrganization(org.Id))
				{
					_leaguePrograms.Add(org);
				}
			}

			return;
		}

		LeagueCatalog catalog = LeagueCatalog.Standard;
		var organizations = new List<OrganizationState>(catalog.Organizations.Count);
		foreach (OrganizationIdentity identity in catalog.Organizations)
		{
			organizations.Add(new OrganizationState
			{
				Identity = identity,
				DistrictId = catalog.DistrictIdFor(identity.Id),
				CompetitionLevel = catalog.CompetitionLevel,
				TalentTier = ProgramTalentTier.Average,
			});
		}

		ISeededRandom rng = new SeededRandom(SeasonYear * 37 + 13);
		ProgramTalentTier[] tiers = TalentDistribution.AssignTiers(organizations.Count, rng.Derive(11));
		for (int i = 0; i < organizations.Count; i++)
		{
			organizations[i].TalentTier = tiers[i];
		}

		ProgramHistoryGenerator.Assign(organizations, rng.Derive(13), SeasonYear);
		if (_recruiting != null)
		{
			for (int i = 0; i < organizations.Count; i++)
			{
				OrganizationState? live = RecruitingSchool(organizations[i].Id);
				if (live != null)
				{
					organizations[i] = live;
				}
			}
		}

		_leaguePrograms.AddRange(organizations);
	}

	private void ArchiveCareerSeason(
		ProgramSeasonResult season,
		int reputation,
		int baseballBudget,
		ProgramHallOfFameInductee? inductee)
	{
		if (!HasCareerDatabase)
		{
			return;
		}

		try
		{
			using SqliteCareerStore store = new(CareerDatabasePath(ActiveSaveId!));
			using CareerService service = CareerService.Create(store);
			service.ArchiveCompletedSeason(season, reputation, baseballBudget, inductee);
			CareerState state = service.LoadCareer();
			AvailableJobs = service.GetJobBoard();
			BindWorld(state);
			ApplyCoachFrom(state.Career);
			_jobMarketAcknowledged = false;
			_jobOfferSeen = false;
			SeedLeaguePrograms(state.Organizations);
			if (state.Career.HasSignedContract)
			{
				BindRecruiting(state);
			}
		}
		catch (Exception ex)
		{
			GD.PushError($"Failed to archive program season: {ex.Message}");
		}
	}

	private void ApplySignedOrganization(CareerState state)
	{
		BindWorld(state);
		OrganizationState org = state.Organizations.First(item => item.Id == state.Career.UserOrganizationId);
		string previousVarsity = VarsityTeam?.Id ?? "";
		Organization = org.Identity;
		VarsityTeam = state.Teams.First(team => team.OrganizationId == org.Id && team.Level == TeamLevel.Varsity);
		JuniorVarsityTeam = state.Teams.First(team => team.OrganizationId == org.Id && team.Level == TeamLevel.JuniorVarsity);
		if (ActiveTeam == null || previousVarsity != VarsityTeam.Id)
		{
			ActiveTeam = VarsityTeam;
			ClubhouseBoard.Reset();
		}
		else if (ActiveTeam.Level == TeamLevel.JuniorVarsity)
		{
			ActiveTeam = JuniorVarsityTeam;
		}
		else
		{
			ActiveTeam = VarsityTeam;
		}

		CurrentDate = state.Career.CurrentDate;
		BindRecruiting(state);
		SeedLeaguePrograms(state.Organizations);
		BindTransactions(state);
		NotifyRosterChanged();
		SyncChampionshipsFromWorld();
		SyncRecordFromWorld();
	}

	private void NotifyContractState(bool wasSigned)
	{
		if (wasSigned != HasSignedContract)
		{
			ContractStateChanged?.Invoke();
		}
	}

	public bool IsScoutTarget(string teamId, string playerName)
	{
		foreach (ScoutTarget target in _scoutTargets)
		{
			if (target.TeamId == teamId && target.PlayerName == playerName)
			{
				return true;
			}
		}

		return false;
	}

	public void AddScoutTarget(string teamId, string playerName)
	{
		if (teamId == DistrictHubData.UserTeamId || string.IsNullOrEmpty(playerName) || IsScoutTarget(teamId, playerName))
		{
			return;
		}

		_scoutTargets.Insert(0, new ScoutTarget(teamId, playerName));
		PersistRecruiting();
		ScoutTargetsChanged?.Invoke();
	}

	public void RemoveScoutTarget(string teamId, string playerName)
	{
		int index = _scoutTargets.FindIndex(target => target.TeamId == teamId && target.PlayerName == playerName);
		if (index < 0)
		{
			return;
		}

		_scoutTargets.RemoveAt(index);
		PersistRecruiting();
		ScoutTargetsChanged?.Invoke();
	}

	public void ToggleScoutTarget(string teamId, string playerName)
	{
		if (IsScoutTarget(teamId, playerName))
		{
			RemoveScoutTarget(teamId, playerName);
			return;
		}

		AddScoutTarget(teamId, playerName);
	}

	public void RecordRelease(string firstName, string lastName, string position, string year, string squad)
	{
		FinalRosterPlayer? match = _rosterBoard.FindByName(firstName, lastName);
		if (match != null)
		{
			TryReleaseRoster(match.Id);
			return;
		}

		HandednessExtensions.ParseBatsThrows("R/R", out BattingSide bats, out ThrowingHand throws);
		_openPool.TryAdd(new OpenPoolPlayer
		{
			Id = ProgramRosterFactory.StableId(firstName, lastName),
			FirstName = firstName,
			LastName = lastName,
			Position = PlayerPositionExtensions.ParseLabel(position),
			Throws = throws,
			Bats = bats,
			Year = PlayerGradeExtensions.FromAbbrev(year),
			TrueOverall = 55,
			TruePotential = 62,
			Source = OpenPoolSource.Release,
		});
		InsertReleaseLog(firstName, lastName, position, year, squad);
		RosterBoardChanged?.Invoke();
	}

	private void InsertReleaseLog(string firstName, string lastName, string position, string year, string squad)
	{
		RecordTransaction(PlayerTransaction.Release(
			$"{firstName} {lastName}",
			position,
			year,
			squad,
			Organization.ShortName,
			CurrentDateLabel));
	}

	private void ApplyCoachFrom(Career career)
	{
		Coach = career.Coach?.Clone() ?? new CoachProfile();
		if (string.IsNullOrWhiteSpace(Coach.FullName) && !string.IsNullOrWhiteSpace(CoachName))
		{
			string[] parts = CoachName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 1)
			{
				Coach.FirstName = parts[0];
			}
			else if (parts.Length > 1)
			{
				Coach.FirstName = parts[0];
				Coach.LastName = string.Join(" ", parts.Skip(1));
			}
		}

		if (!string.IsNullOrWhiteSpace(Coach.FullName))
		{
			CoachName = Coach.FullName;
		}

		int reputation = career.Reputation <= 0 ? CoachReputation.Starting : career.Reputation;
		CoachReputationScore = CoachReputation.Clamp(reputation);
		CareerWins = career.CareerWins;
		CareerLosses = career.CareerLosses;
		CareerDistrictTitles = career.DistrictTitles;
		CareerRegionalTitles = career.RegionalTitles;
		CareerStateTitles = career.StateTitles;
		if (career.SeasonsCoached > 0)
		{
			SeasonsCoached = career.SeasonsCoached;
			SeasonNumber = SeasonsCoached;
		}
		else if (!career.HasSignedContract)
		{
			SeasonsCoached = 0;
		}
		else if (SeasonsCoached < 1)
		{
			SeasonsCoached = Math.Max(1, SeasonNumber);
		}

		CareerStints = career.Stints.Select(stint => stint.Clone()).ToList();
		if (CareerStints.Count == 0 && career.HasSignedContract)
		{
			string school = string.IsNullOrWhiteSpace(career.Contract.SchoolName)
				? career.UserOrganizationId
				: career.Contract.SchoolName;
			int year = career.CurrentSeasonYear > 0 ? career.CurrentSeasonYear : SeasonYear;
			CareerStints =
			[
				new CoachStint
				{
					OrganizationId = career.UserOrganizationId,
					SchoolName = school,
					StartYear = year,
				},
			];
		}

		PendingJobOffers = career.PendingJobOffers.Select(offer => offer.Clone()).ToList();
		if (career.Contract.IsActive)
		{
			ContractLength = career.Contract.TermYears;
			ContractYear = career.Contract.YearOnDeal;
			AnnualSalary = career.Contract.AnnualSalary;
		}
		else if (!career.HasSignedContract)
		{
			ContractYear = 1;
			ContractLength = 4;
			AnnualSalary = 0;
		}
	}

	private static CoachProfile PrototypeCoach() => new()
	{
		Id = Guid.Parse("9a2c0c1e-6d4a-4f3e-9b18-7c4e2a1d0b11"),
		FirstName = "Daniel",
		LastName = "Hart",
		Age = 34,
		Hometown = "Cascade",
	};

	private static IReadOnlyList<CoachStint> PrototypeStints() =>
	[
		new()
		{
			OrganizationId = "org-cascade-regional",
			SchoolName = "Cascade Regional",
			StartYear = 2023,
		},
	];

	private string ProgramDisplayName =>
		Organization.Name.Replace(" High School", string.Empty, StringComparison.Ordinal);

	/// <summary>
	/// Clubhouse prototype log used when no career save is bound.
	/// Career saves replace this with live district moves.
	/// </summary>
	private void SeedPrototypeTransactions()
	{
		const string cascade = "Cascade";
		const string lakes = "Great Lakes";
		const string harbor = "Harbor Ridge";
		const string delaware = "Delaware Valley";

		_transactions.AddRange(
		[
			PlayerTransaction.Promote("Luis Torres", "SS", "SO", cascade, "MAR 12"),
			PlayerTransaction.Tryout("Mason Young", "RHP", "FR", "JV", lakes, "MAR 11"),
			PlayerTransaction.Release("Diego Gonzalez", "OF", "JR", "Varsity", "Palmetto", "MAR 9"),
			PlayerTransaction.Trade("Cole Moss", "C", "FR", "JV", cascade, lakes, "APR 22"),
			PlayerTransaction.Trade("Sam Keene", "CF", "FR", "JV", lakes, cascade, "APR 22"),
			PlayerTransaction.Release("Cole Brennan", "OF", "JR", "Varsity", cascade, "APR 21"),
			PlayerTransaction.SignedFromOpenPool("Mario Cruz", "C", "JR", "Varsity", cascade, "APR 20"),
			PlayerTransaction.Release("Mario Cruz", "C", "JR", "Varsity", lakes, "APR 19"),
			PlayerTransaction.SignedFromOpenPool("Jordan Hale", "SS", "SO", "JV", cascade, "APR 18"),
			PlayerTransaction.Release("Jordan Hale", "SS", "SO", "JV", delaware, "APR 17"),
			PlayerTransaction.Release("Marcus Webb", "LHP", "SO", "JV", cascade, "APR 14"),
			PlayerTransaction.Trade("Ian Frost", "SS", "SO", "Varsity", cascade, harbor, "APR 9"),
			PlayerTransaction.Trade("Ben Ortiz", "RHP", "JR", "Varsity", harbor, cascade, "APR 9"),
			PlayerTransaction.SignedFromOpenPool("Andre Kim", "OF", "JR", "Varsity", cascade, "APR 6"),
			PlayerTransaction.Release("Andre Kim", "OF", "JR", "Varsity", harbor, "APR 3"),
			PlayerTransaction.Tryout("Chris Nolan", "2B", "FR", "JV", cascade, "MAR 28"),
			PlayerTransaction.Tryout("Leo Hayes", "RHP", "FR", "JV", cascade, "MAR 28"),
			PlayerTransaction.Tryout("Sam Keene", "CF", "FR", "JV", cascade, "MAR 28"),
			PlayerTransaction.Tryout("Riley Fox", "C", "FR", "JV", cascade, "MAR 28"),
			PlayerTransaction.Tryout("Jonah Parks", "3B", "FR", "JV", cascade, "MAR 27"),
		]);
	}

	private IReadOnlyList<RecruitingTarget> ToRecruitingTargets()
	{
		var list = new List<RecruitingTarget>(_scoutTargets.Count);
		foreach (ScoutTarget target in _scoutTargets)
		{
			list.Add(new RecruitingTarget
			{
				PlayerId = target.PlayerId,
				TeamId = target.TeamId,
				PlayerName = target.PlayerName,
				SeasonYear = SeasonYear,
			});
		}

		return list;
	}

	private void BindScoutTargets(IReadOnlyList<RecruitingTarget> saved)
	{
		_scoutTargets.Clear();
		foreach (RecruitingTarget target in saved)
		{
			_scoutTargets.Add(new ScoutTarget(target.TeamId, target.PlayerName, target.PlayerId));
		}

		ScoutTargetsChanged?.Invoke();
	}

	public bool IsScoutTarget(Guid playerId)
	{
		if (playerId == Guid.Empty)
		{
			return false;
		}

		foreach (ScoutTarget target in _scoutTargets)
		{
			if (target.PlayerId == playerId)
			{
				return true;
			}
		}

		return false;
	}

	public void AddScoutTarget(Player player)
	{
		if (player.OrganizationId == CatalogOrganizationId && !player.IsUnsigned)
		{
			return;
		}

		if (IsScoutTarget(player.Id))
		{
			return;
		}

		_scoutTargets.Insert(0, new ScoutTarget(player.TeamId, player.FullName, player.Id));
		_recruiting.Market.MarkInterest(CatalogOrganizationId, player.Id, CurrentDate, SeasonYear, 2);
		PersistRecruiting();
		ScoutTargetsChanged?.Invoke();
		NotifyRosterChanged();
	}

	public void RemoveScoutTarget(Guid playerId)
	{
		int index = _scoutTargets.FindIndex(target => target.PlayerId == playerId);
		if (index < 0)
		{
			return;
		}

		_scoutTargets.RemoveAt(index);
		PersistRecruiting();
		ScoutTargetsChanged?.Invoke();
		NotifyRosterChanged();
	}

	public void ToggleScoutTarget(Player player)
	{
		if (IsScoutTarget(player.Id))
		{
			RemoveScoutTarget(player.Id);
			return;
		}

		AddScoutTarget(player);
	}

	public string InterestCopy(Guid playerId)
	{
		var names = new List<string>();
		foreach (RecruitingInterest item in _recruiting.Market.InterestFor(playerId))
		{
			OrganizationState? school = RecruitingSchool(item.OrganizationId);
			string name = school?.ShortName ?? item.OrganizationId;
			if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
			{
				names.Add(name);
			}
		}

		return RecruitingInterest.Format(names);
	}
}

internal sealed class DayAdvanceSignals
{
	public DateOnly StartedOn;
	public bool HadGame;
	public bool HadMajorTransaction;
	public bool HadPostseason;
	public bool RolledOver;
	public readonly List<GameSummary> Games = new();
	public readonly List<PlayerTransaction> Transactions = new();
}

public sealed record ScoutTarget(string TeamId, string PlayerName, Guid PlayerId = default);
