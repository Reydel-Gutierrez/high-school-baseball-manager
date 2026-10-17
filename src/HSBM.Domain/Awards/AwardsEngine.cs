using System.Globalization;

namespace HSBM.Domain;

public sealed class SeasonAwards
{
	public AwardScope Scope { get; set; }
	public int SeasonYear { get; set; }
	public string Headline { get; set; } = "";
	public string ScopeLabel { get; set; } = "";
	public bool Official { get; set; }
	public List<SeasonHonor> Honors { get; set; } = [];
}

public sealed class SeasonHonor
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public string Blurb { get; set; } = "";
	public bool IsCoachAward { get; set; }
	public bool IsTeamAward { get; set; }
	public SeasonHonoree? Winner { get; set; }
	public List<SeasonHonoree> Field { get; set; } = [];
}

public sealed class SeasonHonoree
{
	public Guid PlayerId { get; set; }
	public string Name { get; set; } = "";
	public string Position { get; set; } = "";
	public string Year { get; set; } = "";
	public string OrganizationId { get; set; } = "";
	public string TeamShort { get; set; } = "";
	public string HeadlineStat { get; set; } = "";
	public string Detail { get; set; } = "";
	public int Gap { get; set; }
}

/// <summary>
/// Year-end ballots from actual batting and pitching lines.
/// District honors close after the regular season; state honors include
/// the postseason.
/// </summary>
public static class AwardsEngine
{
	public const int MinAtBats = 30;
	public const double MinInnings = 18;

