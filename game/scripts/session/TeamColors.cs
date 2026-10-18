using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// School colors for jerseys, cards and headshot backdrops. Schools with a
/// logo get their colors sampled from it; the rest get a classic high
/// school combination picked deterministically from their id.
/// </summary>
public static class TeamColors
{
	private static readonly (Color Primary, Color Secondary)[] Classic =
	[
		(new Color(0.07f, 0.14f, 0.32f), new Color(0.95f, 0.70f, 0.16f)), // navy / gold
		(new Color(0.60f, 0.07f, 0.10f), new Color(0.95f, 0.95f, 0.95f)), // cardinal / white
		(new Color(0.05f, 0.30f, 0.17f), new Color(0.96f, 0.80f, 0.20f)), // forest / gold
		(new Color(0.10f, 0.28f, 0.66f), new Color(0.95f, 0.95f, 0.95f)), // royal / white
		(new Color(0.40f, 0.07f, 0.13f), new Color(0.90f, 0.72f, 0.30f)), // maroon / old gold
		(new Color(0.08f, 0.08f, 0.09f), new Color(0.95f, 0.46f, 0.10f)), // black / orange
		(new Color(0.30f, 0.12f, 0.48f), new Color(0.98f, 0.80f, 0.18f)), // purple / gold
		(new Color(0.03f, 0.40f, 0.22f), new Color(0.95f, 0.95f, 0.95f)), // kelly / white
		(new Color(0.75f, 0.10f, 0.12f), new Color(0.10f, 0.10f, 0.12f)), // red / black
		(new Color(0.08f, 0.20f, 0.40f), new Color(0.75f, 0.10f, 0.14f)), // navy / red
		(new Color(0.00f, 0.38f, 0.45f), new Color(0.95f, 0.95f, 0.95f)), // teal / white
		(new Color(0.85f, 0.42f, 0.05f), new Color(0.08f, 0.08f, 0.10f)), // orange / black
	];

	private static readonly Dictionary<string, (Color, Color)> Cache = new(StringComparer.Ordinal);

	public static Color Primary(string? organizationId) => For(organizationId).Primary;

	public static Color Secondary(string? organizationId) => For(organizationId).Secondary;

	public static (Color Primary, Color Secondary) For(string? organizationId)
	{
		string id = string.IsNullOrWhiteSpace(organizationId) ? "" : SchoolProfiles.NormalizeId(organizationId);
		if (Cache.TryGetValue(id, out (Color, Color) hit))
		{
			return hit;
		}

		(Color, Color) colors = FromLogo(id) ?? Pick(id);
		Cache[id] = colors;
		return colors;
	}

	private static (Color, Color) Pick(string id)
	{
		unchecked
		{
			int hash = 17;
			foreach (char c in id)
			{
				hash = hash * 31 + c;
			}

			return Classic[(int)((uint)hash % (uint)Classic.Length)];
		}
	}

	/// <summary>Two most common saturated colors in the school's logo.</summary>
	private static (Color, Color)? FromLogo(string id)
	{
		Texture2D? logo = TeamLogos.Load(id);
		if (logo == null)
		{
			return null;
		}

		Image image = logo.GetImage();
		if (image == null || image.IsEmpty())
		{
			return null;
		}

		if (image.IsCompressed())
		{
			image.Decompress();
		}

		var bins = new Dictionary<int, (int Count, float R, float G, float B)>();
		int step = Math.Max(1, image.GetWidth() / 64);
		for (int y = 0; y < image.GetHeight(); y += step)
		{
			for (int x = 0; x < image.GetWidth(); x += step)
			{
				Color c = image.GetPixel(x, y);
				if (c.A < 0.6f)
				{
					continue;
				}

				float max = Mathf.Max(c.R, Mathf.Max(c.G, c.B));
				float min = Mathf.Min(c.R, Mathf.Min(c.G, c.B));
				bool neutral = max - min < 0.12f;
				int key = neutral
					? (max > 0.8f ? 1000 : max < 0.2f ? 1001 : 1002)
					: (int)(c.H * 12) * 10 + (int)(c.V * 4);
				bins.TryGetValue(key, out (int Count, float R, float G, float B) b);
				bins[key] = (b.Count + 1, b.R + c.R, b.G + c.G, b.B + c.B);
			}
		}

		var ranked = new List<KeyValuePair<int, (int Count, float R, float G, float B)>>(bins);
		ranked.Sort((a, b) => b.Value.Count.CompareTo(a.Value.Count));
		Color? primary = null;
		Color? secondary = null;
		foreach (KeyValuePair<int, (int Count, float R, float G, float B)> kv in ranked)
		{
			var avg = new Color(kv.Value.R / kv.Value.Count, kv.Value.G / kv.Value.Count, kv.Value.B / kv.Value.Count);
			if (kv.Key == 1000 && primary != null && secondary == null)
			{
				secondary = avg;
				continue;
			}

			if (kv.Key >= 1000)
			{
				continue;
			}

			if (primary == null)
			{
				primary = avg;
			}
			else if (secondary == null && Mathf.Abs(avg.H - primary.Value.H) > 0.08f)
			{
				secondary = avg;
			}
		}

		if (primary == null)
		{
			return null;
		}

		Color p = primary.Value;
		if (p.V > 0.75f)
		{
			p = p.Darkened(0.35f);
		}

		return (p, secondary ?? new Color(0.95f, 0.95f, 0.95f));
	}
}
