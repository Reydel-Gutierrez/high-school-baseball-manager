using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// Deterministic appearance rolls for coaches, players, and staff.
/// The same seed always produces the same <see cref="PersonAppearance"/>.
/// </summary>
public static class PersonAppearanceGenerator
{
	public static PersonAppearance Generate(int seed)
	{
		ISeededRandom rng = new SeededRandom(seed);
		HairColor hairColor = rng.Pick(AppearanceCatalog.All<HairColor>());
		FacialHairStyle facialHair = RollFacialHair(rng);

		return new PersonAppearance
		{
			Seed = seed,
			SkinTone = rng.Pick(AppearanceCatalog.All<SkinTone>()),
			FaceShape = rng.Pick(AppearanceCatalog.All<FaceShape>()),
			HairStyle = rng.Pick(AppearanceCatalog.All<HairStyle>()),
			HairColor = hairColor,
			EyeStyle = rng.Pick(AppearanceCatalog.All<EyeStyle>()),
			EyeColor = rng.Pick(AppearanceCatalog.All<EyeColor>()),
			BrowStyle = rng.Pick(AppearanceCatalog.All<BrowStyle>()),
			FacialHairStyle = facialHair,
			FacialHairColor = facialHair == FacialHairStyle.None
				? hairColor
				: RollFacialHairColor(rng, hairColor),
			NoseStyle = rng.Pick(AppearanceCatalog.All<NoseStyle>()),
			MouthStyle = rng.Pick(AppearanceCatalog.All<MouthStyle>()),
			Accessory = RollAccessory(rng),
			BodyBuild = rng.Pick(AppearanceCatalog.All<BodyBuild>()),
			OutfitStyle = rng.Pick(AppearanceCatalog.All<OutfitStyle>()),
			OutfitColorway = OutfitColorway.Neutral,
		}.Also(a => Harmonize(a, new SeededRandom(seed).Derive(0x6E7E), player: false));
	}

	public static PersonAppearance GenerateCoach(int seed)
	{
		PersonAppearance appearance = Generate(seed);
		ISeededRandom outfitRng = new SeededRandom(seed).Derive(0x07F17);
		appearance.OutfitStyle = outfitRng.Pick(AppearanceCatalog.All<OutfitStyle>());
		appearance.OutfitColorway = OutfitColorway.Neutral;
		appearance.TeamPrimaryArgb = 0;
		appearance.TeamSecondaryArgb = 0;
		return appearance;
	}

	public static PersonAppearance GenerateFrom(ISeededRandom rng)
	{
		int seed = rng.Next(int.MinValue, int.MaxValue);
		return Generate(seed);
	}

	public static PersonAppearance GeneratePlayer(int seed, BodyBuild build)
	{
		ISeededRandom rng = new SeededRandom(seed);
		HairColor hairColor = rng.Pick(PlayerHairColors);
		FacialHairStyle facialHair = RollPlayerFacialHair(rng);

		return new PersonAppearance
		{
			Seed = seed,
			SkinTone = rng.Pick(AppearanceCatalog.All<SkinTone>()),
			FaceShape = rng.Pick(AppearanceCatalog.All<FaceShape>()),
			HairStyle = rng.Pick(PlayerHairStyles),
			HairColor = hairColor,
			EyeStyle = rng.Pick(AppearanceCatalog.All<EyeStyle>()),
			EyeColor = rng.Pick(AppearanceCatalog.All<EyeColor>()),
			BrowStyle = rng.Pick(AppearanceCatalog.All<BrowStyle>()),
			FacialHairStyle = facialHair,
			FacialHairColor = facialHair == FacialHairStyle.None
				? hairColor
				: RollFacialHairColor(rng, hairColor),
			NoseStyle = rng.Pick(AppearanceCatalog.All<NoseStyle>()),
			MouthStyle = rng.Pick(AppearanceCatalog.All<MouthStyle>()),
			Accessory = rng.NextDouble() < 0.88 ? AccessoryStyle.None : RollAccessory(rng),
			BodyBuild = build,
			OutfitStyle = OutfitStyle.BaseballJersey,
			OutfitColorway = OutfitColorway.Team,
		}.Also(a => Harmonize(a, new SeededRandom(seed).Derive(0x6E7E), player: true));
	}

