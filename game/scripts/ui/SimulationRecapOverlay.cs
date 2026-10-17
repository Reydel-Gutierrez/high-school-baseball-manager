using System;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// After Simulate Day / Week, list the games, injuries, signings, and
/// other beats that just happened instead of silently updating the world.
/// </summary>
public partial class SimulationRecapOverlay : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);

	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Label _kicker = null!;
	private Label _title = null!;
	private VBoxContainer _list = null!;
	private bool _built;

	public event Action? Closed;

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

	public void Present(SimulationRecap recap)
	{
		EnsureBuilt();
		_kicker.Text = recap.DaysAdvanced == 1
			? "1 DAY SIMULATED"
			: $"{recap.DaysAdvanced} DAYS SIMULATED";
		_title.Text = recap.RangeLabel.ToUpperInvariant();

		foreach (Node child in _list.GetChildren())
		{
			_list.RemoveChild(child);
			child.QueueFree();
		}

		foreach (SimulationRecapItem item in recap.Items)
		{
			_list.AddChild(BuildRow(item));
		}

		Visible = true;
		MoveToFront();
		UiSounds.Play(UiSound.Notification);
	}

	public void Close()
	{
		if (!Visible)
		{
			return;
		}

		Visible = false;
		Closed?.Invoke();
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
			Color = new Color(0.02f, 0.03f, 0.04f, 0.82f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		dim.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				UiSounds.Play(UiSound.Back);
				Close();
			}
		};
		AddChild(dim);

		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(460, 0),
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
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		center.AddChild(panel);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 10);
		panel.AddChild(layout);

		_kicker = MakeText("SIMULATION", _semibold, 12, TextMuted, HorizontalAlignment.Left);
		layout.AddChild(_kicker);
		_title = MakeText("WHAT HAPPENED", _bold, 24, TextPrimary, HorizontalAlignment.Left);
		layout.AddChild(_title);
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });

		var scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, 280),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		layout.AddChild(scroll);
		_list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_list.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(_list);

		var close = new Button
		{
			Text = "CONTINUE",
			CustomMinimumSize = new Vector2(0, 40),
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		close.AddThemeFontOverride("font", _bold);
		close.AddThemeFontSizeOverride("font_size", 15);
		close.AddThemeColorOverride("font_color", TextOnAccent);
		close.AddThemeColorOverride("font_hover_color", TextOnAccent);
		close.AddThemeStyleboxOverride("normal", MakeFill(Accent));
		close.AddThemeStyleboxOverride("hover", MakeFill(AccentHover));
		close.AddThemeStyleboxOverride("pressed", MakeFill(AccentHover));
		close.AddThemeStyleboxOverride("focus", MakeFill(AccentHover));
		UiSounds.SetClick(close, UiClickSound.Silent);
		close.Pressed += () =>
		{
			UiSounds.Play(UiSound.Confirm);
			Close();
		};
		layout.AddChild(close);
	}

	private Control BuildRow(SimulationRecapItem item)
	{
		Color color = item.Kind switch
		{
			SimulationRecapKind.Game => item.Headline.StartsWith("L ", System.StringComparison.Ordinal) ? Urgent : Green,
			SimulationRecapKind.Injury => Urgent,
			SimulationRecapKind.Signing => Accent,
			SimulationRecapKind.Rollover => Accent,
			_ => TextMuted,
		};
		var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = color,
			BorderWidthLeft = 2,
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
		});
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 2);
		card.AddChild(box);
		box.AddChild(MakeText(
			item.Kind.ToString().ToUpperInvariant() + "  ·  " + item.Date.ToString("MMM d", CultureInfo.InvariantCulture).ToUpperInvariant(),
			_semibold,
			10,
			color,
			HorizontalAlignment.Left));
		var headline = MakeText(item.Headline, _semibold, 14, TextPrimary, HorizontalAlignment.Left);
		headline.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		box.AddChild(headline);
		if (!string.IsNullOrEmpty(item.Detail))
		{
			var detail = MakeText(item.Detail, _medium, 12, TextMuted, HorizontalAlignment.Left);
			detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			box.AddChild(detail);
		}

		return card;
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

	private static StyleBoxFlat MakeFill(Color color) =>
		new()
		{
			BgColor = color,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		};
}
