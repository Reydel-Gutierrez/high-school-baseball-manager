using System;
using Godot;

/// <summary>
/// Shared CALENDAR / SEASON SCHEDULE pills. Calendar is the month desk;
/// schedule is the results table.
/// </summary>
public static class SeasonDeskTabs
{
	public const string Calendar = "calendar";
	public const string Schedule = "schedule";

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color Accent = new(0.772549f, 0.635294f, 0.325490f, 1f);
	private static readonly Color AccentHover = new(0.850980f, 0.729412f, 0.443137f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);

	public static Control Build(FontFile semibold, string active, Action<string> onPicked)
	{
		// The shell's section tabs (Season: Calendar · Schedule …) replace these pills.
		var row = new HBoxContainer { Visible = false };
		row.AddThemeConstantOverride("separation", 4);
		AddPill(row, semibold, Calendar, "CALENDAR", active, onPicked);
		AddPill(row, semibold, Schedule, "SEASON SCHEDULE", active, onPicked);
		return row;
	}

	private static void AddPill(
		HBoxContainer row,
		FontFile semibold,
		string key,
		string label,
		string active,
		Action<string> onPicked)
	{
		bool isActive = key == active;
		var button = new Button
		{
			Text = label,
			Flat = false,
			FocusMode = Control.FocusModeEnum.All,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
		};
		button.AddThemeFontOverride("font", semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", isActive ? TextOnAccent : TextPrimary);
		button.AddThemeColorOverride("font_hover_color", isActive ? TextOnAccent : Colors.White);
		button.AddThemeColorOverride("font_pressed_color", isActive ? TextOnAccent : TextPrimary);
		button.AddThemeColorOverride("font_focus_color", isActive ? TextOnAccent : Colors.White);
		button.AddThemeStyleboxOverride("normal", MakePill(isActive ? Accent : Colors.Transparent));
		button.AddThemeStyleboxOverride("hover", MakePill(isActive ? AccentHover : HoverBg));
		button.AddThemeStyleboxOverride("pressed", MakePill(isActive ? AccentHover : HoverBg));
		button.AddThemeStyleboxOverride("focus", MakePill(isActive ? AccentHover : HoverBg));
		button.Pressed += () => onPicked(key);
		row.AddChild(button);
	}

	private static StyleBoxFlat MakePill(Color bg)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			DrawCenter = true,
			ContentMarginLeft = 12,
			ContentMarginTop = 5,
			ContentMarginRight = 12,
			ContentMarginBottom = 5,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerDetail = 5,
		};
	}
}
