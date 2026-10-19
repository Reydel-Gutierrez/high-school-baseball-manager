using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// Persistent-shell navigator: the top bar stays mounted and content pages
/// are shown or hidden under ContentArea. Prefer this over ChangeScene
/// (which would rebuild chrome) or hiding individual home cards.
/// </summary>
public partial class AppNavigator : MarginContainer
{
	public const string HomeViewKey = "home";
	public const string CalendarViewKey = "calendar";
	public const string ScheduleViewKey = "schedule";
	public const string RosterViewKey = "roster";
	public const string TransactionsViewKey = "transactions";
	public const string GoalsViewKey = "goals";
	public const string TryoutsViewKey = "tryouts";
	public const string GraduationViewKey = "graduation";
	public const string LineupsViewKey = "lineups";
	public const string RotationViewKey = "rotation";
	public const string InjuriesViewKey = "injuries";
	public const string LeagueOverviewViewKey = "league-overview";
	public const string StandingsViewKey = "standings";
	public const string RankingsViewKey = "rankings";
	public const string LeadersViewKey = "leaders";
	public const string AwardsViewKey = "awards";
	public const string StatisticsViewKey = "statistics";
	public const string NewsViewKey = "news";
	public const string MessagesViewKey = "messages";
	public const string PlayerSearchViewKey = "player-search";
	public const string TargetsViewKey = "targets";
	public const string BudgetViewKey = "budget";
	public const string SponsorsViewKey = "sponsors";
	public const string CareerViewKey = "career";
	public const string SettingsViewKey = "settings";
	public const string PlaceholderViewKey = "placeholder";
	public const string GamePreviewViewKey = "game-preview";
	public const string LiveGameViewKey = "live-game";
	public const string TrainingViewKey = "training";
	public const string ChampionshipViewKey = "championship";
	public const string SeasonReviewViewKey = "season-review";
	public const string HallOfFameViewKey = "hall-of-fame";
	public const string SeasonRolloverViewKey = "season-rollover";

	public const int OrganizationRosterId = 100;
	public const int OrganizationTransactionsId = 102;
	public const int OrganizationGoalsId = 103;
	public const int OrganizationTryoutsId = 104;
	public const int OrganizationGraduationId = 106;
	public const int ClubhouseLineupsId = 200;
	public const int ClubhouseRotationId = 201;
	public const int ClubhouseInjuriesId = 202;
	public const int ClubhouseTrainingId = 203;
	public const int LeagueStandingsId = 300;
	public const int LeagueLeadersId = 301;
	public const int LeagueAwardsId = 302;
	public const int LeagueStatisticsId = 303;
	public const int LeagueNewsId = 304;
	public const int LeagueRankingsId = 305;
	public const int LeagueWorldId = 306;
	public const int LeaguePlayoffsId = 307;
	public const int FinancesBudgetId = 400;
	public const int FinancesSponsorsId = 401;
	public const int ScoutingPlayerSearchId = 500;
	public const int ScoutingTargetsId = 502;

	private TopBarNavigation _topBar = null!;
	private Control _homeView = null!;
	private Control _calendarView = null!;
	private Control _scheduleView = null!;
	private Control _rosterView = null!;
	private Control _transactionsView = null!;
	private Control _goalsView = null!;
	private Control _tryoutsView = null!;
	private Control _graduationView = null!;
	private Control _lineupsView = null!;
	private Control _rotationView = null!;
	private Control _injuriesView = null!;
	private LeagueOverviewPage _leagueOverviewPage = null!;
	private StandingsPage _standingsPage = null!;
	private RankingsPage _rankingsPage = null!;
	private Control _leadersView = null!;
	private AwardsPage _awardsPage = null!;
	private Control _statisticsView = null!;
	private Control _newsView = null!;
	private Control _messagesView = null!;
	private Control _playerSearchView = null!;
	private Control _targetsView = null!;
	private Control _budgetView = null!;
	private Control _sponsorsView = null!;
	private Control _careerView = null!;
	private Control _settingsView = null!;
	private Control _placeholderView = null!;
	private Label _placeholderTitle = null!;
	private Label _placeholderBody = null!;
	private ProfileDock _profileDock = null!;
	private GamePreviewPage _gamePreviewPage = null!;
	private LiveGamePage _liveGamePage = null!;
	private TrainingPage _trainingPage = null!;
	private ChampionshipCelebrationPage _championshipPage = null!;
	private SeasonReviewPage _seasonReviewPage = null!;
	private HallOfFameInductionPage _hallOfFamePage = null!;
	private SeasonRolloverPage _seasonRolloverPage = null!;
	private readonly Dictionary<string, Control> _views = new();
	private string _activeView = HomeViewKey;
	private string _previewReturnView = HomeViewKey;
	private string _previewReturnTopBar = TopBarNavigation.HomeKey;
	private bool _navigationSoundsEnabled;
	private bool _unsignedLock;
	private bool _awardsCeremony;
	private GameSession _session = null!;

