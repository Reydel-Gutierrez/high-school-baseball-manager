using HSBM.Domain;

namespace HSBM.Simulation;

public sealed class GeneratedWorld
{
	public int Seed { get; init; }
	public int SeasonYear { get; init; }
	public CompetitionLevel CompetitionLevel { get; init; } = CompetitionLevel.HighSchool;
	public List<DistrictIdentity> Districts { get; init; } = [];
	public List<OrganizationState> Organizations { get; init; } = [];
	public List<TeamIdentity> Teams { get; init; } = [];
	public List<Player> Players { get; init; } = [];
	public List<ProgramCoach> Coaches { get; init; } = [];
	public List<ScheduledGame> Games { get; init; } = [];
	public List<DistrictOpenPlay> OpenPlay { get; init; } = [];

	public GameWorld ToGameWorld(DateOnly currentDate) => new()
	{
		Seed = Seed,
		CurrentDate = currentDate,
		CurrentSeasonYear = SeasonYear,
		CompetitionLevel = CompetitionLevel,
		Districts = Districts,
		Organizations = Organizations,
		Teams = Teams,
		Players = Players,
		Coaches = Coaches,
		Games = Games,
		OpenPlay = OpenPlay,
	};
}

public sealed class CareerWorldGenerator
{
	public GeneratedWorld Generate(
		LeagueCatalog catalog,
		int seed,
		int seasonYear,
		Action<WorldGenerationProgress>? onProgress = null)
	{
		SeededRandom root = new(seed);
		ISeededRandom talentRng = root.Derive(11);
		ISeededRandom historyRng = root.Derive(13);
		ISeededRandom openingRng = root.Derive(17);
		ISeededRandom rosterRng = root.Derive(29);
		ISeededRandom openPlayRng = root.Derive(41);
		ISeededRandom scheduleRng = root.Derive(47);
		ISeededRandom coachRng = root.Derive(53);

		int programCount = catalog.Organizations.Count;
		Report(onProgress, WorldGenerationSteps.Programs(programCount), 0);

		var organizations = new List<OrganizationState>(programCount);
		for (int i = 0; i < programCount; i++)
		{
			OrganizationIdentity identity = catalog.Organizations[i];
			organizations.Add(new OrganizationState
			{
				Identity = identity,
				DistrictId = catalog.DistrictIdFor(identity.Id),
				CompetitionLevel = catalog.CompetitionLevel,
				TalentTier = ProgramTalentTier.Average,
			});
		}

		Report(onProgress, WorldGenerationSteps.Histories, 1);
		ProgramTalentTier[] tiers = TalentDistribution.AssignTiers(programCount, talentRng);
		for (int i = 0; i < programCount; i++)
		{
			organizations[i].TalentTier = tiers[i];
		}

		ProgramHistoryGenerator.Assign(organizations, historyRng, seasonYear);
		OpeningGenerator.Assign(organizations, openingRng);

		var players = new List<Player>();
		Report(onProgress, WorldGenerationSteps.Varsity, 2);
		foreach (TeamIdentity team in catalog.Teams.Where(t => t.Level == TeamLevel.Varsity))
		{
			AddRoster(players, organizations, team, seasonYear, rosterRng, returningOnly: true);
		}

		Report(onProgress, WorldGenerationSteps.JuniorVarsity, 3);
		foreach (TeamIdentity team in catalog.Teams.Where(t => t.Level == TeamLevel.JuniorVarsity))
		{
			AddRoster(players, organizations, team, seasonYear, rosterRng, returningOnly: true);
		}

		Report(onProgress, WorldGenerationSteps.Identities, 4);
		List<ProgramCoach> coaches = ProgramCoachGenerator.Assign(organizations, coachRng);

		var teams = catalog.Teams.ToList();
		organizations.Add(new OrganizationState
		{
			Identity = new OrganizationIdentity(LeagueCatalog.UnsignedOrganizationId, "Unsigned Prospects", "Unsigned", 0),
			DistrictId = catalog.Districts[0].Id,
			CompetitionLevel = catalog.CompetitionLevel,
			TalentTier = ProgramTalentTier.Average,
		});
		foreach (DistrictIdentity district in catalog.Districts)
		{
			teams.Add(new TeamIdentity(
				LeagueCatalog.OpenPlayTeamId(district.Id),
				LeagueCatalog.UnsignedOrganizationId,
				TeamLevel.JuniorVarsity,
				0));
		}

		var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (Player player in players)
		{
			usedNames.Add(player.FullName);
		}

		Report(onProgress, WorldGenerationSteps.OpenPlay, 5);
		var openPlayPlayers = new List<Player>();
		foreach (DistrictIdentity district in catalog.Districts)
		{
			ISeededRandom districtRng = openPlayRng.Derive(StableSalt(district.Id));
			openPlayPlayers.AddRange(OpenPlayGenerator.GenerateDistrict(district.Id, seasonYear, districtRng, usedNames));
		}

		players.AddRange(openPlayPlayers);

		Report(onProgress, WorldGenerationSteps.OpenPlayGames, 6);
		var openPlay = new List<DistrictOpenPlay>();
		foreach (DistrictIdentity district in catalog.Districts)
		{
			ISeededRandom districtRng = openPlayRng.Derive(StableSalt(district.Id) ^ 917);
			List<Player> districtPlayers = openPlayPlayers
				.Where(p => p.TeamId == LeagueCatalog.OpenPlayTeamId(district.Id))
				.ToList();
			openPlay.Add(OpenPlaySimulator.Simulate(district.Id, seasonYear, districtPlayers, districtRng));
		}

		Report(onProgress, WorldGenerationSteps.Schedule, 7);
		List<ScheduledGame> games = ScheduleGenerator.Generate(
			catalog,
			catalog.Teams,
			seasonYear,
			catalog.OpeningDay(seasonYear),
			scheduleRng);

		return new GeneratedWorld
		{
			Seed = seed,
			SeasonYear = seasonYear,
			CompetitionLevel = catalog.CompetitionLevel,
			Districts = catalog.Districts.ToList(),
			Organizations = organizations,
			Teams = teams,
			Players = players,
			Coaches = coaches,
			Games = games,
			OpenPlay = openPlay,
		};
	}

	private static void AddRoster(
		List<Player> players,
		List<OrganizationState> organizations,
		TeamIdentity team,
		int seasonYear,
		ISeededRandom rosterRng,
		bool returningOnly)
	{
		OrganizationState org = organizations.First(o => o.Id == team.OrganizationId);
		ISeededRandom teamRng = rosterRng.Derive(StableSalt(team.Id));
		players.AddRange(RosterGenerator.GenerateTeam(team, org.TalentTier, seasonYear, teamRng, returningOnly));
	}

	private static void Report(Action<WorldGenerationProgress>? onProgress, string label, int stepIndex) =>
		onProgress?.Invoke(new WorldGenerationProgress(label, stepIndex, WorldGenerationSteps.Count));

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
