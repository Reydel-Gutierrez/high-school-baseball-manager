using System.Collections.Generic;
using Godot;

/// <summary>
/// Persistent-shell navigator: the top bar stays mounted and content pages
/// are shown or hidden under ContentArea. Prefer this over ChangeScene
/// (which would rebuild chrome) or hiding individual home cards.
/// </summary>
public partial class AppNavigator : MarginContainer
{
	public const string HomeViewKey = "home";
	public const string CalendarViewKey = "calendar";
	public const string RosterViewKey = "roster";
	public const string TradesViewKey = "trades";
	public const string TransactionsViewKey = "transactions";
	public const string GoalsViewKey = "goals";
	public const string LineupsViewKey = "lineups";
	public const string RotationViewKey = "rotation";
	public const string StandingsViewKey = "standings";
	public const string LeadersViewKey = "leaders";
	public const string AwardsViewKey = "awards";
	public const string StatisticsViewKey = "statistics";
	public const string NewsViewKey = "news";
	public const string PlayerSearchViewKey = "player-search";
	public const string TargetsViewKey = "targets";
	public const string CareerViewKey = "career";
	public const string SettingsViewKey = "settings";
	public const string PlaceholderViewKey = "placeholder";

	public const int OrganizationRosterId = 100;
	public const int OrganizationTradesId = 101;
	public const int OrganizationTransactionsId = 102;
	public const int OrganizationGoalsId = 103;
	public const int ClubhouseLineupsId = 200;
	public const int ClubhouseRotationId = 201;
	public const int DistrictStandingsId = 300;
	public const int DistrictLeadersId = 301;
	public const int DistrictAwardsId = 302;
	public const int DistrictStatisticsId = 303;
	public const int DistrictNewsId = 304;
	public const int ScoutingPlayerSearchId = 500;
	public const int ScoutingTradesId = 501;
	public const int ScoutingTargetsId = 502;

	private TopBarNavigation _topBar = null!;
	private Control _homeView = null!;
	private Control _calendarView = null!;
	private Control _rosterView = null!;
	private Control _tradesView = null!;
	private Control _transactionsView = null!;
	private Control _goalsView = null!;
	private Control _lineupsView = null!;
	private Control _rotationView = null!;
	private Control _standingsView = null!;
	private Control _leadersView = null!;
	private Control _awardsView = null!;
	private Control _statisticsView = null!;
	private Control _newsView = null!;
	private Control _playerSearchView = null!;
	private Control _targetsView = null!;
	private Control _careerView = null!;
	private Control _settingsView = null!;
	private Control _placeholderView = null!;
	private Label _placeholderTitle = null!;
	private Label _placeholderBody = null!;

	private readonly Dictionary<string, Control> _views = new();
	private string _activeView = HomeViewKey;
	private bool _navigationSoundsEnabled;

	public override void _Ready()
	{
		_topBar = GetNode<TopBarNavigation>("AppLayout/TopBar");
		_homeView = GetNode<Control>("AppLayout/ContentArea/ContentRoot");
		_calendarView = GetNode<Control>("AppLayout/ContentArea/CalendarView");
		_rosterView = GetNode<Control>("AppLayout/ContentArea/RosterView");
		_tradesView = GetNode<Control>("AppLayout/ContentArea/TradesView");
		_transactionsView = GetNode<Control>("AppLayout/ContentArea/TransactionsView");
		_goalsView = GetNode<Control>("AppLayout/ContentArea/GoalsView");
		_lineupsView = GetNode<Control>("AppLayout/ContentArea/LineupsView");
		_rotationView = GetNode<Control>("AppLayout/ContentArea/RotationView");
		_standingsView = GetNode<Control>("AppLayout/ContentArea/StandingsView");
		_leadersView = GetNode<Control>("AppLayout/ContentArea/LeadersView");
		_awardsView = GetNode<Control>("AppLayout/ContentArea/AwardsView");
		_statisticsView = GetNode<Control>("AppLayout/ContentArea/StatisticsView");
		_newsView = GetNode<Control>("AppLayout/ContentArea/NewsView");
		_playerSearchView = GetNode<Control>("AppLayout/ContentArea/PlayerSearchView");
		_targetsView = GetNode<Control>("AppLayout/ContentArea/TargetsView");
		_careerView = GetNode<Control>("AppLayout/ContentArea/CareerView");
		_settingsView = GetNode<Control>("AppLayout/ContentArea/SettingsView");

		_placeholderView = BuildPlaceholderView();
		GetNode("AppLayout/ContentArea").AddChild(_placeholderView);

		_views[HomeViewKey] = _homeView;
		_views[CalendarViewKey] = _calendarView;
		_views[RosterViewKey] = _rosterView;
		_views[TradesViewKey] = _tradesView;
		_views[TransactionsViewKey] = _transactionsView;
		_views[GoalsViewKey] = _goalsView;
		_views[LineupsViewKey] = _lineupsView;
		_views[RotationViewKey] = _rotationView;
		_views[StandingsViewKey] = _standingsView;
		_views[LeadersViewKey] = _leadersView;
		_views[AwardsViewKey] = _awardsView;
		_views[StatisticsViewKey] = _statisticsView;
		_views[NewsViewKey] = _newsView;
		_views[PlayerSearchViewKey] = _playerSearchView;
		_views[TargetsViewKey] = _targetsView;
		_views[CareerViewKey] = _careerView;
		_views[SettingsViewKey] = _settingsView;
		_views[PlaceholderViewKey] = _placeholderView;

		ConnectChrome();
		ShowView(HomeViewKey, TopBarNavigation.HomeKey);
		AudioManager.Current.StartSoundtrack();
		_navigationSoundsEnabled = true;
	}

