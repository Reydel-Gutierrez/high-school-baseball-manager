using Godot;

/// <summary>
/// Design tokens and building blocks for the redesigned UI (see
/// docs/ui-concepts): one dark palette, a gold accent, Barlow Condensed in
/// three weights, rounded cards, pills, KPI tiles and a page header with
/// crest, kicker, title and subtitle. Pages should build from these so the
/// whole game reads as one system.
/// </summary>
public static class UiKit
{
	public static readonly Color Bg = new("#0b0d12");
	public static readonly Color RailBg = new("#08090d");
	public static readonly Color Card = new("#12151c");
	public static readonly Color CardRaised = new("#171b24");
	public static readonly Color Line = new("#232733");
	public static readonly Color Text = new("#f2f3f5");
	public static readonly Color Muted = new("#8a909c");
	public static readonly Color Gold = new("#C5A253");
	public static readonly Color GoldInk = new("#16120a");
	public static readonly Color Green = new("#3ddc73");
	public static readonly Color Red = new("#e74c3c");
	public static readonly Color Selected = new("#1b1f29");

	private static FontFile? _bold;
	private static FontFile? _semibold;
	private static FontFile? _medium;

	public static FontFile Bold => _bold ??= GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");

	public static FontFile Semibold => _semibold ??= GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");

	public static FontFile Medium => _medium ??= GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");

