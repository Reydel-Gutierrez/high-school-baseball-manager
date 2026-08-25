using Godot;
using HSBM.Domain;

/// <summary>
/// Opens a player profile from any hub screen. Pages that already keep a
/// player card in their inspector select in place; everywhere else a
/// right-side dock opens over the current page.
/// </summary>
public static class PlayerLinks
{
	public static void Open(Node from, string teamId, string playerName)
	{
		AppNavigator? navigator = FindNavigator(from);
		if (navigator != null)
		{
			navigator.OpenPlayerProfile(teamId, playerName);
			return;
		}

		GD.PushWarning($"PlayerLinks: no AppNavigator in the tree for '{playerName}'.");
	}

	public static void MakeClickable(Control control, string teamId, string playerName)
	{
		string name = playerName ?? string.Empty;
		if (name.Length == 0)
		{
			return;
		}

		string id = string.IsNullOrWhiteSpace(teamId) ? string.Empty : SchoolProfiles.NormalizeId(teamId);
		control.MouseFilter = Control.MouseFilterEnum.Stop;
		control.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		control.GuiInput += (InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				Open(control, id, name);
				control.AcceptEvent();
			}
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
