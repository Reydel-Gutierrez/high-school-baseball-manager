using Godot;

/// <summary>
/// The broadcast look from the player banner, reusable anywhere a player
/// is featured: team-color gradient, a soft trim-color sash, a giant faint
/// jersey number and a trim stripe along the bottom edge. Children are laid
/// out on top (anchor them yourself).
/// </summary>
public partial class BroadcastPanel : Control
{
	private static FontFile? _numberFont;

	public Color Primary { get; set; } = new(0.08f, 0.12f, 0.30f);

	public Color Secondary { get; set; } = new(0.95f, 0.70f, 0.16f);

	public string Number { get; set; } = "";

	/// <summary>Thickness of the trim stripe along the bottom.</summary>
	public float Stripe { get; set; } = 3f;

	public BroadcastPanel()
	{
		ClipContents = true;
		MouseFilter = MouseFilterEnum.Pass;
		Resized += QueueRedraw;
	}

	public override void _Draw()
	{
		Vector2 size = Size;
		if (size.X <= 0 || size.Y <= 0)
		{
			return;
		}

		Color top = Primary.Lightened(0.10f);
		Color bottom = Primary.Darkened(0.55f);
		const int bands = 24;
		for (int i = 0; i < bands; i++)
		{
			float t0 = i / (float)bands;
			float t1 = (i + 1) / (float)bands;
			DrawRect(new Rect2(0, size.Y * t0, size.X, size.Y * (t1 - t0) + 1), top.Lerp(bottom, t0), true);
		}

		var sash = new Vector2[]
		{
			new(size.X * 0.58f, 0), new(size.X * 0.68f, 0), new(size.X * 0.50f, size.Y), new(size.X * 0.40f, size.Y),
		};
		DrawColoredPolygon(sash, new Color(Secondary, 0.10f));

		if (!string.IsNullOrEmpty(Number))
		{
			_numberFont ??= GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
			int fontSize = (int)(size.Y * 1.05f);
			Vector2 textSize = _numberFont.GetStringSize(Number, HorizontalAlignment.Left, -1, fontSize);
			DrawString(_numberFont, new Vector2(size.X - textSize.X - 10, size.Y * 0.92f), Number, HorizontalAlignment.Left, -1, fontSize, new Color(1, 1, 1, 0.07f));
		}

		if (Stripe > 0)
		{
			DrawRect(new Rect2(0, size.Y - Stripe, size.X, Stripe), Secondary, true);
		}
	}
}