	public static Label Text_(string text, FontFile font, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
	{
		var label = new Label { Text = text, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	/// <summary>Small caps label above a title ("TEAM · VARSITY").</summary>
	public static Label Kicker(string text, Color? color = null) => Text_(text, Semibold, 12, color ?? Gold);

	public static StyleBoxFlat Box(Color bg, int radius = 14, float padX = 0, float padY = 0, Color? border = null, int borderWidth = 0)
	{
		var box = new StyleBoxFlat
		{
			BgColor = bg,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerRadiusBottomRight = radius,
			ContentMarginLeft = padX,
			ContentMarginRight = padX,
			ContentMarginTop = padY,
			ContentMarginBottom = padY,
			CornerDetail = 6,
		};
		if (border is { } b && borderWidth > 0)
		{
			box.BorderColor = b;
			box.BorderWidthLeft = borderWidth;
			box.BorderWidthTop = borderWidth;
			box.BorderWidthRight = borderWidth;
			box.BorderWidthBottom = borderWidth;
		}

		return box;
	}

	/// <summary>Standard content card: dark panel, hairline border, 16px radius.</summary>
	public static PanelContainer MakeCard(float padX = 22, float padY = 18)
	{
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", Box(Card, 16, padX, padY, Line, 1));
		return card;
	}

	/// <summary>Card heading row: muted caps title left, optional gold link right.</summary>
	public static Control CardHeader(string title, string? link = null, System.Action? onLink = null)
	{
		var row = new HBoxContainer();
		var t = Text_(title, Semibold, 13, Muted);
		t.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(t);
		if (!string.IsNullOrEmpty(link))
		{
			var b = FlatButton(link + "  →", 12, Gold);
			if (onLink != null)
			{
				b.Pressed += onLink;
			}

			row.AddChild(b);
		}

		return row;
	}

	public static Button FlatButton(string text, int size, Color color)
	{
		var b = new Button { Text = text, Flat = true, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		b.AddThemeFontOverride("font", Semibold);
		b.AddThemeFontSizeOverride("font_size", size);
		b.AddThemeColorOverride("font_color", color);
		b.AddThemeColorOverride("font_hover_color", Text);
		b.AddThemeColorOverride("font_pressed_color", color);
		return b;
	}

	/// <summary>Rounded action button; <paramref name="primary"/> is the gold one.</summary>
	public static Button Action(string text, bool primary, System.Action? onPressed = null, int size = 15)
	{
		var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		b.AddThemeFontOverride("font", Bold);
		b.AddThemeFontSizeOverride("font_size", size);
		Color bg = primary ? Gold : Selected;
		Color fg = primary ? GoldInk : Text;
		b.AddThemeColorOverride("font_color", fg);
		b.AddThemeColorOverride("font_hover_color", fg);
		b.AddThemeColorOverride("font_pressed_color", fg);
		b.AddThemeColorOverride("font_disabled_color", new Color(fg, 0.4f));
		b.AddThemeStyleboxOverride("normal", Box(bg, 10, 18, 10, primary ? null : Line, primary ? 0 : 1));
		b.AddThemeStyleboxOverride("hover", Box(primary ? bg.Lightened(0.12f) : new Color("#232838"), 10, 18, 10, primary ? null : Line, primary ? 0 : 1));
		b.AddThemeStyleboxOverride("pressed", Box(primary ? bg.Darkened(0.08f) : new Color("#232838"), 10, 18, 10, primary ? null : Line, primary ? 0 : 1));
		b.AddThemeStyleboxOverride("disabled", Box(new Color(bg, 0.4f), 10, 18, 10));
		b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		if (onPressed != null)
		{
			b.Pressed += onPressed;
		}

		return b;
	}

	/// <summary>Status pill: neutral, good (green), warn (gold) or bad (red).</summary>
	public static PanelContainer Pill(string text, Tone tone = Tone.Neutral)
	{
		(Color bg, Color fg) = tone switch
		{
			Tone.Good => (new Color(Green, 0.13f), Green),
			Tone.Warn => (new Color(Gold, 0.16f), Gold),
			Tone.Bad => (new Color(Red, 0.15f), new Color("#ff7a6b")),
			_ => (new Color("#1d2130"), Muted),
		};
		var pill = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		pill.AddThemeStyleboxOverride("panel", Box(bg, 20, 10, 3));
		pill.AddChild(Text_(text, Semibold, 12, fg, HorizontalAlignment.Center));
		return pill;
	}

	/// <summary>KPI tile: caps label, big number, optional caption.</summary>
	public static PanelContainer Kpi(string label, string value, string? caption = null, Color? valueColor = null)
	{
		var card = MakeCard(18, 14);
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 0);
		col.AddChild(Text_(label, Semibold, 12, Muted));
		col.AddChild(Text_(value, Bold, 34, valueColor ?? Text));
		if (!string.IsNullOrEmpty(caption))
		{
			col.AddChild(Text_(caption, Medium, 13, Muted));
		}

		card.AddChild(col);
		return card;
	}

	/// <summary>KPI tile whose value and caption labels the page updates later.</summary>
	public static PanelContainer KpiLive(string label, out Label value, out Label caption)
	{
		var card = MakeCard(16, 6);
		card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", -4);
		col.AddChild(Text_(label, Semibold, 11, Muted));
		value = Text_("–", Bold, 26, Text);
		value.ClipText = true;
		col.AddChild(value);
		caption = Text_("", Medium, 12, Muted);
		caption.ClipText = true;
		col.AddChild(caption);
		card.AddChild(col);
		return card;
	}

	/// <summary>
	/// Page header from the concepts: school crest, gold kicker, big title,
	/// muted subtitle, then anything passed in <paramref name="right"/>.
	/// </summary>
	public static HBoxContainer PageHeader(string kicker, string title, string subtitle = "", Control? right = null, bool crest = true)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 18);
		if (crest)
		{
			row.AddChild(new ProgramMark(60));
		}

		var col = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
		col.AddThemeConstantOverride("separation", -4);
		col.AddChild(Kicker(kicker));
		col.AddChild(Text_(title, Bold, 42, Text));
		if (!string.IsNullOrEmpty(subtitle))
		{
			col.AddChild(Text_(subtitle, Medium, 16, Muted));
		}

		row.AddChild(col);
		if (right != null)
		{
			right.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
			row.AddChild(right);
		}

		return row;
	}

	/// <summary>Thin progress bar (ratings, goals, budget).</summary>
	public static ProgressBar Meter(float value, float max = 100, Color? fill = null, float width = 120, float height = 7)
	{
		var bar = new ProgressBar
		{
			MaxValue = max,
			Value = value,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(width, height),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		bar.AddThemeStyleboxOverride("background", Box(new Color("#1d2230"), (int)(height / 2)));
		bar.AddThemeStyleboxOverride("fill", Box(fill ?? Gold, (int)(height / 2)));
		return bar;
	}

	public static Control Spacer(float w = 0, float h = 0, bool expand = false) =>
		new Control { CustomMinimumSize = new Vector2(w, h), SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill, MouseFilter = Control.MouseFilterEnum.Ignore };

	public enum Tone
	{
		Neutral,
		Good,
		Warn,
		Bad,
	}
}