	/// <summary>
	/// Real-world coherence: skin tone is drawn from a US high-school mix,
	/// then hair color, eye color and hair texture follow from it (no
	/// copper-haired, green-eyed faces on the deepest tones, and so on).
	/// Uses its own derived stream so the other rolls keep their values.
	/// </summary>
	private static void Harmonize(PersonAppearance a, ISeededRandom rng, bool player)
	{
		a.SkinTone = Weighted(rng, ToneWeights);
		bool deep = a.SkinTone >= SkinTone.Brown;
		bool olive = a.SkinTone is SkinTone.Medium or SkinTone.Tan;
		bool fair = a.SkinTone <= SkinTone.Light;
		HairColor hair = deep
			? Weighted<HairColor>(rng, [(HairColor.Black, 85), (HairColor.DarkBrown, 15)])
			: olive
				? Weighted<HairColor>(rng, [(HairColor.Black, 55), (HairColor.DarkBrown, 35), (HairColor.Brown, 10)])
				: fair
					? Weighted<HairColor>(rng, [(HairColor.Brown, 30), (HairColor.DarkBrown, 25), (HairColor.Blonde, 24), (HairColor.Auburn, 8), (HairColor.Copper, 7), (HairColor.Black, 6)])
					: Weighted<HairColor>(rng, [(HairColor.DarkBrown, 40), (HairColor.Brown, 25), (HairColor.Black, 28), (HairColor.Blonde, 7)]);
		if (!player && a.HairColor is HairColor.Gray or HairColor.SaltAndPepper)
		{
			hair = a.HairColor; // coaches keep their gray
		}

		bool beardMatched = a.FacialHairColor == a.HairColor;
		a.HairColor = hair;
		if (beardMatched || deep || olive)
		{
			a.FacialHairColor = hair;
		}

		a.EyeColor = deep
			? Weighted<EyeColor>(rng, [(EyeColor.Dark, 60), (EyeColor.Brown, 37), (EyeColor.Hazel, 3)])
			: olive
				? Weighted<EyeColor>(rng, [(EyeColor.Brown, 55), (EyeColor.Dark, 27), (EyeColor.Hazel, 15), (EyeColor.Green, 3)])
				: fair
					? Weighted<EyeColor>(rng, [(EyeColor.Blue, 34), (EyeColor.Brown, 24), (EyeColor.Hazel, 16), (EyeColor.Green, 15), (EyeColor.Gray, 11)])
					: Weighted<EyeColor>(rng, [(EyeColor.Brown, 50), (EyeColor.Hazel, 25), (EyeColor.Blue, 13), (EyeColor.Green, 12)]);

		if (player && deep && rng.NextDouble() < 0.7)
		{
			a.HairStyle = Weighted<HairStyle>(rng, [(HairStyle.Fade, 30), (HairStyle.Buzz, 20), (HairStyle.ShortAfro, 20), (HairStyle.Waves, 18), (HairStyle.Curls, 12)]);
		}
	}

	private static readonly (SkinTone, int)[] ToneWeights =
	[
		(SkinTone.Fair, 10), (SkinTone.Light, 22), (SkinTone.LightMedium, 18), (SkinTone.Medium, 14),
		(SkinTone.Tan, 12), (SkinTone.Brown, 10), (SkinTone.Deep, 8), (SkinTone.Dark, 6),
	];

	private static T Weighted<T>(ISeededRandom rng, (T Value, int Weight)[] table)
	{
		int total = 0;
		foreach ((T _, int w) in table)
		{
			total += w;
		}

		double roll = rng.NextDouble() * total;
		foreach ((T v, int w) in table)
		{
			roll -= w;
			if (roll < 0)
			{
				return v;
			}
		}

		return table[^1].Value;
	}

	private static PersonAppearance Also(this PersonAppearance a, Action<PersonAppearance> apply)
	{
		apply(a);
		return a;
	}

	private static readonly HairStyle[] PlayerHairStyles =
	[
		HairStyle.Buzz,
		HairStyle.ShortCrop,
		HairStyle.Fade,
		HairStyle.SidePart,
		HairStyle.Waves,
		HairStyle.Pompadour,
		HairStyle.ShortAfro,
		HairStyle.Curls,
		HairStyle.Long,
		HairStyle.Bun,
		HairStyle.Ponytail,
	];

	private static readonly HairColor[] PlayerHairColors =
	[
		HairColor.Black,
		HairColor.DarkBrown,
		HairColor.Brown,
		HairColor.Auburn,
		HairColor.Blonde,
		HairColor.Copper,
	];

	private static FacialHairStyle RollPlayerFacialHair(ISeededRandom rng)
	{
		double roll = rng.NextDouble();
		if (roll < 0.82)
		{
			return FacialHairStyle.None;
		}

		if (roll < 0.94)
		{
			return FacialHairStyle.Stubble;
		}

		return rng.NextDouble() < 0.5 ? FacialHairStyle.Mustache : FacialHairStyle.SoulPatch;
	}

	private static FacialHairStyle RollFacialHair(ISeededRandom rng)
	{
		double roll = rng.NextDouble();
		if (roll < 0.46)
		{
			return FacialHairStyle.None;
		}

		if (roll < 0.62)
		{
			return FacialHairStyle.Stubble;
		}

		return rng.Pick(AppearanceCatalog.All<FacialHairStyle>());
	}

	private static HairColor RollFacialHairColor(ISeededRandom rng, HairColor hairColor)
	{
		return rng.NextDouble() < 0.78 ? hairColor : rng.Pick(AppearanceCatalog.All<HairColor>());
	}

	private static AccessoryStyle RollAccessory(ISeededRandom rng)
	{
		double roll = rng.NextDouble();
		if (roll < 0.72)
		{
			return AccessoryStyle.None;
		}

		if (roll < 0.84)
		{
			return AccessoryStyle.Glasses;
		}

		if (roll < 0.90)
		{
			return AccessoryStyle.Sunglasses;
		}

		return roll < 0.96 ? AccessoryStyle.Cap : AccessoryStyle.Visor;
	}
}
