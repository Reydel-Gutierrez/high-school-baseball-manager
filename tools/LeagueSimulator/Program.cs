using System.Globalization;
using HSBM.Application;
using HSBM.Domain;
using HSBM.Persistence;
using HSBM.Simulation;

string command = args.Length == 0 ? "help" : args[0].ToLowerInvariant();
Dictionary<string, string> flags = ParseFlags(args.Skip(command == "help" ? 0 : 1).ToArray());
string dbPath = flags.GetValueOrDefault("db") ?? Path.Combine("saves", "career.db");

try
{
		switch (command)
		{
			case "create":
				Create(flags, dbPath);
				break;
			case "roster":
				Roster(flags, dbPath);
				break;
			case "advance":
				Advance(flags, dbPath);
				break;
			case "season":
				Season(dbPath);
				break;
			case "seasons":
				Seasons(flags, dbPath);
				break;
			case "standings":
				Standings(flags, dbPath);
				break;
			case "leaders":
				Leaders(flags, dbPath);
				break;
			case "balance":
				Balance(flags);
				break;
			default:
				PrintHelp();
				break;
		}
}
catch (Exception ex)
{
	Console.Error.WriteLine(ex.Message);
	Environment.ExitCode = 1;
}

static void Create(Dictionary<string, string> flags, string dbPath)
{
	int seed = flags.TryGetValue("seed", out string? seedText) ? int.Parse(seedText, CultureInfo.InvariantCulture) : Environment.TickCount;
	string name = flags.GetValueOrDefault("name") ?? "Test Career";
	if (File.Exists(dbPath) && flags.ContainsKey("force"))
	{
		File.Delete(dbPath);
	}

	using CareerService service = Open(dbPath);
	CreateCareerResult result = service.CreateCareer(new CreateCareerRequest
	{
		Name = name,
		Seed = seed,
		UserOrganizationId = flags.GetValueOrDefault("org") ?? LeagueCatalog.DefaultUserOrganizationId,
	});

	CareerState state = service.LoadCareer();
	Console.WriteLine($"Created '{result.Name}' ({result.CareerId})");
	Console.WriteLine($"  seed: {result.Seed}");
	Console.WriteLine($"  date: {result.CurrentDate:yyyy-MM-dd}");
	Console.WriteLine($"  db:   {service.DatabasePath}");
	Console.WriteLine($"  players: {result.PlayerCount}");
	Console.WriteLine($"  games: {result.GameCount}");
	Console.WriteLine();
	foreach (OrganizationState org in state.Organizations.OrderBy(o => o.ShortName))
	{
		if (LeagueCatalog.IsUnsignedOrganization(org.Id))
		{
			continue;
		}

		int varsity = state.Players.Count(p => p.OrganizationId == org.Id && p.TeamLevel == TeamLevel.Varsity);
		int jv = state.Players.Count(p => p.OrganizationId == org.Id && p.TeamLevel == TeamLevel.JuniorVarsity);
		int top = state.Players.Where(p => p.OrganizationId == org.Id).Max(p => p.Overall);
		Console.WriteLine($"  {org.ShortName,-16} {org.TalentTier.ToLabel(),-14} V:{varsity,2} JV:{jv,2}  top OVR {top}");
	}
}

static void Roster(Dictionary<string, string> flags, string dbPath)
{
	using CareerService service = Open(dbPath);
	CareerState state = service.LoadCareer();
	string orgId = flags.GetValueOrDefault("org") ?? state.Career.UserOrganizationId;
	TeamLevel level = flags.ContainsKey("jv") ? TeamLevel.JuniorVarsity : TeamLevel.Varsity;
	string teamId = $"{orgId}-{(level == TeamLevel.JuniorVarsity ? "jv" : "varsity")}";
	List<Player> roster = service.GetRoster(teamId)
		.OrderByDescending(p => p.Overall)
		.ToList();
	Console.WriteLine($"{orgId} {level.ToLabel()} ({roster.Count})");
	foreach (Player player in roster)
	{
		Console.WriteLine(
			$"  #{player.JerseyNumber,2} {player.FullName,-22} {player.DisplayPosition,-4} {player.Grade.ToAbbrev()}  OVR {player.Overall,3}  {player.Bats.ToBatsThrowsLabel(player.Throws)}");
	}
}

