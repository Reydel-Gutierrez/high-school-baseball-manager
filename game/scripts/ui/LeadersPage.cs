using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// League leaders (concept 14). Opens on a board of six leader cards (the
/// leader's face and number, then the next three); "TOP 25" on a card, or the
/// BATTING / PITCHING header tabs, open the full ranked table for one stat.
/// </summary>
public partial class LeadersPage : Control
{
	private const string ViewBatting = "batting";
	private const string ViewPitching = "pitching";

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.772549f, 0.635294f, 0.325490f, 1f);
	private static readonly Color AccentHover = new(0.850980f, 0.729412f, 0.443137f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color CardBorder = new(0.137255f, 0.152941f, 0.200000f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color UserRow = new(0.772549f, 0.635294f, 0.325490f, 0.14f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;

	/// <summary>The six boards: group, stat key, card title.</summary>
	private static readonly (string Group, string Stat, string Title)[] Boards =
	[
		(ViewBatting, "AVG", "BATTING AVERAGE"),
		(ViewBatting, "HR", "HOME RUNS"),
		(ViewBatting, "SB", "STOLEN BASES"),
		(ViewPitching, "SO", "STRIKEOUTS"),
		(ViewPitching, "ERA", "ERA"),
		(ViewBatting, "RBI", "RBI"),
	];

	private readonly Dictionary<string, Button> _statButtons = new();

	private Label _subtitle = null!;
	private Control _tabsHost = null!;
	private HFlowContainer _statRow = null!;
	private Control _listCard = null!;
	private VBoxContainer _boardHost = null!;
	private bool _board = true;
	private Label _tableTitle = null!;
	private Label _statHeader = null!;
	private VBoxContainer _rowsHost = null!;
	private string _activeGroup = ViewBatting;
	private string _activeStat = "AVG";

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		_session.CareerChanged += Refresh;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
			_session.CareerChanged -= Refresh;
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => Refresh();

	private void BuildPage()
	{
		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		AddChild(margin);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 12);
		margin.AddChild(layout);

		_tabsHost = new HBoxContainer();
		layout.AddChild(UiKit.PageHeader($"LEAGUE  ·  {DistrictHubData.SeasonYear}", "LEADERS & STATS", " ", out _subtitle, _tabsHost));

		_statRow = new HFlowContainer();
		_statRow.AddThemeConstantOverride("h_separation", 4);
		_statRow.AddThemeConstantOverride("v_separation", 4);
		layout.AddChild(_statRow);

		_listCard = BuildListCard();
		layout.AddChild(_listCard);

		_boardHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
		_boardHost.AddThemeConstantOverride("separation", 12);
		layout.AddChild(_boardHost);
	}

	private Control BuildListCard()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Card, 16, 0, 6, UiKit.Line, 1));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);

		var heading = new PanelContainer();
		heading.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		});
		_tableTitle = MakeText("BATTING AVERAGE", _bold, 16, TextPrimary, HorizontalAlignment.Left);
		heading.AddChild(_tableTitle);
		layout.AddChild(heading);
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		layout.AddChild(BuildColumnHeader());

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);

		_rowsHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_rowsHost.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(_rowsHost);
		return card;
	}

	private Control BuildColumnHeader()
	{
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			BorderColor = CardBorder,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 7,
			ContentMarginRight = 12,
			ContentMarginBottom = 7,
		});

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(MakeFixed("#", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		var player = MakeText("PLAYER", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		player.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(player);
		columns.AddChild(MakeFixed("POS", 44, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed("TEAM", 120, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		_statHeader = MakeFixed("AVG", 64, _semibold, 10, TextMuted, HorizontalAlignment.Right);
		columns.AddChild(_statHeader);
		header.AddChild(columns);
		return header;
	}

	private void Refresh()
	{
		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		HubStatTab tab = FindTab(_activeStat);
		_subtitle.Visible = true;
		string level = varsity ? "Varsity" : "JV  ·  varsity boards until JV box scores land";
		_subtitle.Text = _board ? $"{level}  ·  all four districts" : $"{level}  ·  {tab.Title}  ·  all four districts";
		_tableTitle.Text = tab.Title.ToUpperInvariant();
		_statHeader.Text = tab.Label;

		RebuildTabs();
		_statRow.Visible = !_board;
		_listCard.Visible = !_board;
		_boardHost.Visible = _board;
		if (_board)
		{
			RebuildBoard();
			return;
		}

		RebuildStatTabs();
		RebuildRows();
	}

	private void RebuildTabs()
	{
		foreach (Node child in _tabsHost.GetChildren())
		{
			_tabsHost.RemoveChild(child);
			child.QueueFree();
		}

		string active = _board ? "LEADERS" : _activeGroup == ViewPitching ? "PITCHING" : "BATTING";
		_tabsHost.AddChild(UiKit.HeaderTabs(
			[
				("LEADERS", ShowBoard),
				("BATTING", () => ShowList(ViewBatting, "AVG")),
				("PITCHING", () => ShowList(ViewPitching, "ERA")),
			],
			active));
	}

	private void ShowBoard()
	{
		_board = true;
		Refresh();
	}

	private void ShowList(string group, string stat)
	{
		_board = false;
		_activeGroup = group;
		_activeStat = stat;
		Refresh();
	}

	private void RebuildBoard()
	{
		foreach (Node child in _boardHost.GetChildren())
		{
			_boardHost.RemoveChild(child);
			child.QueueFree();
		}

		HBoxContainer? line = null;
		for (int i = 0; i < Boards.Length; i++)
		{
			if (i % 3 == 0)
			{
				line = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
				line.AddThemeConstantOverride("separation", 12);
				_boardHost.AddChild(line);
			}

			line!.AddChild(BuildBoardCard(Boards[i].Group, Boards[i].Stat, Boards[i].Title));
		}
	}

	/// <summary>Leader with face and big number, then places two to four.</summary>
	private Control BuildBoardCard(string group, string stat, string title)
	{
		PanelContainer card = UiKit.MakeCard(18, 12);
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 4);
		card.AddChild(col);
		col.AddChild(UiKit.CardHeader(title, "TOP 25", () => ShowList(group, stat)));

		List<HubLeader> rows = DistrictHubData.LeadersFor(group, stat);
		if (rows.Count == 0)
		{
			Label empty = UiKit.Text_("No qualified leaders yet. The boards fill in once games are played.", UiKit.Medium, 14, UiKit.Muted);
			empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			col.AddChild(empty);
			return card;
		}

		HubLeader top = rows[0];
		var hero = new HBoxContainer();
		hero.AddThemeConstantOverride("separation", 14);
		Control? face = PlayerFaces.For(top.TeamId, top.Name, 64, 64, true, _session.ActiveTeam.Level);
		if (face != null)
		{
			face.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			hero.AddChild(face);
		}

		var who = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		who.AddThemeConstantOverride("separation", -6);
		who.AddChild(UiKit.Kicker($"{top.TeamShort.ToUpperInvariant()}  ·  {top.Position}", UiKit.Muted));
		Label name = UiKit.Text_(top.Name.ToUpperInvariant(), UiKit.Bold, 22, top.TeamId == DistrictHubData.UserTeamId ? UiKit.Gold : UiKit.Text);
		name.MouseFilter = MouseFilterEnum.Stop;
		PlayerLinks.MakeClickable(name, top.TeamId, top.Name);
		who.AddChild(name);
		who.AddChild(UiKit.Text_(top.Display, UiKit.Bold, 32, UiKit.Gold));
		hero.AddChild(who);
		col.AddChild(hero);
		col.AddChild(UiKit.Hairline());

		for (int i = 1; i < rows.Count && i < 4; i++)
		{
			HubLeader row = rows[i];
			var line = new HBoxContainer();
			line.AddThemeConstantOverride("separation", 10);
			line.AddChild(UiKit.Cell($"{i + 1}", 16, UiKit.Semibold, 14, UiKit.Muted));
			Label rowName = UiKit.Text_(row.Name, UiKit.Semibold, 15, row.TeamId == DistrictHubData.UserTeamId ? UiKit.Gold : UiKit.Text);
			rowName.MouseFilter = MouseFilterEnum.Stop;
			PlayerLinks.MakeClickable(rowName, row.TeamId, row.Name);
			line.AddChild(rowName);
			line.AddChild(UiKit.Cell(row.TeamShort, 0, UiKit.Medium, 13, UiKit.Muted));
			line.AddChild(UiKit.Cell(row.Display, 60, UiKit.Bold, 16, UiKit.Text, HorizontalAlignment.Right));
			col.AddChild(line);
		}

		return card;
	}

	private void RebuildStatTabs()
	{
		foreach (Node child in _statRow.GetChildren())
		{
			_statRow.RemoveChild(child);
			child.QueueFree();
		}

		_statButtons.Clear();
		foreach (HubStatTab tab in DistrictHubData.StatsFor(_activeGroup))
		{
			var button = new Button { Text = tab.Label };
			string key = tab.Key;
			button.Pressed += () =>
			{
				_activeStat = key;
				Refresh();
			};
			_statButtons[tab.Key] = button;
			_statRow.AddChild(button);
		}

		StylePills(_statButtons, _activeStat);
	}

	private void RebuildRows()
	{
		foreach (Node child in _rowsHost.GetChildren())
		{
			_rowsHost.RemoveChild(child);
			child.QueueFree();
		}

		List<HubLeader> rows = DistrictHubData.LeadersFor(_activeGroup, _activeStat);
		for (int i = 0; i < rows.Count; i++)
		{
			_rowsHost.AddChild(BuildRow(rows[i], i + 1, i % 2 == 0));
		}
	}

	private Control BuildRow(HubLeader row, int rank, bool even)
	{
		bool user = row.TeamId == DistrictHubData.UserTeamId;
		var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = user ? UserRow : even ? RowEven : RowOdd,
			BorderColor = user ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = user ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		});

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(MakeFixed($"{rank}", 28, _bold, 14, rank == 1 ? Accent : TextMuted, HorizontalAlignment.Center));

		var name = MakeText(row.Name, user ? _bold : _semibold, 15, user ? Accent : TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		PlayerLinks.MakeClickable(name, row.TeamId, row.Name);
		columns.AddChild(name);

		columns.AddChild(MakeFixed(row.Position, 44, _semibold, 13, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(BuildTeamMark(row.TeamId, row.TeamShort));
		columns.AddChild(MakeFixed(row.Display, 64, _bold, 15, TextPrimary, HorizontalAlignment.Right));
		panel.AddChild(columns);
		return panel;
	}

	private static HubStatTab FindTab(string key)
	{
		foreach (HubStatTab tab in DistrictHubData.BattingStats)
		{
			if (tab.Key == key)
			{
				return tab;
			}
		}

		foreach (HubStatTab tab in DistrictHubData.PitchingStats)
		{
			if (tab.Key == key)
			{
				return tab;
			}
		}

		return DistrictHubData.BattingStats[0];
	}

	private void StylePills(Dictionary<string, Button> buttons, string active)
	{
		foreach (KeyValuePair<string, Button> pair in buttons)
		{
			bool isActive = pair.Key == active;
			Button button = pair.Value;
			button.Flat = false;
			button.AddThemeFontOverride("font", _semibold);
			button.AddThemeFontSizeOverride("font_size", 12);
			button.AddThemeColorOverride("font_color", isActive ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("font_hover_color", isActive ? TextOnAccent : Colors.White);
			button.AddThemeColorOverride("font_pressed_color", isActive ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("font_focus_color", isActive ? TextOnAccent : Colors.White);
			button.AddThemeStyleboxOverride("normal", MakePill(isActive ? Accent : Colors.Transparent, 12));
			button.AddThemeStyleboxOverride("hover", MakePill(isActive ? AccentHover : HoverBg, 12));
			button.AddThemeStyleboxOverride("pressed", MakePill(isActive ? AccentHover : HoverBg, 12));
			button.AddThemeStyleboxOverride("focus", MakePill(isActive ? AccentHover : HoverBg, 12));
		}
	}

	private Control BuildTeamMark(string teamId, string teamShort)
	{
		var mark = new HBoxContainer();
		mark.AddThemeConstantOverride("separation", 6);
		mark.CustomMinimumSize = new Vector2(120, 0);
		TextureRect? logo = TeamLogos.TryMakeIcon(teamId, 20);
		if (logo != null)
		{
			mark.AddChild(logo);
		}

		var label = MakeText(teamShort, _semibold, 13, TextMuted, HorizontalAlignment.Left);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		mark.AddChild(label);
		SchoolLinks.MakeClickable(mark, teamId);
		return mark;
	}

	private Label MakeFixed(string text, float width, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		Label label = MakeText(text, font, size, color, align);
		label.CustomMinimumSize = new Vector2(width, 0);
		return label;
	}

	private static Label MakeText(string text, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		var label = new Label { Text = text, HorizontalAlignment = align };
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static StyleBoxFlat MakePill(Color bg, float padX, float padY = 5)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			DrawCenter = true,
			ContentMarginLeft = padX,
			ContentMarginTop = padY,
			ContentMarginRight = padX,
			ContentMarginBottom = padY,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerDetail = 5,
		};
	}
}
