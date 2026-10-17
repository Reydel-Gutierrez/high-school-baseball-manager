using HSBM.Domain;
using Godot;

/// <summary>
/// Procedural layered portrait. Drop PNG layers into res://assets/portraits/
/// later; this renderer stays as the offline fallback.
/// </summary>
public sealed class LayeredPortraitRenderer : IPortraitRenderer
{
	public void Draw(CanvasItem canvas, PersonAppearance appearance, Rect2 bounds)
	{
		if (bounds.Size.X < 8f || bounds.Size.Y < 8f)
		{
			return;
		}

		float size = Mathf.Min(bounds.Size.X, bounds.Size.Y);
		Vector2 center = bounds.GetCenter() + new Vector2(0f, size * 0.04f);
		Color skin = PortraitAssetCatalog.SkinTint(appearance.SkinTone);
		Color hair = PortraitAssetCatalog.HairTint(appearance.HairColor);
		Color outfit = PortraitAssetCatalog.OutfitTint(appearance);
		Color accent = PortraitAssetCatalog.OutfitAccent(appearance);
		Color shadow = skin.Darkened(0.22f);

		DrawBackdrop(canvas, bounds, size);
		DrawShoulders(canvas, appearance, center, size, outfit, accent);
		DrawNeck(canvas, center, size, skin, shadow);
		DrawHead(canvas, appearance, center, size, skin, shadow);
		DrawEars(canvas, appearance, center, size, skin, shadow);
		DrawEyes(canvas, appearance, center, size, skin);
		DrawBrows(canvas, appearance, center, size, hair);
		DrawNose(canvas, appearance, center, size, shadow);
		DrawMouth(canvas, appearance, center, size, skin);
		DrawFacialHair(canvas, appearance, center, size);
		DrawHair(canvas, appearance, center, size, hair);
		DrawAccessory(canvas, appearance, center, size, outfit, accent);
	}

	private static void DrawBackdrop(CanvasItem canvas, Rect2 bounds, float size)
	{
		Color dark = new(0.06f, 0.10f, 0.18f);
		Color lift = new(0.10f, 0.16f, 0.28f);
		for (int i = 0; i < 8; i++)
		{
			float t = i / 8f;
			float inset = t * size * 0.08f;
			var rect = new Rect2(
				bounds.Position + new Vector2(inset, inset),
				bounds.Size - new Vector2(inset * 2f, inset * 2f));
			canvas.DrawRect(rect, dark.Lerp(lift, t), true);
		}
	}

	private static void DrawShoulders(CanvasItem canvas, PersonAppearance appearance, Vector2 center, float size, Color outfit, Color accent)
	{
		float width = appearance.BodyBuild switch
		{
			BodyBuild.Slim => size * 0.78f,
			BodyBuild.Athletic => size * 0.92f,
			BodyBuild.Stocky => size * 0.98f,
			_ => size * 0.86f,
		};
		float top = center.Y + size * 0.16f;
		float bottom = center.Y + size * 0.58f;
		Vector2[] body =
		[
			center + new Vector2(-width * 0.18f, size * 0.16f),
			center + new Vector2(width * 0.18f, size * 0.16f),
			new(center.X + width * 0.50f, bottom),
			new(center.X - width * 0.50f, bottom),
		];
		canvas.DrawColoredPolygon(body, outfit);

		if (appearance.OutfitStyle == OutfitStyle.BaseballJersey)
		{
			canvas.DrawRect(new Rect2(center.X - width * 0.08f, top, width * 0.16f, size * 0.28f), accent, true);
		}
		else if (appearance.OutfitStyle == OutfitStyle.TeamWindbreaker)
		{
			canvas.DrawRect(new Rect2(center.X - width * 0.50f, top + size * 0.06f, width, size * 0.045f), accent, true);
		}
		else if (appearance.OutfitStyle == OutfitStyle.BaseballPolo)
		{
			Vector2[] collar =
			[
				center + new Vector2(-size * 0.10f, size * 0.18f),
				center + new Vector2(0f, size * 0.26f),
				center + new Vector2(size * 0.10f, size * 0.18f),
			];
			canvas.DrawColoredPolygon(collar, accent);
		}
	}

