namespace HSBM.Domain;

/// <summary>
/// Static league content: districts, schools, and varsity/JV team identities.
/// Players, ratings, results, and talent modifiers are career-generated.
/// </summary>
public sealed class LeagueCatalog
{
	public const int OrganizationRosterCap = 40;
	public const int TemporaryRosterCap = 42;
	public const int TeamRosterCap = 20;
	public const int UnassignedSlots = 2;
	public const int DistrictCount = 4;
	public const int SchoolsPerDistrict = 8;
	public const string DefaultUserOrganizationId = "cascade";
	public const int DefaultSeasonYear = 2026;
	/// <summary>
	/// Holding organization for students who are not on a team: tryout
	/// candidates (on a school's tryout team) and players who were cut or
	/// released (on the inactive team). Nobody can sign them.
	/// </summary>
	public const string UnsignedOrganizationId = "unsigned";

	/// <summary>Students who were cut or released. Off the team for the year.</summary>
	public const string InactiveTeamId = "inactive";

	public const int TypicalIncomingMin = 7;
	public const int TypicalIncomingMax = 15;

	/// <summary>The school's tryout group: students who signed up to try out.</summary>
	public static string TryoutTeamId(string organizationId) => $"tryouts-{organizationId}";

	public static bool IsTryoutTeam(string? teamId) =>
		!string.IsNullOrWhiteSpace(teamId) && teamId.StartsWith("tryouts-", StringComparison.Ordinal);

	/// <summary>The school a tryout team belongs to, or null.</summary>
	public static string? SchoolForTryoutTeam(string? teamId) =>
		IsTryoutTeam(teamId) ? teamId!["tryouts-".Length..] : null;

	public static string TeamIdFor(string organizationId, TeamLevel level) =>
		level == TeamLevel.JuniorVarsity ? $"{organizationId}-jv" : $"{organizationId}-varsity";

	public static bool IsUnsignedOrganization(string? organizationId) =>
		string.IsNullOrWhiteSpace(organizationId) || organizationId == UnsignedOrganizationId;


	public CompetitionLevel CompetitionLevel { get; }
	public IReadOnlyList<DistrictIdentity> Districts { get; }
	public IReadOnlyList<OrganizationIdentity> Organizations { get; }
	public IReadOnlyDictionary<string, string> OrganizationDistricts { get; }
	public IReadOnlyList<TeamIdentity> Teams { get; }

	private LeagueCatalog(
		CompetitionLevel competitionLevel,
		IReadOnlyList<DistrictIdentity> districts,
		IReadOnlyList<OrganizationIdentity> organizations,
		IReadOnlyDictionary<string, string> organizationDistricts,
		IReadOnlyList<TeamIdentity> teams)
	{
		CompetitionLevel = competitionLevel;
		Districts = districts;
		Organizations = organizations;
		OrganizationDistricts = organizationDistricts;
		Teams = teams;
	}

	public static LeagueCatalog Standard { get; } = BuildStandard();

	public OrganizationIdentity GetOrganization(string id)
	{
		foreach (OrganizationIdentity org in Organizations)
		{
			if (org.Id == id)
			{
				return org;
			}
		}

		throw new KeyNotFoundException($"Unknown organization '{id}'.");
	}

	public string DistrictIdFor(string organizationId) => OrganizationDistricts[organizationId];

	public TeamIdentity GetTeam(string teamId)
	{
		foreach (TeamIdentity team in Teams)
		{
			if (team.Id == teamId)
			{
				return team;
			}
		}

		throw new KeyNotFoundException($"Unknown team '{teamId}'.");
	}

	public TeamIdentity TeamFor(string organizationId, TeamLevel level)
	{
		foreach (TeamIdentity team in Teams)
		{
			if (team.OrganizationId == organizationId && team.Level == level)
			{
				return team;
			}
		}

		throw new KeyNotFoundException($"No {level} team for '{organizationId}'.");
	}

	public IReadOnlyList<OrganizationIdentity> OrganizationsInDistrict(string districtId)
	{
		var list = new List<OrganizationIdentity>();
		foreach (OrganizationIdentity org in Organizations)
		{
			if (OrganizationDistricts[org.Id] == districtId)
			{
				list.Add(org);
			}
		}

		return list;
	}

	public IReadOnlyList<TeamIdentity> TeamsInDistrict(string districtId, TeamLevel level)
	{
		var list = new List<TeamIdentity>();
		foreach (TeamIdentity team in Teams)
		{
			if (team.Level == level && OrganizationDistricts[team.OrganizationId] == districtId)
			{
				list.Add(team);
			}
		}

		return list;
	}

	public bool SameDistrict(string organizationIdA, string organizationIdB) =>
		OrganizationDistricts[organizationIdA] == OrganizationDistricts[organizationIdB];

	public bool SameDistrictTeams(string teamIdA, string teamIdB)
	{
		TeamIdentity a = GetTeam(teamIdA);
		TeamIdentity b = GetTeam(teamIdB);
		return SameDistrict(a.OrganizationId, b.OrganizationId);
	}

