using System;
using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// Full prototype squads for every District Hub team. Named stars stay
/// in place; the rest of each roster is generated so Statistics can
/// open any of the 32 programs with the same profile card as Roster.
/// </summary>
public static class DistrictHubSquads
{
	private const int HitterCount = 13;
	private const int PitcherCount = 6;
	private const string TonyPortrait = "res://assets/teams/02_Great_Lakes_High_School/Players/Great_lakes_player_tony.png";

	private static readonly string[] FirstNames =
	[
		"Aiden", "Andre", "Ben", "Bryce", "Calvin", "Cam", "Cole", "Diego", "Drew", "Eli",
		"Evan", "Finn", "Gavin", "Grant", "Henry", "Ian", "Isaiah", "Jalen", "Jake", "Jonah",
		"Luis", "Marcus", "Mateo", "Max", "Miles", "Nate", "Noah", "Owen", "Parker", "Quinn",
		"Reid", "Ryan", "Sam", "Seth", "Theo", "Trey", "Tyler", "Wes", "Will", "Leo",
	];

	private static readonly string[] LastNames =
	[
		"Ashford", "Bennett", "Carver", "Dalton", "Ellis", "Farrell", "Griffin", "Hayes", "Ingram", "Jennings",
		"Keller", "Lambert", "Monroe", "Nash", "Owens", "Parker", "Quincy", "Ramos", "Sutton", "Tucker",
		"Underwood", "Vargas", "Walsh", "Yates", "Bishop", "Crowe", "Dunn", "Frost", "Glenn", "Hale",
	];

	private static readonly string[] HitterPositions =
		["C", "1B", "2B", "SS", "3B", "LF", "CF", "RF", "DH", "C", "OF", "INF", "UTIL"];

	private static readonly string[] PitcherPositions =
		["RHP", "RHP", "LHP", "RHP", "RHP", "LHP"];

	private static readonly string[] ClassYears =
		["SR", "SR", "JR", "JR", "JR", "SO", "SO", "SO", "FR", "FR", "JR", "SO", "FR"];

	private static readonly Dictionary<string, string> CascadePriorOrgs = new(StringComparer.OrdinalIgnoreCase)
	{
		["Cruz"] = "Great Lakes High School",
		["Whitaker"] = "Great Lakes High School",
		["Vargas"] = "Delaware Valley High School",
		["Grant"] = "Delaware Valley High School",
		["Drake"] = "Great Lakes High School",
		["Walsh"] = "Harbor Ridge High School",
		["Holt"] = "Harbor Ridge High School",
	};

	private static readonly Dictionary<string, List<HubSquadPlayer>> SquadCache = new();

	public static List<HubSquadPlayer> Hitters(string teamId, TeamLevel level)
	{
		var list = new List<HubSquadPlayer>();
		foreach (HubSquadPlayer player in Squad(teamId, level))
		{
			if (player.CurrentHitting != null)
			{
				list.Add(player);
			}
		}

		return list;
	}

	public static List<HubSquadPlayer> Arms(string teamId, TeamLevel level)
	{
		var list = new List<HubSquadPlayer>();
		foreach (HubSquadPlayer player in Squad(teamId, level))
		{
			if (player.CurrentPitching != null)
			{
				list.Add(player);
			}
		}

		return list;
	}

	public static List<HubSquadPlayer> Squad(string teamId, TeamLevel level)
	{
		string key = $"{teamId}:{level}";
		if (SquadCache.TryGetValue(key, out List<HubSquadPlayer>? cached))
		{
			return cached;
		}

		var list = new List<HubSquadPlayer>();
		if (teamId == DistrictHubData.UserTeamId)
		{
			AddCascade(list, level);
		}

		AddKnownHitters(teamId, level, list);
		AddKnownPitchers(teamId, level, list);
		FillHitters(teamId, level, list);
		FillPitchers(teamId, level, list);
		SquadCache[key] = list;
		return list;
	}

	public static List<HubSquadPlayer> AllPlayers(TeamLevel level)
	{
		var list = new List<HubSquadPlayer>();
		foreach (HubTeam team in DistrictHubData.Teams)
		{
			list.AddRange(Squad(team.Id, level));
		}

		return list;
	}

