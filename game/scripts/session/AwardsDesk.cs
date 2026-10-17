using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HSBM.Domain;

/// <summary>
/// Year-end awards slate: District MVP, Pitcher of the Year, Freshman of
/// the Year, All-District, and Coach of the Year, plus the state banquet
/// after the championship. Live careers compile ballots from season stats;
/// the hub prototype is the fallback before a world is bound.
/// </summary>
public static class AwardsDesk
{
	private static readonly Dictionary<string, string> KnownCoaches = new(StringComparer.OrdinalIgnoreCase)
	{
		["great-lakes"] = "Elena Voss",
		["harbor-ridge"] = "Chris Lang",
		["delaware-valley"] = "Paul Gentry",
		["port-clinton"] = "Marcus Quinn",
		["sandusky"] = "Dana Briggs",
		["vermilion"] = "Joel Prado",
		["capital-city"] = "Ray Delgado",
		["springfield"] = "Owen Blake",
		["beavercreek"] = "Lena Hartwell",
		["hibbing"] = "Tom Rourke",
		["duluth-east"] = "Kelly Nye",
		["iron-range"] = "Pat Keegan",
	};

	public static AwardsCeremony For(GameSession session, AwardScope scope)
	{
		if (session.World != null)
		{
			int year = GameSession.SeasonYear;
			SeasonAwards? stored = AwardsEngine.Find(session.World.Awards, year, scope);
			SeasonAwards slate = stored ?? AwardsEngine.Compile(
				session.World,
				scope,
				year,
				session.CatalogOrganizationId,
				session.CoachName,
				IsOfficial(session, scope));
			return FromSlate(slate);
		}

		TeamLevel level = session.ActiveTeam.Level;
		string districtId = DistrictHubData.UserDistrictId;
		HubDistrict district = DistrictHubData.GetDistrict(districtId);
		List<HubSquadPlayer> pool = PlayersFor(scope, districtId, level);
		List<HubTeam> teams = TeamsFor(scope, districtId);

		return new AwardsCeremony(
			scope,
			GameSession.SeasonYear,
			scope == AwardScope.State
				? $"{GameSession.SeasonYear} STATE AWARDS"
				: $"{GameSession.SeasonYear} DISTRICT AWARDS",
			scope == AwardScope.State
				? "STATE BANQUET"
				: $"{district.Name.ToUpperInvariant()} DISTRICT",
			IsOfficial(session, scope),
			scope == AwardScope.State ? "CONTINUE TO SEASON REVIEW" : "CONTINUE",
			[
				BuildPlayerHonor(
					scope == AwardScope.State ? "state-mvp" : "district-mvp",
					scope == AwardScope.State ? "State MVP" : "District MVP",
					"The player who moved the lineup, the scoreboard, and the race.",
					RankHitters(pool),
					HitterHeadline,
					HitterDetail),
				BuildPlayerHonor(
					scope == AwardScope.State ? "state-pitcher" : "district-pitcher",
					"Pitcher of the Year",
					"Friday nights, innings, and the ERA a title run is built on.",
					RankPitchers(pool),
					PitcherHeadline,
					PitcherDetail),
				BuildPlayerHonor(
					scope == AwardScope.State ? "state-freshman" : "district-freshman",
					"Freshman of the Year",
					"A first-year varsity player who already looks like a regular.",
					RankFreshmen(pool),
					FreshmanHeadline,
					FreshmanDetail),
				BuildAllTeamHonor(scope, pool),
				BuildCoachHonor(session, scope, teams, level, district.Name),
			]);
	}

	public static AwardScope? PendingOn(GameSession session)
	{
		AwardScope? scope = SeasonCalendar.AwardCeremonyOn(GameSession.SeasonYear, session.CurrentDate);
		if (scope != null && !session.AwardsAcknowledged(scope.Value))
		{
			return scope;
		}

		if (!session.AwardsAcknowledged(AwardScope.State)
			&& session.ChampionshipAcknowledged(ChampionshipTitle.State)
			&& session.CurrentDate >= SeasonCalendar.ChampionCelebration(GameSession.SeasonYear).Start)
		{
			return AwardScope.State;
		}

		return null;
	}