	public static SeasonAwards Compile(
		GameWorld world,
		AwardScope scope,
		int seasonYear,
		string userOrganizationId,
		string userCoachName,
		bool official)
	{
		string districtId = "";
		string districtName = "";
		if (!string.IsNullOrEmpty(userOrganizationId))
		{
			foreach (OrganizationState org in world.Organizations)
			{
				if (org.Id == userOrganizationId)
				{
					districtId = org.DistrictId;
					break;
				}
			}

			foreach (DistrictIdentity district in world.Districts)
			{
				if (district.Id == districtId)
				{
					districtName = district.Name;
					break;
				}
			}
		}

		List<AwardCandidate> pool = Players(world, seasonYear, scope, districtId);
		List<StandingEntry> teams = LeagueStandings.FromWorld(world, seasonYear);
		if (scope == AwardScope.District && !string.IsNullOrEmpty(districtId))
		{
			teams = teams.Where(row => row.DistrictId == districtId).ToList();
		}

		return new SeasonAwards
		{
			Scope = scope,
			SeasonYear = seasonYear,
			Headline = scope == AwardScope.State
				? $"{seasonYear} STATE AWARDS"
				: $"{seasonYear} DISTRICT AWARDS",
			ScopeLabel = scope == AwardScope.State
				? "STATE BANQUET"
				: string.IsNullOrEmpty(districtName)
					? "DISTRICT"
					: $"{districtName.ToUpperInvariant()} DISTRICT",
			Official = official,
			Honors =
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
				BuildCoachHonor(world, scope, teams, userOrganizationId, userCoachName, districtName),
			],
		};
	}

	public static SeasonAwards? Find(IReadOnlyList<SeasonAwards> stored, int seasonYear, AwardScope scope)
	{
		foreach (SeasonAwards slate in stored)
		{
			if (slate.SeasonYear == seasonYear && slate.Scope == scope)
			{
				return slate;
			}
		}

		return null;
	}

	public static void Upsert(List<SeasonAwards> stored, SeasonAwards slate)
	{
		for (int i = 0; i < stored.Count; i++)
		{
			if (stored[i].SeasonYear == slate.SeasonYear && stored[i].Scope == slate.Scope)
			{
				stored[i] = slate;
				return;
			}
		}

		stored.Add(slate);
	}

	private static List<AwardCandidate> Players(
		GameWorld world,
		int seasonYear,
		AwardScope scope,
		string districtId)
	{
		var batting = new Dictionary<Guid, BattingSeasonStats>();
		foreach (BattingSeasonStats row in world.BattingStats)
		{
			if (row.SeasonYear == seasonYear)
			{
				batting[row.PlayerId] = row;
			}
		}

		var pitching = new Dictionary<Guid, PitchingSeasonStats>();
		foreach (PitchingSeasonStats row in world.PitchingStats)
		{
			if (row.SeasonYear == seasonYear)
			{
				pitching[row.PlayerId] = row;
			}
		}

		var orgs = new Dictionary<string, OrganizationState>(StringComparer.Ordinal);
		foreach (OrganizationState org in world.Organizations)
		{
			orgs[org.Id] = org;
		}

		var list = new List<AwardCandidate>();
		foreach (Player player in world.Players)
		{
			if (player.TeamLevel != TeamLevel.Varsity || player.IsUnsigned)
			{
				continue;
			}

			if (!orgs.TryGetValue(player.OrganizationId, out OrganizationState? org)
				|| LeagueCatalog.IsUnsignedOrganization(org.Id))
			{
				continue;
			}

			if (scope == AwardScope.District
				&& !string.IsNullOrEmpty(districtId)
				&& org.DistrictId != districtId)
			{
				continue;
			}

			batting.TryGetValue(player.Id, out BattingSeasonStats? hit);
			pitching.TryGetValue(player.Id, out PitchingSeasonStats? arm);
			if ((hit == null || hit.AtBats == 0) && (arm == null || arm.OutsRecorded == 0))
			{
				continue;
			}

			list.Add(new AwardCandidate(player, org, hit, arm));
		}

		return list;
	}

	private static SeasonHonor BuildPlayerHonor(
		string id,
		string name,
		string blurb,
		List<AwardCandidate> ranked,
		Func<AwardCandidate, string> headline,
		Func<AwardCandidate, string> detail)
	{
		if (ranked.Count == 0)
		{
			return new SeasonHonor { Id = id, Name = name, Blurb = blurb };
		}

		var field = new List<SeasonHonoree>();
		for (int i = 1; i < ranked.Count && field.Count < 3; i++)
		{
			field.Add(ToHonoree(ranked[i], headline(ranked[i]), detail(ranked[i]), i));
		}

		return new SeasonHonor
		{
			Id = id,
			Name = name,
			Blurb = blurb,
			Winner = ToHonoree(ranked[0], headline(ranked[0]), detail(ranked[0]), 0),
			Field = field,
		};
	}

	private static SeasonHonor BuildAllTeamHonor(AwardScope scope, List<AwardCandidate> pool)
	{
		var taken = new HashSet<Guid>();
		var team = new List<SeasonHonoree>();
		AddHitterSlot(team, taken, pool, "C", PlayerPosition.Catcher);
		AddHitterSlot(team, taken, pool, "1B", PlayerPosition.FirstBase);
		AddHitterSlot(team, taken, pool, "2B", PlayerPosition.SecondBase);
		AddHitterSlot(team, taken, pool, "3B", PlayerPosition.ThirdBase);
		AddHitterSlot(team, taken, pool, "SS", PlayerPosition.Shortstop);
		AddHitterSlot(team, taken, pool, "OF", PlayerPosition.LeftField);
		AddHitterSlot(team, taken, pool, "OF", PlayerPosition.CenterField);
		AddHitterSlot(team, taken, pool, "OF", PlayerPosition.RightField);
		AddHitterSlot(team, taken, pool, "DH", null);
		AddPitcherSlot(team, taken, pool, starter: true);
		AddPitcherSlot(team, taken, pool, starter: false);
		return new SeasonHonor
		{
			Id = scope == AwardScope.State ? "all-state" : "all-district",
			Name = scope == AwardScope.State ? "All-State Team" : "All-District Team",
			Blurb = scope == AwardScope.State
				? "First-team honors across every district in the state."
				: "First-team honors from every school in the district.",
			IsTeamAward = true,
			Winner = team.Count == 0 ? null : team[0],
			Field = team,
		};
	}

	private static SeasonHonor BuildCoachHonor(
		GameWorld world,
		AwardScope scope,
		List<StandingEntry> teams,
		string userOrganizationId,
		string userCoachName,
		string districtName)
	{
		teams.Sort(LeagueStandings.Compare);
		if (teams.Count == 0)
		{
			return new SeasonHonor
			{
				Id = scope == AwardScope.State ? "state-coach" : "district-coach",
				Name = "Coach of the Year",
				Blurb = "The bench that turned the district race.",
				IsCoachAward = true,
			};
		}

		var field = new List<SeasonHonoree>();
		for (int i = 1; i < teams.Count && field.Count < 3; i++)
		{
			field.Add(CoachHonoree(world, teams[i], i, userOrganizationId, userCoachName, districtName, scope));
		}

		return new SeasonHonor
		{
			Id = scope == AwardScope.State ? "state-coach" : "district-coach",
			Name = "Coach of the Year",
			Blurb = "Lineups, Friday pitching, and the way a program carries itself in April.",
			IsCoachAward = true,
			Winner = CoachHonoree(world, teams[0], 0, userOrganizationId, userCoachName, districtName, scope),
			Field = field,
		};
	}

	private static List<AwardCandidate> RankHitters(List<AwardCandidate> pool)
	{
		var ranked = new List<AwardCandidate>();
		foreach (AwardCandidate player in pool)
		{
			if (player.Hitting != null && player.Hitting.AtBats >= MinAtBats)
			{
				ranked.Add(player);
			}
		}

		ranked.Sort((a, b) =>
		{
			int cmp = HitterScore(b.Hitting!).CompareTo(HitterScore(a.Hitting!));
			return cmp != 0 ? cmp : string.CompareOrdinal(a.Player.FullName, b.Player.FullName);
		});
		return ranked;
	}

	private static List<AwardCandidate> RankPitchers(List<AwardCandidate> pool)
	{
		var ranked = new List<AwardCandidate>();
		foreach (AwardCandidate player in pool)
		{
			if (player.Pitching != null && player.Pitching.InningsPitched >= MinInnings)
			{
				ranked.Add(player);
			}
		}

		ranked.Sort((a, b) =>
		{
			int cmp = PitcherScore(b.Pitching!).CompareTo(PitcherScore(a.Pitching!));
			return cmp != 0 ? cmp : string.CompareOrdinal(a.Player.FullName, b.Player.FullName);
		});
		return ranked;
	}

	private static List<AwardCandidate> RankFreshmen(List<AwardCandidate> pool)
	{
		var ranked = new List<AwardCandidate>();
		foreach (AwardCandidate player in pool)
		{
			if (player.Player.Grade != PlayerGrade.Freshman)
			{
				continue;
			}

			if (player.Hitting == null && player.Pitching == null)
			{
				continue;
			}

			ranked.Add(player);
		}

		ranked.Sort((a, b) =>
		{
			int cmp = FreshmanScore(b).CompareTo(FreshmanScore(a));
			return cmp != 0 ? cmp : string.CompareOrdinal(a.Player.FullName, b.Player.FullName);
		});
		return ranked;
	}

	private static void AddHitterSlot(
		List<SeasonHonoree> team,
		HashSet<Guid> taken,
		List<AwardCandidate> pool,
		string slot,
		PlayerPosition? position)
	{
		AwardCandidate? pick = BestUnusedHitter(pool, taken, slot, position);
		if (pick == null)
		{
			return;
		}

		taken.Add(pick.Player.Id);
		team.Add(ToHonoree(pick, HitterHeadline(pick), HitterDetail(pick), 0, slot));
	}

	private static void AddPitcherSlot(
		List<SeasonHonoree> team,
		HashSet<Guid> taken,
		List<AwardCandidate> pool,
		bool starter)
	{
		AwardCandidate? pick = BestUnusedPitcher(pool, taken, starter);
		if (pick == null)
		{
			return;
		}

		taken.Add(pick.Player.Id);
		team.Add(ToHonoree(pick, PitcherHeadline(pick), PitcherDetail(pick), 0, starter ? "SP" : "RP"));
	}

	private static AwardCandidate? BestUnusedHitter(
		List<AwardCandidate> pool,
		HashSet<Guid> taken,
		string slot,
		PlayerPosition? position)
	{
		AwardCandidate? best = null;
		double bestScore = double.MinValue;
		foreach (AwardCandidate player in pool)
		{
			if (taken.Contains(player.Player.Id) || player.Hitting == null)
			{
				continue;
			}

			if (slot != "DH" && !FitsHitterSlot(player.Player.PrimaryPosition, slot, position))
			{
				continue;
			}

			double score = HitterScore(player.Hitting);
			if (score > bestScore)
			{
				bestScore = score;
				best = player;
			}
		}

		return best;
	}

	private static AwardCandidate? BestUnusedPitcher(
		List<AwardCandidate> pool,
		HashSet<Guid> taken,
		bool starter)
	{
		AwardCandidate? best = null;
		double bestScore = double.MinValue;
		foreach (AwardCandidate player in pool)
		{
			if (taken.Contains(player.Player.Id) || player.Pitching == null)
			{
				continue;
			}

			PitchingSeasonStats line = player.Pitching;
			if (starter && line.GamesStarted < 3)
			{
				continue;
			}

			if (!starter && line.Saves <= 0 && line.GamesStarted >= 3)
			{
				continue;
			}

			double score = PitcherScore(line);
			if (!starter)
			{
				score += line.Saves * 6;
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

		foreach (AwardCandidate player in pool)
		{
			if (taken.Contains(player.Player.Id) || player.Pitching == null)
			{
				continue;
			}

			double score = PitcherScore(player.Pitching);
			if (score > bestScore)
			{
				bestScore = score;
				best = player;
			}
		}

		return best;
	}

	private static bool FitsHitterSlot(PlayerPosition position, string slot, PlayerPosition? expected)
	{
		return slot switch
		{
			"C" => position == PlayerPosition.Catcher,
			"1B" => position == PlayerPosition.FirstBase,
			"2B" => position is PlayerPosition.SecondBase,
			"3B" => position == PlayerPosition.ThirdBase,
			"SS" => position == PlayerPosition.Shortstop,
			"OF" => position.IsOutfielder() || expected == position,
			_ => false,
		};
	}

	private static double HitterScore(BattingSeasonStats line) =>
		line.Ops * 1000 + line.HomeRuns * 8 + line.RunsBattedIn * 2 + line.StolenBases * 2 + line.Average * 80;

	private static double PitcherScore(PitchingSeasonStats line) =>
		(6 - line.Era) * 36 + line.Strikeouts * 1.15 + line.InningsPitched * 0.7 - line.Whip * 14 + line.Wins * 6;

	private static double FreshmanScore(AwardCandidate player)
	{
		double hit = player.Hitting == null ? double.MinValue : HitterScore(player.Hitting);
		double pit = player.Pitching == null ? double.MinValue : PitcherScore(player.Pitching);
		return Math.Max(hit, pit);
	}

	private static SeasonHonoree ToHonoree(
		AwardCandidate player,
		string headline,
		string detail,
		int gap,
		string? slot = null) =>
		new()
		{
			PlayerId = player.Player.Id,
			Name = player.Player.FullName,
			Position = slot ?? player.Player.PrimaryPosition.ToCode(),
			Year = player.Player.Grade.ToAbbrev(),
			OrganizationId = player.Organization.Id,
			TeamShort = player.Organization.ShortName,
			HeadlineStat = headline,
			Detail = detail,
			Gap = gap,
		};

	private static SeasonHonoree CoachHonoree(
		GameWorld world,
		StandingEntry team,
		int gap,
		string userOrganizationId,
		string userCoachName,
		string districtName,
		AwardScope scope)
	{
		string district = districtName;
		foreach (DistrictIdentity item in world.Districts)
		{
			if (item.Id == team.DistrictId)
			{
				district = item.Name;
				break;
			}
		}

		return new SeasonHonoree
		{
			Name = CoachNameFor(world, team.OrganizationId, userOrganizationId, userCoachName),
			Position = "HC",
			Year = "STAFF",
			OrganizationId = team.OrganizationId,
			TeamShort = team.Name,
			HeadlineStat = $"{team.Wins}\u2013{team.Losses}  ·  {Ordinal(gap + 1)}",
			Detail = scope == AwardScope.State ? $"{district} District" : districtName,
			Gap = gap,
		};
	}

	private static string CoachNameFor(
		GameWorld world,
		string organizationId,
		string userOrganizationId,
		string userCoachName)
	{
		if (organizationId == userOrganizationId && !string.IsNullOrWhiteSpace(userCoachName))
		{
			return userCoachName;
		}

		ProgramCoach? coach = world.CoachFor(organizationId);
		if (coach != null && !string.IsNullOrWhiteSpace(coach.FullName))
		{
			return coach.FullName;
		}

		foreach (OrganizationState org in world.Organizations)
		{
			if (org.Id == organizationId)
			{
				return org.HeadCoach != null && !string.IsNullOrWhiteSpace(org.HeadCoach.FullName)
					? org.HeadCoach.FullName
					: $"{org.ShortName} Bench";
			}
		}

		return "Head Coach";
	}

	private static string HitterHeadline(AwardCandidate player) =>
		$"{FormatRate(player.Hitting!.Average)} AVG";

	private static string HitterDetail(AwardCandidate player)
	{
		BattingSeasonStats line = player.Hitting!;
		return string.Create(
			CultureInfo.InvariantCulture,
			$"{line.HomeRuns} HR · {line.RunsBattedIn} RBI · {line.StolenBases} SB · {FormatRate(line.Ops)} OPS");
	}

	private static string PitcherHeadline(AwardCandidate player) =>
		string.Create(CultureInfo.InvariantCulture, $"{player.Pitching!.Era:0.00} ERA");

	private static string PitcherDetail(AwardCandidate player)
	{
		PitchingSeasonStats line = player.Pitching!;
		return string.Create(
			CultureInfo.InvariantCulture,
			$"{line.Wins}-{line.Losses} · {line.InningsPitched:0.0} IP · {line.Strikeouts} K");
	}

	private static string FreshmanHeadline(AwardCandidate player)
	{
		if (player.Pitching != null && (player.Hitting == null || player.Player.IsPitcher))
		{
			return PitcherHeadline(player);
		}

		return player.Hitting != null ? HitterHeadline(player) : $"{player.Player.Overall} OVR";
	}

	private static string FreshmanDetail(AwardCandidate player)
	{
		if (player.Pitching != null && (player.Hitting == null || player.Player.IsPitcher))
		{
			return PitcherDetail(player);
		}

		return player.Hitting != null ? HitterDetail(player) : "First-year varsity.";
	}

	private static string FormatRate(double value)
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

	private sealed record AwardCandidate(
		Player Player,
		OrganizationState Organization,
		BattingSeasonStats? Hitting,
		PitchingSeasonStats? Pitching);
}
