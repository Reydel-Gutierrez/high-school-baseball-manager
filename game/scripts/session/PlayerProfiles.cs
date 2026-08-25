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

	public static PlayerProfileSnapshot From(HubSquadPlayer player, bool captain = false) =>
		From(
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
		bool captain = false,
		PlayerAgreementSnapshot? agreement = null)
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
			BuildScoutReport(firstName, lastName, position, year, overall, potential, pitcher, seed),
			Agreement: agreement);
	}

	public static PlayerProfileSnapshot FromOpenPlay(
		Player player,
		DistrictOpenPlay openPlay,
		DateOnly today,
		string teamShort,
		PlayerAgreementSnapshot? agreement = null)
	{
		int looks = Math.Max(1, openPlay.LooksOn(today));
		OpenPlayLine? line = openPlay.LineFor(player.Id);
		bool pitcher = player.IsPitcher;
		var seasons = new List<HubPlayerSeason>();
		if (line != null && looks > 0 && (line.Hitting.AtBats > 0 || (line.Pitching != null && line.Pitching.Outs > 0)))
		{
			seasons.Add(new HubPlayerSeason(
				openPlay.SeasonYear,
				"District Open Play",
				ToHitting(line.Hitting),
				pitcher ? ToPitching(line.Pitching) : null));
		}

		bool committed = agreement != null;
		return new PlayerProfileSnapshot(
			player.FirstName,
			player.LastName,
			player.JerseyNumber,
			player.DisplayPosition,
			player.Grade.ToAbbrev(),
			YearLabel(player.Grade.ToAbbrev()),
			player.Bats.ToBatsThrowsLabel(player.Throws),
			committed ? HeightLabel(player.Id.GetHashCode()) : "?",
			committed ? WeightLbs(player.Id.GetHashCode()) : 0,
			player.Overall,
			player.Potential,
			player.Priorities.Label,
			player.Priorities.Hook,
			committed ? "Healthy" : "?",
			null,
			player.OrganizationId,
			string.IsNullOrEmpty(teamShort) ? "Open Play" : teamShort,
			committed ? (player.TeamLevel == TeamLevel.Varsity ? "Varsity" : "JV") : "Unsigned",
			pitcher,
			false,
			new PlayerHittingAttributes(0, 0, 0, 0),
			new PlayerFieldingAttributes(0, 0, 0),
			new PlayerPhysicalAttributes(0, 0),
			pitcher ? new PlayerPitchingAttributes(0, 0, 0, 0) : null,
			seasons,
			[],
			new PlayerScoutReport(
				player.Priorities.Label,
				player.Priorities.Hook,
				$"{player.FullName} is in the district Open Play pool. {player.Priorities.Hook}.",
				"?",
				looks),
			Fogged: !committed,
			OverallLabel: TryoutScouting.FormatOverall(player.Overall, looks, player.Id.GetHashCode()),
			PotentialLabel: TryoutScouting.FormatPotential(player.Overall, player.Potential, looks, player.Id.GetHashCode()),
			StatsHeader: "OPEN PLAY",
			RecruitingHook: player.Priorities.Hook,
			Agreement: agreement);
	}

	public static PlayerProfileSnapshot FromTryout(TryoutProspect prospect, TryoutDay day)
	{
		string year = prospect.Year.ToAbbrev();
		bool pitcher = prospect.Position.IsPitcher();
		TryoutHittingLine hitting = prospect.HittingOn(day);
		TryoutPitchingLine? pitching = prospect.PitchingOn(day);
		string period = day == TryoutDay.Final ? "Tryout Week" : $"Day {(int)day + 1}";
		var seasons = new List<HubPlayerSeason>();
		if (hitting.AtBats > 0 || (pitching != null && pitching.Outs > 0))
		{
			seasons.Add(new HubPlayerSeason(
				DistrictHubData.SeasonYear,
				period,
				ToHitting(hitting),
				pitcher ? ToPitching(pitching) : null));
		}

		return new PlayerProfileSnapshot(
			prospect.FirstName,
			prospect.LastName,
			0,
			prospect.Position.ToDisplayLabel(prospect.Throws),
			year,
			YearLabel(year),
			prospect.Bats.ToBatsThrowsLabel(prospect.Throws),
			"?",
			0,
			prospect.TrueOverall,
			prospect.TruePotential,
			"?",
			"?",
			"?",
			null,
			string.Empty,
			"Tryout Camp",
			"Varsity",
			pitcher,
			false,
			new PlayerHittingAttributes(0, 0, 0, 0),
			new PlayerFieldingAttributes(0, 0, 0),
			new PlayerPhysicalAttributes(0, 0),
			pitcher ? new PlayerPitchingAttributes(0, 0, 0, 0) : null,
			seasons,
			[],
			new PlayerScoutReport(
				"?",
				"?",
				"Camp file is still incomplete. Present tools stay behind a range until the staff has a longer look.",
				"?",
				0),
			Fogged: true,
			OverallLabel: TryoutScouting.FormatOverall(prospect, day),
			PotentialLabel: TryoutScouting.FormatPotential(prospect, day),
			StatsHeader: day == TryoutDay.Final ? "TRYOUT WEEK" : $"DAY {(int)day + 1}");
	}

	public static PlayerProfileSnapshot FromBoard(FinalRosterPlayer player, TryoutCamp camp)
	{
		if (player.FromTryout)
		{
			TryoutProspect? prospect = camp.Find(player.Id);
			if (prospect != null)
			{
				return FromTryout(prospect, TryoutDay.Final) with
				{
					TeamShort = "Cascade",
					Squad = ProgramRosterRules.AssignmentLabel(player.Assignment),
				};
			}
		}

		TeamLevel level = player.Assignment == RosterAssignment.JuniorVarsity
			? TeamLevel.JuniorVarsity
			: TeamLevel.Varsity;
		HubSquadPlayer? hub = DistrictHubSquads.FindPlayer(DistrictHubData.UserTeamId, player.FullName, level);
		if (hub != null)
		{
			return From(hub);
		}

		return From(
			player.FirstName,
			player.LastName,
			player.Jersey,
			player.PositionLabel,
			player.Year.ToAbbrev(),
			player.Bats.ToBatsThrowsLabel(player.Throws),
			player.Overall,
			50,
			50,
			50,
			50,
			50,
			"Healthy",
			null,
			DistrictHubData.UserTeamId,
			"Cascade",
			ProgramRosterRules.AssignmentLabel(player.Assignment),
			[]);
	}

	public static PlayerProfileSnapshot FromPool(OpenPoolPlayer player, TryoutCamp camp)
	{
		TryoutProspect? prospect = camp.Find(player.Id);
		if (prospect != null)
		{
			return FromTryout(prospect, TryoutDay.Final) with
			{
				TeamId = string.Empty,
				TeamShort = "Open Pool",
				Squad = "Pool",
			};
		}

		string year = player.Year.ToAbbrev();
		bool pitcher = player.Position.IsPitcher();
		return new PlayerProfileSnapshot(
			player.FirstName,
			player.LastName,
			0,
			player.PositionLabel,
			year,
			YearLabel(year),
			player.Bats.ToBatsThrowsLabel(player.Throws),
			"?",
			0,
			player.TrueOverall,
			player.TruePotential,
			"?",
			"?",
			"?",
			null,
			string.Empty,
			"Open Pool",
			"Pool",
			pitcher,
			false,
			new PlayerHittingAttributes(0, 0, 0, 0),
			new PlayerFieldingAttributes(0, 0, 0),
			new PlayerPhysicalAttributes(0, 0),
			pitcher ? new PlayerPitchingAttributes(0, 0, 0, 0) : null,
			[],
			[],
			new PlayerScoutReport(
				"?",
				"?",
				"Available from the District Open Pool. Any program can sign him if they have roster room.",
				"?",
				0),
			Fogged: true,
			OverallLabel: TryoutScouting.FormatOverall(player.TrueOverall, 3, player.Id.GetHashCode()),
			PotentialLabel: TryoutScouting.FormatPotential(player.TrueOverall, player.TruePotential, 3, player.Id.GetHashCode()),
			StatsHeader: "OPEN POOL");
	}

	private static HubSeasonHitting ToHitting(TryoutHittingLine line) =>
		new(
			line.Games,
			line.AtBats,
			line.Runs,
			line.Hits,
			line.Doubles,
			line.Triples,
			line.HomeRuns,
			line.Rbi,
			line.Walks,
			line.Strikeouts,
			line.StolenBases,
			line.Average,
			line.OnBase,
			line.Slugging);

	private static HubSeasonPitching? ToPitching(TryoutPitchingLine? line) =>
		line == null || line.Outs <= 0
			? null
			: new HubSeasonPitching(
				line.Games,
				line.GamesStarted,
				line.Wins,
				line.Losses,
				line.Saves,
				line.Innings,
				line.Hits,
				line.Runs,
				line.EarnedRuns,
				line.Walks,
				line.Strikeouts,
				line.HomeRuns,
				line.Era,
				line.Whip);

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
	PlayerAgreementSnapshot? Agreement = null)
{
	public string FullName => $"{FirstName} {LastName}";

	public string DisplayName => FullName.ToUpperInvariant();
}

public sealed record PlayerAgreementSnapshot(
	string School,
	int StartSeasonYear,
	int YearsRemaining,
	int TermYears,
	int AnnualSponsorship,
	int TotalCommitment)
{
	public string AnnualLabel => PlayerAgreement.FormatMoney(AnnualSponsorship);

	public string TotalLabel => PlayerAgreement.FormatMoney(TotalCommitment);

	public string TermLabel =>
		YearsRemaining <= 1
			? $"{YearsRemaining} year remaining"
			: $"{YearsRemaining} years remaining";
}

public sealed record PlayerHittingAttributes(int ContactRight, int ContactLeft, int Power, int PlateVision);

public sealed record PlayerFieldingAttributes(int Fielding, int Arm, int Reaction);

public sealed record PlayerPhysicalAttributes(int Speed, int Strength);

public sealed record PlayerPitchingAttributes(int Stuff, int Control, int Movement, int Stamina);

public sealed record PlayerHistoryBeat(string Year, string Organization, string Detail);

public sealed record PlayerScoutReport(string Role, string Projection, string Summary, string Comparable, int PresentGrade);
