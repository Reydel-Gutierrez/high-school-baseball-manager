using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// Clubhouse lineup: batting order stays on screen while the toggle and
/// right-click swap gloves. Bench and a summary strip stay visible either way.
/// </summary>
public partial class LineupsPage : Control
{
	private const int CurrentSeasonYear = 2026;
	private const string FilterAll = "all";
	private const string FilterHitters = "hitters";
	private const string FilterPitchers = "pitchers";

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color RowSelected = new(0.956863f, 0.643137f, 0.109804f, 0.14f);
	private static readonly Color RowGlove = new(0.24f, 0.62f, 0.92f, 0.16f);
	private static readonly Color BarBg = new(0.121569f, 0.137255f, 0.160784f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private ShaderMaterial _circleMaterial = null!;
	private Texture2D _cascadeLogo = null!;

	private Label _subtitle = null!;
	private Label _hint = null!;
	private Label _status = null!;
	private Label _starterLabel = null!;
	private HBoxContainer _lineupHeader = null!;
	private VBoxContainer _lineupRows = null!;
	private VBoxContainer _benchRows = null!;
	private VBoxContainer _detailHost = null!;
	private Button _orderModeButton = null!;
	private Button _positionModeButton = null!;
	private readonly Dictionary<string, Button> _filterButtons = new();

	private int _selectedSlot = -1;
	private int _gloveSlot = -1;
	private int _selectedBench;
	private string _benchFilter = FilterHitters;
	private bool _positionMode;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_circleMaterial = new ShaderMaterial
		{
			Shader = GD.Load<Shader>("res://assets/ui/circle_crop.gdshader"),
		};
		_cascadeLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		VisibilityChanged += OnVisibilityChanged;
		ApplyActiveTeam();
		RefreshAll();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
		}

