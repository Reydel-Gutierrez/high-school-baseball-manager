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
	public const string RosterViewKey = "roster";
	public const string PlaceholderViewKey = "placeholder";

	public const int OrganizationRosterId = 100;
	public const int OrganizationTradesId = 101;
	public const int OrganizationTransactionsId = 102;
	public const int OrganizationGoalsId = 103;

	private TopBarNavigation _topBar = null!;
	private Control _homeView = null!;
	private Control _rosterView = null!;
	private Control _placeholderView = null!;
	private Label _placeholderTitle = null!;
	private Label _placeholderBody = null!;

	private readonly Dictionary<string, Control> _views = new();
	private string _activeView = HomeViewKey;

	public override void _Ready()
	{
		_topBar = GetNode<TopBarNavigation>("AppLayout/TopBar");
		_homeView = GetNode<Control>("AppLayout/ContentArea/ContentRoot");
		_rosterView = GetNode<Control>("AppLayout/ContentArea/RosterView");

		_placeholderView = BuildPlaceholderView();
		GetNode("AppLayout/ContentArea").AddChild(_placeholderView);

		_views[HomeViewKey] = _homeView;
		_views[RosterViewKey] = _rosterView;
		_views[PlaceholderViewKey] = _placeholderView;

		ConnectChrome();
		ShowView(HomeViewKey, TopBarNavigation.HomeKey);
	}

	public void ShowHome()
	{
		ShowView(HomeViewKey, TopBarNavigation.HomeKey);
	}

	public void ShowRoster()
	{
		ShowView(RosterViewKey, TopBarNavigation.OrganizationKey);
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
		Button homeButton = GetNode<Button>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/HomeButton");
		homeButton.Pressed += ShowHome;

		MenuButton organizationMenu = GetNode<MenuButton>("AppLayout/TopBar/TopBarMargin/HBoxContainer/NavigationRow/OrganizationMenu");
		organizationMenu.GetPopup().IdPressed += OnOrganizationItemPressed;
	}

	private void OnOrganizationItemPressed(long id)
	{
		switch ((int)id)
		{
			case OrganizationRosterId:
				ShowRoster();
				break;
			case OrganizationTradesId:
				ShowPlaceholder("TRADES", "The trade desk is coming soon.");
				break;
			case OrganizationTransactionsId:
				ShowPlaceholder("TRANSACTIONS", "Transaction history is coming soon.");
				break;
			case OrganizationGoalsId:
				ShowPlaceholder("GOALS & SATISFACTION", "Program goals are coming soon.");
				break;
			default:
				GD.PushWarning($"AppNavigator: unhandled organization item id '{id}'.");
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

		_activeView = viewKey;

		foreach (KeyValuePair<string, Control> pair in _views)
		{
			pair.Value.Visible = pair.Key == viewKey;
		}

		_topBar.SetActiveNavigation(topBarKey);
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
