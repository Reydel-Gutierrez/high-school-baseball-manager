using HSBM.Domain;

namespace HSBM.Simulation;

public sealed class RecruitingDayResult
{
	public List<PlayerAgreement> Signed { get; } = [];
	public List<RecruitingOffer> NewOffers { get; } = [];
	public List<RecruitingOffer> Withdrawn { get; } = [];
	public List<RecruitingInterest> NewInterest { get; } = [];
}

/// <summary>
/// Shared recruiting process. The user and every AI school place offers
/// through the same methods and players decide with the same evaluator.
/// </summary>
public sealed class RecruitingMarket
{
	private readonly List<OrganizationState> _schools;
	private readonly Dictionary<string, List<Player>> _returning;
	private readonly Dictionary<Guid, Player> _prospects;
	private readonly Dictionary<string, TeamIdentity> _varsity;
	private readonly Dictionary<string, TeamIdentity> _jv;
	private readonly Dictionary<string, int> _incomingTarget;
	private readonly List<RecruitingOffer> _offers;
	private readonly List<PlayerAgreement> _agreements;
	private readonly List<RecruitingInterest> _interest;
	private readonly HashSet<Guid> _committed = [];

	public RecruitingMarket(
		IReadOnlyList<OrganizationState> schools,
		IReadOnlyList<Player> returningPlayers,
		IReadOnlyList<Player> prospects,
		IReadOnlyList<TeamIdentity> teams,
		IReadOnlyList<RecruitingOffer>? offers = null,
		IReadOnlyList<PlayerAgreement>? agreements = null,
		IReadOnlyList<RecruitingInterest>? interest = null)
	{
		_schools = schools.Where(s => !LeagueCatalog.IsUnsignedOrganization(s.Id)).ToList();
		_returning = [];
		foreach (OrganizationState school in _schools)
		{
			_returning[school.Id] = [];
		}

		foreach (Player player in returningPlayers)
		{
			if (player.IsUnsigned || !_returning.ContainsKey(player.OrganizationId))
			{
				continue;
			}

			_returning[player.OrganizationId].Add(player);
		}

		_prospects = prospects.ToDictionary(p => p.Id);
		_varsity = [];
		_jv = [];
		foreach (TeamIdentity team in teams)
		{
			if (LeagueCatalog.IsUnsignedOrganization(team.OrganizationId) || team.RosterCap <= 0)
			{
				continue;
			}

			if (team.Level == TeamLevel.Varsity)
			{
				_varsity[team.OrganizationId] = team;
			}
			else
			{
				_jv[team.OrganizationId] = team;
			}
		}

		_offers = offers?.ToList() ?? [];
		_agreements = agreements?.ToList() ?? [];
		_interest = interest?.ToList() ?? [];
		foreach (PlayerAgreement agreement in _agreements)
		{
			_committed.Add(agreement.PlayerId);
		}

		_incomingTarget = [];
		foreach (OrganizationState school in _schools)
		{
			int returning = _returning[school.Id].Count;
			_incomingTarget[school.Id] = RosterNeed.IncomingTarget(returning);
		}
	}

	public IReadOnlyList<RecruitingOffer> Offers => _offers;

	public IReadOnlyList<PlayerAgreement> Agreements => _agreements;

	public IReadOnlyList<RecruitingInterest> Interest => _interest;

	public IReadOnlyList<Player> Prospects => _prospects.Values.ToList();

	public IReadOnlyList<Player> UnsignedProspects =>
		_prospects.Values.Where(p => p.IsUnsigned && !_committed.Contains(p.Id)).ToList();

	public int SignedCount(string organizationId)
	{
		int count = 0;
		foreach (PlayerAgreement agreement in _agreements)
		{
			if (agreement.OrganizationId == organizationId && agreement.YearsRemaining > 0)
			{
				count++;
			}
		}

		return count;
	}

	public int PendingCount(string organizationId)
	{
		int count = 0;
		foreach (RecruitingOffer offer in _offers)
		{
			if (offer.IsActive && offer.OrganizationId == organizationId)
			{
				count++;
			}
		}

		return count;
	}

	public int RemainingNeed(string organizationId) =>
		Math.Max(0, IncomingTarget(organizationId) - SignedCount(organizationId) - PendingCount(organizationId));

	public IReadOnlyList<RecruitingOffer> PendingForPlayer(Guid playerId)
	{
		var list = new List<RecruitingOffer>();
		foreach (RecruitingOffer offer in _offers)
		{
			if (offer.PlayerId == playerId && offer.IsActive)
			{
				list.Add(offer);
			}
		}

		return list;
	}

	public RecruitingOffer? PendingOffer(string organizationId, Guid playerId)
	{
		foreach (RecruitingOffer offer in _offers)
		{
			if (offer.IsActive && offer.OrganizationId == organizationId && offer.PlayerId == playerId)
			{
				return offer;
			}
		}

		return null;
	}

