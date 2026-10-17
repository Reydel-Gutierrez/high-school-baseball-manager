using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// AI roster moves after signing week: cut to cap, claim unsigned
/// leftovers, and call JV players up. User schools are left alone.
/// </summary>
public static class WorldRosterDesk
{
	public const int MaxPoolSigningsPerDay = 2;
	public const int MaxPromotionsPerDay = 4;
	public const int PromotionOverallGap = 3;
	public const int HotAverageAtBats = 12;
	public const double HotAverage = 0.330;
	public const int HotEraOuts = 15;
	public const double HotEra = 2.40;

	public static List<PlayerTransaction> AdvanceDay(
		CareerState state,
		DateOnly today,
		ISeededRandom rng)
	{
		var posted = new List<PlayerTransaction>();
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (PlayerTransaction existing in state.Transactions)
		{
			if (!string.IsNullOrWhiteSpace(existing.DedupKey))
			{
				seen.Add(existing.DedupKey);
			}
		}

		int year = state.Career.CurrentSeasonYear;
		string userOrg = state.Career.UserOrganizationId;
		List<OrganizationState> schools = AiSchools(state, userOrg);
		rng.Shuffle(schools);

		CutToCap(state, today, year, schools, seen, posted);
		BalanceTeamCaps(state, schools);

		if (today > SeasonCalendar.SigningDeadline(year)
			&& today <= SeasonCalendar.TransferDeadline(year))
		{
			SignFromPool(state, today, year, schools, rng, seen, posted);
		}

		if (today > SeasonCalendar.SigningDeadline(year)
			&& today <= SeasonCalendar.PromotionDeadline(year))
		{
			Promote(state, today, year, schools, rng, seen, posted);
		}

		if (posted.Count > 0)
		{
			state.Transactions.AddRange(posted);
		}

		return posted;
	}

	private static void CutToCap(
		CareerState state,
		DateOnly today,
		int year,
		List<OrganizationState> schools,
		HashSet<string> seen,
		List<PlayerTransaction> posted)
	{
		int cap = ProgramRosterRules.RegisteredCap(today, year);
		foreach (OrganizationState school in schools)
		{
			List<Player> roster = Roster(state, school.Id);
			if (roster.Count <= cap)
			{
				continue;
			}

			roster.Sort((a, b) =>
			{
				int overall = a.Overall.CompareTo(b.Overall);
				return overall != 0 ? overall : string.CompareOrdinal(a.LastName, b.LastName);
			});

			int extra = roster.Count - cap;
			for (int i = 0; i < extra && i < roster.Count; i++)
			{
				Player player = roster[i];
				if (Release(state, school, player, today, seen, posted) && today < SeasonCalendar.InitialRosterCut(year))
				{
					break;
				}
			}
		}
	}

	private static void BalanceTeamCaps(CareerState state, List<OrganizationState> schools)
	{
		foreach (OrganizationState school in schools)
		{
			TeamIdentity? varsityTeam = Team(state, school.Id, TeamLevel.Varsity);
			TeamIdentity? jvTeam = Team(state, school.Id, TeamLevel.JuniorVarsity);
			if (varsityTeam == null || jvTeam == null)
			{
				continue;
			}

			List<Player> roster = Roster(state, school.Id);
			var varsity = new List<Player>();
			var jv = new List<Player>();
			foreach (Player player in roster)
			{
				if (player.TeamLevel == TeamLevel.Varsity)
				{
					varsity.Add(player);
				}
				else
				{
					jv.Add(player);
				}
			}

			while (varsity.Count > LeagueCatalog.TeamRosterCap && jv.Count < LeagueCatalog.TeamRosterCap)
			{
				Player? weakest = WeakestVarsity(state, varsity, varsity[0]);
				if (weakest == null)
				{
					break;
				}

				Assign(weakest, jvTeam, roster);
				varsity.Remove(weakest);
				jv.Add(weakest);
			}

			while (jv.Count > LeagueCatalog.TeamRosterCap && varsity.Count < LeagueCatalog.TeamRosterCap)
			{
				Player? callup = BestCallup(state, jv, rosterWeek: true);
				if (callup == null)
				{
					break;
				}

				Assign(callup, varsityTeam, roster);
				jv.Remove(callup);
				varsity.Add(callup);
			}

			FillPlayable(state, varsityTeam, roster, varsity, jv);
		}
	}

	private static void FillPlayable(
		CareerState state,
		TeamIdentity varsityTeam,
		List<Player> roster,
		List<Player> varsity,
		List<Player> jv)
	{
		while (varsity.Count < WorldIntegrity.MinPlayableRoster && jv.Count > 0)
		{
			Player callup = BestCallup(state, jv, rosterWeek: true) ?? jv[0];
			Assign(callup, varsityTeam, roster);
			jv.Remove(callup);
			varsity.Add(callup);
		}
	}

