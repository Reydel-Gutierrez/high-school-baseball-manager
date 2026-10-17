using Godot;
using HSBM.Domain;

/// <summary>
/// Evaluation recap after a tryout scrimmage. Not the visual gameplay engine.
/// View box score reuses the regular clubhouse sheet.
/// </summary>
public partial class TryoutGameRecapOverlay : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.96f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color BlueJersey = new(0.22f, 0.42f, 0.78f, 1f);
	private static readonly Color WhiteJersey = new(0.88f, 0.90f, 0.93f, 1f);

	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Label _title = null!;
	private Label _blueName = null!;
	private Label _blueScore = null!;
	private Label _whiteName = null!;
	private Label _whiteScore = null!;
	private VBoxContainer _standouts = null!;
	private TryoutGame? _game;
	private bool _built;

	public event System.Action<TryoutGame>? ViewBoxScoreRequested;

	public override void _Ready()
	{
		EnsureBuilt();
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (!Visible || !ev.IsActionPressed("ui_cancel"))
		{
			return;
		}

		UiSounds.Play(UiSound.Back);
		Close();
		GetViewport().SetInputAsHandled();
	}

	public void Open(TryoutGame game)
	{
		EnsureBuilt();
		_game = game;
		_title.Text = game.Title;
		_blueName.Text = TryoutGame.BlueName.ToUpperInvariant();
		_whiteName.Text = TryoutGame.WhiteName.ToUpperInvariant();
		_blueScore.Text = game.BlueRuns.ToString();
		_whiteScore.Text = game.WhiteRuns.ToString();

		foreach (Node child in _standouts.GetChildren())
		{
			child.QueueFree();
		}

		foreach (TryoutStandout standout in game.Standouts)
		{
			_standouts.AddChild(BuildStandout(standout));
		}

		Visible = true;
		MoveToFront();
	}

	public void Close()
	{
		Visible = false;
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
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var dim = new ColorRect
		{
			Color = new Color(0.02f, 0.03f, 0.04f, 0.78f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var center = new CenterContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(420, 0),
			MouseFilter = MouseFilterEnum.Stop,
		};
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.078431f, 0.094118f, 0.98f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 22,
			ContentMarginTop = 18,
			ContentMarginRight = 22,
			ContentMarginBottom = 18,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
		});
		center.AddChild(panel);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 12);
		panel.AddChild(layout);

		_title = MakeText("TRYOUT GAME 1 OF 3", _bold, 22, TextPrimary, HorizontalAlignment.Center);
		layout.AddChild(_title);
		layout.AddChild(new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 2),
			Color = Accent,
		});

		layout.AddChild(BuildScoreRow(TryoutGame.BlueName, out _blueName, out _blueScore, BlueJersey, Colors.White));
		layout.AddChild(BuildScoreRow(TryoutGame.WhiteName, out _whiteName, out _whiteScore, WhiteJersey, new Color(0.12f, 0.14f, 0.18f)));

		layout.AddChild(MakeText("TOP PERFORMERS", _semibold, 11, Accent, HorizontalAlignment.Left));
		_standouts = new VBoxContainer();
		_standouts.AddThemeConstantOverride("separation", 8);
		layout.AddChild(_standouts);

		var actions = new VBoxContainer();
		actions.AddThemeConstantOverride("separation", 8);
		actions.AddChild(MakeAction("VIEW BOX SCORE", false, OnViewBoxScore));
		actions.AddChild(MakeAction("CONTINUE", true, Close));
		layout.AddChild(actions);
	}

	private Control BuildScoreRow(string name, out Label nameLabel, out Label scoreLabel, Color mark, Color markText)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		var chip = new PanelContainer
		{
			CustomMinimumSize = new Vector2(36, 36),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = mark,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
		});
		var initial = MakeText(name.Contains("BLUE", System.StringComparison.OrdinalIgnoreCase) ? "B" : "W", _bold, 16, markText, HorizontalAlignment.Center);
		initial.VerticalAlignment = VerticalAlignment.Center;
		chip.AddChild(initial);
		row.AddChild(chip);

		nameLabel = MakeText(name.ToUpperInvariant(), _semibold, 18, TextPrimary, HorizontalAlignment.Left);
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(nameLabel);

		scoreLabel = MakeText("0", _bold, 32, TextPrimary, HorizontalAlignment.Right);
		scoreLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(scoreLabel);
		return row;
	}

	private Control BuildStandout(TryoutStandout standout)
	{
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = CardBorder,
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
		});
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", -2);
		box.AddChild(MakeText(standout.Name, _semibold, 16, TextPrimary, HorizontalAlignment.Left));
		box.AddChild(MakeText(standout.Line, _medium, 13, TextMuted, HorizontalAlignment.Left));
		card.AddChild(box);
		return card;
	}

	private void OnViewBoxScore()
	{
		if (_game != null)
		{
			ViewBoxScoreRequested?.Invoke(_game);
		}
	}

	private Button MakeAction(string text, bool primary, System.Action pressed)
	{
		var button = new Button
		{
			Text = text,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 36),
		};
		button.AddThemeFontOverride("font", _bold);
		button.AddThemeFontSizeOverride("font_size", 13);
		if (primary)
		{
			button.AddThemeColorOverride("font_color", TextOnAccent);
			button.AddThemeStyleboxOverride("normal", MakeFilled(Accent));
			button.AddThemeStyleboxOverride("hover", MakeFilled(AccentHover));
			button.AddThemeStyleboxOverride("pressed", MakeFilled(AccentHover));
		}
		else
		{
			button.AddThemeColorOverride("font_color", TextPrimary);
			button.AddThemeStyleboxOverride("normal", MakeOutlined(CardInner, CardBorder));
			button.AddThemeStyleboxOverride("hover", MakeOutlined(HoverBg, Accent));
			button.AddThemeStyleboxOverride("pressed", MakeOutlined(HoverBg, Accent));
		}

		button.Pressed += pressed;
		return button;
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

	private static StyleBoxFlat MakeFilled(Color bg) =>
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
		};

	private static StyleBoxFlat MakeOutlined(Color bg, Color border) =>
		new()
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 14,
			ContentMarginTop = 8,
			ContentMarginRight = 14,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		};
}
