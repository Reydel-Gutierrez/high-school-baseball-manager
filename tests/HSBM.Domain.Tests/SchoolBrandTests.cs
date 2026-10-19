using System.Globalization;
using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class SchoolBrandTests
{
	[Fact]
	public void Every_brand_belongs_to_a_catalog_school()
	{
		foreach (SchoolBrand brand in SchoolBrandCatalog.All)
		{
			OrganizationIdentity org = LeagueCatalog.Standard.GetOrganization(brand.OrganizationId);
			Assert.False(string.IsNullOrWhiteSpace(org.ShortName));
		}
	}

	[Fact]
	public void Brand_colors_are_six_digit_hex()
	{
		foreach (SchoolBrand brand in SchoolBrandCatalog.All)
		{
			foreach (string hex in new[] { brand.PrimaryHex, brand.SecondaryHex, brand.AccentHex })
			{
				Assert.Matches("^#[0-9A-Fa-f]{6}$", hex);
				Assert.True(int.TryParse(hex[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _));
			}
		}
	}

	[Fact]
	public void Branded_schools_have_a_home_and_a_story()
	{
		Assert.NotEmpty(SchoolBrandCatalog.All);
		foreach (SchoolBrand brand in SchoolBrandCatalog.All)
		{
			Assert.False(string.IsNullOrWhiteSpace(brand.Mascot));
			Assert.Equal(2, brand.StateCode.Length);
			Assert.InRange(brand.Founded, 1800, 2026);
			Assert.True(brand.Enrollment > 0);
			Assert.False(string.IsNullOrWhiteSpace(brand.History));
			Assert.InRange(brand.CapMonogram.Length, 1, 2);
		}
	}

	[Fact]
	public void Northfield_is_a_real_high_school_name_now()
	{
		Assert.Equal("Northfield High School", LeagueCatalog.Standard.GetOrganization("northfield").Name);
		Assert.Equal("Northfield, MI", SchoolBrandCatalog.TryGet("northfield")!.LocationShort);
		Assert.Null(SchoolBrandCatalog.TryGet("not-a-school"));
	}
}