	private static void DrawNeck(CanvasItem canvas, Vector2 center, float size, Color skin, Color shadow)
	{
		var neck = new Rect2(center.X - size * 0.08f, center.Y + size * 0.10f, size * 0.16f, size * 0.16f);
		canvas.DrawRect(neck, skin, true);
		canvas.DrawRect(new Rect2(neck.Position.X, neck.End.Y - size * 0.02f, neck.Size.X, size * 0.02f), shadow, true);
	}

	private static void DrawHead(CanvasItem canvas, PersonAppearance appearance, Vector2 center, float size, Color skin, Color shadow)
	{
		float w = appearance.FaceShape switch
		{
			FaceShape.Round => size * 0.34f,
			FaceShape.Square => size * 0.33f,
			FaceShape.Long => size * 0.28f,
			FaceShape.Heart => size * 0.32f,
			FaceShape.Diamond => size * 0.30f,
			_ => size * 0.31f,
		};
		float h = appearance.FaceShape switch
		{
			FaceShape.Long => size * 0.44f,
			FaceShape.Round => size * 0.38f,
			_ => size * 0.40f,
		};
		Vector2 head = center + new Vector2(0f, -size * 0.06f);
		DrawEllipse(canvas, head, w, h, skin);
		DrawEllipse(canvas, head + new Vector2(0f, h * 0.18f), w * 0.72f, h * 0.42f, shadow * new Color(1f, 1f, 1f, 0.18f));
	}

	private static void DrawEars(CanvasItem canvas, PersonAppearance appearance, Vector2 center, float size, Color skin, Color shadow)
	{
		if (appearance.HairStyle is HairStyle.Long or HairStyle.Bun or HairStyle.Ponytail)
		{
			return;
		}

		float y = center.Y - size * 0.04f;
		float x = appearance.FaceShape == FaceShape.Square ? size * 0.33f : size * 0.31f;
		DrawEllipse(canvas, center + new Vector2(-x, y), size * 0.045f, size * 0.07f, skin);
		DrawEllipse(canvas, center + new Vector2(x, y), size * 0.045f, size * 0.07f, skin);
		DrawEllipse(canvas, center + new Vector2(-x, y), size * 0.022f, size * 0.035f, shadow);
		DrawEllipse(canvas, center + new Vector2(x, y), size * 0.022f, size * 0.035f, shadow);
	}

	private static void DrawEyes(CanvasItem canvas, PersonAppearance appearance, Vector2 center, float size, Color skin)
	{
		Color white = new(0.95f, 0.95f, 0.96f);
		Color iris = PortraitAssetCatalog.EyeTint(appearance.EyeColor);
		float eyeW = appearance.EyeStyle == EyeStyle.Narrow ? size * 0.048f : size * 0.052f;
		float eyeH = appearance.EyeStyle switch
		{
			EyeStyle.Round => size * 0.038f,
			EyeStyle.Narrow => size * 0.022f,
			EyeStyle.DeepSet => size * 0.028f,
			_ => size * 0.032f,
		};
		float y = center.Y - size * 0.08f;
		float spread = size * 0.11f;
		foreach (float side in new[] { -1f, 1f })
		{
			Vector2 pos = center + new Vector2(side * spread, y);
			DrawEllipse(canvas, pos, eyeW, eyeH, white);
			DrawEllipse(canvas, pos, eyeW * 0.45f, eyeH * 0.70f, iris);
			DrawEllipse(canvas, pos + new Vector2(-eyeW * 0.12f, -eyeH * 0.12f), eyeW * 0.14f, eyeH * 0.18f, Colors.White);
			if (appearance.EyeStyle == EyeStyle.DeepSet)
			{
				canvas.DrawLine(pos + new Vector2(-eyeW, -eyeH * 1.4f), pos + new Vector2(eyeW, -eyeH * 1.4f), skin.Darkened(0.25f), size * 0.012f);
			}
		}
	}