	public PlayerAgreement? AgreementFor(Guid playerId)
	{
		foreach (PlayerAgreement agreement in _agreements)
		{
			if (agreement.PlayerId == playerId)
			{
				return agreement;
			}
		}

		return null;
	}

	public List<Player> ReturningFor(string organizationId) =>
		_returning.TryGetValue(organizationId, out List<Player>? roster) ? roster : [];

	public List<Player> EvaluationRoster(string organizationId) => CombinedRoster(organizationId);

	public int IncomingTarget(string organizationId) =>
		_incomingTarget.TryGetValue(organizationId, out int target) ? target : 0;

	public IReadOnlyList<RecruitingInterest> InterestFor(Guid playerId)
	{
		var list = new List<RecruitingInterest>();
		foreach (RecruitingInterest item in _interest)
		{
			if (item.PlayerId == playerId)
			{
				list.Add(item);
			}
		}

		return list;
	}

	public RecruitingInterest? InterestFrom(string organizationId, Guid playerId)
	{
		foreach (RecruitingInterest item in _interest)
		{
			if (item.PlayerId == playerId && item.OrganizationId == organizationId)
			{
				return item;
			}
		}

		return null;
	}

	public RecruitingInterest MarkInterest(
		string organizationId,
		Guid playerId,
		DateOnly today,
		int seasonYear,
		int heat = 1)
	{
		RecruitingInterest? existing = InterestFrom(organizationId, playerId);
		if (existing != null)
		{
			existing.Heat = Math.Clamp(existing.Heat + Math.Max(1, heat), 1, 5);
			existing.NotedOn = today;
			return existing;
		}

		var added = new RecruitingInterest
		{
			PlayerId = playerId,
			OrganizationId = organizationId,
			SeasonYear = seasonYear,
			NotedOn = today,
			Heat = Math.Clamp(heat, 1, 5),
		};
		_interest.Add(added);
		return added;
	}

	public bool TryPlaceOffer(
		string organizationId,
		Guid playerId,
		int annualSponsorship,
		int termYears,
		DateOnly today,
		int seasonYear,
		out string error)
	{
		error = "";
		if (!_prospects.TryGetValue(playerId, out Player? player) || _committed.Contains(playerId))
		{
			error = "That player is no longer available.";
			return false;
		}

		OrganizationState? school = School(organizationId);
		if (school == null)
		{
			error = "Unknown school.";
			return false;
		}

		if (today < SeasonCalendar.SigningWeek(seasonYear).Start)
		{
			error = "Offers open when Signing Week begins.";
			return false;
		}

		if (today > SeasonCalendar.SigningDeadline(seasonYear))
		{
			error = "Signing has closed.";
			return false;
		}

		int annual = ProgramBudget.SnapOffer(annualSponsorship);
		int term = Math.Clamp(termYears, 1, player.RemainingEligibility);
		if (term <= 0)
		{
			term = 1;
		}

		RecruitingOffer? existing = PendingOffer(organizationId, playerId);
		int available = ProgramBudget.RecruitingAvailable(
			school.Profile,
			_agreements,
			organizationId,
			_offers,
			existing?.Id);
		if (annual > available)
		{
			error = "Not enough program budget remaining.";
			return false;
		}

		int signed = SignedCount(organizationId);
		int returning = ReturningFor(organizationId).Count;
		if (existing == null
			&& returning + signed >= LeagueCatalog.TemporaryRosterCap)
		{
			error = "The program is at roster capacity.";
			return false;
		}

		if (existing != null)
		{
			existing.AnnualSponsorship = annual;
			existing.TermYears = term;
			existing.OfferedOn = today;
			MarkInterest(organizationId, playerId, today, seasonYear, 1);
			return true;
		}

		_offers.Add(new RecruitingOffer
		{
			Id = Guid.NewGuid(),
			PlayerId = playerId,
			OrganizationId = organizationId,
			SeasonYear = seasonYear,
			AnnualSponsorship = annual,
			TermYears = term,
			OfferedOn = today,
			Status = RecruitingOfferStatus.Pending,
		});
		MarkInterest(organizationId, playerId, today, seasonYear, 2);
		return true;
	}

	public bool TryWithdraw(string organizationId, Guid playerId)
	{
		RecruitingOffer? offer = PendingOffer(organizationId, playerId);
		if (offer == null)
		{
			return false;
		}

		offer.Status = RecruitingOfferStatus.Withdrawn;
		return true;
	}

