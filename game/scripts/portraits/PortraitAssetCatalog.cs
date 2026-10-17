using HSBM.Domain;
using Godot;

/// <summary>
/// Maps appearance options to colors and future texture paths under
/// res://assets/portraits/. Missing textures fall back to procedural layers.
/// </summary>
public static class PortraitAssetCatalog
{
	public const string Root = "res://assets/portraits/";

	public static Color SkinTint(SkinTone tone) => tone switch
	{
		SkinTone.Fair => new Color(0.96f, 0.82f, 0.72f),
		SkinTone.Light => new Color(0.90f, 0.74f, 0.62f),
		SkinTone.LightMedium => new Color(0.82f, 0.64f, 0.50f),
		SkinTone.Medium => new Color(0.72f, 0.52f, 0.38f),
		SkinTone.Tan => new Color(0.62f, 0.42f, 0.28f),
		SkinTone.Brown => new Color(0.48f, 0.30f, 0.20f),
		SkinTone.Deep => new Color(0.34f, 0.20f, 0.14f),
		_ => new Color(0.22f, 0.14f, 0.10f),
	};

	public static Color HairTint(HairColor color) => color switch
	{
		HairColor.Black => new Color(0.10f, 0.09f, 0.08f),
		HairColor.DarkBrown => new Color(0.22f, 0.13f, 0.08f),
		HairColor.Brown => new Color(0.36f, 0.22f, 0.12f),
		HairColor.Auburn => new Color(0.48f, 0.20f, 0.10f),
		HairColor.Blonde => new Color(0.82f, 0.68f, 0.38f),
		HairColor.Copper => new Color(0.70f, 0.34f, 0.16f),
		HairColor.Gray => new Color(0.62f, 0.62f, 0.64f),
		_ => new Color(0.42f, 0.40f, 0.38f),
	};

	public static Color EyeTint(EyeColor color) => color switch
	{
		EyeColor.Brown => new Color(0.36f, 0.22f, 0.12f),
		EyeColor.Hazel => new Color(0.50f, 0.38f, 0.16f),
		EyeColor.Green => new Color(0.28f, 0.48f, 0.28f),
		EyeColor.Blue => new Color(0.28f, 0.46f, 0.70f),
		EyeColor.Dark => new Color(0.12f, 0.10f, 0.09f),
		_ => new Color(0.50f, 0.54f, 0.58f),
	};

	public static Color FromArgb(int argb)
	{
		uint value = unchecked((uint)argb);
		return Color.Color8(
			(byte)((value >> 16) & 0xFF),
			(byte)((value >> 8) & 0xFF),
			(byte)(value & 0xFF),
			(byte)((value >> 24) & 0xFF));
	}

	public static Color OutfitTint(PersonAppearance appearance)
	{
		if (appearance.OutfitColorway == OutfitColorway.Team && appearance.TeamPrimaryArgb != 0)
		{
			return FromArgb(appearance.TeamPrimaryArgb);
		}

		return appearance.OutfitStyle switch
		{
			OutfitStyle.BaseballPolo => new Color(0.14f, 0.22f, 0.38f),
			OutfitStyle.TeamWindbreaker => new Color(0.16f, 0.18f, 0.22f),
			OutfitStyle.BaseballJersey => new Color(0.78f, 0.80f, 0.84f),
			_ => new Color(0.18f, 0.32f, 0.28f),
		};
	}

	public static Color OutfitAccent(PersonAppearance appearance)
	{
		if (appearance.OutfitColorway == OutfitColorway.Team && appearance.TeamSecondaryArgb != 0)
		{
			return FromArgb(appearance.TeamSecondaryArgb);
		}

		return appearance.OutfitStyle switch
		{
			OutfitStyle.BaseballPolo => new Color(0.86f, 0.88f, 0.90f),
			OutfitStyle.TeamWindbreaker => new Color(0.72f, 0.18f, 0.18f),
			OutfitStyle.BaseballJersey => new Color(0.12f, 0.20f, 0.42f),
			_ => new Color(0.90f, 0.90f, 0.88f),
		};
	}

	public static Texture2D? TryLoad(string path)
	{
		if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
		{
			return null;
		}

		return GD.Load<Texture2D>(path);
	}
}

public interface IPortraitRenderer
{
	void Draw(CanvasItem canvas, PersonAppearance appearance, Rect2 bounds);
}

/// <summary>
/// Future backend for baked portrait sheets. Returns false until assets exist.
/// </summary>
public sealed class PreRenderedPortraitRenderer : IPortraitRenderer
{
	public bool TryDraw(CanvasItem canvas, PersonAppearance appearance, Rect2 bounds)
	{
		string path = $"{PortraitAssetCatalog.Root}prerendered/{appearance.Seed:x8}.png";
		Texture2D? texture = PortraitAssetCatalog.TryLoad(path);
		if (texture == null)
		{
			return false;
		}

		canvas.DrawTextureRect(texture, bounds, false);
		return true;
	}

	public void Draw(CanvasItem canvas, PersonAppearance appearance, Rect2 bounds) =>
		TryDraw(canvas, appearance, bounds);
}

public sealed class CompositePortraitRenderer : IPortraitRenderer
{
	private readonly PreRenderedPortraitRenderer _preRendered = new();
	private readonly LayeredPortraitRenderer _layered = new();

	public void Draw(CanvasItem canvas, PersonAppearance appearance, Rect2 bounds)
	{
		if (_preRendered.TryDraw(canvas, appearance, bounds))
		{
			return;
		}

		_layered.Draw(canvas, appearance, bounds);
	}
}