	public static HubSquadPlayer? FindPlayer(string teamId, string name, TeamLevel preferred)
	{
		HubSquadPlayer? match = FindByName(Squad(teamId, preferred), name);
		if (match != null)
		{
			return match;
		}

		TeamLevel other = preferred == TeamLevel.Varsity ? TeamLevel.JuniorVarsity : TeamLevel.Varsity;
		return FindByName(Squad(teamId, other), name);
	}

	private static void AddCascade(List<HubSquadPlayer> list, TeamLevel level)
	{
		HubTeam team = DistrictHubData.GetTeam(DistrictHubData.UserTeamId);
		foreach (ClubhousePlayer player in ClubhouseSquad.ForTeam(level))
		{
			if (ContainsName(list, $"{player.FirstName} {player.LastName}"))
			{
				continue;
			}

			HubBatter? knownHit = FindKnownBatter($"{player.FirstName} {player.LastName}");
			HubPitcher? knownPit = FindKnownPitcher($"{player.FirstName} {player.LastName}");
			HubSeasonHitting? hitting = knownHit != null
				? FromBatter(knownHit)
				: player.Hitting == null ? null : FromClubhouseHit(player.Hitting);
			HubSeasonPitching? pitching = knownPit != null
				? FromPitcher(knownPit)
				: player.Pitching == null ? null : FromClubhousePit(player.Pitching);

			CascadePriorOrgs.TryGetValue(player.LastName, out string? prior);
			list.Add(MakePlayer(
				player.FirstName,
				player.LastName,
				player.Jersey,
				ClubhouseSquad.SuggestDefense(player) == "P"
					? (player.Position.Contains("LHP", StringComparison.Ordinal) ? "LHP" : "RHP")
					: ClubhouseSquad.SuggestDefense(player),
				player.Year,
				player.BatsThrows,
				player.Overall,
				player.Contact,
				player.Power,
				player.Speed,
				player.Arm,
				player.Field,
				player.Status,
				player.PortraitPath,
				team,
				level,
				hitting,
				pitching,
				prior));
		}
	}

	private static void AddKnownHitters(string teamId, TeamLevel level, List<HubSquadPlayer> list)
	{
		HubTeam team = DistrictHubData.GetTeam(teamId);
		foreach (HubBatter batter in DistrictHubData.Batters)
		{
			if (batter.TeamId != teamId || ContainsName(list, batter.Name))
			{
				continue;
			}

			(string first, string last) = SplitName(batter.Name);
			int seed = StableHash(batter.Name + teamId);
			string year = ClassYearFromAwards(batter.Name) ?? ClassYears[Math.Abs(seed) % 4];
			(int overall, int contact, int power, int speed, int arm, int field) = RatingsFromBatter(batter, seed);
			list.Add(MakePlayer(
				first,
				last,
				JerseyFromName(batter.Name),
				batter.Position,
				year,
				BatsThrowsForHitter(seed),
				overall,
				contact,
				power,
				speed,
				arm,
				field,
				StatusFromSeed(seed),
				PortraitFor(batter.Name),
				team,
				level,
				FromBatter(batter),
				null,
				PriorOrg(team, year, seed)));
		}
	}

	private static void AddKnownPitchers(string teamId, TeamLevel level, List<HubSquadPlayer> list)
	{
		HubTeam team = DistrictHubData.GetTeam(teamId);
		foreach (HubPitcher pitcher in DistrictHubData.Pitchers)
		{
			if (pitcher.TeamId != teamId)
			{
				continue;
			}

			HubSquadPlayer? existing = FindByName(list, pitcher.Name);
			if (existing != null)
			{
				continue;
			}

			(string first, string last) = SplitName(pitcher.Name);
			int seed = StableHash(pitcher.Name + teamId + "p");
			string year = ClassYearFromAwards(pitcher.Name) ?? ClassYears[Math.Abs(seed) % 4];
			(int overall, int contact, int power, int speed, int arm, int field) = RatingsFromPitcher(pitcher, seed);
			list.Add(MakePlayer(
				first,
				last,
				JerseyFromName(pitcher.Name),
				pitcher.Position,
				year,
				pitcher.Position.Contains("LHP", StringComparison.Ordinal) ? "L/L" : "R/R",
				overall,
				contact,
				power,
				speed,
				arm,
				field,
				StatusFromSeed(seed),
				PortraitFor(pitcher.Name),
				team,
				level,
				null,
				FromPitcher(pitcher),
				PriorOrg(team, year, seed)));
		}
	}

