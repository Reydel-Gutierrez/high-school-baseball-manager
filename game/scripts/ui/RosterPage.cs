using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// Roster for the active program team (Varsity or JV). Players, title, and
/// counts all come from GameSession.ActiveTeam so this page stays in sync
/// with the top-bar team switch.
/// </summary>
public partial class RosterPage : Control
{
	private const string FilterAll = "all";
	private const string FilterPitchers = "pitchers";
	private const string FilterCatchers = "catchers";
	private const string FilterInfield = "infield";
	private const string FilterOutfield = "outfield";
	private const int RosterCapJv = 20;
	private const int RosterCapVarsity = 20;
	private const int CurrentSeasonYear = 2026;

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color RowSelected = new(0.956863f, 0.643137f, 0.109804f, 0.14f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color BarBg = new(0.121569f, 0.137255f, 0.160784f, 1f);

	private readonly List<RosterPlayer> _players = BuildPrototypeRoster();
	private readonly Dictionary<string, Button> _filterButtons = new();
	private readonly Dictionary<int, PanelContainer> _rowByJersey = new();

	private GameSession _session = null!;
	private VBoxContainer _rowsHost = null!;
	private VBoxContainer _detailHost = null!;
	private ScrollContainer _playerList = null!;
	private Label _title = null!;
	private Label _countValue = null!;
	private Label _jvValue = null!;
	private Label _varsityValue = null!;
	private Label _injuredValue = null!;
	private ShaderMaterial _circleMaterial = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Control _actionOverlay = null!;
	private VBoxContainer _actionBody = null!;
	private Label _seasonTitle = null!;
	private Label _seasonKindLabel = null!;
	private VBoxContainer _seasonBody = null!;
	private readonly Dictionary<int, Button> _seasonYearButtons = new();
	private string _activeFilter = FilterAll;
	private int _selectedJersey = 11;
	private int _selectedSeasonYear = CurrentSeasonYear;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");

		var shader = GD.Load<Shader>("res://assets/ui/circle_crop.gdshader");
		_circleMaterial = new ShaderMaterial { Shader = shader };

		_session = GetNode<GameSession>("/root/GameSession");
		_rowsHost = GetNode<VBoxContainer>("%RowsHost");
		_detailHost = GetNode<VBoxContainer>("%PlayerDetailHost");
		_playerList = GetNode<ScrollContainer>("%PlayerList");
		_title = GetNode<Label>("%Title");
		_countValue = GetNode<Label>("%CountValue");
		_jvValue = GetNode<Label>("%JvValue");
		_varsityValue = GetNode<Label>("%VarsityValue");
		_injuredValue = GetNode<Label>("%InjuredValue");

		RegisterFilter("%FilterAll", FilterAll);
		RegisterFilter("%FilterPitchers", FilterPitchers);
		RegisterFilter("%FilterCatchers", FilterCatchers);
		RegisterFilter("%FilterInfield", FilterInfield);
		RegisterFilter("%FilterOutfield", FilterOutfield);

		BuildActionOverlay();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		ApplyActiveTeam();
		ApplyFilter(_activeFilter);
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _)
	{
		ApplyActiveTeam();
		RebuildRows();
	}

	private void ApplyActiveTeam()
	{
		_title.Text = _session.ActiveTeam.RosterTitle.ToUpperInvariant();
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

		if (key.Keycode is not (Key.Up or Key.Down))
		{
			return;
		}

		SelectAdjacent(key.Keycode == Key.Down ? 1 : -1);
		GetViewport().SetInputAsHandled();
	}

	private void RegisterFilter(string uniquePath, string filterKey)
	{
		Button button = GetNode<Button>(uniquePath);
		_filterButtons[filterKey] = button;
		button.Pressed += () => ApplyFilter(filterKey);
	}

	private void ApplyFilter(string filterKey)
	{
		_activeFilter = filterKey;
		UpdateFilterStyles();
		RebuildRows();
	}

