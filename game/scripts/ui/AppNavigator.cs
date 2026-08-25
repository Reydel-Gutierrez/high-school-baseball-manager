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
	public const string TradesViewKey = "trades";
	public const string TransactionsViewKey = "transactions";
	public const string GoalsViewKey = "goals";
	public const string TryoutsViewKey = "tryouts";
	public const string IncomingClassViewKey = "incoming-class";
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
	public const string OpenPoolViewKey = "open-pool";
	public const string BudgetViewKey = "budget";
	public const string SponsorsViewKey = "sponsors";
	public const string CareerViewKey = "career";
	public const string SettingsViewKey = "settings";
	public const string PlaceholderViewKey = "placeholder";
	public const string GamePreviewViewKey = "game-preview";
	public const string ChampionshipViewKey = "championship";
	public const string SeasonReviewViewKey = "season-review";
	public const string HallOfFameViewKey = "hall-of-fame";

	public const int OrganizationRosterId = 100;
	public const int OrganizationTradesId = 101;
	public const int OrganizationTransactionsId = 102;
	public const int OrganizationGoalsId = 103;
	public const int OrganizationTryoutsId = 104;
	public const int OrganizationIncomingClassId = 105;
	public const int OrganizationGraduationId = 106;
	public const int ClubhouseLineupsId = 200;
	public const int ClubhouseRotationId = 201;
	public const int ClubhouseInjuriesId = 202;
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
	public const int ScoutingTradesId = 501;
	public const int ScoutingTargetsId = 502;
	public const int ScoutingOpenPoolId = 503;

	private TopBarNavigation _topBar = null!;
	private Control _homeView = null!;
	private Control _calendarView = null!;
	private Control _scheduleView = null!;
	private Control _rosterView = null!;
	private Control _tradesView = null!;
	private Control _transactionsView = null!;
	private Control _goalsView = null!;
	private Control _tryoutsView = null!;
	private Control _incomingClassView = null!;
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
	private Control _openPoolView = null!;
	private Control _budgetView = null!;
	private Control _sponsorsView = null!;
	private Control _careerView = null!;
	private Control _settingsView = null!;
	private Control _placeholderView = null!;
	private Label _placeholderTitle = null!;
	private Label _placeholderBody = null!;
	private ProfileDock _profileDock = null!;
	private GamePreviewPage _gamePreviewPage = null!;
	private ChampionshipCelebrationPage _championshipPage = null!;
	private SeasonReviewPage _seasonReviewPage = null!;
	private HallOfFameInductionPage _hallOfFamePage = null!;
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
		_topBar = GetNode<TopBarNavigation>("AppLayout/TopBar");
		_homeView = GetNode<Control>("AppLayout/ContentArea/ContentRoot");
		_calendarView = GetNode<Control>("AppLayout/ContentArea/CalendarView");
		_scheduleView = GetNode<Control>("AppLayout/ContentArea/ScheduleView");
		_rosterView = GetNode<Control>("AppLayout/ContentArea/RosterView");
		_tradesView = GetNode<Control>("AppLayout/ContentArea/TradesView");
		_transactionsView = GetNode<Control>("AppLayout/ContentArea/TransactionsView");
		_goalsView = GetNode<Control>("AppLayout/ContentArea/GoalsView");
		_tryoutsView = GetNode<Control>("AppLayout/ContentArea/TryoutsView");
		_incomingClassView = GetNode<Control>("AppLayout/ContentArea/IncomingClassView");
		_graduationView = GetNode<Control>("AppLayout/ContentArea/GraduationView");
		_lineupsView = GetNode<Control>("AppLayout/ContentArea/LineupsView");
		_rotationView = GetNode<Control>("AppLayout/ContentArea/RotationView");
		_injuriesView = GetNode<Control>("AppLayout/ContentArea/InjuriesView");
		_leagueOverviewPage = GetNode<LeagueOverviewPage>("AppLayout/ContentArea/LeagueOverviewView");
		_standingsPage = GetNode<StandingsPage>("AppLayout/ContentArea/StandingsView");
		_rankingsPage = GetNode<RankingsPage>("AppLayout/ContentArea/RankingsView");
		_leadersView = GetNode<Control>("AppLayout/ContentArea/LeadersView");
		_awardsPage = GetNode<AwardsPage>("AppLayout/ContentArea/AwardsView");
		_awardsPage.Closed += OnAwardsClosed;
		_awardsPage.Continued += OnAwardsContinued;
		_statisticsView = GetNode<Control>("AppLayout/ContentArea/StatisticsView");
		_newsView = GetNode<Control>("AppLayout/ContentArea/NewsView");
		_messagesView = GetNode<Control>("AppLayout/ContentArea/MessagesView");
		_playerSearchView = GetNode<Control>("AppLayout/ContentArea/PlayerSearchView");
		_targetsView = GetNode<Control>("AppLayout/ContentArea/TargetsView");
		_openPoolView = GetNode<Control>("AppLayout/ContentArea/OpenPoolView");
		_budgetView = GetNode<Control>("AppLayout/ContentArea/BudgetView");
		_sponsorsView = GetNode<Control>("AppLayout/ContentArea/SponsorsView");
		_careerView = GetNode<Control>("AppLayout/ContentArea/CareerView");
		_settingsView = GetNode<Control>("AppLayout/ContentArea/SettingsView");

		_placeholderView = BuildPlaceholderView();
		GetNode("AppLayout/ContentArea").AddChild(_placeholderView);

		_profileDock = new ProfileDock
		{
			Name = "ProfileDock",
		};
		GetNode("AppLayout/ContentArea").AddChild(_profileDock);

		_gamePreviewPage = GetNode<GamePreviewPage>("AppLayout/ContentArea/GamePreviewView");
		_gamePreviewPage.Closed += ReturnFromGamePreview;
		_gamePreviewPage.EditLineupRequested += ShowLineups;
		_gamePreviewPage.EditRotationRequested += ShowRotation;

		_championshipPage = GetNode<ChampionshipCelebrationPage>("AppLayout/ContentArea/ChampionshipView");
		_championshipPage.Closed += ReturnFromFullscreenDesk;
		_championshipPage.Continued += OnChampionshipContinued;

		_seasonReviewPage = GetNode<SeasonReviewPage>("AppLayout/ContentArea/SeasonReviewView");
		_seasonReviewPage.Closed += ReturnFromFullscreenDesk;
		_seasonReviewPage.Continued += OnSeasonReviewContinued;
		_hallOfFamePage = GetNode<HallOfFameInductionPage>("AppLayout/ContentArea/HallOfFameView");
		_hallOfFamePage.Closed += ReturnFromFullscreenDesk;
		_hallOfFamePage.Continued += OnHallOfFameContinued;

		_views[HomeViewKey] = _homeView;
		_views[CalendarViewKey] = _calendarView;
		_views[ScheduleViewKey] = _scheduleView;
		_views[RosterViewKey] = _rosterView;
		_views[TradesViewKey] = _tradesView;
		_views[TransactionsViewKey] = _transactionsView;
		_views[GoalsViewKey] = _goalsView;
		_views[TryoutsViewKey] = _tryoutsView;
		_views[IncomingClassViewKey] = _incomingClassView;
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
		_views[OpenPoolViewKey] = _openPoolView;
		_views[BudgetViewKey] = _budgetView;
		_views[SponsorsViewKey] = _sponsorsView;
		_views[CareerViewKey] = _careerView;
		_views[SettingsViewKey] = _settingsView;
		_views[PlaceholderViewKey] = _placeholderView;
		_views[GamePreviewViewKey] = _gamePreviewPage;
		_views[ChampionshipViewKey] = _championshipPage;
		_views[SeasonReviewViewKey] = _seasonReviewPage;
		_views[HallOfFameViewKey] = _hallOfFamePage;

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
		if (_activeView is ChampionshipViewKey or SeasonReviewViewKey or HallOfFameViewKey or GamePreviewViewKey
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

	private void ReturnFromFullscreenDesk()
	{
		string view = _previewReturnView;
		string topBar = _previewReturnTopBar;
		if (string.IsNullOrEmpty(view)
			|| view is ChampionshipViewKey or SeasonReviewViewKey or HallOfFameViewKey or GamePreviewViewKey
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
		if (string.IsNullOrEmpty(view) || view == GamePreviewViewKey || !_views.ContainsKey(view))
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

	public void ShowTrades()
	{
		ShowTrades(TopBarNavigation.OrganizationKey);
	}

	public void ShowTrades(string topBarKey)
	{
		ShowView(TradesViewKey, topBarKey);
	}

	public void ShowScoutingTrades(string teamId, string firstName, string lastName)
	{
		ShowView(TradesViewKey, TopBarNavigation.ScoutingKey);
		if (_tradesView is TradesPage trades)
		{
			trades.OpenForPlayer(teamId, firstName, lastName);
		}
	}

	public void ShowPlayerSearch()
	{
		ShowView(PlayerSearchViewKey, TopBarNavigation.ScoutingKey);
	}

	public void ShowTargets()
	{
		ShowView(TargetsViewKey, TopBarNavigation.ScoutingKey);
	}

	public void ShowOpenPool()
	{
		ShowView(OpenPoolViewKey, TopBarNavigation.ScoutingKey);
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

	public void ShowIncomingClass()
	{
		ShowView(IncomingClassViewKey, TopBarNavigation.OrganizationKey);
	}

	public void ShowGraduation()
	{
		ShowView(GraduationViewKey, TopBarNavigation.OrganizationKey);
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
		ShowStandings();
	}

	public void ShowPlayoffs()
	{
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

		if (_activeView == RankingsViewKey)
		{
			_rankingsPage.SelectSchool(id);
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
			case InboxDestination.OpenPool:
				ShowOpenPool();
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
		Button calendarButton = GetNode<Button>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/CalendarButton");
		calendarButton.Pressed += ShowCalendar;

		Button homeButton = GetNode<Button>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/HomeButton");
		homeButton.Pressed += ShowHome;

		Button messagesButton = GetNode<Button>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/MessagesButton");
		messagesButton.Pressed += ShowMessages;

		MenuButton organizationMenu = GetNode<MenuButton>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/OrganizationMenu");
		organizationMenu.GetPopup().IdPressed += OnOrganizationItemPressed;

		MenuButton clubhouseMenu = GetNode<MenuButton>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/ClubhouseMenu");
		clubhouseMenu.GetPopup().IdPressed += OnClubhouseItemPressed;

		MenuButton leagueMenu = GetNode<MenuButton>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/DistrictHubMenu");
		leagueMenu.GetPopup().IdPressed += OnLeagueItemPressed;

		MenuButton scoutingMenu = GetNode<MenuButton>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/ScoutingMenu");
		scoutingMenu.GetPopup().IdPressed += OnScoutingItemPressed;

		MenuButton financesMenu = GetNode<MenuButton>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/FinancesMenu");
		financesMenu.GetPopup().IdPressed += OnFinancesItemPressed;

		Button careerButton = GetNode<Button>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/CareerButton");
		careerButton.Pressed += ShowCareer;

		Button settingsButton = GetNode<Button>("AppLayout/TopBar/TopBarMargin/HBoxContainer/RightCluster/SettingsButton");
		settingsButton.Pressed += ShowSettings;
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
			case OrganizationTradesId:
				ShowTrades();
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
			case OrganizationIncomingClassId:
				ShowIncomingClass();
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
			case ScoutingTradesId:
				ShowTrades(TopBarNavigation.ScoutingKey);
				break;
			case ScoutingTargetsId:
				ShowTargets();
				break;
			case ScoutingOpenPoolId:
				ShowOpenPool();
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

		_topBar.Visible = viewKey is not (GamePreviewViewKey or ChampionshipViewKey or SeasonReviewViewKey or HallOfFameViewKey)
			&& !(viewKey == AwardsViewKey && _awardsCeremony);
		_topBar.ApplyCareerShell(
			hideClubhouseNav: _unsignedLock || viewKey == CareerViewKey,
			lockToCareer: _unsignedLock);
		if (_topBar.Visible)
		{
			_topBar.SetActiveNavigation(topBarKey);
		}

		RefreshInboxBadge();

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
			BgColor = new Color(0.070588f, 0.078431f, 0.094118f, 0.94f),
			BorderColor = new Color(0.243137f, 0.262745f, 0.301961f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 24,
			ContentMarginTop = 24,
			ContentMarginRight = 24,
			ContentMarginBottom = 24,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		};
	}
}