	private static void FillHitters(string teamId, TeamLevel level, List<HubSquadPlayer> list)
	{
		HubTeam team = DistrictHubData.GetTeam(teamId);
		int seed = StableHash(teamId + level + "hit");
		int slot = 0;
		while (CountHitters(list) < HitterCount)
		{
			string name = NextName(seed + slot, list);
			(string first, string last) = SplitName(name);
			string position = HitterPositions[slot % HitterPositions.Length];
			string year = ClassYears[slot % ClassYears.Length];
			float quality = TeamQuality(team, level);
			float avg = Clamp(0.210f + quality * 0.14f - slot * 0.008f + ((seed + slot) % 7) * 0.003f, 0.180f, 0.390f);
			int games = level == TeamLevel.Varsity ? 22 - slot / 4 : 16 - slot / 5;
			int ab = Math.Max(18, games * 3 - slot);
			int hits = Math.Max(3, (int)Math.Round(avg * ab));
			int doubles = Math.Max(0, hits / 4 - slot / 6);
			int triples = slot % 5 == 0 ? 1 : 0;
			int hr = Math.Max(0, (int)(quality * 6) - slot / 3);
			int rbi = Math.Max(2, hits / 2 + hr);
			int walks = 4 + (seed + slot) % 8;
			int so = 8 + slot + (seed + slot) % 6;
			int sb = position is "CF" or "SS" or "2B" ? 4 + (seed + slot) % 9 : (seed + slot) % 4;
			int runs = Math.Max(3, hits / 2 + walks / 3);
			float obp = Clamp(avg + walks / (float)(ab + walks) * 0.55f, avg, 0.480f);
			float slg = Clamp(avg + doubles * 0.012f + triples * 0.02f + hr * 0.04f, avg, 0.720f);
			var hitting = new HubSeasonHitting(games, ab, runs, hits, doubles, triples, hr, rbi, walks, so, sb, avg, obp, slg);
			int playerSeed = seed + slot * 17;
			(int overall, int contact, int power, int speed, int arm, int field) = RatingsFromBatter(hitting, position, playerSeed);
			list.Add(MakePlayer(
				first,
				last,
				JerseyFromName(name),
				position,
				year,
				BatsThrowsForHitter(playerSeed),
				overall,
				contact,
				power,
				speed,
				arm,
				field,
				StatusFromSeed(playerSeed),
				null,
				team,
				level,
				hitting,
				null,
				PriorOrg(team, year, playerSeed)));
			slot++;
		}
	}