	private static void SignFromPool(
		CareerState state,
		DateOnly today,
		int year,
		List<OrganizationState> schools,
		ISeededRandom rng,
		HashSet<string> seen,
		List<PlayerTransaction> posted)
	{
		int cap = ProgramRosterRules.RegisteredCap(today, year);
		int signed = 0;
		foreach (OrganizationState school in schools)
		{
			if (signed >= MaxPoolSigningsPerDay)
			{
				break;
			}

			List<Player> roster = Roster(state, school.Id);
			int holes = cap - roster.Count;
			if (holes <= 0)
			{
				continue;
			}

			bool hungry = holes >= 2 || today <= SeasonCalendar.InitialRosterCut(year);
			if (!hungry && rng.NextDouble() > 0.35)
			{
				continue;
			}

			Player? pickup = BestUnsigned(state, school, roster);
			if (pickup == null)
			{
				continue;
			}

			TeamIdentity? varsity = Team(state, school.Id, TeamLevel.Varsity);
			TeamIdentity? jv = Team(state, school.Id, TeamLevel.JuniorVarsity);
			if (varsity == null)
			{
				continue;
			}

			TeamLevel? level = RosterNeed.FitIncoming(roster, pickup);
			if (level == null)
			{
				continue;
			}

			TeamIdentity dest = level == TeamLevel.Varsity || jv == null ? varsity : jv;
			Assign(pickup, dest, roster);
			VoidAgreement(state, pickup.Id);
			if (TryAdd(seen, posted, PlayerTransaction.FromSigning(pickup, school.ShortName, school.Id, today, fromPool: true)))
			{
				signed++;
			}
		}
	}

	private static void Promote(
		CareerState state,
		DateOnly today,
		int year,
		List<OrganizationState> schools,
		ISeededRandom rng,
		HashSet<string> seen,
		List<PlayerTransaction> posted)
	{
		int promoted = 0;
		bool rosterWeek = today <= SeasonCalendar.InitialRosterCut(year);
		int dailyCap = rosterWeek ? MaxPromotionsPerDay : 2;
		foreach (OrganizationState school in schools)
		{
			if (promoted >= dailyCap)
			{
				break;
			}

			TeamIdentity? varsityTeam = Team(state, school.Id, TeamLevel.Varsity);
			TeamIdentity? jvTeam = Team(state, school.Id, TeamLevel.JuniorVarsity);
			if (varsityTeam == null || jvTeam == null)
			{
				continue;
			}

			List<Player> roster = Roster(state, school.Id);
			var varsity = new List<Player>();
			var jv = new List<Player>();
			foreach (Player player in roster)
			{
				if (player.TeamLevel == TeamLevel.Varsity)
				{
					varsity.Add(player);
				}
				else
				{
					jv.Add(player);
				}
			}

			if (jv.Count == 0)
			{
				continue;
			}

			Player? callup = null;
			Player? demote = null;
			if (varsity.Count < LeagueCatalog.TeamRosterCap)
			{
				callup = BestCallup(state, jv, rosterWeek);
			}
			else
			{
				callup = BestCallup(state, jv, rosterWeek);
				if (callup != null)
				{
					demote = WeakestVarsity(state, varsity, callup);
					if (demote == null
						|| Score(state, callup) < Score(state, demote) + PromotionOverallGap)
					{
						callup = null;
						demote = null;
					}
				}
			}

			if (callup == null)
			{
				continue;
			}

			if (varsity.Count >= LeagueCatalog.TeamRosterCap && demote == null)
			{
				continue;
			}

			if (!rosterWeek && rng.NextDouble() > 0.45 && varsity.Count >= LeagueCatalog.TeamRosterCap)
			{
				continue;
			}

			if (demote != null)
			{
				Assign(demote, jvTeam, roster);
			}

			Assign(callup, varsityTeam, roster);
			if (TryAdd(seen, posted, PlayerTransaction.Promoted(callup, school.ShortName, school.Id, today)))
			{
				promoted++;
			}
		}
	}

	private static bool Release(
		CareerState state,
		OrganizationState school,
		Player player,
		DateOnly today,
		HashSet<string> seen,
		List<PlayerTransaction> posted)
	{
		PlayerTransaction entry = PlayerTransaction.Released(player, school.ShortName, school.Id, today);
		if (!TryAdd(seen, posted, entry))
		{
			return false;
		}

		RosterMovement.ReleaseToPool(player, school.DistrictId);
		VoidAgreement(state, player.Id);
		return true;
	}

	private static Player? BestUnsigned(CareerState state, OrganizationState school, List<Player> roster)
	{
		string openPlay = LeagueCatalog.OpenPlayTeamId(school.DistrictId);
		Player? best = null;
		double bestScore = double.MinValue;
		foreach (Player player in state.Players)
		{
			if (!player.IsUnsigned || player.TeamId != openPlay)
			{
				continue;
			}

			double score = player.Overall + (RosterNeed.PlayingTimeScore(roster, player.PrimaryPosition, player.Overall) * 12);
			if (score > bestScore)
			{
				bestScore = score;
				best = player;
			}
		}

		return best;
	}

