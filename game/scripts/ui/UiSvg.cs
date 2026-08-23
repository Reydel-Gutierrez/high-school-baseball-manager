using System.Globalization;
using Godot;

/// <summary>
/// Rasterizes SVG icons at a high pixel density so window stretch does
/// not upscale a 16px import into a blurry glyph.
/// </summary>
public static class UiSvg
{
	public static Texture2D Load(string path, float rasterPixels = 256f)
	{
		var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return GD.Load<Texture2D>(path);
		}

		string svg = file.GetAsText();
		file.Dispose();

		float native = NativeSize(svg);
		float scale = rasterPixels / native;
		if (scale < 1f)
		{
			scale = 1f;
		}

		var image = new Image();
		Error err = image.LoadSvgFromString(svg, scale);
		if (err != Error.Ok || image.IsEmpty())
		{
			return GD.Load<Texture2D>(path);
		}

		image.GenerateMipmaps();
		return ImageTexture.CreateFromImage(image);
	}

	private static float NativeSize(string svg)
	{
		int i = svg.IndexOf("width=\"", System.StringComparison.Ordinal);
		if (i < 0)
		{
			i = svg.IndexOf("width='", System.StringComparison.Ordinal);
		}

		if (i < 0)
		{
			return 16f;
		}

		int start = i + 7;
		int end = start;
		while (end < svg.Length && (char.IsDigit(svg[end]) || svg[end] == '.'))
		{
			end++;
		}

		if (end == start
			|| !float.TryParse(
				svg.AsSpan(start, end - start),
				NumberStyles.Float,
				CultureInfo.InvariantCulture,
				out float width)
			|| width <= 0f)
		{
			return 16f;
		}

		return width;
	}
}