	public static bool IsOfficial(GameSession session, AwardScope scope)
	{
		SeasonEvent banquet = scope == AwardScope.State
			? SeasonCalendar.StateAwards(GameSession.SeasonYear)
			: SeasonCalendar.DistrictAwards(GameSession.SeasonYear);
		return session.CurrentDate >= banquet.Start;
	}

	public static IReadOnlyList<string> HonorsFor(GameSession session, string fullName)
	{
		var honors = new List<string>();
		CollectHonors(honors, For(session, AwardScope.District), fullName, false);
		if (session.CurrentDate >= SeasonCalendar.StateAwards(GameSession.SeasonYear).Start)
		{
			CollectHonors(honors, For(session, AwardScope.State), fullName, true);
		}

		return honors;
	}

	private static AwardsCeremony FromSlate(SeasonAwards slate)
	{
		var honors = new List<AwardHonor>(slate.Honors.Count);
		foreach (SeasonHonor honor in slate.Honors)
		{
			honors.Add(new AwardHonor(
				honor.Id,
				honor.Name,
				honor.Blurb,
				honor.IsCoachAward,
				honor.IsTeamAward,
				honor.Winner == null ? null : FromHonoree(honor.Winner),
				honor.Field.Select(FromHonoree).ToList()));
		}

		return new AwardsCeremony(
			slate.Scope,
			slate.SeasonYear,
			slate.Headline,
			slate.ScopeLabel,
			slate.Official,
			slate.Scope == AwardScope.State ? "CONTINUE TO SEASON REVIEW" : "CONTINUE",
			honors);
	}

	private static AwardHonoree FromHonoree(SeasonHonoree honoree) =>
		new(
			honoree.Name,
			honoree.Position,
			honoree.Year,
			honoree.OrganizationId,
			honoree.TeamShort,
			honoree.HeadlineStat,
			honoree.Detail,
			honoree.Gap);

	private static void CollectHonors(
		List<string> honors,
		AwardsCeremony ceremony,
		string fullName,
		bool state)
	{
		string prefix = state ? "State " : "";
		foreach (AwardHonor honor in ceremony.Honors)
		{
			if (honor.IsCoachAward)
			{
				continue;
			}

			if (honor.IsTeamAward)
			{
				foreach (AwardHonoree member in honor.Field)
				{
					if (NamesMatch(member.Name, fullName))
					{
						AddUnique(honors, prefix + honor.Name);
					}
				}

				continue;
			}

			if (honor.Winner != null && NamesMatch(honor.Winner.Name, fullName))
			{
				AddUnique(honors, prefix + honor.Name);
			}
		}
	}

	private static void AddUnique(List<string> honors, string honor)
	{
		if (!honors.Contains(honor))
		{
			honors.Add(honor);
		}
	}

	private static List<HubSquadPlayer> PlayersFor(AwardScope scope, string districtId, TeamLevel level)
	{
		var list = new List<HubSquadPlayer>();
		foreach (HubSquadPlayer player in DistrictHubSquads.AllPlayers(level))
		{
			if (scope == AwardScope.District
				&& DistrictHubData.GetTeam(player.TeamId).DistrictId != districtId)
			{
				continue;
			}

			list.Add(player);
		}

		return list;
	}

	private static List<HubTeam> TeamsFor(AwardScope scope, string districtId)
	{
		var list = new List<HubTeam>();
		foreach (HubTeam team in DistrictHubData.Teams)
		{
			if (scope == AwardScope.District && team.DistrictId != districtId)
			{
				continue;
			}

			list.Add(team);
		}

		return list;
	}

	private static AwardHonor BuildPlayerHonor(
		string id,
		string name,
		string blurb,
		List<HubSquadPlayer> ranked,
		Func<HubSquadPlayer, string> headline,
		Func<HubSquadPlayer, string> detail)
	{
		if (ranked.Count == 0)
		{
			return new AwardHonor(id, name, blurb, false, false, null, []);
		}

		HubSquadPlayer winner = ranked[0];
		var field = new List<AwardHonoree>();
		for (int i = 1; i < ranked.Count && field.Count < 3; i++)
		{
			HubSquadPlayer row = ranked[i];
			field.Add(ToHonoree(row, headline(row), detail(row), i));
		}

		return new AwardHonor(
			id,
			name,
			blurb,
			false,
			false,
			ToHonoree(winner, headline(winner), detail(winner), 0),
			field);
	}

