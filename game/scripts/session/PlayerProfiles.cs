using System;
using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// Builds the player profile snapshot used by every hub screen. Prototype
/// squads already carry overall, tools, and seasons; this layer fills the
/// identity, potential, and scout copy the profile card needs.
/// </summary>
public static class PlayerProfiles
{
	private static readonly string[] Personalities =
	[
		"Competitive", "Composed", "Vocal", "Quiet Leader", "Fiery", "Coachable", "Easygoing", "Intense",
	];

	public static PlayerProfileSnapshot? Find(string teamId, string playerName, TeamLevel level)
	{
		string name = (playerName ?? string.Empty).Trim();
		if (name.Length == 0)
		{
			return null;
		}

		GameWorld? world = GameSession.Current?.World;
		if (world != null)
		{
			Player? live = GameSession.Current.FindWorldPlayer(teamId, name, level);
			if (live != null)
			{
				return FromWorld(live, world);
			}
		}

		string id = string.IsNullOrWhiteSpace(teamId) ? string.Empty : SchoolProfiles.NormalizeId(teamId);
		if (id.Length > 0)
		{
			HubSquadPlayer? match = DistrictHubSquads.FindPlayer(id, name, level);
			if (match != null)
			{
				return From(match);
			}
		}

		foreach (HubSquadPlayer player in DistrictHubSquads.AllPlayers(level))
		{
			if (NamesMatch(player, name) && (id.Length == 0 || player.TeamId == id))
			{
				return From(player);
			}
		}

		TeamLevel other = level == TeamLevel.Varsity ? TeamLevel.JuniorVarsity : TeamLevel.Varsity;
		foreach (HubSquadPlayer player in DistrictHubSquads.AllPlayers(other))
		{
			if (NamesMatch(player, name) && (id.Length == 0 || player.TeamId == id))
			{
				return From(player);
			}
		}

		return null;
	}

	public static PlayerProfileSnapshot From(HubSquadPlayer player, bool captain = false)
	{
		GameWorld? world = GameSession.Current?.World;
		if (world != null)
		{
			TeamLevel level = TeamLevel.Varsity.MatchesSquad(player.Squad)
				? TeamLevel.Varsity
				: TeamLevel.JuniorVarsity;
			Player? live = GameSession.Current.FindWorldPlayer(player.TeamId, player.Name, level);
			if (live != null)
			{
				return FromWorld(live, world, captain);
			}
		}

		return From(
			player.FirstName,
			player.LastName,
			player.Jersey,
			player.Position,
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
			player.TeamId,
			player.TeamShort,
			player.Squad,
			player.Seasons,
			captain);
	}