	private static void FillPitchers(string teamId, TeamLevel level, List<HubSquadPlayer> list)
	{
		HubTeam team = DistrictHubData.GetTeam(teamId);
		int seed = StableHash(teamId + level + "pit");
		int slot = 0;
		while (CountPitchers(list) < PitcherCount)
		{
			string name = NextName(seed + slot + 40, list);
			(string first, string last) = SplitName(name);
			string position = PitcherPositions[slot % PitcherPositions.Length];
			string year = ClassYears[(slot + 1) % ClassYears.Length];
			float quality = TeamQuality(team, level);
			float era = Clamp(4.80f - quality * 2.6f + slot * 0.28f, 1.80f, 6.20f);
			int wins = Math.Max(0, (int)(quality * 6) - slot);
			int losses = Math.Max(0, 2 + slot / 2 - (int)(quality * 2));
			int saves = slot == 3 ? 2 + (seed % 3) : slot == 4 ? 1 : 0;
			int games = 6 + (seed + slot) % 5;
			int gs = slot < 3 ? games - 1 : Math.Max(0, games / 3);
			float ip = Math.Max(8f, gs * 4.2f + (games - gs) * 1.4f - slot);
			int walks = 6 + slot + (seed + slot) % 6;
			int so = Math.Max(6, (int)(ip * (0.6f + quality * 0.5f)));
			float whip = Clamp(0.95f + (1f - quality) * 0.7f + slot * 0.06f, 0.85f, 1.85f);
			int hits = Math.Max(6, (int)Math.Round(whip * ip - walks));
			int earned = Math.Max(3, (int)Math.Round(era * ip / 9f));
			int hr = Math.Max(0, (int)Math.Round(ip * 0.08f));
			var pitching = new HubSeasonPitching(
				games, gs, wins, losses, saves, ip, hits, earned + 1, earned, walks, so, hr, era, whip);
			int playerSeed = seed + slot * 23;
			(int overall, int contact, int power, int speed, int arm, int field) = RatingsFromPitcher(pitching, playerSeed);
			list.Add(MakePlayer(
				first,
				last,
				JerseyFromName(name),
				position,
				year,
				position.Contains("LHP", StringComparison.Ordinal) ? "L/L" : "R/R",
				overall,
				contact,
				power,
				speed,
				arm,
				field,
				StatusFromSeed(playerSeed),
				null,
				team,
				level,
				null,
				pitching,
				PriorOrg(team, year, playerSeed)));
			slot++;
		}
	}

	private static HubSquadPlayer MakePlayer(
		string firstName,
		string lastName,
		int jersey,
		string position,
		string year,
		string batsThrows,
		int overall,
		int contact,
		int power,
		int speed,
		int arm,
		int field,
		string status,
		string? portraitPath,
		HubTeam team,
		TeamLevel level,
		HubSeasonHitting? hitting,
		HubSeasonPitching? pitching,
		string? priorOrganization)
	{
		string orgName = team.Id == DistrictHubData.UserTeamId
			? "Cascade Regional High School"
			: $"{team.Name} High School";
		return new HubSquadPlayer(
			firstName,
			lastName,
			jersey,
			position,
			year,
			batsThrows,
			overall,
			contact,
			power,
			speed,
			arm,
			field,
			status,
			portraitPath,
			team.Id,
			team.ShortName,
			level.ToSquadKey(),
			BuildSeasons(year, orgName, hitting, pitching, priorOrganization));
	}

	private static List<HubPlayerSeason> BuildSeasons(
		string classYear,
		string organization,
		HubSeasonHitting? currentHitting,
		HubSeasonPitching? currentPitching,
		string? priorOrganization)
	{
		int count = classYear.ToUpperInvariant() switch
		{
			"FR" => 1,
			"SO" => 2,
			"JR" => 3,
			"SR" => 4,
			_ => 1,
		};
		var seasons = new List<HubPlayerSeason>(count);
		for (int i = 0; i < count; i++)
		{
			int year = DistrictHubData.SeasonYear - (count - 1) + i;
			float progress = count == 1 ? 1f : i / (float)(count - 1);
			string org = year == DistrictHubData.SeasonYear || string.IsNullOrEmpty(priorOrganization)
				? organization
				: priorOrganization;
			seasons.Add(new HubPlayerSeason(
				year,
				org,
				currentHitting == null ? null : ScaleHitting(currentHitting, progress),
				currentPitching == null ? null : ScalePitching(currentPitching, progress)));
		}

		return seasons;
	}

