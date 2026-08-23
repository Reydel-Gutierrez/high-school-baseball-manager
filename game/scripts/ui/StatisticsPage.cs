using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// District Hub statistics: pick a district, open a team, then click a
/// player to open a roster-style profile card on the right.
/// </summary>
public partial class StatisticsPage : Control
{
	private const string StatBatting = "batting";
	private const string StatPitching = "pitching";

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color RowSelected = new(0.956863f, 0.643137f, 0.109804f, 0.14f);
	private static readonly Color UserRow = new(0.956863f, 0.643137f, 0.109804f, 0.14f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color BarBg = new(0.121569f, 0.137255f, 0.160784f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _cascadeLogo = null!;
	private ShaderMaterial _circleMaterial = null!;

	private readonly Dictionary<string, Button> _districtButtons = new();
	private readonly Dictionary<string, Button> _statButtons = new();
	private readonly Dictionary<string, PanelContainer> _rowByName = new();

	private Label _subtitle = null!;
	private Label _countValue = null!;
	private Label _countLabel = null!;
	private HBoxContainer _statFilterRow = null!;
	private Button _backButton = null!;
	private Label _tableTitle = null!;
	private Control _listCard = null!;
	private PanelContainer _profileCard = null!;
	private VBoxContainer _listLayout = null!;
	private Control _columnHeader = null!;
	private VBoxContainer _rowsHost = null!;
	private VBoxContainer _detailHost = null!;
	private readonly Dictionary<int, Button> _seasonYearButtons = new();
	private Label _seasonTitle = null!;
	private Label _seasonOrgLabel = null!;
	private Label _seasonKindLabel = null!;
	private VBoxContainer _seasonBody = null!;
	private HubSquadPlayer? _profilePlayer;
	private int _selectedSeasonYear = DistrictHubData.SeasonYear;
	private string _activeDistrict = DistrictHubData.UserDistrictId;
	private string _activeStat = StatBatting;
	private string? _activeTeamId;
	private string? _selectedPlayerName;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_cascadeLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
		_circleMaterial = new ShaderMaterial
		{
			Shader = GD.Load<Shader>("res://assets/ui/circle_crop.gdshader"),
		};
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
		layout.AddChild(BuildDistrictFilters());
		layout.AddChild(BuildTeamChrome());

		var body = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 8);
		layout.AddChild(body);

		_listCard = BuildListCard();
		_listCard.SizeFlagsStretchRatio = 1.5f;
		body.AddChild(_listCard);

		_profileCard = BuildProfileCard();
		body.AddChild(_profileCard);
	}