	public static PlayerProfileSnapshot FromWorld(
		Player player,
		GameWorld world,
		bool captain = false,
		bool fogged = false)
	{
		string teamShort = TeamShortFor(world, player);
		string squad = player.IsUnsigned
			? "Unsigned"
			: player.TeamLevel == TeamLevel.Varsity ? "Varsity" : "JV";
		PlayerRatings ratings = player.Ratings;
		bool pitcher = player.IsPitcher;
		int seed = player.Id.GetHashCode();
		string bats = player.Bats.ToAbbrev();
		int contactR = SplitContact(ratings.Contact, bats, true, seed);
		int contactL = SplitContact(ratings.Contact, bats, false, seed);
		IReadOnlyList<HubPlayerSeason> seasons = SeasonsFromWorld(player, world);
		string health = fogged ? "?" : "Healthy";
		int strength = player.Physical.Strength > 0
			? player.Physical.Strength
			: Clamp(((ratings.Power + ratings.ArmStrength) / 2) + ((seed >> 2) % 7) - 3);
		int reaction = Clamp(((ratings.Fielding + ratings.Speed) / 2) + ((seed >> 5) % 7) - 3);

		return new PlayerProfileSnapshot(
			player.FirstName,
			player.LastName,
			player.JerseyNumber,
			player.DisplayPosition,
			player.Grade.ToAbbrev(),
			YearLabel(player.Grade.ToAbbrev()),
			player.Bats.ToBatsThrowsLabel(player.Throws),
			fogged || player.Physical.HeightInches <= 0 ? "?" : player.Physical.HeightLabel,
			fogged ? 0 : player.Physical.WeightLbs,
			player.Overall,
			player.Potential,
			player.Personality.Label(),
			DevelopmentFor(player.Grade.ToAbbrev(), player.Overall, player.Potential, seed),
			health,
			null,
			player.OrganizationId,
			teamShort,
			squad,
			pitcher,
			captain,
			fogged
				? new PlayerHittingAttributes(0, 0, 0, 0)
				: new PlayerHittingAttributes(contactR, contactL, ratings.Power, ratings.PlateDiscipline),
			fogged
				? new PlayerFieldingAttributes(0, 0, 0)
				: new PlayerFieldingAttributes(ratings.Fielding, ratings.ArmStrength, reaction),
			fogged
				? new PlayerPhysicalAttributes(0, 0)
				: new PlayerPhysicalAttributes(ratings.Speed, strength),
			pitcher
				? (fogged
					? new PlayerPitchingAttributes(0, 0, 0, 0)
					: new PlayerPitchingAttributes(ratings.Stuff, ratings.Control, ratings.Movement, ratings.Stamina))
				: null,
			seasons,
			BuildHistory(player.FirstName, player.LastName, player.Grade.ToAbbrev(), seasons, captain, health, seed),
			BuildScoutReport(
				player.FirstName,
				player.LastName,
				player.DisplayPosition,
				player.Grade.ToAbbrev(),
				player.Overall,
				player.Potential,
				pitcher,
				seed),
			Fogged: fogged,
			RecruitingHook: player.Priorities.Hook,
			Appearance: player.Appearance);
	}

	public static PlayerProfileSnapshot From(
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
		string teamId,
		string teamShort,
		string squad,
		IReadOnlyList<HubPlayerSeason> seasons,
		bool captain = false)
	{
		int seed = StableSeed($"{firstName}|{lastName}|{jersey}|{teamId}");
		bool pitcher = IsPitcher(position);
		int potential = PotentialFor(overall, year, seed);
		string bats = BatsSide(batsThrows);
		int contactR = SplitContact(contact, bats, true, seed);
		int contactL = SplitContact(contact, bats, false, seed);
		int plateVision = Clamp(contact + ((seed >> 3) % 9) - 3);
		int reaction = Clamp(((field + speed) / 2) + ((seed >> 5) % 7) - 3);
		int strength = Clamp(((power + arm) / 2) + ((seed >> 2) % 7) - 3);

		PlayerPitchingAttributes? pitching = pitcher
			? new PlayerPitchingAttributes(
				Clamp(arm + ((seed >> 4) % 7) - 2),
				Clamp(overall - 4 + ((seed >> 6) % 9) - 3),
				Clamp(((arm + overall) / 2) + ((seed >> 1) % 7) - 3),
				Clamp(overall - 6 + ((seed >> 7) % 11) - 4))
			: null;

		return new PlayerProfileSnapshot(
			firstName,
			lastName,
			jersey,
			position,
			year,
			YearLabel(year),
			batsThrows,
			HeightLabel(seed),
			WeightLbs(seed),
			overall,
			potential,
			Personalities[Math.Abs(seed) % Personalities.Length],
			DevelopmentFor(year, overall, potential, seed),
			string.IsNullOrWhiteSpace(status) ? "Healthy" : status,
			portraitPath,
			teamId,
			teamShort,
			squad,
			pitcher,
			captain,
			new PlayerHittingAttributes(contactR, contactL, power, plateVision),
			new PlayerFieldingAttributes(field, arm, reaction),
			new PlayerPhysicalAttributes(speed, strength),
			pitching,
			seasons,
			BuildHistory(firstName, lastName, year, seasons, captain, status, seed),
			BuildScoutReport(firstName, lastName, position, year, overall, potential, pitcher, seed));
	}

	private static string TeamShortFor(GameWorld world, Player player)
	{
		if (player.IsUnsigned)
		{
			return "Open Play";
		}

		foreach (OrganizationState org in world.Organizations)
		{
			if (org.Id == player.OrganizationId)
			{
				return org.ShortName;
			}
		}

		return player.OrganizationId;
	}

