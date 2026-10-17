using System;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// Full-screen pregame desk. No top bar or menus — stadium, logos, and
/// the four actions a coach needs before first pitch.
/// </summary>
public partial class GamePreviewPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.88f);
	private static readonly Color CardFill = new(0.070588f, 0.078431f, 0.094118f, 0.86f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private ShaderMaterial _circleMaterial = null!;
	private TextureRect _homeMark = null!;
	private TextureRect _awayMark = null!;
	private VBoxContainer _body = null!;
	private VBoxContainer _actionsHost = null!;
	private CalendarContest? _game;
	private bool _built;

	public event Action? Closed;
	public event Action? EditLineupRequested;
	public event Action? EditRotationRequested;
	public event Action? PlayGameRequested;
	public event Action? SimulateRequested;

	public override void _Ready()
	{
		EnsureBuilt();
		if (_game != null)
		{
			Rebuild();
		}
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (!Visible || !ev.IsActionPressed("ui_cancel"))
		{
			return;
		}

		GoBack();
		GetViewport().SetInputAsHandled();
	}

	public void Bind(CalendarContest game)
	{
		_game = game;
		if (_built)
		{
			Rebuild();
		}
	}

	private void EnsureBuilt()
	{
		if (_built)
		{
			return;
		}

		_built = true;
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_circleMaterial = new ShaderMaterial
		{
			Shader = GD.Load<Shader>("res://assets/ui/circle_crop.gdshader"),
		};
		_session = GetNode<GameSession>("/root/GameSession");
		MouseFilter = MouseFilterEnum.Stop;

		var dim = new ColorRect
		{
			Color = new Color(0.02f, 0.025f, 0.04f, 0.48f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		_homeMark = MakeWatermark(true);
		_awayMark = MakeWatermark(false);
		AddChild(_homeMark);
		AddChild(_awayMark);

		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 28);
		margin.AddThemeConstantOverride("margin_top", 20);
		margin.AddThemeConstantOverride("margin_right", 28);
		margin.AddThemeConstantOverride("margin_bottom", 20);
		AddChild(margin);

		var shell = new VBoxContainer();
		shell.AddThemeConstantOverride("separation", 12);
		margin.AddChild(shell);
		shell.AddChild(BuildChrome());

		// Matchup details scroll; the pregame actions stay pinned below so
		// PLAY GAME is reachable at any window size.
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		shell.AddChild(scroll);
		_body = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_body.AddThemeConstantOverride("separation", 12);
		scroll.AddChild(_body);

		_actionsHost = new VBoxContainer();
		shell.AddChild(_actionsHost);
	}

	private void Rebuild()
	{
		if (_game == null)
		{
			return;
		}

		GamePreviewSheet sheet = GamePreviewDesk.For(_game, _session);
		ApplyMarks(sheet);

		foreach (Node child in _body.GetChildren())
		{
			child.QueueFree();
		}

		foreach (Node child in _actionsHost.GetChildren())
		{
			child.QueueFree();
		}

		var card = MakeGlass();
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		_body.AddChild(card);

		var layout = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		layout.AddThemeConstantOverride("separation", 14);
		card.AddChild(layout);

		layout.AddChild(BuildHeadline(sheet));
		layout.AddChild(MakeAccentLine());
		layout.AddChild(BuildMatchup(sheet));
		layout.AddChild(BuildPitchers(sheet));
		layout.AddChild(BuildComparison(sheet));
		_actionsHost.AddChild(BuildActions());
	}

	private Control BuildChrome()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);

		Button back = MakeAction("GO BACK", false, 0f, GoBack, 132);
		back.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		UiSounds.SetClick(back, UiClickSound.Back);
		row.AddChild(back);

		var titles = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		titles.AddThemeConstantOverride("separation", -2);
		titles.AddChild(MakeText("GAME PREVIEW", _bold, 26, TextPrimary, HorizontalAlignment.Center));
		row.AddChild(titles);

		var spacer = new Control { CustomMinimumSize = new Vector2(132, 0) };
		row.AddChild(spacer);
		return row;
	}

	private Control BuildHeadline(GamePreviewSheet sheet)
	{
		CalendarContest game = sheet.Game;
		string when = game.Date.ToString("dddd, MMMM d", CultureInfo.InvariantCulture).ToUpperInvariant();
		string kind = game.Exhibition ? "EXHIBITION" : game.Level.ToLabel().ToUpperInvariant();
		string venue = game.Home ? "HOME" : "AWAY";

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", -2);
		box.AddChild(MakeText(kind, _medium, 12, Accent, HorizontalAlignment.Center));
		box.AddChild(MakeText($"{when}  ·  {game.Time}  ·  {venue}", _semibold, 16, TextPrimary, HorizontalAlignment.Center));
		return box;
	}

	private Control BuildMatchup(GamePreviewSheet sheet)
	{
		var row = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.6f,
		};
		row.AddThemeConstantOverride("separation", 18);
		row.AddChild(BuildTeam(sheet.HomeTeamId, sheet.HomeName, sheet.HomeRecord));

		var vs = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(64, 0),
		};
		vs.AddThemeConstantOverride("separation", 0);
		vs.AddChild(MakeText("VS", _bold, 28, TextMuted, HorizontalAlignment.Center));
		row.AddChild(vs);

		row.AddChild(BuildTeam(sheet.AwayTeamId, sheet.AwayName, sheet.AwayRecord));
		return row;
	}

	private Control BuildTeam(string teamId, string name, string record)
	{
		var col = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		col.AddThemeConstantOverride("separation", 8);

		Texture2D? logo = TeamLogos.Load(teamId);
		if (logo != null)
		{
			col.AddChild(new TextureRect
			{
				Texture = logo,
				CustomMinimumSize = new Vector2(168, 168),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill,
				TextureFilter = TextureFilterEnum.LinearWithMipmaps,
				MouseFilter = MouseFilterEnum.Ignore,
			});
		}
		else
		{
			col.AddChild(BuildFallbackMark(ClubhouseCalendar.OpponentMark(teamId), 128));
		}

		var title = MakeText(name, _bold, 32, TextPrimary, HorizontalAlignment.Center);
		title.AutowrapMode = TextServer.AutowrapMode.Word;
		col.AddChild(title);
		col.AddChild(MakeText(record, _semibold, 22, Accent, HorizontalAlignment.Center));
		return col;
	}

	private Control BuildPitchers(GamePreviewSheet sheet)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		box.AddChild(MakeText("STARTING PITCHERS", _semibold, 12, Accent, HorizontalAlignment.Center));

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 18);
		row.AddChild(BuildPitcher(sheet.HomeStarter));
		row.AddChild(BuildPitcher(sheet.AwayStarter));
		box.AddChild(row);
		return box;
	}

	private Control BuildPitcher(GamePreviewStarter starter)
	{
		var chip = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		chip.AddThemeStyleboxOverride("panel", MakeInnerCard(16, 12));

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 14);
		chip.AddChild(row);
		row.AddChild(BuildPortrait(starter));

		var copy = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		copy.AddThemeConstantOverride("separation", -2);
		copy.AddChild(MakeText(starter.Hand, _semibold, 12, Accent, HorizontalAlignment.Left));
		var name = MakeText(starter.Name.ToUpperInvariant(), _bold, 22, TextPrimary, HorizontalAlignment.Left);
		name.AutowrapMode = TextServer.AutowrapMode.Word;
		copy.AddChild(name);
		copy.AddChild(MakeText($"{starter.Record}  ·  {starter.Era} ERA", _semibold, 16, TextMuted, HorizontalAlignment.Left));
		row.AddChild(copy);
		return chip;
	}

	private Control BuildComparison(GamePreviewSheet sheet)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		box.AddChild(MakeText("TEAM COMPARISON", _semibold, 12, Accent, HorizontalAlignment.Center));

		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", MakeInnerCard(18, 12));
		box.AddChild(card);

		var grid = new VBoxContainer();
		grid.AddThemeConstantOverride("separation", 6);
		card.AddChild(grid);

		grid.AddChild(BuildCompareHeader(sheet.HomeName, sheet.AwayName));
		grid.AddChild(MakeHairline());
		grid.AddChild(BuildCompareRow("AVG", sheet.HomeAvg, sheet.AwayAvg, higherIsBetter: true));
		grid.AddChild(BuildCompareRow("ERA", sheet.HomeEra, sheet.AwayEra, higherIsBetter: false));
		grid.AddChild(BuildCompareRow("DEF", sheet.HomeDef, sheet.AwayDef, higherIsBetter: true));
		return box;
	}

	private Control BuildCompareHeader(string home, string away)
	{
		var row = new HBoxContainer();
		row.AddChild(HeaderCell(home, HorizontalAlignment.Right, SizeFlags.ExpandFill));
		row.AddChild(HeaderCell(string.Empty, HorizontalAlignment.Center, SizeFlags.ShrinkCenter, 72));
		row.AddChild(HeaderCell(away, HorizontalAlignment.Left, SizeFlags.ExpandFill));
		return row;
	}

	private Label HeaderCell(string text, HorizontalAlignment align, SizeFlags stretch, float width = 0)
	{
		var label = MakeText(text, _medium, 11, TextMuted, align);
		label.SizeFlagsHorizontal = stretch;
		if (width > 0)
		{
			label.CustomMinimumSize = new Vector2(width, 0);
		}

		return label;
	}

	private Control BuildCompareRow(string stat, string home, string away, bool higherIsBetter)
	{
		Color homeColor = TextPrimary;
		Color awayColor = TextPrimary;
		if (TryParseStat(home, out float homeValue) && TryParseStat(away, out float awayValue)
			&& Math.Abs(homeValue - awayValue) > 0.0001f)
		{
			bool homeBetter = higherIsBetter ? homeValue > awayValue : homeValue < awayValue;
			homeColor = homeBetter ? Green : TextMuted;
			awayColor = homeBetter ? TextMuted : Green;
		}

		var row = new HBoxContainer();
		var left = MakeText(home, _bold, 26, homeColor, HorizontalAlignment.Right);
		left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(left);

		var mid = MakeText(stat, _semibold, 14, TextMuted, HorizontalAlignment.Center);
		mid.CustomMinimumSize = new Vector2(72, 0);
		row.AddChild(mid);

		var right = MakeText(away, _bold, 26, awayColor, HorizontalAlignment.Left);
		right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(right);
		return row;
	}

	private Control BuildActions()
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);

		var prep = new HBoxContainer();
		prep.AddThemeConstantOverride("separation", 8);
		prep.AddChild(MakeAction("EDIT LINEUP", false, 1f, () => EditLineupRequested?.Invoke()));
		prep.AddChild(MakeAction("EDIT ROTATION", false, 1f, () => EditRotationRequested?.Invoke()));
		box.AddChild(prep);

		var play = new HBoxContainer();
		play.AddThemeConstantOverride("separation", 8);
		Button playGame = MakeAction("PLAY GAME", true, 1.45f, () => PlayGameRequested?.Invoke());
		UiSounds.SetClick(playGame, UiClickSound.Confirm);
		play.AddChild(playGame);
		play.AddChild(MakeAction("SIMULATE", false, 1f, () => SimulateRequested?.Invoke()));
		box.AddChild(play);
		return box;
	}

	private void ApplyMarks(GamePreviewSheet sheet)
	{
		_homeMark.Texture = TeamLogos.Load(sheet.HomeTeamId);
		_awayMark.Texture = TeamLogos.Load(sheet.AwayTeamId);
		_homeMark.Visible = _homeMark.Texture != null;
		_awayMark.Visible = _awayMark.Texture != null;
	}

	private void GoBack()
	{
		UiSounds.Play(UiSound.Back);
		Closed?.Invoke();
	}

	private Control BuildPortrait(GamePreviewStarter starter)
	{
		const float size = 64f;
		var wrap = new Control
		{
			CustomMinimumSize = new Vector2(size, size),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};

		Texture2D? texture = string.IsNullOrEmpty(starter.PortraitPath)
			? null
			: GD.Load<Texture2D>(starter.PortraitPath);
		if (texture != null)
		{
			var portrait = new TextureRect
			{
				Texture = texture,
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

		wrap.AddChild(BuildFallbackMark(starter.Hand[..1], size));
		return wrap;
	}

	private Control BuildFallbackMark(string initial, float size)
	{
		int radius = Mathf.RoundToInt(size / 2f);
		var fallback = new PanelContainer
		{
			CustomMinimumSize = new Vector2(size, size),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
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
		var label = MakeText(initial, _bold, Mathf.RoundToInt(size * 0.34f), Accent, HorizontalAlignment.Center);
		label.VerticalAlignment = VerticalAlignment.Center;
		fallback.AddChild(label);
		return fallback;
	}

	private static TextureRect MakeWatermark(bool home)
	{
		var mark = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Modulate = new Color(1f, 1f, 1f, 0.10f),
			MouseFilter = MouseFilterEnum.Ignore,
			TextureFilter = TextureFilterEnum.LinearWithMipmaps,
		};
		mark.SetAnchor(Side.Left, home ? 0f : 0.52f);
		mark.SetAnchor(Side.Right, home ? 0.48f : 1f);
		mark.SetAnchor(Side.Top, 0.08f);
		mark.SetAnchor(Side.Bottom, 0.92f);
		mark.OffsetLeft = 0;
		mark.OffsetTop = 0;
		mark.OffsetRight = 0;
		mark.OffsetBottom = 0;
		return mark;
	}

	private PanelContainer MakeGlass()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardFill,
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 28,
			ContentMarginTop = 18,
			ContentMarginRight = 28,
			ContentMarginBottom = 18,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerDetail = 4,
			ShadowColor = new Color(0f, 0f, 0f, 0.35f),
			ShadowSize = 12,
			ShadowOffset = new Vector2(0, 4),
		});
		return card;
	}

	private Button MakeAction(string text, bool primary, float stretch, Action onPressed, float minWidth = 0)
	{
		var button = new Button
		{
			Text = text,
			SizeFlagsHorizontal = stretch > 0 ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
			SizeFlagsStretchRatio = stretch,
			CustomMinimumSize = new Vector2(minWidth, 40),
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 14);
		if (primary)
		{
			button.AddThemeColorOverride("font_color", TextOnAccent);
			button.AddThemeColorOverride("font_hover_color", TextOnAccent);
			button.AddThemeColorOverride("font_pressed_color", TextOnAccent);
			button.AddThemeColorOverride("font_focus_color", TextOnAccent);
			button.AddThemeStyleboxOverride("normal", MakeFilledButton(Accent));
			button.AddThemeStyleboxOverride("hover", MakeFilledButton(AccentHover));
			button.AddThemeStyleboxOverride("pressed", MakeFilledButton(AccentHover));
			button.AddThemeStyleboxOverride("focus", MakeFilledButton(AccentHover));
		}
		else
		{
			button.AddThemeColorOverride("font_color", TextPrimary);
			button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, CardBorder));
			button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
			button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
			button.AddThemeStyleboxOverride("focus", MakeOutlinedButton(HoverBg, Accent));
		}

		button.Pressed += onPressed;
		return button;
	}

	private static bool TryParseStat(string value, out float parsed) =>
		float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);

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
		};
	}

	private static ColorRect MakeHairline()
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 1),
			Color = Hairline,
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

	private static StyleBoxFlat MakeFilledButton(Color bg)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			ContentMarginLeft = 16,
			ContentMarginTop = 10,
			ContentMarginRight = 16,
			ContentMarginBottom = 10,
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
			ContentMarginLeft = 16,
			ContentMarginTop = 10,
			ContentMarginRight = 16,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};
	}
}
