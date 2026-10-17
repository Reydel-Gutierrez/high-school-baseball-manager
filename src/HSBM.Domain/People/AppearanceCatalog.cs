namespace HSBM.Domain;

/// <summary>
/// Display labels and cycling helpers for appearance enums.
/// Texture paths live in the Godot portrait catalog, not here.
/// </summary>
public static class AppearanceCatalog
{
	public static T[] All<T>() where T : struct, Enum => Enum.GetValues<T>();

	public static T Cycle<T>(T current, int delta) where T : struct, Enum
	{
		T[] values = Enum.GetValues<T>();
		int index = Array.IndexOf(values, current);
		if (index < 0)
		{
			index = 0;
		}

		int length = values.Length;
		int next = (index + (delta % length) + length) % length;
		return values[next];
	}

	public static string DisplayName(Enum value) => value switch
	{
		SkinTone tone => SkinName(tone),
		FaceShape shape => FaceName(shape),
		HairStyle hair => HairName(hair),
		HairColor color => HairColorName(color),
		EyeStyle eyes => EyeStyleName(eyes),
		EyeColor eyeColor => EyeColorName(eyeColor),
		BrowStyle brow => BrowName(brow),
		FacialHairStyle facial => FacialHairName(facial),
		NoseStyle nose => NoseName(nose),
		MouthStyle mouth => MouthName(mouth),
		AccessoryStyle accessory => AccessoryName(accessory),
		BodyBuild build => BuildName(build),
		OutfitStyle outfit => OutfitName(outfit),
		OutfitColorway colorway => colorway == OutfitColorway.Team ? "Team Colors" : "Neutral",
		_ => value.ToString(),
	};

	private static string SkinName(SkinTone tone) => tone switch
	{
		SkinTone.Fair => "Fair",
		SkinTone.Light => "Light",
		SkinTone.LightMedium => "Light Medium",
		SkinTone.Medium => "Medium",
		SkinTone.Tan => "Tan",
		SkinTone.Brown => "Brown",
		SkinTone.Deep => "Deep",
		_ => "Dark",
	};

	private static string FaceName(FaceShape shape) => shape switch
	{
		FaceShape.Oval => "Oval",
		FaceShape.Round => "Round",
		FaceShape.Square => "Square",
		FaceShape.Long => "Long",
		FaceShape.Heart => "Heart",
		_ => "Diamond",
	};

	private static string HairName(HairStyle style) => style switch
	{
		HairStyle.Buzz => "Buzz Cut",
		HairStyle.ShortCrop => "Short Crop",
		HairStyle.Fade => "Fade",
		HairStyle.SidePart => "Side Part",
		HairStyle.Waves => "Waves",
		HairStyle.Pompadour => "Pompadour",
		HairStyle.ShortAfro => "Short Afro",
		HairStyle.Curls => "Curls",
		HairStyle.Long => "Long",
		HairStyle.Balding => "Balding",
		HairStyle.Bald => "Bald",
		HairStyle.Bun => "Bun",
		_ => "Ponytail",
	};

	private static string HairColorName(HairColor color) => color switch
	{
		HairColor.Black => "Black",
		HairColor.DarkBrown => "Dark Brown",
		HairColor.Brown => "Brown",
		HairColor.Auburn => "Auburn",
		HairColor.Blonde => "Blonde",
		HairColor.Copper => "Copper",
		HairColor.Gray => "Gray",
		_ => "Salt and Pepper",
	};

	private static string EyeStyleName(EyeStyle style) => style switch
	{
		EyeStyle.Almond => "Almond",
		EyeStyle.Round => "Round",
		EyeStyle.Narrow => "Narrow",
		_ => "Deep Set",
	};

	private static string EyeColorName(EyeColor color) => color switch
	{
		EyeColor.Brown => "Brown",
		EyeColor.Hazel => "Hazel",
		EyeColor.Green => "Green",
		EyeColor.Blue => "Blue",
		EyeColor.Dark => "Dark",
		_ => "Gray",
	};

	private static string BrowName(BrowStyle style) => style switch
	{
		BrowStyle.Soft => "Soft",
		BrowStyle.Straight => "Straight",
		BrowStyle.Arched => "Arched",
		_ => "Thick",
	};

	private static string FacialHairName(FacialHairStyle style) => style switch
	{
		FacialHairStyle.None => "None",
		FacialHairStyle.Stubble => "Stubble",
		FacialHairStyle.Mustache => "Mustache",
		FacialHairStyle.Goatee => "Goatee",
		FacialHairStyle.ShortBeard => "Short Beard",
		FacialHairStyle.FullBeard => "Full Beard",
		_ => "Soul Patch",
	};

	private static string NoseName(NoseStyle style) => style switch
	{
		NoseStyle.Straight => "Straight",
		NoseStyle.Button => "Button",
		NoseStyle.Wide => "Wide",
		_ => "Long",
	};

	private static string MouthName(MouthStyle style) => style switch
	{
		MouthStyle.Neutral => "Neutral",
		MouthStyle.SlightSmile => "Slight Smile",
		MouthStyle.Wide => "Wide",
		_ => "Thin",
	};

	private static string AccessoryName(AccessoryStyle style) => style switch
	{
		AccessoryStyle.None => "None",
		AccessoryStyle.Glasses => "Glasses",
		AccessoryStyle.Sunglasses => "Sunglasses",
		AccessoryStyle.Cap => "Cap",
		_ => "Visor",
	};

	private static string BuildName(BodyBuild build) => build switch
	{
		BodyBuild.Slim => "Slim",
		BodyBuild.Average => "Average",
		BodyBuild.Athletic => "Athletic",
		_ => "Stocky",
	};

	private static string OutfitName(OutfitStyle style) => style switch
	{
		OutfitStyle.BaseballPolo => "Baseball Polo",
		OutfitStyle.TeamWindbreaker => "Team Windbreaker",
		OutfitStyle.BaseballJersey => "Baseball Jersey",
		_ => "Training Shirt",
	};
}