	private static void DrawBrows(CanvasItem canvas, PersonAppearance appearance, Vector2 center, float size, Color hair)
	{
		float thickness = appearance.BrowStyle == BrowStyle.Thick ? size * 0.018f : size * 0.012f;
		float y = center.Y - size * 0.125f;
		float spread = size * 0.11f;
		float arch = appearance.BrowStyle == BrowStyle.Arched ? -size * 0.018f : appearance.BrowStyle == BrowStyle.Straight ? 0f : -size * 0.008f;
		foreach (float side in new[] { -1f, 1f })
		{
			Vector2 a = center + new Vector2(side * (spread + size * 0.05f), y);
			Vector2 b = center + new Vector2(side * spread, y + arch);
			Vector2 c = center + new Vector2(side * (spread - size * 0.05f), y + size * 0.004f);
			canvas.DrawLine(a, b, hair, thickness);
			canvas.DrawLine(b, c, hair, thickness * 0.85f);
		}
	}

	private static void DrawNose(CanvasItem canvas, PersonAppearance appearance, Vector2 center, float size, Color shadow)
	{
		float w = appearance.NoseStyle switch
		{
			NoseStyle.Wide => size * 0.045f,
			NoseStyle.Button => size * 0.028f,
			NoseStyle.Long => size * 0.030f,
			_ => size * 0.032f,
		};
		Vector2 tip = center + new Vector2(0f, size * 0.01f);
		canvas.DrawLine(center + new Vector2(0f, -size * 0.05f), tip, shadow, size * 0.01f);
		DrawEllipse(canvas, tip, w, size * 0.02f, shadow * new Color(1f, 1f, 1f, 0.55f));
	}

	private static void DrawMouth(CanvasItem canvas, PersonAppearance appearance, Vector2 center, float size, Color skin)
	{
		Color lip = skin.Lerp(new Color(0.62f, 0.28f, 0.28f), 0.35f);
		float y = center.Y + size * 0.08f;
		float w = appearance.MouthStyle == MouthStyle.Wide ? size * 0.12f : size * 0.10f;
		float curve = appearance.MouthStyle == MouthStyle.SlightSmile ? -size * 0.012f : 0f;
		Vector2 left = center + new Vector2(-w, y);
		Vector2 right = center + new Vector2(w, y);
		Vector2 mid = center + new Vector2(0f, y + curve);
		canvas.DrawLine(left, mid, lip, size * 0.012f);
		canvas.DrawLine(mid, right, lip, size * 0.012f);
	}

	private static void DrawFacialHair(CanvasItem canvas, PersonAppearance appearance, Vector2 center, float size)
	{
		if (appearance.FacialHairStyle == FacialHairStyle.None)
		{
			return;
		}

		Color color = PortraitAssetCatalog.HairTint(appearance.FacialHairColor);
		if (appearance.FacialHairStyle == FacialHairStyle.Stubble)
		{
			color = color * new Color(1f, 1f, 1f, 0.45f);
		}

		float y = center.Y + size * 0.07f;
		switch (appearance.FacialHairStyle)
		{
			case FacialHairStyle.Stubble:
				DrawEllipse(canvas, center + new Vector2(0f, size * 0.10f), size * 0.20f, size * 0.12f, color);
				break;
			case FacialHairStyle.Mustache:
				canvas.DrawLine(center + new Vector2(-size * 0.08f, y), center + new Vector2(size * 0.08f, y), color, size * 0.02f);
				break;
			case FacialHairStyle.Goatee:
			case FacialHairStyle.SoulPatch:
				DrawEllipse(canvas, center + new Vector2(0f, y + size * 0.05f), size * 0.045f, size * 0.05f, color);
				break;
			default:
				DrawEllipse(canvas, center + new Vector2(0f, size * 0.12f), size * 0.22f, size * 0.14f, color);
				break;
		}
	}

