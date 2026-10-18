using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// How a high school roster is built: students at that school sign up,
/// the coaches watch a week of tryouts, and on Decision Day every kid is
/// told Varsity, JV, or cut. Nobody is recruited and nobody switches
/// schools to chase a roster spot. Cut players stay in school and are
/// simply off the team (the "inactive" bucket).
/// </summary>
public static class TryoutDesk
{
	private static readonly PlayerPosition[] Slots =
	[
		PlayerPosition.StartingPitcher,
		PlayerPosition.Shortstop,
		PlayerPosition.Catcher,
		PlayerPosition.CenterField,
		PlayerPosition.ReliefPitcher,
		PlayerPosition.SecondBase,
		PlayerPosition.ThirdBase,
		PlayerPosition.FirstBase,
		PlayerPosition.LeftField,
		PlayerPosition.RightField,
		PlayerPosition.StartingPitcher,
		PlayerPosition.ReliefPitcher,
		PlayerPosition.Catcher,
		PlayerPosition.Shortstop,
		PlayerPosition.CenterField,
		PlayerPosition.StartingPitcher,
	];

	/// <summary>
	/// Holding teams the foreign keys need: one tryout list per school and
	/// one "inactive" list for kids not on any team. Idempotent.
	/// </summary>
	public static void EnsureTeams(List<OrganizationState> organizations, List<TeamIdentity> teams, string fallbackDistrictId, CompetitionLevel level)
	{
		if (!organizations.Any(o => o.Id == LeagueCatalog.UnsignedOrganizationId))
		{
			organizations.Add(new OrganizationState
			{
				Identity = new OrganizationIdentity(LeagueCatalog.UnsignedOrganizationId, "Not on a Team", "None", 0),
				DistrictId = fallbackDistrictId,
				CompetitionLevel = level,
				TalentTier = ProgramTalentTier.Average,
			});
		}

		var have = new HashSet<string>(teams.Select(t => t.Id), StringComparer.Ordinal);
		if (have.Add(LeagueCatalog.InactiveTeamId))
		{
			teams.Add(new TeamIdentity(LeagueCatalog.InactiveTeamId, LeagueCatalog.UnsignedOrganizationId, TeamLevel.JuniorVarsity, 0));
		}

		foreach (OrganizationState org in organizations)
		{
			if (LeagueCatalog.IsUnsignedOrganization(org.Id))
			{
				continue;
			}

			string id = LeagueCatalog.TryoutTeamId(org.Id);
			if (have.Add(id))
			{
				teams.Add(new TeamIdentity(id, LeagueCatalog.UnsignedOrganizationId, TeamLevel.JuniorVarsity, 0));
			}
		}
	}

	/// <summary>
	/// Bring a save from before tryouts existed up to date: leftover
	/// open-play pool players are taken off the books, holding teams are
	/// created, and if this year's tryouts haven't happened yet but no one
	/// has signed up, the sign-up sheet is posted.
	/// </summary>
	public static void Migrate(CareerState state, DateOnly today)
	{
		EnsureTeams(state);
		foreach (Player player in state.Players)
		{
			if (!player.Alumni && player.IsUnsigned && !player.IsTryoutCandidate && player.TeamId != LeagueCatalog.InactiveTeamId)
			{
				player.TeamId = LeagueCatalog.InactiveTeamId;
				player.JerseyNumber = 0;
			}
		}

		int year = state.Career.CurrentSeasonYear;
		if (today >= SeasonCalendar.BaseballYearStart(year)
			&& today < SeasonCalendar.TryoutDecisionDay(year)
			&& !state.Players.Any(p => p.IsTryoutCandidate))
		{
			PostSignups(state, year, new SeededRandom(SeededRandom.Mix(state.Career.RandomSeed, year * 7919)));
		}
	}

	public static void EnsureTeams(CareerState state)
	{
		string district = state.Districts.Count > 0 ? state.Districts[0].Id : "";
		EnsureTeams(state.Organizations, state.Teams, district, state.CompetitionLevel);
	}

	/// <summary>How many kids sign up at a school. Strong programs draw more.</summary>
	public static int ClassSize(ProgramTalentTier tier, ISeededRandom rng) => tier switch
	{
		ProgramTalentTier.Powerhouse => rng.Next(14, 19),
		ProgramTalentTier.AboveAverage => rng.Next(12, 17),
		ProgramTalentTier.Average => rng.Next(10, 15),
		ProgramTalentTier.BelowAverage => rng.Next(9, 13),
		_ => rng.Next(8, 12),
	};

