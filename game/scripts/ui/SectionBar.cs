using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Page tabs for the active rail section (Team: Roster · Depth Chart ·
/// Pitching Staff …). Sits under the top bar; hidden on Home.
/// </summary>
public partial class SectionBar : PanelContainer
{
	private HBoxContainer _row = null!;

	public override void _Ready()
	{
		AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = UiKit.Bg,
			ContentMarginLeft = 28,
			ContentMarginRight = 28,
			ContentMarginTop = 2,
			ContentMarginBottom = 6,
		});
		_row = new HBoxContainer();
		_row.AddThemeConstantOverride("separation", 4);
		AddChild(_row);
	}

	public void SetTabs(IReadOnlyList<(string Label, Action Open)> tabs, string active)
	{
		if (_row == null)
		{
			return;
		}

		foreach (Node child in _row.GetChildren())
		{
			_row.RemoveChild(child);
			child.QueueFree();
		}

		Visible = tabs.Count > 0;
		foreach ((string label, Action open) in tabs)
		{
			bool on = string.Equals(label, active, StringComparison.Ordinal);
			var b = new Button { Text = label, FocusMode = FocusModeEnum.None, MouseDefaultCursorShape = CursorShape.PointingHand };
			b.AddThemeFontOverride("font", UiKit.Semibold);
			b.AddThemeFontSizeOverride("font_size", 14);
			b.AddThemeColorOverride("font_color", on ? UiKit.Text : UiKit.Muted);
			b.AddThemeColorOverride("font_hover_color", UiKit.Text);
			b.AddThemeColorOverride("font_pressed_color", UiKit.Text);
			var normal = UiKit.Box(on ? new Color("#1d2130") : Colors.Transparent, 10, 16, 8);
			if (on)
			{
				normal.BorderColor = UiKit.Gold;
				normal.BorderWidthBottom = 2;
			}

			b.AddThemeStyleboxOverride("normal", normal);
			b.AddThemeStyleboxOverride("hover", UiKit.Box(new Color("#171b25"), 10, 16, 8));
			b.AddThemeStyleboxOverride("pressed", normal);
			b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			b.Pressed += open;
			_row.AddChild(b);
		}
	}
}