	public RecruitingDayResult AdvanceDay(
		DateOnly today,
		int seasonYear,
		string? userOrganizationId,
		ISeededRandom rng)
	{
		var result = new RecruitingDayResult();
		SeasonEvent openPlay = SeasonCalendar.OpenPlay(seasonYear);
		bool scouting = openPlay.OccursOn(today) || today == openPlay.End;
		bool signing = SeasonCalendar.SigningWeek(seasonYear).OccursOn(today)
			|| today == SeasonCalendar.SigningDeadline(seasonYear);
		if (!scouting && !signing)
		{
			return result;
		}

		bool deadline = today >= SeasonCalendar.SigningDeadline(seasonYear);
		foreach (OrganizationState school in _schools)
		{
			if (school.Id == userOrganizationId)
			{
				continue;
			}

			ISeededRandom schoolRng = rng.Derive(StableSalt(school.Id));
			if (scouting)
			{
				result.NewInterest.AddRange(
					AiRecruitingDirector.MarkInterest(this, school, today, seasonYear, schoolRng));
			}

			if (signing)
			{
				result.NewOffers.AddRange(
					AiProgramController.PlaceOffers(this, school, today, seasonYear, schoolRng));
			}
		}

		if (!signing)
		{
			return result;
		}

		foreach (Player player in UnsignedProspects)
		{
			List<RecruitingOffer> pending = PendingForPlayer(player.Id).ToList();
			if (pending.Count == 0)
			{
				continue;
			}

			int oldest = pending.Max(o => today.DayNumber - o.OfferedOn.DayNumber);
			if (!RecruitingEvaluator.ReadyToDecide(pending.Count, oldest, deadline))
			{
				continue;
			}

			var scored = new List<ScoredOffer>(pending.Count);
			foreach (RecruitingOffer offer in pending)
			{
				OrganizationState? school = School(offer.OrganizationId);
				if (school == null)
				{
					continue;
				}

				double score = RecruitingEvaluator.ScoreOffer(
					player,
					school,
					CombinedRoster(school.Id),
					offer);
				scored.Add(new ScoredOffer(offer, score));
			}

			RecruitingOffer? choice = RecruitingEvaluator.Choose(player, scored, rng.Derive(player.Id.GetHashCode()));
			if (choice == null)
			{
				continue;
			}

			PlayerAgreement? agreement = Commit(choice, player);
			if (agreement != null)
			{
				result.Signed.Add(agreement);
			}
		}

		if (deadline)
		{
			foreach (RecruitingOffer offer in _offers)
			{
				if (offer.IsActive)
				{
					offer.Status = RecruitingOfferStatus.Expired;
				}
			}
		}

		return result;
	}

	public List<Player> ReleaseUnsignedToPool()
	{
		var leftover = new List<Player>();
		foreach (Player player in _prospects.Values)
		{
			if (player.IsUnsigned && !_committed.Contains(player.Id))
			{
				leftover.Add(player);
			}
		}

		return leftover;
	}

	private PlayerAgreement? Commit(RecruitingOffer accepted, Player player)
	{
		if (_committed.Contains(player.Id) || !accepted.IsActive)
		{
			return null;
		}

		OrganizationState? school = School(accepted.OrganizationId);
		if (school == null || !_varsity.TryGetValue(school.Id, out TeamIdentity? varsity))
		{
			return null;
		}

		_jv.TryGetValue(school.Id, out TeamIdentity? jv);
		List<Player> roster = CombinedRoster(school.Id);
		TeamLevel? level = RosterNeed.FitIncoming(roster, player);
		if (level == null)
		{
			return null;
		}

		TeamIdentity team = level == TeamLevel.Varsity || jv == null ? varsity : jv;

		player.OrganizationId = school.Id;
		player.TeamId = team.Id;
		player.TeamLevel = team.Level;
		player.JerseyNumber = NextJersey(roster);

		accepted.Status = RecruitingOfferStatus.Accepted;
		foreach (RecruitingOffer offer in _offers)
		{
			if (offer.PlayerId == player.Id && offer.Id != accepted.Id && offer.IsActive)
			{
				offer.Status = RecruitingOfferStatus.Declined;
			}
		}

		PlayerAgreement agreement = PlayerAgreement.FromOffer(accepted);
		_agreements.Add(agreement);
		_committed.Add(player.Id);
		return agreement;
	}

	private List<Player> CombinedRoster(string organizationId)
	{
		var list = new List<Player>(ReturningFor(organizationId));
		foreach (PlayerAgreement agreement in _agreements)
		{
			if (agreement.OrganizationId != organizationId)
			{
				continue;
			}

			if (_prospects.TryGetValue(agreement.PlayerId, out Player? player)
				&& list.TrueForAll(p => p.Id != player.Id))
			{
				list.Add(player);
			}
		}

		return list;
	}

	private OrganizationState? School(string organizationId)
	{
		foreach (OrganizationState school in _schools)
		{
			if (school.Id == organizationId)
			{
				return school;
			}
		}

		return null;
	}

	private static int NextJersey(IReadOnlyList<Player> roster)
	{
		var used = new HashSet<int>();
		foreach (Player player in roster)
		{
			used.Add(player.JerseyNumber);
		}

		for (int n = 1; n <= 99; n++)
		{
			if (used.Add(n))
			{
				return n;
			}
		}

		return 99;
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
