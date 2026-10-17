using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

/// <summary>
/// Title screen shown before the clubhouse. Continue/new/load all enter the
/// current prototype career until save slots are wired to CareerService.
/// </summary>
public partial class MainMenu : Control
{
	private const string ClubhouseScene = "res://scenes/main/main.tscn";
	private const string SettingsScene = "res://scenes/pages/settings.tscn";
	private const float XpProgress = 4250f / 7500f;

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.70f, 0.74f, 0.80f, 1f);
	private static readonly Color TextDim = new(0.55f, 0.60f, 0.68f, 1f);
	private static readonly Color AccentBlue = new(0.117647f, 0.454902f, 0.960784f, 1f);
	private static readonly Color AccentBlueHot = new(0.22f, 0.56f, 1f, 1f);
	private static readonly Color ButtonIdle = new(0.055f, 0.078f, 0.145f, 0.78f);
	private static readonly Color ButtonHover = new(0.10f, 0.16f, 0.30f, 0.90f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.96f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);

	private enum MenuAction
	{
		ContinueCareer,
		NewCareer,
		LoadCareer,
		Settings,
		Credits,
		ExitGame,
	}

	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Control _wash = null!;
	private Control _overlay = null!;
	private Control _settingsHost = null!;
	private Control _dialogHost = null!;
	private Control _dialogBody = null!;
	private Label _dialogKicker = null!;
	private Label _dialogTitle = null!;
	private SettingsPage? _settingsPage;
	private CareersOverlay _careersOverlay = null!;
	private MenuAction _selected = MenuAction.ContinueCareer;
	private readonly List<MenuButtonBind> _buttons = new();

	private readonly struct MenuButtonBind
	{
		public MenuButtonBind(MenuAction action, Button button, Label title, Label hint, TextureRect icon)
		{
			Action = action;
			Button = button;
			Title = title;
			Hint = hint;
			Icon = icon;
		}

		public MenuAction Action { get; }
		public Button Button { get; }
		public Label Title { get; }
		public Label Hint { get; }
		public TextureRect Icon { get; }
	}

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");

		BuildWash();
		BuildTopBar();
		BuildMenuColumn();
		BuildOverlay();
		_careersOverlay = new CareersOverlay();
		AddChild(_careersOverlay);
		_careersOverlay.CareerChosen += _ => EnterCareer();
		CallDeferred(nameof(FocusContinue));
		AudioManager.Current.StartSoundtrack();
		AudioManager.Current.PlayAmbience(AmbienceTrack.StadiumNight);
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is not InputEventKey key || !key.Pressed || key.Echo || key.Keycode != Key.Escape)
		{
			return;
		}

		if (_careersOverlay.Visible)
		{
			return;
		}

		if (_overlay.Visible)
		{
			CloseOverlay(playBack: true);
			GetViewport().SetInputAsHandled();
		}
	}

	private void FocusContinue()
	{
		if (_buttons.Count > 0)
		{
			_buttons[0].Button.GrabFocus();
		}
	}

	private void BuildWash()
	{
		_wash = new Control
		{
			Name = "LeftWash",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_wash.SetAnchor(Side.Left, 0);
		_wash.SetAnchor(Side.Top, 0);
		_wash.SetAnchor(Side.Right, 0.38f);
		_wash.SetAnchor(Side.Bottom, 1);
		_wash.OffsetRight = 64;
		_wash.Draw += OnWashDraw;
		_wash.Resized += _wash.QueueRedraw;
		AddChild(_wash);
	}

	private void OnWashDraw()
	{
		float width = _wash.Size.X;
		float height = _wash.Size.Y;
		if (width <= 1f || height <= 1f)
		{
			return;
		}

		float slant = Mathf.Min(110f, width * 0.22f);
		Vector2[] fill =
		[
			Vector2.Zero,
			new(width - slant, 0),
			new(width, height),
			new(0, height),
		];
		_wash.DrawColoredPolygon(fill, new Color(0.012f, 0.028f, 0.062f, 0.82f));

		Vector2[] edge =
		[
			new(width - slant - 2f, 0),
			new(width - slant + 3f, 0),
			new(width + 3f, height),
			new(width - 2f, height),
		];
		_wash.DrawColoredPolygon(edge, new Color(0.12f, 0.32f, 0.72f, 0.28f));
	}

	private void BuildTopBar()
	{
		var bar = new PanelContainer
		{
			Name = "StatusBar",
		};
		bar.SetAnchor(Side.Left, 0);
		bar.SetAnchor(Side.Top, 0);
		bar.SetAnchor(Side.Right, 1);
		bar.SetAnchor(Side.Bottom, 0);
		bar.OffsetBottom = 78;
		bar.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.020f, 0.035f, 0.070f, 0.78f),
			BorderWidthBottom = 1,
			BorderColor = new Color(0.16f, 0.28f, 0.48f, 0.55f),
			ContentMarginLeft = 22,
			ContentMarginTop = 10,
			ContentMarginRight = 18,
			ContentMarginBottom = 10,
		});
		AddChild(bar);

		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		row.AddThemeConstantOverride("separation", 18);
		bar.AddChild(row);

		row.AddChild(BuildProfileCluster());
		row.AddChild(MakeFlex());
		row.AddChild(BuildDateCluster());
		row.AddChild(MakeFlex());
		row.AddChild(BuildBudgetCluster());
		row.AddChild(BuildSettingsButton());
	}

	private Control BuildProfileCluster()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);

		var avatar = new PanelContainer
		{
			CustomMinimumSize = new Vector2(52, 52),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		avatar.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.14f, 0.28f, 1f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = AccentBlue,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerDetail = 6,
		});
		var initials = MakeText(CareerDesk.CoachInitials, _bold, 20, TextPrimary, HorizontalAlignment.Center);
		initials.VerticalAlignment = VerticalAlignment.Center;
		initials.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		initials.SizeFlagsVertical = SizeFlags.ExpandFill;
		avatar.AddChild(initials);
		row.AddChild(avatar);

		var identity = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -3);
		identity.AddChild(MakeText(CareerDesk.CoachName.ToUpperInvariant(), _bold, 18, TextPrimary, HorizontalAlignment.Left));
		identity.AddChild(MakeText("HEAD COACH", _medium, 11, TextDim, HorizontalAlignment.Left));
		row.AddChild(identity);

		row.AddChild(BuildOverallBadge());
		row.AddChild(BuildXpBlock());
		return row;
	}

	private Control BuildOverallBadge()
	{
		var wrap = new Control
		{
			CustomMinimumSize = new Vector2(54, 56),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		wrap.MouseFilter = MouseFilterEnum.Ignore;
		wrap.Draw += () => DrawHex(wrap, AccentBlue);
		wrap.Resized += wrap.QueueRedraw;

		var labels = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		labels.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		labels.AddThemeConstantOverride("separation", -6);
		var value = MakeText("72", _bold, 16, TextPrimary, HorizontalAlignment.Center);
		value.VerticalAlignment = VerticalAlignment.Center;
		labels.AddChild(value);
		labels.AddChild(MakeText("OVR", _semibold, 9, TextPrimary, HorizontalAlignment.Center));
		wrap.AddChild(labels);
		return wrap;
	}

	private static void DrawHex(Control target, Color fill)
	{
		Vector2 center = target.Size * 0.5f;
		float radius = Mathf.Min(target.Size.X, target.Size.Y) * 0.46f;
		var points = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			float angle = Mathf.DegToRad(30 + i * 60);
			points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
		}

		target.DrawColoredPolygon(points, fill);
	}

	private Control BuildXpBlock()
	{
		var col = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(148, 0),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		col.AddThemeConstantOverride("separation", 3);
		col.AddChild(MakeText("LVL 12", _semibold, 11, TextPrimary, HorizontalAlignment.Left));

		var track = new Control
		{
			CustomMinimumSize = new Vector2(0, 8),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		var trackBg = new ColorRect
		{
			Color = new Color(0.10f, 0.14f, 0.22f, 1f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		trackBg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		var fill = new ColorRect
		{
			Color = AccentBlue,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		fill.SetAnchor(Side.Left, 0);
		fill.SetAnchor(Side.Top, 0);
		fill.SetAnchor(Side.Bottom, 1);
		fill.SetAnchor(Side.Right, XpProgress);
		track.AddChild(trackBg);
		track.AddChild(fill);
		col.AddChild(track);
		col.AddChild(MakeText("4,250 / 7,500 XP", _medium, 10, TextDim, HorizontalAlignment.Left));
		return col;
	}

	private Control BuildDateCluster()
	{
		var row = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		row.AddThemeConstantOverride("separation", 10);
		row.AddChild(MakeIcon(UiSvg.Load("res://assets/ui/icon_calendar.svg"), 22));

		DateOnly date = GameSession.Current.CurrentDate;
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", -3);
		col.AddChild(MakeText(
			date.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture).ToUpperInvariant(),
			_bold,
			16,
			TextPrimary,
			HorizontalAlignment.Left));
		col.AddChild(MakeText("REGULAR SEASON", _medium, 11, TextDim, HorizontalAlignment.Left));
		row.AddChild(col);
		return row;
	}

	private Control BuildBudgetCluster()
	{
		var row = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		row.AddThemeConstantOverride("separation", 10);

		var coin = new PanelContainer
		{
			CustomMinimumSize = new Vector2(34, 34),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		coin.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.10f, 0.18f, 0.34f, 1f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = AccentBlue,
			CornerRadiusTopLeft = 17,
			CornerRadiusTopRight = 17,
			CornerRadiusBottomRight = 17,
			CornerRadiusBottomLeft = 17,
			CornerDetail = 8,
		});
		var dollar = MakeText("$", _bold, 16, TextPrimary, HorizontalAlignment.Center);
		dollar.VerticalAlignment = VerticalAlignment.Center;
		dollar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		dollar.SizeFlagsVertical = SizeFlags.ExpandFill;
		coin.AddChild(dollar);
		row.AddChild(coin);

		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", -3);
		col.AddChild(MakeText("PROGRAM BUDGET", _medium, 10, TextDim, HorizontalAlignment.Left));
		col.AddChild(MakeText("$125,000", _bold, 18, TextPrimary, HorizontalAlignment.Left));
		row.AddChild(col);
		return row;
	}

	private Button BuildSettingsButton()
	{
		var button = new Button
		{
			Name = "MenuSettingsButton",
			Flat = false,
			CustomMinimumSize = new Vector2(40, 40),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			Icon = UiSvg.Load("res://assets/ui/gear_icon.svg"),
			ExpandIcon = true,
			TooltipText = "Game settings and preferences.",
		};
		button.AddThemeConstantOverride("icon_max_width", 18);
		button.AddThemeStyleboxOverride("normal", MakeIconButtonStyle(new Color(0.08f, 0.12f, 0.22f, 0.0f)));
		button.AddThemeStyleboxOverride("hover", MakeIconButtonStyle(ButtonHover));
		button.AddThemeStyleboxOverride("pressed", MakeIconButtonStyle(AccentBlue));
		button.AddThemeStyleboxOverride("focus", MakeIconButtonStyle(ButtonHover, 1));
		UiSounds.SetClick(button, UiClickSound.Navigate);
		button.Pressed += OpenSettings;
		return button;
	}

	private void BuildMenuColumn()
	{
		var margin = new MarginContainer
		{
			Name = "MenuColumn",
		};
		margin.SetAnchor(Side.Left, 0);
		margin.SetAnchor(Side.Top, 0);
		margin.SetAnchor(Side.Right, 0);
		margin.SetAnchor(Side.Bottom, 1);
		margin.OffsetLeft = 32;
		margin.OffsetTop = 90;
		margin.OffsetRight = 428;
		margin.OffsetBottom = -20;
		AddChild(margin);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		margin.AddChild(layout);

		layout.AddChild(BuildLogo());
		layout.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
		layout.AddChild(MakeMenuButton(
			MenuAction.ContinueCareer,
			"CONTINUE CAREER",
			"Continue your existing career.",
			"res://assets/ui/icon_home.svg",
			UiClickSound.Confirm,
			ContinueCareer));
		layout.AddChild(MakeMenuButton(
			MenuAction.NewCareer,
			"NEW CAREER",
			"Start a new coaching journey.",
			"res://assets/ui/icon_plus_circle.svg",
			UiClickSound.Confirm,
			StartNewCareer));
		layout.AddChild(MakeMenuButton(
			MenuAction.LoadCareer,
			"LOAD CAREER",
			"Load a previously saved career.",
			"res://assets/ui/icon_folder.svg",
			UiClickSound.Navigate,
			OpenLoadCareer));
		layout.AddChild(MakeMenuButton(
			MenuAction.Settings,
			"SETTINGS",
			"Game settings and preferences.",
			"res://assets/ui/gear_icon.svg",
			UiClickSound.Navigate,
			OpenSettings));
		layout.AddChild(MakeMenuButton(
			MenuAction.Credits,
			"CREDITS",
			"View the team behind the game.",
			"res://assets/ui/icon_star.svg",
			UiClickSound.Select,
			OpenCredits));
		layout.AddChild(MakeMenuButton(
			MenuAction.ExitGame,
			"EXIT GAME",
			"Exit High School Baseball Manager.",
			"res://assets/ui/icon_exit.svg",
			UiClickSound.Select,
			OpenExitConfirm));

		WireFocusNeighbors();
		RestyleMenuButtons();
	}

	private Control BuildLogo()
	{
		var logo = new TextureRect
		{
			Name = "GameLogo",
			Texture = LoadKeyedLogo(),
			CustomMinimumSize = new Vector2(0, 84),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = TextureFilterEnum.LinearWithMipmaps,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
		};
		return logo;
	}

	private Button MakeMenuButton(
		MenuAction action,
		string title,
		string hint,
		string iconPath,
		UiClickSound click,
		Action onPressed)
	{
		var button = new Button
		{
			Name = action.ToString(),
			Flat = false,
			Text = string.Empty,
			CustomMinimumSize = new Vector2(0, 56),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			ClipText = true,
		};
		UiSounds.SetClick(button, click);
		button.Pressed += onPressed;
		button.MouseEntered += RestyleMenuButtons;
		button.MouseExited += RestyleMenuButtons;
		button.FocusEntered += () =>
		{
			_selected = action;
			RestyleMenuButtons();
		};

		var margin = new MarginContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		button.AddChild(margin);

		var row = new HBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", 14);
		margin.AddChild(row);

		var icon = new TextureRect
		{
			Texture = UiSvg.Load(iconPath),
			CustomMinimumSize = new Vector2(22, 22),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddChild(icon);

		var text = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		text.AddThemeConstantOverride("separation", -2);
		var titleLabel = MakeText(title, _bold, 18, TextPrimary, HorizontalAlignment.Left);
		var hintLabel = MakeText(hint, _medium, 12, TextMuted, HorizontalAlignment.Left);
		text.AddChild(titleLabel);
		text.AddChild(hintLabel);
		row.AddChild(text);

		_buttons.Add(new MenuButtonBind(action, button, titleLabel, hintLabel, icon));
		return button;
	}

	private void WireFocusNeighbors()
	{
		int count = _buttons.Count;
		for (int i = 0; i < count; i++)
		{
			Button current = _buttons[i].Button;
			Button previous = _buttons[(i - 1 + count) % count].Button;
			Button next = _buttons[(i + 1) % count].Button;
			current.FocusNeighborTop = current.GetPathTo(previous);
			current.FocusNeighborBottom = current.GetPathTo(next);
			current.FocusNeighborLeft = new NodePath();
			current.FocusNeighborRight = new NodePath();
		}
	}

	private void RestyleMenuButtons()
	{
		foreach (MenuButtonBind bind in _buttons)
		{
			bool selected = bind.Action == _selected;
			bool hover = bind.Button.IsHovered();
			StyleBoxFlat style = selected
				? MakeMenuStyle(AccentBlue, AccentBlueHot, 10)
				: MakeMenuStyle(hover ? ButtonHover : ButtonIdle, new Color(0.16f, 0.32f, 0.62f, hover ? 0.7f : 0.28f), hover ? 6 : 0);
			bind.Button.AddThemeStyleboxOverride("normal", style);
			bind.Button.AddThemeStyleboxOverride("hover", style);
			bind.Button.AddThemeStyleboxOverride("pressed", MakeMenuStyle(AccentBlue, AccentBlueHot, 8));
			bind.Button.AddThemeStyleboxOverride("focus", style);
			bind.Title.AddThemeColorOverride("font_color", TextPrimary);
			bind.Hint.AddThemeColorOverride("font_color", selected ? new Color(0.86f, 0.92f, 1f, 0.92f) : TextMuted);
			bind.Icon.Modulate = TextPrimary;
		}
	}

	private void BuildOverlay()
	{
		_overlay = new Control
		{
			Name = "Overlay",
			Visible = false,
			MouseFilter = MouseFilterEnum.Stop,
		};
		_overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(_overlay);

		var dim = new ColorRect
		{
			Color = new Color(0.01f, 0.02f, 0.04f, 0.72f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		dim.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				CloseOverlay(playBack: true);
			}
		};
		_overlay.AddChild(dim);

		_settingsHost = new MarginContainer
		{
			Visible = false,
			MouseFilter = MouseFilterEnum.Stop,
		};
		_settingsHost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_settingsHost.AddThemeConstantOverride("margin_left", 36);
		_settingsHost.AddThemeConstantOverride("margin_top", 36);
		_settingsHost.AddThemeConstantOverride("margin_right", 36);
		_settingsHost.AddThemeConstantOverride("margin_bottom", 36);
		_overlay.AddChild(_settingsHost);
		_settingsHost.AddChild(BuildSettingsSheet());

		_dialogHost = new CenterContainer
		{
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_dialogHost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_overlay.AddChild(_dialogHost);
		_dialogHost.AddChild(BuildDialogSheet());
	}

	private Control BuildSettingsSheet()
	{
		var panel = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		panel.AddThemeStyleboxOverride("panel", MakeDialogPanel(18));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		panel.AddChild(layout);

		var closeBar = new HBoxContainer();
		closeBar.AddChild(MakeFlex());
		closeBar.AddChild(MakeDialogButton("CLOSE", false, CloseOverlay, UiClickSound.Back));
		layout.AddChild(closeBar);

		var slot = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		layout.AddChild(slot);

		var packed = GD.Load<PackedScene>(SettingsScene);
		_settingsPage = packed.Instantiate<SettingsPage>();
		_settingsPage.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		CanvasItem? background = _settingsPage.GetNodeOrNull<CanvasItem>("BackgroundImage");
		if (background != null)
		{
			background.Visible = false;
		}

		slot.AddChild(_settingsPage);
		return panel;
	}

	private Control BuildDialogSheet()
	{
		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(480, 0),
			MouseFilter = MouseFilterEnum.Stop,
		};
		panel.AddThemeStyleboxOverride("panel", MakeDialogPanel(16));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 12);
		panel.AddChild(layout);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		titles.AddThemeConstantOverride("separation", -2);
		_dialogKicker = MakeText("MENU", _medium, 10, AccentBlue, HorizontalAlignment.Left);
		_dialogTitle = MakeText("TITLE", _bold, 22, TextPrimary, HorizontalAlignment.Left);
		titles.AddChild(_dialogKicker);
		titles.AddChild(_dialogTitle);
		header.AddChild(titles);
		header.AddChild(MakeDialogButton("CLOSE", false, CloseOverlay, UiClickSound.Back));
		layout.AddChild(header);
		layout.AddChild(new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 2),
			Color = AccentBlue,
		});

		_dialogBody = new VBoxContainer();
		_dialogBody.AddThemeConstantOverride("separation", 10);
		layout.AddChild(_dialogBody);
		return panel;
	}

	private void OpenSettings()
	{
		ShowOverlay(settings: true);
		_settingsPage?.SetInGame(false);
		_settingsPage?.FocusAudio();
	}

	private void OpenLoadCareer()
	{
		_careersOverlay.Open(CareersOverlay.Mode.LoadOnly);
	}

	private void OpenCredits()
	{
		FillDialog("THE BENCH", "CREDITS", BuildCreditsBody());
		ShowOverlay(settings: false);
	}

	private void OpenExitConfirm()
	{
		FillDialog("LEAVE", "EXIT GAME", BuildExitBody());
		ShowOverlay(settings: false);
	}

	private void FillDialog(string kicker, string title, Control body)
	{
		_dialogKicker.Text = kicker;
		_dialogTitle.Text = title;
		foreach (Node child in _dialogBody.GetChildren())
		{
			child.QueueFree();
		}

		_dialogBody.AddChild(body);
	}

	private Control BuildLoadBody()
	{
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 12);

		var slot = new Button
		{
			Flat = false,
			Text = string.Empty,
			CustomMinimumSize = new Vector2(0, 92),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			FocusMode = FocusModeEnum.All,
		};
		slot.AddThemeStyleboxOverride("normal", MakeMenuStyle(ButtonIdle, CardBorder, 0));
		slot.AddThemeStyleboxOverride("hover", MakeMenuStyle(ButtonHover, AccentBlue, 0));
		slot.AddThemeStyleboxOverride("pressed", MakeMenuStyle(AccentBlue, AccentBlueHot, 0));
		slot.AddThemeStyleboxOverride("focus", MakeMenuStyle(ButtonHover, AccentBlue, 0));
		UiSounds.SetClick(slot, UiClickSound.Confirm);
		slot.Pressed += EnterCareer;

		var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		slot.AddChild(margin);

		var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", -1);
		GameSession session = GameSession.Current;
		col.AddChild(MakeText(session.Organization.Name.ToUpperInvariant(), _bold, 18, TextPrimary, HorizontalAlignment.Left));
		col.AddChild(MakeText(
			$"{CareerDesk.CoachName.ToUpperInvariant()}  ·  HEAD COACH",
			_medium,
			12,
			TextMuted,
			HorizontalAlignment.Left));
		col.AddChild(MakeText(
			$"{session.CurrentDate.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture).ToUpperInvariant()}  ·  {CareerDesk.ThisYearRecord}  ·  YEAR {session.ContractYear} OF {session.ContractLength}",
			_semibold,
			12,
			AccentBlueHot,
			HorizontalAlignment.Left));
		margin.AddChild(col);
		layout.AddChild(slot);
		layout.AddChild(MakeText(
			"One prototype career is available until multiple save slots are wired.",
			_medium,
			12,
			TextDim,
			HorizontalAlignment.Left));
		return layout;
	}

	private Control BuildCreditsBody()
	{
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		layout.AddChild(MakeText("HIGH SCHOOL BASEBALL MANAGER", _bold, 18, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(MakeText(
			"A high school baseball management simulation.",
			_medium,
			13,
			TextMuted,
			HorizontalAlignment.Left));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = CardBorder });
		layout.AddChild(CreditLine("ENGINE", "Godot 4"));
		layout.AddChild(CreditLine("TYPE", "Barlow Condensed"));
		layout.AddChild(CreditLine("UI AUDIO", "Kenney"));
		layout.AddChild(CreditLine("CREATED BY", "Reydel Gutierrez"));
		layout.AddChild(CreditLine("MUSIC & SFX", "See assets/audio/AUDIO_README.md"));
		return layout;
	}

	private Control CreditLine(string role, string name)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		var left = MakeText(role, _semibold, 12, TextDim, HorizontalAlignment.Left);
		left.CustomMinimumSize = new Vector2(140, 0);
		row.AddChild(left);
		row.AddChild(MakeText(name, _bold, 14, TextPrimary, HorizontalAlignment.Left));
		return row;
	}

	private Control BuildExitBody()
	{
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 14);
		layout.AddChild(MakeText(
			"Exit High School Baseball Manager?",
			_medium,
			14,
			TextMuted,
			HorizontalAlignment.Left));
		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 8);
		actions.AddChild(MakeDialogButton("CANCEL", false, CloseOverlay, UiClickSound.Back));
		actions.AddChild(MakeDialogButton("EXIT GAME", true, QuitGame, UiClickSound.Confirm));
		layout.AddChild(actions);
		return layout;
	}

	private void ShowOverlay(bool settings)
	{
		_overlay.Visible = true;
		_settingsHost.Visible = settings;
		_dialogHost.Visible = !settings;
	}

	private void CloseOverlay() => CloseOverlay(playBack: false);

	private void CloseOverlay(bool playBack)
	{
		if (!_overlay.Visible)
		{
			return;
		}

		_overlay.Visible = false;
		_settingsHost.Visible = false;
		_dialogHost.Visible = false;
		if (playBack)
		{
			UiSounds.Play(UiSound.Back);
		}

		FocusContinue();
	}

	private void ContinueCareer()
	{
		CareerLibrary.EnsureReady();
		CareerSaveSlot? active = CareerLibrary.ActiveSlot();
		if (active != null)
		{
			GameSession.Current.ApplyCareerSlot(active);
		}

		EnterCareer();
	}

	private void StartNewCareer()
	{
		CareerCreationSession.Current.StartOrResume();
		GetTree().ChangeSceneToFile(CareerCreationChrome.CoachCreationScene);
	}

	private void EnterCareer()
	{
		AudioManager.Current.StopAmbience();
		GetTree().ChangeSceneToFile(ClubhouseScene);
	}

	private void QuitGame()
	{
		GetTree().Quit();
	}

	private Button MakeDialogButton(string text, bool primary, Action onPressed, UiClickSound click)
	{
		var button = new Button
		{
			Text = text,
			Flat = false,
			CustomMinimumSize = new Vector2(primary ? 120 : 88, 36),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			FocusMode = FocusModeEnum.All,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 13);
		button.AddThemeColorOverride("font_color", primary ? new Color(0.05f, 0.07f, 0.10f) : TextPrimary);
		button.AddThemeColorOverride("font_hover_color", primary ? new Color(0.05f, 0.07f, 0.10f) : TextPrimary);
		button.AddThemeStyleboxOverride("normal", MakeMenuStyle(primary ? AccentBlue : ButtonIdle, CardBorder, 0));
		button.AddThemeStyleboxOverride("hover", MakeMenuStyle(primary ? AccentBlueHot : ButtonHover, AccentBlue, 0));
		button.AddThemeStyleboxOverride("pressed", MakeMenuStyle(AccentBlue, AccentBlueHot, 0));
		button.AddThemeStyleboxOverride("focus", MakeMenuStyle(primary ? AccentBlueHot : ButtonHover, AccentBlue, 0));
		UiSounds.SetClick(button, click);
		button.Pressed += onPressed;
		return button;
	}

	private static Control MakeFlex()
	{
		return new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
	}

	private static TextureRect MakeIcon(Texture2D texture, float size)
	{
		return new TextureRect
		{
			Texture = texture,
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
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

	private static StyleBoxFlat MakeMenuStyle(Color fill, Color border, int glow)
	{
		return new StyleBoxFlat
		{
			BgColor = fill,
			DrawCenter = true,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			BorderColor = border,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerDetail = 6,
			ContentMarginLeft = 0,
			ContentMarginTop = 0,
			ContentMarginRight = 0,
			ContentMarginBottom = 0,
			ShadowColor = new Color(border.R, border.G, border.B, glow > 0 ? 0.42f : 0f),
			ShadowSize = glow,
			ShadowOffset = Vector2.Zero,
		};
	}

	private static StyleBoxFlat MakeIconButtonStyle(Color fill, int border = 0)
	{
		return new StyleBoxFlat
		{
			BgColor = fill,
			DrawCenter = true,
			ContentMarginLeft = 8,
			ContentMarginTop = 8,
			ContentMarginRight = 8,
			ContentMarginBottom = 8,
			BorderWidthLeft = border,
			BorderWidthTop = border,
			BorderWidthRight = border,
			BorderWidthBottom = border,
			BorderColor = AccentBlue,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
		};
	}

	private static StyleBoxFlat MakeDialogPanel(float pad)
	{
		return new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = pad,
			ContentMarginTop = pad,
			ContentMarginRight = pad,
			ContentMarginBottom = pad,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerDetail = 4,
		};
	}

	private static Texture2D LoadKeyedLogo()
	{
		var source = GD.Load<Texture2D>("res://assets/themes/gamelogo.png");
		if (source == null)
		{
			return null!;
		}

		Image? image = source.GetImage();
		if (image == null || image.IsEmpty())
		{
			return source!;
		}

		if (image.GetFormat() != Image.Format.Rgba8)
		{
			image.Convert(Image.Format.Rgba8);
		}

		int width = image.GetWidth();
		int height = image.GetHeight();
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				Color pixel = image.GetPixel(x, y);
				float luma = (0.2126f * pixel.R) + (0.7152f * pixel.G) + (0.0722f * pixel.B);
				if (luma < 0.11f)
				{
					pixel.A *= Mathf.Clamp((luma - 0.03f) / 0.08f, 0f, 1f);
					image.SetPixel(x, y, pixel);
				}
			}
		}

		return ImageTexture.CreateFromImage(image);
	}
}
