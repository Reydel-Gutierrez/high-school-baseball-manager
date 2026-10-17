using System;
using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// District Open Pool: unsigned and released players any program can draft
/// during the season if they have roster room.
/// </summary>
public partial class ScoutingPoolPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color RowSelected = new(0.956863f, 0.643137f, 0.109804f, 0.14f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _cascadeLogo = null!;

	private Label _subtitle = null!;
	private Label _countValue = null!;
	private Label _roomValue = null!;
	private VBoxContainer _rowsHost = null!;
	private VBoxContainer _dossierHost = null!;
	private Guid _selectedId;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_cascadeLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		_session.RosterBoardChanged += Refresh;
		VisibilityChanged += OnVisibilityChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
			_session.RosterBoardChanged -= Refresh;
		}

		VisibilityChanged -= OnVisibilityChanged;
	}

	private void OnVisibilityChanged()
	{
		if (IsVisibleInTree())
		{
			Refresh();
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => Refresh();

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

		var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 6);
		layout.AddChild(body);
		body.AddChild(BuildListCard());
		body.AddChild(BuildDossierCard());
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
		identity.AddThemeConstantOverride("separation", -4);
		identity.AddChild(MakeText($"SCOUTING  ·  {GameSession.SeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("OPEN POOL", _bold, 22, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("UNSIGNED AND RELEASED PLAYERS ANY PROGRAM CAN DRAFT", _medium, 11, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var meta = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
		meta.AddThemeConstantOverride("separation", 18);
		meta.AddChild(BuildStat("AVAILABLE", "0", out _countValue));
		meta.AddChild(BuildStat("ROSTER ROOM", "0", out _roomValue));
		header.AddChild(meta);
		return header;
	}

	private Control BuildStat(string label, string value, out Label valueLabel)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", -4);
		col.AddChild(MakeText(label, _medium, 9, TextMuted, HorizontalAlignment.Right));
		valueLabel = MakeText(value, _bold, 16, TextPrimary, HorizontalAlignment.Right);
		col.AddChild(valueLabel);
		return col;
	}

	private Control BuildListCard()
	{
		var card = MakeCard();
		card.SizeFlagsStretchRatio = 1.55f;
		card.ClipContents = true;
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);
		layout.AddChild(BuildColumnHeader());
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
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
			ContentMarginLeft = 12,
			ContentMarginTop = 7,
			ContentMarginRight = 12,
			ContentMarginBottom = 7,
		});
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		var player = MakeText("PLAYER", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		player.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(player);
		columns.AddChild(MakeFixed("POS", 36, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("YEAR", 36, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("OVR", 56, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("POT", 56, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("SOURCE", 80, TextMuted, _semibold, 10));
		header.AddChild(columns);
		return header;
	}

	private Control BuildDossierCard()
	{
		var card = MakeCard();
		card.SizeFlagsStretchRatio = 1.05f;
		_dossierHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_dossierHost.AddThemeConstantOverride("separation", 8);
		card.AddChild(_dossierHost);
		return card;
	}

	private void Refresh()
	{
		if (_rowsHost == null)
		{
			return;
		}

		int room = Math.Max(0, ProgramRosterRules.RegisteredCap(_session.CurrentDate, GameSession.SeasonYear) - _session.RosterBoard.RegisteredCount);
		_countValue.Text = $"{_session.OpenPool.Count}";
		_roomValue.Text = $"{room}";
		_roomValue.AddThemeColorOverride("font_color", room == 0 ? Urgent : Green);
		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		_subtitle.Text = varsity
			? "UNSIGNED AND RELEASED PLAYERS ANY PROGRAM CAN DRAFT"
			: "JV  ·  UNSIGNED AND RELEASED PLAYERS ANY PROGRAM CAN DRAFT";
		RebuildRows();
		RebuildDossier();
	}

	private void RebuildRows()
	{
		foreach (Node child in _rowsHost.GetChildren())
		{
			child.QueueFree();
		}

		var players = new List<OpenPoolPlayer>(_session.OpenPool.Players);
		players.Sort((a, b) =>
		{
			int overall = b.TrueOverall.CompareTo(a.TrueOverall);
			return overall != 0 ? overall : string.Compare(a.LastName, b.LastName, StringComparison.Ordinal);
		});

		if (_selectedId == Guid.Empty && players.Count > 0)
		{
			_selectedId = players[0].Id;
		}

		if (players.Count == 0)
		{
			_rowsHost.AddChild(PadBody(
				MakeText("No players in the Open Pool.", _medium, 13, TextMuted, HorizontalAlignment.Center),
				expand: false));
			return;
		}

		if (FindPlayer(_selectedId) == null)
		{
			_selectedId = players[0].Id;
		}

		for (int i = 0; i < players.Count; i++)
		{
			_rowsHost.AddChild(BuildRow(players[i], i % 2 == 0));
		}
	}

	private Control BuildRow(OpenPoolPlayer player, bool even)
	{
		bool selected = player.Id == _selectedId;
		var row = new PanelContainer { MouseDefaultCursorShape = CursorShape.PointingHand };
		row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = selected ? RowSelected : even ? RowEven : RowOdd,
			BorderColor = selected ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		});
		Guid id = player.Id;
		row.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				_selectedId = id;
				Refresh();
			}
		};

		string ovr = TryoutScouting.FormatOverall(player.TrueOverall, 3, player.Id.GetHashCode());
		string pot = TryoutScouting.FormatPotential(player.TrueOverall, player.TruePotential, 3, player.Id.GetHashCode());
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		var name = MakeText(player.FullName, _semibold, 14, TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(name);
		columns.AddChild(MakeFixed(player.PositionLabel, 36, TextMuted));
		columns.AddChild(MakeFixed(player.Year.ToAbbrev(), 36, TextMuted));
		columns.AddChild(MakeFixed(ovr, 56, Accent, _bold));
		columns.AddChild(MakeFixed(pot, 56, pot == "?" ? TextMuted : Accent, _bold));
		columns.AddChild(MakeFixed(SourceLabel(player.Source), 80, SourceColor(player.Source), _semibold));
		row.AddChild(columns);
		return row;
	}

	private void RebuildDossier()
	{
		foreach (Node child in _dossierHost.GetChildren())
		{
			child.QueueFree();
		}

		OpenPoolPlayer? player = FindPlayer(_selectedId);
		if (player == null)
		{
			_dossierHost.AddChild(MakeText("Select a player.", _medium, 13, TextMuted, HorizontalAlignment.Left));
			return;
		}

		var profile = new PlayerProfileCard
		{
			FrameVisible = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_dossierHost.AddChild(profile);
		profile.Bind(PlayerProfiles.FromPool(player, _session.Tryouts));

		bool canDraft = _session.RosterBoard.CanAdd(_session.CurrentDate, GameSession.SeasonYear);
		_dossierHost.AddChild(MakeText("POOL DECISION", _semibold, 10, Accent, HorizontalAlignment.Left));
		_dossierHost.AddChild(MakeText(
			canDraft
				? "Draft him onto the program as unassigned, then set Varsity or JV on Final Roster Decisions."
				: "No roster room. Release a player on Final Roster Decisions, then come back.",
			_medium,
			12,
			canDraft ? TextMuted : Urgent,
			HorizontalAlignment.Left));

		Guid id = player.Id;
		var draft = new Button
		{
			Text = "DRAFT",
			Disabled = !canDraft,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		draft.AddThemeFontOverride("font", _bold);
		draft.AddThemeFontSizeOverride("font_size", 12);
		draft.AddThemeColorOverride("font_color", canDraft ? TextOnAccent : TextMuted);
		draft.AddThemeStyleboxOverride("normal", MakePill(canDraft ? Accent : new Color(0.12f, 0.13f, 0.16f), 10));
		draft.AddThemeStyleboxOverride("hover", MakePill(AccentHover, 10));
		draft.AddThemeStyleboxOverride("disabled", MakePill(new Color(0.12f, 0.13f, 0.16f), 10));
		draft.Pressed += () =>
		{
			if (_session.TryDraftFromPool(id))
			{
				_selectedId = Guid.Empty;
			}
		};
		_dossierHost.AddChild(draft);
	}

	private OpenPoolPlayer? FindPlayer(Guid id) => _session.OpenPool.Find(id);

	private static string SourceLabel(OpenPoolSource source) => source switch
	{
		OpenPoolSource.TryoutCut => "CUT",
		OpenPoolSource.Release => "RELEASE",
		_ => "UNSIGNED",
	};

	private static Color SourceColor(OpenPoolSource source) => source switch
	{
		OpenPoolSource.TryoutCut => Urgent,
		OpenPoolSource.Release => Accent,
		_ => TextMuted,
	};

	private Label MakeFixed(string text, float width, Color color, FontFile? font = null, int size = 13)
	{
		Label label = MakeText(text, font ?? _medium, size, color, HorizontalAlignment.Center);
		label.CustomMinimumSize = new Vector2(width, 0);
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

	private static MarginContainer PadBody(Control child, bool expand = true)
	{
		var pad = new MarginContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = expand ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
		};
		pad.AddThemeConstantOverride("margin_left", 14);
		pad.AddThemeConstantOverride("margin_top", 12);
		pad.AddThemeConstantOverride("margin_right", 14);
		pad.AddThemeConstantOverride("margin_bottom", 12);
		pad.AddChild(child);
		return pad;
	}

	private static PanelContainer MakeCard()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
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
		});
		return card;
	}

	private static StyleBoxFlat MakePill(Color bg, float padX) =>
		new()
		{
			BgColor = bg,
			ContentMarginLeft = padX,
			ContentMarginTop = 6,
			ContentMarginRight = padX,
			ContentMarginBottom = 6,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
		};
}
