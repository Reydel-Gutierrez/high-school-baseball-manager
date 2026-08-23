using System.Globalization;
using Godot;

/// <summary>
/// Top-bar date leaf: a hanging calendar page for the current sim day,
/// not a text menu item. Opens the season calendar.
/// </summary>
public partial class CalendarTabButton : Button
{
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color PageBg = new(0.121569f, 0.137255f, 0.160784f, 1f);
	private static readonly Color PageBgHover = new(0.168627f, 0.184314f, 0.215686f, 1f);
	private static readonly Color PageBgActive = new(0.196078f, 0.164706f, 0.101961f, 1f);
	private static readonly Color Border = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color FocusBorder = new(0.988235f, 0.843137f, 0.450980f, 1f);
	private static readonly Color DayColor = new(0.956863f, 0.964706f, 0.972549f, 1f);

	private FontFile _bold = null!;
	private FontVariation _monthFont = null!;
	private string _monthText = "APR";
	private string _dayText = "24";
	private bool _active;
	private bool _hover;
	private bool _focus;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_monthFont = new FontVariation
		{
			BaseFont = _bold,
			SpacingGlyph = 2,
		};

		Text = string.Empty;
		Icon = null;
		Flat = false;
		ClipContents = true;
		FocusMode = FocusModeEnum.All;
		MouseDefaultCursorShape = CursorShape.PointingHand;
		CustomMinimumSize = new Vector2(42, 38);
		SizeFlagsVertical = SizeFlags.ShrinkCenter;

		var empty = new StyleBoxEmpty();
		AddThemeStyleboxOverride("normal", empty);
		AddThemeStyleboxOverride("hover", empty);
		AddThemeStyleboxOverride("pressed", empty);
		AddThemeStyleboxOverride("hover_pressed", empty);
		AddThemeStyleboxOverride("focus", empty);
		AddThemeStyleboxOverride("disabled", empty);

		BindDate();

		MouseEntered += () =>
		{
			_hover = true;
			QueueRedraw();
		};
		MouseExited += () =>
		{
			_hover = false;
			QueueRedraw();
		};
		FocusEntered += () =>
		{
			_focus = true;
			QueueRedraw();
		};
		FocusExited += () =>
		{
			_focus = false;
			QueueRedraw();
		};
	}

	public void SetActive(bool active)
	{
		_active = active;
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 size = Size;
		if (size.X < 8f || size.Y < 8f)
		{
			return;
		}

		bool lit = _active || _hover;
		Color page = _active ? PageBgActive : _hover ? PageBgHover : PageBg;
		Color header = lit ? AccentHover : Accent;
		Color border = _focus ? FocusBorder : _active ? Accent : Border;
		float headerH = Mathf.Clamp(Mathf.Round(size.Y * 0.36f), 12f, 15f);

		var pageBox = new StyleBoxFlat
		{
			BgColor = page,
			DrawCenter = true,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			CornerRadiusBottomRight = 5,
			CornerRadiusBottomLeft = 5,
			CornerDetail = 4,
		};
		DrawStyleBox(pageBox, new Rect2(Vector2.Zero, size));

		var headerBox = new StyleBoxFlat
		{
			BgColor = header,
			DrawCenter = true,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 0,
			CornerRadiusBottomLeft = 0,
			CornerDetail = 4,
		};
		DrawStyleBox(headerBox, new Rect2(1, 1, size.X - 2, headerH));

		DrawString(
			_monthFont,
			new Vector2(0, headerH - 2f),
			_monthText,
			HorizontalAlignment.Center,
			size.X,
			9,
			TextOnAccent);

		float bodyTop = headerH + 1f;
		float bodyHeight = size.Y - bodyTop - 2f;
		float dayAscent = _bold.GetAscent(18);
		float dayY = bodyTop + (bodyHeight + dayAscent) * 0.5f - 1f;
		DrawString(
			_bold,
			new Vector2(0, dayY),
			_dayText,
			HorizontalAlignment.Center,
			size.X,
			18,
			DayColor);
	}

	private void BindDate()
	{
		DateOnly date = GameSession.Current != null
			? GameSession.Current.CurrentDate
			: new DateOnly(GameSession.SeasonYear, 4, 24);
		_monthText = date.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
		_dayText = date.Day.ToString(CultureInfo.InvariantCulture);
		TooltipText = date.ToString("dddd, MMMM d", CultureInfo.InvariantCulture);
	}
}