	public DateOnly OpeningDay(int seasonYear) => SeasonCalendar.OpeningDay(seasonYear);

	private static LeagueCatalog BuildStandard()
	{
		DistrictIdentity[] districts =
		[
			new("great-lakes", "Great Lakes", "GL", "East"),
			new("north-shore", "North Shore", "NS", "East"),
			new("capital-valley", "Capital Valley", "CV", "West"),
			new("iron-range", "Iron Range", "IR", "West"),
		];

		var orgs = new List<OrganizationIdentity>();
		var districtMap = new Dictionary<string, string>(StringComparer.Ordinal);
		Add(orgs, districtMap, "cascade", "Cascade Regional High School", "Cascade", "great-lakes");
		Add(orgs, districtMap, "great-lakes", "Great Lakes High School", "Great Lakes", "great-lakes");
		Add(orgs, districtMap, "harbor-ridge", "Harbor Ridge High School", "Harbor Ridge", "great-lakes");
		Add(orgs, districtMap, "delaware-valley", "Delaware Valley High School", "Delaware Valley", "great-lakes");
		Add(orgs, districtMap, "bayview", "Bayview Central High School", "Bayview", "great-lakes");
		Add(orgs, districtMap, "northfield", "Northfield Prep", "Northfield", "great-lakes");
		Add(orgs, districtMap, "lakeside", "Lakeside Academy", "Lakeside", "great-lakes");
		Add(orgs, districtMap, "pinecrest", "Pinecrest High School", "Pinecrest", "great-lakes");

		Add(orgs, districtMap, "port-clinton", "Port Clinton High School", "Port Clinton", "north-shore");
		Add(orgs, districtMap, "sandusky", "Sandusky High School", "Sandusky", "north-shore");
		Add(orgs, districtMap, "vermilion", "Vermilion High School", "Vermilion", "north-shore");
		Add(orgs, districtMap, "huron", "Huron High School", "Huron", "north-shore");
		Add(orgs, districtMap, "avon-lake", "Avon Lake High School", "Avon Lake", "north-shore");
		Add(orgs, districtMap, "westlake", "Westlake High School", "Westlake", "north-shore");
		Add(orgs, districtMap, "rocky-river", "Rocky River High School", "Rocky River", "north-shore");
		Add(orgs, districtMap, "marblehead", "Marblehead High School", "Marblehead", "north-shore");

		Add(orgs, districtMap, "capital-city", "Capital City High School", "Capital City", "capital-valley");
		Add(orgs, districtMap, "springfield", "Springfield High School", "Springfield", "capital-valley");
		Add(orgs, districtMap, "beavercreek", "Beavercreek High School", "Beavercreek", "capital-valley");
		Add(orgs, districtMap, "kettering", "Kettering High School", "Kettering", "capital-valley");
		Add(orgs, districtMap, "centerville", "Centerville High School", "Centerville", "capital-valley");
		Add(orgs, districtMap, "fairborn", "Fairborn High School", "Fairborn", "capital-valley");
		Add(orgs, districtMap, "miamisburg", "Miamisburg High School", "Miamisburg", "capital-valley");
		Add(orgs, districtMap, "huber", "Huber Heights High School", "Huber", "capital-valley");

		Add(orgs, districtMap, "hibbing", "Hibbing High School", "Hibbing", "iron-range");
		Add(orgs, districtMap, "duluth-east", "Duluth East High School", "Duluth East", "iron-range");
		Add(orgs, districtMap, "iron-range", "Iron Range High School", "Iron Range", "iron-range");
		Add(orgs, districtMap, "grand-rapids", "Grand Rapids High School", "Grand Rapids", "iron-range");
		Add(orgs, districtMap, "cloquet", "Cloquet High School", "Cloquet", "iron-range");
		Add(orgs, districtMap, "virginia", "Virginia High School", "Virginia", "iron-range");
		Add(orgs, districtMap, "eveleth", "Eveleth High School", "Eveleth", "iron-range");
		Add(orgs, districtMap, "two-harbors", "Two Harbors High School", "Two Harbors", "iron-range");

		var teams = new List<TeamIdentity>(orgs.Count * 2);
		foreach (OrganizationIdentity org in orgs)
		{
			teams.Add(new TeamIdentity($"{org.Id}-varsity", org.Id, TeamLevel.Varsity, TeamRosterCap));
			teams.Add(new TeamIdentity($"{org.Id}-jv", org.Id, TeamLevel.JuniorVarsity, TeamRosterCap));
		}

		return new LeagueCatalog(CompetitionLevel.HighSchool, districts, orgs, districtMap, teams);
	}

	private static void Add(
		List<OrganizationIdentity> orgs,
		Dictionary<string, string> districtMap,
		string id,
		string name,
		string shortName,
		string districtId)
	{
		orgs.Add(new OrganizationIdentity(id, name, shortName, OrganizationRosterCap));
		districtMap[id] = districtId;
	}
}
