using System.Text.Json;
using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class CoachIdentityTests
{
	[Fact]
	public void Valid_coach_passes()
	{
		CoachIdentityValidation result = CoachIdentity.Validate(ValidCoach());
		Assert.True(result.IsValid);
		Assert.Empty(result.Errors);
	}

	[Fact]
	public void Missing_names_and_hometown_fail()
	{
		var coach = new CoachProfile
		{
			FirstName = "  ",
			LastName = "",
			Age = CoachIdentity.DefaultAge,
			Hometown = " ",
		};

		CoachIdentityValidation result = CoachIdentity.Validate(coach);
		Assert.False(result.IsValid);
		Assert.Equal(3, result.Errors.Count);
	}

	[Fact]
	public void Age_outside_range_fails()
	{
		CoachProfile young = ValidCoach();
		young.Age = 18;
		Assert.False(CoachIdentity.Validate(young).IsValid);

		CoachProfile old = ValidCoach();
		old.Age = 81;
		Assert.False(CoachIdentity.Validate(old).IsValid);
	}

	[Fact]
	public void Clamp_age_stays_inside_bounds()
	{
		Assert.Equal(CoachIdentity.MinAge, CoachIdentity.ClampAge(12));
		Assert.Equal(CoachIdentity.MaxAge, CoachIdentity.ClampAge(120));
		Assert.Equal(34, CoachIdentity.ClampAge(34));
	}

	[Fact]
	public void Age_limits_are_centralized()
	{
		Assert.Equal(21, CoachIdentity.MinAge);
		Assert.Equal(80, CoachIdentity.MaxAge);
	}

	private static CoachProfile ValidCoach() => new()
	{
		Id = Guid.NewGuid(),
		FirstName = "Reydel",
		LastName = "Gutierrez",
		Age = 34,
		Hometown = "Miami, FL",
	};
}

public class PersonAppearanceSerializationTests
{
	[Fact]
	public void Json_round_trip_keeps_every_field()
	{
		var original = new PersonAppearance
		{
			Seed = 738291,
			SkinTone = SkinTone.Tan,
			FaceShape = FaceShape.Square,
			HairStyle = HairStyle.Fade,
			HairColor = HairColor.DarkBrown,
			EyeStyle = EyeStyle.Almond,
			EyeColor = EyeColor.Hazel,
			BrowStyle = BrowStyle.Thick,
			FacialHairStyle = FacialHairStyle.ShortBeard,
			FacialHairColor = HairColor.DarkBrown,
			NoseStyle = NoseStyle.Straight,
			MouthStyle = MouthStyle.SlightSmile,
			Accessory = AccessoryStyle.Glasses,
			BodyBuild = BodyBuild.Athletic,
			OutfitStyle = OutfitStyle.TeamWindbreaker,
			OutfitColorway = OutfitColorway.Neutral,
			TeamPrimaryArgb = unchecked((int)0xFF1C3A6E),
			TeamSecondaryArgb = unchecked((int)0xFFE8E4DC),
		};

		string json = JsonSerializer.Serialize(original);
		PersonAppearance? restored = JsonSerializer.Deserialize<PersonAppearance>(json);
		Assert.NotNull(restored);
		Assert.Equal(original, restored);
		Assert.Contains("\"seed\":738291", json, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Clone_does_not_alias_the_original()
	{
		var original = new PersonAppearance { Seed = 9, HairStyle = HairStyle.Buzz };
		PersonAppearance copy = original.Clone();
		copy.HairStyle = HairStyle.Bald;
		Assert.Equal(HairStyle.Buzz, original.HairStyle);
	}
}