		VisibilityChanged -= OnVisibilityChanged;
	}

	private void OnVisibilityChanged()
	{
		if (IsVisibleInTree())
		{
			RefreshAll();
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _)
	{
		ClearSelection();
		ApplyActiveTeam();
		RefreshAll();
	}

	private void ApplyActiveTeam()
	{
		_subtitle.Text = $"{_session.Organization.Name.ToUpperInvariant()}  ·  {_session.ActiveTeam.LevelLabel.ToUpperInvariant()}";
	}

	private void BuildPage()
	{
		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		AddChild(margin);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		margin.AddChild(layout);

		layout.AddChild(BuildHeader());

		var body = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 6);
		layout.AddChild(body);

		body.AddChild(BuildLineupCard());
		body.AddChild(BuildBenchCard());
		layout.AddChild(BuildDetailCard());
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
		identity.AddChild(MakeText($"CLUBHOUSE  ·  {CurrentSeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("LINEUP", _bold, 22, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("CASCADE REGIONAL HIGH SCHOOL", _medium, 11, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var side = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		side.AddThemeConstantOverride("separation", 2);
		_starterLabel = MakeText(string.Empty, _semibold, 12, TextPrimary, HorizontalAlignment.Right);
		side.AddChild(_starterLabel);
		_status = MakeText(string.Empty, _medium, 11, TextMuted, HorizontalAlignment.Right);
		side.AddChild(_status);
		header.AddChild(side);
		return header;
	}

	private Control BuildLineupCard()
	{
		var card = MakeCard();
		card.SizeFlagsStretchRatio = 1.45f;
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 4);
		card.AddChild(layout);

		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 10);
		titleRow.AddChild(MakeText("STARTING NINE", _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		titleRow.AddChild(spacer);
		titleRow.AddChild(BuildModeToggle());
		layout.AddChild(titleRow);

		_hint = MakeText(
			"Select a slot, then a bench player to insert. Click two slots to swap batting order.",
			_medium,
			11,
			TextMuted,
			HorizontalAlignment.Left);
		_hint.ClipText = true;
		layout.AddChild(_hint);
		layout.AddChild(MakeAccentLine());

		_lineupHeader = new HBoxContainer();
		_lineupHeader.AddThemeConstantOverride("separation", 8);
		layout.AddChild(_lineupHeader);

		_lineupRows = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_lineupRows.AddThemeConstantOverride("separation", 0);
		layout.AddChild(_lineupRows);
		return card;
	}

	private Control BuildModeToggle()
	{
		var frame = new PanelContainer();
		frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.121569f, 0.137255f, 0.160784f, 1f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 2,
			ContentMarginTop = 2,
			ContentMarginRight = 2,
			ContentMarginBottom = 2,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerDetail = 4,
		});

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 0);
		frame.AddChild(row);

		_orderModeButton = MakeModeButton("BATTING ORDER", false);
		_positionModeButton = MakeModeButton("POSITIONS", true);
		row.AddChild(_orderModeButton);
		row.AddChild(_positionModeButton);
		return frame;
	}

	private Button MakeModeButton(string text, bool positionMode)
	{
		var button = new Button
		{
			Text = text,
			Flat = false,
			CustomMinimumSize = new Vector2(0, 24),
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		button.Pressed += () => SetPositionMode(positionMode);
		return button;
	}

	private void SetPositionMode(bool positionMode)
	{
		if (_positionMode == positionMode)
		{
			return;
		}

		_positionMode = positionMode;
		ClearSelection();
		RefreshAll();
	}

	private Control BuildBenchCard()
	{
		var card = MakeCard();
		card.SizeFlagsStretchRatio = 1f;
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		card.AddChild(layout);

		layout.AddChild(MakeText("BENCH", _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(MakeText("Available players and their season line.", _medium, 11, TextMuted, HorizontalAlignment.Left));
		layout.AddChild(BuildFilterRow());
		layout.AddChild(MakeAccentLine());
		layout.AddChild(BuildBenchHeader());

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);

		_benchRows = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_benchRows.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(_benchRows);
		return card;
	}

	private Control BuildFilterRow()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		row.AddChild(MakeFilter(FilterAll, "ALL"));
		row.AddChild(MakeFilter(FilterHitters, "HITTERS"));
		row.AddChild(MakeFilter(FilterPitchers, "PITCHERS"));
		return row;
	}

	private Button MakeFilter(string key, string label)
	{
		var button = new Button
		{
			Text = label,
			Flat = false,
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
		};
		button.Pressed += () => ApplyBenchFilter(key);
		_filterButtons[key] = button;
		return button;
	}

	private Control BuildDetailCard()
	{
		var card = MakeCard();
		card.SizeFlagsVertical = SizeFlags.ShrinkBegin;
		card.CustomMinimumSize = new Vector2(0, 108);
		_detailHost = new VBoxContainer();
		_detailHost.AddThemeConstantOverride("separation", 4);
		card.AddChild(_detailHost);
		return card;
	}

	private void FillLineupHeader()
	{
		ClearHost(_lineupHeader);
		_lineupHeader.AddChild(MakeCell("POS", 36, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		var name = MakeText("PLAYER", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_lineupHeader.AddChild(name);
		AddTableStatHeaders(_lineupHeader);
	}

	private Control BuildBenchHeader()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		var name = MakeText("PLAYER", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(name);
		AddTableStatHeaders(row);
		return row;
	}

	private void AddTableStatHeaders(HBoxContainer row)
	{
		row.AddChild(MakeCell("OVR", 32, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		row.AddChild(MakeCell("YR", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		row.AddChild(MakeCell("POS", 56, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		row.AddChild(MakeCell("AVG", 40, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		row.AddChild(MakeCell("HR", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
	}

	private void ApplyBenchFilter(string key)
	{
		_benchFilter = key;
		RefreshAll();
	}

	private void ClearSelection()
	{
		_selectedSlot = -1;
		_gloveSlot = -1;
		_selectedBench = 0;
	}

	private void RefreshAll()
	{
		RefreshHeaderMeta();
		RefreshFilters();
		RefreshModeToggle();
		FillLineupHeader();
		RebuildLineup();
		RebuildBench();
		BindDetail();
	}

	private void RefreshHeaderMeta()
	{
		LineupCard lineup = ClubhouseBoard.GetLineup(_session.ActiveTeam);
		_status.Text = BuildStatus(lineup);
		_status.AddThemeColorOverride("font_color", lineup.FilledCount == 9 ? Green : Accent);

		ClubhousePlayer? starter = ClubhouseBoard.GetLikelyStarter(_session.ActiveTeam);
		_starterLabel.Text = starter == null
			? "STARTER  ·  UNSET"
			: $"STARTER  ·  {starter.DisplayLast}  {FormatPitchLine(starter.Pitching)}";

		if (_gloveSlot >= 0)
		{
			_hint.Text = "Right-click another player to swap their positions.";
		}
		else if (_positionMode)
		{
			if (_selectedSlot >= 0)
			{
				_hint.Text = "Click another player to swap positions. Batting order stays put.";
			}
			else if (_selectedBench != 0)
			{
				_hint.Text = "Click a batting slot to insert this player at that spot.";
			}
			else
			{
				_hint.Text = "Click two players to swap positions. Right-click does the same. Order does not change.";
			}
		}
		else if (_selectedSlot >= 0)
		{
			_hint.Text = "Click another batting slot to swap order, or a bench player to insert.";
		}
		else if (_selectedBench != 0)
		{
			_hint.Text = "Click a batting slot to insert this player. Their glove moves with them.";
		}
		else
		{
			_hint.Text = "Click two slots to swap batting order. Right-click two players to swap positions.";
		}
	}

	private void RefreshModeToggle()
	{
		ApplyModeButton(_orderModeButton, !_positionMode);
		ApplyModeButton(_positionModeButton, _positionMode);
	}

	private void ApplyModeButton(Button button, bool active)
	{
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 11);
		button.AddThemeColorOverride("font_color", active ? TextOnAccent : TextPrimary);
		button.AddThemeColorOverride("font_hover_color", active ? TextOnAccent : Colors.White);
		button.AddThemeColorOverride("font_pressed_color", active ? TextOnAccent : TextPrimary);
		button.AddThemeStyleboxOverride("normal", MakePill(active ? Accent : Colors.Transparent));
		button.AddThemeStyleboxOverride("hover", MakePill(active ? AccentHover : HoverBg));
		button.AddThemeStyleboxOverride("pressed", MakePill(active ? AccentHover : HoverBg));
		button.AddThemeStyleboxOverride("focus", MakePill(active ? AccentHover : HoverBg));
	}

	private void RefreshFilters()
	{
		foreach (KeyValuePair<string, Button> pair in _filterButtons)
		{
			bool active = pair.Key == _benchFilter;
			Button button = pair.Value;
			button.AddThemeFontOverride("font", _semibold);
			button.AddThemeFontSizeOverride("font_size", 11);
			button.AddThemeColorOverride("font_color", active ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("font_hover_color", active ? TextOnAccent : Colors.White);
			button.AddThemeStyleboxOverride("normal", MakePill(active ? Accent : Colors.Transparent));
			button.AddThemeStyleboxOverride("hover", MakePill(active ? AccentHover : HoverBg));
			button.AddThemeStyleboxOverride("pressed", MakePill(active ? AccentHover : HoverBg));
			button.AddThemeStyleboxOverride("focus", MakePill(active ? AccentHover : HoverBg));
		}
	}

	private void RebuildLineup()
	{
		ClearHost(_lineupRows);
		LineupCard lineup = ClubhouseBoard.GetLineup(_session.ActiveTeam);
		for (int i = 0; i < lineup.Slots.Length; i++)
		{
			_lineupRows.AddChild(BuildLineupRow(i, lineup.Slots[i]));
		}
	}

	private Control BuildLineupRow(int slotIndex, LineupSlot slot)
	{
		bool orderSelected = slotIndex == _selectedSlot;
		bool gloveSelected = slotIndex == _gloveSlot;
		bool highlighted = orderSelected || gloveSelected;
		Color rowColor = gloveSelected
			? RowGlove
			: orderSelected ? RowSelected : (slotIndex % 2 == 0 ? RowEven : RowOdd);
		ClubhousePlayer? player = slot.IsEmpty ? null : ClubhouseSquad.Find(slot.Jersey);

		var row = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		row.AddThemeStyleboxOverride("panel", MakeLineupRowStyle(rowColor, highlighted));
		row.GuiInput += @event =>
		{
			if (@event is not InputEventMouseButton mouse || !mouse.Pressed)
			{
				return;
			}

			int index = slotIndex;
			if (mouse.ButtonIndex == MouseButton.Left)
			{
				Callable.From(() => OnLineupClicked(index)).CallDeferred();
			}
			else if (mouse.ButtonIndex == MouseButton.Right)
			{
				Callable.From(() => OnLineupGloveClicked(index)).CallDeferred();
			}
		};
		row.MouseEntered += () =>
		{
			if (!highlighted)
			{
				row.AddThemeStyleboxOverride("panel", MakeLineupRowStyle(HoverBg, false));
			}
		};
		row.MouseExited += () =>
		{
			if (!highlighted)
			{
				Color restored = slotIndex % 2 == 0 ? RowEven : RowOdd;
				row.AddThemeStyleboxOverride("panel", MakeLineupRowStyle(restored, false));
			}
		};

		var columns = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		columns.AddThemeConstantOverride("separation", 6);
		row.AddChild(columns);

		string playingPos = string.IsNullOrEmpty(slot.Position) ? "—" : slot.Position;
		Color playingColor = player != null && !ClubhouseSquad.CanPlayAt(player, slot.Position)
			? Urgent
			: Accent;
		columns.AddChild(MakeCell(playingPos, 36, _bold, 14, playingColor, HorizontalAlignment.Center));
		if (player == null)
		{
			var empty = MakeText("EMPTY  ·  pick from the bench", _medium, 12, TextMuted, HorizontalAlignment.Left);
			empty.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			columns.AddChild(empty);
			AddTableStats(columns, null);
		}
		else
		{
			columns.AddChild(BuildPortrait(player, 22));
			columns.AddChild(BuildNameCell(player, true));
			AddTableStats(columns, player);
		}

		return row;
	}

	private void RebuildBench()
	{
		ClearHost(_benchRows);
		List<ClubhousePlayer> bench = GetBenchPlayers();
		if (bench.Count == 0)
		{
			_benchRows.AddChild(MakeText("No players in this group.", _medium, 12, TextMuted, HorizontalAlignment.Center));
			return;
		}

		for (int i = 0; i < bench.Count; i++)
		{
			_benchRows.AddChild(BuildBenchRow(bench[i], i));
		}
	}

	private Control BuildBenchRow(ClubhousePlayer player, int index)
	{
		bool selected = player.Jersey == _selectedBench;
		Color rowColor = selected ? RowSelected : (index % 2 == 0 ? RowEven : RowOdd);

		var row = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		row.AddThemeStyleboxOverride("panel", MakeRowStyle(rowColor, selected));
		row.GuiInput += @event =>
		{
			if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				int jersey = player.Jersey;
				Callable.From(() => OnBenchClicked(jersey)).CallDeferred();
			}
		};
		row.MouseEntered += () =>
		{
			if (!selected)
			{
				row.AddThemeStyleboxOverride("panel", MakeRowStyle(HoverBg, false));
			}
		};
		row.MouseExited += () =>
		{
			if (!selected)
			{
				Color restored = index % 2 == 0 ? RowEven : RowOdd;
				row.AddThemeStyleboxOverride("panel", MakeRowStyle(restored, false));
			}
		};

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 6);
		row.AddChild(columns);

		columns.AddChild(BuildPortrait(player, 24));
		columns.AddChild(BuildNameCell(player, true));
		AddTableStats(columns, player);
		return row;
	}

	private void AddTableStats(HBoxContainer columns, ClubhousePlayer? player)
	{
		string eligible = player == null || string.IsNullOrEmpty(player.Position) ? "—" : player.Position;
		if (player == null)
		{
			columns.AddChild(MakeCell("—", 32, _medium, 12, TextMuted, HorizontalAlignment.Center));
			columns.AddChild(MakeCell("—", 28, _medium, 12, TextMuted, HorizontalAlignment.Center));
			columns.AddChild(MakeCell("—", 56, _medium, 12, TextMuted, HorizontalAlignment.Center));
			columns.AddChild(MakeCell("—", 40, _medium, 12, TextMuted, HorizontalAlignment.Center));
			columns.AddChild(MakeCell("—", 28, _medium, 12, TextMuted, HorizontalAlignment.Center));
			return;
		}

		columns.AddChild(MakeCell(FormatInt(player.Overall), 32, _bold, 13, OverallColor(player.Overall), HorizontalAlignment.Center));
		columns.AddChild(MakeCell(player.Year, 28, _semibold, 12, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeCell(eligible, 56, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeCell(player.Hitting == null ? "—" : FormatAvg(player.Hitting.Average), 40, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeCell(player.Hitting == null ? "—" : FormatInt(player.Hitting.HomeRuns), 28, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
	}

	private void OnLineupClicked(int slotIndex)
	{
		if (slotIndex < 0)
		{
			return;
		}

		LineupCard lineup = ClubhouseBoard.GetLineup(_session.ActiveTeam);
		if (_selectedSlot >= 0 && _selectedSlot != slotIndex)
		{
			if (_positionMode)
			{
				lineup.SwapDefense(_selectedSlot, slotIndex);
			}
			else
			{
				lineup.Swap(_selectedSlot, slotIndex);
			}

			ClearSelection();
			RefreshAll();
			return;
		}

		if (_selectedBench != 0)
		{
			InsertBenchIntoSlot(slotIndex, _selectedBench);
			return;
		}

		_selectedSlot = _selectedSlot == slotIndex ? -1 : slotIndex;
		_selectedBench = 0;
		RefreshAll();
	}

	private void OnLineupGloveClicked(int slotIndex)
	{
		if (slotIndex < 0)
		{
			return;
		}

		if (_gloveSlot >= 0 && _gloveSlot != slotIndex)
		{
			ClubhouseBoard.GetLineup(_session.ActiveTeam).SwapDefense(_gloveSlot, slotIndex);
			_gloveSlot = -1;
			RefreshAll();
			return;
		}

		_gloveSlot = _gloveSlot == slotIndex ? -1 : slotIndex;
		RefreshAll();
	}

	private void OnBenchClicked(int jersey)
	{
		if (_selectedSlot >= 0)
		{
			InsertBenchIntoSlot(_selectedSlot, jersey);
			return;
		}

		_selectedBench = _selectedBench == jersey ? 0 : jersey;
		_selectedSlot = -1;
		RefreshAll();
	}

	private void InsertBenchIntoSlot(int slotIndex, int jersey)
	{
		ClubhouseBoard.GetLineup(_session.ActiveTeam).Insert(slotIndex, jersey);
		ClearSelection();
		_selectedSlot = slotIndex;
		RefreshAll();
	}

	private void SendSelectedToBench()
	{
		if (_selectedSlot < 0)
		{
			return;
		}

		ClubhouseBoard.GetLineup(_session.ActiveTeam).Bench(_selectedSlot);
		ClearSelection();
		RefreshAll();
	}

	private void BindDetail()
	{
		ClearHost(_detailHost);
		ClubhousePlayer? player = GetSelectedPlayer();
		if (player == null)
		{
			_detailHost.AddChild(MakeText("PLAYER CARD", _semibold, 11, Accent, HorizontalAlignment.Left));
			_detailHost.AddChild(MakeText(
				"Select a batting slot or bench player to see ratings and the season summary.",
				_medium,
				12,
				TextMuted,
				HorizontalAlignment.Left));
			return;
		}

		var top = new HBoxContainer();
		top.AddThemeConstantOverride("separation", 12);
		_detailHost.AddChild(top);

		top.AddChild(BuildPortrait(player, 48));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		identity.AddThemeConstantOverride("separation", 0);
		identity.AddChild(MakeText(player.DisplayLast, _bold, 20, TextPrimary, HorizontalAlignment.Left));
		identity.AddChild(MakeText(
			$"{player.FirstName}  ·  #{FormatInt(player.Jersey)}  ·  {player.Position}  ·  {player.Year}  ·  {player.BatsThrows}",
			_medium,
			12,
			TextMuted,
			HorizontalAlignment.Left));
		identity.AddChild(MakeText(
			player.Status.ToUpperInvariant(),
			_semibold,
			11,
			player.IsHealthy ? Green : Accent,
			HorizontalAlignment.Left));
		top.AddChild(identity);

		top.AddChild(BuildRatingChip("OVR", player.Overall));
		top.AddChild(BuildRatingChip("CON", player.Contact));
		top.AddChild(BuildRatingChip("POW", player.Power));
		top.AddChild(BuildRatingChip("SPD", player.Speed));
		top.AddChild(BuildRatingChip("ARM", player.Arm));
		top.AddChild(BuildRatingChip("FLD", player.Field));

		if (_selectedSlot >= 0 && !ClubhouseBoard.GetLineup(_session.ActiveTeam).Slots[_selectedSlot].IsEmpty)
		{
			Button bench = MakeAction("MOVE TO BENCH", false);
			bench.Pressed += SendSelectedToBench;
			top.AddChild(bench);
		}

		_detailHost.AddChild(MakeHairline());
		_detailHost.AddChild(BuildSummaryLine(player));
	}

	private Control BuildSummaryLine(ClubhousePlayer player)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 18);

		if (player.Hitting != null)
		{
			HittingLine hit = player.Hitting;
			row.AddChild(MakeStat("AVG", FormatAvg(hit.Average)));
			row.AddChild(MakeStat("HR", FormatInt(hit.HomeRuns)));
			row.AddChild(MakeStat("RBI", FormatInt(hit.Rbi)));
			row.AddChild(MakeStat("OPS", FormatOps(hit.Ops)));
			row.AddChild(MakeStat("SB", FormatInt(hit.StolenBases)));
			row.AddChild(MakeStat("BB", FormatInt(hit.Walks)));
			row.AddChild(MakeStat("SO", FormatInt(hit.Strikeouts)));
			row.AddChild(MakeStat("G", FormatInt(hit.Games)));
		}

		if (player.Pitching != null)
		{
			PitchingLine pit = player.Pitching;
			row.AddChild(MakeStat("ERA", FormatEra(pit.Era)));
			row.AddChild(MakeStat("W-L", $"{FormatInt(pit.Wins)}-{FormatInt(pit.Losses)}"));
			row.AddChild(MakeStat("IP", FormatIp(pit.Innings)));
			row.AddChild(MakeStat("SO", FormatInt(pit.Strikeouts)));
			row.AddChild(MakeStat("WHIP", FormatEra(pit.Whip)));
			row.AddChild(MakeStat("GS", FormatInt(pit.GamesStarted)));
		}

		if (player.Hitting == null && player.Pitching == null)
		{
			row.AddChild(MakeText("No season line yet.", _medium, 12, TextMuted, HorizontalAlignment.Left));
		}

		return row;
	}

	private Control MakeStat(string label, string value)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", -2);
		box.AddChild(MakeText(value, _bold, 16, TextPrimary, HorizontalAlignment.Left));
		box.AddChild(MakeText(label, _medium, 10, TextMuted, HorizontalAlignment.Left));
		return box;
	}

	private Control BuildRatingChip(string label, int value)
	{
		var chip = new PanelContainer
		{
			CustomMinimumSize = new Vector2(48, 0),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		chip.AddThemeStyleboxOverride("panel", MakeInnerCard(6, 4));

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 0);
		chip.AddChild(box);
		box.AddChild(MakeText(label, _semibold, 8, label == "OVR" ? Accent : TextMuted, HorizontalAlignment.Center));
		box.AddChild(MakeText(FormatInt(value), _bold, 15, OverallColor(value), HorizontalAlignment.Center));

		var bar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0, 3),
			MaxValue = 99,
			Value = value,
			ShowPercentage = false,
		};
		bar.AddThemeStyleboxOverride("background", MakeFlat(BarBg, 2));
		bar.AddThemeStyleboxOverride("fill", MakeFlat(OverallColor(value), 2));
		box.AddChild(bar);
		return chip;
	}

	private ClubhousePlayer? GetSelectedPlayer()
	{
		if (_selectedSlot >= 0)
		{
			LineupSlot slot = ClubhouseBoard.GetLineup(_session.ActiveTeam).Slots[_selectedSlot];
			return slot.IsEmpty ? null : ClubhouseSquad.Find(slot.Jersey);
		}

		return _selectedBench == 0 ? null : ClubhouseSquad.Find(_selectedBench);
	}

	private List<ClubhousePlayer> GetBenchPlayers()
	{
		LineupCard lineup = ClubhouseBoard.GetLineup(_session.ActiveTeam);
		var bench = new List<ClubhousePlayer>();
		foreach (ClubhousePlayer player in ClubhouseSquad.ForTeam(_session.ActiveTeam.Level))
		{
			if (lineup.Contains(player.Jersey))
			{
				continue;
			}

			if (_benchFilter == FilterPitchers && !player.IsPitcher)
			{
				continue;
			}

			if (_benchFilter == FilterHitters && player.IsPitcher)
			{
				continue;
			}

			bench.Add(player);
		}

		bench.Sort((a, b) => b.Overall.CompareTo(a.Overall));
		return bench;
	}

	private static string BuildStatus(LineupCard lineup)
	{
		var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var duplicates = new List<string>();
		bool hasDh = false;
		bool hasPitcher = false;
		bool injured = false;

		foreach (LineupSlot slot in lineup.Slots)
		{
			if (slot.IsEmpty)
			{
				continue;
			}

			ClubhousePlayer? player = ClubhouseSquad.Find(slot.Jersey);
			if (player != null && !player.IsHealthy)
			{
				injured = true;
			}

			if (slot.Position == "DH")
			{
				hasDh = true;
			}

			if (slot.Position == "P")
			{
				hasPitcher = true;
			}

			if (!string.IsNullOrEmpty(slot.Position) && slot.Position != "DH" && !used.Add(slot.Position))
			{
				duplicates.Add(slot.Position);
			}
		}

		string head = $"{FormatInt(lineup.FilledCount)}/9 SET";
		if (duplicates.Count > 0)
		{
			return $"{head}  ·  DOUBLE {duplicates[0]}";
		}

		if (hasDh && hasPitcher)
		{
			return $"{head}  ·  DH AND P BOTH BATTING";
		}

		if (injured)
		{
			return $"{head}  ·  {(hasDh ? "DH LINEUP" : "PITCHER HITS")}  ·  INJURED IN ORDER";
		}

		return $"{head}  ·  {(hasDh ? "DH LINEUP" : hasPitcher ? "PITCHER HITS" : "SET POSITIONS")}";
	}

	private Control BuildNameCell(ClubhousePlayer player, bool showJersey)
	{
		var box = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		box.AddThemeConstantOverride("separation", -2);
		box.AddChild(MakeText(player.DisplayLast, _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		string sub = showJersey
			? $"{player.FirstName}  ·  #{FormatInt(player.Jersey)}"
			: player.FirstName;
		if (!player.IsHealthy)
		{
			sub = $"{sub}  ·  {player.Status.ToUpperInvariant()}";
		}

		box.AddChild(MakeText(sub, _medium, 10, player.IsHealthy ? TextMuted : Accent, HorizontalAlignment.Left));
		return box;
	}

	private Control BuildPortrait(ClubhousePlayer player, float size)
	{
		var wrap = new Control
		{
			CustomMinimumSize = new Vector2(size, size),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		wrap.SizeFlagsVertical = SizeFlags.ShrinkCenter;

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
		var fallback = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
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
		var initial = MakeText(player.LastName[..1].ToUpperInvariant(), _bold, Mathf.RoundToInt(size * 0.38f), Accent, HorizontalAlignment.Center);
		initial.VerticalAlignment = VerticalAlignment.Center;
		fallback.AddChild(initial);
		wrap.AddChild(fallback);
		return wrap;
	}

	private Button MakeAction(string text, bool primary)
	{
		var button = new Button
		{
			Text = text,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(0, 32),
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		if (primary)
		{
			button.AddThemeColorOverride("font_color", TextOnAccent);
			button.AddThemeStyleboxOverride("normal", MakeFilledButton(Accent));
			button.AddThemeStyleboxOverride("hover", MakeFilledButton(AccentHover));
			button.AddThemeStyleboxOverride("pressed", MakeFilledButton(AccentHover));
		}
		else
		{
			button.AddThemeColorOverride("font_color", TextPrimary);
			button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, CardBorder));
			button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
			button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
		}

		return button;
	}

	private PanelContainer MakeCard()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle());
		return card;
	}

	private static void ClearHost(Node host)
	{
		while (host.GetChildCount() > 0)
		{
			Node child = host.GetChild(0);
			host.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static Label MakeCell(string text, float width, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		Label label = MakeText(text, font, size, color, align);
		label.CustomMinimumSize = new Vector2(width, 0);
		label.VerticalAlignment = VerticalAlignment.Center;
		label.ClipText = true;
		label.MouseFilter = MouseFilterEnum.Ignore;
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

	private static ColorRect MakeAccentLine()
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 2),
			Color = Accent,
			MouseFilter = MouseFilterEnum.Ignore,
		};
	}

	private static ColorRect MakeHairline()
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 1),
			Color = Hairline,
			MouseFilter = MouseFilterEnum.Ignore,
		};
	}

	private static string FormatPitchLine(PitchingLine? pitching) =>
		pitching == null ? string.Empty : $"·  {FormatEra(pitching.Era)} ERA";

	private static string FormatInt(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static string FormatAvg(float value) => value.ToString(".000", CultureInfo.InvariantCulture);

	private static string FormatOps(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);

	private static string FormatEra(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

	private static string FormatIp(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

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

	private static StyleBoxFlat MakePill(Color bg)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			DrawCenter = true,
			ContentMarginLeft = 10,
			ContentMarginTop = 4,
			ContentMarginRight = 10,
			ContentMarginBottom = 4,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerDetail = 5,
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
			ContentMarginLeft = 8,
			ContentMarginTop = 6,
			ContentMarginRight = 8,
			ContentMarginBottom = 6,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		};
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
			CornerDetail = 3,
		};
	}

	private static StyleBoxFlat MakeLineupRowStyle(Color bg, bool selected)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = selected ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = selected ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 8,
			ContentMarginTop = 2,
			ContentMarginRight = 8,
			ContentMarginBottom = 2,
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
			ContentMarginLeft = 8,
			ContentMarginTop = 5,
			ContentMarginRight = 8,
			ContentMarginBottom = 5,
		};
	}

	private static StyleBoxFlat MakeFilledButton(Color bg)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
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
	}

	private static StyleBoxFlat MakeOutlinedButton(Color bg, Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 8,
			ContentMarginTop = 4,
			ContentMarginRight = 8,
			ContentMarginBottom = 4,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
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
}
