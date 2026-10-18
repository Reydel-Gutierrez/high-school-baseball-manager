using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;
using HSBM.Domain;

/// <summary>One adjustable feature in the face creator.</summary>
public sealed record FaceSculptSlot(string Label, string Group, string[] Negative, string[] Positive, bool Heritage = false);

/// <summary>
/// The sculpt sliders, in the order they are stored in
/// <see cref="PersonAppearance.Sculpt"/>. Never reorder: saves index into this list.
/// </summary>
public static class FaceSculpt
{
	public static readonly FaceSculptSlot[] Slots =
	[
		new("Head Width", "Head", ["head-scale-horiz-decr"], ["head-scale-horiz-incr"]),
		new("Head Length", "Head", ["head-scale-vert-decr"], ["head-scale-vert-incr"]),
		new("Jaw", "Jaw", ["chin-bones-decr"], ["chin-bones-incr"]),
		new("Chin Length", "Jaw", ["chin-height-decr"], ["chin-height-incr"]),
		new("Chin Projection", "Jaw", ["chin-prominent-decr"], ["chin-prominent-incr"]),
		new("Cheekbones", "Cheeks", ["cheek-bones-decr"], ["cheek-bones-incr"]),
		new("Cheek Fullness", "Cheeks", ["cheek-volume-decr"], ["cheek-volume-incr"]),
		new("Brow Ridge", "Eyes", ["eyebrows-trans-backward"], ["eyebrows-trans-forward"]),
		new("Eye Size", "Eyes", ["eye-scale-decr"], ["eye-scale-incr"]),
		new("Eye Spacing", "Eyes", ["eye-trans-in"], ["eye-trans-out"]),
		new("Eye Tilt", "Eyes", ["eye-eyefold-angle-down"], ["eye-eyefold-angle-up"]),
		new("Nose Width", "Nose", ["nose-scale-horiz-decr"], ["nose-scale-horiz-incr"]),
		new("Nose Length", "Nose", ["nose-scale-vert-decr"], ["nose-scale-vert-incr"]),
		new("Nose Bridge", "Nose", ["nose-hump-decr"], ["nose-hump-incr"]),
		new("Nose Tip", "Nose", ["nose-point-down"], ["nose-point-up"]),
		new("Mouth Width", "Mouth", ["mouth-scale-horiz-decr"], ["mouth-scale-horiz-incr"]),
		new("Lip Fullness", "Mouth", ["mouth-upperlip-volume-decr", "mouth-lowerlip-volume-decr"], ["mouth-upperlip-volume-incr", "mouth-lowerlip-volume-incr"]),
		new("Ears", "Head", ["ear-scale-decr"], ["ear-scale-incr"]),
		new("Neck", "Head", ["neck-scale-horiz-decr"], ["neck-scale-horiz-incr"]),
		new("Forehead", "Head", ["forehead-scale-vert-decr"], ["forehead-scale-vert-incr"]),
		new("African", "Heritage", [], ["african"], Heritage: true),
		new("East Asian", "Heritage", [], ["asian"], Heritage: true),
		new("European", "Heritage", [], ["caucasian"], Heritage: true),
	];

	public const int HeritageStart = 20;

	public static int Count => Slots.Length;

	/// <summary>Every generated person gets a unique, stable face from their seed.</summary>
	public static int[] FromSeed(int seed, SkinTone tone)
	{
		var rng = new Random(seed ^ 0x5F3759);
		var values = new int[Count];
		for (int i = 0; i < HeritageStart; i++)
		{
			// Box-Muller: most faces are near average, a few are distinctive.
			double u1 = 1.0 - rng.NextDouble();
			double u2 = rng.NextDouble();
			double g = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
			values[i] = (int)Math.Clamp(Math.Round(g * 32), -100, 100);
		}

		(int african, int asian, int european) = HeritageFor(tone, rng);
		values[HeritageStart] = african;
		values[HeritageStart + 1] = asian;
		values[HeritageStart + 2] = european;
		return values;
	}

	public static (int African, int Asian, int European) HeritageFor(SkinTone tone, Random rng)
	{
		double r = rng.NextDouble();
		return tone switch
		{
			SkinTone.Fair or SkinTone.Light => r < 0.12 ? (0, 70, 30) : (0, (int)(rng.NextDouble() * 12), 100),
			SkinTone.LightMedium => r < 0.35 ? (0, 85, 15) : (8, (int)(rng.NextDouble() * 20), 85),
			SkinTone.Medium => r < 0.30 ? (5, 75, 20) : (18, 22, 70),
			SkinTone.Tan => r < 0.20 ? (10, 60, 30) : (35, 20, 55),
			SkinTone.Brown => (70, 0, 30),
			_ => (92, 0, 8),
		};
	}
}

