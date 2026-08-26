using Godot;

/// <summary>
/// Top-bar inbox control: a vector envelope with an unread badge.
/// Drawn in place so the glyph stays sharp under canvas stretch.
/// </summary>
public partial class MessagesTabButton : Button
{
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);

	private PanelContainer _badge = null!;
	private Label _badgeLabel = null!;

	public override void _Ready()
	{
		Text = string.Empty;
		Icon = null;
		ExpandIcon = false;
		Flat = false;
		FocusMode = FocusModeEnum.All;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		CustomMinimumSize = new Vector2(32, 32);
		SizeFlagsVertical = SizeFlags.ShrinkCenter;
		ClipContents = false;
		TooltipText = "Inbox";

		MouseEntered += QueueRedraw;
		MouseExited += QueueRedraw;
		FocusEntered += QueueRedraw;
		FocusExited += QueueRedraw;

		_badge = new PanelContainer
		{
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_badge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = Accent,
			ContentMarginLeft = 3,
			ContentMarginTop = 1,
			ContentMarginRight = 3,
			ContentMarginBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6,
		});
		_badge.SetAnchorsPreset(LayoutPreset.TopRight);
		_badge.GrowHorizontal = GrowDirection.Begin;
		_badge.OffsetLeft = -12;
		_badge.OffsetTop = -1;
		_badge.OffsetRight = 3;
		_badge.OffsetBottom = 11;
		AddChild(_badge);

		_badgeLabel = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_badgeLabel.AddThemeFontOverride("font", GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf"));
		_badgeLabel.AddThemeFontSizeOverride("font_size", 9);
		_badgeLabel.AddThemeColorOverride("font_color", TextOnAccent);
		_badge.AddChild(_badgeLabel);
	}

	public void SetUnread(int count)
	{
		int unread = Mathf.Max(0, count);
		_badge.Visible = unread > 0;
		_badgeLabel.Text = unread > 9 ? "9+" : unread.ToString();
		TooltipText = unread > 0
			? $"{unread} item{(unread == 1 ? "" : "s")} need attention"
			: "Inbox — nothing waiting";
	}

	public override void _Notification(int what)
	{
		if (what == NotificationThemeChanged)
		{
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		Vector2 size = Size;
		if (size.X < 8f || size.Y < 8f)
		{
			return;
		}

		Color color = EnvelopeColor();
		const float stroke = 1.75f;
		const float bodyW = 18f;
		const float bodyH = 12.5f;
		float x0 = Mathf.Round((size.X - bodyW) * 0.5f);
		float y0 = Mathf.Round((size.Y - bodyH) * 0.5f);
		var body = new Rect2(x0, y0, bodyW, bodyH);

		var outline = new StyleBoxFlat
		{
			BgColor = Colors.Transparent,
			DrawCenter = false,
			BorderColor = color,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2,
			CornerDetail = 4,
		};
		DrawStyleBox(outline, body);

		float inset = 2f;
		var flap = new Vector2[]
		{
			new(x0 + inset, y0 + inset + 0.4f),
			new(size.X * 0.5f, y0 + bodyH * 0.58f),
			new(x0 + bodyW - inset, y0 + inset + 0.4f),
		};
		DrawPolyline(flap, color, stroke, antialiased: true);
	}

	private Color EnvelopeColor()
	{
		DrawMode mode = GetDrawMode();
		if (mode is DrawMode.Hover or DrawMode.HoverPressed)
		{
			return GetThemeColor("icon_hover_color");
		}

		if (mode == DrawMode.Pressed)
		{
			return GetThemeColor("icon_pressed_color");
		}

		if (HasFocus())
		{
			return GetThemeColor("icon_focus_color");
		}

		return GetThemeColor("icon_normal_color");
	}
}