	public static PlayerGrade RollGrade(ISeededRandom rng)
	{
		double roll = rng.NextDouble();
		return roll switch
		{
			< 0.64 => PlayerGrade.Freshman,
			< 0.84 => PlayerGrade.Sophomore,
			< 0.95 => PlayerGrade.Junior,
			_ => PlayerGrade.Senior,
		};
	}

	/// <summary>Post this year's sign-up sheet at every school.</summary>
	public static List<Player> GenerateClasses(
		IReadOnlyList<OrganizationState> organizations,
		IEnumerable<Player> existingPlayers,
		int seasonYear,
		ISeededRandom rng)
	{
		var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (Player player in existingPlayers)
		{
			usedNames.Add(player.FullName);
		}

		var candidates = new List<Player>();
		foreach (OrganizationState org in organizations)
		{
			if (LeagueCatalog.IsUnsignedOrganization(org.Id))
			{
				continue;
			}

			ISeededRandom schoolRng = rng.Derive(StableSalt(org.Id));
			var team = new TeamIdentity(LeagueCatalog.TryoutTeamId(org.Id), LeagueCatalog.UnsignedOrganizationId, TeamLevel.JuniorVarsity, 0);
			int count = ClassSize(org.TalentTier, schoolRng);
			int offset = schoolRng.Next(0, Slots.Length);
			for (int i = 0; i < count; i++)
			{
				PlayerPosition position = Slots[(offset + i) % Slots.Length];
				candidates.Add(RosterGenerator.CreateTryoutCandidate(
					team,
					org.TalentTier,
					position,
					RollGrade(schoolRng),
					seasonYear,
					schoolRng,
					usedNames));
			}
		}

		return candidates;
	}

	/// <summary>Replace any leftover sign-ups with a fresh class for this year.</summary>
	public static void PostSignups(CareerState state, int seasonYear, ISeededRandom rng)
	{
		EnsureTeams(state);
		state.Players.RemoveAll(p => p.IsTryoutCandidate);
		state.Players.AddRange(GenerateClasses(state.Organizations, state.Players, seasonYear, rng));
	}

	public static List<Player> CandidatesFor(CareerState state, string organizationId) =>
		CandidatesFor(state.Players, organizationId);

	public static List<Player> CandidatesFor(IEnumerable<Player> players, string organizationId)
	{
		string teamId = LeagueCatalog.TryoutTeamId(organizationId);
		return players.Where(p => !p.Alumni && p.TeamId == teamId).ToList();
	}

	/// <summary>
	/// The assistant coach's suggestion for every candidate at a school,
	/// based on what the staff has seen (not on hidden ratings).
	/// </summary>
	public static Dictionary<Guid, TryoutCall> Recommend(CareerState state, string organizationId, int daysSeen) =>
		Recommend(state.Players, organizationId, daysSeen);

	public static Dictionary<Guid, TryoutCall> Recommend(IReadOnlyList<Player> players, string organizationId, int daysSeen)
	{
		List<Player> candidates = CandidatesFor(players, organizationId);
		var reads = candidates.ToDictionary(p => p.Id, p => TryoutEvaluation.Report(p, daysSeen));
		return Plan(players, organizationId, candidates, p => Value(p, reads[p.Id].OverallMid, reads[p.Id].PotentialMid));
	}

	/// <summary>
	/// Decide roster spots: returning players keep theirs; candidates fill
	/// what's open, best first. Seniors don't play JV.
	/// </summary>
	private static Dictionary<Guid, TryoutCall> Plan(
		IReadOnlyList<Player> players,
		string organizationId,
		List<Player> candidates,
		Func<Player, double> value)
	{
		List<Player> roster = players
			.Where(p => !p.Alumni && p.OrganizationId == organizationId)
			.ToList();
		int varsity = roster.Count(p => p.TeamLevel == TeamLevel.Varsity);
		int jv = roster.Count(p => p.TeamLevel == TeamLevel.JuniorVarsity);
		int varsityOpen = Math.Max(0, LeagueCatalog.TeamRosterCap - varsity);
		int jvOpen = Math.Max(0, LeagueCatalog.TeamRosterCap - jv);

		// The bar for varsity: as good as the bottom third of the returning varsity.
		List<int> varsityOveralls = roster
			.Where(p => p.TeamLevel == TeamLevel.Varsity)
			.Select(p => p.Overall)
			.OrderBy(o => o)
			.ToList();
		double varsityBar = varsityOveralls.Count >= 3
			? varsityOveralls[varsityOveralls.Count / 3]
			: 48;
		double jvBar = varsityBar - 14;

		var calls = new Dictionary<Guid, TryoutCall>();
		foreach (Player player in candidates.OrderByDescending(value))
		{
			double score = value(player);
			if (varsityOpen > 0 && (score >= varsityBar || (player.Grade == PlayerGrade.Senior && score >= jvBar + 6)))
			{
				calls[player.Id] = TryoutCall.Varsity;
				varsityOpen--;
			}
			else if (jvOpen > 0 && player.Grade != PlayerGrade.Senior && score >= jvBar)
			{
				calls[player.Id] = TryoutCall.JuniorVarsity;
				jvOpen--;
			}
			else
			{
				calls[player.Id] = TryoutCall.Cut;
			}
		}

		return calls;
	}

