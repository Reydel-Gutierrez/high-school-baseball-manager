using System;
using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// The depth chart drawn as a ballfield (docs/ui-concepts/04_team_lineup):
/// grass, an infield diamond and each starter's capped headshot standing at
/// his position, with the DH off to the side and the probable starter on
/// the mound. Clicking two fielders swaps their positions.
/// </summary>
public partial class DepthChartField : Control
{
	private static readonly Dictionary<string, Vector2> Spots = new(StringComparer.OrdinalIgnoreCase)
	{
		["C"] = new(0.50f, 0.86f),
		["P"] = new(0.50f, 0.60f),
		["1B"] = new(0.74f, 0.57f),
		["2B"] = new(0.63f, 0.41f),
		["SS"] = new(0.37f, 0.41f),
		["3B"] = new(0.26f, 0.57f),
		["LF"] = new(0.18f, 0.20f),
		["CF"] = new(0.50f, 0.10f),
		["RF"] = new(0.82f, 0.20f),
		["DH"] = new(0.13f, 0.86f),
	};

	private readonly List<(Control Chip, Vector2 Spot)> _chips = [];

	/// <summary>Raised with the lineup slot index when a fielder is clicked.</summary>
	public event Action<int>? SlotPressed;

	public DepthChartField()
	{
		ClipContents = true;
		MouseFilter = MouseFilterEnum.Pass;
		Resized += Relayout;
	}

	/// <summary>Rebuilds the chips. <paramref name="selected"/> is the highlighted slot, or -1.</summary>
	public void Bind(LineupCard lineup, string teamId, TeamLevel level, ClubhousePlayer? starter, int selected)
	{
		foreach ((Control chip, Vector2 _) in _chips)
		{
			RemoveChild(chip);
			chip.QueueFree();
		}

		_chips.Clear();
		for (int i = 0; i < lineup.Slots.Length; i++)
		{
			LineupSlot slot = lineup.Slots[i];
			if (!Spots.TryGetValue(slot.Position ?? "", out Vector2 spot))
			{
				continue;
			}

			ClubhousePlayer? player = slot.IsEmpty ? null : ClubhouseSquad.Find(slot.Jersey);
			bool outOfPosition = player != null && !ClubhouseSquad.CanPlayAt(player, slot.Position);
			int index = i;
			AddChip(BuildChip(player, slot.Position, teamId, level, outOfPosition, i == selected, () => SlotPressed?.Invoke(index)), spot);
		}

		if (starter != null)
		{
			AddChip(BuildChip(starter, "P", teamId, level, false, false, null), Spots["P"]);
		}

		Relayout();
	}

	private void AddChip(Control chip, Vector2 spot)
	{
		AddChild(chip);
		_chips.Add((chip, spot));
	}

	private void Relayout()
	{
		foreach ((Control chip, Vector2 spot) in _chips)
		{
			Vector2 min = chip.GetCombinedMinimumSize();
			chip.Size = min;
			chip.Position = new Vector2(spot.X * Size.X - min.X / 2f, spot.Y * Size.Y - min.Y / 2f);
		}

		QueueRedraw();
	}

	private static Control BuildChip(ClubhousePlayer? player, string position, string teamId, TeamLevel level, bool outOfPosition, bool selected, System.Action? onPressed)
	{
		var col = new VBoxContainer { MouseFilter = onPressed == null ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop };
		col.AddThemeConstantOverride("separation", 2);
		if (onPressed != null)
		{
			col.MouseDefaultCursorShape = CursorShape.PointingHand;
			col.GuiInput += ev =>
			{
				if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				{
					onPressed();
				}
			};
		}

		const float face = 50f;
		var ring = new PanelContainer { CustomMinimumSize = new Vector2(face + 6, face + 6), SizeFlagsHorizontal = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		ring.AddThemeStyleboxOverride("panel", UiKit.Box(selected ? UiKit.Text : UiKit.Gold, (int)(face / 2 + 3), 3, 3));
		Control? portrait = player == null ? null : PlayerFaces.For(teamId, player.FullName, face, face, circle: true, level: level);
		if (portrait != null)
		{
			portrait.MouseFilter = MouseFilterEnum.Ignore;
			ring.AddChild(portrait);
		}
		else
		{
			var empty = UiKit.Text_(player == null ? "?" : player.LastName[..1], UiKit.Bold, 22, UiKit.Muted, HorizontalAlignment.Center);
			empty.VerticalAlignment = VerticalAlignment.Center;
			var disc = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
			disc.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Card, (int)(face / 2)));
			disc.AddChild(empty);
			ring.AddChild(disc);
		}

		col.AddChild(ring);

		var tag = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		tag.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.03f, 0.04f, 0.05f, 0.82f), 6, 8, 2));
		string name = player == null ? "OPEN" : player.LastName.ToUpperInvariant() + (outOfPosition ? "*" : "");
		tag.AddChild(UiKit.Text_(name, UiKit.Bold, 13, outOfPosition ? new Color("#ff9a8b") : UiKit.Text, HorizontalAlignment.Center));
		col.AddChild(tag);
		col.AddChild(UiKit.Text_(position, UiKit.Semibold, 10, UiKit.Gold, HorizontalAlignment.Center));
		return col;
	}

	public override void _Draw()
	{
		Vector2 s = Size;
		if (s.X <= 0 || s.Y <= 0)
		{
			return;
		}

		// Grass: vertical gradient with soft mowing stripes.
		var top = new Color("#1b3a1f");
		var bottom = new Color("#2f6233");
		const int bands = 14;
		for (int i = 0; i < bands; i++)
		{
			float t = i / (float)bands;
			Color c = top.Lerp(bottom, t);
			if (i % 2 == 1)
			{
				c = c.Lightened(0.03f);
			}

			DrawRect(new Rect2(0, s.Y * t, s.X, s.Y / bands + 1), c);
		}

		// Infield dirt diamond centered between home and second.
		Vector2 home = new(0.50f * s.X, 0.80f * s.Y);
		float r = Mathf.Min(s.X, s.Y) * 0.25f;
		Vector2 center = home + new Vector2(0, -r);
		var dirt = new Color("#6b4a2e") { A = 0.55f };
		DrawColoredPolygon(
			[center + new Vector2(0, -r * 1.25f), center + new Vector2(r * 1.25f, 0), center + new Vector2(0, r * 1.25f), center + new Vector2(-r * 1.25f, 0)],
			dirt);
		var grass = new Color("#2a5a2e");
		DrawColoredPolygon(
			[center + new Vector2(0, -r * 0.82f), center + new Vector2(r * 0.82f, 0), center + new Vector2(0, r * 0.82f), center + new Vector2(-r * 0.82f, 0)],
			grass);

		// Foul lines from home out to the corners.
		var chalk = new Color(1, 1, 1, 0.35f);
		DrawLine(home, home + new Vector2(-s.Y, -s.Y), chalk, 2f, true);
		DrawLine(home, home + new Vector2(s.Y, -s.Y), chalk, 2f, true);

		// Bases.
		foreach (Vector2 b in new[] { center + new Vector2(r, 0), center + new Vector2(0, -r), center + new Vector2(-r, 0), home })
		{
			DrawRect(new Rect2(b - new Vector2(5, 5), new Vector2(10, 10)), new Color(1, 1, 1, 0.85f));
		}

		DrawCircle(center + new Vector2(0, r * 0.05f), 9f, new Color("#7a5636"));
	}
}