	public static List<HubPlayerSeason> SeasonsFromWorld(Player player, GameWorld world)
	{
		var seasons = new List<HubPlayerSeason>();
		foreach (BattingSeasonStats hitting in world.BattingStats)
		{
			if (hitting.PlayerId != player.Id || hitting.AtBats <= 0)
			{
				continue;
			}

			PitchingSeasonStats? pitching = null;
			foreach (PitchingSeasonStats arm in world.PitchingStats)
			{
				if (arm.PlayerId == player.Id && arm.SeasonYear == hitting.SeasonYear && arm.OutsRecorded > 0)
				{
					pitching = arm;
					break;
				}
			}

			seasons.Add(new HubPlayerSeason(
				hitting.SeasonYear,
				TeamShortFor(world, player),
				new HubSeasonHitting(
					hitting.Games,
					hitting.AtBats,
					hitting.Runs,
					hitting.Hits,
					hitting.Doubles,
					hitting.Triples,
					hitting.HomeRuns,
					hitting.RunsBattedIn,
					hitting.Walks,
					hitting.Strikeouts,
					hitting.StolenBases,
					(float)hitting.Average,
					(float)hitting.OnBasePercentage,
					(float)hitting.Slugging),
				player.IsPitcher && pitching != null ? ToHubPitching(pitching) : null));
		}

		if (player.IsPitcher)
		{
			foreach (PitchingSeasonStats arm in world.PitchingStats)
			{
				if (arm.PlayerId != player.Id || arm.OutsRecorded <= 0)
				{
					continue;
				}

				bool exists = false;
				foreach (HubPlayerSeason season in seasons)
				{
					if (season.Year == arm.SeasonYear)
					{
						exists = true;
						break;
					}
				}

				if (!exists)
				{
					seasons.Add(new HubPlayerSeason(
						arm.SeasonYear,
						TeamShortFor(world, player),
						null,
						ToHubPitching(arm)));
				}
			}
		}

		seasons.Sort((a, b) => a.Year.CompareTo(b.Year));
		return seasons;
	}

	private static HubSeasonPitching ToHubPitching(PitchingSeasonStats arm) =>
		new(
			arm.Games,
			arm.GamesStarted,
			arm.Wins,
			arm.Losses,
			arm.Saves,
			(float)arm.InningsPitched,
			arm.HitsAllowed,
			arm.RunsAllowed,
			arm.EarnedRuns,
			arm.Walks,
			arm.Strikeouts,
			arm.HomeRunsAllowed,
			(float)arm.Era,
			(float)arm.Whip);