	private static HubSeasonHitting ScaleHitting(HubSeasonHitting current, float progress)
	{
		if (progress >= 0.999f)
		{
			return current;
		}

		float volume = Lerp(0.38f, 0.86f, progress);
		float contact = Lerp(0.84f, 0.98f, progress);
		float power = Lerp(0.55f, 0.92f, progress);
		int games = Math.Max(4, RoundStat(current.Games * volume));
		int atBats = Math.Max(games, RoundStat(current.AtBats * volume));
		int walks = RoundStat(current.Walks * volume * contact);
		int strikeouts = RoundStat(current.Strikeouts * volume / contact);
		int hits = ClampInt(RoundStat(current.Hits * volume * contact), 0, atBats);
		int doubles = Math.Min(hits, RoundStat(current.Doubles * volume * power));
		int triples = Math.Min(Math.Max(0, hits - doubles), RoundStat(current.Triples * volume * power));
		int homeRuns = Math.Min(Math.Max(0, hits - doubles - triples), RoundStat(current.HomeRuns * volume * power));
		int runs = RoundStat(current.Runs * volume * contact);
		int rbi = RoundStat(current.Rbi * volume * power);
		int stolenBases = RoundStat(current.StolenBases * volume);
		int singles = Math.Max(0, hits - doubles - triples - homeRuns);
		int plateAppearances = atBats + walks;
		float average = atBats == 0 ? 0f : hits / (float)atBats;
		float onBase = plateAppearances == 0 ? 0f : (hits + walks) / (float)plateAppearances;
		float slugging = atBats == 0 ? 0f : (singles + (2 * doubles) + (3 * triples) + (4 * homeRuns)) / (float)atBats;
		return new HubSeasonHitting(
			games, atBats, runs, hits, doubles, triples, homeRuns, rbi, walks, strikeouts, stolenBases, average, onBase, slugging);
	}

	private static HubSeasonPitching ScalePitching(HubSeasonPitching current, float progress)
	{
		if (progress >= 0.999f)
		{
			return current;
		}

		float volume = Lerp(0.32f, 0.88f, progress);
		float command = Lerp(0.78f, 0.97f, progress);
		int games = Math.Max(2, RoundStat(current.Games * volume));
		int gamesStarted = Math.Min(games, RoundStat(current.GamesStarted * volume));
		int wins = RoundStat(current.Wins * volume * command);
		int losses = RoundStat(current.Losses * volume / command);
		int saves = RoundStat(current.Saves * volume);
		float innings = Math.Max(3f, (float)Math.Round(current.Innings * volume, 1));
		int hits = RoundStat(current.Hits * volume / command);
		int walks = RoundStat(current.Walks * volume / command);
		int strikeouts = RoundStat(current.Strikeouts * volume * command);
		int homeRuns = RoundStat(current.HomeRuns * volume / command);
		int runs = RoundStat(current.Runs * volume / command);
		int earnedRuns = Math.Min(runs, RoundStat(current.EarnedRuns * volume / command));
		float era = innings <= 0f ? 0f : earnedRuns * 9f / innings;
		float whip = innings <= 0f ? 0f : (walks + hits) / innings;
		return new HubSeasonPitching(
			games, gamesStarted, wins, losses, saves, innings, hits, runs, earnedRuns, walks, strikeouts, homeRuns, era, whip);
	}

	private static HubSeasonHitting FromBatter(HubBatter batter) =>
		new(
			batter.Games,
			batter.AtBats,
			batter.Runs,
			batter.Hits,
			batter.Doubles,
			batter.Triples,
			batter.HomeRuns,
			batter.Rbi,
			batter.Walks,
			batter.Strikeouts,
			batter.StolenBases,
			batter.Average,
			batter.OnBase,
			batter.Slugging);

	private static HubSeasonPitching FromPitcher(HubPitcher pitcher) =>
		new(
			pitcher.Games,
			pitcher.GamesStarted,
			pitcher.Wins,
			pitcher.Losses,
			pitcher.Saves,
			pitcher.Innings,
			pitcher.Hits,
			pitcher.Runs,
			pitcher.EarnedRuns,
			pitcher.Walks,
			pitcher.Strikeouts,
			Math.Max(0, (int)Math.Round(pitcher.Innings * 0.07f)),
			pitcher.Era,
			pitcher.Whip);

	private static HubSeasonHitting FromClubhouseHit(HittingLine hit)
	{
		int hits = Math.Max(0, (int)Math.Round(hit.Average * hit.AtBats));
		int doubles = Math.Max(0, hits / 4);
		int triples = hit.StolenBases >= 10 ? 2 : hits >= 20 ? 1 : 0;
		int runs = Math.Max(hit.Rbi / 2, (int)(hits * 0.65f) + hit.Walks / 3);
		return new HubSeasonHitting(
			hit.Games,
			hit.AtBats,
			runs,
			hits,
			doubles,
			triples,
			hit.HomeRuns,
			hit.Rbi,
			hit.Walks,
			hit.Strikeouts,
			hit.StolenBases,
			hit.Average,
			hit.OnBase,
			hit.Slugging);
	}