/// <summary>
/// Everything the headshot studio needs to draw one person: morph weights
/// for the 3D head plus colors and grooming for the shaders.
/// </summary>
public sealed class FaceRecipe
{
	public Dictionary<string, float> Weights { get; } = new(StringComparer.Ordinal);
	public Color Skin;
	public Color Hair;
	public Color Brow;
	public Color Iris;
	public Vector4 HairShape;
	public float HairLength;
	public Vector4 Beard;
	public Color Jersey;
	public Color Trim;
	public bool ShowJersey = true;
	public float AgeLines;
	public string Key = "";

	/// <summary>Bump when shaders or framing change so cached headshots re-render.</summary>
	public const string RenderVersion = "v3;";

	public static FaceRecipe From(PersonAppearance appearance, int age, int heightInches, int weightLbs, Color? jersey = null, Color? trim = null)
	{
		var r = new FaceRecipe();
		int[] sculpt = appearance.Sculpt is { Length: > 0 } s && s.Length == FaceSculpt.Count
			? s
			: FaceSculpt.FromSeed(appearance.Seed, appearance.SkinTone);

		// Macro shape: heritage x age, then muscle x weight.
		double afr = Math.Max(0, sculpt[FaceSculpt.HeritageStart]);
		double asi = Math.Max(0, sculpt[FaceSculpt.HeritageStart + 1]);
		double eur = Math.Max(0, sculpt[FaceSculpt.HeritageStart + 2]);
		double total = afr + asi + eur;
		if (total <= 0)
		{
			eur = 1;
			total = 1;
		}

		(double child, double young, double old) = AgeWeights(age);
		void Macro(string race, double share)
		{
			if (share <= 0)
			{
				return;
			}

			Add(r, $"macro-{race}-male-child", share * child);
			Add(r, $"macro-{race}-male-young", share * young);
			Add(r, $"macro-{race}-male-old", share * old);
		}

		Macro("african", afr / total);
		Macro("asian", asi / total);
		Macro("caucasian", eur / total);

		double muscle = appearance.BodyBuild switch
		{
			BodyBuild.Slim => 0.38,
			BodyBuild.Athletic => 0.72,
			BodyBuild.Stocky => 0.62,
			_ => 0.5,
		};
		double weight = 0.5;
		if (heightInches > 0 && weightLbs > 0)
		{
			double bmi = 703.0 * weightLbs / (heightInches * heightInches);
			weight = Math.Clamp(0.5 + (bmi - 22.5) / 13.0, 0.05, 0.98);
		}
		else if (appearance.BodyBuild == BodyBuild.Stocky)
		{
			weight = 0.68;
		}

		(double mMin, double mAvg, double mMax) = Tri(muscle);
		(double wMin, double wAvg, double wMax) = Tri(weight);
		foreach ((string ageName, double aw) in new[] { ("child", child), ("young", young), ("old", old) })
		{
			foreach ((string mName, double mw) in new[] { ("minmuscle", mMin), ("averagemuscle", mAvg), ("maxmuscle", mMax) })
			{
				foreach ((string wName, double ww) in new[] { ("minweight", wMin), ("averageweight", wAvg), ("maxweight", wMax) })
				{
					Add(r, $"universal-male-{ageName}-{mName}-{wName}", aw * mw * ww);
				}
			}
		}

		// Sculpt sliders.
		for (int i = 0; i < FaceSculpt.HeritageStart; i++)
		{
			FaceSculptSlot slot = FaceSculpt.Slots[i];
			float v = sculpt[i] / 100f;
			foreach (string t in v < 0 ? slot.Negative : slot.Positive)
			{
				Add(r, t, Math.Abs(v));
			}
		}

		// Catalog features layer on top of the sculpt.
		switch (appearance.FaceShape)
		{
			case FaceShape.Oval: Add(r, "head-oval", 0.45); break;
			case FaceShape.Round: Add(r, "head-round", 0.55); break;
			case FaceShape.Square: Add(r, "head-square", 0.6); break;
			case FaceShape.Long: Add(r, "head-scale-vert-incr", 0.45); break;
			case FaceShape.Heart: Add(r, "head-invertedtriangular", 0.5); break;
			case FaceShape.Diamond: Add(r, "head-diamond", 0.5); break;
		}

		switch (appearance.NoseStyle)
		{
			case NoseStyle.Button: Add(r, "nose-point-up", 0.5); Add(r, "nose-scale-vert-decr", 0.3); break;
			case NoseStyle.Wide: Add(r, "nose-scale-horiz-incr", 0.45); Add(r, "nose-flaring-incr", 0.4); break;
			case NoseStyle.Long: Add(r, "nose-scale-vert-incr", 0.45); break;
		}

		switch (appearance.MouthStyle)
		{
			case MouthStyle.SlightSmile: Add(r, "mouth-angles-up", 0.45); break;
			case MouthStyle.Wide: Add(r, "mouth-scale-horiz-incr", 0.45); break;
			case MouthStyle.Thin: Add(r, "mouth-upperlip-volume-decr", 0.6); Add(r, "mouth-lowerlip-volume-decr", 0.5); break;
		}

		switch (appearance.EyeStyle)
		{
			case EyeStyle.Round: Add(r, "eye-height2-incr", 0.5); break;
			case EyeStyle.Narrow: Add(r, "eye-height2-decr", 0.5); break;
			case EyeStyle.DeepSet: Add(r, "eye-push1-in", 0.6); Add(r, "eyebrows-trans-forward", 0.3); break;
		}

		if (age >= 40)
		{
			Add(r, "head-age-incr", Math.Clamp((age - 40) / 30.0, 0, 1));
			r.AgeLines = (float)Math.Clamp((age - 35) / 30.0, 0, 1);
		}

		r.Skin = SkinColor(appearance.SkinTone, appearance.Seed);
		r.Hair = HairColorOf(appearance.HairColor, age);
		r.Brow = HairColorOf(appearance.HairColor, Math.Min(age, 45)).Darkened(0.15f);
		r.Iris = IrisColor(appearance.EyeColor);
		(r.HairShape, r.HairLength) = HairStyleOf(appearance.HairStyle);
		r.Beard = BeardOf(appearance.FacialHairStyle);
		Color beardColor = HairColorOf(appearance.FacialHairColor, age);
		if (r.Beard != Vector4.Zero)
		{
			r.Hair = r.HairShape.Y <= 0.01f ? beardColor : r.Hair;
		}

		r.Jersey = jersey ?? (appearance.TeamPrimaryArgb != 0 ? Argb(appearance.TeamPrimaryArgb) : new Color(0.10f, 0.13f, 0.20f));
		r.Trim = trim ?? (appearance.TeamSecondaryArgb != 0 ? Argb(appearance.TeamSecondaryArgb) : new Color(0.85f, 0.85f, 0.88f));
		r.Key = BuildKey(r);
		return r;
	}