	private Control BuildHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);
		header.AddChild(TeamLogos.MakeHeaderMark(_cascadeLogo));
		header.AddChild(TeamLogos.MakeHeaderRule(Hairline));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -2);
		identity.AddChild(MakeText($"DISTRICT HUB  ·  {DistrictHubData.SeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("STATISTICS", _bold, 26, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("VARSITY  ·  GREAT LAKES DISTRICT", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var countBlock = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		countBlock.AddThemeConstantOverride("separation", -4);
		_countValue = MakeText("8", _bold, 28, TextPrimary, HorizontalAlignment.Right);
		countBlock.AddChild(_countValue);
		_countLabel = MakeText("TEAMS", _semibold, 11, TextMuted, HorizontalAlignment.Right);
		countBlock.AddChild(_countLabel);
		header.AddChild(countBlock);
		return header;
	}

	private Control BuildDistrictFilters()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		foreach (HubDistrict district in DistrictHubData.Districts)
		{
			var button = new Button { Text = district.Name.ToUpperInvariant() };
			string id = district.Id;
			button.Pressed += () =>
			{
				_activeDistrict = id;
				_activeTeamId = null;
				_selectedPlayerName = null;
				Refresh();
			};
			_districtButtons[district.Id] = button;
			row.AddChild(button);
		}

		return row;
	}

	private Control BuildTeamChrome()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);

		_backButton = new Button { Text = "ALL TEAMS" };
		_backButton.Pressed += () =>
		{
			_activeTeamId = null;
			_selectedPlayerName = null;
			Refresh();
		};
		row.AddChild(_backButton);

		_statFilterRow = new HBoxContainer();
		_statFilterRow.AddThemeConstantOverride("separation", 4);
		AddStatFilter(StatBatting, "BATTING");
		AddStatFilter(StatPitching, "PITCHING");
		row.AddChild(_statFilterRow);
		return row;
	}

	private void AddStatFilter(string key, string label)
	{
		var button = new Button { Text = label };
		button.Pressed += () =>
		{
			_activeStat = key;
			_selectedPlayerName = null;
			Refresh();
		};
		_statButtons[key] = button;
		_statFilterRow.AddChild(button);
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

		_listLayout = new VBoxContainer();
		_listLayout.AddThemeConstantOverride("separation", 0);
		card.AddChild(_listLayout);

		var heading = new PanelContainer();
		heading.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047059f, 0.054902f, 0.070588f, 0.96f),
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		});
		_tableTitle = MakeText("GREAT LAKES DISTRICT", _bold, 16, TextPrimary, HorizontalAlignment.Left);
		heading.AddChild(_tableTitle);
		_listLayout.AddChild(heading);
		_listLayout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });

		_columnHeader = BuildTeamHeader();
		_listLayout.AddChild(_columnHeader);

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
			FollowFocus = true,
		};
		_listLayout.AddChild(scroll);

		_rowsHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_rowsHost.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(_rowsHost);
		return card;
	}

	private PanelContainer BuildProfileCard()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1f,
			ClipContents = true,
			Visible = false,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle());

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		card.AddChild(scroll);

		_detailHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_detailHost.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_detailHost);
		return card;
	}

	private void Refresh()
	{
		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		string level = varsity ? "VARSITY" : "JV";
		HubDistrict district = DistrictHubData.GetDistrict(_activeDistrict);
		bool teamOpen = _activeTeamId != null;
		HubTeam? team = teamOpen ? DistrictHubData.GetTeam(_activeTeamId!) : null;

		_subtitle.Text = teamOpen
			? $"{level}  ·  {district.Name.ToUpperInvariant()}  ·  {team!.Name.ToUpperInvariant()}"
			: $"{level}  ·  {district.Name.ToUpperInvariant()} DISTRICT  ·  SELECT A TEAM";
		_tableTitle.Text = teamOpen
			? team!.Name.ToUpperInvariant() + (_activeStat == StatPitching ? "  ·  PITCHING" : "  ·  BATTING")
			: district.Name.ToUpperInvariant() + " DISTRICT  ·  CLICK A TEAM";
		_countLabel.Text = teamOpen ? "PLAYERS" : "TEAMS";
		_backButton.Visible = teamOpen;
		_statFilterRow.Visible = teamOpen;
		_profileCard.Visible = teamOpen;
		_listCard.SizeFlagsStretchRatio = teamOpen ? 1.5f : 1f;

		StylePills(_districtButtons, _activeDistrict);
		StylePills(_statButtons, _activeStat);
		StyleBackButton();
		ReplaceHeader(teamOpen
			? (_activeStat == StatPitching ? BuildPitchingHeader() : BuildBattingHeader())
			: BuildTeamHeader());
		RebuildRows();
		if (teamOpen)
		{
			BindProfile();
		}
	}

	private void StyleBackButton()
	{
		_backButton.Flat = false;
		_backButton.AddThemeFontOverride("font", _semibold);
		_backButton.AddThemeFontSizeOverride("font_size", 12);
		_backButton.AddThemeColorOverride("font_color", TextPrimary);
		_backButton.AddThemeColorOverride("font_hover_color", Colors.White);
		_backButton.AddThemeColorOverride("font_pressed_color", TextPrimary);
		_backButton.AddThemeColorOverride("font_focus_color", Colors.White);
		_backButton.AddThemeStyleboxOverride("normal", MakePill(Colors.Transparent, 12));
		_backButton.AddThemeStyleboxOverride("hover", MakePill(HoverBg, 12));
		_backButton.AddThemeStyleboxOverride("pressed", MakePill(HoverBg, 12));
		_backButton.AddThemeStyleboxOverride("focus", MakePill(HoverBg, 12));
	}

	private void ReplaceHeader(Control next)
	{
		int index = _columnHeader.GetIndex();
		_listLayout.RemoveChild(_columnHeader);
		_columnHeader.QueueFree();
		_columnHeader = next;
		_listLayout.AddChild(_columnHeader);
		_listLayout.MoveChild(_columnHeader, index);
	}

	private void RebuildRows()
	{
		foreach (Node child in _rowsHost.GetChildren())
		{
			_rowsHost.RemoveChild(child);
			child.QueueFree();
		}

		_rowByName.Clear();
		TeamLevel level = _session.ActiveTeam.Level;
		if (_activeTeamId == null)
		{
			List<HubTeam> teams = DistrictHubData.TeamsInDistrict(_activeDistrict, level);
			_countValue.Text = $"{teams.Count}";
			for (int i = 0; i < teams.Count; i++)
			{
				_rowsHost.AddChild(BuildTeamRow(teams[i], i + 1, i % 2 == 0, level));
			}

			return;
		}

		List<HubSquadPlayer> players = _activeStat == StatPitching
			? DistrictHubSquads.Arms(_activeTeamId, level)
			: DistrictHubSquads.Hitters(_activeTeamId, level);
		_countValue.Text = $"{players.Count}";
		if (players.Count > 0 && (_selectedPlayerName == null || !ContainsPlayer(players, _selectedPlayerName)))
		{
			_selectedPlayerName = players[0].Name;
		}

		for (int i = 0; i < players.Count; i++)
		{
			_rowsHost.AddChild(BuildPlayerRow(players[i], i));
		}
	}

	private void SelectPlayer(string name)
	{
		_selectedPlayerName = name;
		foreach (KeyValuePair<string, PanelContainer> pair in _rowByName)
		{
			int index = (int)pair.Value.GetMeta("row_index");
			bool selected = pair.Key == _selectedPlayerName;
			pair.Value.AddThemeStyleboxOverride("panel", MakePlayerRowStyle(selected, index % 2 == 0));
		}

		BindProfile();
	}

	private Control BuildTeamHeader() =>
		BuildHeaderRow(["#", "TEAM", "REC", "AVG", "HR", "ERA", "DIFF", ""]);

	private Control BuildBattingHeader()
	{
		var header = MakeHeaderPanel();
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		columns.AddChild(MakeFixed("#", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeSpacer(28));
		var player = MakeText("PLAYER", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		player.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(player);
		columns.AddChild(MakeFixed("POS", 36, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("YR", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("OVR", 36, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("AVG", 44, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		header.AddChild(columns);
		return header;
	}

	private Control BuildPitchingHeader()
	{
		var header = MakeHeaderPanel();
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		columns.AddChild(MakeFixed("#", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeSpacer(28));
		var player = MakeText("PLAYER", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		player.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(player);
		columns.AddChild(MakeFixed("POS", 36, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("YR", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("OVR", 36, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("ERA", 44, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		header.AddChild(columns);
		return header;
	}

	private Control BuildHeaderRow(string[] labels)
	{
		var header = MakeHeaderPanel();
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		columns.AddChild(MakeFixed(labels[0], 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		var main = MakeText(labels[1], _semibold, 10, TextMuted, HorizontalAlignment.Left);
		main.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(main);
		for (int i = 2; i < labels.Length; i++)
		{
			float width = labels[i] == "" ? 18 : 44;
			columns.AddChild(MakeFixed(labels[i], width, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		}

		header.AddChild(columns);
		return header;
	}

	private static PanelContainer MakeHeaderPanel()
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
		return header;
	}

	private Control BuildTeamRow(HubTeam team, int rank, bool even, TeamLevel level)
	{
		int diff = team.RunsFor - team.RunsAgainst;
		var panel = MakeRowPanel(team.IsUserTeam, even);
		panel.MouseFilter = MouseFilterEnum.Stop;
		panel.MouseDefaultCursorShape = CursorShape.PointingHand;
		string teamId = team.Id;
		panel.GuiInput += (InputEvent evt) =>
		{
			if (evt is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				_activeTeamId = teamId;
				_activeStat = StatBatting;
				_selectedPlayerName = null;
				Refresh();
			}
		};
		panel.MouseEntered += () => panel.AddThemeStyleboxOverride("panel", MakeTeamRowStyle(true, even, true));
		panel.MouseExited += () => panel.AddThemeStyleboxOverride("panel", MakeTeamRowStyle(team.IsUserTeam, even, false));

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		columns.MouseFilter = MouseFilterEnum.Ignore;
		columns.AddChild(MakeFixed($"{rank}", 28, _bold, 14, rank <= 2 ? Green : TextMuted, HorizontalAlignment.Center));
		columns.AddChild(BuildTeamIdentity(team));

		columns.AddChild(MakeFixed(team.Record(level), 44, _bold, 13, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed(FormatAvg(team.TeamAvg), 44, _semibold, 13, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed($"{team.TeamHr}", 44, _semibold, 13, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed(team.TeamEra.ToString("0.00"), 44, _semibold, 13, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed(diff > 0 ? $"+{diff}" : $"{diff}", 44, _semibold, 13, diff > 0 ? Green : diff < 0 ? Urgent : TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("›", 18, _bold, 18, Accent, HorizontalAlignment.Center));
		panel.AddChild(columns);
		return panel;
	}

	private Control BuildPlayerRow(HubSquadPlayer player, int index)
	{
		bool selected = player.Name == _selectedPlayerName;
		var row = MakePlayerRow(player.Name, index, selected);

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		columns.MouseFilter = MouseFilterEnum.Ignore;
		columns.AddChild(MakeFixed($"{player.Jersey}", 28, _bold, 13, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(BuildPortrait(player, 28));
		columns.AddChild(BuildNameCell(player.FirstName, player.LastName));
		columns.AddChild(MakeFixed(player.Position, 36, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed(player.Year, 28, _semibold, 12, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed($"{player.Overall}", 36, _bold, 13, OverallColor(player.Overall), HorizontalAlignment.Center));
		string stat = _activeStat == StatPitching
			? FormatEra(player.CurrentPitching?.Era ?? 0f)
			: FormatAvg(player.CurrentHitting?.Average ?? 0f);
		columns.AddChild(MakeFixed(stat, 44, _bold, 14, TextPrimary, HorizontalAlignment.Right));
		row.AddChild(columns);
		return row;
	}

	private PanelContainer MakePlayerRow(string name, int index, bool selected)
	{
		var row = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			MouseFilter = MouseFilterEnum.Stop,
		};
		row.SetMeta("row_index", index);
		row.AddThemeStyleboxOverride("panel", MakePlayerRowStyle(selected, index % 2 == 0));
		row.GuiInput += (InputEvent evt) =>
		{
			if (evt is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				string selectedName = name;
				Callable.From(() => SelectPlayer(selectedName)).CallDeferred();
			}
		};
		row.MouseEntered += () =>
		{
			if (name != _selectedPlayerName)
			{
				row.AddThemeStyleboxOverride("panel", MakePlayerRowStyle(false, true, true));
			}
		};
		row.MouseExited += () =>
		{
			if (name != _selectedPlayerName)
			{
				row.AddThemeStyleboxOverride("panel", MakePlayerRowStyle(false, index % 2 == 0));
			}
		};

		_rowByName[name] = row;
		return row;
	}

	private Control BuildNameCell(string first, string last)
	{
		var box = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		box.AddThemeConstantOverride("separation", -2);
		box.MouseFilter = MouseFilterEnum.Ignore;
		box.AddChild(MakeText(last.ToUpperInvariant(), _semibold, 13, TextPrimary, HorizontalAlignment.Left));
		box.AddChild(MakeText(first, _medium, 10, TextMuted, HorizontalAlignment.Left));
		return box;
	}

	private void BindProfile()
	{
		foreach (Node child in _detailHost.GetChildren())
		{
			_detailHost.RemoveChild(child);
			child.QueueFree();
		}

		_profilePlayer = FindSelectedPlayer();
		if (_profilePlayer == null)
		{
			return;
		}

		var margin = new MarginContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		_detailHost.AddChild(margin);

		var layout = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		layout.AddThemeConstantOverride("separation", 8);
		margin.AddChild(layout);

		layout.AddChild(BuildProfileHeader(_profilePlayer));
		layout.AddChild(BuildRatingsRow(_profilePlayer));
		layout.AddChild(MakeAccentLine());
		layout.AddChild(BuildSeasonStats(_profilePlayer));
	}

	private Control BuildProfileHeader(HubSquadPlayer player)
	{
		var header = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
		};
		header.AddThemeConstantOverride("separation", 10);
		header.AddChild(BuildPortrait(player, 76));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		identity.AddThemeConstantOverride("separation", 2);
		identity.AddChild(MakeText(player.LastName.ToUpperInvariant(), _bold, 22, TextPrimary, HorizontalAlignment.Left));
		identity.AddChild(MakeText(player.FirstName, _medium, 14, TextMuted, HorizontalAlignment.Left));
		identity.AddChild(MakeText(
			$"#{player.Jersey}  ·  {player.Position}  ·  {player.Year}  ·  {player.BatsThrows}  ·  {player.Squad.ToUpperInvariant()}",
			_semibold,
			12,
			TextPrimary,
			HorizontalAlignment.Left));

		var statusRow = new HBoxContainer();
		statusRow.AddThemeConstantOverride("separation", 8);
		statusRow.AddChild(MakeText(player.Status.ToUpperInvariant(), _semibold, 12, StatusColor(player.Status), HorizontalAlignment.Left));
		identity.AddChild(statusRow);

		header.AddChild(identity);
		return header;
	}

	private Control BuildRatingsRow(HubSquadPlayer player)
	{
		var row = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
		};
		row.AddThemeConstantOverride("separation", 5);
		row.AddChild(BuildRatingChip("OVR", player.Overall));
		row.AddChild(BuildRatingChip("CON", player.Contact));
		row.AddChild(BuildRatingChip("POW", player.Power));
		row.AddChild(BuildRatingChip("SPD", player.Speed));
		row.AddChild(BuildRatingChip("ARM", player.Arm));
		row.AddChild(BuildRatingChip("FLD", player.Field));
		return row;
	}

	private Control BuildRatingChip(string label, int value)
	{
		var chip = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		chip.AddThemeStyleboxOverride("panel", MakeInnerCard(5, 5));
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 2);
		chip.AddChild(box);
		box.AddChild(MakeText(label, _semibold, 8, label == "OVR" ? Accent : TextMuted, HorizontalAlignment.Center));
		box.AddChild(MakeText($"{value}", _bold, 16, OverallColor(value), HorizontalAlignment.Center));
		var bar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0, 4),
			MaxValue = 99,
			Value = value,
			ShowPercentage = false,
		};
		bar.AddThemeStyleboxOverride("background", MakeFlat(BarBg, 2));
		bar.AddThemeStyleboxOverride("fill", MakeFlat(OverallColor(value), 2));
		box.AddChild(bar);
		return chip;
	}

	private Control BuildSeasonStats(HubSquadPlayer player)
	{
		EnsureSelectedSeason(player);

		var section = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		section.AddThemeConstantOverride("separation", 6);

		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 8);
		_seasonTitle = MakeText($"{_selectedSeasonYear} SEASON", _semibold, 12, TextPrimary, HorizontalAlignment.Left);
		_seasonTitle.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		titleRow.AddChild(_seasonTitle);

		if (player.Seasons.Count > 1)
		{
			titleRow.AddChild(BuildSeasonYearTabs(player));
		}

		var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		titleRow.AddChild(spacer);
		_seasonKindLabel = MakeText(string.Empty, _medium, 10, Accent, HorizontalAlignment.Right);
		_seasonKindLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		titleRow.AddChild(_seasonKindLabel);
		section.AddChild(titleRow);

		_seasonOrgLabel = MakeText(string.Empty, _medium, 11, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		_seasonOrgLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_seasonOrgLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		section.AddChild(_seasonOrgLabel);

		_seasonBody = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_seasonBody.AddThemeConstantOverride("separation", 10);
		section.AddChild(_seasonBody);
		FillSeasonStats(player);
		return section;
	}

	private Control BuildSeasonYearTabs(HubSquadPlayer player)
	{
		_seasonYearButtons.Clear();
		var row = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		row.AddThemeConstantOverride("separation", 3);

		foreach (HubPlayerSeason season in player.Seasons)
		{
			int year = season.Year;
			var button = new Button
			{
				Text = year.ToString(CultureInfo.InvariantCulture),
				Flat = false,
				FocusMode = FocusModeEnum.All,
				MouseDefaultCursorShape = CursorShape.PointingHand,
			};
			button.Pressed += () => SelectSeasonYear(year);
			_seasonYearButtons[year] = button;
			row.AddChild(button);
		}

		RefreshSeasonYearTabs();
		return row;
	}

	private void SelectSeasonYear(int year)
	{
		if (_selectedSeasonYear == year || _profilePlayer == null)
		{
			return;
		}

		_selectedSeasonYear = year;
		RefreshSeasonYearTabs();
		FillSeasonStats(_profilePlayer);
	}

	private void RefreshSeasonYearTabs()
	{
		foreach (KeyValuePair<int, Button> pair in _seasonYearButtons)
		{
			bool isActive = pair.Key == _selectedSeasonYear;
			Button button = pair.Value;
			button.AddThemeFontOverride("font", _semibold);
			button.AddThemeFontSizeOverride("font_size", 11);
			button.AddThemeColorOverride("font_color", isActive ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("font_hover_color", isActive ? TextOnAccent : Colors.White);
			button.AddThemeColorOverride("font_pressed_color", isActive ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("font_focus_color", isActive ? TextOnAccent : Colors.White);
			button.AddThemeStyleboxOverride("normal", MakePill(isActive ? Accent : Colors.Transparent, 8, 3));
			button.AddThemeStyleboxOverride("hover", MakePill(isActive ? AccentHover : HoverBg, 8, 3));
			button.AddThemeStyleboxOverride("pressed", MakePill(isActive ? AccentHover : HoverBg, 8, 3));
			button.AddThemeStyleboxOverride("focus", MakePill(isActive ? AccentHover : HoverBg, 8, 3));
		}
	}

	private void FillSeasonStats(HubSquadPlayer player)
	{
		while (_seasonBody.GetChildCount() > 0)
		{
			Node child = _seasonBody.GetChild(0);
			_seasonBody.RemoveChild(child);
			child.QueueFree();
		}

		HubPlayerSeason? season = FindSeason(player, _selectedSeasonYear);
		if (season == null)
		{
			_seasonTitle.Text = $"{_selectedSeasonYear} SEASON";
			_seasonOrgLabel.Text = string.Empty;
			_seasonKindLabel.Text = string.Empty;
			_seasonBody.AddChild(MakeText("No season stats.", _medium, 12, TextMuted, HorizontalAlignment.Left));
			return;
		}

		_seasonTitle.Text = $"{season.Year} SEASON";
		_seasonOrgLabel.Text = season.OrganizationName.ToUpperInvariant();

		if (season.Pitching != null)
		{
			_seasonKindLabel.Text = "PITCHING";
			_seasonBody.AddChild(BuildPitchingSheet(season.Pitching));
		}
		else if (season.Hitting != null)
		{
			_seasonKindLabel.Text = "BATTING";
		}
		else
		{
			_seasonKindLabel.Text = string.Empty;
		}

		if (season.Hitting != null)
		{
			if (season.Pitching != null)
			{
				_seasonBody.AddChild(MakeText("BATTING", _medium, 10, Accent, HorizontalAlignment.Left));
			}

			_seasonBody.AddChild(BuildHittingSheet(season.Hitting, includeHeadline: season.Pitching == null));
		}
	}

	private void EnsureSelectedSeason(HubSquadPlayer player)
	{
		if (player.Seasons.Count == 0)
		{
			return;
		}

		foreach (HubPlayerSeason season in player.Seasons)
		{
			if (season.Year == _selectedSeasonYear)
			{
				return;
			}
		}

		_selectedSeasonYear = player.Seasons[player.Seasons.Count - 1].Year;
	}

	private static HubPlayerSeason? FindSeason(HubSquadPlayer player, int year)
	{
		HubPlayerSeason? fallback = null;
		foreach (HubPlayerSeason season in player.Seasons)
		{
			fallback = season;
			if (season.Year == year)
			{
				return season;
			}
		}

		return fallback;
	}

	private Control BuildHittingSheet(HubSeasonHitting stats, bool includeHeadline)
	{
		float ops = stats.Ops;
		var sheet = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		sheet.AddThemeConstantOverride("separation", 10);

		if (includeHeadline)
		{
			sheet.AddChild(BuildHeadlineRow(
			[
				(FormatAvg(stats.Average), "Average"),
				(stats.HomeRuns.ToString(CultureInfo.InvariantCulture), "Home Runs"),
				(stats.Rbi.ToString(CultureInfo.InvariantCulture), "RBI"),
				(FormatAvg(ops), "OPS"),
			]));
		}

		sheet.AddChild(BuildBoxScore(
		[
			("G", "Games", stats.Games.ToString(CultureInfo.InvariantCulture)),
			("AB", "At Bats", stats.AtBats.ToString(CultureInfo.InvariantCulture)),
			("R", "Runs", stats.Runs.ToString(CultureInfo.InvariantCulture)),
			("H", "Hits", stats.Hits.ToString(CultureInfo.InvariantCulture)),
			("2B", "Doubles", stats.Doubles.ToString(CultureInfo.InvariantCulture)),
			("3B", "Triples", stats.Triples.ToString(CultureInfo.InvariantCulture)),
			("HR", "Home Runs", stats.HomeRuns.ToString(CultureInfo.InvariantCulture)),
			("RBI", "Runs Batted In", stats.Rbi.ToString(CultureInfo.InvariantCulture)),
			("BB", "Walks", stats.Walks.ToString(CultureInfo.InvariantCulture)),
			("SO", "Strikeouts", stats.Strikeouts.ToString(CultureInfo.InvariantCulture)),
			("SB", "Stolen Bases", stats.StolenBases.ToString(CultureInfo.InvariantCulture)),
		]));

		sheet.AddChild(BuildBoxScore(
		[
			("AVG", "Batting Average", FormatAvg(stats.Average)),
			("OBP", "On-Base Percentage", FormatAvg(stats.OnBase)),
			("SLG", "Slugging Percentage", FormatAvg(stats.Slugging)),
			("OPS", "On-base Plus Slugging", FormatAvg(ops)),
		]));
		return sheet;
	}

	private Control BuildPitchingSheet(HubSeasonPitching stats)
	{
		var sheet = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		sheet.AddThemeConstantOverride("separation", 10);
		sheet.AddChild(BuildHeadlineRow(
		[
			(FormatEra(stats.Era), "ERA"),
			($"{stats.Wins}-{stats.Losses}", "W-L"),
			(stats.Strikeouts.ToString(CultureInfo.InvariantCulture), "Strikeouts"),
			(FormatEra(stats.Whip), "WHIP"),
		]));
		sheet.AddChild(BuildBoxScore(
		[
			("G", "Games", stats.Games.ToString(CultureInfo.InvariantCulture)),
			("GS", "Games Started", stats.GamesStarted.ToString(CultureInfo.InvariantCulture)),
			("IP", "Innings Pitched", FormatIp(stats.Innings)),
			("H", "Hits Allowed", stats.Hits.ToString(CultureInfo.InvariantCulture)),
			("R", "Runs Allowed", stats.Runs.ToString(CultureInfo.InvariantCulture)),
			("ER", "Earned Runs", stats.EarnedRuns.ToString(CultureInfo.InvariantCulture)),
			("BB", "Walks", stats.Walks.ToString(CultureInfo.InvariantCulture)),
			("SO", "Strikeouts", stats.Strikeouts.ToString(CultureInfo.InvariantCulture)),
			("HR", "Home Runs Allowed", stats.HomeRuns.ToString(CultureInfo.InvariantCulture)),
			("SV", "Saves", stats.Saves.ToString(CultureInfo.InvariantCulture)),
		]));
		return sheet;
	}

	private Control BuildHeadlineRow(List<(string Value, string Label)> stats)
	{
		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		row.AddThemeConstantOverride("separation", 0);
		for (int i = 0; i < stats.Count; i++)
		{
			if (i > 0)
			{
				row.AddChild(new ColorRect
				{
					CustomMinimumSize = new Vector2(1, 0),
					Color = Hairline,
					SizeFlagsVertical = SizeFlags.ExpandFill,
				});
			}

			(string value, string label) = stats[i];
			var col = new VBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				Alignment = BoxContainer.AlignmentMode.Center,
			};
			col.AddThemeConstantOverride("separation", 0);
			col.AddChild(MakeText(value, _bold, 26, TextPrimary, HorizontalAlignment.Center));
			col.AddChild(MakeText(label.ToUpperInvariant(), _medium, 10, TextMuted, HorizontalAlignment.Center));
			row.AddChild(col);
		}

		return row;
	}

	private Control BuildBoxScore(List<(string Abbr, string FullName, string Value)> columns)
	{
		var panel = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		panel.AddThemeStyleboxOverride("panel", MakeInnerCard(8, 8));

		var table = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		table.AddThemeConstantOverride("separation", 4);
		panel.AddChild(table);

		var headers = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		headers.AddThemeConstantOverride("separation", 0);
		var values = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		values.AddThemeConstantOverride("separation", 0);

		foreach ((string abbr, string fullName, string value) in columns)
		{
			var header = MakeText(abbr, _semibold, 10, TextMuted, HorizontalAlignment.Center);
			header.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			header.TooltipText = fullName;
			headers.AddChild(header);

			var amount = MakeText(value, _bold, 15, TextPrimary, HorizontalAlignment.Center);
			amount.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			values.AddChild(amount);
		}

		table.AddChild(headers);
		table.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = Hairline });
		table.AddChild(values);
		return panel;
	}

	private Control BuildPortrait(HubSquadPlayer player, float size)
	{
		var wrap = new Control
		{
			CustomMinimumSize = new Vector2(size, size),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		wrap.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		wrap.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

		if (!string.IsNullOrEmpty(player.PortraitPath))
		{
			var portrait = new TextureRect
			{
				Texture = GD.Load<Texture2D>(player.PortraitPath),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				Material = _circleMaterial,
				CustomMinimumSize = new Vector2(size, size),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			portrait.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			wrap.AddChild(portrait);
			return wrap;
		}

		int radius = Mathf.RoundToInt(size / 2f);
		var fallback = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
		fallback.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		fallback.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = HoverBg,
			BorderColor = Accent,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerDetail = 8,
		});
		var initial = MakeText(
			string.IsNullOrEmpty(player.LastName) ? "?" : player.LastName[..1].ToUpperInvariant(),
			_bold,
			Mathf.RoundToInt(size * 0.38f),
			Accent,
			HorizontalAlignment.Center);
		initial.VerticalAlignment = VerticalAlignment.Center;
		fallback.AddChild(initial);
		wrap.AddChild(fallback);
		return wrap;
	}

	private HubSquadPlayer? FindSelectedPlayer()
	{
		if (_selectedPlayerName == null || _activeTeamId == null)
		{
			return null;
		}

		List<HubSquadPlayer> players = _activeStat == StatPitching
			? DistrictHubSquads.Arms(_activeTeamId, _session.ActiveTeam.Level)
			: DistrictHubSquads.Hitters(_activeTeamId, _session.ActiveTeam.Level);
		foreach (HubSquadPlayer player in players)
		{
			if (player.Name == _selectedPlayerName)
			{
				return player;
			}
		}

		return null;
	}

	private static bool ContainsPlayer(List<HubSquadPlayer> list, string name)
	{
		foreach (HubSquadPlayer player in list)
		{
			if (player.Name == name)
			{
				return true;
			}
		}

		return false;
	}

	private static ColorRect MakeAccentLine()
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 2),
			Color = Accent,
		};
	}

	private PanelContainer MakeRowPanel(bool user, bool even)
	{
		var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		panel.AddThemeStyleboxOverride("panel", MakeTeamRowStyle(user, even, false));
		return panel;
	}

	private static StyleBoxFlat MakeTeamRowStyle(bool user, bool even, bool hover)
	{
		return new StyleBoxFlat
		{
			BgColor = hover ? HoverBg : user ? UserRow : even ? RowEven : RowOdd,
			BorderColor = user ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = user ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		};
	}

	private static StyleBoxFlat MakePlayerRowStyle(bool selected, bool even, bool hover = false)
	{
		return new StyleBoxFlat
		{
			BgColor = hover ? HoverBg : selected ? RowSelected : even ? RowEven : RowOdd,
			BorderColor = selected ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = selected ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 10,
			ContentMarginTop = 6,
			ContentMarginRight = 10,
			ContentMarginBottom = 6,
		};
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

	private static string FormatAvg(float avg) => avg.ToString(".000", CultureInfo.InvariantCulture);

	private static string FormatEra(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

	private static string FormatIp(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

	private static Color StatusColor(string status)
	{
		return status.ToLowerInvariant() switch
		{
			"healthy" => Green,
			"day-to-day" => Accent,
			_ => Urgent,
		};
	}

	private static Color OverallColor(int value)
	{
		if (value >= 80)
		{
			return Green;
		}

		if (value >= 70)
		{
			return Accent;
		}

		return TextMuted;
	}

	private static Control MakeSpacer(float width)
	{
		return new Control { CustomMinimumSize = new Vector2(width, 0), MouseFilter = MouseFilterEnum.Ignore };
	}

	private Control BuildTeamIdentity(HubTeam team)
	{
		var identity = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		identity.AddThemeConstantOverride("separation", 8);
		TextureRect? logo = TeamLogos.TryMakeIcon(team.Id, 28);
		if (logo != null)
		{
			logo.MouseFilter = MouseFilterEnum.Ignore;
			identity.AddChild(logo);
		}

		var name = MakeText(team.Name.ToUpperInvariant(), team.IsUserTeam ? _bold : _semibold, 14, team.IsUserTeam ? Accent : TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		name.MouseFilter = MouseFilterEnum.Ignore;
		identity.AddChild(name);
		return identity;
	}

	private Label MakeFixed(string text, float width, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		Label label = MakeText(text, font, size, color, align);
		label.CustomMinimumSize = new Vector2(width, 0);
		return label;
	}

	private static Label MakeText(string text, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = align,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static StyleBoxFlat MakeInnerCard(float padX, float padY)
	{
		return new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = Hairline,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = padX,
			ContentMarginTop = padY,
			ContentMarginRight = padX,
			ContentMarginBottom = padY,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		};
	}

	private static StyleBoxFlat MakeFlat(Color bg, int radius)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerDetail = 4,
		};
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
