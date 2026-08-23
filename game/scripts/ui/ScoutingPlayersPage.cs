using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

public enum ScoutingBoardMode
{
	Search,
	Targets,
}

/// <summary>
/// Scouting board used by Player Search and My Targets. Search waits for the
/// Search button; Targets lists players added from a profile card action.
/// </summary>
public partial class ScoutingPlayersPage : Control
{
	[Export]
	public ScoutingBoardMode BoardMode { get; set; }

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
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color BarBg = new(0.121569f, 0.137255f, 0.160784f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _cascadeLogo = null!;
	private ShaderMaterial _circleMaterial = null!;

	private readonly Dictionary<string, PanelContainer> _rowByKey = new();
	private readonly Dictionary<int, Button> _seasonYearButtons = new();
	private readonly List<HubSquadPlayer> _results = new();

	private Label _subtitle = null!;
	private Label _countValue = null!;
	private Label _countLabel = null!;
	private Label _tableTitle = null!;
	private Control _filterBar = null!;
	private OptionButton _yearFilter = null!;
	private OptionButton _positionFilter = null!;
	private OptionButton _ovrFilter = null!;
	private OptionButton _avgFilter = null!;
	private OptionButton _districtFilter = null!;
	private VBoxContainer _rowsHost = null!;
	private VBoxContainer _detailHost = null!;
	private PanelContainer _profileCard = null!;
	private Control _actionOverlay = null!;
	private VBoxContainer _actionBody = null!;
	private Label _seasonTitle = null!;
	private Label _seasonOrgLabel = null!;
	private Label _seasonKindLabel = null!;
	private VBoxContainer _seasonBody = null!;

	private HubSquadPlayer? _profilePlayer;
	private string? _selectedKey;
	private int _selectedSeasonYear = DistrictHubData.SeasonYear;
	private bool _hasSearched;
	private int _appliedYear;
	private int _appliedPosition;
	private int _appliedOvr;
	private int _appliedAvg;
	private int _appliedDistrict;

	public override void _Ready()
	{
		if (Name == "TargetsPage" || Name == "TargetsView")
		{
			BoardMode = ScoutingBoardMode.Targets;
		}

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
		BuildActionOverlay();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		_session.ScoutTargetsChanged += OnScoutTargetsChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
			_session.ScoutTargetsChanged -= OnScoutTargetsChanged;
		}
	}

	public override void _Notification(int what)
	{
		if (what == NotificationVisibilityChanged && Visible && _session != null)
		{
			Refresh();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsVisibleInTree() || @event is not InputEventKey key || !key.Pressed || key.Echo)
		{
			return;
		}

		if (_actionOverlay.Visible)
		{
			if (key.Keycode == Key.Escape)
			{
				ClosePlayerActions();
			}

			GetViewport().SetInputAsHandled();
			return;
		}

		if (key.Keycode is not (Key.Up or Key.Down) || _results.Count == 0)
		{
			return;
		}

		SelectAdjacent(key.Keycode == Key.Down ? 1 : -1);
		GetViewport().SetInputAsHandled();
	}

	private void OnActiveTeamChanged(TeamIdentity _)
	{
		if (BoardMode == ScoutingBoardMode.Search)
		{
			_hasSearched = false;
			_selectedKey = null;
			_results.Clear();
		}

		Refresh();
	}

	private void OnScoutTargetsChanged()
	{
		if (BoardMode == ScoutingBoardMode.Targets || _profilePlayer != null)
		{
			Refresh();
		}
	}

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
		_filterBar = BuildFilterBar();
		layout.AddChild(_filterBar);