	private void UpdateFilterStyles()
	{
		foreach (KeyValuePair<string, Button> pair in _filterButtons)
		{
			bool isActive = pair.Key == _activeFilter;
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

	private void SelectAdjacent(int delta)
	{
		List<RosterPlayer> visible = GetVisiblePlayers();
		if (visible.Count == 0)
		{
			return;
		}

		int index = visible.FindIndex(player => player.Jersey == _selectedJersey);
		if (index < 0)
		{
			index = 0;
		}

		int next = Mathf.Clamp(index + delta, 0, visible.Count - 1);
		SelectPlayer(visible[next].Jersey);
	}

	private void SelectPlayer(int jersey)
	{
		if (_selectedJersey == jersey && _detailHost.GetChildCount() > 0)
		{
			return;
		}

		int previous = _selectedJersey;
		_selectedJersey = jersey;
		_selectedSeasonYear = CurrentSeasonYear;
		RefreshRowStyle(previous);
		RefreshRowStyle(jersey);
		BindSelectedPlayer();
		ScrollSelectedIntoView();
	}

	private void RebuildRows()
	{
		foreach (Node child in _rowsHost.GetChildren())
		{
			child.QueueFree();
		}

		_rowByJersey.Clear();
		UpdateRosterSummary();

		List<RosterPlayer> visible = GetVisiblePlayers();

		if (visible.Count == 0)
		{
			var empty = MakeText("No players in this group.", _medium, 13, TextMuted, HorizontalAlignment.Center);
			_rowsHost.AddChild(empty);
			ClearDetail();
			return;
		}

		if (!visible.Exists(player => player.Jersey == _selectedJersey))
		{
			_selectedJersey = visible[0].Jersey;
			_selectedSeasonYear = CurrentSeasonYear;
		}

		for (int i = 0; i < visible.Count; i++)
		{
			PanelContainer row = BuildRow(visible[i], i);
			_rowByJersey[visible[i].Jersey] = row;
			_rowsHost.AddChild(row);
		}

		BindSelectedPlayer();
		Callable.From(ScrollSelectedIntoView).CallDeferred();
	}

	private void UpdateRosterSummary()
	{
		TeamLevel activeLevel = _session.ActiveTeam.Level;
		int jv = 0;
		int varsity = 0;
		int injured = 0;

		foreach (RosterPlayer player in _players)
		{
			if (TeamLevel.JuniorVarsity.MatchesSquad(player.Squad))
			{
				jv++;
			}
			else
			{
				varsity++;
			}

			if (activeLevel.MatchesSquad(player.Squad)
				&& !string.Equals(player.Status, "Healthy", StringComparison.OrdinalIgnoreCase))
			{
				injured++;
			}
		}

		_countValue.Text = FormatCap(_players.Count, _session.Organization.RosterCap);
		_jvValue.Text = FormatCap(jv, RosterCapJv);
		_varsityValue.Text = FormatCap(varsity, RosterCapVarsity);
		_injuredValue.Text = FormatInt(injured);
		_injuredValue.AddThemeColorOverride("font_color", injured > 0 ? Urgent : Green);
	}

	private void BindSelectedPlayer()
	{
		RosterPlayer? player = FindPlayer(_selectedJersey);
		if (player == null)
		{
			ClearDetail();
			return;
		}

		BindPlayerDetail(player);
	}

	private void ClearDetail()
	{
		foreach (Node child in _detailHost.GetChildren())
		{
			child.QueueFree();
		}
	}

	private void ScrollSelectedIntoView()
	{
		if (_rowByJersey.TryGetValue(_selectedJersey, out PanelContainer? row))
		{
			_playerList.EnsureControlVisible(row);
		}
	}

	private void RefreshRowStyle(int jersey)
	{
		if (!_rowByJersey.TryGetValue(jersey, out PanelContainer? row))
		{
			return;
		}

		int index = (int)row.GetMeta("row_index");
		bool selected = jersey == _selectedJersey;
		Color rowColor = selected ? RowSelected : (index % 2 == 0 ? RowEven : RowOdd);
		row.AddThemeStyleboxOverride("panel", MakeRowStyle(rowColor, selected));
	}

	private PanelContainer BuildRow(RosterPlayer player, int visualIndex)
	{
		bool selected = player.Jersey == _selectedJersey;
		Color rowColor = selected ? RowSelected : (visualIndex % 2 == 0 ? RowEven : RowOdd);

		var row = new PanelContainer
		{
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
		};
		row.SetMeta("row_index", visualIndex);
		row.AddThemeStyleboxOverride("panel", MakeRowStyle(rowColor, selected));
		row.GuiInput += @event =>
		{
			if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				int jersey = player.Jersey;
				Callable.From(() => SelectPlayer(jersey)).CallDeferred();
			}
		};
		row.MouseEntered += () =>
		{
			if (player.Jersey != _selectedJersey)
			{
				row.AddThemeStyleboxOverride("panel", MakeRowStyle(HoverBg, false));
			}
		};
		row.MouseExited += () =>
		{
			if (player.Jersey != _selectedJersey)
			{
				Color restored = visualIndex % 2 == 0 ? RowEven : RowOdd;
				row.AddThemeStyleboxOverride("panel", MakeRowStyle(restored, false));
			}
		};

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		row.AddChild(columns);

		columns.AddChild(MakeCell(FormatInt(player.Jersey), 28, _bold, 13, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(BuildPortrait(player, 28));
		columns.AddChild(BuildNameCell(player));
		columns.AddChild(MakeCell(player.Position, 84, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeCell(player.Year, 28, _semibold, 12, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeCell(player.BatsThrows, 36, _medium, 12, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeCell(FormatInt(player.Overall), 36, _bold, 14, OverallColor(player.Overall), HorizontalAlignment.Center));
		columns.AddChild(BuildStatusCell(player.Status));

		return row;
	}

	private void BindPlayerDetail(RosterPlayer player)
	{
		ClearDetail();

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

		layout.AddChild(BuildProfileHeader(player));
		layout.AddChild(BuildRatingsRow(player));
		layout.AddChild(MakeAccentLine());
		layout.AddChild(BuildSeasonStats(player));
	}

	private Control BuildProfileHeader(RosterPlayer player)
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

		var last = MakeText(player.LastName.ToUpperInvariant(), _bold, 22, TextPrimary, HorizontalAlignment.Left);
		identity.AddChild(last);

		var first = MakeText(player.FirstName, _medium, 14, TextMuted, HorizontalAlignment.Left);
		identity.AddChild(first);

		var meta = MakeText(
			$"#{FormatInt(player.Jersey)}  ·  {player.Position}  ·  {player.Year}  ·  {player.BatsThrows}  ·  {player.Squad.ToUpperInvariant()}",
			_semibold,
			12,
			TextPrimary,
			HorizontalAlignment.Left);
		identity.AddChild(meta);

		var statusRow = new HBoxContainer();
		statusRow.AddThemeConstantOverride("separation", 8);
		statusRow.AddChild(MakeText(player.Status.ToUpperInvariant(), _semibold, 12, StatusColor(player.Status), HorizontalAlignment.Left));
		if (player.IsCaptain)
		{
			statusRow.AddChild(MakeText("CAPTAIN", _semibold, 12, Accent, HorizontalAlignment.Left));
		}

		identity.AddChild(statusRow);
		header.AddChild(identity);
		header.AddChild(BuildActionButton());
		return header;
	}

	private Button BuildActionButton()
	{
		var button = new Button
		{
			Text = "MANAGE PLAYER",
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
			Flat = false,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", TextOnAccent);
		button.AddThemeColorOverride("font_hover_color", TextOnAccent);
		button.AddThemeColorOverride("font_pressed_color", TextOnAccent);
		button.AddThemeColorOverride("font_focus_color", TextOnAccent);
		button.AddThemeStyleboxOverride("normal", MakeFilledButtonStyle(Accent));
		button.AddThemeStyleboxOverride("hover", MakeFilledButtonStyle(AccentHover));
		button.AddThemeStyleboxOverride("pressed", MakeFilledButtonStyle(AccentHover));
		button.AddThemeStyleboxOverride("focus", MakeFilledButtonStyle(AccentHover));
		button.Pressed += OpenPlayerActions;
		return button;
	}

	private void BuildActionOverlay()
	{
		var layer = new CanvasLayer { Layer = 40 };
		AddChild(layer);

		_actionOverlay = new Control
		{
			Name = "PlayerActionOverlay",
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

		var center = new CenterContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
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
		RosterPlayer? player = FindPlayer(_selectedJersey);
		if (player == null)
		{
			return;
		}

		while (_actionBody.GetChildCount() > 0)
		{
			Node child = _actionBody.GetChild(0);
			_actionBody.RemoveChild(child);
			child.QueueFree();
		}

		_actionBody.AddChild(MakeText("PLAYER ACTIONS", _semibold, 11, Accent, HorizontalAlignment.Left));
		_actionBody.AddChild(MakeText(player.LastName.ToUpperInvariant(), _bold, 22, TextPrimary, HorizontalAlignment.Left));
		_actionBody.AddChild(MakeText(
			$"{player.FirstName}  ·  #{FormatInt(player.Jersey)}  ·  {player.Position}  ·  {player.Squad.ToUpperInvariant()}",
			_medium,
			12,
			TextMuted,
			HorizontalAlignment.Left));
		_actionBody.AddChild(MakeAccentLine());

		bool isVarsity = TeamLevel.Varsity.MatchesSquad(player.Squad);
		_actionBody.AddChild(MakeModalOption(
			"PROMOTE TO VARSITY",
			TextPrimary,
			isVarsity,
			() => ApplyPlayerAction("promote")));
		_actionBody.AddChild(MakeModalOption(
			"DEMOTE TO JV",
			TextPrimary,
			!isVarsity,
			() => ApplyPlayerAction("demote")));
		_actionBody.AddChild(MakeModalOption(
			player.IsCaptain ? "REMOVE CAPTAIN" : "MAKE CAPTAIN",
			TextPrimary,
			false,
			() => ApplyPlayerAction("captain")));
		_actionBody.AddChild(MakeModalOption(
			"RELEASE FROM ORGANIZATION",
			Urgent,
			false,
			() => ApplyPlayerAction("release")));
		_actionBody.AddChild(MakeModalOption("CANCEL", TextMuted, false, ClosePlayerActions));

		_actionOverlay.Visible = true;
	}

	private void ClosePlayerActions()
	{
		_actionOverlay.Visible = false;
	}

	private void ApplyPlayerAction(string action)
	{
		RosterPlayer? player = FindPlayer(_selectedJersey);
		if (player == null)
		{
			ClosePlayerActions();
			return;
		}

		switch (action)
		{
			case "promote":
				ReplacePlayer(player with { Squad = TeamLevel.Varsity.ToSquadKey() });
				break;
			case "demote":
				ReplacePlayer(player with { Squad = TeamLevel.JuniorVarsity.ToSquadKey() });
				break;
			case "captain":
				ReplacePlayer(player with { IsCaptain = !player.IsCaptain });
				break;
			case "release":
				ReleaseSelectedPlayer();
				break;
		}

		ClosePlayerActions();
		RebuildRows();
	}

	private void ReplacePlayer(RosterPlayer updated)
	{
		int index = _players.FindIndex(existing => existing.Jersey == updated.Jersey);
		if (index >= 0)
		{
			_players[index] = updated;
		}

		_selectedJersey = updated.Jersey;
	}

	private void ReleaseSelectedPlayer()
	{
		int released = _selectedJersey;
		List<RosterPlayer> visible = GetVisiblePlayers();
		int index = visible.FindIndex(player => player.Jersey == released);
		_players.RemoveAll(player => player.Jersey == released);

		List<RosterPlayer> remaining = GetVisiblePlayers();
		if (remaining.Count > 0)
		{
			_selectedJersey = remaining[Mathf.Clamp(index, 0, remaining.Count - 1)].Jersey;
		}
		else if (_players.Count > 0)
		{
			_selectedJersey = _players[0].Jersey;
		}
	}

	private Button MakeModalOption(string text, Color fontColor, bool disabled, Action onPressed)
	{
		var button = new Button
		{
			Text = text,
			Disabled = disabled,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 13);
		button.AddThemeColorOverride("font_color", disabled ? new Color(0.40f, 0.42f, 0.46f) : fontColor);
		button.AddThemeColorOverride("font_hover_color", disabled ? new Color(0.40f, 0.42f, 0.46f) : Colors.White);
		button.AddThemeColorOverride("font_pressed_color", fontColor);
		button.AddThemeColorOverride("font_disabled_color", new Color(0.40f, 0.42f, 0.46f));
		button.AddThemeStyleboxOverride("normal", MakeModalButtonStyle(CardInner, new Color(0.28f, 0.30f, 0.35f)));
		button.AddThemeStyleboxOverride("hover", MakeModalButtonStyle(HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", MakeModalButtonStyle(HoverBg, Accent));
		button.AddThemeStyleboxOverride("focus", MakeModalButtonStyle(HoverBg, Accent));
		button.AddThemeStyleboxOverride("disabled", MakeModalButtonStyle(new Color(0.05f, 0.055f, 0.07f, 0.9f), new Color(0.18f, 0.20f, 0.24f)));
		if (!disabled)
		{
			button.Pressed += onPressed;
		}

		return button;
	}

	private static StyleBoxFlat MakeFilledButtonStyle(Color bg)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			DrawCenter = true,
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
	}

	private static StyleBoxFlat MakeModalButtonStyle(Color bg, Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 14,
			ContentMarginTop = 10,
			ContentMarginRight = 14,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};
	}

	private Control BuildRatingsRow(RosterPlayer player)
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
		var chip = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		chip.AddThemeStyleboxOverride("panel", MakeInnerCard(5, 5));

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 2);
		chip.AddChild(box);

		box.AddChild(MakeText(label, _semibold, 8, label == "OVR" ? Accent : TextMuted, HorizontalAlignment.Center));
		box.AddChild(MakeText(FormatInt(value), _bold, 16, OverallColor(value), HorizontalAlignment.Center));

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

	private Control BuildSeasonStats(RosterPlayer player)
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

	private Control BuildSeasonYearTabs(RosterPlayer player)
	{
		_seasonYearButtons.Clear();
		var row = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		row.AddThemeConstantOverride("separation", 3);

		foreach (PlayerSeason season in player.Seasons)
		{
			int year = season.Year;
			var button = new Button
			{
				Text = FormatInt(year),
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
		if (_selectedSeasonYear == year)
		{
			return;
		}

		RosterPlayer? player = FindPlayer(_selectedJersey);
		if (player == null)
		{
			return;
		}

		_selectedSeasonYear = year;
		_seasonTitle.Text = $"{year} SEASON";
		RefreshSeasonYearTabs();
		FillSeasonStats(player);
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

	private void FillSeasonStats(RosterPlayer player)
	{
		while (_seasonBody.GetChildCount() > 0)
		{
			Node child = _seasonBody.GetChild(0);
			_seasonBody.RemoveChild(child);
			child.QueueFree();
		}

		PlayerSeason? season = FindSeason(player, _selectedSeasonYear);
		if (season == null)
		{
			_seasonKindLabel.Text = string.Empty;
			_seasonBody.AddChild(MakeText("No season stats.", _medium, 12, TextMuted, HorizontalAlignment.Left));
			return;
		}

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

	private void EnsureSelectedSeason(RosterPlayer player)
	{
		if (player.Seasons.Count == 0)
		{
			return;
		}

		foreach (PlayerSeason season in player.Seasons)
		{
			if (season.Year == _selectedSeasonYear)
			{
				return;
			}
		}

		_selectedSeasonYear = player.Seasons[player.Seasons.Count - 1].Year;
	}

	private static PlayerSeason? FindSeason(RosterPlayer player, int year)
	{
		PlayerSeason? fallback = null;
		foreach (PlayerSeason season in player.Seasons)
		{
			fallback = season;
			if (season.Year == year)
			{
				return season;
			}
		}

		return fallback;
	}

	private Control BuildHittingSheet(SeasonHitting stats, bool includeHeadline)
	{
		float ops = stats.OnBase + stats.Slugging;
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
				(FormatInt(stats.HomeRuns), "Home Runs"),
				(FormatInt(stats.Rbi), "RBI"),
				(FormatAvg(ops), "OPS"),
			]));
		}

		sheet.AddChild(BuildBoxScore(
		[
			("G", "Games", FormatInt(stats.Games)),
			("AB", "At Bats", FormatInt(stats.AtBats)),
			("R", "Runs", FormatInt(stats.Runs)),
			("H", "Hits", FormatInt(stats.Hits)),
			("2B", "Doubles", FormatInt(stats.Doubles)),
			("3B", "Triples", FormatInt(stats.Triples)),
			("HR", "Home Runs", FormatInt(stats.HomeRuns)),
			("RBI", "Runs Batted In", FormatInt(stats.Rbi)),
			("BB", "Walks", FormatInt(stats.Walks)),
			("SO", "Strikeouts", FormatInt(stats.Strikeouts)),
			("SB", "Stolen Bases", FormatInt(stats.StolenBases)),
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

	private Control BuildPitchingSheet(SeasonPitching stats)
	{
		var sheet = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		sheet.AddThemeConstantOverride("separation", 10);

		sheet.AddChild(BuildHeadlineRow(
		[
			(FormatEra(stats.Era), "ERA"),
			($"{FormatInt(stats.Wins)}-{FormatInt(stats.Losses)}", "W-L"),
			(FormatInt(stats.Strikeouts), "Strikeouts"),
			(FormatEra(stats.Whip), "WHIP"),
		]));

		sheet.AddChild(BuildBoxScore(
		[
			("G", "Games", FormatInt(stats.Games)),
			("GS", "Games Started", FormatInt(stats.GamesStarted)),
			("IP", "Innings Pitched", FormatIp(stats.Innings)),
			("H", "Hits Allowed", FormatInt(stats.Hits)),
			("R", "Runs Allowed", FormatInt(stats.Runs)),
			("ER", "Earned Runs", FormatInt(stats.EarnedRuns)),
			("BB", "Walks", FormatInt(stats.Walks)),
			("SO", "Strikeouts", FormatInt(stats.Strikeouts)),
			("HR", "Home Runs Allowed", FormatInt(stats.HomeRuns)),
			("SV", "Saves", FormatInt(stats.Saves)),
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
				row.AddChild(MakeVerticalRule());
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
		table.AddChild(MakeHairline());
		table.AddChild(values);
		return panel;
	}

	private static ColorRect MakeHairline()
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 1),
			Color = new Color(0.28f, 0.30f, 0.35f, 1f),
		};
	}

	private static ColorRect MakeVerticalRule()
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(1, 0),
			Color = new Color(0.28f, 0.30f, 0.35f, 1f),
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
	}

	private Control BuildPortrait(RosterPlayer player, float size)
	{
		var wrap = new Control
		{
			CustomMinimumSize = new Vector2(size, size),
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
			};
			portrait.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			wrap.AddChild(portrait);
			return wrap;
		}

		int radius = Mathf.RoundToInt(size / 2f);
		var fallback = new PanelContainer();
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
			player.LastName[..1].ToUpperInvariant(),
			_bold,
			Mathf.RoundToInt(size * 0.38f),
			Accent,
			HorizontalAlignment.Center);
		initial.VerticalAlignment = VerticalAlignment.Center;
		fallback.AddChild(initial);
		wrap.AddChild(fallback);
		return wrap;
	}

	private Control BuildNameCell(RosterPlayer player)
	{
		var box = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		box.AddThemeConstantOverride("separation", -2);
		var nameRow = new HBoxContainer();
		nameRow.AddThemeConstantOverride("separation", 6);
		nameRow.AddChild(MakeText(player.LastName.ToUpperInvariant(), _semibold, 13, TextPrimary, HorizontalAlignment.Left));
		if (player.IsCaptain)
		{
			nameRow.AddChild(MakeText("C", _bold, 12, Accent, HorizontalAlignment.Left));
		}

		box.AddChild(nameRow);
		box.AddChild(MakeText(player.FirstName, _medium, 10, TextMuted, HorizontalAlignment.Left));
		return box;
	}

	private Label BuildStatusCell(string status)
	{
		var label = MakeText(status.ToUpperInvariant(), _semibold, 11, StatusColor(status), HorizontalAlignment.Right);
		label.CustomMinimumSize = new Vector2(84, 0);
		label.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		label.VerticalAlignment = VerticalAlignment.Center;
		return label;
	}

	private static Label MakeCell(string text, float width, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		Label label = MakeText(text, font, size, color, align);
		label.CustomMinimumSize = new Vector2(width, 0);
		label.VerticalAlignment = VerticalAlignment.Center;
		label.ClipText = true;
		return label;
	}

	private static Label MakeText(string text, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = align,
		};
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static ColorRect MakeAccentLine()
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 2),
			Color = Accent,
		};
	}

	private List<RosterPlayer> GetVisiblePlayers()
	{
		TeamLevel activeLevel = _session.ActiveTeam.Level;
		var visible = new List<RosterPlayer>();
		foreach (RosterPlayer player in _players)
		{
			if (activeLevel.MatchesSquad(player.Squad) && MatchesFilter(player, _activeFilter))
			{
				visible.Add(player);
			}
		}

		return visible;
	}

	private RosterPlayer? FindPlayer(int jersey)
	{
		foreach (RosterPlayer player in _players)
		{
			if (player.Jersey == jersey)
			{
				return player;
			}
		}

		return null;
	}

	private static string FormatInt(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static string FormatCap(int current, int cap) => $"{FormatInt(current)}/{FormatInt(cap)}";

	private static string FormatEra(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

	private static string FormatIp(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

	private static string FormatAvg(float value) => value.ToString(".000", CultureInfo.InvariantCulture);

	private static bool MatchesFilter(RosterPlayer player, string filter)
	{
		return filter switch
		{
			FilterPitchers => player.Group == FilterPitchers,
			FilterCatchers => player.Group == FilterCatchers,
			FilterInfield => player.Group == FilterInfield,
			FilterOutfield => player.Group == FilterOutfield,
			_ => true,
		};
	}

	private static Color OverallColor(int value)
	{
		if (value >= 85)
		{
			return Green;
		}

		if (value >= 78)
		{
			return Accent;
		}

		if (value >= 70)
		{
			return TextPrimary;
		}

		return TextMuted;
	}

	private static Color StatusColor(string status)
	{
		return status.ToLowerInvariant() switch
		{
			"healthy" => Green,
			"day-to-day" => Accent,
			_ => Urgent,
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

	private static StyleBoxFlat MakeRowStyle(Color bg, bool selected)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = selected ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = selected ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 10,
			ContentMarginTop = 6,
			ContentMarginRight = 10,
			ContentMarginBottom = 6,
		};
	}

	private static StyleBoxFlat MakeInnerCard(float padX, float padY)
	{
		return new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = new Color(0.28f, 0.30f, 0.35f, 1f),
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
			CornerDetail = 3,
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

	private static List<RosterPlayer> BuildPrototypeRoster()
	{
		// Cascade program prototype: ~20 Varsity and ~18 JV, near season caps.
		// Replace with a query keyed by GameSession.ActiveTeam.Id.
		const string marini = "res://assets/teams/01_Cascade_Regional_High_School/Players/Cascade_player_01_Marini.png";

		return
		[
			MakePlayer("Luca", "Marini", 11, "RHP", "SR", "R/R", FilterPitchers, 88, 62, 58, 71, 90, 64, "Healthy", marini,
				null,
				new SeasonPitching(8, 8, 5, 0, 0, 36.0f, 24, 10, 8, 8, 28, 1, 1.95f, 0.89f)),
			MakePlayer("Jalen", "Brooks", 7, "CF", "SR", "L/R", FilterOutfield, 84, 82, 71, 91, 80, 86, "Healthy", null,
				new SeasonHitting(22, 78, 24, 31, 7, 2, 4, 18, 12, 11, 16, 0.397f, 0.468f, 0.692f),
				null),
			MakePlayer("Mateo", "Cruz", 21, "C", "JR", "R/R", FilterCatchers, 81, 74, 78, 58, 86, 84, "Healthy", null,
				new SeasonHitting(21, 70, 14, 23, 6, 0, 5, 22, 9, 13, 1, 0.329f, 0.405f, 0.629f),
				null),
			MakePlayer("Drew", "Hale", 4, "SS/2B", "JR", "R/R", FilterInfield, 80, 79, 68, 84, 82, 88, "Healthy", null,
				new SeasonHitting(22, 76, 19, 26, 5, 1, 2, 15, 8, 12, 9, 0.342f, 0.405f, 0.513f),
				null),
			MakePlayer("Isaiah", "Cole", 9, "1B", "SR", "L/L", FilterInfield, 79, 83, 86, 48, 72, 70, "Healthy", null,
				new SeasonHitting(22, 74, 16, 24, 4, 0, 7, 24, 11, 16, 0, 0.324f, 0.412f, 0.662f),
				null),
			MakePlayer("Trey", "Lawson", 32, "LHP", "SR", "L/L", FilterPitchers, 76, 40, 38, 54, 84, 42, "Healthy", null,
				null,
				new SeasonPitching(7, 7, 3, 1, 0, 29.1f, 26, 12, 11, 11, 24, 2, 2.66f, 1.26f)),
			MakePlayer("Noah", "Barrett", 15, "3B/1B", "JR", "R/R", FilterInfield, 77, 76, 81, 62, 85, 74, "Healthy", null,
				new SeasonHitting(21, 69, 13, 21, 5, 0, 4, 17, 7, 15, 2, 0.304f, 0.368f, 0.551f),
				null),
			MakePlayer("Cam", "Whitaker", 2, "2B/SS", "SO", "S/R", FilterInfield, 74, 78, 61, 82, 74, 80, "Healthy", null,
				new SeasonHitting(20, 61, 15, 19, 3, 1, 1, 9, 10, 9, 11, 0.311f, 0.408f, 0.443f),
				null),
			MakePlayer("Eli", "Vargas", 24, "LF/RF", "SR", "L/R", FilterOutfield, 73, 80, 77, 68, 71, 66, "Day-to-Day", null,
				new SeasonHitting(16, 52, 10, 16, 4, 0, 3, 11, 6, 10, 3, 0.308f, 0.379f, 0.558f),
				null),
			MakePlayer("Ryan", "Peck", 18, "RF/LF", "JR", "R/R", FilterOutfield, 72, 75, 79, 70, 83, 69, "Healthy", null,
				new SeasonHitting(20, 64, 12, 18, 3, 1, 3, 14, 7, 14, 4, 0.281f, 0.352f, 0.500f),
				null),
			MakePlayer("Ben", "Ortiz", 8, "RHP", "JR", "R/R", FilterPitchers, 71, 36, 34, 52, 79, 40, "Healthy", null,
				null,
				new SeasonPitching(9, 4, 2, 1, 1, 24.2f, 22, 11, 10, 9, 19, 1, 3.28f, 1.26f)),
			MakePlayer("Miles", "Grant", 5, "RHP", "SO", "R/R", FilterPitchers, 68, 33, 30, 58, 76, 38, "Healthy", null,
				null,
				new SeasonPitching(8, 2, 1, 2, 0, 16.0f, 18, 10, 9, 8, 12, 2, 5.06f, 1.63f),
				"JV"),
			MakePlayer("Owen", "Drake", 14, "C/1B", "SO", "R/R", FilterCatchers, 67, 64, 70, 50, 74, 72, "Healthy", null,
				new SeasonHitting(12, 31, 4, 8, 2, 0, 1, 6, 3, 9, 0, 0.258f, 0.324f, 0.419f),
				null,
				"JV"),
			MakePlayer("Sam", "Keene", 6, "CF/LF", "FR", "L/R", FilterOutfield, 64, 66, 58, 78, 62, 60, "Healthy", null,
				new SeasonHitting(11, 28, 6, 7, 1, 1, 0, 3, 4, 8, 5, 0.250f, 0.344f, 0.357f),
				null,
				"JV"),
			MakePlayer("Chris", "Nolan", 27, "2B/SS", "FR", "R/R", FilterInfield, 62, 61, 55, 70, 64, 68, "Healthy", null,
				new SeasonHitting(9, 22, 3, 5, 1, 0, 0, 2, 2, 7, 1, 0.227f, 0.292f, 0.273f),
				null,
				"JV"),
			MakePlayer("Jake", "Pruitt", 12, "RHP", "SR", "R/R", FilterPitchers, 74, 34, 32, 50, 82, 38, "Healthy", null,
				null,
				PrototypePitching("SR", 74, false)),
			MakePlayer("Cole", "Reid", 22, "RHP", "JR", "R/R", FilterPitchers, 73, 38, 36, 56, 80, 40, "Healthy", null,
				null,
				PrototypePitching("JR", 73, true)),
			MakePlayer("Devin", "Shore", 28, "LHP", "JR", "L/L", FilterPitchers, 70, 36, 34, 52, 78, 38, "Healthy", null,
				null,
				PrototypePitching("JR", 70, false)),
			MakePlayer("Finn", "Walsh", 16, "C/1B", "SO", "R/R", FilterCatchers, 70, 66, 68, 48, 76, 74, "Healthy", null,
				PrototypeHitting("SO", 70, 48),
				null),
			MakePlayer("Marcus", "Bell", 33, "1B/3B", "SR", "R/R", FilterInfield, 72, 74, 80, 42, 68, 64, "Healthy", null,
				PrototypeHitting("SR", 72, 42),
				null),
			MakePlayer("Andre", "Holt", 3, "LF/CF/RF", "JR", "L/R", FilterOutfield, 71, 73, 70, 74, 68, 66, "Healthy", null,
				PrototypeHitting("JR", 71, 74),
				null),
			MakePlayer("Quinn", "Mercer", 10, "SS/2B", "SO", "R/R", FilterInfield, 69, 70, 58, 80, 74, 78, "Healthy", null,
				PrototypeHitting("SO", 69, 80),
				null),
			MakePlayer("Tyler", "Knox", 19, "RF/CF", "SO", "R/R", FilterOutfield, 68, 69, 72, 66, 76, 64, "Healthy", null,
				PrototypeHitting("SO", 68, 66),
				null),
			MakePlayer("Leo", "Hayes", 17, "RHP", "FR", "R/R", FilterPitchers, 64, 32, 30, 54, 74, 36, "Healthy", null,
				null,
				PrototypePitching("FR", 64, true),
				"JV"),
			MakePlayer("Mason", "Crowe", 23, "LHP", "SO", "L/L", FilterPitchers, 66, 34, 32, 50, 76, 36, "Healthy", null,
				null,
				PrototypePitching("SO", 66, true),
				"JV"),
			MakePlayer("Brett", "Lang", 29, "RHP", "FR", "R/R", FilterPitchers, 61, 30, 28, 52, 72, 34, "Healthy", null,
				null,
				PrototypePitching("FR", 61, false),
				"JV"),
			MakePlayer("Seth", "Bowman", 40, "RHP", "SO", "R/R", FilterPitchers, 65, 33, 30, 48, 75, 36, "Healthy", null,
				null,
				PrototypePitching("SO", 65, false),
				"JV"),
			MakePlayer("Nico", "Palumbo", 13, "C", "FR", "R/R", FilterCatchers, 63, 60, 64, 46, 70, 68, "Healthy", null,
				PrototypeHitting("FR", 63, 46),
				null,
				"JV"),
			MakePlayer("Evan", "Briggs", 1, "SS/2B", "SO", "R/R", FilterInfield, 66, 65, 54, 76, 70, 74, "Healthy", null,
				PrototypeHitting("SO", 66, 76),
				null,
				"JV"),
			MakePlayer("Cody", "Rhine", 20, "2B/SS", "FR", "S/R", FilterInfield, 60, 62, 48, 74, 62, 70, "Healthy", null,
				PrototypeHitting("FR", 60, 74),
				null,
				"JV"),
			MakePlayer("Will", "Hargrove", 25, "3B/1B", "SO", "R/R", FilterInfield, 65, 64, 70, 52, 74, 66, "Healthy", null,
				PrototypeHitting("SO", 65, 52),
				null,
				"JV"),
			MakePlayer("Jonah", "Vale", 31, "1B", "FR", "L/L", FilterInfield, 61, 63, 68, 40, 58, 60, "Healthy", null,
				PrototypeHitting("FR", 61, 40),
				null,
				"JV"),
			MakePlayer("Alex", "Ruiz", 34, "CF/LF", "SO", "R/R", FilterOutfield, 67, 66, 58, 82, 68, 70, "Healthy", null,
				PrototypeHitting("SO", 67, 82),
				null,
				"JV"),
			MakePlayer("Henry", "Cho", 36, "LF/RF", "FR", "L/R", FilterOutfield, 59, 60, 56, 70, 60, 58, "Healthy", null,
				PrototypeHitting("FR", 59, 70),
				null,
				"JV"),
			MakePlayer("Parker", "Dean", 38, "RF/LF", "SO", "R/R", FilterOutfield, 64, 63, 66, 62, 72, 60, "Day-to-Day", null,
				PrototypeHitting("SO", 64, 62),
				null,
				"JV"),
			MakePlayer("Ian", "Frost", 42, "2B/3B", "FR", "R/R", FilterInfield, 58, 57, 50, 68, 60, 66, "Healthy", null,
				PrototypeHitting("FR", 58, 68),
				null,
				"JV"),
			MakePlayer("Luke", "Santos", 44, "LF/CF/RF", "FR", "L/R", FilterOutfield, 57, 58, 52, 72, 58, 56, "Healthy", null,
				PrototypeHitting("FR", 57, 72),
				null,
				"JV"),
		];
	}

	private static RosterPlayer MakePlayer(
		string firstName,
		string lastName,
		int jersey,
		string position,
		string year,
		string batsThrows,
		string group,
		int overall,
		int contact,
		int power,
		int speed,
		int arm,
		int field,
		string status,
		string? portraitPath,
		SeasonHitting? hitting,
		SeasonPitching? pitching,
		string squad = "Varsity",
		bool isCaptain = false)
	{
		return new(
			firstName,
			lastName,
			jersey,
			position,
			year,
			batsThrows,
			group,
			overall,
			contact,
			power,
			speed,
			arm,
			field,
			status,
			portraitPath,
			BuildSeasons(year, hitting, pitching),
			squad,
			isCaptain);
	}

	private static SeasonHitting PrototypeHitting(string classYear, int overall, int speed)
	{
		float skill = Mathf.Clamp((overall - 56) / 32f, 0.08f, 1f);
		int games = classYear.ToUpperInvariant() switch
		{
			"SR" => 22,
			"JR" => 20,
			"SO" => 16,
			_ => 11,
		};
		int atBats = Math.Max(games * 2, RoundStat(games * Mathf.Lerp(2.3f, 3.5f, skill)));
		float average = Mathf.Lerp(0.208f, 0.338f, skill);
		int hits = Mathf.Clamp(RoundStat(atBats * average), 1, atBats);
		int doubles = Math.Min(hits, RoundStat(hits * Mathf.Lerp(0.12f, 0.22f, skill)));
		int triples = Math.Min(Math.Max(0, hits - doubles), speed >= 75 ? RoundStat(hits * 0.06f) : RoundStat(hits * 0.02f));
		int homeRuns = Math.Min(Math.Max(0, hits - doubles - triples), RoundStat(hits * Mathf.Lerp(0.02f, 0.16f, skill)));
		int walks = RoundStat(atBats * Mathf.Lerp(0.07f, 0.14f, skill));
		int strikeouts = RoundStat(atBats * Mathf.Lerp(0.32f, 0.16f, skill));
		int stolenBases = speed >= 70 ? RoundStat(games * Mathf.Lerp(0.15f, 0.55f, (speed - 70) / 25f)) : RoundStat(games * 0.05f);
		int runs = RoundStat(hits * 0.62f + walks * 0.28f);
		int rbi = RoundStat(hits * Mathf.Lerp(0.35f, 0.72f, skill));
		int singles = Math.Max(0, hits - doubles - triples - homeRuns);
		int plateAppearances = atBats + walks;
		float onBase = plateAppearances == 0 ? 0f : (hits + walks) / (float)plateAppearances;
		float slugging = atBats == 0 ? 0f : (singles + (2 * doubles) + (3 * triples) + (4 * homeRuns)) / (float)atBats;
		return new SeasonHitting(
			games,
			atBats,
			runs,
			hits,
			doubles,
			triples,
			homeRuns,
			rbi,
			walks,
			strikeouts,
			stolenBases,
			average,
			onBase,
			slugging);
	}

	private static SeasonPitching PrototypePitching(string classYear, int overall, bool starter)
	{
		float skill = Mathf.Clamp((overall - 56) / 32f, 0.08f, 1f);
		int games = classYear.ToUpperInvariant() switch
		{
			"SR" => starter ? 8 : 12,
			"JR" => starter ? 7 : 10,
			"SO" => starter ? 6 : 9,
			_ => starter ? 4 : 7,
		};
		int gamesStarted = starter ? Math.Max(1, games - 1) : Math.Min(2, games / 4);
		float innings = Math.Max(4f, (float)Math.Round((starter ? 3.8f : 1.4f) * games, 1));
		int wins = starter ? RoundStat(games * Mathf.Lerp(0.15f, 0.45f, skill)) : RoundStat(games * 0.12f);
		int losses = starter ? Math.Max(0, gamesStarted - wins - 1) : RoundStat(games * 0.1f);
		int saves = starter ? 0 : RoundStat(games * Mathf.Lerp(0.05f, 0.25f, skill));
		int hits = RoundStat(innings * Mathf.Lerp(1.15f, 0.72f, skill));
		int walks = RoundStat(innings * Mathf.Lerp(0.62f, 0.28f, skill));
		int strikeouts = RoundStat(innings * Mathf.Lerp(0.55f, 1.05f, skill));
		int homeRuns = RoundStat(innings * Mathf.Lerp(0.12f, 0.04f, skill));
		int runs = RoundStat(hits * 0.42f + homeRuns * 1.2f);
		int earnedRuns = Math.Max(0, runs - RoundStat(runs * 0.12f));
		float era = innings <= 0f ? 0f : earnedRuns * 9f / innings;
		float whip = innings <= 0f ? 0f : (walks + hits) / innings;
		return new SeasonPitching(
			games,
			gamesStarted,
			wins,
			losses,
			saves,
			innings,
			hits,
			runs,
			earnedRuns,
			walks,
			strikeouts,
			homeRuns,
			era,
			whip);
	}

	private static List<PlayerSeason> BuildSeasons(
		string classYear,
		SeasonHitting? currentHitting,
		SeasonPitching? currentPitching)
	{
		int count = SeasonCountForClass(classYear);
		var seasons = new List<PlayerSeason>(count);
		for (int i = 0; i < count; i++)
		{
			int year = CurrentSeasonYear - (count - 1) + i;
			float progress = count == 1 ? 1f : i / (float)(count - 1);
			seasons.Add(new PlayerSeason(
				year,
				currentHitting == null ? null : ScaleHitting(currentHitting, progress),
				currentPitching == null ? null : ScalePitching(currentPitching, progress)));
		}

		return seasons;
	}

	private static int SeasonCountForClass(string classYear) => classYear.ToUpperInvariant() switch
	{
		"FR" => 1,
		"SO" => 2,
		"JR" => 3,
		"SR" => 4,
		_ => 1,
	};

	private static SeasonHitting ScaleHitting(SeasonHitting current, float progress)
	{
		if (progress >= 0.999f)
		{
			return current;
		}

		float volume = Mathf.Lerp(0.38f, 0.86f, progress);
		float contact = Mathf.Lerp(0.84f, 0.98f, progress);
		float power = Mathf.Lerp(0.55f, 0.92f, progress);

		int games = Math.Max(4, RoundStat(current.Games * volume));
		int atBats = Math.Max(games, RoundStat(current.AtBats * volume));
		int walks = RoundStat(current.Walks * volume * contact);
		int strikeouts = RoundStat(current.Strikeouts * volume / contact);
		int hits = Mathf.Clamp(RoundStat(current.Hits * volume * contact), 0, atBats);
		int doubles = Math.Min(hits, RoundStat(current.Doubles * volume * power));
		int triples = Math.Min(Math.Max(0, hits - doubles), RoundStat(current.Triples * volume * power));
		int homeRuns = Math.Min(Math.Max(0, hits - doubles - triples), RoundStat(current.HomeRuns * volume * power));
		int runs = RoundStat(current.Runs * volume * contact);
		int rbi = RoundStat(current.Rbi * volume * power);
		int stolenBases = RoundStat(current.StolenBases * volume);

		int singles = Math.Max(0, hits - doubles - triples - homeRuns);
		int plateAppearances = atBats + walks;
		float average = atBats == 0 ? 0f : hits / (float)atBats;
		float onBase = plateAppearances == 0 ? 0f : (hits + walks) / (float)plateAppearances;
		float slugging = atBats == 0 ? 0f : (singles + (2 * doubles) + (3 * triples) + (4 * homeRuns)) / (float)atBats;

		return new SeasonHitting(
			games,
			atBats,
			runs,
			hits,
			doubles,
			triples,
			homeRuns,
			rbi,
			walks,
			strikeouts,
			stolenBases,
			average,
			onBase,
			slugging);
	}

	private static SeasonPitching ScalePitching(SeasonPitching current, float progress)
	{
		if (progress >= 0.999f)
		{
			return current;
		}

		float volume = Mathf.Lerp(0.32f, 0.88f, progress);
		float command = Mathf.Lerp(0.78f, 0.97f, progress);

		int games = Math.Max(2, RoundStat(current.Games * volume));
		int gamesStarted = Math.Min(games, RoundStat(current.GamesStarted * volume));
		int wins = RoundStat(current.Wins * volume * command);
		int losses = RoundStat(current.Losses * volume / command);
		int saves = RoundStat(current.Saves * volume);
		float innings = Math.Max(3f, (float)Math.Round(current.Innings * volume, 1));
		int hits = RoundStat(current.Hits * volume / command);
		int walks = RoundStat(current.Walks * volume / command);
		int strikeouts = RoundStat(current.Strikeouts * volume * command);
		int homeRuns = RoundStat(current.HomeRuns * volume / command);
		int runs = RoundStat(current.Runs * volume / command);
		int earnedRuns = Math.Min(runs, RoundStat(current.EarnedRuns * volume / command));
		float era = innings <= 0f ? 0f : earnedRuns * 9f / innings;
		float whip = innings <= 0f ? 0f : (walks + hits) / innings;

		return new SeasonPitching(
			games,
			gamesStarted,
			wins,
			losses,
			saves,
			innings,
			hits,
			runs,
			earnedRuns,
			walks,
			strikeouts,
			homeRuns,
			era,
			whip);
	}

	private static int RoundStat(float value) => Math.Max(0, Mathf.RoundToInt(value));

	private sealed record PlayerSeason(
		int Year,
		SeasonHitting? Hitting,
		SeasonPitching? Pitching);

	private sealed record SeasonHitting(
		int Games,
		int AtBats,
		int Runs,
		int Hits,
		int Doubles,
		int Triples,
		int HomeRuns,
		int Rbi,
		int Walks,
		int Strikeouts,
		int StolenBases,
		float Average,
		float OnBase,
		float Slugging);

	private sealed record SeasonPitching(
		int Games,
		int GamesStarted,
		int Wins,
		int Losses,
		int Saves,
		float Innings,
		int Hits,
		int Runs,
		int EarnedRuns,
		int Walks,
		int Strikeouts,
		int HomeRuns,
		float Era,
		float Whip);

	private sealed record RosterPlayer(
		string FirstName,
		string LastName,
		int Jersey,
		string Position,
		string Year,
		string BatsThrows,
		string Group,
		int Overall,
		int Contact,
		int Power,
		int Speed,
		int Arm,
		int Field,
		string Status,
		string? PortraitPath,
		IReadOnlyList<PlayerSeason> Seasons,
		string Squad = "Varsity",
		bool IsCaptain = false);
}
