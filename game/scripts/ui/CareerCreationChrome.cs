using System;
using HSBM.Application;
using Godot;

/// <summary>
/// Shared chrome for the new-career sequence so later stages match
/// coach creation without copying layout code.
/// </summary>
public static class CareerCreationChrome
{
	public const string MainMenuScene = "res://scenes/main_menu/main_menu.tscn";
	public const string CoachCreationScene = "res://scenes/career_creation/coach_creation.tscn";
	public const string CoachBackgroundScene = "res://scenes/career_creation/coach_background.tscn";
	public const string WorldSetupScene = "res://scenes/career_creation/world_setup.tscn";
	public const string WorldGeneratingScene = "res://scenes/career_creation/world_generating.tscn";
	public const string ClubhouseScene = "res://scenes/main/main.tscn";

	public static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	public static readonly Color TextMuted = new(0.70f, 0.74f, 0.80f, 1f);
	public static readonly Color TextDim = new(0.55f, 0.60f, 0.68f, 1f);
	public static readonly Color Accent = new(0.117647f, 0.454902f, 0.960784f, 1f);
	public static readonly Color AccentHot = new(0.22f, 0.56f, 1f, 1f);
	public static readonly Color ButtonIdle = new(0.055f, 0.078f, 0.145f, 0.78f);
	public static readonly Color ButtonHover = new(0.10f, 0.16f, 0.30f, 0.90f);
	public static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.96f);
	public static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	public static readonly Color FieldBg = new(0.04f, 0.055f, 0.10f, 0.92f);
	public static readonly Color Ink = new(0.05f, 0.07f, 0.10f, 1f);

	public static FontFile Bold() => GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");

	public static FontFile SemiBold() => GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");

	public static FontFile Medium() => GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");

	public static void MountBackground(Control host)
	{
		var image = new TextureRect
		{
			Name = "BackgroundImage",
			Texture = GD.Load<Texture2D>("res://assets/themes/mainmenu.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		image.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		host.AddChild(image);

		var dim = new ColorRect
		{
			Color = new Color(0.012f, 0.028f, 0.062f, 0.55f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		host.AddChild(dim);
	}

	public static void FadeIn(Control host)
	{
		host.Modulate = new Color(1f, 1f, 1f, 0f);
		Tween tween = host.CreateTween();
		tween.TweenProperty(host, "modulate:a", 1f, 0.18f);
	}

	public static MarginContainer PageMargin()
	{
		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 36);
		margin.AddThemeConstantOverride("margin_top", 28);
		margin.AddThemeConstantOverride("margin_right", 36);
		margin.AddThemeConstantOverride("margin_bottom", 24);
		return margin;
	}

	public static PanelContainer Card()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", CardStyle(18));
		return card;
	}

	public static Label Heading(string text, FontFile font, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = align,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	public static Control Progress(CareerCreationStep current, FontFile bold, FontFile medium)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 18);
		AddStep(row, "01", "COACH", current == CareerCreationStep.CoachIdentity, bold, medium);
		AddStep(row, "02", "BACKGROUND", current == CareerCreationStep.CoachBackground, bold, medium);
		AddStep(row, "03", "WORLD", current == CareerCreationStep.WorldGeneration, bold, medium);
		AddStep(row, "04", "JOB", current == CareerCreationStep.JobSelection, bold, medium);
		return row;
	}

	public static Button Ghost(string text, FontFile font, Action onPressed, UiClickSound click)
	{
		var button = MakeButton(text, font, 15, TextPrimary, Fill(ButtonIdle, CardBorder), Fill(ButtonHover, Accent), Fill(Accent, AccentHot), onPressed, click);
		button.CustomMinimumSize = new Vector2(128, 42);
		return button;
	}

	public static Button Primary(string text, FontFile font, Action onPressed, UiClickSound click)
	{
		var button = MakeButton(text, font, 15, Ink, Fill(Accent, AccentHot), Fill(AccentHot, AccentHot), Fill(Accent, Accent), onPressed, click);
		button.AddThemeColorOverride("font_hover_color", Ink);
		button.AddThemeColorOverride("font_pressed_color", Ink);
		button.AddThemeColorOverride("font_disabled_color", TextDim);
		button.AddThemeStyleboxOverride("disabled", Fill(new Color(0.18f, 0.22f, 0.30f, 0.8f), CardBorder));
		button.CustomMinimumSize = new Vector2(148, 42);
		return button;
	}

	public static LineEdit Field(FontFile font, string placeholder, int maxLength)
	{
		var edit = new LineEdit
		{
			PlaceholderText = placeholder,
			MaxLength = maxLength,
			CaretBlink = true,
			CustomMinimumSize = new Vector2(0, 40),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			FocusMode = Control.FocusModeEnum.All,
		};
		edit.AddThemeFontOverride("font", font);
		edit.AddThemeFontSizeOverride("font_size", 16);
		edit.AddThemeColorOverride("font_color", TextPrimary);
		edit.AddThemeColorOverride("font_placeholder_color", TextDim);
		edit.AddThemeColorOverride("caret_color", Accent);
		edit.AddThemeStyleboxOverride("normal", Fill(FieldBg, CardBorder, 12, 8));
		edit.AddThemeStyleboxOverride("focus", Fill(FieldBg, Accent, 12, 8));
		edit.AddThemeStyleboxOverride("read_only", Fill(FieldBg, CardBorder, 12, 8));
		return edit;
	}

	public static Control Stepper(string value, FontFile font, Action left, Action right)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		Button back = Ghost("<", font, left, UiClickSound.Select);
		back.CustomMinimumSize = new Vector2(42, 36);
		row.AddChild(back);
		Label label = Heading(value, font, 16, TextPrimary, HorizontalAlignment.Center);
		label.Name = "Value";
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(label);
		Button next = Ghost(">", font, right, UiClickSound.Select);
		next.CustomMinimumSize = new Vector2(42, 36);
		row.AddChild(next);
		return row;
	}

	public static void SetStepperValue(Control stepper, string value)
	{
		if (stepper.GetNodeOrNull<Label>("Value") is Label label)
		{
			label.Text = value;
		}
	}

	public static Control Flex() => new()
	{
		SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		MouseFilter = Control.MouseFilterEnum.Ignore,
	};

	public static StyleBoxFlat CardStyle(float pad) => new()
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
		CornerRadiusTopLeft = 8,
		CornerRadiusTopRight = 8,
		CornerRadiusBottomRight = 8,
		CornerRadiusBottomLeft = 8,
		CornerDetail = 6,
	};

	private static Button MakeButton(
		string text,
		FontFile font,
		int size,
		Color fontColor,
		StyleBoxFlat normal,
		StyleBoxFlat hover,
		StyleBoxFlat pressed,
		Action onPressed,
		UiClickSound click)
	{
		var button = new Button
		{
			Text = text,
			Flat = false,
			FocusMode = Control.FocusModeEnum.All,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
		};
		button.AddThemeFontOverride("font", font);
		button.AddThemeFontSizeOverride("font_size", size);
		button.AddThemeColorOverride("font_color", fontColor);
		button.AddThemeStyleboxOverride("normal", normal);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("pressed", pressed);
		button.AddThemeStyleboxOverride("focus", hover);
		UiSounds.SetClick(button, click);
		button.Pressed += onPressed;
		return button;
	}

	private static void AddStep(HBoxContainer row, string number, string name, bool active, FontFile bold, FontFile medium)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", -2);
		col.AddChild(Heading(number, bold, active ? 16 : 13, active ? AccentHot : TextDim));
		col.AddChild(Heading(name, medium, 11, active ? TextPrimary : TextDim));
		row.AddChild(col);
		if (name == "JOB")
		{
			return;
		}

		row.AddChild(new ColorRect
		{
			CustomMinimumSize = new Vector2(36, 2),
			Color = active ? Accent : new Color(0.20f, 0.26f, 0.36f, 0.8f),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		});
	}

	private static StyleBoxFlat Fill(Color fill, Color border, float padX = 14, float padY = 8) => new()
	{
		BgColor = fill,
		BorderColor = border,
		ContentMarginLeft = padX,
		ContentMarginTop = padY,
		ContentMarginRight = padX,
		ContentMarginBottom = padY,
		BorderWidthLeft = 1,
		BorderWidthTop = 1,
		BorderWidthRight = 1,
		BorderWidthBottom = 1,
		CornerRadiusTopLeft = 8,
		CornerRadiusTopRight = 8,
		CornerRadiusBottomRight = 8,
		CornerRadiusBottomLeft = 8,
		CornerDetail = 6,
	};
}