	private static void DrawHair(CanvasItem canvas, PersonAppearance appearance, Vector2 center, float size, Color hair)
	{
		Vector2 top = center + new Vector2(0f, -size * 0.28f);
		switch (appearance.HairStyle)
		{
			case HairStyle.Bald:
				return;
			case HairStyle.Buzz:
				DrawEllipse(canvas, top + new Vector2(0f, size * 0.10f), size * 0.30f, size * 0.18f, hair * new Color(1f, 1f, 1f, 0.55f));
				break;
			case HairStyle.Balding:
				DrawEllipse(canvas, top + new Vector2(0f, size * 0.08f), size * 0.32f, size * 0.12f, hair);
				break;
			case HairStyle.ShortAfro:
			case HairStyle.Curls:
				DrawEllipse(canvas, top + new Vector2(0f, size * 0.06f), size * 0.36f, size * 0.26f, hair);
				break;
			case HairStyle.Long:
			case HairStyle.Ponytail:
			case HairStyle.Bun:
				DrawEllipse(canvas, top + new Vector2(0f, size * 0.08f), size * 0.34f, size * 0.22f, hair);
				canvas.DrawRect(new Rect2(center.X - size * 0.22f, center.Y - size * 0.04f, size * 0.08f, size * 0.28f), hair, true);
				canvas.DrawRect(new Rect2(center.X + size * 0.14f, center.Y - size * 0.04f, size * 0.08f, size * 0.28f), hair, true);
				if (appearance.HairStyle == HairStyle.Bun)
				{
					DrawEllipse(canvas, top - new Vector2(0f, size * 0.08f), size * 0.10f, size * 0.08f, hair);
				}

				break;
			case HairStyle.Pompadour:
				DrawEllipse(canvas, top, size * 0.30f, size * 0.20f, hair);
				DrawEllipse(canvas, top + new Vector2(0f, -size * 0.06f), size * 0.18f, size * 0.10f, hair);
				break;
			default:
				DrawEllipse(canvas, top + new Vector2(0f, size * 0.08f), size * 0.33f, size * 0.20f, hair);
				break;
		}
	}

	private static void DrawAccessory(CanvasItem canvas, PersonAppearance appearance, Vector2 center, float size, Color outfit, Color accent)
	{
		switch (appearance.Accessory)
		{
			case AccessoryStyle.Glasses:
			case AccessoryStyle.Sunglasses:
				Color lens = appearance.Accessory == AccessoryStyle.Sunglasses
					? new Color(0.08f, 0.09f, 0.10f, 0.82f)
					: new Color(0.70f, 0.82f, 0.90f, 0.28f);
				float y = center.Y - size * 0.08f;
				DrawEllipse(canvas, center + new Vector2(-size * 0.11f, y), size * 0.07f, size * 0.045f, lens);
				DrawEllipse(canvas, center + new Vector2(size * 0.11f, y), size * 0.07f, size * 0.045f, lens);
				canvas.DrawLine(center + new Vector2(-size * 0.04f, y), center + new Vector2(size * 0.04f, y), new Color(0.12f, 0.12f, 0.14f), size * 0.01f);
				break;
			case AccessoryStyle.Cap:
			case AccessoryStyle.Visor:
				Color cap = outfit.Lerp(accent, 0.25f);
				Vector2 capCenter = center + new Vector2(0f, -size * 0.26f);
				DrawEllipse(canvas, capCenter, size * 0.34f, size * 0.12f, cap);
				if (appearance.Accessory == AccessoryStyle.Cap)
				{
					canvas.DrawRect(new Rect2(center.X - size * 0.02f, capCenter.Y - size * 0.16f, size * 0.28f, size * 0.07f), cap.Darkened(0.1f), true);
				}

				break;
		}
	}

	private static void DrawEllipse(CanvasItem canvas, Vector2 center, float radiusX, float radiusY, Color color)
	{
		const int points = 20;
		var polygon = new Vector2[points];
		for (int i = 0; i < points; i++)
		{
			float angle = i / (float)points * Mathf.Tau;
			polygon[i] = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
		}

		canvas.DrawColoredPolygon(polygon, color);
	}
}