	public override void _Ready()
	{
		_topBar = GetNode<TopBarNavigation>("Shell/AppLayout/TopBar");
		_homeView = GetNode<Control>("Shell/AppLayout/ContentArea/ContentRoot");
		_calendarView = GetNode<Control>("Shell/AppLayout/ContentArea/CalendarView");
		_scheduleView = GetNode<Control>("Shell/AppLayout/ContentArea/ScheduleView");
		_rosterView = GetNode<Control>("Shell/AppLayout/ContentArea/RosterView");
		_transactionsView = GetNode<Control>("Shell/AppLayout/ContentArea/TransactionsView");
		_goalsView = GetNode<Control>("Shell/AppLayout/ContentArea/GoalsView");
		_tryoutsView = GetNode<Control>("Shell/AppLayout/ContentArea/TryoutsView");
		_graduationView = GetNode<Control>("Shell/AppLayout/ContentArea/GraduationView");
		_lineupsView = GetNode<Control>("Shell/AppLayout/ContentArea/LineupsView");
		_rotationView = GetNode<Control>("Shell/AppLayout/ContentArea/RotationView");
		_injuriesView = GetNode<Control>("Shell/AppLayout/ContentArea/InjuriesView");
		_leagueOverviewPage = GetNode<LeagueOverviewPage>("Shell/AppLayout/ContentArea/LeagueOverviewView");
		_standingsPage = GetNode<StandingsPage>("Shell/AppLayout/ContentArea/StandingsView");
		_rankingsPage = GetNode<RankingsPage>("Shell/AppLayout/ContentArea/RankingsView");
		_leadersView = GetNode<Control>("Shell/AppLayout/ContentArea/LeadersView");
		_awardsPage = GetNode<AwardsPage>("Shell/AppLayout/ContentArea/AwardsView");
		_awardsPage.Closed += OnAwardsClosed;
		_awardsPage.Continued += OnAwardsContinued;
		_statisticsView = GetNode<Control>("Shell/AppLayout/ContentArea/StatisticsView");
		_newsView = GetNode<Control>("Shell/AppLayout/ContentArea/NewsView");
		_messagesView = GetNode<Control>("Shell/AppLayout/ContentArea/MessagesView");
		_playerSearchView = GetNode<Control>("Shell/AppLayout/ContentArea/PlayerSearchView");
		_targetsView = GetNode<Control>("Shell/AppLayout/ContentArea/TargetsView");
		_budgetView = GetNode<Control>("Shell/AppLayout/ContentArea/BudgetView");
		_sponsorsView = GetNode<Control>("Shell/AppLayout/ContentArea/SponsorsView");
		_careerView = GetNode<Control>("Shell/AppLayout/ContentArea/CareerView");
		_settingsView = GetNode<Control>("Shell/AppLayout/ContentArea/SettingsView");

		_placeholderView = BuildPlaceholderView();
		GetNode("Shell/AppLayout/ContentArea").AddChild(_placeholderView);

		_profileDock = new ProfileDock
		{
			Name = "ProfileDock",
		};
		GetNode("Shell/AppLayout/ContentArea").AddChild(_profileDock);

		_gamePreviewPage = GetNode<GamePreviewPage>("Shell/AppLayout/ContentArea/GamePreviewView");
		_gamePreviewPage.Closed += ReturnFromGamePreview;
		_gamePreviewPage.EditLineupRequested += ShowLineups;
		_gamePreviewPage.EditRotationRequested += ShowRotation;
		_gamePreviewPage.SimulateRequested += OnSimulateGame;
		_gamePreviewPage.PlayGameRequested += OnPlayLiveGame;

		// Live game day is built in code and parked beside the preview.
		_liveGamePage = new LiveGamePage
		{
			Name = "LiveGameView",
			Visible = false,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		_gamePreviewPage.GetParent().AddChild(_liveGamePage);
		_liveGamePage.Completed += OnLiveGameCompleted;

		_trainingPage = new TrainingPage
		{
			Name = "TrainingView",
			Visible = false,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		_lineupsView.GetParent().AddChild(_trainingPage);

		_championshipPage = GetNode<ChampionshipCelebrationPage>("Shell/AppLayout/ContentArea/ChampionshipView");
		_championshipPage.Closed += ReturnFromFullscreenDesk;
		_championshipPage.Continued += OnChampionshipContinued;

		_seasonReviewPage = GetNode<SeasonReviewPage>("Shell/AppLayout/ContentArea/SeasonReviewView");
		_seasonReviewPage.Closed += ReturnFromFullscreenDesk;
		_seasonReviewPage.Continued += OnSeasonReviewContinued;
		_hallOfFamePage = GetNode<HallOfFameInductionPage>("Shell/AppLayout/ContentArea/HallOfFameView");
		_hallOfFamePage.Closed += ReturnFromFullscreenDesk;
		_hallOfFamePage.Continued += OnHallOfFameContinued;

		_seasonRolloverPage = new SeasonRolloverPage
		{
			Name = "SeasonRolloverView",
			Visible = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		GetNode("Shell/AppLayout/ContentArea").AddChild(_seasonRolloverPage);
		_seasonRolloverPage.Closed += ReturnFromFullscreenDesk;
		_seasonRolloverPage.Continued += OnSeasonRolloverContinued;

		_views[HomeViewKey] = _homeView;
		_views[CalendarViewKey] = _calendarView;
		_views[ScheduleViewKey] = _scheduleView;
		_views[RosterViewKey] = _rosterView;
		_views[TransactionsViewKey] = _transactionsView;
		_views[GoalsViewKey] = _goalsView;
		_views[TryoutsViewKey] = _tryoutsView;
		_views[GraduationViewKey] = _graduationView;
		_views[LineupsViewKey] = _lineupsView;
		_views[RotationViewKey] = _rotationView;
		_views[InjuriesViewKey] = _injuriesView;
		_views[LeagueOverviewViewKey] = _leagueOverviewPage;
		_views[StandingsViewKey] = _standingsPage;
		_views[RankingsViewKey] = _rankingsPage;
		_views[LeadersViewKey] = _leadersView;
		_views[AwardsViewKey] = _awardsPage;
		_views[StatisticsViewKey] = _statisticsView;
		_views[NewsViewKey] = _newsView;
		_views[MessagesViewKey] = _messagesView;
		_views[PlayerSearchViewKey] = _playerSearchView;
		_views[TargetsViewKey] = _targetsView;
		_views[BudgetViewKey] = _budgetView;
		_views[SponsorsViewKey] = _sponsorsView;
		_views[CareerViewKey] = _careerView;
		_views[SettingsViewKey] = _settingsView;
		_views[PlaceholderViewKey] = _placeholderView;
		_views[GamePreviewViewKey] = _gamePreviewPage;
		_views[LiveGameViewKey] = _liveGamePage;
		_views[TrainingViewKey] = _trainingPage;
		_views[ChampionshipViewKey] = _championshipPage;
		_views[SeasonReviewViewKey] = _seasonReviewPage;
		_views[HallOfFameViewKey] = _hallOfFamePage;
		_views[SeasonRolloverViewKey] = _seasonRolloverPage;

		ConnectChrome();
		_session = GetNode<GameSession>("/root/GameSession");
		_session.ContractStateChanged += OnContractStateChanged;
		_session.CareerChanged += RefreshInboxBadge;
		_session.ScoutTargetsChanged += RefreshInboxBadge;
		_unsignedLock = !_session.HasSignedContract;
		if (_session.HasSignedContract)
		{
			ShowView(HomeViewKey, TopBarNavigation.HomeKey);
		}
		else
		{
			ShowView(CareerViewKey, TopBarNavigation.CareerKey);
		}

		AudioManager.Current.StartSoundtrack();
		_navigationSoundsEnabled = true;
		if (_settingsView is SettingsPage settings)
		{
			settings.SetInGame(true);
			settings.SavedAndExited += ReturnToTitle;
			settings.ExitGameRequested += QuitGame;
		}
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ContractStateChanged -= OnContractStateChanged;
			_session.CareerChanged -= RefreshInboxBadge;
			_session.ScoutTargetsChanged -= RefreshInboxBadge;
		}
	}

	public void ShowHome()
	{
		ShowView(HomeViewKey, TopBarNavigation.HomeKey);
	}

	public void ShowGamePreview(CalendarContest game)
	{
		if (_activeView != GamePreviewViewKey)
		{
			_previewReturnView = _activeView;
			_previewReturnTopBar = _topBar.GetActiveNavigation();
			if (string.IsNullOrEmpty(_previewReturnTopBar))
			{
				_previewReturnTopBar = TopBarNavigation.HomeKey;
			}
		}

		_previewContest = game;
		_gamePreviewPage.Bind(game);
		ShowView(GamePreviewViewKey, TopBarNavigation.HomeKey);
	}

	public void ShowChampionship(ChampionshipTitle title)
	{
		RememberFullscreenReturn();
		_championshipPage.Bind(title);
		ShowView(ChampionshipViewKey, TopBarNavigation.HomeKey);
	}

	public void ShowSeasonReview()
	{
		RememberFullscreenReturn();
		_seasonReviewPage.Refresh();
		ShowView(SeasonReviewViewKey, TopBarNavigation.HomeKey);
	}

	public void ShowHallOfFameInduction()
	{
		RememberFullscreenReturn();
		_hallOfFamePage.Refresh();
		ShowView(HallOfFameViewKey, TopBarNavigation.HomeKey);
	}

	public void ShowSeasonRollover()
	{
		RememberFullscreenReturn();
		_seasonRolloverPage.Refresh();
		ShowView(SeasonRolloverViewKey, TopBarNavigation.HomeKey);
	}

	public void ShowCareerHallOfFame()
	{
		ShowCareer();
		if (_careerView is CareerPage career)
		{
			career.ShowHallOfFame();
		}
	}

	public void ShowCareerJobMarket()
	{
		ShowCareer();
		if (_careerView is CareerPage career)
		{
			career.ShowJobMarket();
		}
	}

	public void ShowJobOffer()
	{
		ShowHome();
		if (_homeView is HomePage home)
		{
			home.ShowJobOffer();
		}
	}

	private void RememberFullscreenReturn()
	{
		if (_activeView is ChampionshipViewKey or SeasonReviewViewKey or HallOfFameViewKey or SeasonRolloverViewKey or GamePreviewViewKey or LiveGameViewKey
			|| (_activeView == AwardsViewKey && _awardsCeremony))
		{
			return;
		}

		_previewReturnView = _activeView;
		_previewReturnTopBar = _topBar.GetActiveNavigation();
		if (string.IsNullOrEmpty(_previewReturnTopBar))
		{
			_previewReturnTopBar = TopBarNavigation.HomeKey;
		}
	}

	private void OnChampionshipContinued(ChampionshipTitle title)
	{
		_session.AcknowledgeChampionship(title);
		if (title == ChampionshipTitle.State)
		{
			ShowAwardsCeremony(AwardScope.State);
			return;
		}

		ShowHome();
	}

	private void OnSeasonReviewContinued()
	{
		SeasonReviewDesk.Commit(_session);
		if (HallOfFameDesk.ThisYear(_session) != null)
		{
			ShowHallOfFameInduction();
			return;
		}

		ShowHome();
	}

	private void OnHallOfFameContinued()
	{
		_session.AcknowledgeHallOfFame();
		ShowCareerHallOfFame();
	}

	private void OnSeasonRolloverContinued()
	{
		_session.AcknowledgeSeasonRollover();
		SimulationRun run = _session.SimulateDays(1);
		ShowHome();
		if (_homeView is HomePage home)
		{
			home.ApplySimulationRun(run);
		}
	}

	private void ReturnFromFullscreenDesk()
	{
		string view = _previewReturnView;
		string topBar = _previewReturnTopBar;
		if (string.IsNullOrEmpty(view)
			|| view is ChampionshipViewKey or SeasonReviewViewKey or HallOfFameViewKey or SeasonRolloverViewKey or GamePreviewViewKey or LiveGameViewKey
			|| view == AwardsViewKey && _awardsCeremony
			|| !_views.ContainsKey(view))
		{
			view = HomeViewKey;
			topBar = TopBarNavigation.HomeKey;
		}

		ShowView(view, topBar, playSound: false);
	}

	private void OnAwardsContinued(AwardScope scope)
	{
		_session.AcknowledgeAwards(scope);
		_awardsCeremony = false;
		if (scope == AwardScope.State)
		{
			ShowSeasonReview();
			return;
		}

		ShowHome();
	}

	private void OnAwardsClosed()
	{
		_awardsCeremony = false;
		ReturnFromFullscreenDesk();
	}

	private void ReturnFromGamePreview()
	{
		string view = _previewReturnView;
		string topBar = _previewReturnTopBar;
		if (string.IsNullOrEmpty(view) || view == GamePreviewViewKey || view == LiveGameViewKey || !_views.ContainsKey(view))
		{
			view = HomeViewKey;
			topBar = TopBarNavigation.HomeKey;
		}

		ShowView(view, topBar, playSound: false);
		if (view == ScheduleViewKey && _scheduleView is SchedulePage schedule)
		{
			schedule.FocusCurrentGame();
		}
	}

	private CalendarContest? _previewContest;

	private void OnPlayLiveGame()
	{
		if (_previewContest is CalendarContest contest && _liveGamePage.Begin(contest))
		{
			ShowView(LiveGameViewKey, TopBarNavigation.HomeKey);
			return;
		}

		// Tryouts, prototype careers, or a forfeit: fall back to the quick sim.
		OnSimulateGame();
	}

	private void OnLiveGameCompleted(SimulationRun run)
	{
		ReturnFromGamePreview();
		ShowHome();
		if (_homeView is HomePage home)
		{
			home.ApplySimulationRun(run);
		}
	}

	private void OnSimulateGame()
	{
		SimulationRun run = _session.SimulateDays(1);
		ReturnFromGamePreview();
		ShowHome();
		if (_homeView is HomePage home)
		{
			home.ApplySimulationRun(run);
		}
	}

	public void ShowCalendar()
	{
		ShowView(CalendarViewKey, TopBarNavigation.CalendarKey);
		if (_calendarView is CalendarPage page)
		{
			page.FocusCurrentDate();
		}
	}

	public void ShowSchedule()
	{
		ShowView(ScheduleViewKey, TopBarNavigation.CalendarKey);
		if (_scheduleView is SchedulePage page)
		{
			page.FocusCurrentGame();
		}
	}

	public void ShowRoster()
	{
		ShowView(RosterViewKey, TopBarNavigation.OrganizationKey);
	}


	public void ShowPlayerSearch()
	{
		ShowView(PlayerSearchViewKey, TopBarNavigation.ScoutingKey);
	}

	public void ShowTargets()
	{
		ShowView(TargetsViewKey, TopBarNavigation.ScoutingKey);
	}

	public void ShowTransactions()
	{
		ShowView(TransactionsViewKey, TopBarNavigation.OrganizationKey);
	}

	public void ShowGoals()
	{
		ShowView(GoalsViewKey, TopBarNavigation.OrganizationKey);
	}

	public void ShowTryouts()
	{
		ShowView(TryoutsViewKey, TopBarNavigation.OrganizationKey);
	}

	/// <summary>The new class lives on the roster once Decision Day has passed.</summary>
	public void ShowIncomingClass()
	{
		ShowRoster();
	}

	public void ShowGraduation()
	{
		ShowView(GraduationViewKey, TopBarNavigation.OrganizationKey);
	}

	public void ShowTraining()
	{
		ShowView(TrainingViewKey, TopBarNavigation.ClubhouseKey);
	}

	public void ShowLineups()
	{
		ShowView(LineupsViewKey, TopBarNavigation.ClubhouseKey);
	}

	public void ShowRotation()
	{
		ShowView(RotationViewKey, TopBarNavigation.ClubhouseKey);
	}

	public void ShowInjuries()
	{
		ShowView(InjuriesViewKey, TopBarNavigation.ClubhouseKey);
	}

	public void ShowLeague()
	{
		_leagueOverviewPage.ShowWorld();
		ShowView(LeagueOverviewViewKey, TopBarNavigation.LeagueKey);
	}

	public void ShowStandings()
	{
		_standingsPage.ShowLeagueTable();
		ShowView(StandingsViewKey, TopBarNavigation.LeagueKey);
	}

	public void ShowStandingsForDistrict(string districtId)
	{
		_standingsPage.ShowDistrictTable(districtId);
		ShowView(StandingsViewKey, TopBarNavigation.LeagueKey);
	}

	public void ShowPlayoffs()
	{
		_tabOverride = "PLAYOFFS";
		_standingsPage.ShowPlayoffBracket();
		ShowView(StandingsViewKey, TopBarNavigation.LeagueKey);
	}

	public void ShowRankings()
	{
		ShowView(RankingsViewKey, TopBarNavigation.LeagueKey);
	}

	public void OpenSchoolProfile(string teamId)
	{
		string id = SchoolProfiles.NormalizeId(teamId);
		if (_activeView == HomeViewKey && _homeView is HomePage home)
		{
			home.ShowSchool(id);
			return;
		}

		if (_activeView == CalendarViewKey && _calendarView is CalendarPage calendar)
		{
			calendar.ShowSchool(id);
			return;
		}

		if (_activeView == LeagueOverviewViewKey)
		{
			_leagueOverviewPage.SelectSchool(id);
			return;
		}

		if (_activeView == StandingsViewKey)
		{
			_standingsPage.SelectSchool(id);
			return;
		}

		_profileDock.ShowSchool(id);
	}

	public void OpenPlayerProfile(string teamId, string playerName)
	{
		string id = string.IsNullOrWhiteSpace(teamId) ? string.Empty : SchoolProfiles.NormalizeId(teamId);
		string name = playerName ?? string.Empty;
		if (name.Length == 0)
		{
			return;
		}

		if (_activeView == HomeViewKey && _homeView is HomePage home)
		{
			home.ShowPlayer(id, name);
			return;
		}

		if (_activeView == CalendarViewKey && _calendarView is CalendarPage calendar)
		{
			calendar.ShowPlayer(id, name);
			return;
		}

		if (_activeView == RosterViewKey && _rosterView is RosterPage roster && roster.TrySelectByName(name))
		{
			return;
		}

		if (_activeView == StatisticsViewKey && _statisticsView is StatisticsPage statistics && statistics.TryShowPlayer(id, name))
		{
			return;
		}

		if (_activeView == PlayerSearchViewKey && _playerSearchView is ScoutingPlayersPage search && search.TryShowPlayer(id, name))
		{
			return;
		}

		if (_activeView == TargetsViewKey && _targetsView is ScoutingPlayersPage targets && targets.TryShowPlayer(id, name))
		{
			return;
		}

		_profileDock.ShowPlayer(id, name);
	}

	public void ShowLeaders()
	{
		ShowView(LeadersViewKey, TopBarNavigation.LeagueKey);
	}

	public void ShowAwards()
	{
		_awardsCeremony = false;
		_awardsPage.ShowHub();
		ShowView(AwardsViewKey, TopBarNavigation.LeagueKey);
	}

	public void ShowAwardsCeremony(AwardScope scope)
	{
		RememberFullscreenReturn();
		_awardsCeremony = true;
		_awardsPage.BindCeremony(scope);
		ShowView(AwardsViewKey, TopBarNavigation.HomeKey);
	}

	public void ShowStatistics()
	{
		ShowView(StatisticsViewKey, TopBarNavigation.LeagueKey);
	}

	public void ShowNews()
	{
		ShowView(NewsViewKey, TopBarNavigation.LeagueKey);
	}

	public void ShowMessages()
	{
		ShowView(MessagesViewKey, TopBarNavigation.MessagesKey);
	}

	public void OpenInboxItem(InboxMessage item)
	{
		_session.AcknowledgeMessage(item.Id);
		switch (item.Destination)
		{
			case InboxDestination.Tryouts:
				ShowTryouts();
				break;
			case InboxDestination.IncomingClass:
				ShowIncomingClass();
				break;
			case InboxDestination.Graduation:
				ShowGraduation();
				break;
			case InboxDestination.Goals:
				ShowGoals();
				break;
			case InboxDestination.Injuries:
				ShowInjuries();
				break;
			case InboxDestination.PlayerSearch:
				ShowPlayerSearch();
				break;
			case InboxDestination.Targets:
				ShowTargets();
				break;
			case InboxDestination.JobMarket:
				ShowCareerJobMarket();
				break;
			case InboxDestination.JobOffer:
				ShowJobOffer();
				break;
			case InboxDestination.Championship:
				if (item.Championship != null)
				{
					ShowChampionship(item.Championship.Value);
				}
				else
				{
					ShowHome();
				}

				break;
			case InboxDestination.Awards:
				if (item.Award != null)
				{
					ShowAwardsCeremony(item.Award.Value);
				}
				else
				{
					ShowAwards();
				}

				break;
			case InboxDestination.SeasonReview:
				ShowSeasonReview();
				break;
			case InboxDestination.HallOfFame:
				ShowHallOfFameInduction();
				break;
			case InboxDestination.SeasonRollover:
				ShowSeasonRollover();
				break;
			case InboxDestination.Transactions:
				ShowTransactions();
				break;
			case InboxDestination.GamePreview:
				if (item.Game != null)
				{
					ShowGamePreview(item.Game);
				}
				else
				{
					ShowSchedule();
				}

				break;
			case InboxDestination.Calendar:
				ShowCalendar();
				break;
			case InboxDestination.Schedule:
				ShowSchedule();
				break;
			case InboxDestination.Roster:
				ShowRoster();
				break;
			case InboxDestination.Lineups:
				ShowLineups();
				break;
			case InboxDestination.Budget:
				ShowBudget();
				break;
			case InboxDestination.PlayerProfile:
				if (!string.IsNullOrEmpty(item.PlayerName))
				{
					OpenPlayerProfile(item.TeamId ?? string.Empty, item.PlayerName);
				}
				else
				{
					ShowInjuries();
				}

				break;
			default:
				ShowMessages();
				break;
		}
	}

	public void ShowBudget()
	{
		ShowView(BudgetViewKey, TopBarNavigation.FinancesKey);
	}

	public void ShowSponsors()
	{
		ShowView(SponsorsViewKey, TopBarNavigation.FinancesKey);
	}

	public void ShowCareer()
	{
		ShowView(CareerViewKey, TopBarNavigation.CareerKey);
	}

	public void ShowSettings()
	{
		ShowView(SettingsViewKey, TopBarNavigation.SettingsKey);
		if (_settingsView is SettingsPage page)
		{
			page.FocusAudio();
		}
	}

	public void ShowPlaceholder(string title, string body)
	{
		ShowPlaceholder(title, body, TopBarNavigation.OrganizationKey);
	}

	public void ShowPlaceholder(string title, string body, string topBarKey)
	{
		_placeholderTitle.Text = title;
		_placeholderBody.Text = body;
		ShowView(PlaceholderViewKey, topBarKey);
	}

	private void ConnectChrome()
	{
		Button calendarButton = GetNode<Button>("Shell/AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/CalendarButton");
		calendarButton.Pressed += ShowCalendar;

		Button homeButton = GetNode<Button>("Shell/AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/HomeButton");
		homeButton.Pressed += ShowHome;

		Button messagesButton = GetNode<Button>("Shell/AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/MessagesButton");
		messagesButton.Pressed += ShowMessages;

		MenuButton organizationMenu = GetNode<MenuButton>("Shell/AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/OrganizationMenu");
		organizationMenu.GetPopup().IdPressed += OnOrganizationItemPressed;

		MenuButton clubhouseMenu = GetNode<MenuButton>("Shell/AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/ClubhouseMenu");
		clubhouseMenu.GetPopup().IdPressed += OnClubhouseItemPressed;
		if (clubhouseMenu.GetPopup().GetItemIndex(ClubhouseTrainingId) < 0)
		{
			clubhouseMenu.GetPopup().AddItem("TRAINING", ClubhouseTrainingId);
		}

		MenuButton leagueMenu = GetNode<MenuButton>("Shell/AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/DistrictHubMenu");
		leagueMenu.GetPopup().IdPressed += OnLeagueItemPressed;

		MenuButton scoutingMenu = GetNode<MenuButton>("Shell/AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/ScoutingMenu");
		scoutingMenu.GetPopup().IdPressed += OnScoutingItemPressed;

		MenuButton financesMenu = GetNode<MenuButton>("Shell/AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/FinancesMenu");
		financesMenu.GetPopup().IdPressed += OnFinancesItemPressed;

		Button careerButton = GetNode<Button>("Shell/AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/CareerButton");
		careerButton.Pressed += ShowCareer;

		HBoxContainer rightCluster = GetNode<HBoxContainer>("Shell/AppLayout/TopBar/TopBarMargin/HBoxContainer/RightCluster");
		Button settingsButton = GetNode<Button>("Shell/AppLayout/TopBar/TopBarMargin/HBoxContainer/RightCluster/SettingsButton");
		var soundtrack = new SoundtrackControls { Name = "SoundtrackControls" };
		rightCluster.AddChild(soundtrack);
		rightCluster.MoveChild(soundtrack, settingsButton.GetIndex());
		settingsButton.Pressed += ShowSettings;

		BuildShell(rightCluster);
	}

	// ------------------------------------------------------------------ shell
	private ShellRail _rail = null!;
	private SectionBar _sectionBar = null!;
	private Label _shellDate = null!;
	private PanelContainer _shellPhase = null!;
	private Label _shellPhaseText = null!;
	private Button _continueButton = null!;
	private MenuButton _simMenu = null!;
	private string? _tabOverride;
	private string _lastSection = ShellRail.Home;

	/// <summary>
	/// Redesigned chrome (docs/ui-concepts): left rail for the six sections,
	/// a slim top bar with the date, season phase, mail, settings and one gold
	/// CONTINUE button, and section tabs under it. The old dropdown menus stay
	/// in the scene (hidden) so their ids and handlers keep working.
	/// </summary>
	private void BuildShell(HBoxContainer rightCluster)
	{
		HBoxContainer shell = GetNode<HBoxContainer>("Shell");
		_rail = new ShellRail { Name = "ShellRail", SizeFlagsVertical = SizeFlags.ExpandFill };
		shell.AddChild(_rail);
		shell.MoveChild(_rail, 0);
		_rail.SectionPressed += OpenSection;
		_rail.CoachPressed += ShowCareer;

		// Flat dark canvas from the concepts: drop each page's stadium photo.
		RenderingServer.SetDefaultClearColor(UiKit.Bg);
		PanelContainer contentArea = GetNode<PanelContainer>("Shell/AppLayout/ContentArea");
		contentArea.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = UiKit.Bg });
		foreach (Node page in contentArea.GetChildren())
		{
			if (page.GetNodeOrNull<CanvasItem>("BackgroundImage") is { } photo)
			{
				photo.Visible = false;
			}
		}

		VBoxContainer layout = GetNode<VBoxContainer>("Shell/AppLayout");
		_sectionBar = new SectionBar { Name = "SectionBar" };
		layout.AddChild(_sectionBar);
		layout.MoveChild(_sectionBar, 1);

		_topBar.CompactShell = true;
		_topBar.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = UiKit.Bg });
		_topBar.CustomMinimumSize = new Vector2(0, 76);
		_topBar.CustomMaximumSize = new Vector2(-1, 84);
		MarginContainer margin = GetNode<MarginContainer>("Shell/AppLayout/TopBar/TopBarMargin");
		margin.AddThemeConstantOverride("margin_left", 28);
		margin.AddThemeConstantOverride("margin_right", 28);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_bottom", 10);

		HBoxContainer bar = GetNode<HBoxContainer>("Shell/AppLayout/TopBar/TopBarMargin/HBoxContainer");
		GetNode<Control>("Shell/AppLayout/TopBar/TopBarMargin/HBoxContainer/BrandMark").Visible = false;

		var status = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		status.AddThemeConstantOverride("separation", 16);
		_shellDate = UiKit.Text_("", UiKit.Bold, 26, UiKit.Text);
		_shellDate.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		status.AddChild(_shellDate);
		_shellPhase = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
		_shellPhase.AddThemeStyleboxOverride("panel", UiKit.Box(new Color("#1b1810"), 20, 14, 5, new Color("#3a3424"), 1));
		_shellPhaseText = UiKit.Text_("", UiKit.Semibold, 13, UiKit.Gold);
		_shellPhase.AddChild(_shellPhaseText);
		status.AddChild(_shellPhase);
		bar.AddChild(status);
		bar.MoveChild(status, 1);

		_continueButton = UiKit.Action("CONTINUE", true, OnContinuePressed, 20);
		_continueButton.CustomMinimumSize = new Vector2(190, 48);
		_continueButton.TooltipText = "Advance to the next thing that needs you";
		rightCluster.AddChild(_continueButton);
		rightCluster.AddThemeConstantOverride("separation", 12);

		// Simulate options live under a small arrow beside CONTINUE.
		_simMenu = new MenuButton { Text = "▾", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(44, 48), TooltipText = "Simulate ahead" };
		_simMenu.AddThemeFontOverride("font", UiKit.Bold);
		_simMenu.AddThemeFontSizeOverride("font_size", 20);
		_simMenu.AddThemeColorOverride("font_color", UiKit.Gold);
		_simMenu.AddThemeColorOverride("font_hover_color", UiKit.Text);
		_simMenu.AddThemeStyleboxOverride("normal", UiKit.Box(UiKit.Selected, 10, 8, 6, UiKit.Line, 1));
		_simMenu.AddThemeStyleboxOverride("hover", UiKit.Box(new Color("#232838"), 10, 8, 6, UiKit.Line, 1));
		_simMenu.AddThemeStyleboxOverride("pressed", UiKit.Box(new Color("#232838"), 10, 8, 6, UiKit.Line, 1));
		_simMenu.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		PopupMenu sim = _simMenu.GetPopup();
		sim.AddItem("Simulate day", 1);
		sim.AddItem("Simulate week", 7);
		sim.AddItem("Simulate to next event", 0);
		sim.AddThemeFontOverride("font", UiKit.Semibold);
		sim.AddThemeFontSizeOverride("font_size", 16);
		sim.IdPressed += OnSimulatePicked;
		rightCluster.AddChild(_simMenu);

		var timer = new Godot.Timer { WaitTime = 0.5, Autostart = true };
		timer.Timeout += RefreshShellStatus;
		AddChild(timer);
		RefreshShellStatus();
	}

	private void RefreshShellStatus()
	{
		if (_session == null || _shellDate == null)
		{
			return;
		}

		System.DateOnly date = _session.CurrentDate;
		_shellDate.Text = date.ToString("dddd, MMMM d", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant();
		string phase = (_session.SeasonStatus ?? "").ToUpperInvariant();
		_shellPhaseText.Text = phase;
		_shellPhase.Visible = phase.Length > 0;

		bool canContinue = !_unsignedLock && _homeView is HomePage;
		_continueButton.Visible = canContinue;
		_simMenu.Visible = canContinue;
		if (canContinue && _homeView is HomePage home)
		{
			string label = home.ContinueLabel;
			_continueButton.Text = label is "ADVANCE DAY" ? "CONTINUE" : label;
		}
	}

	private void OnContinuePressed()
	{
		if (_homeView is not HomePage home)
		{
			return;
		}

		if (_activeView != HomeViewKey)
		{
			ShowHome();
		}

		home.Continue();
		RefreshShellStatus();
	}

	private void OnSimulatePicked(long id)
	{
		if (_homeView is not HomePage home)
		{
			return;
		}

		if (_activeView != HomeViewKey)
		{
			ShowHome();
		}

		if (id == 0)
		{
			home.SimulateToNextEvent();
		}
		else
		{
			home.Simulate((int)id);
		}

		RefreshShellStatus();
	}

	private void OpenSection(string section)
	{
		switch (section)
		{
			case ShellRail.Home: ShowHome(); break;
			case ShellRail.Team: ShowRoster(); break;
			case ShellRail.Season: ShowCalendar(); break;
			case ShellRail.League: ShowLeague(); break;
			case ShellRail.Scout: ShowPlayerSearch(); break;
			case ShellRail.Office: ShowCareer(); break;
		}
	}

	private static readonly Dictionary<string, (string Section, string Tab)> ViewSections = new()
	{
		[HomeViewKey] = (ShellRail.Home, ""),
		[RosterViewKey] = (ShellRail.Team, "ROSTER"),
		[LineupsViewKey] = (ShellRail.Team, "DEPTH CHART"),
		[RotationViewKey] = (ShellRail.Team, "PITCHING STAFF"),
		[TrainingViewKey] = (ShellRail.Team, "TRAINING"),
		[InjuriesViewKey] = (ShellRail.Team, "MEDICAL"),
		[CalendarViewKey] = (ShellRail.Season, "CALENDAR"),
		[ScheduleViewKey] = (ShellRail.Season, "SCHEDULE"),
		[TryoutsViewKey] = (ShellRail.Season, "TRYOUTS"),
		[GraduationViewKey] = (ShellRail.Season, "GRADUATION"),
		[GamePreviewViewKey] = (ShellRail.Season, "SCHEDULE"),
		[LiveGameViewKey] = (ShellRail.Season, "SCHEDULE"),
		[LeagueOverviewViewKey] = (ShellRail.League, "DISTRICTS"),
		[StandingsViewKey] = (ShellRail.League, "STANDINGS"),
		[RankingsViewKey] = (ShellRail.League, "RANKINGS"),
		[LeadersViewKey] = (ShellRail.League, "LEADERS"),
		[StatisticsViewKey] = (ShellRail.League, "STATISTICS"),
		[AwardsViewKey] = (ShellRail.League, "AWARDS"),
		[NewsViewKey] = (ShellRail.League, "NEWS"),
		[TransactionsViewKey] = (ShellRail.League, "TRANSACTIONS"),
		[PlayerSearchViewKey] = (ShellRail.Scout, "PLAYER SEARCH"),
		[TargetsViewKey] = (ShellRail.Scout, "MY TARGETS"),
		[CareerViewKey] = (ShellRail.Office, "MY CAREER"),
		[MessagesViewKey] = (ShellRail.Office, "MAIL"),
		[GoalsViewKey] = (ShellRail.Office, "BOARD & GOALS"),
		[BudgetViewKey] = (ShellRail.Office, "BUDGET"),
		[SponsorsViewKey] = (ShellRail.Office, "SPONSORS"),
		[SettingsViewKey] = (ShellRail.Office, "SETTINGS"),
	};

	private List<(string Label, System.Action Open)> TabsFor(string section)
	{
		var tabs = new List<(string Label, System.Action Open)>();
		void Add(string label, System.Action open) => tabs.Add((label, open));
		switch (section)
		{
			case ShellRail.Team:
				Add("ROSTER", ShowRoster);
				Add("DEPTH CHART", ShowLineups);
				Add("PITCHING STAFF", ShowRotation);
				Add("TRAINING", ShowTraining);
				Add("MEDICAL", ShowInjuries);
				break;
			case ShellRail.Season:
				Add("CALENDAR", ShowCalendar);
				Add("SCHEDULE", ShowSchedule);
				Add("TRYOUTS", ShowTryouts);
				Add("GRADUATION", ShowGraduation);
				break;
			case ShellRail.League:
				Add("DISTRICTS", ShowLeague);
				Add("STANDINGS", ShowStandings);
				Add("PLAYOFFS", ShowPlayoffs);
				Add("RANKINGS", ShowRankings);
				Add("LEADERS", ShowLeaders);
				Add("STATISTICS", ShowStatistics);
				Add("AWARDS", ShowAwards);
				Add("NEWS", ShowNews);
				Add("TRANSACTIONS", ShowTransactions);
				break;
			case ShellRail.Scout:
				Add("PLAYER SEARCH", ShowPlayerSearch);
				Add("MY TARGETS", ShowTargets);
				break;
			case ShellRail.Office:
				Add("MY CAREER", ShowCareer);
				if (!_unsignedLock)
				{
					Add("MAIL", ShowMessages);
					Add("BOARD & GOALS", ShowGoals);
					Add("BUDGET", ShowBudget);
					Add("SPONSORS", ShowSponsors);
				}

				Add("SETTINGS", ShowSettings);
				break;
		}

		return tabs;
	}

	private void SyncShell(string viewKey)
	{
		if (_rail == null)
		{
			return;
		}

		string section = _lastSection;
		string tab = "";
		if (ViewSections.TryGetValue(viewKey, out (string Section, string Tab) map))
		{
			section = map.Section;
			tab = map.Tab;
		}

		if (_tabOverride != null)
		{
			tab = _tabOverride;
			_tabOverride = null;
		}

		_lastSection = section;
		_rail.SetLocked(_unsignedLock);
		_rail.SetActive(section);
		_rail.Visible = _topBar.Visible;
		_sectionBar.Visible = _topBar.Visible;
		_sectionBar.SetTabs(TabsFor(section), tab);
		if (!_topBar.Visible)
		{
			_sectionBar.Visible = false;
		}

		RefreshShellStatus();
	}

	private void ReturnToTitle()
	{
		if (_session != null)
		{
			_session.ContractStateChanged -= OnContractStateChanged;
		}

		AudioManager.Current.StopAmbience();
		GetTree().ChangeSceneToFile("res://scenes/main_menu/main_menu.tscn");
	}

	private void QuitGame()
	{
		GetTree().Quit();
	}

	private void OnContractStateChanged()
	{
		_unsignedLock = !_session.HasSignedContract;
		if (_session.HasSignedContract)
		{
			ShowHome();
		}
		else
		{
			ShowCareer();
		}
	}

	private void OnOrganizationItemPressed(long id)
	{
		switch ((int)id)
		{
			case OrganizationRosterId:
				ShowRoster();
				break;
			case OrganizationTransactionsId:
				ShowTransactions();
				break;
			case OrganizationGoalsId:
				ShowGoals();
				break;
			case OrganizationTryoutsId:
				ShowTryouts();
				break;
			case OrganizationGraduationId:
				ShowGraduation();
				break;
			default:
				GD.PushWarning($"AppNavigator: unhandled organization item id '{id}'.");
				break;
		}
	}

	private void OnClubhouseItemPressed(long id)
	{
		switch ((int)id)
		{
			case ClubhouseLineupsId:
				ShowLineups();
				break;
			case ClubhouseRotationId:
				ShowRotation();
				break;
			case ClubhouseInjuriesId:
				ShowInjuries();
				break;
			case ClubhouseTrainingId:
				ShowTraining();
				break;
			default:
				GD.PushWarning($"AppNavigator: unhandled clubhouse item id '{id}'.");
				break;
		}
	}

	private void OnLeagueItemPressed(long id)
	{
		switch ((int)id)
		{
			case LeagueWorldId:
				ShowLeague();
				break;
			case LeagueStandingsId:
				ShowStandings();
				break;
			case LeagueRankingsId:
				ShowRankings();
				break;
			case LeagueLeadersId:
				ShowLeaders();
				break;
			case LeagueStatisticsId:
				ShowStatistics();
				break;
			case LeagueAwardsId:
				ShowAwards();
				break;
			case LeaguePlayoffsId:
				ShowPlayoffs();
				break;
			case LeagueNewsId:
				ShowNews();
				break;
			default:
				GD.PushWarning($"AppNavigator: unhandled league item id '{id}'.");
				break;
		}
	}

	private void OnFinancesItemPressed(long id)
	{
		switch ((int)id)
		{
			case FinancesBudgetId:
				ShowBudget();
				break;
			case FinancesSponsorsId:
				ShowSponsors();
				break;
			default:
				GD.PushWarning($"AppNavigator: unhandled finances item id '{id}'.");
				break;
		}
	}

	private void OnScoutingItemPressed(long id)
	{
		switch ((int)id)
		{
			case ScoutingPlayerSearchId:
				ShowPlayerSearch();
				break;
			case ScoutingTargetsId:
				ShowTargets();
				break;
			default:
				GD.PushWarning($"AppNavigator: unhandled scouting item id '{id}'.");
				break;
		}
	}

	private void ShowView(string viewKey, string topBarKey, bool playSound = true)
	{
		if (_unsignedLock && viewKey != CareerViewKey && viewKey != SettingsViewKey)
		{
			viewKey = CareerViewKey;
			topBarKey = TopBarNavigation.CareerKey;
		}

		if (!_views.ContainsKey(viewKey))
		{
			GD.PushWarning($"AppNavigator: unknown view '{viewKey}'.");
			return;
		}

		bool changed = _activeView != viewKey;
		_activeView = viewKey;
		if (changed)
		{
			_profileDock?.Dismiss();
		}

		foreach (KeyValuePair<string, Control> pair in _views)
		{
			pair.Value.Visible = pair.Key == viewKey;
		}

		_topBar.Visible = viewKey is not (GamePreviewViewKey or LiveGameViewKey or ChampionshipViewKey or SeasonReviewViewKey or HallOfFameViewKey or SeasonRolloverViewKey)
			&& !(viewKey == AwardsViewKey && _awardsCeremony);
		_topBar.ApplyCareerShell(
			hideClubhouseNav: _unsignedLock || viewKey == CareerViewKey,
			lockToCareer: _unsignedLock);
		if (_topBar.Visible)
		{
			_topBar.SetActiveNavigation(topBarKey);
		}

		RefreshInboxBadge();
		SyncShell(viewKey);

		if (_navigationSoundsEnabled && changed && playSound)
		{
			UiSounds.Play(UiSound.Navigate);
		}
	}

	private void RefreshInboxBadge()
	{
		if (_topBar == null || _session == null)
		{
			return;
		}

		_topBar.SetMessagesUnread(MessagesDesk.UnreadCount(_session));
	}

	private Control BuildPlaceholderView()
	{
		var root = new Control
		{
			Name = "PlaceholderView",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		root.SizeFlagsVertical = SizeFlags.ExpandFill;

		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		root.AddChild(margin);

		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", MakeCardStyle());
		margin.AddChild(card);

		var layout = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		layout.AddThemeConstantOverride("separation", 8);
		card.AddChild(layout);

		_placeholderTitle = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		_placeholderTitle.AddThemeFontSizeOverride("font_size", 22);
		_placeholderTitle.AddThemeColorOverride("font_color", new Color(0.956863f, 0.964706f, 0.972549f));
		layout.AddChild(_placeholderTitle);

		_placeholderBody = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		_placeholderBody.AddThemeFontSizeOverride("font_size", 13);
		_placeholderBody.AddThemeColorOverride("font_color", new Color(0.55f, 0.58f, 0.64f));
		layout.AddChild(_placeholderBody);

		return root;
	}

	private static StyleBoxFlat MakeCardStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			BorderColor = new Color(0.137255f, 0.152941f, 0.200000f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 24,
			ContentMarginTop = 24,
			ContentMarginRight = 24,
			ContentMarginBottom = 24,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerDetail = 3,
		};
	}
}