		var body = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 8);
		layout.AddChild(body);

		Control listCard = BuildListCard();
		listCard.SizeFlagsStretchRatio = 1.55f;
		body.AddChild(listCard);

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
		identity.AddChild(MakeText($"SCOUTING  ·  {DistrictHubData.SeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText(
			BoardMode == ScoutingBoardMode.Targets ? "MY TARGETS" : "PLAYER SEARCH",
			_bold,
			26,
			TextPrimary,
			HorizontalAlignment.Left));
		_subtitle = MakeText(string.Empty, _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
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
		_countLabel = MakeText("PLAYERS", _semibold, 11, TextMuted, HorizontalAlignment.Right);
		countBlock.AddChild(_countLabel);
		header.AddChild(countBlock);
		return header;
	}

	private Control BuildFilterBar()
	{
		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Begin,
		};
		row.AddThemeConstantOverride("separation", 8);

		_yearFilter = MakeFilter("YEAR", ["ANY", "FR", "SO", "JR", "SR"]);
		_positionFilter = MakeFilter("POSITION", ["ANY", "PITCHERS", "CATCHERS", "INFIELD", "OUTFIELD"]);
		_ovrFilter = MakeFilter("MIN OVR", ["ANY", "60+", "70+", "80+"]);
		_avgFilter = MakeFilter("MIN AVG", ["ANY", ".250+", ".300+", ".350+"]);
		_districtFilter = MakeFilter("DISTRICT", DistrictOptions());

		row.AddChild(WrapFilter("YEAR", _yearFilter));
		row.AddChild(WrapFilter("POSITION", _positionFilter));
		row.AddChild(WrapFilter("MIN OVR", _ovrFilter));
		row.AddChild(WrapFilter("MIN AVG", _avgFilter));
		row.AddChild(WrapFilter("DISTRICT", _districtFilter));

		var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddChild(spacer);

		var search = new Button
		{
			Text = "SEARCH",
			SizeFlagsVertical = SizeFlags.ShrinkEnd,
			Flat = false,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		search.AddThemeFontOverride("font", _semibold);
		search.AddThemeFontSizeOverride("font_size", 13);
		search.AddThemeColorOverride("font_color", TextOnAccent);
		search.AddThemeColorOverride("font_hover_color", TextOnAccent);
		search.AddThemeColorOverride("font_pressed_color", TextOnAccent);
		search.AddThemeColorOverride("font_focus_color", TextOnAccent);
		search.AddThemeStyleboxOverride("normal", MakeFilledButton(Accent));
		search.AddThemeStyleboxOverride("hover", MakeFilledButton(AccentHover));
		search.AddThemeStyleboxOverride("pressed", MakeFilledButton(AccentHover));
		search.AddThemeStyleboxOverride("focus", MakeFilledButton(AccentHover));
		search.Pressed += RunSearch;
		row.AddChild(search);
		return row;
	}

	private static string[] DistrictOptions()
	{
		var options = new string[DistrictHubData.Districts.Count + 1];
		options[0] = "ANY";
		for (int i = 0; i < DistrictHubData.Districts.Count; i++)
		{
			options[i + 1] = DistrictHubData.Districts[i].Name.ToUpperInvariant();
		}

		return options;
	}

	private Control WrapFilter(string label, OptionButton button)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 2);
		box.AddChild(MakeText(label, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		box.AddChild(button);
		return box;
	}

	private OptionButton MakeFilter(string name, string[] items)
	{
		var button = new OptionButton
		{
			Name = name,
			Flat = false,
			CustomMinimumSize = new Vector2(118, 0),
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		foreach (string item in items)
		{
			button.AddItem(item);
		}

		button.Selected = 0;
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeColorOverride("font_pressed_color", TextPrimary);
		button.AddThemeColorOverride("font_focus_color", Colors.White);
		button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, CardBorder));
		button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("focus", MakeOutlinedButton(HoverBg, Accent));
		return button;
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
		_tableTitle = MakeText(string.Empty, _bold, 16, TextPrimary, HorizontalAlignment.Left);
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
			FollowFocus = true,
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
		columns.AddThemeConstantOverride("separation", 8);
		columns.AddChild(MakeFixed("#", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeSpacer(28));
		var player = MakeText("PLAYER", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		player.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(player);
		columns.AddChild(MakeFixed("POS", 36, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("YR", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("TEAM", 92, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed("OVR", 36, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("AVG/ERA", 56, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		header.AddChild(columns);
		return header;
	}

	private PanelContainer BuildProfileCard()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1f,
			ClipContents = true,
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

	private void RunSearch()
	{
		_hasSearched = true;
		_appliedYear = _yearFilter.Selected;
		_appliedPosition = _positionFilter.Selected;
		_appliedOvr = _ovrFilter.Selected;
		_appliedAvg = _avgFilter.Selected;
		_appliedDistrict = _districtFilter.Selected;
		_selectedKey = null;
		Refresh();
	}

	private void Refresh()
	{
		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		string level = varsity ? "VARSITY" : "JV";
		_filterBar.Visible = BoardMode == ScoutingBoardMode.Search;

		if (BoardMode == ScoutingBoardMode.Targets)
		{
			RebuildTargetResults();
			_subtitle.Text = $"{level}  ·  PLAYERS YOU HAVE MARKED";
			_tableTitle.Text = "WATCH LIST";
			_countLabel.Text = "TARGETS";
		}
		else if (_hasSearched)
		{
			RebuildSearchResults();
			_subtitle.Text = $"{level}  ·  CLICK SEARCH TO REFRESH RESULTS";
			_tableTitle.Text = "SEARCH RESULTS";
			_countLabel.Text = "PLAYERS";
		}
		else
		{
			_results.Clear();
			_selectedKey = null;
			_subtitle.Text = $"{level}  ·  SET FILTERS, THEN SEARCH";
			_tableTitle.Text = "SET FILTERS AND SEARCH";
			_countLabel.Text = "PLAYERS";
		}

		_countValue.Text = $"{_results.Count}";
		RebuildRows();
		BindProfile();
	}

	private void RebuildSearchResults()
	{
		_results.Clear();
		TeamLevel level = _session.ActiveTeam.Level;
		foreach (HubSquadPlayer player in DistrictHubSquads.AllPlayers(level))
		{
			if (MatchesSearch(player))
			{
				_results.Add(player);
			}
		}

		_results.Sort(ComparePlayers);
		EnsureSelection();
	}

	private void RebuildTargetResults()
	{
		_results.Clear();
		TeamLevel level = _session.ActiveTeam.Level;
		foreach (ScoutTarget target in _session.ScoutTargets)
		{
			HubSquadPlayer? player = DistrictHubSquads.FindPlayer(target.TeamId, target.PlayerName, level);
			if (player != null)
			{
				_results.Add(player);
			}
		}

		EnsureSelection();
	}

	private void EnsureSelection()
	{
		if (_results.Count == 0)
		{
			_selectedKey = null;
			return;
		}

		if (_selectedKey == null || FindResult(_selectedKey) == null)
		{
			_selectedKey = PlayerKey(_results[0]);
		}
	}

	private bool MatchesSearch(HubSquadPlayer player)
	{
		if (_appliedYear > 0)
		{
			string year = _appliedYear switch
			{
				1 => "FR",
				2 => "SO",
				3 => "JR",
				_ => "SR",
			};
			if (!player.Year.Equals(year, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
		}

		if (_appliedPosition > 0 && PositionGroup(player.Position) != _appliedPosition)
		{
			return false;
		}

		int minOvr = _appliedOvr switch
		{
			1 => 60,
			2 => 70,
			3 => 80,
			_ => 0,
		};
		if (player.Overall < minOvr)
		{
			return false;
		}

		float minAvg = _appliedAvg switch
		{
			1 => 0.250f,
			2 => 0.300f,
			3 => 0.350f,
			_ => 0f,
		};
		if (minAvg > 0f)
		{
			if (player.CurrentHitting == null || player.CurrentHitting.Average < minAvg)
			{
				return false;
			}
		}

		if (_appliedDistrict > 0)
		{
			HubTeam team = DistrictHubData.GetTeam(player.TeamId);
			string districtId = DistrictHubData.Districts[_appliedDistrict - 1].Id;
			if (team.DistrictId != districtId)
			{
				return false;
			}
		}

		return true;
	}

	private static int PositionGroup(string position)
	{
		return position.ToUpperInvariant() switch
		{
			"RHP" or "LHP" or "P" => 1,
			"C" => 2,
			"1B" or "2B" or "SS" or "3B" or "INF" or "UTIL" or "DH" => 3,
			"LF" or "CF" or "RF" or "OF" => 4,
			_ => 0,
		};
	}

	private static int ComparePlayers(HubSquadPlayer a, HubSquadPlayer b)
	{
		int ovr = b.Overall.CompareTo(a.Overall);
		return ovr != 0 ? ovr : string.Compare(a.LastName, b.LastName, StringComparison.OrdinalIgnoreCase);
	}

	private void RebuildRows()
	{
		foreach (Node child in _rowsHost.GetChildren())
		{
			_rowsHost.RemoveChild(child);
			child.QueueFree();
		}

		_rowByKey.Clear();
		if (_results.Count == 0)
		{
			_rowsHost.AddChild(MakeEmptyRow());
			return;
		}

		for (int i = 0; i < _results.Count; i++)
		{
			_rowsHost.AddChild(BuildPlayerRow(_results[i], i));
		}
	}

	private Control MakeEmptyRow()
	{
		var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		panel.AddThemeStyleboxOverride("panel", MakePlayerRowStyle(false, true));
		string message = BoardMode == ScoutingBoardMode.Targets
			? "No targets yet. Open Player Search, pick a player from another program, and use Add Target."
			: _hasSearched
				? "No players match those filters."
				: "Set year, average, overall, and the rest, then click Search.";
		var label = MakeText(message, _medium, 13, TextMuted, HorizontalAlignment.Center);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		panel.AddChild(label);
		return panel;
	}

	private Control BuildPlayerRow(HubSquadPlayer player, int index)
	{
		string key = PlayerKey(player);
		bool selected = key == _selectedKey;
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
				string selectedKey = key;
				Callable.From(() => SelectPlayer(selectedKey)).CallDeferred();
			}
		};
		row.MouseEntered += () =>
		{
			if (key != _selectedKey)
			{
				row.AddThemeStyleboxOverride("panel", MakePlayerRowStyle(false, true, true));
			}
		};
		row.MouseExited += () =>
		{
			if (key != _selectedKey)
			{
				row.AddThemeStyleboxOverride("panel", MakePlayerRowStyle(false, index % 2 == 0));
			}
		};

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		columns.MouseFilter = MouseFilterEnum.Ignore;
		columns.AddChild(MakeFixed($"{player.Jersey}", 28, _bold, 13, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(BuildPortrait(player, 28));
		columns.AddChild(BuildNameCell(player.FirstName, player.LastName));
		columns.AddChild(MakeFixed(player.Position, 36, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed(player.Year, 28, _semibold, 12, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(BuildTeamMark(player.TeamId, player.TeamShort, player.TeamId == DistrictHubData.UserTeamId));
		columns.AddChild(MakeFixed($"{player.Overall}", 36, _bold, 13, OverallColor(player.Overall), HorizontalAlignment.Center));
		columns.AddChild(MakeFixed(LineStat(player), 56, _bold, 13, TextPrimary, HorizontalAlignment.Right));
		row.AddChild(columns);
		_rowByKey[key] = row;
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

	private void SelectPlayer(string key)
	{
		_selectedKey = key;
		foreach (KeyValuePair<string, PanelContainer> pair in _rowByKey)
		{
			int index = (int)pair.Value.GetMeta("row_index");
			pair.Value.AddThemeStyleboxOverride("panel", MakePlayerRowStyle(pair.Key == _selectedKey, index % 2 == 0));
		}

		BindProfile();
	}

	private void SelectAdjacent(int delta)
	{
		if (_results.Count == 0)
		{
			return;
		}

		int index = _results.FindIndex(player => PlayerKey(player) == _selectedKey);
		if (index < 0)
		{
			index = 0;
		}
		else
		{
			index = Mathf.Clamp(index + delta, 0, _results.Count - 1);
		}

		SelectPlayer(PlayerKey(_results[index]));
	}

	private void BindProfile()
	{
		foreach (Node child in _detailHost.GetChildren())
		{
			_detailHost.RemoveChild(child);
			child.QueueFree();
		}

		_profilePlayer = _selectedKey == null ? null : FindResult(_selectedKey);
		if (_profilePlayer == null)
		{
			_detailHost.AddChild(MakeEmptyProfile());
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

	private Control MakeEmptyProfile()
	{
		var box = new CenterContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		string message = BoardMode == ScoutingBoardMode.Targets
			? "Targets you add show up here."
			: "Select a player to open the profile.";
		box.AddChild(MakeText(message, _medium, 13, TextMuted, HorizontalAlignment.Center));
		return box;
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
			$"#{player.Jersey}  ·  {player.Position}  ·  {player.Year}  ·  {player.BatsThrows}  ·  {player.TeamShort}",
			_semibold,
			12,
			TextPrimary,
			HorizontalAlignment.Left));

		var statusRow = new HBoxContainer();
		statusRow.AddThemeConstantOverride("separation", 8);
		statusRow.AddChild(MakeText(player.Status.ToUpperInvariant(), _semibold, 12, StatusColor(player.Status), HorizontalAlignment.Left));
		if (_session.IsScoutTarget(player.TeamId, player.Name))
		{
			statusRow.AddChild(MakeText("TARGET", _semibold, 12, Accent, HorizontalAlignment.Left));
		}

		identity.AddChild(statusRow);
		header.AddChild(identity);
		if (player.TeamId != DistrictHubData.UserTeamId)
		{
			header.AddChild(BuildActionButton());
		}

		return header;
	}

	private Button BuildActionButton()
	{
		var button = new Button
		{
			Text = "ACTIONS",
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
			Flat = false,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", TextOnAccent);
		button.AddThemeColorOverride("font_hover_color", TextOnAccent);
		button.AddThemeColorOverride("font_pressed_color", TextOnAccent);
		button.AddThemeColorOverride("font_focus_color", TextOnAccent);
		button.AddThemeStyleboxOverride("normal", MakeFilledButton(Accent));
		button.AddThemeStyleboxOverride("hover", MakeFilledButton(AccentHover));
		button.AddThemeStyleboxOverride("pressed", MakeFilledButton(AccentHover));
		button.AddThemeStyleboxOverride("focus", MakeFilledButton(AccentHover));
		button.Pressed += OpenPlayerActions;
		return button;
	}

	private void BuildActionOverlay()
	{
		var layer = new CanvasLayer { Layer = 40 };
		AddChild(layer);

		_actionOverlay = new Control
		{
			Name = "ScoutActionOverlay",
			Visible = false,
			MouseFilter = MouseFilterEnum.Stop,
		};
		_actionOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		layer.AddChild(_actionOverlay);

		var dimmer = new ColorRect
		{
			Color = new Color(0.02f, 0.03f, 0.04f, 0.72f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dimmer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		dimmer.GuiInput += @event =>
		{
			if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				ClosePlayerActions();
			}
		};
		_actionOverlay.AddChild(dimmer);

		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_actionOverlay.AddChild(center);

		var dialog = new PanelContainer
		{
			CustomMinimumSize = new Vector2(380, 0),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dialog.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.078431f, 0.094118f, 0.98f),
			BorderColor = new Color(0.27451f, 0.294118f, 0.333333f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 18,
			ContentMarginTop = 16,
			ContentMarginRight = 18,
			ContentMarginBottom = 16,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerDetail = 4,
		});
		center.AddChild(dialog);

		_actionBody = new VBoxContainer();
		_actionBody.AddThemeConstantOverride("separation", 8);
		dialog.AddChild(_actionBody);
	}

	private void OpenPlayerActions()
	{
		HubSquadPlayer? player = _profilePlayer;
		if (player == null || player.TeamId == DistrictHubData.UserTeamId)
		{
			return;
		}

		while (_actionBody.GetChildCount() > 0)
		{
			Node child = _actionBody.GetChild(0);
			_actionBody.RemoveChild(child);
			child.QueueFree();
		}

		bool targeted = _session.IsScoutTarget(player.TeamId, player.Name);
		_actionBody.AddChild(MakeText("PLAYER ACTIONS", _semibold, 11, Accent, HorizontalAlignment.Left));
		_actionBody.AddChild(MakeText(player.LastName.ToUpperInvariant(), _bold, 22, TextPrimary, HorizontalAlignment.Left));
		_actionBody.AddChild(MakeText(
			$"{player.FirstName}  ·  {player.TeamShort}  ·  {player.Position}  ·  {player.Year}",
			_medium,
			12,
			TextMuted,
			HorizontalAlignment.Left));
		_actionBody.AddChild(MakeAccentLine());
		_actionBody.AddChild(MakeModalOption("TRADE", TextPrimary, () => StartTrade(player)));
		_actionBody.AddChild(MakeModalOption(
			targeted ? "REMOVE TARGET" : "ADD TARGET",
			targeted ? Urgent : TextPrimary,
			() => ToggleTarget(player)));
		_actionBody.AddChild(MakeModalOption("CANCEL", TextMuted, ClosePlayerActions));
		_actionOverlay.Visible = true;
	}

	private void ClosePlayerActions()
	{
		_actionOverlay.Visible = false;
	}

	private void ToggleTarget(HubSquadPlayer player)
	{
		_session.ToggleScoutTarget(player.TeamId, player.Name);
		ClosePlayerActions();
		if (BoardMode == ScoutingBoardMode.Targets)
		{
			Refresh();
			return;
		}

		BindProfile();
	}

	private void StartTrade(HubSquadPlayer player)
	{
		ClosePlayerActions();
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowScoutingTrades(player.TeamId, player.FirstName, player.LastName);
		}
	}

	private Button MakeModalOption(string text, Color color, Action onPressed)
	{
		var button = new Button
		{
			Text = text,
			Flat = false,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 13);
		button.AddThemeColorOverride("font_color", color);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeColorOverride("font_pressed_color", color);
		button.AddThemeColorOverride("font_focus_color", Colors.White);
		button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, CardBorder));
		button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("focus", MakeOutlinedButton(HoverBg, Accent));
		button.Pressed += onPressed;
		return button;
	}

	private Control BuildRatingsRow(HubSquadPlayer player)
	{
		var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkBegin };
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

		titleRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
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
		var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
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
		var sheet = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		sheet.AddThemeConstantOverride("separation", 10);
		if (includeHeadline)
		{
			sheet.AddChild(BuildHeadlineRow(
			[
				(FormatAvg(stats.Average), "Average"),
				(stats.HomeRuns.ToString(CultureInfo.InvariantCulture), "Home Runs"),
				(stats.Rbi.ToString(CultureInfo.InvariantCulture), "RBI"),
				(FormatAvg(stats.Ops), "OPS"),
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
			("OPS", "On-base Plus Slugging", FormatAvg(stats.Ops)),
		]));
		return sheet;
	}

	private Control BuildPitchingSheet(HubSeasonPitching stats)
	{
		var sheet = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
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
		var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
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
		var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		panel.AddThemeStyleboxOverride("panel", MakeInnerCard(8, 8));
		var table = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
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

	private HubSquadPlayer? FindResult(string key)
	{
		foreach (HubSquadPlayer player in _results)
		{
			if (PlayerKey(player) == key)
			{
				return player;
			}
		}

		return null;
	}

	private static string PlayerKey(HubSquadPlayer player) => $"{player.TeamId}|{player.Name}";

	private static string LineStat(HubSquadPlayer player)
	{
		if (player.CurrentPitching != null && player.CurrentHitting == null)
		{
			return FormatEra(player.CurrentPitching.Era);
		}

		return player.CurrentHitting == null ? "—" : FormatAvg(player.CurrentHitting.Average);
	}

	private static ColorRect MakeAccentLine() =>
		new() { CustomMinimumSize = new Vector2(0, 2), Color = Accent };

	private static string FormatAvg(float avg) => avg.ToString(".000", CultureInfo.InvariantCulture);

	private static string FormatEra(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

	private static string FormatIp(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

	private static Color StatusColor(string status) =>
		status.ToLowerInvariant() switch
		{
			"healthy" => Green,
			"day-to-day" => Accent,
			_ => Urgent,
		};

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

	private static Control MakeSpacer(float width) =>
		new() { CustomMinimumSize = new Vector2(width, 0), MouseFilter = MouseFilterEnum.Ignore };

	private Control BuildTeamMark(string teamId, string teamShort, bool userTeam)
	{
		var mark = new HBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		mark.AddThemeConstantOverride("separation", 6);
		mark.CustomMinimumSize = new Vector2(92, 0);
		TextureRect? logo = TeamLogos.TryMakeIcon(teamId, 18);
		if (logo != null)
		{
			logo.MouseFilter = MouseFilterEnum.Ignore;
			mark.AddChild(logo);
		}

		var label = MakeText(teamShort, _semibold, 12, userTeam ? Accent : TextPrimary, HorizontalAlignment.Left);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		label.ClipText = true;
		label.MouseFilter = MouseFilterEnum.Ignore;
		mark.AddChild(label);
		return mark;
	}

	private Label MakeFixed(string text, float width, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		Label label = MakeText(text, font, size, color, align);
		label.CustomMinimumSize = new Vector2(width, 0);
		label.ClipText = true;
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

	private static StyleBoxFlat MakeInnerCard(float padX, float padY) =>
		new()
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

	private static StyleBoxFlat MakeFlat(Color bg, int radius) =>
		new()
		{
			BgColor = bg,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerDetail = 4,
		};

	private static StyleBoxFlat MakeCardStyle() =>
		new()
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

	private static StyleBoxFlat MakePlayerRowStyle(bool selected, bool even, bool hover = false) =>
		new()
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

	private static StyleBoxFlat MakeFilledButton(Color bg) =>
		new()
		{
			BgColor = bg,
			ContentMarginLeft = 14,
			ContentMarginTop = 8,
			ContentMarginRight = 14,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};

	private static StyleBoxFlat MakeOutlinedButton(Color bg, Color border) =>
		new()
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};

	private static StyleBoxFlat MakePill(Color bg, float padX, float padY = 5) =>
		new()
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