	public static Color Argb(int argb) => new(
		((argb >> 16) & 0xFF) / 255f,
		((argb >> 8) & 0xFF) / 255f,
		(argb & 0xFF) / 255f,
		1f);

	private static void Add(FaceRecipe r, string name, double weight)
	{
		if (weight <= 0.0005)
		{
			return;
		}

		r.Weights[name] = (r.Weights.TryGetValue(name, out float w) ? w : 0f) + (float)weight;
	}

	public static (double Child, double Young, double Old) AgeWeights(int age)
	{
		if (age <= 11)
		{
			return (1, 0, 0);
		}

		if (age <= 25)
		{
			double t = (age - 11) / 14.0;
			return (1 - t, t, 0);
		}

		double o = Math.Min(1, (age - 25) / 65.0);
		return (0, 1 - o, o);
	}

	private static (double Min, double Avg, double Max) Tri(double v)
	{
		if (v < 0.5)
		{
			double m = (0.5 - v) * 2;
			return (m, 1 - m, 0);
		}

		double x = (v - 0.5) * 2;
		return (0, 1 - x, x);
	}

	public static Color SkinColor(SkinTone tone, int seed)
	{
		Color c = tone switch
		{
			SkinTone.Fair => new Color(0.93f, 0.78f, 0.69f),
			SkinTone.Light => new Color(0.88f, 0.70f, 0.58f),
			SkinTone.LightMedium => new Color(0.82f, 0.62f, 0.48f),
			SkinTone.Medium => new Color(0.74f, 0.54f, 0.40f),
			SkinTone.Tan => new Color(0.64f, 0.45f, 0.31f),
			SkinTone.Brown => new Color(0.50f, 0.33f, 0.22f),
			SkinTone.Deep => new Color(0.38f, 0.24f, 0.16f),
			_ => new Color(0.28f, 0.18f, 0.12f),
		};
		// small per-person variation so two kids of the same tone aren't identical
		var rng = new Random(seed ^ 0x2C1B);
		float k = 1f + (float)(rng.NextDouble() - 0.5) * 0.06f;
		float warm = (float)(rng.NextDouble() - 0.5) * 0.03f;
		return new Color(Mathf.Clamp(c.R * k + warm, 0, 1), Mathf.Clamp(c.G * k, 0, 1), Mathf.Clamp(c.B * k - warm, 0, 1));
	}