	private static double Value(Player player, double overall, double potential)
	{
		int yearsLeft = player.Grade.RemainingEligibility();
		return overall + Math.Max(0, potential - overall) * 0.12 * yearsLeft;
	}

	/// <summary>
	/// Decision Day. Every school posts its rosters. The user's school uses
	/// the coach's calls; anything left pending goes to the assistant's
	/// recommendation. Calls that no longer fit (a full team) fall back to
	/// the next level down, then a cut.
	/// </summary>
	public static List<PlayerTransaction> Finalize(CareerState state, DateOnly today)
	{
		EnsureTeams(state);
		var posted = new List<PlayerTransaction>();
		string userOrg = state.Career.UserOrganizationId;
		foreach (OrganizationState org in state.Organizations)
		{
			if (LeagueCatalog.IsUnsignedOrganization(org.Id))
			{
				continue;
			}

			List<Player> candidates = CandidatesFor(state, org.Id);
			if (candidates.Count == 0)
			{
				continue;
			}

			Dictionary<Guid, TryoutCall> calls = org.Id == userOrg
				? Recommend(state, org.Id, TryoutEvaluation.TryoutDays)
				: Plan(state.Players, org.Id, candidates, p => Value(p, p.Overall, p.Potential));
			if (org.Id == userOrg)
			{
				foreach (Player player in candidates)
				{
					if (player.TryoutCall != TryoutCall.Pending)
					{
						calls[player.Id] = player.TryoutCall;
					}
				}
			}

			TeamIdentity? varsity = ProgramTeam(state, org.Id, TeamLevel.Varsity);
			TeamIdentity? jv = ProgramTeam(state, org.Id, TeamLevel.JuniorVarsity);
			// Best kids are placed first so a squeeze cuts the weakest.
			foreach (Player player in candidates.OrderByDescending(p => p.Overall))
			{
				TryoutCall call = calls.TryGetValue(player.Id, out TryoutCall c) ? c : TryoutCall.Cut;
				TeamIdentity? destination = call switch
				{
					TryoutCall.Varsity => HasRoom(state, varsity) ? varsity : HasRoom(state, jv) && player.Grade != PlayerGrade.Senior ? jv : null,
					TryoutCall.JuniorVarsity => HasRoom(state, jv) ? jv : HasRoom(state, varsity) ? varsity : null,
					_ => null,
				};

				if (destination == null)
				{
					player.TryoutCall = TryoutCall.Cut;
					player.OrganizationId = LeagueCatalog.UnsignedOrganizationId;
					player.TeamId = LeagueCatalog.InactiveTeamId;
					player.TeamLevel = TeamLevel.JuniorVarsity;
					player.JerseyNumber = 0;
					posted.Add(PlayerTransaction.CutAtTryouts(player, org.ShortName, org.Id, today));
					continue;
				}

				player.TryoutCall = destination.Level == TeamLevel.Varsity ? TryoutCall.Varsity : TryoutCall.JuniorVarsity;
				List<Player> roster = state.Players.Where(p => p.OrganizationId == org.Id && !p.Alumni).ToList();
				RosterMovement.ForceAssign(player, destination, roster);
				player.MarkSeasonStart();
				posted.Add(PlayerTransaction.MadeTeam(player, org.ShortName, org.Id, today));
			}
		}

		return posted;
	}

	private static bool HasRoom(CareerState state, TeamIdentity? team)
	{
		if (team == null)
		{
			return false;
		}

		int cap = team.RosterCap > 0 ? team.RosterCap : LeagueCatalog.TeamRosterCap;
		return state.Players.Count(p => !p.Alumni && p.TeamId == team.Id) < cap;
	}

	private static TeamIdentity? ProgramTeam(CareerState state, string organizationId, TeamLevel level)
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
