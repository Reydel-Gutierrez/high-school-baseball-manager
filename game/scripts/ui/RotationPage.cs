using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// Clubhouse pitching staff: weekly starting rotation, named bullpen
/// seats, and the remaining arms. Same click-to-assign pattern as lineup.
/// </summary>
public partial class RotationPage : Control
{
	private const int CurrentSeasonYear = 2026;
	private const string AvailableKey = "available";

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color RowSelected = new(0.956863f, 0.643137f, 0.109804f, 0.14f);
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
	private VBoxContainer _starterRows = null!;
	private VBoxContainer _bullpenRows = null!;
	private VBoxContainer _availableRows = null!;
	private VBoxContainer _detailHost = null!;

	private string _selectedSeat = string.Empty;
	private int _selectedArm = 0;

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
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		AddChild(margin);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		margin.AddChild(layout);
		layout.AddChild(BuildHeader());

		var body = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 8);
		layout.AddChild(body);

		body.AddChild(BuildSeatCard("STARTING ROTATION", "Click a seat, then an available arm. Click two seats to swap.", out _starterRows, 1.15f));
		body.AddChild(BuildSeatCard("BULLPEN", "Closer and support roles for the week.", out _bullpenRows, 1.05f));
		body.AddChild(BuildSeatCard("AVAILABLE ARMS", "Pitchers not in the rotation or bullpen.", out _availableRows, 1f));
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
		identity.AddChild(MakeText("PITCHING ROTATION", _bold, 26, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("CASCADE REGIONAL HIGH SCHOOL", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var side = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		side.AddThemeConstantOverride("separation", 2);
		_status = MakeText(string.Empty, _semibold, 12, TextPrimary, HorizontalAlignment.Right);
		side.AddChild(_status);
		_hint = MakeText("Select a seat or an available pitcher.", _medium, 11, TextMuted, HorizontalAlignment.Right);
		side.AddChild(_hint);
		header.AddChild(side);
		return header;
	}

	private Control BuildSeatCard(string title, string hint, out VBoxContainer rows, float stretch)
	{
		var card = MakeCard();
		card.SizeFlagsStretchRatio = stretch;
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		card.AddChild(layout);

		layout.AddChild(MakeText(title, _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		var hintLabel = MakeText(hint, _medium, 11, TextMuted, HorizontalAlignment.Left);
		hintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(hintLabel);
		layout.AddChild(MakeAccentLine());
		layout.AddChild(BuildPitchHeader());

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);

		rows = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		rows.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(rows);
		return card;
	}

	private Control BuildPitchHeader()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		row.AddChild(MakeCell("ROLE", 52, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		var name = MakeText("PITCHER", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(name);
		row.AddChild(MakeCell("OVR", 32, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		row.AddChild(MakeCell("ERA", 40, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		row.AddChild(MakeCell("W-L", 36, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		row.AddChild(MakeCell("K", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		return row;
	}

	private Control BuildDetailCard()
	{
		var card = MakeCard();
		card.SizeFlagsVertical = SizeFlags.ShrinkBegin;
		card.CustomMinimumSize = new Vector2(0, 132);
		_detailHost = new VBoxContainer();
		_detailHost.AddThemeConstantOverride("separation", 6);
		card.AddChild(_detailHost);
		return card;
	}

	private void ClearSelection()
	{
		_selectedSeat = string.Empty;
		_selectedArm = 0;
	}

	private void RefreshAll()
	{
		RotationCard rotation = ClubhouseBoard.GetRotation(_session.ActiveTeam);
		int starters = 0;
		foreach (int jersey in rotation.Starters)
		{
			if (jersey != 0)
			{
				starters++;
			}
		}

		_status.Text = $"{FormatInt(starters)}/5 STARTERS  ·  {FormatInt(rotation.AssignedCount)} ARMS SET";
		_status.AddThemeColorOverride("font_color", starters >= 3 ? Green : Accent);

		if (!string.IsNullOrEmpty(_selectedSeat) && _selectedSeat != AvailableKey)
		{
			_hint.Text = "Pick an available arm to assign, or another seat to swap.";
		}
		else if (_selectedArm != 0)
		{
			_hint.Text = "Pick a rotation or bullpen seat for this pitcher.";
		}
		else
		{
			_hint.Text = "Select a seat or an available pitcher.";
		}

		RebuildSeats(_starterRows, RotationSeat.StarterSeats);
		RebuildSeats(_bullpenRows, RotationSeat.BullpenSeats);
		RebuildAvailable();
		BindDetail();
	}

	private void RebuildSeats(VBoxContainer host, RotationSeat[] seats)
	{
		ClearHost(host);
		RotationCard rotation = ClubhouseBoard.GetRotation(_session.ActiveTeam);
		for (int i = 0; i < seats.Length; i++)
		{
			host.AddChild(BuildSeatRow(seats[i], rotation.GetSeat(seats[i].Key), i));
		}
	}

	private Control BuildSeatRow(RotationSeat seat, int jersey, int index)
	{
		bool selected = _selectedSeat == seat.Key;
		Color rowColor = selected ? RowSelected : (index % 2 == 0 ? RowEven : RowOdd);
		ClubhousePlayer? player = jersey == 0 ? null : ClubhouseSquad.Find(jersey);

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
				string key = seat.Key;
				Callable.From(() => OnSeatClicked(key)).CallDeferred();
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
		columns.AddThemeConstantOverride("separation", 8);
		row.AddChild(columns);
		columns.AddChild(MakeCell(seat.Label, 52, _bold, 12, Accent, HorizontalAlignment.Left));

		if (player == null)
		{
			var empty = MakeText("OPEN", _medium, 12, TextMuted, HorizontalAlignment.Left);
			empty.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			columns.AddChild(empty);
			columns.AddChild(MakeCell("—", 32, _medium, 12, TextMuted, HorizontalAlignment.Center));
			columns.AddChild(MakeCell("—", 40, _medium, 12, TextMuted, HorizontalAlignment.Center));
			columns.AddChild(MakeCell("—", 36, _medium, 12, TextMuted, HorizontalAlignment.Center));
			columns.AddChild(MakeCell("—", 28, _medium, 12, TextMuted, HorizontalAlignment.Center));
		}
		else
		{
			columns.AddChild(BuildPortrait(player, 24));
			columns.AddChild(BuildNameCell(player));
			columns.AddChild(MakeCell(FormatInt(player.Overall), 32, _bold, 13, OverallColor(player.Overall), HorizontalAlignment.Center));
			PitchingLine? pit = player.Pitching;
			columns.AddChild(MakeCell(pit == null ? "—" : FormatEra(pit.Era), 40, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
			columns.AddChild(MakeCell(pit == null ? "—" : $"{FormatInt(pit.Wins)}-{FormatInt(pit.Losses)}", 36, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
			columns.AddChild(MakeCell(pit == null ? "—" : FormatInt(pit.Strikeouts), 28, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
		}

		return row;
	}

	private void RebuildAvailable()
	{
		ClearHost(_availableRows);
		List<ClubhousePlayer> arms = GetAvailableArms();
		if (arms.Count == 0)
		{
			_availableRows.AddChild(MakeText("All pitchers are assigned.", _medium, 12, TextMuted, HorizontalAlignment.Center));
			return;
		}

		for (int i = 0; i < arms.Count; i++)
		{
			_availableRows.AddChild(BuildAvailableRow(arms[i], i));
		}
	}

	private Control BuildAvailableRow(ClubhousePlayer player, int index)
	{
		bool selected = player.Jersey == _selectedArm;
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
				Callable.From(() => OnArmClicked(jersey)).CallDeferred();
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
		columns.AddThemeConstantOverride("separation", 8);
		row.AddChild(columns);
		columns.AddChild(MakeCell(player.Position, 52, _semibold, 11, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(BuildPortrait(player, 24));
		columns.AddChild(BuildNameCell(player));
		columns.AddChild(MakeCell(FormatInt(player.Overall), 32, _bold, 13, OverallColor(player.Overall), HorizontalAlignment.Center));
		PitchingLine? pit = player.Pitching;
		columns.AddChild(MakeCell(pit == null ? "—" : FormatEra(pit.Era), 40, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeCell(pit == null ? "—" : $"{FormatInt(pit.Wins)}-{FormatInt(pit.Losses)}", 36, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeCell(pit == null ? "—" : FormatInt(pit.Strikeouts), 28, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
		return row;
	}

	private void OnSeatClicked(string key)
	{
		if (!string.IsNullOrEmpty(_selectedSeat) && _selectedSeat != AvailableKey && _selectedSeat != key)
		{
			ClubhouseBoard.GetRotation(_session.ActiveTeam).SwapSeats(_selectedSeat, key);
			ClearSelection();
			RefreshAll();
			return;
		}

		if (_selectedArm != 0)
		{
			AssignArmToSeat(key, _selectedArm);
			return;
		}

		_selectedSeat = _selectedSeat == key ? string.Empty : key;
		_selectedArm = 0;
		RefreshAll();
	}

	private void OnArmClicked(int jersey)
	{
		if (!string.IsNullOrEmpty(_selectedSeat) && _selectedSeat != AvailableKey)
		{
			AssignArmToSeat(_selectedSeat, jersey);
			return;
		}

		_selectedArm = _selectedArm == jersey ? 0 : jersey;
		_selectedSeat = string.Empty;
		RefreshAll();
	}

	private void AssignArmToSeat(string seat, int jersey)
	{
		RotationCard rotation = ClubhouseBoard.GetRotation(_session.ActiveTeam);
		int displaced = rotation.GetSeat(seat);
		if (displaced == jersey)
		{
			ClearSelection();
			RefreshAll();
			return;
		}

		ClearJersey(rotation, jersey);
		rotation.SetSeat(seat, jersey);
		_selectedSeat = seat;
		_selectedArm = 0;
		RefreshAll();
	}

	private void SendSelectedToAvailable()
	{
		if (string.IsNullOrEmpty(_selectedSeat) || _selectedSeat == AvailableKey)
		{
			return;
		}

		ClubhouseBoard.GetRotation(_session.ActiveTeam).SetSeat(_selectedSeat, 0);
		ClearSelection();
		RefreshAll();
	}

	private static void ClearJersey(RotationCard rotation, int jersey)
	{
		if (jersey == 0)
		{
			return;
		}

		for (int i = 0; i < rotation.Starters.Length; i++)
		{
			if (rotation.Starters[i] == jersey)
			{
				rotation.Starters[i] = 0;
			}
		}

		if (rotation.Closer == jersey) rotation.Closer = 0;
		if (rotation.Setup == jersey) rotation.Setup = 0;
		if (rotation.Middle == jersey) rotation.Middle = 0;
		if (rotation.LongRelief == jersey) rotation.LongRelief = 0;
	}

	private void BindDetail()
	{
		ClearHost(_detailHost);
		ClubhousePlayer? player = GetSelectedPlayer();
		if (player == null)
		{
			_detailHost.AddChild(MakeText("PITCHER CARD", _semibold, 11, Accent, HorizontalAlignment.Left));
			_detailHost.AddChild(MakeText(
				"Select a rotation seat or an available arm to see the season pitching line.",
				_medium,
				12,
				TextMuted,
				HorizontalAlignment.Left));
			return;
		}

		var top = new HBoxContainer();
		top.AddThemeConstantOverride("separation", 12);
		_detailHost.AddChild(top);
		top.AddChild(BuildPortrait(player, 56));

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
		top.AddChild(BuildRatingChip("ARM", player.Arm));
		top.AddChild(BuildRatingChip("SPD", player.Speed));
		top.AddChild(BuildRatingChip("FLD", player.Field));

		if (!string.IsNullOrEmpty(_selectedSeat) && _selectedSeat != AvailableKey
			&& ClubhouseBoard.GetRotation(_session.ActiveTeam).GetSeat(_selectedSeat) != 0)
		{
			Button remove = MakeAction("MOVE TO AVAILABLE");
			remove.Pressed += SendSelectedToAvailable;
			top.AddChild(remove);
		}

		_detailHost.AddChild(MakeHairline());
		_detailHost.AddChild(BuildSummaryLine(player));
	}

	private Control BuildSummaryLine(ClubhousePlayer player)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 18);
		if (player.Pitching == null)
		{
			row.AddChild(MakeText("No pitching line yet.", _medium, 12, TextMuted, HorizontalAlignment.Left));
			return row;
		}

		PitchingLine pit = player.Pitching;
		row.AddChild(MakeStat("ERA", FormatEra(pit.Era)));
		row.AddChild(MakeStat("W-L", $"{FormatInt(pit.Wins)}-{FormatInt(pit.Losses)}"));
		row.AddChild(MakeStat("WHIP", FormatEra(pit.Whip)));
		row.AddChild(MakeStat("IP", FormatIp(pit.Innings)));
		row.AddChild(MakeStat("SO", FormatInt(pit.Strikeouts)));
		row.AddChild(MakeStat("BB", FormatInt(pit.Walks)));
		row.AddChild(MakeStat("GS", FormatInt(pit.GamesStarted)));
		row.AddChild(MakeStat("SV", FormatInt(pit.Saves)));
		row.AddChild(MakeStat("G", FormatInt(pit.Games)));
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
		if (!string.IsNullOrEmpty(_selectedSeat) && _selectedSeat != AvailableKey)
		{
			int jersey = ClubhouseBoard.GetRotation(_session.ActiveTeam).GetSeat(_selectedSeat);
			return jersey == 0 ? null : ClubhouseSquad.Find(jersey);
		}

		return _selectedArm == 0 ? null : ClubhouseSquad.Find(_selectedArm);
	}

	private List<ClubhousePlayer> GetAvailableArms()
	{
		RotationCard rotation = ClubhouseBoard.GetRotation(_session.ActiveTeam);
		var arms = new List<ClubhousePlayer>();
		foreach (ClubhousePlayer player in ClubhouseSquad.ForTeam(_session.ActiveTeam.Level))
		{
			if (!player.IsPitcher || rotation.Contains(player.Jersey))
			{
				continue;
			}

			arms.Add(player);
		}

		arms.Sort((a, b) => b.Overall.CompareTo(a.Overall));
		return arms;
	}

	private Control BuildNameCell(ClubhousePlayer player)
	{
		var box = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		box.AddThemeConstantOverride("separation", -2);
		box.AddChild(MakeText(player.DisplayLast, _semibold, 13, TextPrimary, HorizontalAlignment.Left));
		box.AddChild(MakeText($"{player.FirstName}  ·  {player.Year}", _medium, 10, TextMuted, HorizontalAlignment.Left));
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
		var initial = MakeText(player.LastName[..1].ToUpperInvariant(), _bold, Mathf.RoundToInt(size * 0.38f), Accent, HorizontalAlignment.Center);
		initial.VerticalAlignment = VerticalAlignment.Center;
		fallback.AddChild(initial);
		wrap.AddChild(fallback);
		return wrap;
	}

	private Button MakeAction(string text)
	{
		var button = new Button
		{
			Text = text,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(0, 32),
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, CardBorder));
		button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
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

	private static ColorRect MakeAccentLine() => new()
	{
		CustomMinimumSize = new Vector2(0, 2),
		Color = Accent,
		MouseFilter = MouseFilterEnum.Ignore,
	};

	private static ColorRect MakeHairline() => new()
	{
		CustomMinimumSize = new Vector2(0, 1),
		Color = Hairline,
		MouseFilter = MouseFilterEnum.Ignore,
	};

	private static string FormatInt(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static string FormatEra(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

	private static string FormatIp(float value) => value.ToString("0.0", CultureInfo.InvariantCulture);

	private static Color OverallColor(int value)
	{
		if (value >= 85) return Green;
		if (value >= 78) return Accent;
		if (value >= 70) return TextPrimary;
		return TextMuted;
	}

	private static StyleBoxFlat MakeCardStyle() => new()
	{
		BgColor = new Color(0.070588f, 0.078431f, 0.094118f, 0.94f),
		BorderColor = CardBorder,
		BorderWidthLeft = 1,
		BorderWidthTop = 1,
		BorderWidthRight = 1,
		BorderWidthBottom = 1,
		ContentMarginLeft = 10,
		ContentMarginTop = 8,
		ContentMarginRight = 10,
		ContentMarginBottom = 8,
		CornerRadiusTopLeft = 3,
		CornerRadiusTopRight = 3,
		CornerRadiusBottomRight = 3,
		CornerRadiusBottomLeft = 3,
		CornerDetail = 3,
	};

	private static StyleBoxFlat MakeInnerCard(float padX, float padY) => new()
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

	private static StyleBoxFlat MakeRowStyle(Color bg, bool selected) => new()
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

	private static StyleBoxFlat MakeOutlinedButton(Color bg, Color border) => new()
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

	private static StyleBoxFlat MakeFlat(Color bg, int radius) => new()
	{
		BgColor = bg,
		CornerRadiusTopLeft = radius,
		CornerRadiusTopRight = radius,
		CornerRadiusBottomRight = radius,
		CornerRadiusBottomLeft = radius,
		CornerDetail = 4,
	};
}