	private static HubSeasonPitching FromClubhousePit(PitchingLine pit)
	{
		int hits = Math.Max(0, (int)Math.Round((pit.Whip * pit.Innings) - pit.Walks));
		int earned = Math.Max(0, (int)Math.Round(pit.Era * pit.Innings / 9f));
		return new HubSeasonPitching(
			pit.Games,
			pit.GamesStarted,
			pit.Wins,
			pit.Losses,
			pit.Saves,
			pit.Innings,
			hits,
			earned + 1,
			earned,
			pit.Walks,
			pit.Strikeouts,
			Math.Max(0, (int)Math.Round(pit.Innings * 0.07f)),
			pit.Era,
			pit.Whip);
	}

	private static (int Overall, int Contact, int Power, int Speed, int Arm, int Field) RatingsFromBatter(HubBatter batter, int seed) =>
		RatingsFromBatter(
			new HubSeasonHitting(
				batter.Games, batter.AtBats, batter.Runs, batter.Hits, batter.Doubles, batter.Triples,
				batter.HomeRuns, batter.Rbi, batter.Walks, batter.Strikeouts, batter.StolenBases,
				batter.Average, batter.OnBase, batter.Slugging),
			batter.Position,
			seed);

	private static (int Overall, int Contact, int Power, int Speed, int Arm, int Field) RatingsFromBatter(
		HubSeasonHitting hit,
		string position,
		int seed)
	{
		int contact = ClampInt(42 + (int)(hit.Average * 130), 44, 94);
		int power = ClampInt(40 + hit.HomeRuns * 5 + hit.Doubles, 40, 94);
		int speed = ClampInt(42 + hit.StolenBases * 3 + (position is "CF" or "SS" or "2B" ? 8 : 0), 40, 94);
		int arm = ClampInt(50 + (position is "C" or "SS" or "RF" or "3B" ? 14 : 4) + Math.Abs(seed) % 8, 45, 92);
		int field = ClampInt(52 + (position is "SS" or "C" or "CF" ? 14 : 6) - hit.Strikeouts / 6, 46, 92);
		int overall = ClampInt((int)(contact * 0.28f + power * 0.22f + speed * 0.16f + arm * 0.14f + field * 0.20f), 50, 92);
		return (overall, contact, power, speed, arm, field);
	}

	private static (int Overall, int Contact, int Power, int Speed, int Arm, int Field) RatingsFromPitcher(HubPitcher pitcher, int seed) =>
		RatingsFromPitcher(
			new HubSeasonPitching(
				pitcher.Games, pitcher.GamesStarted, pitcher.Wins, pitcher.Losses, pitcher.Saves, pitcher.Innings,
				pitcher.Hits, pitcher.Runs, pitcher.EarnedRuns, pitcher.Walks, pitcher.Strikeouts, 0, pitcher.Era, pitcher.Whip),
			seed);

	private static (int Overall, int Contact, int Power, int Speed, int Arm, int Field) RatingsFromPitcher(
		HubSeasonPitching pit,
		int seed)
	{
		int arm = ClampInt(90 - (int)(pit.Era * 8) + Math.Abs(seed) % 4, 54, 94);
		int overall = ClampInt(86 - (int)(pit.Era * 7) - (int)((pit.Whip - 1f) * 12), 52, 92);
		int contact = 32 + Math.Abs(seed) % 8;
		int power = 28 + Math.Abs(seed / 3) % 8;
		int speed = 48 + Math.Abs(seed / 5) % 12;
		int field = 38 + Math.Abs(seed / 7) % 10;
		return (overall, contact, power, speed, arm, field);
	}

	private static string? ClassYearFromAwards(string name)
	{
		foreach (HubAward award in DistrictHubData.Awards)
		{
			if (string.Equals(award.Leader.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return award.Leader.Year;
			}

			foreach (HubAwardCandidate candidate in award.Competitors)
			{
				if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					return candidate.Year;
				}
			}
		}

		return null;
	}

