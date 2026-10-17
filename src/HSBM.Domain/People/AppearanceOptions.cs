namespace HSBM.Domain;

/// <summary>
/// Layered portrait traits. Integer-backed so SQLite and JSON can store
/// the configuration instead of a screenshot.
/// </summary>
public enum SkinTone
{
	Fair = 0,
	Light = 1,
	LightMedium = 2,
	Medium = 3,
	Tan = 4,
	Brown = 5,
	Deep = 6,
	Dark = 7,
}

public enum FaceShape
{
	Oval = 0,
	Round = 1,
	Square = 2,
	Long = 3,
	Heart = 4,
	Diamond = 5,
}

public enum HairStyle
{
	Buzz = 0,
	ShortCrop = 1,
	Fade = 2,
	SidePart = 3,
	Waves = 4,
	Pompadour = 5,
	ShortAfro = 6,
	Curls = 7,
	Long = 8,
	Balding = 9,
	Bald = 10,
	Bun = 11,
	Ponytail = 12,
}

public enum HairColor
{
	Black = 0,
	DarkBrown = 1,
	Brown = 2,
	Auburn = 3,
	Blonde = 4,
	Copper = 5,
	Gray = 6,
	SaltAndPepper = 7,
}

public enum EyeStyle
{
	Almond = 0,
	Round = 1,
	Narrow = 2,
	DeepSet = 3,
}

public enum EyeColor
{
	Brown = 0,
	Hazel = 1,
	Green = 2,
	Blue = 3,
	Dark = 4,
	Gray = 5,
}

public enum BrowStyle
{
	Soft = 0,
	Straight = 1,
	Arched = 2,
	Thick = 3,
}

public enum FacialHairStyle
{
	None = 0,
	Stubble = 1,
	Mustache = 2,
	Goatee = 3,
	ShortBeard = 4,
	FullBeard = 5,
	SoulPatch = 6,
}

public enum NoseStyle
{
	Straight = 0,
	Button = 1,
	Wide = 2,
	Long = 3,
}

public enum MouthStyle
{
	Neutral = 0,
	SlightSmile = 1,
	Wide = 2,
	Thin = 3,
}

public enum AccessoryStyle
{
	None = 0,
	Glasses = 1,
	Sunglasses = 2,
	Cap = 3,
	Visor = 4,
}

public enum BodyBuild
{
	Slim = 0,
	Average = 1,
	Athletic = 2,
	Stocky = 3,
}

public enum OutfitStyle
{
	BaseballPolo = 0,
	TeamWindbreaker = 1,
	BaseballJersey = 2,
	TrainingShirt = 3,
}

/// <summary>
/// Neutral until a school is chosen. Team later recolors clothing
/// without changing <see cref="OutfitStyle"/>.
/// </summary>
public enum OutfitColorway
{
	Neutral = 0,
	Team = 1,
}