static void Advance(Dictionary<string, string> flags, string dbPath)
{
	int days = flags.TryGetValue("days", out string? daysText) ? int.Parse(daysText, CultureInfo.InvariantCulture) : 1;
	using CareerService service = Open(dbPath);
	AdvanceDayResult result = days == 1 ? service.AdvanceDay() : service.AdvanceDays(days);
	Console.WriteLine($"{result.PreviousDate:yyyy-MM-dd} -> {result.NewDate:yyyy-MM-dd}");
	Console.WriteLine($"Games played: {result.GamesPlayed.Count}");
	foreach (GameSummary game in result.GamesPlayed.Take(24))
	{
		Console.WriteLine(
			$"  {game.TeamLevel.ToLabel(),-8} {game.AwayTeamId} {game.AwayScore} @ {game.HomeTeamId} {game.HomeScore}  ({game.InningsPlayed} inn)");
	}

	if (result.GamesPlayed.Count > 24)
	{
		Console.WriteLine($"  ... {result.GamesPlayed.Count - 24} more");
	}
}

static void Season(string dbPath)
{
	using CareerService service = Open(dbPath);
	AdvanceDayResult result = service.SimulateRemainingSeason();
	Console.WriteLine($"Season simulated {result.PreviousDate:yyyy-MM-dd} -> {result.NewDate:yyyy-MM-dd}");
	if (result.RolledOver)
	{
		Console.WriteLine("Baseball year rolled over.");
	}

	PrintStandings(service, null, TeamLevel.Varsity);
}

static void Seasons(Dictionary<string, string> flags, string dbPath)
{
	int count = flags.TryGetValue("count", out string? countText)
		? int.Parse(countText, CultureInfo.InvariantCulture)
		: CareerWorldRunner.DefaultSeasons;
	int seed = flags.TryGetValue("seed", out string? seedText)
		? int.Parse(seedText, CultureInfo.InvariantCulture)
		: 2026;
	bool fresh = flags.ContainsKey("force") || !File.Exists(dbPath);
	if (File.Exists(dbPath) && flags.ContainsKey("force"))
	{
		File.Delete(dbPath);
	}

	using CareerService service = Open(dbPath);
	if (fresh)
	{
		service.CreateCareer(new CreateCareerRequest
		{
			Name = flags.GetValueOrDefault("name") ?? $"Sim {count} seasons",
			Seed = seed,
		});
	}

	LeagueSimulationResult result = service.SimulateSeasons(count);
	Console.Write(result.Format());
	if (result.IntegrityIssues.Count > 0)
	{
		Environment.ExitCode = 1;
	}
}

static void Standings(Dictionary<string, string> flags, string dbPath)
{
	using CareerService service = Open(dbPath);
	string? district = flags.GetValueOrDefault("district");
	TeamLevel level = flags.ContainsKey("jv") ? TeamLevel.JuniorVarsity : TeamLevel.Varsity;
	PrintStandings(service, district, level);
}

static void PrintStandings(CareerService service, string? district, TeamLevel level)
{
	IReadOnlyList<StandingRow> rows = service.GetStandings(district, level);
	string currentDistrict = "";
	foreach (StandingRow row in rows)
	{
		if (row.DistrictId != currentDistrict)
		{
			currentDistrict = row.DistrictId;
			Console.WriteLine();
			Console.WriteLine(currentDistrict.ToUpperInvariant());
			Console.WriteLine($"  {"Rk",-3} {"Team",-16} {"W",3} {"L",3} {"DW",3} {"DL",3} {"RF",4} {"RA",4}");
		}

		Console.WriteLine(
			$"  {row.Rank,-3} {row.Name,-16} {row.Wins,3} {row.Losses,3} {row.DistrictWins,3} {row.DistrictLosses,3} {row.RunsFor,4} {row.RunsAgainst,4}");
	}
}

