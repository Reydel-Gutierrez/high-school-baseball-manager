using System;
using Godot;
using HSBM.Domain;

/// <summary>
/// End-of-season Hall of Fame announcement: one plaque, then the wall
/// on Career → Program Hall of Fame.
/// </summary>
public partial class HallOfFameInductionPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.772549f, 0.635294f, 0.325490f, 1f);
	private static readonly Color AccentHover = new(0.850980f, 0.729412f, 0.443137f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color CardInner = new(0.090196f, 0.105882f, 0.141176f, 1f);
	private static readonly Color CardFill = new(0.070588f, 0.082353f, 0.109804f, 1f);
	private static readonly Color CardBorder = new(0.137255f, 0.152941f, 0.200000f, 1f);
	private static readonly Color Gold = new(0.772549f, 0.635294f, 0.325490f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private VBoxContainer _body = null!;
	private bool _built;

	public event Action? Closed;
	public event Action? Continued;

	public override void _Ready()
	{
		EnsureBuilt();
		Rebuild();
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

	public void Refresh()
	{
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
		_session = GetNode<GameSession>("/root/GameSession");
		MouseFilter = MouseFilterEnum.Stop;

		var dim = new ColorRect
		{
			Color = new Color(0.02f, 0.025f, 0.04f, 0.52f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 36);
		margin.AddThemeConstantOverride("margin_top", 22);
		margin.AddThemeConstantOverride("margin_right", 36);
		margin.AddThemeConstantOverride("margin_bottom", 22);
		AddChild(margin);

		_body = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		margin.AddChild(_body);
	}

	private void Rebuild()
	{
		if (!_built)
		{
			return;
		}

		foreach (Node child in _body.GetChildren())
		{
			child.QueueFree();
		}

		ProgramHallOfFameInductee? inductee = HallOfFameDesk.ThisYear(_session) ?? HallOfFameDesk.Select(_session);
		OrganizationState? school = HallOfFameDesk.ProgramFor(_session);
		string display = school?.DisplayName.ToUpperInvariant() ?? _session.Organization.ShortName.ToUpperInvariant();
		string full = school?.Name.ToUpperInvariant() ?? _session.Organization.Name.ToUpperInvariant();

		var poster = new PanelContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(520, 0),
		};
		poster.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardFill,
			BorderColor = Gold,
			BorderWidthLeft = 3,
			BorderWidthTop = 3,
			BorderWidthRight = 3,
			BorderWidthBottom = 3,
			ContentMarginLeft = 32,
			ContentMarginTop = 26,
			ContentMarginRight = 32,
			ContentMarginBottom = 24,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10,
			ShadowColor = new Color(0f, 0f, 0f, 0.4f),
			ShadowSize = 14,
			ShadowOffset = new Vector2(0, 5),
		});
		_body.AddChild(poster);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		poster.AddChild(layout);

		layout.AddChild(MakeText($"{GameSession.SeasonYear} INDUCTION", _medium, 12, Gold, HorizontalAlignment.Center));
		layout.AddChild(MakeText(display, _bold, 28, TextPrimary, HorizontalAlignment.Center));
		layout.AddChild(MakeText("HALL OF FAME", _semibold, 18, Gold, HorizontalAlignment.Center));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Gold });

		if (inductee == null)
		{
			layout.AddChild(MakeText("No inductee this year.", _medium, 16, TextMuted, HorizontalAlignment.Center));
		}
		else
		{
			layout.AddChild(MakeText(inductee.PlayerName.ToUpperInvariant(), _bold, 32, TextPrimary, HorizontalAlignment.Center));
			layout.AddChild(MakeText(inductee.YearsLabel, _semibold, 18, new Color(0.82f, 0.85f, 0.90f), HorizontalAlignment.Center));
			layout.AddChild(MakeText(inductee.Position, _bold, 20, Accent, HorizontalAlignment.Center));
			layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = CardBorder });
			foreach (string honor in inductee.HonorLines)
			{
				layout.AddChild(MakeText(honor, _semibold, 16, TextPrimary, HorizontalAlignment.Center));
			}

			layout.AddChild(MakeText(inductee.CareerLine, _medium, 15, TextMuted, HorizontalAlignment.Center));
		}

		layout.AddChild(MakeText(full, _medium, 11, TextMuted, HorizontalAlignment.Center));

		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 12);
		layout.AddChild(actions);
		actions.AddChild(MakeAction("GO BACK", false, 0.45f, GoBack));
		actions.AddChild(MakeAction("ENTER THE HALL", true, 1.55f, Continue));
	}

	private void Continue()
	{
		UiSounds.Play(UiSound.Navigate);
		Continued?.Invoke();
	}

	private void GoBack()
	{
		UiSounds.Play(UiSound.Back);
		Closed?.Invoke();
	}

	private Button MakeAction(string text, bool primary, float stretch, Action onPressed)
	{
		var button = new Button
		{
			Text = text,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = stretch,
			CustomMinimumSize = new Vector2(0, 44),
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 15);
		if (primary)
		{
			button.AddThemeColorOverride("font_color", TextOnAccent);
			button.AddThemeColorOverride("font_hover_color", TextOnAccent);
			button.AddThemeColorOverride("font_pressed_color", TextOnAccent);
			button.AddThemeColorOverride("font_focus_color", TextOnAccent);
			button.AddThemeStyleboxOverride("normal", MakeFilled(Accent));
			button.AddThemeStyleboxOverride("hover", MakeFilled(AccentHover));
			button.AddThemeStyleboxOverride("pressed", MakeFilled(AccentHover));
			button.AddThemeStyleboxOverride("focus", MakeFilled(AccentHover));
		}
		else
		{
			button.AddThemeColorOverride("font_color", TextPrimary);
			button.AddThemeStyleboxOverride("normal", MakeOutlined(CardInner, CardBorder));
			button.AddThemeStyleboxOverride("hover", MakeOutlined(HoverBg, Accent));
			button.AddThemeStyleboxOverride("pressed", MakeOutlined(HoverBg, Accent));
			button.AddThemeStyleboxOverride("focus", MakeOutlined(HoverBg, Accent));
		}

		button.Pressed += onPressed;
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
			ContentMarginLeft = 16,
			ContentMarginTop = 10,
			ContentMarginRight = 16,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
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
			ContentMarginLeft = 16,
			ContentMarginTop = 10,
			ContentMarginRight = 16,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
		};
}