	private static AwardHonor BuildAllTeamHonor(AwardScope scope, List<HubSquadPlayer> pool)
	{
		List<AwardHonoree> team = SelectAllTeam(pool);
		return new AwardHonor(
			scope == AwardScope.State ? "all-state" : "all-district",
			scope == AwardScope.State ? "All-State Team" : "All-District Team",
			scope == AwardScope.State
				? "First-team honors across every district in the state."
				: "First-team honors from every school in the district.",
			false,
			true,
			team.Count == 0 ? null : team[0],
			team);
	}

	private static AwardHonor BuildCoachHonor(
		GameSession session,
		AwardScope scope,
		List<HubTeam> teams,
		TeamLevel level,
		string districtName)
	{
		teams.Sort((a, b) =>
		{
			int winCmp = b.Wins(level).CompareTo(a.Wins(level));
			if (winCmp != 0)
			{
				return winCmp;
			}

			int lossCmp = a.Losses(level).CompareTo(b.Losses(level));
			return lossCmp != 0 ? lossCmp : string.CompareOrdinal(a.ShortName, b.ShortName);
		});

		if (teams.Count == 0)
		{
			return new AwardHonor(
				scope == AwardScope.State ? "state-coach" : "district-coach",
				"Coach of the Year",
				"The bench that turned the district race.",
				true,
				false,
				null,
				[]);
		}

		var field = new List<AwardHonoree>();
		for (int i = 1; i < teams.Count && field.Count < 3; i++)
		{
			field.Add(CoachHonoree(session, teams[i], level, i, districtName, scope));
		}

		return new AwardHonor(
			scope == AwardScope.State ? "state-coach" : "district-coach",
			"Coach of the Year",
			"Lineups, Friday pitching, and the way a program carries itself in April.",
			true,
			false,
			CoachHonoree(session, teams[0], level, 0, districtName, scope),
			field);
	}

	private static List<HubSquadPlayer> RankHitters(List<HubSquadPlayer> pool)
	{
		var ranked = new List<HubSquadPlayer>();
		foreach (HubSquadPlayer player in pool)
		{
			if (player.CurrentHitting != null && player.CurrentHitting.AtBats >= 30)
			{
				ranked.Add(player);
			}
		}

		ranked.Sort((a, b) =>
		{
			int cmp = HitterScore(b.CurrentHitting!).CompareTo(HitterScore(a.CurrentHitting!));
			return cmp != 0 ? cmp : string.CompareOrdinal(a.Name, b.Name);
		});
		return ranked;
	}

	private static List<HubSquadPlayer> RankPitchers(List<HubSquadPlayer> pool)
	{
		var ranked = new List<HubSquadPlayer>();
		foreach (HubSquadPlayer player in pool)
		{
			if (player.CurrentPitching != null && player.CurrentPitching.Innings >= 18f)
			{
				ranked.Add(player);
			}
		}

		ranked.Sort((a, b) =>
		{
			int cmp = PitcherScore(b.CurrentPitching!).CompareTo(PitcherScore(a.CurrentPitching!));
			return cmp != 0 ? cmp : string.CompareOrdinal(a.Name, b.Name);
		});
		return ranked;
	}