static void Leaders(Dictionary<string, string> flags, string dbPath)
{
	using CareerService service = Open(dbPath);
	if (flags.ContainsKey("pitching"))
	{
		Console.WriteLine("Pitching ERA");
		foreach (PitchingLeaderRow row in service.GetPitchingLeaders())
		{
			Console.WriteLine(
				$"  {row.Name,-22} {row.Position,-4} {row.Stats.Era,5:0.00}  {row.Stats.Wins}-{row.Stats.Losses}  {row.Stats.InningsPitchedDisplay} IP  {row.Stats.Strikeouts} K");
		}

		return;
	}

	Console.WriteLine("Batting AVG");
	foreach (BattingLeaderRow row in service.GetBattingLeaders())
	{
		Console.WriteLine(
			$"  {row.Name,-22} {row.Position,-4} {row.Stats.Average,5:0.000}  {row.Stats.Hits} H  {row.Stats.HomeRuns} HR  {row.Stats.RunsBattedIn} RBI");
	}
}

static void Balance(Dictionary<string, string> flags)
{
	int games = flags.TryGetValue("games", out string? gamesText) ? int.Parse(gamesText, CultureInfo.InvariantCulture) : 1000;
	int seed = flags.TryGetValue("seed", out string? seedText) ? int.Parse(seedText, CultureInfo.InvariantCulture) : 12345;
	SimulationBalanceMetrics metrics = SimulationBalanceRunner.Run(games, seed);
	Console.WriteLine($"Balance sample: {metrics.Games} games (seed {seed})");
	Console.WriteLine($"  runs/team/game:  {metrics.RunsPerTeamGame:0.00}");
	Console.WriteLine($"  hits/team/game:  {metrics.HitsPerTeamGame:0.00}");
	Console.WriteLine($"  walks/team/game: {metrics.WalksPerTeamGame:0.00}");
	Console.WriteLine($"  K/team/game:     {metrics.StrikeoutsPerTeamGame:0.00}");
	Console.WriteLine($"  HR/team/game:    {metrics.HomeRunsPerTeamGame:0.00}");
	Console.WriteLine($"  extra innings:   {metrics.ExtraInningRate:0.0%}");
	Console.WriteLine($"  batting average: {metrics.BattingAverage:0.000}");
	Console.WriteLine();
	CareerState world = CareerWorldRunner.NewCareer(seed);
	Console.Write(LeagueBalanceReport.FormatWorld(world));
}

static CareerService Open(string dbPath) => CareerService.Create(new SqliteCareerStore(dbPath));

static Dictionary<string, string> ParseFlags(string[] args)
{
	var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	for (int i = 0; i < args.Length; i++)
	{
		string token = args[i];
		if (!token.StartsWith("--", StringComparison.Ordinal))
		{
			continue;
		}

		string key = token[2..];
		if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
		{
			flags[key] = args[++i];
		}
		else
		{
			flags[key] = "true";
		}
	}

	return flags;
}

static void PrintHelp()
{
	Console.WriteLine("High School Baseball Manager — LeagueSimulator");
	Console.WriteLine();
	Console.WriteLine("  create    --seed 12345 [--name \"Test\"] [--db saves/career.db] [--force]");
	Console.WriteLine("  roster    [--org cascade] [--jv] [--db saves/career.db]");
	Console.WriteLine("  advance   [--days 1] [--db saves/career.db]");
	Console.WriteLine("  season    [--db saves/career.db]");
	Console.WriteLine("  seasons   --count 10 [--seed 2026] [--db saves/career.db] [--force]");
	Console.WriteLine("  standings [--district great-lakes] [--jv] [--db saves/career.db]");
	Console.WriteLine("  leaders   [--pitching] [--db saves/career.db]");
	Console.WriteLine("  balance   [--games 1000] [--seed 12345]");
}