	private static string? PortraitFor(string name) =>
		string.Equals(name, "Tony Romano", StringComparison.OrdinalIgnoreCase) ? TonyPortrait : null;

	private static string? PriorOrg(HubTeam team, string classYear, int seed)
	{
		if (classYear.Equals("FR", StringComparison.OrdinalIgnoreCase) || seed % 2 == 0)
		{
			return null;
		}

		List<HubTeam> others = [];
		foreach (HubTeam candidate in DistrictHubData.Teams)
		{
			if (candidate.DistrictId == team.DistrictId && candidate.Id != team.Id)
			{
				others.Add(candidate);
			}
		}

		if (others.Count == 0)
		{
			return null;
		}

		HubTeam prior = others[Math.Abs(seed) % others.Count];
		return $"{prior.Name} High School";
	}

	private static string BatsThrowsForHitter(int seed) =>
		(Math.Abs(seed) % 10) switch
		{
			< 6 => "R/R",
			< 8 => "L/R",
			< 9 => "L/L",
			_ => "S/R",
		};

	private static string StatusFromSeed(int seed) =>
		Math.Abs(seed) % 31 == 0 ? "Out" : Math.Abs(seed) % 17 == 0 ? "Day-to-Day" : "Healthy";

	private static int JerseyFromName(string name)
	{
		int hash = 17;
		foreach (char c in name)
		{
			hash = hash * 31 + c;
		}

		return Math.Abs(hash % 89) + 1;
	}

	private static HubBatter? FindKnownBatter(string name)
	{
		foreach (HubBatter batter in DistrictHubData.Batters)
		{
			if (string.Equals(batter.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return batter;
			}
		}

		return null;
	}

	private static HubPitcher? FindKnownPitcher(string name)
	{
		foreach (HubPitcher pitcher in DistrictHubData.Pitchers)
		{
			if (string.Equals(pitcher.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return pitcher;
			}
		}

		return null;
	}

	private static HubSquadPlayer? FindByName(List<HubSquadPlayer> list, string name)
	{
		foreach (HubSquadPlayer player in list)
		{
			if (string.Equals(player.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return player;
			}
		}

		return null;
	}

	private static float TeamQuality(HubTeam team, TeamLevel level)
	{
		int wins = team.Wins(level);
		int games = wins + team.Losses(level);
		return games == 0 ? 0.5f : wins / (float)games;
	}

	private static string NextName(int seed, List<HubSquadPlayer> existing)
	{
		for (int i = 0; i < 80; i++)
		{
			string name = $"{FirstNames[Math.Abs(seed + i * 3) % FirstNames.Length]} {LastNames[Math.Abs(seed + i * 7) % LastNames.Length]}";
			if (!ContainsName(existing, name))
			{
				return name;
			}
		}

		return $"Player {existing.Count + 1}";
	}

	private static bool ContainsName(List<HubSquadPlayer> list, string name) => FindByName(list, name) != null;

	private static int CountHitters(List<HubSquadPlayer> list)
	{
		int count = 0;
		foreach (HubSquadPlayer player in list)
		{
			if (player.CurrentHitting != null)
			{
				count++;
			}
		}

		return count;
	}

	private static int CountPitchers(List<HubSquadPlayer> list)
	{
		int count = 0;
		foreach (HubSquadPlayer player in list)
		{
			if (player.CurrentPitching != null)
			{
				count++;
			}
		}

		return count;
	}

	private static (string First, string Last) SplitName(string name)
	{
		int space = name.LastIndexOf(' ');
		return space <= 0 ? (name, name) : (name[..space], name[(space + 1)..]);
	}

	private static int StableHash(string value)
	{
		int hash = 17;
		foreach (char c in value)
		{
			hash = hash * 31 + c;
		}

		return hash;
	}

	private static float Clamp(float value, float min, float max) =>
		value < min ? min : value > max ? max : value;

	private static int ClampInt(int value, int min, int max) =>
		value < min ? min : value > max ? max : value;

	private static float Lerp(float a, float b, float t) => a + (b - a) * t;

	private static int RoundStat(float value) => Math.Max(0, (int)Math.Round(value));
}
