using Godot;

/// <summary>
/// The broadcast look from the player banner, reusable anywhere a player
/// or school is featured: the school's banner art (when it has one) under a
/// team-color gradient, a soft trim-color sash, a giant faint jersey number
/// and a trim stripe along the bottom edge. Children are laid out on top
/// (anchor them yourself).
/// </summary>
public partial class BroadcastPanel : Control
{
	private static FontFile? _numberFont;

	public Color Primary { get; set; } = new(0.08f, 0.12f, 0.30f);

	public Color Secondary { get; set; } = new(0.95f, 0.70f, 0.16f);

	public string Number { get; set; } = "";

	/// <summary>Thickness of the trim stripe along the bottom.</summary>
	public float Stripe { get; set; } = 3f;

	/// <summary>Wide team art drawn behind the gradient (cover-fit, right-aligned).</summary>
	public Texture2D? Background { get; set; }

	public BroadcastPanel()
	{
		ClipContents = true;
		MouseFilter = MouseFilterEnum.Pass;
		Resized += QueueRedraw;
	}

	/// <summary>Primary/secondary colors and banner art for a school in one go.</summary>
	public void UseTeam(string? organizationId)
	{
		(Primary, Secondary) = TeamColors.For(organizationId);
		Background = TeamBrands.Banner(organizationId);
		QueueRedraw();
	}

	public override void _Draw() => Paint(this, Size, Primary, Secondary, Number, Background, Stripe);

	/// <summary>Draws the broadcast backdrop onto any control (the profile hero uses this too).</summary>
	public static void Paint(CanvasItem target, Vector2 size, Color primary, Color secondary, string number, Texture2D? background, float stripe = 3f)
	{
		if (size.X <= 0 || size.Y <= 0)
		{
			return;
		}

		Color top = primary.Lightened(0.10f);
		Color bottom = primary.Darkened(0.55f);
		float alphaTop = 1f;
		float alphaBottom = 1f;
		if (background != null)
		{
			// Cover-fit the art, anchored right so the scene sits behind the
			// text while the headshot side stays dark.
			Vector2 tex = background.GetSize();
			float scale = Mathf.Max(size.X / tex.X, size.Y / tex.Y);
			Vector2 drawn = tex * scale;
			target.DrawTextureRect(background, new Rect2(new Vector2(size.X - drawn.X, (size.Y - drawn.Y) / 2f), drawn), false);
			alphaTop = 0.35f;
			alphaBottom = 0.80f;
		}

		const int bands = 24;
		for (int i = 0; i < bands; i++)
		{
			float t0 = i / (float)bands;
			float t1 = (i + 1) / (float)bands;
			Color c = top.Lerp(bottom, t0);
			c.A = Mathf.Lerp(alphaTop, alphaBottom, t0);
			target.DrawRect(new Rect2(0, size.Y * t0, size.X, size.Y * (t1 - t0) + 1), c, true);
		}

		if (background != null)
		{
			// Darken the left side further so faces and names always read.
			const int cols = 12;
			for (int i = 0; i < cols; i++)
			{
				float t = i / (float)cols;
				target.DrawRect(new Rect2(size.X * 0.5f * t, 0, size.X * 0.5f / cols + 1, size.Y), new Color(primary.Darkened(0.6f), 0.55f * (1f - t)), true);
			}
		}

		var sash = new Vector2[]
		{
			new(size.X * 0.58f, 0), new(size.X * 0.68f, 0), new(size.X * 0.50f, size.Y), new(size.X * 0.40f, size.Y),
		};
		target.DrawColoredPolygon(sash, new Color(secondary, 0.10f));

		if (!string.IsNullOrEmpty(number))
		{
			_numberFont ??= GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
			int fontSize = (int)(size.Y * 1.05f);
			Vector2 textSize = _numberFont.GetStringSize(number, HorizontalAlignment.Left, -1, fontSize);
			target.DrawString(_numberFont, new Vector2(size.X - textSize.X - 10, size.Y * 0.92f), number, HorizontalAlignment.Left, -1, fontSize, new Color(1, 1, 1, 0.07f));
		}

		if (stripe > 0)
		{
			target.DrawRect(new Rect2(0, size.Y - stripe, size.X, stripe), secondary, true);
		}
	}
}
