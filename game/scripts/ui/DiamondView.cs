using Godot;

/// <summary>
/// Top-down infield for the live game: occupied bases glow, outs light up
/// beneath, and the batter's box shows who is hitting from which side.
/// </summary>
public partial class DiamondView : Control
{
	private static readonly Color Grass = new(0.10f, 0.20f, 0.12f, 0.92f);
	private static readonly Color Dirt = new(0.42f, 0.30f, 0.20f, 0.95f);
	private static readonly Color Chalk = new(0.92f, 0.92f, 0.88f, 0.9f);
	private static readonly Color BaseEmpty = new(0.85f, 0.86f, 0.88f, 1f);
	private static readonly Color BaseLit = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color OutOff = new(0.25f, 0.27f, 0.31f, 1f);
	private static readonly Color OutOn = new(0.906f, 0.298f, 0.235f, 1f);

	private bool _first;
	private bool _second;
	private bool _third;
	private int _outs;
	private Font? _font;

	public DiamondView()
	{
		CustomMinimumSize = new Vector2(240, 210);
		MouseFilter = MouseFilterEnum.Ignore;
	}

	public void SetState(bool first, bool second, bool third, int outs)
	{
		_first = first;
		_second = second;
		_third = third;
		_outs = Mathf.Clamp(outs, 0, 3);
		QueueRedraw();
	}

	public override void _Ready()
	{
		_font = GD.Load<Font>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
	}

	public override void _Draw()
	{
		Vector2 size = Size;
		float field = Mathf.Min(size.X, size.Y - 34);
		var center = new Vector2(size.X / 2f, field * 0.56f);
		float r = field * 0.40f;

		// Outfield fan + infield dirt diamond.
		DrawCircle(center + new Vector2(0, r * 0.15f), r * 1.2f, Grass);
		Vector2 home = center + new Vector2(0, r);
		Vector2 first = center + new Vector2(r, 0);
		Vector2 second = center + new Vector2(0, -r);
		Vector2 third = center + new Vector2(-r, 0);
		DrawColoredPolygon([home, first, second, third], Dirt);
		var inner = new[]
		{
			center + new Vector2(0, r * 0.62f),
			center + new Vector2(r * 0.62f, 0),
			center + new Vector2(0, -r * 0.62f),
			center + new Vector2(-r * 0.62f, 0),
		};
		DrawColoredPolygon(inner, Grass);
		DrawPolyline([home, first, second, third, home], Chalk, 1.5f, true);
		DrawCircle(center, r * 0.09f, Dirt);

		float bag = r * 0.17f;
		DrawBag(first, bag, _first);
		DrawBag(second, bag, _second);
		DrawBag(third, bag, _third);
		DrawColoredPolygon(
			[
				home + new Vector2(-bag * 0.5f, -bag * 0.35f),
				home + new Vector2(bag * 0.5f, -bag * 0.35f),
				home + new Vector2(bag * 0.5f, 0),
				home + new Vector2(0, bag * 0.45f),
				home + new Vector2(-bag * 0.5f, 0),
			],
			BaseEmpty);

		// Outs.
		float dot = 7f;
		float y = size.Y - 14f;
		float startX = size.X / 2f - 30f;
		if (_font != null)
		{
			DrawString(_font, new Vector2(startX - 44f, y + 5f), "OUTS", HorizontalAlignment.Left, -1, 13, OutOff.Lightened(0.5f));
		}

		for (int i = 0; i < 3; i++)
		{
			Vector2 c = new(startX + i * 22f + 10f, y);
			DrawCircle(c, dot, i < _outs ? OutOn : OutOff);
		}
	}

	private void DrawBag(Vector2 at, float half, bool lit)
	{
		DrawColoredPolygon(
			[
				at + new Vector2(0, -half),
				at + new Vector2(half, 0),
				at + new Vector2(0, half),
				at + new Vector2(-half, 0),
			],
			lit ? BaseLit : BaseEmpty);
		if (lit)
		{
			DrawArc(at, half * 1.5f, 0, Mathf.Tau, 24, new Color(BaseLit, 0.45f), 2f, true);
		}
	}
}
