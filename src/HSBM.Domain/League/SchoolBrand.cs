namespace HSBM.Domain;

/// <summary>
/// A school's identity beyond its name: where it is, what it's called on
/// the scoreboard, its colors and mascot, its ballpark and its story. Static
/// content like <see cref="LeagueCatalog"/>; schools without a brand fall
/// back to generated colors and a monogram.
/// </summary>
public sealed record SchoolBrand(
	string OrganizationId,
	string Mascot,
	string City,
	string State,
	string StateCode,
	int Founded,
	int Enrollment,
	string Classification,
	string HomeField,
	string ColorNames,
	string PrimaryHex,
	string SecondaryHex,
	string AccentHex,
	string Abbreviation,
	string CapMonogram,
	string Motto,
	string History,
	IReadOnlyList<string> Traditions)
{
	/// <summary>"Northfield, Michigan".</summary>
	public string Location => $"{City}, {State}";

	/// <summary>"Northfield, MI".</summary>
	public string LocationShort => $"{City}, {StateCode}";
}

public static class SchoolBrandCatalog
{
	private static readonly Dictionary<string, SchoolBrand> Brands = new(StringComparer.Ordinal)
	{
		["northfield"] = new SchoolBrand(
			OrganizationId: "northfield",
			Mascot: "Knights",
			City: "Northfield",
			State: "Michigan",
			StateCode: "MI",
			Founded: 1958,
			Enrollment: 1140,
			Classification: "MHSAA Division 2",
			HomeField: "Dunmore Field",
			ColorNames: "Royal Purple & Vegas Gold",
			PrimaryHex: "#4B2E83",
			SecondaryHex: "#C5A253",
			AccentHex: "#FFFFFF",
			Abbreviation: "NHS",
			CapMonogram: "N",
			Motto: "Hold the line.",
			History: "Northfield High opened in 1958 to serve the farm townships north of the river, and the Knights fielded a baseball team that first spring on a cow pasture behind the bus garage. Dunmore Field, named for longtime coach Walt Dunmore, went up in 1974 with lights added in 1999. The program is known for small-ball discipline and homegrown pitching, and it reached the state semifinals in 1987 and 2011.",
			Traditions:
			[
				"The \"Round Table\": seniors eat together before every home opener.",
				"A wooden sword is passed to the player of the game.",
				"Purple-and-gold \"Knight Night\" against the district's top seed.",
			]),
		["marblehead"] = new SchoolBrand(
			OrganizationId: "marblehead",
			Mascot: "Mariners",
			City: "Marblehead",
			State: "Ohio",
			StateCode: "OH",
			Founded: 1921,
			Enrollment: 410,
			Classification: "OHSAA Division IV",
			HomeField: "Lighthouse Park",
			ColorNames: "Navy & Signal Red",
			PrimaryHex: "#13294B",
			SecondaryHex: "#C8102E",
			AccentHex: "#FFFFFF",
			Abbreviation: "MHS",
			CapMonogram: "M",
			Motto: "Keep the light on.",
			History: "Marblehead High sits at the tip of the Lake Erie peninsula, where quarry families and fishing crews built a tiny school with an outsized ballclub. The Mariners play at Lighthouse Park, a short walk from the old limestone quarries, with the lake wind blowing straight out to left. A small Division IV school, Marblehead is a perennial spoiler that won a regional title in 1996 and still plays every spring like it has something to prove.",
			Traditions:
			[
				"The lighthouse bell rings after every Mariners home run.",
				"Seniors paint the rocks along the left-field line before the season.",
				"\"Lake Day\": the whole team jumps in Lake Erie after the last home game.",
			]),
	};

	public static SchoolBrand? TryGet(string? organizationId) =>
		organizationId != null && Brands.TryGetValue(organizationId, out SchoolBrand? brand) ? brand : null;

	public static IReadOnlyCollection<SchoolBrand> All => Brands.Values;
}
