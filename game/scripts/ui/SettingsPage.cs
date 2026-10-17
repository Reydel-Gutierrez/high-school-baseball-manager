using System;
using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// Machine preferences opened from the top-bar wrench. Audio, visual,
/// and autosave are live; controls and accessibility stay stubbed.
/// </summary>
public partial class SettingsPage : Control
{
	private enum SettingsMenu
	{
		FileManagement,
		Audio,
		Visual,
		Gameplay,
		Controls,
		Accessibility,
	}

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color TrackBg = new(0.121569f, 0.137255f, 0.160784f, 1f);

	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _gear = null!;
	private Texture2D _grabber = null!;
	private Texture2D _grabberHover = null!;

	private readonly Dictionary<SettingsMenu, Button> _menuButtons = new();
	private readonly Dictionary<SettingsMenu, Control> _panels = new();
	private SettingsMenu _menu = SettingsMenu.Audio;
	private bool _syncing;
	private FileManagementPanel _fileManagement = null!;

	public event Action? SavedAndExited;

	public event Action? ExitGameRequested;

	private Button _muteButton = null!;
	private Button _shuffleButton = null!;
	private Button _pauseButton = null!;
	private Label _trackTitle = null!;
	private Label _trackMeta = null!;
	private readonly Dictionary<AudioChannel, HSlider> _sliders = new();
	private readonly Dictionary<AudioChannel, Label> _percents = new();
	private Button _autosaveMaster = null!;
	private readonly Dictionary<string, Button> _autosaveToggles = new();
	private Button _simRecapToggle = null!;
	private OptionButton _windowModeChoice = null!;
	private OptionButton _resolutionChoice = null!;
	private OptionButton _scaleChoice = null!;
	private Label _resolutionHint = null!;
	private HSlider _uiScaleSlider = null!;
	private Label _uiScalePercent = null!;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_gear = UiSvg.Load("res://assets/ui/gear_icon.svg", 512);
		_grabber = MakeGrabber(Accent, 16);
		_grabberHover = MakeGrabber(AccentHover, 16);

