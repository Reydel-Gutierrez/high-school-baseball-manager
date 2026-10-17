using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// Builds one district's Open Play + recruiting market. Used by career
/// generation and the live clubhouse so AI schools share the user rules.
/// </summary>
public static class DistrictRecruitingFactory
{
	public static DistrictRecruitingSession Create(
		LeagueCatalog catalog,
		string userOrganizationId,
		int seasonYear,
		ISeededRandom rng,
		IReadOnlyList<Player>? userReturning = null)
	{
		string districtId = catalog.DistrictIdFor(userOrganizationId);
		IReadOnlyList<OrganizationIdentity> orgs = catalog.OrganizationsInDistrict(districtId);
		var organizations = new List<OrganizationState>(orgs.Count);
		foreach (OrganizationIdentity identity in orgs)
		{
			organizations.Add(new OrganizationState
			{
				Identity = identity,
				DistrictId = districtId,
				CompetitionLevel = catalog.CompetitionLevel,
				TalentTier = ProgramTalentTier.Average,
			});
		}

		ProgramTalentTier[] tiers = TalentDistribution.AssignTiers(organizations.Count, rng.Derive(11));
		for (int i = 0; i < organizations.Count; i++)
		{
			organizations[i].TalentTier = tiers[i];
		}

		ProgramHistoryGenerator.Assign(organizations, rng.Derive(13), seasonYear);

		var teams = new List<TeamIdentity>();
		foreach (OrganizationIdentity org in orgs)
		{
			teams.Add(catalog.TeamFor(org.Id, TeamLevel.Varsity));
			teams.Add(catalog.TeamFor(org.Id, TeamLevel.JuniorVarsity));
		}

		var returning = new List<Player>();
		var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (OrganizationState org in organizations)
		{
			if (userReturning != null && org.Id == userOrganizationId)
			{
				returning.AddRange(userReturning);
				foreach (Player player in userReturning)
				{
					usedNames.Add(player.FullName);
				}

				continue;
			}

			foreach (TeamLevel level in new[] { TeamLevel.Varsity, TeamLevel.JuniorVarsity })
			{
				TeamIdentity team = catalog.TeamFor(org.Id, level);
				List<Player> roster = RosterGenerator.GenerateTeam(
					team,
					org.TalentTier,
					seasonYear,
					rng.Derive(StableSalt(team.Id)),
					returningOnly: true);
				returning.AddRange(roster);
				foreach (Player player in roster)
				{
					usedNames.Add(player.FullName);
				}
			}
		}

		List<Player> prospects = OpenPlayGenerator.GenerateDistrict(districtId, seasonYear, rng.Derive(41), usedNames);
		DistrictOpenPlay openPlay = OpenPlaySimulator.Simulate(districtId, seasonYear, prospects, rng.Derive(917));
		var market = new RecruitingMarket(organizations, returning, prospects, teams);
		return new DistrictRecruitingSession(districtId, organizations, returning, prospects, teams, openPlay, market);
	}

	public static DistrictRecruitingSession FromCareer(CareerState state, string userOrganizationId)
	{
		string districtId = state.OrganizationById(userOrganizationId).DistrictId;
		var schools = new List<OrganizationState>();
		var schoolIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (OrganizationState school in state.Organizations)
		{
			if (school.DistrictId == districtId && !LeagueCatalog.IsUnsignedOrganization(school.Id))
			{
				schools.Add(school);
				schoolIds.Add(school.Id);
			}
		}

		DistrictOpenPlay openPlay = state.OpenPlayFor(districtId)
			?? new DistrictOpenPlay(districtId, state.Career.CurrentSeasonYear, SeasonCalendar.OpenPlay(state.Career.CurrentSeasonYear), []);

		var returning = new List<Player>();
		var prospects = new List<Player>();
		foreach (Player player in state.Players)
		{
			if (openPlay.Contains(player.Id) || player.TeamId == LeagueCatalog.OpenPlayTeamId(districtId))
			{
				prospects.Add(player);
			}
			else if (schoolIds.Contains(player.OrganizationId) && !player.IsUnsigned)
			{
				returning.Add(player);
			}
		}

		var teams = new List<TeamIdentity>();
		foreach (TeamIdentity team in state.Teams)
		{
			if (schoolIds.Contains(team.OrganizationId) || LeagueCatalog.IsOpenPlayTeam(team.Id))
			{
				teams.Add(team);
			}
		}

		var market = new RecruitingMarket(
			schools,
			returning,
			prospects,
			teams,
			state.Offers,
			state.Agreements,
			state.RecruitingInterest);
		return new DistrictRecruitingSession(districtId, schools, returning, prospects, teams, openPlay, market);
	}

	private static int StableSalt(string value)
	{
		unchecked
		{
			int hash = 17;
			foreach (char c in value)
			{
				hash = (hash * 31) + c;
			}

			return hash;
		}
	}
}

public sealed class DistrictRecruitingSession
{
	public DistrictRecruitingSession(
		string districtId,
		List<OrganizationState> schools,
		List<Player> returning,
		List<Player> prospects,
		List<TeamIdentity> teams,
		DistrictOpenPlay openPlay,
		RecruitingMarket market)
	{
		DistrictId = districtId;
		Schools = schools;
		Returning = returning;
		Prospects = prospects;
		Teams = teams;
		OpenPlay = openPlay;
		Market = market;
	}

	public string DistrictId { get; }
	public List<OrganizationState> Schools { get; }
	public List<Player> Returning { get; }
	public List<Player> Prospects { get; }
	public List<TeamIdentity> Teams { get; }
	public DistrictOpenPlay OpenPlay { get; }
	public RecruitingMarket Market { get; }
}