	public static bool NamesMatch(HubSquadPlayer player, string query)
	{
		string needle = query.Trim();
		if (needle.Length == 0)
		{
			return false;
		}

		if (string.Equals(player.Name, needle, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(player.LastName, needle, StringComparison.OrdinalIgnoreCase)
			|| string.Equals($"{player.LastName}, {player.FirstName}", needle, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		if (player.FirstName.Length == 0)
		{
			return false;
		}

		string compact = needle.Replace(".", string.Empty, StringComparison.Ordinal);
		return string.Equals($"{player.FirstName[0]} {player.LastName}", compact, StringComparison.OrdinalIgnoreCase)
			|| string.Equals($"{player.FirstName[0]}. {player.LastName}", needle, StringComparison.OrdinalIgnoreCase);
	}

	public static bool IsPitcher(string position)
	{
		string value = position.ToUpperInvariant();
		return value.Contains("HP", StringComparison.Ordinal)
			|| value == "P"
			|| value == "SP"
			|| value == "RP"
			|| value.StartsWith("P/", StringComparison.Ordinal);
	}

	public static string YearLabel(string year) =>
		year.ToUpperInvariant() switch
		{
			"FR" => "Freshman",
			"SO" => "Sophomore",
			"JR" => "Junior",
			"SR" => "Senior",
			_ => year,
		};

	private static string BatsSide(string batsThrows)
	{
		if (string.IsNullOrEmpty(batsThrows))
		{
			return "R";
		}

		char first = char.ToUpperInvariant(batsThrows[0]);
		return first is 'L' or 'S' ? first.ToString() : "R";
	}

	private static int SplitContact(int contact, string bats, bool vsRight, int seed)
	{
		int gap = 4 + (Math.Abs(seed) % 6);
		return bats switch
		{
			"L" => Clamp(vsRight ? contact : contact - gap),
			"S" => Clamp(contact - (gap / 3)),
			_ => Clamp(vsRight ? contact - gap : contact),
		};
	}

	private static int PotentialFor(int overall, string year, int seed)
	{
		int ceiling = year.ToUpperInvariant() switch
		{
			"FR" => 8 + (Math.Abs(seed) % 7),
			"SO" => 5 + (Math.Abs(seed) % 6),
			"JR" => 3 + (Math.Abs(seed) % 5),
			_ => 1 + (Math.Abs(seed) % 4),
		};
		return Math.Max(overall, Clamp(overall + ceiling));
	}

	private static string DevelopmentFor(string year, int overall, int potential, int seed)
	{
		int gap = potential - overall;
		if (year.Equals("FR", StringComparison.OrdinalIgnoreCase) && gap >= 10)
		{
			return "Raw";
		}

		if (gap >= 8)
		{
			return "Breakout";
		}

		if (gap <= 2 && year.Equals("SR", StringComparison.OrdinalIgnoreCase))
		{
			return "Polished";
		}

		return (Math.Abs(seed) % 3) switch
		{
			0 => "Improving",
			1 => "Steady",
			_ => "Coachable",
		};
	}

	private static string HeightLabel(int seed)
	{
		int inches = 68 + (Math.Abs(seed) % 10);
		return $"{inches / 12}'{inches % 12}\"";
	}

	private static int WeightLbs(int seed)
	{
		int inches = 68 + (Math.Abs(seed) % 10);
		return 155 + (Math.Abs(seed / 3) % 40) + ((inches - 68) * 4);
	}

	private static List<PlayerHistoryBeat> BuildHistory(
		string firstName,
		string lastName,
		string year,
		IReadOnlyList<HubPlayerSeason> seasons,
		bool captain,
		string status,
		int seed)
	{
		var beats = new List<PlayerHistoryBeat>();
		string currentOrg = seasons.Count == 0 ? string.Empty : seasons[^1].OrganizationName;
		for (int i = 0; i < seasons.Count; i++)
		{
			HubPlayerSeason season = seasons[i];
			beats.Add(new PlayerHistoryBeat(season.Year.ToString(), season.OrganizationName, SeasonLine(season)));
			if (i == 0
				&& !string.IsNullOrEmpty(currentOrg)
				&& !string.Equals(season.OrganizationName, currentOrg, StringComparison.OrdinalIgnoreCase))
			{
				beats.Add(new PlayerHistoryBeat(
					season.Year.ToString(),
					season.OrganizationName,
					$"Arrived from {season.OrganizationName} before joining {currentOrg}."));
			}
		}

		if (captain)
		{
			beats.Add(new PlayerHistoryBeat(
				DistrictHubData.SeasonYear.ToString(),
				currentOrg,
				$"{firstName} {lastName} was named a program captain."));
		}

		if (!string.Equals(status, "Healthy", StringComparison.OrdinalIgnoreCase))
		{
			beats.Add(new PlayerHistoryBeat(
				DistrictHubData.SeasonYear.ToString(),
				currentOrg,
				$"Currently listed as {status.ToLowerInvariant()} on the training report."));
		}

		if (year.Equals("SR", StringComparison.OrdinalIgnoreCase) && Math.Abs(seed) % 3 == 0)
		{
			beats.Add(new PlayerHistoryBeat(
				(DistrictHubData.SeasonYear - 1).ToString(),
				currentOrg,
				"Earned all-district honorable mention last spring."));
		}

		if (beats.Count == 0)
		{
			beats.Add(new PlayerHistoryBeat(
				DistrictHubData.SeasonYear.ToString(),
				currentOrg,
				"No archived high school log yet. This is the first season on the books."));
		}

		return beats;
	}

	private static string SeasonLine(HubPlayerSeason season)
	{
		if (season.Pitching != null)
		{
			HubSeasonPitching pitching = season.Pitching;
			return $"{pitching.Wins}-{pitching.Losses}, {pitching.Era:0.00} ERA, {pitching.Strikeouts} K in {pitching.Innings:0.0} IP";
		}

		if (season.Hitting != null)
		{
			HubSeasonHitting hitting = season.Hitting;
			return $"{hitting.Average:.000}, {hitting.HomeRuns} HR, {hitting.Rbi} RBI, {hitting.StolenBases} SB";
		}

		return "No recorded line.";
	}

	private static PlayerScoutReport BuildScoutReport(
		string firstName,
		string lastName,
		string position,
		string year,
		int overall,
		int potential,
		bool pitcher,
		int seed)
	{
		string role = pitcher
			? overall >= 78 ? "Friday starter" : overall >= 70 ? "Rotation arm" : "Bullpen piece"
			: overall >= 78 ? $"Everyday {PrimarySpot(position)}" : overall >= 70 ? $"Regular at {PrimarySpot(position)}" : "Role player";
		string future = potential >= overall + 8 ? "Plus projection" : potential >= overall + 4 ? "Solid projection" : "Close to finished";
		string body = pitcher
			? $"{firstName} {lastName} shows a {GradeWord(overall)} fastball look with {future.ToLowerInvariant()}. Command will decide whether this is a starter or a leverage arm."
			: $"{firstName} {lastName} is a {GradeWord(overall)} {PrimarySpot(position)} with {future.ToLowerInvariant()}. The hit tool and defensive reliability are the skills that will carry the profile.";
		return new PlayerScoutReport(role, future, body, pitcher ? "a district mid-rotation arm" : "a contact-first varsity regular", overall);
	}

	private static string PrimarySpot(string position)
	{
		int slash = position.IndexOf('/');
		return slash > 0 ? position[..slash] : position;
	}

	private static string GradeWord(int overall) =>
		overall >= 85 ? "plus" : overall >= 75 ? "average-to-plus" : overall >= 65 ? "playable" : "fringe";

	private static int StableSeed(string key)
	{
		unchecked
		{
			int hash = 23;
			foreach (char c in key.ToUpperInvariant())
			{
				hash = (hash * 31) + c;
			}

			return hash == int.MinValue ? 17 : Math.Abs(hash);
		}
	}

	private static int Clamp(int value) => Math.Clamp(value, 1, 99);
}

public sealed record PlayerProfileSnapshot(
	string FirstName,
	string LastName,
	int Jersey,
	string Position,
	string Year,
	string YearLabel,
	string BatsThrows,
	string HeightLabel,
	int WeightLbs,
	int Overall,
	int Potential,
	string Personality,
	string Development,
	string Health,
	string? PortraitPath,
	string TeamId,
	string TeamShort,
	string Squad,
	bool IsPitcher,
	bool IsCaptain,
	PlayerHittingAttributes Hitting,
	PlayerFieldingAttributes Fielding,
	PlayerPhysicalAttributes Physical,
	PlayerPitchingAttributes? Pitching,
	IReadOnlyList<HubPlayerSeason> Seasons,
	IReadOnlyList<PlayerHistoryBeat> History,
	PlayerScoutReport Scout,
	bool Fogged = false,
	string? OverallLabel = null,
	string? PotentialLabel = null,
	string? StatsHeader = null,
	string? RecruitingHook = null,
	string? SchoolInterest = null,
	PersonAppearance? Appearance = null)
{
	public string FullName => $"{FirstName} {LastName}";

	public string DisplayName => FullName.ToUpperInvariant();
}

public sealed record PlayerHittingAttributes(int ContactRight, int ContactLeft, int Power, int PlateVision);

public sealed record PlayerFieldingAttributes(int Fielding, int Arm, int Reaction);

public sealed record PlayerPhysicalAttributes(int Speed, int Strength);

public sealed record PlayerPitchingAttributes(int Stuff, int Control, int Movement, int Stamina);

public sealed record PlayerHistoryBeat(string Year, string Organization, string Detail);

public sealed record PlayerScoutReport(string Role, string Projection, string Summary, string Comparable, int PresentGrade);