		BuildPage();
		AudioManager.Current.SoundtrackChanged += OnSoundtrackChanged;
		SetInGame(false);
		ShowMenu(SettingsMenu.Audio);
		RefreshAudio();
	}

	public override void _ExitTree()
	{
		if (AudioManager.Current != null)
		{
			AudioManager.Current.SoundtrackChanged -= OnSoundtrackChanged;
		}
	}

	public void FocusAudio()
	{
		ShowMenu(SettingsMenu.Audio);
		RefreshAudio();
	}

	public void SetInGame(bool inGame)
	{
		if (_menuButtons.TryGetValue(SettingsMenu.FileManagement, out Button? button))
		{
			button.Visible = inGame;
		}

		if (!inGame && _menu == SettingsMenu.FileManagement)
		{
			ShowMenu(SettingsMenu.Audio);
		}
	}

	private void OnSoundtrackChanged() => RefreshNowPlaying();

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
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 8);
		layout.AddChild(body);
		body.AddChild(BuildMenuCard());
		body.AddChild(BuildContentHost());
	}

	private Control BuildHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);
		TextureRect mark = TeamLogos.MakeHeaderMark(_gear, 52);
		mark.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
		header.AddChild(mark);
		header.AddChild(TeamLogos.MakeHeaderRule(Hairline));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -4);
		identity.AddChild(MakeText("MACHINE  ·  PREFERENCES", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("SETTINGS", _bold, 22, TextPrimary, HorizontalAlignment.Left));
		identity.AddChild(MakeText(
			"Audio, files, visual, and gameplay options for this install.",
			_medium,
			11,
			new Color(0.72f, 0.76f, 0.82f),
			HorizontalAlignment.Left));
		header.AddChild(identity);
		return header;
	}

	private Control BuildMenuCard()
	{
		var card = new PanelContainer
		{
			CustomMinimumSize = new Vector2(230, 0),
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(0));

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
		heading.AddChild(MakeText("MENUS", _bold, 14, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(heading);
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });

		var list = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		list.AddThemeConstantOverride("separation", 4);
		var listMargin = new MarginContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		listMargin.AddThemeConstantOverride("margin_left", 8);
		listMargin.AddThemeConstantOverride("margin_top", 8);
		listMargin.AddThemeConstantOverride("margin_right", 8);
		listMargin.AddThemeConstantOverride("margin_bottom", 8);
		listMargin.AddChild(list);
		layout.AddChild(listMargin);

		list.AddChild(MakeMenuButton(SettingsMenu.FileManagement, "FILE MANAGEMENT", "Save, save as, load, return to menu, or exit"));
		list.AddChild(MakeMenuButton(SettingsMenu.Audio, "AUDIO", "Music, system sounds, and SFX"));
		list.AddChild(MakeMenuButton(SettingsMenu.Visual, "VISUAL", "Display, scale, and lighting"));
		list.AddChild(MakeMenuButton(SettingsMenu.Gameplay, "GAMEPLAY", "Autosave and simulation recap"));
		list.AddChild(MakeMenuButton(SettingsMenu.Controls, "CONTROLS", "Keyboard and pointer"));
		list.AddChild(MakeMenuButton(SettingsMenu.Accessibility, "ACCESSIBILITY", "Readability and motion"));
		return card;
	}

	private Button MakeMenuButton(SettingsMenu menu, string title, string hint)
	{
		var button = new Button
		{
			Flat = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 52),
			MouseDefaultCursorShape = CursorShape.PointingHand,
			Text = title,
			Alignment = HorizontalAlignment.Left,
			ClipText = true,
			TooltipText = hint,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 15);
		button.Pressed += () => ShowMenu(menu);
		_menuButtons[menu] = button;
		return button;
	}

	private Control BuildContentHost()
	{
		var host = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};

		_fileManagement = new FileManagementPanel();
		_fileManagement.SavedAndExited += () => SavedAndExited?.Invoke();
		_fileManagement.ExitGameRequested += () => ExitGameRequested?.Invoke();
		_panels[SettingsMenu.FileManagement] = _fileManagement;
		_panels[SettingsMenu.Audio] = BuildAudioPanel();
		_panels[SettingsMenu.Visual] = BuildVisualPanel();
		_panels[SettingsMenu.Gameplay] = BuildGameplayPanel();
		_panels[SettingsMenu.Controls] = BuildComingSoonPanel(
			"CONTROLS",
			"Keyboard shortcuts and pointer behavior will land here once the sim screens need them.");
		_panels[SettingsMenu.Accessibility] = BuildComingSoonPanel(
			"ACCESSIBILITY",
			"Larger type, reduced motion, and color-safe accents will land here.");

		foreach (Control panel in _panels.Values)
		{
			panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			panel.Visible = false;
			host.AddChild(panel);
		}

		return host;
	}

	private Control BuildAudioPanel()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(0));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);
		layout.AddChild(BuildPanelHeading("AUDIO", "Volumes for music, system clicks, and gameplay sounds."));

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);

		var body = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 10);
		var bodyMargin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		bodyMargin.AddThemeConstantOverride("margin_left", 14);
		bodyMargin.AddThemeConstantOverride("margin_top", 12);
		bodyMargin.AddThemeConstantOverride("margin_right", 14);
		bodyMargin.AddThemeConstantOverride("margin_bottom", 14);
		bodyMargin.AddChild(body);
		scroll.AddChild(bodyMargin);

		body.AddChild(BuildMuteRow());
		body.AddChild(BuildVolumeRow(
			AudioChannel.Master,
			"MASTER",
			"Overall loudness. Every other slider sits under this."));
		body.AddChild(BuildVolumeRow(
			AudioChannel.Music,
			"MUSIC",
			"Soundtrack that plays across the clubhouse and district screens."));
		body.AddChild(BuildVolumeRow(
			AudioChannel.Ui,
			"SYSTEM SOUNDS",
			"Clicks, hovers, navigation, and confirmations throughout the app.",
			"Preview",
			() => AudioManager.Current.PlayUi(UiSound.Select)));
		body.AddChild(BuildVolumeRow(
			AudioChannel.Sfx,
			"GAMEPLAY",
			"Hits, crowd, and umpire calls during simulated games.",
			"Preview",
			() => AudioManager.Current.PlaySfx(SfxId.BatHit)));
		body.AddChild(BuildVolumeRow(
			AudioChannel.Ambience,
			"AMBIENCE",
			"Stadium and locker-room beds under the menus."));
		body.AddChild(BuildNowPlayingCard());
		body.AddChild(BuildResetRow());
		return card;
	}

	private Control BuildMuteRow()
	{
		var row = MakeInnerCard();
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 12);
		row.AddChild(columns);

		var copy = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		copy.AddThemeConstantOverride("separation", -2);
		copy.AddChild(MakeText("MUTE ALL", _semibold, 14, TextPrimary, HorizontalAlignment.Left));
		copy.AddChild(MakeText("Silence every bus without changing the sliders.", _medium, 11, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(copy);

		_muteButton = MakeActionButton("MUTE", () =>
		{
			AudioManager.Current.Muted = !AudioManager.Current.Muted;
			GameSettings.SaveAudio(AudioManager.Current);
			RefreshMute();
		});
		_muteButton.CustomMinimumSize = new Vector2(92, 32);
		columns.AddChild(_muteButton);
		return row;
	}

	private Control BuildVolumeRow(
		AudioChannel channel,
		string title,
		string hint,
		string? previewLabel = null,
		Action? preview = null)
	{
		var row = MakeInnerCard();
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		row.AddChild(layout);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		header.AddChild(MakeText(title, _semibold, 14, TextPrimary, HorizontalAlignment.Left));

		var percent = MakeText("0%", _bold, 16, Accent, HorizontalAlignment.Right);
		percent.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(percent);
		_percents[channel] = percent;

		if (preview != null && previewLabel != null)
		{
			Button previewButton = MakeActionButton(previewLabel, preview);
			previewButton.CustomMinimumSize = new Vector2(78, 26);
			header.AddChild(previewButton);
		}

		layout.AddChild(header);
		layout.AddChild(MakeText(hint, _medium, 11, TextMuted, HorizontalAlignment.Left));

		HSlider slider = MakeVolumeSlider();
		slider.ValueChanged += value => OnVolumeChanged(channel, value);
		_sliders[channel] = slider;
		layout.AddChild(slider);
		return row;
	}

	private HSlider MakeVolumeSlider()
	{
		var slider = new HSlider
		{
			MinValue = 0,
			MaxValue = 100,
			Step = 1,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 18),
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		slider.AddThemeStyleboxOverride("slider", new StyleBoxFlat
		{
			BgColor = TrackBg,
			ContentMarginTop = 5,
			ContentMarginBottom = 5,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		slider.AddThemeStyleboxOverride("grabber_area", new StyleBoxFlat
		{
			BgColor = Accent,
			ContentMarginTop = 5,
			ContentMarginBottom = 5,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		slider.AddThemeStyleboxOverride("grabber_area_highlight", new StyleBoxFlat
		{
			BgColor = AccentHover,
			ContentMarginTop = 5,
			ContentMarginBottom = 5,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		slider.AddThemeIconOverride("grabber", _grabber);
		slider.AddThemeIconOverride("grabber_highlight", _grabberHover);
		return slider;
	}

	private Control BuildNowPlayingCard()
	{
		var row = MakeInnerCard();
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		row.AddChild(layout);

		layout.AddChild(MakeText("NOW PLAYING", _semibold, 11, Accent, HorizontalAlignment.Left));
		_trackTitle = MakeText("No soundtrack loaded", _bold, 18, TextPrimary, HorizontalAlignment.Left);
		layout.AddChild(_trackTitle);
		_trackMeta = MakeText("Drop track_*.ogg files into assets/audio/music to start the playlist.", _medium, 11, TextMuted, HorizontalAlignment.Left);
		layout.AddChild(_trackMeta);

		var transport = new HBoxContainer();
		transport.AddThemeConstantOverride("separation", 6);
		transport.AddChild(MakeActionButton("PREV", () => AudioManager.Current.PreviousTrack()));
		_pauseButton = MakeActionButton("PAUSE", () =>
		{
			AudioManager.Current.TogglePauseMusic();
			RefreshNowPlaying();
		});
		transport.AddChild(_pauseButton);
		transport.AddChild(MakeActionButton("NEXT", () => AudioManager.Current.NextTrack()));
		_shuffleButton = MakeActionButton("SHUFFLE", () =>
		{
			AudioManager.Current.ToggleShuffle();
			GameSettings.SaveAudio(AudioManager.Current);
			RefreshNowPlaying();
		});
		transport.AddChild(_shuffleButton);
		layout.AddChild(transport);
		return row;
	}

	private Control BuildResetRow()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		var copy = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		copy.AddThemeConstantOverride("separation", -2);
		copy.AddChild(MakeText("RESTORE AUDIO DEFAULTS", _semibold, 13, TextPrimary, HorizontalAlignment.Left));
		copy.AddChild(MakeText("Master 100%  ·  Music 65%  ·  System 55%  ·  Gameplay 80%  ·  Ambience 45%", _medium, 11, TextMuted, HorizontalAlignment.Left));
		row.AddChild(copy);

		Button reset = MakeActionButton("RESET", ResetAudioDefaults);
		reset.CustomMinimumSize = new Vector2(92, 32);
		row.AddChild(reset);
		return row;
	}

	private Control BuildGameplayPanel()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(0));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);
		layout.AddChild(BuildPanelHeading(
			"GAMEPLAY",
			"Autosave after simulation events, and whether day advances show a recap."));

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);

		var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 10);
		var bodyMargin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		bodyMargin.AddThemeConstantOverride("margin_left", 14);
		bodyMargin.AddThemeConstantOverride("margin_top", 12);
		bodyMargin.AddThemeConstantOverride("margin_right", 14);
		bodyMargin.AddThemeConstantOverride("margin_bottom", 14);
		bodyMargin.AddChild(body);
		scroll.AddChild(bodyMargin);

		body.AddChild(BuildAutosaveMasterRow());
		body.AddChild(BuildAutosaveTriggerRow(
			"game",
			"AFTER A GAME",
			"Lock the seat when a simulated contest finishes."));
		body.AddChild(BuildAutosaveTriggerRow(
			"transaction",
			"AFTER A MAJOR TRANSACTION",
			"Trades, releases, and open-pool signings."));
		body.AddChild(BuildAutosaveTriggerRow(
			"week",
			"AFTER A WEEK ADVANCE",
			"When Simulate Week moves the calendar."));
		body.AddChild(BuildAutosaveTriggerRow(
			"postseason",
			"AFTER A POSTSEASON ROUND",
			"Playoff games and championship acknowledgements."));
		body.AddChild(BuildAutosaveTriggerRow(
			"offseason",
			"AFTER THE OFFSEASON TRANSITION",
			"When board review closes the season."));
		body.AddChild(BuildSimRecapRow());
		return card;
	}

	private Control BuildSimRecapRow()
	{
		var row = MakeInnerCard();
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 12);
		row.AddChild(columns);

		var copy = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		copy.AddThemeConstantOverride("separation", -2);
		copy.AddChild(MakeText("SIMULATION RECAP", _semibold, 14, TextPrimary, HorizontalAlignment.Left));
		copy.AddChild(MakeText(
			"After Simulate Day or Week, list the games, injuries, and signings that just posted.",
			_medium,
			11,
			TextMuted,
			HorizontalAlignment.Left));
		columns.AddChild(copy);

		_simRecapToggle = MakeActionButton("ON", ToggleSimulationRecap);
		_simRecapToggle.CustomMinimumSize = new Vector2(92, 32);
		columns.AddChild(_simRecapToggle);
		return row;
	}

	private void ToggleSimulationRecap()
	{
		GameSettings.ShowSimulationRecap = !GameSettings.ShowSimulationRecap;
		RefreshGameplay();
	}

	private Control BuildAutosaveMasterRow()
	{
		var row = MakeInnerCard();
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 12);
		row.AddChild(columns);

		var copy = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		copy.AddThemeConstantOverride("separation", -2);
		copy.AddChild(MakeText("AUTOSAVE", _semibold, 14, TextPrimary, HorizontalAlignment.Left));
		copy.AddChild(MakeText(
			"Write the active career after the events below. Manual Save Career still always works.",
			_medium,
			11,
			TextMuted,
			HorizontalAlignment.Left));
		columns.AddChild(copy);

		_autosaveMaster = MakeActionButton("ON", ToggleAutosaveMaster);
		_autosaveMaster.CustomMinimumSize = new Vector2(92, 32);
		columns.AddChild(_autosaveMaster);
		return row;
	}

	private Control BuildAutosaveTriggerRow(string key, string title, string hint)
	{
		var row = MakeInnerCard();
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 12);
		row.AddChild(columns);

		var copy = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		copy.AddThemeConstantOverride("separation", -2);
		copy.AddChild(MakeText(title, _semibold, 14, TextPrimary, HorizontalAlignment.Left));
		copy.AddChild(MakeText(hint, _medium, 11, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(copy);

		Button button = MakeActionButton("ON", () => ToggleAutosaveTrigger(key));
		button.CustomMinimumSize = new Vector2(92, 32);
		_autosaveToggles[key] = button;
		columns.AddChild(button);
		return row;
	}

	private void ToggleAutosaveMaster()
	{
		GameSettings.Autosave.Enabled = !GameSettings.Autosave.Enabled;
		GameSettings.SaveAutosave();
		RefreshGameplay();
	}

	private void ToggleAutosaveTrigger(string key)
	{
		AutosavePreferences prefs = GameSettings.Autosave;
		switch (key)
		{
			case "game":
				prefs.AfterGame = !prefs.AfterGame;
				break;
			case "transaction":
				prefs.AfterMajorTransaction = !prefs.AfterMajorTransaction;
				break;
			case "week":
				prefs.AfterWeekAdvance = !prefs.AfterWeekAdvance;
				break;
			case "postseason":
				prefs.AfterPostseasonRound = !prefs.AfterPostseasonRound;
				break;
			case "offseason":
				prefs.AfterOffseasonTransition = !prefs.AfterOffseasonTransition;
				break;
		}

		GameSettings.SaveAutosave();
		RefreshGameplay();
	}

	private void RefreshGameplay()
	{
		AutosavePreferences prefs = GameSettings.Autosave;
		_autosaveMaster.Text = prefs.Enabled ? "ON" : "OFF";
		ApplyToggleStyle(_autosaveMaster, prefs.Enabled);
		SetAutosaveToggle("game", prefs.AfterGame);
		SetAutosaveToggle("transaction", prefs.AfterMajorTransaction);
		SetAutosaveToggle("week", prefs.AfterWeekAdvance);
		SetAutosaveToggle("postseason", prefs.AfterPostseasonRound);
		SetAutosaveToggle("offseason", prefs.AfterOffseasonTransition);
		if (_simRecapToggle != null)
		{
			_simRecapToggle.Text = GameSettings.ShowSimulationRecap ? "ON" : "OFF";
			ApplyToggleStyle(_simRecapToggle, GameSettings.ShowSimulationRecap);
		}
	}

	private void SetAutosaveToggle(string key, bool on)
	{
		if (!_autosaveToggles.TryGetValue(key, out Button? button))
		{
			return;
		}

		button.Text = on ? "ON" : "OFF";
		ApplyToggleStyle(button, on);
	}

	private Control BuildVisualPanel()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(0));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);
		layout.AddChild(BuildPanelHeading(
			"VISUAL",
			"Window mode, resolution, and how the clubhouse fills the monitor."));

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);

		var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 10);
		var bodyMargin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		bodyMargin.AddThemeConstantOverride("margin_left", 14);
		bodyMargin.AddThemeConstantOverride("margin_top", 12);
		bodyMargin.AddThemeConstantOverride("margin_right", 14);
		bodyMargin.AddThemeConstantOverride("margin_bottom", 14);
		bodyMargin.AddChild(body);
		scroll.AddChild(bodyMargin);

		_windowModeChoice = MakeChoice();
		_windowModeChoice.AddItem("Windowed", (int)DisplayWindowMode.Windowed);
		_windowModeChoice.AddItem("Borderless Fullscreen", (int)DisplayWindowMode.Borderless);
		_windowModeChoice.AddItem("Exclusive Fullscreen", (int)DisplayWindowMode.Exclusive);
		_windowModeChoice.ItemSelected += OnWindowModeSelected;
		body.AddChild(BuildChoiceRow(
			"WINDOW MODE",
			"Borderless uses the whole monitor. Exclusive can change the display mode.",
			_windowModeChoice));

		_resolutionChoice = MakeChoice();
		_resolutionChoice.ItemSelected += OnResolutionSelected;
		body.AddChild(BuildChoiceRow(
			"RESOLUTION",
			"Windowed and exclusive fullscreen. Borderless always uses the native monitor.",
			_resolutionChoice));
		_resolutionHint = MakeText("", _medium, 11, TextMuted, HorizontalAlignment.Left);
		_resolutionHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		body.AddChild(_resolutionHint);

		_scaleChoice = MakeChoice();
		_scaleChoice.AddItem("Fill without stretching", (int)DisplayScaleMode.Expand);
		_scaleChoice.AddItem("Stretch to Fit", (int)DisplayScaleMode.Stretch);
		_scaleChoice.ItemSelected += OnScaleModeSelected;
		body.AddChild(BuildChoiceRow(
			"SCALING",
			"Fill keeps proportions and uses extra ultrawide space. Stretch fills the monitor and may look a bit wider.",
			_scaleChoice));
		body.AddChild(BuildUiScaleRow());
		return card;
	}

	private Control BuildUiScaleRow()
	{
		var row = MakeInnerCard();
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		row.AddChild(layout);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		header.AddChild(MakeText("UI SCALE", _semibold, 14, TextPrimary, HorizontalAlignment.Left));
		_uiScalePercent = MakeText("100%", _bold, 16, Accent, HorizontalAlignment.Right);
		_uiScalePercent.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(_uiScalePercent);
		layout.AddChild(header);
		layout.AddChild(MakeText(
			"Size of menus and type. 100% is the original clubhouse scale.",
			_medium,
			11,
			TextMuted,
			HorizontalAlignment.Left));

		_uiScaleSlider = MakeVolumeSlider();
		_uiScaleSlider.MinValue = 85;
		_uiScaleSlider.MaxValue = 125;
		_uiScaleSlider.Step = 5;
		_uiScaleSlider.ValueChanged += OnUiScaleChanged;
		layout.AddChild(_uiScaleSlider);
		return row;
	}

	private Control BuildChoiceRow(string title, string hint, OptionButton choice)
	{
		var row = MakeInnerCard();
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 12);
		row.AddChild(columns);

		var copy = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		copy.AddThemeConstantOverride("separation", -2);
		copy.AddChild(MakeText(title, _semibold, 14, TextPrimary, HorizontalAlignment.Left));
		var hintLabel = MakeText(hint, _medium, 11, TextMuted, HorizontalAlignment.Left);
		hintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		copy.AddChild(hintLabel);
		columns.AddChild(copy);

		choice.CustomMinimumSize = new Vector2(220, 32);
		choice.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		columns.AddChild(choice);
		return row;
	}

	private OptionButton MakeChoice()
	{
		var button = new OptionButton
		{
			Flat = false,
			CustomMinimumSize = new Vector2(220, 32),
			MouseDefaultCursorShape = CursorShape.PointingHand,
			FitToLongestItem = false,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 13);
		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeColorOverride("font_pressed_color", TextPrimary);
		button.AddThemeColorOverride("font_focus_color", Colors.White);
		button.AddThemeStyleboxOverride("normal", MakePill(TrackBg, 10, 6));
		button.AddThemeStyleboxOverride("hover", MakePill(HoverBg, 10, 6));
		button.AddThemeStyleboxOverride("pressed", MakePill(HoverBg, 10, 6));
		button.AddThemeStyleboxOverride("focus", MakePill(HoverBg, 10, 6, 1));
		return button;
	}

	private void OnWindowModeSelected(long index)
	{
		if (_syncing)
		{
			return;
		}

		GameSettings.WindowMode = (DisplayWindowMode)_windowModeChoice.GetItemId((int)index);
		RefreshVisual();
	}

	private void OnResolutionSelected(long index)
	{
		if (_syncing)
		{
			return;
		}

		GameSettings.Resolution = _resolutionChoice.GetItemMetadata((int)index).AsVector2I();
		RefreshVisual();
	}

	private void OnScaleModeSelected(long index)
	{
		if (_syncing)
		{
			return;
		}

		GameSettings.ScaleMode = (DisplayScaleMode)_scaleChoice.GetItemId((int)index);
	}

	private void OnUiScaleChanged(double value)
	{
		if (_uiScalePercent != null)
		{
			_uiScalePercent.Text = $"{value:0}%";
		}

		if (_syncing)
		{
			return;
		}

		GameSettings.UiScale = (float)value / 100f;
	}

	private void RefreshVisual()
	{
		if (_windowModeChoice == null)
		{
			return;
		}

		_syncing = true;
		SelectChoiceId(_windowModeChoice, (int)GameSettings.WindowMode);
		SelectChoiceId(_scaleChoice, (int)(GameSettings.ScaleMode == DisplayScaleMode.Stretch
			? DisplayScaleMode.Stretch
			: DisplayScaleMode.Expand));
		RebuildResolutionChoices();
		bool locked = GameSettings.ResolutionLocked;
		_resolutionChoice.Disabled = locked;
		_resolutionHint.Text = locked
			? "Borderless fullscreen stays on the native monitor size."
			: "Pick a size that fits this monitor. Scaling and UI scale control how large the clubhouse looks.";
		if (_uiScaleSlider != null)
		{
			_uiScaleSlider.Value = Mathf.Round(GameSettings.UiScale * 100f);
			_uiScalePercent.Text = $"{_uiScaleSlider.Value:0}%";
		}

		_syncing = false;
	}

	private void RebuildResolutionChoices()
	{
		_resolutionChoice.Clear();
		IReadOnlyList<Vector2I> sizes = GameSettings.AvailableResolutions();
		Vector2I current = GameSettings.Resolution;
		int selected = 0;
		for (int i = 0; i < sizes.Count; i++)
		{
			Vector2I size = sizes[i];
			_resolutionChoice.AddItem(GameSettings.ResolutionLabel(size));
			_resolutionChoice.SetItemMetadata(i, size);
			if (size == current)
			{
				selected = i;
			}
		}

		if (!ContainsResolution(sizes, current))
		{
			int extra = sizes.Count;
			_resolutionChoice.AddItem(GameSettings.ResolutionLabel(current));
			_resolutionChoice.SetItemMetadata(extra, current);
			selected = extra;
		}

		_resolutionChoice.Selected = selected;
	}

	private static bool ContainsResolution(IReadOnlyList<Vector2I> sizes, Vector2I current)
	{
		foreach (Vector2I size in sizes)
		{
			if (size == current)
			{
				return true;
			}
		}

		return false;
	}

	private static void SelectChoiceId(OptionButton button, int id)
	{
		for (int i = 0; i < button.ItemCount; i++)
		{
			if (button.GetItemId(i) == id)
			{
				button.Selected = i;
				return;
			}
		}
	}

	private Control BuildComingSoonPanel(string title, string body)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(0));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);
		layout.AddChild(BuildPanelHeading(title, body));

		var inner = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		inner.AddThemeConstantOverride("separation", 8);
		var margin = new MarginContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		margin.AddChild(inner);
		layout.AddChild(margin);

		inner.AddChild(BuildLockedRow("These options are outlined, not wired yet."));
		inner.AddChild(BuildLockedRow("Open Audio to change volumes for this install."));
		return card;
	}

	private Control BuildPanelHeading(string title, string hint)
	{
		var heading = new PanelContainer();
		heading.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047059f, 0.054902f, 0.070588f, 0.96f),
			ContentMarginLeft = 14,
			ContentMarginTop = 10,
			ContentMarginRight = 14,
			ContentMarginBottom = 10,
		});

		var copy = new VBoxContainer();
		copy.AddThemeConstantOverride("separation", -2);
		copy.AddChild(MakeText(title, _bold, 16, TextPrimary, HorizontalAlignment.Left));
		var hintLabel = MakeText(hint, _medium, 11, TextMuted, HorizontalAlignment.Left);
		hintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		copy.AddChild(hintLabel);
		heading.AddChild(copy);

		var wrap = new VBoxContainer();
		wrap.AddThemeConstantOverride("separation", 0);
		wrap.AddChild(heading);
		wrap.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		return wrap;
	}

	private Control BuildLockedRow(string text)
	{
		var row = MakeInnerCard();
		row.AddChild(MakeText(text, _medium, 12, TextMuted, HorizontalAlignment.Left));
		return row;
	}

	private void ShowMenu(SettingsMenu menu)
	{
		_menu = menu;
		foreach (KeyValuePair<SettingsMenu, Control> pair in _panels)
		{
			pair.Value.Visible = pair.Key == menu;
		}

		foreach (KeyValuePair<SettingsMenu, Button> pair in _menuButtons)
		{
			bool active = pair.Key == menu;
			Button button = pair.Value;
			button.AddThemeColorOverride("font_color", active ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("font_hover_color", active ? TextOnAccent : Colors.White);
			button.AddThemeColorOverride("font_pressed_color", active ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("font_focus_color", active ? TextOnAccent : Colors.White);
			button.AddThemeStyleboxOverride("normal", MakeMenuStyle(active ? Accent : Colors.Transparent, 0));
			button.AddThemeStyleboxOverride("hover", MakeMenuStyle(active ? AccentHover : HoverBg, 0));
			button.AddThemeStyleboxOverride("pressed", MakeMenuStyle(active ? AccentHover : HoverBg, 0));
			button.AddThemeStyleboxOverride("focus", MakeMenuStyle(active ? AccentHover : HoverBg, 1));
		}

		if (menu == SettingsMenu.Audio)
		{
			RefreshAudio();
		}

		if (menu == SettingsMenu.FileManagement)
		{
			_fileManagement.Refresh();
		}

		if (menu == SettingsMenu.Gameplay)
		{
			RefreshGameplay();
		}

		if (menu == SettingsMenu.Visual)
		{
			RefreshVisual();
		}
	}

	private void OnVolumeChanged(AudioChannel channel, double value)
	{
		if (_percents.TryGetValue(channel, out Label? percent))
		{
			percent.Text = $"{value:0}%";
		}

		if (_syncing)
		{
			return;
		}

		AudioManager.Current.SetVolume(channel, (float)value / 100f);
		GameSettings.SaveAudio(AudioManager.Current);
	}

	private void RefreshAudio()
	{
		AudioManager audio = AudioManager.Current;
		_syncing = true;
		SetSlider(AudioChannel.Master, audio.MasterVolume);
		SetSlider(AudioChannel.Music, audio.MusicVolume);
		SetSlider(AudioChannel.Ui, audio.UiVolume);
		SetSlider(AudioChannel.Sfx, audio.SfxVolume);
		SetSlider(AudioChannel.Ambience, audio.AmbienceVolume);
		_syncing = false;
		RefreshMute();
		RefreshNowPlaying();
	}

	private void SetSlider(AudioChannel channel, float linear)
	{
		if (!_sliders.TryGetValue(channel, out HSlider? slider))
		{
			return;
		}

		slider.Value = Mathf.Round(Mathf.Clamp(linear, 0f, 1f) * 100f);
		if (_percents.TryGetValue(channel, out Label? percent))
		{
			percent.Text = $"{slider.Value:0}%";
		}
	}

	private void RefreshMute()
	{
		bool muted = AudioManager.Current.Muted;
		_muteButton.Text = muted ? "UNMUTE" : "MUTE";
		ApplyToggleStyle(_muteButton, muted);
	}

	private void RefreshNowPlaying()
	{
		AudioManager audio = AudioManager.Current;
		bool hasTrack = !string.IsNullOrEmpty(audio.CurrentTrackTitle);
		_trackTitle.Text = hasTrack ? audio.CurrentTrackTitle.ToUpperInvariant() : "NO SOUNDTRACK LOADED";
		_trackMeta.Text = hasTrack
			? $"Track {audio.CurrentTrackIndex + 1} of {audio.TrackCount}  ·  {(audio.IsMusicPaused ? "Paused" : audio.IsMusicPlaying ? "Playing" : "Stopped")}"
			: "Drop track_*.ogg files into assets/audio/music to start the playlist.";
		_pauseButton.Text = audio.IsMusicPaused || !audio.IsMusicPlaying ? "PLAY" : "PAUSE";
		ApplyToggleStyle(_shuffleButton, audio.ShuffleEnabled);
	}

	private void ResetAudioDefaults()
	{
		AudioManager audio = AudioManager.Current;
		audio.MasterVolume = 1f;
		audio.MusicVolume = 0.65f;
		audio.AmbienceVolume = 0.45f;
		audio.UiVolume = 0.55f;
		audio.SfxVolume = 0.8f;
		audio.Muted = false;
		if (audio.ShuffleEnabled)
		{
			audio.ToggleShuffle();
		}

		GameSettings.SaveAudio(audio);
		RefreshAudio();
	}

	private Button MakeActionButton(string text, Action pressed)
	{
		var button = new Button
		{
			Text = text,
			Flat = false,
			CustomMinimumSize = new Vector2(72, 30),
			MouseDefaultCursorShape = CursorShape.PointingHand,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeStyleboxOverride("normal", MakePill(TrackBg, 10, 4));
		button.AddThemeStyleboxOverride("hover", MakePill(HoverBg, 10, 4));
		button.AddThemeStyleboxOverride("pressed", MakePill(HoverBg, 10, 4));
		button.AddThemeStyleboxOverride("focus", MakePill(HoverBg, 10, 4, 1));
		button.Pressed += pressed;
		return button;
	}

	private void ApplyToggleStyle(Button button, bool active)
	{
		button.AddThemeColorOverride("font_color", active ? TextOnAccent : TextPrimary);
		button.AddThemeColorOverride("font_hover_color", active ? TextOnAccent : Colors.White);
		button.AddThemeColorOverride("font_pressed_color", active ? TextOnAccent : TextPrimary);
		button.AddThemeStyleboxOverride("normal", MakePill(active ? Accent : TrackBg, 10, 4));
		button.AddThemeStyleboxOverride("hover", MakePill(active ? AccentHover : HoverBg, 10, 4));
		button.AddThemeStyleboxOverride("pressed", MakePill(active ? AccentHover : HoverBg, 10, 4));
		button.AddThemeStyleboxOverride("focus", MakePill(active ? AccentHover : HoverBg, 10, 4, 1));
	}

	private static PanelContainer MakeInnerCard()
	{
		var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = Hairline,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		});
		return card;
	}

	private static StyleBoxFlat MakeCardStyle(float pad)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.078431f, 0.094118f, 0.94f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = pad,
			ContentMarginTop = pad,
			ContentMarginRight = pad,
			ContentMarginBottom = pad,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		};
	}

	private static StyleBoxFlat MakeMenuStyle(Color bg, int border)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			DrawCenter = true,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
			BorderWidthLeft = border,
			BorderWidthTop = border,
			BorderWidthRight = border,
			BorderWidthBottom = border,
			BorderColor = new Color(0.988235f, 0.843137f, 0.450980f, 1f),
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerDetail = 4,
		};
	}

	private static StyleBoxFlat MakePill(Color bg, float marginX, float marginY, int border = 0)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			DrawCenter = true,
			ContentMarginLeft = marginX,
			ContentMarginTop = marginY,
			ContentMarginRight = marginX,
			ContentMarginBottom = marginY,
			BorderWidthLeft = border,
			BorderWidthTop = border,
			BorderWidthRight = border,
			BorderWidthBottom = border,
			BorderColor = new Color(0.988235f, 0.843137f, 0.450980f, 1f),
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
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

	private static Texture2D MakeGrabber(Color color, int size)
	{
		var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
		image.Fill(Colors.Transparent);
		Vector2 center = new(size / 2f, size / 2f);
		float radius = size / 2f - 0.5f;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				if (new Vector2(x + 0.5f, y + 0.5f).DistanceTo(center) <= radius)
				{
					image.SetPixel(x, y, color);
				}
			}
		}

		return ImageTexture.CreateFromImage(image);
	}
}