	private static List<HubSquadPlayer> RankFreshmen(List<HubSquadPlayer> pool)
	{
		var ranked = new List<HubSquadPlayer>();
		foreach (HubSquadPlayer player in pool)
		{
			if (!player.Year.Equals("FR", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (player.CurrentHitting == null && player.CurrentPitching == null)
			{
				continue;
			}

			ranked.Add(player);
		}

		ranked.Sort((a, b) =>
		{
			int cmp = FreshmanScore(b).CompareTo(FreshmanScore(a));
			return cmp != 0 ? cmp : string.CompareOrdinal(a.Name, b.Name);
		});
		return ranked;
	}

	private static List<AwardHonoree> SelectAllTeam(List<HubSquadPlayer> pool)
	{
		var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var team = new List<AwardHonoree>();
		AddHitterSlot(team, taken, pool, "C");
		AddHitterSlot(team, taken, pool, "1B");
		AddHitterSlot(team, taken, pool, "2B");
		AddHitterSlot(team, taken, pool, "3B");
		AddHitterSlot(team, taken, pool, "SS");
		AddHitterSlot(team, taken, pool, "OF");
		AddHitterSlot(team, taken, pool, "OF");
		AddHitterSlot(team, taken, pool, "OF");
		AddHitterSlot(team, taken, pool, "DH");
		AddPitcherSlot(team, taken, pool, starter: true);
		AddPitcherSlot(team, taken, pool, starter: false);
		return team;
	}

	private static void AddHitterSlot(
		List<AwardHonoree> team,
		HashSet<string> taken,
		List<HubSquadPlayer> pool,
		string slot)
	{
		HubSquadPlayer? pick = BestUnusedHitter(pool, taken, slot);
		if (pick == null)
		{
			return;
		}

		taken.Add(pick.Name);
		team.Add(ToHonoree(pick, HitterHeadline(pick), HitterDetail(pick), 0, slot));
	}

	private static void AddPitcherSlot(
		List<AwardHonoree> team,
		HashSet<string> taken,
		List<HubSquadPlayer> pool,
		bool starter)
	{
		HubSquadPlayer? pick = BestUnusedPitcher(pool, taken, starter);
		if (pick == null)
		{
			return;
		}

		taken.Add(pick.Name);
		string slot = starter ? "SP" : "RP";
		team.Add(ToHonoree(pick, PitcherHeadline(pick), PitcherDetail(pick), 0, slot));
	}

	private static HubSquadPlayer? BestUnusedHitter(
		List<HubSquadPlayer> pool,
		HashSet<string> taken,
		string slot)
	{
		HubSquadPlayer? best = null;
		float bestScore = float.MinValue;
		foreach (HubSquadPlayer player in pool)
		{
			if (taken.Contains(player.Name) || player.CurrentHitting == null)
			{
				continue;
			}

			if (slot != "DH" && !FitsHitterSlot(player.Position, slot))
			{
				continue;
			}

			float score = HitterScore(player.CurrentHitting);
			if (score > bestScore)
			{
				bestScore = score;
				best = player;
			}
		}

		return best;
	}

	private static HubSquadPlayer? BestUnusedPitcher(
		List<HubSquadPlayer> pool,
		HashSet<string> taken,
		bool starter)
	{
		HubSquadPlayer? best = null;
		float bestScore = float.MinValue;
		foreach (HubSquadPlayer player in pool)
		{
			if (taken.Contains(player.Name) || player.CurrentPitching == null)
			{
				continue;
			}

			HubSeasonPitching line = player.CurrentPitching;
			if (starter && line.GamesStarted < 3)
			{
				continue;
			}

			if (!starter && line.Saves <= 0 && line.GamesStarted >= 3)
			{
				continue;
			}

			float score = PitcherScore(line);
			if (!starter)
			{
				score += line.Saves * 6f;
			}

			if (score > bestScore)
			{
				bestScore = score;
				best = player;
			}
		}

		if (best != null)
		{
			return best;
		}

		foreach (HubSquadPlayer player in pool)
		{
			if (taken.Contains(player.Name) || player.CurrentPitching == null)
			{
				continue;
			}

			float score = PitcherScore(player.CurrentPitching);
			if (score > bestScore)
			{
				bestScore = score;
				best = player;
			}
		}

		return best;
	}

	private static bool FitsHitterSlot(string position, string slot)
	{
		string primary = PrimaryPosition(position);
		return slot switch
		{
			"C" => primary == "C",
			"1B" => primary == "1B",
			"2B" => primary is "2B" or "INF",
			"3B" => primary == "3B",
			"SS" => primary == "SS",
			"OF" => primary is "OF" or "LF" or "CF" or "RF",
			_ => false,
		};
	}

	private static string PrimaryPosition(string position)
	{
		int slash = position.IndexOf('/');
		return slash > 0 ? position[..slash] : position;
	}

	private static float HitterScore(HubSeasonHitting line) =>
		line.Ops * 1000f + line.HomeRuns * 8f + line.Rbi * 2f + line.StolenBases * 2f + line.Average * 80f;

	private static float PitcherScore(HubSeasonPitching line) =>
		(6f - line.Era) * 36f + line.Strikeouts * 1.15f + line.Innings * 0.7f - line.Whip * 14f + line.Wins * 6f;

	private static float FreshmanScore(HubSquadPlayer player)
	{
		float hit = player.CurrentHitting == null ? float.MinValue : HitterScore(player.CurrentHitting);
		float pit = player.CurrentPitching == null ? float.MinValue : PitcherScore(player.CurrentPitching);
		return Math.Max(hit, pit);
	}

	private static AwardHonoree ToHonoree(
		HubSquadPlayer player,
		string headline,
		string detail,
		int gap,
		string? slot = null) =>
		new(
			player.Name,
			slot ?? PrimaryPosition(player.Position),
			player.Year,
			player.TeamId,
			player.TeamShort,
			headline,
			detail,
			gap);

	private static AwardHonoree CoachHonoree(
		GameSession session,
		HubTeam team,
		TeamLevel level,
		int gap,
		string districtName,
		AwardScope scope)
	{
		HubDistrict district = DistrictHubData.GetDistrict(team.DistrictId);
		return new AwardHonoree(
			CoachNameFor(session, team.Id),
			"HC",
			"STAFF",
			team.Id,
			team.ShortName,
			$"{team.Record(level)}  ·  {Ordinal(gap + 1)}",
			scope == AwardScope.State ? $"{district.Name} District" : districtName,
			gap);
	}

	private static string CoachNameFor(GameSession session, string teamId)
	{
		if (teamId == DistrictHubData.UserTeamId)
		{
			return session.CoachName;
		}

		return KnownCoaches.TryGetValue(teamId, out string? name)
			? name
			: $"{DistrictHubData.GetTeam(teamId).ShortName} Bench";
	}

	private static string HitterHeadline(HubSquadPlayer player) =>
		$"{FormatRate(player.CurrentHitting!.Average)} AVG";

	private static string HitterDetail(HubSquadPlayer player)
	{
		HubSeasonHitting line = player.CurrentHitting!;
		return string.Create(
			CultureInfo.InvariantCulture,
			$"{line.HomeRuns} HR · {line.Rbi} RBI · {line.StolenBases} SB · {FormatRate(line.Ops)} OPS");
	}

	private static string PitcherHeadline(HubSquadPlayer player) =>
		string.Create(CultureInfo.InvariantCulture, $"{player.CurrentPitching!.Era:0.00} ERA");

	private static string PitcherDetail(HubSquadPlayer player)
	{
		HubSeasonPitching line = player.CurrentPitching!;
		return string.Create(
			CultureInfo.InvariantCulture,
			$"{line.Wins}-{line.Losses} · {line.Innings:0.0} IP · {line.Strikeouts} K");
	}

	private static string FreshmanHeadline(HubSquadPlayer player)
	{
		if (player.CurrentPitching != null && (player.CurrentHitting == null || IsPitcher(player.Position)))
		{
			return PitcherHeadline(player);
		}

		return player.CurrentHitting != null ? HitterHeadline(player) : $"{player.Overall} OVR";
	}

	private static string FreshmanDetail(HubSquadPlayer player)
	{
		if (player.CurrentPitching != null && (player.CurrentHitting == null || IsPitcher(player.Position)))
		{
			return PitcherDetail(player);
		}

		return player.CurrentHitting != null ? HitterDetail(player) : "First-year varsity.";
	}

	private static bool IsPitcher(string position)
	{
		string primary = PrimaryPosition(position);
		return primary is "P" or "RHP" or "LHP" or "SP" or "RP";
	}

	private static bool NamesMatch(string left, string right) =>
		string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

	private static string FormatRate(float value)
	{
		string raw = value.ToString("0.000", CultureInfo.InvariantCulture);
		return raw.StartsWith('0') ? raw[1..] : raw;
	}

	private static string Ordinal(int rank) => rank switch
	{
		1 => "1st",
		2 => "2nd",
		3 => "3rd",
		_ => $"{rank}th",
	};
}

public sealed record AwardsCeremony(
	AwardScope Scope,
	int SeasonYear,
	string Headline,
	string ScopeLabel,
	bool Official,
	string ContinueLabel,
	IReadOnlyList<AwardHonor> Honors);

public sealed record AwardHonor(
	string Id,
	string Name,
	string Blurb,
	bool IsCoachAward,
	bool IsTeamAward,
	AwardHonoree? Winner,
	IReadOnlyList<AwardHonoree> Field);

public sealed record AwardHonoree(
	string Name,
	string Position,
	string Year,
	string TeamId,
	string TeamShort,
	string HeadlineStat,
	string Detail,
	int Gap);
