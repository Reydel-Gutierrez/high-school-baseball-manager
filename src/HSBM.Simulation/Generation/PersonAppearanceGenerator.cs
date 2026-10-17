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
		};
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
		};
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
