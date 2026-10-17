namespace HSBM.Domain;

/// <summary>
/// Saved portrait parameters for any fictional person (coach, player, staff).
/// Renderers reconstruct the image from these fields, never from a bitmap.
/// </summary>
public sealed class PersonAppearance : IEquatable<PersonAppearance>
{
	public int Seed { get; set; }
	public SkinTone SkinTone { get; set; }
	public FaceShape FaceShape { get; set; }
	public HairStyle HairStyle { get; set; }
	public HairColor HairColor { get; set; }
	public EyeStyle EyeStyle { get; set; }
	public EyeColor EyeColor { get; set; }
	public BrowStyle BrowStyle { get; set; }
	public FacialHairStyle FacialHairStyle { get; set; }
	public HairColor FacialHairColor { get; set; }
	public NoseStyle NoseStyle { get; set; }
	public MouthStyle MouthStyle { get; set; }
	public AccessoryStyle Accessory { get; set; }
	public BodyBuild BodyBuild { get; set; }
	public OutfitStyle OutfitStyle { get; set; }
	public OutfitColorway OutfitColorway { get; set; }

	/// <summary>
	/// Packed ARGB. Zero means unset. Used after a school is selected.
	/// </summary>
	public int TeamPrimaryArgb { get; set; }

	public int TeamSecondaryArgb { get; set; }

	public PersonAppearance Clone() => new()
	{
		Seed = Seed,
		SkinTone = SkinTone,
		FaceShape = FaceShape,
		HairStyle = HairStyle,
		HairColor = HairColor,
		EyeStyle = EyeStyle,
		EyeColor = EyeColor,
		BrowStyle = BrowStyle,
		FacialHairStyle = FacialHairStyle,
		FacialHairColor = FacialHairColor,
		NoseStyle = NoseStyle,
		MouthStyle = MouthStyle,
		Accessory = Accessory,
		BodyBuild = BodyBuild,
		OutfitStyle = OutfitStyle,
		OutfitColorway = OutfitColorway,
		TeamPrimaryArgb = TeamPrimaryArgb,
		TeamSecondaryArgb = TeamSecondaryArgb,
	};

	public bool Equals(PersonAppearance? other)
	{
		if (other is null)
		{
			return false;
		}

		return Seed == other.Seed
			&& SkinTone == other.SkinTone
			&& FaceShape == other.FaceShape
			&& HairStyle == other.HairStyle
			&& HairColor == other.HairColor
			&& EyeStyle == other.EyeStyle
			&& EyeColor == other.EyeColor
			&& BrowStyle == other.BrowStyle
			&& FacialHairStyle == other.FacialHairStyle
			&& FacialHairColor == other.FacialHairColor
			&& NoseStyle == other.NoseStyle
			&& MouthStyle == other.MouthStyle
			&& Accessory == other.Accessory
			&& BodyBuild == other.BodyBuild
			&& OutfitStyle == other.OutfitStyle
			&& OutfitColorway == other.OutfitColorway
			&& TeamPrimaryArgb == other.TeamPrimaryArgb
			&& TeamSecondaryArgb == other.TeamSecondaryArgb;
	}

	public override bool Equals(object? obj) => Equals(obj as PersonAppearance);

	public override int GetHashCode()
	{
		HashCode hash = new();
		hash.Add(Seed);
		hash.Add(SkinTone);
		hash.Add(FaceShape);
		hash.Add(HairStyle);
		hash.Add(HairColor);
		hash.Add(EyeStyle);
		hash.Add(EyeColor);
		hash.Add(BrowStyle);
		hash.Add(FacialHairStyle);
		hash.Add(FacialHairColor);
		hash.Add(NoseStyle);
		hash.Add(MouthStyle);
		hash.Add(Accessory);
		hash.Add(BodyBuild);
		hash.Add(OutfitStyle);
		hash.Add(OutfitColorway);
		hash.Add(TeamPrimaryArgb);
		hash.Add(TeamSecondaryArgb);
		return hash.ToHashCode();
	}
}