	public void ShowHome()
	{
		ShowView(HomeViewKey, TopBarNavigation.HomeKey);
	}

	public void ShowCalendar()
	{
		ShowView(CalendarViewKey, TopBarNavigation.CalendarKey);
		if (_calendarView is CalendarPage page)
		{
			page.FocusCurrentDate();
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

	public void ShowTransactions()
	{
		ShowView(TransactionsViewKey, TopBarNavigation.OrganizationKey);
	}

	public void ShowGoals()
	{
		ShowView(GoalsViewKey, TopBarNavigation.OrganizationKey);
	}

	public void ShowLineups()
	{
		ShowView(LineupsViewKey, TopBarNavigation.ClubhouseKey);
	}

	public void ShowRotation()
	{
		ShowView(RotationViewKey, TopBarNavigation.ClubhouseKey);
	}

	public void ShowStandings()
	{
		ShowView(StandingsViewKey, TopBarNavigation.DistrictHubKey);
	}

	public void ShowLeaders()
	{
		ShowView(LeadersViewKey, TopBarNavigation.DistrictHubKey);
	}

	public void ShowAwards()
	{
		ShowView(AwardsViewKey, TopBarNavigation.DistrictHubKey);
	}

	public void ShowStatistics()
	{
		ShowView(StatisticsViewKey, TopBarNavigation.DistrictHubKey);
	}

	public void ShowNews()
	{
		ShowView(NewsViewKey, TopBarNavigation.DistrictHubKey);
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

		MenuButton organizationMenu = GetNode<MenuButton>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/OrganizationMenu");
		organizationMenu.GetPopup().IdPressed += OnOrganizationItemPressed;

		MenuButton clubhouseMenu = GetNode<MenuButton>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/ClubhouseMenu");
		clubhouseMenu.GetPopup().IdPressed += OnClubhouseItemPressed;

		MenuButton districtHubMenu = GetNode<MenuButton>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/DistrictHubMenu");
		districtHubMenu.GetPopup().IdPressed += OnDistrictHubItemPressed;

		MenuButton scoutingMenu = GetNode<MenuButton>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/ScoutingMenu");
		scoutingMenu.GetPopup().IdPressed += OnScoutingItemPressed;

		Button careerButton = GetNode<Button>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/CareerButton");
		careerButton.Pressed += ShowCareer;

		Button settingsButton = GetNode<Button>("AppLayout/TopBar/TopBarMargin/HBoxContainer/RightCluster/SettingsButton");
		settingsButton.Pressed += ShowSettings;
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
			default:
				GD.PushWarning($"AppNavigator: unhandled clubhouse item id '{id}'.");
				break;
		}
	}

	private void OnDistrictHubItemPressed(long id)
	{
		switch ((int)id)
		{
			case DistrictStandingsId:
				ShowStandings();
				break;
			case DistrictLeadersId:
				ShowLeaders();
				break;
			case DistrictAwardsId:
				ShowAwards();
				break;
			case DistrictStatisticsId:
				ShowStatistics();
				break;
			case DistrictNewsId:
				ShowNews();
				break;
			default:
				GD.PushWarning($"AppNavigator: unhandled district hub item id '{id}'.");
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
			default:
				GD.PushWarning($"AppNavigator: unhandled scouting item id '{id}'.");
				break;
		}
	}

	private void ShowView(string viewKey, string topBarKey)
	{
		if (!_views.ContainsKey(viewKey))
		{
			GD.PushWarning($"AppNavigator: unknown view '{viewKey}'.");
			return;
		}

		bool changed = _activeView != viewKey;
		_activeView = viewKey;

		foreach (KeyValuePair<string, Control> pair in _views)
		{
			pair.Value.Visible = pair.Key == viewKey;
		}

		_topBar.SetActiveNavigation(topBarKey);

		if (_navigationSoundsEnabled && changed)
		{
			UiSounds.Play(UiSound.Navigate);
		}
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
