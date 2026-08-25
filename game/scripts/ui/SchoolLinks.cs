using System;
using Godot;
using HSBM.Domain;

/// <summary>
/// Opens a school profile from any league screen. Pages with a side
/// inspector keep the card in that column; everywhere else a right-side
/// dock opens over the current page.
/// </summary>
public static class SchoolLinks
{
	public static void Open(Node from, string teamId)
	{
		string id = SchoolProfiles.NormalizeId(teamId);
		AppNavigator? navigator = FindNavigator(from);
		if (navigator != null)
		{
			navigator.OpenSchoolProfile(id);
			return;
		}

		GD.PushWarning($"SchoolLinks: no AppNavigator in the tree for '{id}'.");
	}

	public static void MakeClickable(Control control, string teamId)
	{
		string id = SchoolProfiles.NormalizeId(teamId);
		control.MouseFilter = Control.MouseFilterEnum.Stop;
		control.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		control.GuiInput += (InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				Open(control, id);
				control.AcceptEvent();
			}
		};
	}

	public static Control MakeModeToggle(
		FontFile semibold,
		bool schoolActive,
		Action<bool> onPicked)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		row.AddChild(MakePill(semibold, "PLAYER", !schoolActive, () => onPicked(false)));
		row.AddChild(MakePill(semibold, "SCHOOL", schoolActive, () => onPicked(true)));
		return row;
	}

	public static Control MakeMentionChips(string text, FontFile semibold)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		foreach (HubTeam team in SchoolProfiles.FindMentioned(text))
		{
			var chip = new Button
			{
				Text = team.ShortName.ToUpperInvariant(),
				Flat = false,
				MouseDefaultCursorShape = Control.CursorShape.PointingHand,
			};
			chip.AddThemeFontOverride("font", semibold);
			chip.AddThemeFontSizeOverride("font_size", 11);
			chip.AddThemeColorOverride("font_color", new Color(0.070588f, 0.074510f, 0.086275f, 1f));
			chip.AddThemeStyleboxOverride("normal", MakeFilled(new Color(0.956863f, 0.643137f, 0.109804f, 1f)));
			chip.AddThemeStyleboxOverride("hover", MakeFilled(new Color(0.980392f, 0.721569f, 0.200000f, 1f)));
			chip.AddThemeStyleboxOverride("pressed", MakeFilled(new Color(0.980392f, 0.721569f, 0.200000f, 1f)));
			string id = team.Id;
			chip.Pressed += () => Open(chip, id);
			row.AddChild(chip);
		}

		return row;
	}

	private static Button MakePill(FontFile semibold, string label, bool active, Action onPressed)
	{
		var button = new Button
		{
			Text = label,
			Flat = false,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
		};
		Color accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
		Color textOn = new(0.070588f, 0.074510f, 0.086275f, 1f);
		Color idle = new(0.956863f, 0.964706f, 0.972549f, 1f);
		button.AddThemeFontOverride("font", semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", active ? textOn : idle);
		button.AddThemeColorOverride("font_hover_color", active ? textOn : Colors.White);
		button.AddThemeStyleboxOverride("normal", MakeFilled(active ? accent : Colors.Transparent));
		button.AddThemeStyleboxOverride("hover", MakeFilled(active ? new Color(0.980392f, 0.721569f, 0.200000f, 1f) : new Color(0.184314f, 0.203922f, 0.239216f, 1f)));
		button.AddThemeStyleboxOverride("pressed", MakeFilled(active ? accent : new Color(0.184314f, 0.203922f, 0.239216f, 1f)));
		button.Pressed += onPressed;
		return button;
	}

	private static StyleBoxFlat MakeFilled(Color bg)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			ContentMarginLeft = 10,
			ContentMarginTop = 5,
			ContentMarginRight = 10,
			ContentMarginBottom = 5,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
		};
	}

	private static AppNavigator? FindNavigator(Node from)
	{
		Node? node = from;
		while (node != null)
		{
			if (node is AppNavigator navigator)
			{
				return navigator;
			}

			node = node.GetParent();
		}

		return from.GetTree()?.CurrentScene as AppNavigator;
	}
}
