using System;
using Godot;
using HSBM.Domain;

/// <summary>
/// Blocks Simulate Day / Simulate Week when the calendar hits a decision
/// the coach has to make in person: roster, tryouts, playoffs, a job
/// offer, or signing.
/// </summary>
public partial class SimulationStoppedOverlay : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);

	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Label _body = null!;
	private Button _action = null!;
	private bool _built;
	private AdvanceDayInterrupt? _interrupt;

	public event Action<AdvanceDayInterrupt>? GoToRequested;

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

	public void Present(AdvanceDayInterrupt interrupt)
	{
		EnsureBuilt();
		_interrupt = interrupt;
		_body.Text = interrupt.Body;
		_action.Text = interrupt.ActionLabel;
		Visible = true;
		MoveToFront();
		UiSounds.Play(UiSound.Notification);
	}

	public void Close()
	{
		Visible = false;
		_interrupt = null;
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

		var center = new CenterContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(360, 0),
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
			ContentMarginLeft = 28,
			ContentMarginTop = 24,
			ContentMarginRight = 28,
			ContentMarginBottom = 24,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		center.AddChild(panel);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 10);
		panel.AddChild(layout);

		layout.AddChild(MakeText("SIMULATION STOPPED", _semibold, 12, TextMuted, HorizontalAlignment.Center));
		layout.AddChild(MakeText("ACTION REQUIRED", _bold, 28, Urgent, HorizontalAlignment.Center));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Urgent });

		_body = MakeText("", _medium, 16, TextPrimary, HorizontalAlignment.Center);
		_body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(_body);

		_action = new Button
		{
			Text = "GO TO TRYOUTS",
			CustomMinimumSize = new Vector2(0, 40),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		_action.AddThemeFontOverride("font", _bold);
		_action.AddThemeFontSizeOverride("font_size", 15);
		_action.AddThemeColorOverride("font_color", TextOnAccent);
		_action.AddThemeColorOverride("font_hover_color", TextOnAccent);
		_action.AddThemeColorOverride("font_pressed_color", TextOnAccent);
		_action.AddThemeColorOverride("font_focus_color", TextOnAccent);
		_action.AddThemeStyleboxOverride("normal", MakeFill(Accent));
		_action.AddThemeStyleboxOverride("hover", MakeFill(AccentHover));
		_action.AddThemeStyleboxOverride("pressed", MakeFill(AccentHover));
		_action.AddThemeStyleboxOverride("focus", MakeFill(AccentHover));
		UiSounds.SetClick(_action, UiClickSound.Silent);
		_action.Pressed += OnGoTo;
		layout.AddChild(_action);
	}

	private void OnGoTo()
	{
		if (_interrupt == null)
		{
			return;
		}

		AdvanceDayInterrupt interrupt = _interrupt;
		UiSounds.Play(UiSound.Confirm);
		Close();
		GoToRequested?.Invoke(interrupt);
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
