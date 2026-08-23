using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// District Hub leaders: one stat table at a time. Batting and pitching
/// pills pick the group; stat tabs swap the ranked column.
/// </summary>
public partial class LeadersPage : Control
{
	private const string ViewBatting = "batting";
	private const string ViewPitching = "pitching";

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color UserRow = new(0.956863f, 0.643137f, 0.109804f, 0.14f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _cascadeLogo = null!;

	private readonly Dictionary<string, Button> _groupButtons = new();
	private readonly Dictionary<string, Button> _statButtons = new();

	private Label _subtitle = null!;
	private Label _countValue = null!;
	private HFlowContainer _statRow = null!;
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
		_cascadeLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
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
		layout.AddThemeConstantOverride("separation", 8);
		margin.AddChild(layout);

		layout.AddChild(BuildHeader());
		layout.AddChild(BuildGroupFilters());

		_statRow = new HFlowContainer();
		_statRow.AddThemeConstantOverride("h_separation", 4);
		_statRow.AddThemeConstantOverride("v_separation", 4);
		layout.AddChild(_statRow);

		layout.AddChild(BuildListCard());
	}

	private Control BuildHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);
		header.AddChild(TeamLogos.MakeHeaderMark(_cascadeLogo));
		header.AddChild(TeamLogos.MakeHeaderRule(new Color(0.28f, 0.30f, 0.35f, 1f)));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -2);
		identity.AddChild(MakeText($"DISTRICT HUB  ·  {DistrictHubData.SeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("LEADERS", _bold, 26, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("VARSITY  ·  FOUR DISTRICTS", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var countBlock = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		countBlock.AddThemeConstantOverride("separation", -4);
		_countValue = MakeText("0", _bold, 28, TextPrimary, HorizontalAlignment.Right);
		countBlock.AddChild(_countValue);
		countBlock.AddChild(MakeText("PLAYERS", _semibold, 11, TextMuted, HorizontalAlignment.Right));
		header.AddChild(countBlock);
		return header;
	}

	private Control BuildGroupFilters()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		AddGroup(row, ViewBatting, "BATTING");
		AddGroup(row, ViewPitching, "PITCHING");
		return row;
	}

	private void AddGroup(HBoxContainer row, string key, string label)
	{
		var button = new Button { Text = label };
		button.Pressed += () =>
		{
			_activeGroup = key;
			_activeStat = key == ViewPitching ? "ERA" : "AVG";
			Refresh();
		};
		_groupButtons[key] = button;
		row.AddChild(button);
	}

	private Control BuildListCard()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle());

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);

		var heading = new PanelContainer();
		heading.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047059f, 0.054902f, 0.070588f, 0.96f),
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
			BgColor = new Color(0.047059f, 0.054902f, 0.070588f, 0.96f),
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
		_subtitle.Text = varsity
			? $"VARSITY  ·  {tab.Title.ToUpperInvariant()}  ·  ALL FOUR DISTRICTS"
			: $"JV  ·  {tab.Title.ToUpperInvariant()}  ·  VARSITY BOARDS UNTIL JV BOX SCORES LAND";
		_tableTitle.Text = tab.Title.ToUpperInvariant();
		_statHeader.Text = tab.Label;

		StylePills(_groupButtons, _activeGroup);
		RebuildStatTabs();
		RebuildRows();
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
		_countValue.Text = $"{rows.Count}";
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

	private static StyleBoxFlat MakeCardStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.078431f, 0.094118f, 0.94f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		};
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
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerDetail = 5,
		};
	}
}