	public static Color HairColorOf(HairColor hair, int age)
	{
		Color c = hair switch
		{
			HairColor.Black => new Color(0.04f, 0.035f, 0.03f),
			HairColor.DarkBrown => new Color(0.12f, 0.08f, 0.05f),
			HairColor.Brown => new Color(0.25f, 0.16f, 0.09f),
			HairColor.Auburn => new Color(0.33f, 0.14f, 0.07f),
			HairColor.Blonde => new Color(0.62f, 0.50f, 0.32f),
			HairColor.Copper => new Color(0.52f, 0.24f, 0.10f),
			HairColor.Gray => new Color(0.58f, 0.57f, 0.56f),
			_ => new Color(0.34f, 0.33f, 0.32f),
		};
		if (age >= 45 && hair is not (HairColor.Gray or HairColor.SaltAndPepper))
		{
			c = c.Lerp(new Color(0.45f, 0.44f, 0.43f), Mathf.Clamp((age - 45) / 30f, 0, 0.6f));
		}

		return c;
	}

	private static Color IrisColor(EyeColor eye) => eye switch
	{
		EyeColor.Hazel => new Color(0.38f, 0.30f, 0.14f),
		EyeColor.Green => new Color(0.25f, 0.38f, 0.22f),
		EyeColor.Blue => new Color(0.30f, 0.45f, 0.58f),
		EyeColor.Dark => new Color(0.12f, 0.07f, 0.04f),
		EyeColor.Gray => new Color(0.42f, 0.46f, 0.50f),
		_ => new Color(0.30f, 0.18f, 0.09f),
	};

	private static (Vector4 Style, float Length) HairStyleOf(HairStyle style) => style switch
	{
		HairStyle.Buzz => (new Vector4(0.02f, 0.85f, 0.3f, 0f), 0.002f),
		HairStyle.ShortCrop => (new Vector4(0f, 0.95f, 0.2f, 0f), 0.005f),
		HairStyle.Fade => (new Vector4(0f, 1f, 0.9f, 0f), 0.006f),
		HairStyle.SidePart => (new Vector4(0f, 1f, 0.3f, 0f), 0.008f),
		HairStyle.Waves => (new Vector4(0f, 1f, 0.5f, 0f), 0.004f),
		HairStyle.Pompadour => (new Vector4(0f, 1f, 0.6f, 0f), 0.011f),
		HairStyle.ShortAfro => (new Vector4(0f, 1f, 0.2f, 0f), 0.013f),
		HairStyle.Curls => (new Vector4(0f, 1f, 0.2f, 0f), 0.011f),
		HairStyle.Long => (new Vector4(0f, 1f, 0f, 0f), 0.011f),
		HairStyle.Balding => (new Vector4(0.10f, 0.9f, 0.4f, 0.75f), 0.003f),
		HairStyle.Bald => (new Vector4(0f, 0f, 0f, 0f), 0f),
		_ => (new Vector4(0f, 1f, 0f, 0f), 0.009f),
	};

	private static Vector4 BeardOf(FacialHairStyle style) => style switch
	{
		FacialHairStyle.Stubble => new Vector4(0.35f, 0f, 0f, 0f),
		FacialHairStyle.Mustache => new Vector4(0.08f, 0f, 1f, 0f),
		FacialHairStyle.Goatee => new Vector4(0.08f, 0f, 0.9f, 1f),
		FacialHairStyle.ShortBeard => new Vector4(0f, 0.7f, 0.9f, 0.9f),
		FacialHairStyle.FullBeard => new Vector4(0f, 1f, 1f, 1f),
		FacialHairStyle.SoulPatch => new Vector4(0f, 0f, 0f, 0.5f),
		_ => Vector4.Zero,
	};

	private static string BuildKey(FaceRecipe r)
	{
		var sb = new StringBuilder(RenderVersion);
		var names = new List<string>(r.Weights.Keys);
		names.Sort(StringComparer.Ordinal);
		foreach (string n in names)
		{
			sb.Append(n).Append('=').Append(r.Weights[n].ToString("0.000", CultureInfo.InvariantCulture)).Append(';');
		}

		sb.Append(r.Skin.ToHtml()).Append(r.Hair.ToHtml()).Append(r.Brow.ToHtml()).Append(r.Iris.ToHtml())
			.Append(r.HairShape).Append(r.HairLength.ToString(CultureInfo.InvariantCulture)).Append(r.Beard)
			.Append(r.Jersey.ToHtml()).Append(r.Trim.ToHtml()).Append(r.ShowJersey).Append(r.AgeLines.ToString("0.00", CultureInfo.InvariantCulture));
		ulong h = 1469598103934665603UL;
		foreach (char c in sb.ToString())
		{
			h = (h ^ c) * 1099511628211UL;
		}

		return h.ToString("x16", CultureInfo.InvariantCulture);
	}
}