	private static Player? BestCallup(CareerState state, List<Player> jv, bool rosterWeek)
	{
		Player? best = null;
		double bestScore = double.MinValue;
		foreach (Player player in jv)
		{
			double score = Score(state, player);
			if (!rosterWeek && !IsHot(state, player) && player.Overall < 58)
			{
				continue;
			}

			if (score > bestScore)
			{
				bestScore = score;
				best = player;
			}
		}

		return best;
	}

	private static Player? WeakestVarsity(CareerState state, List<Player> varsity, Player callup)
	{
		Player? weakest = null;
		double worst = double.MaxValue;
		foreach (Player player in varsity)
		{
			if (!RosterNeed.SameRole(player.PrimaryPosition, callup.PrimaryPosition)
				&& varsity.Count >= LeagueCatalog.TeamRosterCap)
			{
				continue;
			}

			double score = Score(state, player);
			if (score < worst)
			{
				worst = score;
				weakest = player;
			}
		}

		if (weakest == null)
		{
			foreach (Player player in varsity)
			{
				double score = Score(state, player);
				if (score < worst)
				{
					worst = score;
					weakest = player;
				}
			}
		}

		return weakest;
	}

	private static double Score(CareerState state, Player player)
	{
		double overall = player.Overall;
		if (player.IsPitcher)
		{
			foreach (PitchingSeasonStats arm in state.PitchingStats)
			{
				if (arm.PlayerId != player.Id || arm.SeasonYear != state.Career.CurrentSeasonYear)
				{
					continue;
				}

				if (arm.OutsRecorded >= HotEraOuts)
				{
					overall += arm.Era <= HotEra ? 8 : arm.Era >= 6.0 ? -6 : 0;
				}

				break;
			}
		}
		else
		{
			foreach (BattingSeasonStats hit in state.BattingStats)
			{
				if (hit.PlayerId != player.Id || hit.SeasonYear != state.Career.CurrentSeasonYear)
				{
					continue;
				}

				if (hit.AtBats >= HotAverageAtBats)
				{
					overall += hit.Average >= HotAverage ? 8 : hit.Average <= 0.180 ? -6 : 0;
				}

				break;
			}
		}

		return overall;
	}

	private static bool IsHot(CareerState state, Player player)
	{
		if (player.IsPitcher)
		{
			foreach (PitchingSeasonStats arm in state.PitchingStats)
			{
				if (arm.PlayerId == player.Id
					&& arm.SeasonYear == state.Career.CurrentSeasonYear
					&& arm.OutsRecorded >= HotEraOuts
					&& arm.Era <= HotEra)
				{
					return true;
				}
			}

			return false;
		}

		foreach (BattingSeasonStats hit in state.BattingStats)
		{
			if (hit.PlayerId == player.Id
				&& hit.SeasonYear == state.Career.CurrentSeasonYear
				&& hit.AtBats >= HotAverageAtBats
				&& hit.Average >= HotAverage)
			{
				return true;
			}
		}

		return false;
	}

	private static void Assign(Player player, TeamIdentity team, List<Player> roster)
	{
		RosterMovement.ForceAssign(player, team, roster);
	}

	private static void VoidAgreement(CareerState state, Guid playerId)
	{
		foreach (PlayerAgreement agreement in state.Agreements)
		{
			if (agreement.PlayerId == playerId)
			{
				agreement.YearsRemaining = 0;
			}
		}
	}

	private static List<Player> Roster(CareerState state, string organizationId)
	{
		var list = new List<Player>();
		foreach (Player player in state.Players)
		{
			if (!player.Alumni && !player.IsUnsigned && player.OrganizationId == organizationId)
			{
				list.Add(player);
			}
		}

		return list;
	}

	private static TeamIdentity? Team(CareerState state, string organizationId, TeamLevel level)
	{
		foreach (TeamIdentity team in state.Teams)
		{
			if (team.OrganizationId == organizationId && team.Level == level && team.RosterCap > 0)
			{
				return team;
			}
		}

		return null;
	}

	private static List<OrganizationState> AiSchools(CareerState state, string userOrg)
	{
		var list = new List<OrganizationState>();
		foreach (OrganizationState school in state.Organizations)
		{
			if (LeagueCatalog.IsUnsignedOrganization(school.Id) || school.Id == userOrg)
			{
				continue;
			}

			list.Add(school);
		}

		return list;
	}

	private static bool TryAdd(HashSet<string> seen, List<PlayerTransaction> posted, PlayerTransaction entry)
	{
		if (string.IsNullOrWhiteSpace(entry.DedupKey) || !seen.Add(entry.DedupKey))
		{
			return false;
		}

		posted.Add(entry);
		return true;
	}
}
