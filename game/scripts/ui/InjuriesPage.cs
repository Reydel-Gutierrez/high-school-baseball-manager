using System;
using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// Clubhouse training room: injured players for the active squad, with
/// the same side profile card used on roster and organization lists.
/// </summary>
public partial class InjuriesPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.772549f, 0.635294f, 0.325490f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color CardBorder = new(0.137255f, 0.152941f, 0.200000f, 1f);
	private static readonly Color Hairline = new(0.17f, 0.19f, 0.24f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color RowSelected = new(0.772549f, 0.635294f, 0.325490f, 0.14f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private ShaderMaterial _circleMaterial = null!;

	private Label _subtitle = null!;
	private Label _countValue = null!;
	private VBoxContainer _rowsHost = null!;
	private VBoxContainer _dossierHost = null!;
	private int _selectedJersey;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_circleMaterial = new ShaderMaterial
		{
			Shader = GD.Load<Shader>("res://assets/ui/circle_crop.gdshader"),
		};
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		VisibilityChanged += OnVisibilityChanged;
		ApplyActiveTeam();
		Refresh();
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
			Refresh();
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _)
	{
		_selectedJersey = 0;
		ApplyActiveTeam();
		Refresh();
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
		header.AddChild(UserProgram.MakeHeaderMark());
		header.AddChild(TeamLogos.MakeHeaderRule(Hairline));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -4);
		identity.AddChild(MakeText($"CLUBHOUSE  ·  {GameSession.SeasonYear} SEASON", _semibold, 12, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("INJURIES", _bold, 36, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("CASCADE REGIONAL HIGH SCHOOL", _medium, 11, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var meta = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		_countValue = MakeText("0 INJURED", _bold, 16, TextPrimary, HorizontalAlignment.Right);
		meta.AddChild(_countValue);
		header.AddChild(meta);
		return header;
	}

	private Control BuildListCard()
	{
		var card = MakeCard(true);
		card.SizeFlagsStretchRatio = 1.55f;
		card.ClipContents = true;
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);

		var titlePad = new MarginContainer();
		titlePad.AddThemeConstantOverride("margin_left", 12);
		titlePad.AddThemeConstantOverride("margin_top", 8);
		titlePad.AddThemeConstantOverride("margin_right", 12);
		titlePad.AddThemeConstantOverride("margin_bottom", 6);
		titlePad.AddChild(MakeText("INJURED PLAYERS", _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(titlePad);
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
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
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
		columns.AddChild(MakeFixed("INJURY", 160, TextMuted, _semibold, 10, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed("OUT", 88, TextMuted, _semibold, 10, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed("STATUS", 110, TextMuted, _semibold, 10, HorizontalAlignment.Right));
		header.AddChild(columns);
		return header;
	}

	private Control BuildDossierCard()
	{
		var card = MakeCard(true);
		card.SizeFlagsStretchRatio = 1.05f;
		_dossierHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_dossierHost.AddThemeConstantOverride("separation", 0);
		card.AddChild(_dossierHost);
		return card;
	}

	private void Refresh()
	{
		if (_rowsHost == null)
		{
			return;
		}

		List<ClubhousePlayer> injured = ClubhouseSquad.Injured(_session.ActiveTeam.Level);
		_countValue.Text = injured.Count == 1 ? "1 INJURED" : $"{injured.Count} INJURED";
		_countValue.AddThemeColorOverride("font_color", injured.Count > 0 ? Urgent : Green);

		if (injured.Count > 0 && !Contains(injured, _selectedJersey))
		{
			_selectedJersey = injured[0].Jersey;
		}

		foreach (Node child in _rowsHost.GetChildren())
		{
			child.QueueFree();
		}

		if (injured.Count == 0)
		{
			_rowsHost.AddChild(MakeEmptyState());
		}
		else
		{
			for (int i = 0; i < injured.Count; i++)
			{
				_rowsHost.AddChild(BuildRow(injured[i], i % 2 == 0));
			}
		}

		RebuildDossier(injured);
	}

	private static bool Contains(IReadOnlyList<ClubhousePlayer> players, int jersey)
	{
		foreach (ClubhousePlayer player in players)
		{
			if (player.Jersey == jersey)
			{
				return true;
			}
		}

		return false;
	}

	private Control MakeEmptyState()
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		box.AddChild(MakeText("The training room is empty.", _semibold, 16, TextPrimary, HorizontalAlignment.Left));
		box.AddChild(MakeText(
			"Nobody on this squad is currently listed as injured.",
			_medium,
			13,
			TextMuted,
			HorizontalAlignment.Left));
		return PadBody(box, expand: false);
	}

	private Control BuildRow(ClubhousePlayer player, bool even)
	{
		bool selected = player.Jersey == _selectedJersey;
		var row = new PanelContainer { MouseDefaultCursorShape = CursorShape.PointingHand };
		row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = selected ? RowSelected : even ? RowEven : RowOdd,
			BorderColor = selected ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = selected ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 10,
		});
		int jersey = player.Jersey;
		row.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				_selectedJersey = jersey;
				Refresh();
			}
		};

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(BuildNameCell(player));
		columns.AddChild(MakeFixed(player.InjuryLabel, 160, TextPrimary, _medium, 13, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed(player.TimelineLabel, 88, TextPrimary, _medium, 13, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed(player.Status, 110, StatusColor(player.Status), _semibold, 13, HorizontalAlignment.Right));
		row.AddChild(columns);
		return row;
	}

	private Control BuildNameCell(ClubhousePlayer player)
	{
		var cell = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		cell.AddThemeConstantOverride("separation", 10);
		cell.AddChild(BuildPortrait(player, 36));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		identity.AddThemeConstantOverride("separation", -2);
		identity.AddChild(MakeText(player.FullName, _semibold, 14, TextPrimary, HorizontalAlignment.Left));
		identity.AddChild(MakeText(
			$"#{player.Jersey}  ·  {player.Position}  ·  {player.Year}",
			_medium,
			11,
			TextMuted,
			HorizontalAlignment.Left));
		cell.AddChild(identity);
		return cell;
	}

	private void RebuildDossier(List<ClubhousePlayer> injured)
	{
		foreach (Node child in _dossierHost.GetChildren())
		{
			child.QueueFree();
		}

		ClubhousePlayer? selected = null;
		foreach (ClubhousePlayer player in injured)
		{
			if (player.Jersey == _selectedJersey)
			{
				selected = player;
				break;
			}
		}

		if (selected == null)
		{
			_dossierHost.AddChild(PadBody(MakeText(
				"Select an injured player to open the profile card.",
				_medium,
				13,
				TextMuted,
				HorizontalAlignment.Left), expand: false));
			return;
		}

		HubSquadPlayer? hub = DistrictHubSquads.FindPlayer(
			DistrictHubData.UserTeamId,
			selected.FullName,
			_session.ActiveTeam.Level);
		var profile = new PlayerProfileCard
		{
			FrameVisible = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_dossierHost.AddChild(profile);
		if (hub != null)
		{
			profile.Bind(hub);
			return;
		}

		profile.Bind(PlayerProfiles.From(
			selected.FirstName,
			selected.LastName,
			selected.Jersey,
			selected.Position,
			selected.Year,
			selected.BatsThrows,
			selected.Overall,
			selected.Contact,
			selected.Power,
			selected.Speed,
			selected.Arm,
			selected.Field,
			selected.Status,
			selected.PortraitPath,
			DistrictHubData.UserTeamId,
			_session.Organization.ShortName,
			selected.Squad,
			Array.Empty<HubPlayerSeason>()));
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

	private Label MakeFixed(string text, float width, Color color, FontFile? font = null, int size = 13, HorizontalAlignment align = HorizontalAlignment.Center)
	{
		Label label = MakeText(text, font ?? _medium, size, color, align);
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

	private static PanelContainer MakeCard(bool expand)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = expand ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
		};
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
		});
		return card;
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
}
