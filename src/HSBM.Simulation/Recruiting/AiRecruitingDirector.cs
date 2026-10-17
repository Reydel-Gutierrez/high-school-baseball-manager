using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// AI schools scout, budget, and offer through <see cref="RecruitingMarket"/>.
/// </summary>
public static class AiRecruitingDirector
{
	public static List<RecruitingInterest> MarkInterest(
		RecruitingMarket market,
		OrganizationState school,
		DateOnly today,
		int seasonYear,
		ISeededRandom rng)
	{
		var added = new List<RecruitingInterest>();
		int remainingNeed = market.RemainingNeed(school.Id);
		if (remainingNeed <= 0)
		{
			return added;
		}

		int watching = 0;
		foreach (RecruitingInterest item in market.Interest)
		{
			if (item.OrganizationId == school.Id)
			{
				watching++;
			}
		}

		int cap = Math.Max(remainingNeed * 3, 12);
		if (watching >= cap)
		{
			return added;
		}

		int looks = DistrictOpenPlay.LooksOn(today, SeasonCalendar.OpenPlay(seasonYear));
		looks = Math.Max(1, looks);
		List<Player> returning = market.EvaluationRoster(school.Id);
		List<RankedTarget> ranked = RankTargets(market, school, returning, rng, looks);
		int board = Math.Min(ranked.Count, remainingNeed + 2 + rng.Next(0, 3));
		board = Math.Min(board, cap - watching);
		int marked = 0;
		foreach (RankedTarget target in ranked)
		{
			if (marked >= board)
			{
				break;
			}

			if (market.InterestFrom(school.Id, target.Player.Id) != null
				&& rng.NextDouble() > 0.35)
			{
				continue;
			}

			RecruitingInterest interest = market.MarkInterest(
				school.Id,
				target.Player.Id,
				today,
				seasonYear,
				target.Scouted >= 66 ? 2 : 1);
			added.Add(interest);
			marked++;
		}

		return added;
	}

	public static List<RecruitingOffer> PlaceOffers(
		RecruitingMarket market,
		OrganizationState school,
		DateOnly today,
		int seasonYear,
		ISeededRandom rng)
	{
		var placed = new List<RecruitingOffer>();
		int remainingNeed = market.RemainingNeed(school.Id);
		if (remainingNeed <= 0)
		{
			return placed;
		}

		int budget = ProgramBudget.RecruitingAvailable(
			school.Profile,
			market.Agreements,
			school.Id,
			market.Offers);
		List<Player> returning = market.EvaluationRoster(school.Id);
		int looks = Math.Max(1, DistrictOpenPlay.LooksOn(today, SeasonCalendar.OpenPlay(seasonYear)));
		List<RankedTarget> ranked = RankTargets(market, school, returning, rng, looks);
		int openSlots = Math.Min(remainingNeed, 4 + rng.Next(0, 3));
		int offersOut = 0;
		foreach (RankedTarget target in ranked)
		{
			if (offersOut >= openSlots || budget < 0)
			{
				break;
			}

			if (market.PendingOffer(school.Id, target.Player.Id) != null)
			{
				continue;
			}

			int competing = 0;
			int bestMoney = 0;
			foreach (RecruitingOffer pending in market.PendingForPlayer(target.Player.Id))
			{
				competing++;
				bestMoney = Math.Max(bestMoney, pending.AnnualSponsorship);
			}

			int annual = SuggestOffer(target.Player, school, budget, remainingNeed - offersOut, bestMoney, rng);
			if (annual > budget)
			{
				annual = ProgramBudget.SnapOffer(budget);
			}

			if (competing >= 3 && target.Scouted < 62)
			{
				continue;
			}

			if (!market.TryPlaceOffer(
				school.Id,
				target.Player.Id,
				annual,
				target.Player.RemainingEligibility,
				today,
				seasonYear,
				out _))
			{
				continue;
			}

			RecruitingOffer? offer = market.PendingOffer(school.Id, target.Player.Id);
			if (offer != null)
			{
				placed.Add(offer);
				budget -= offer.AnnualSponsorship;
				offersOut++;
			}
		}

		return placed;
	}

	private static List<RankedTarget> RankTargets(
		RecruitingMarket market,
		OrganizationState school,
		List<Player> returning,
		ISeededRandom rng,
		int looks)
	{
		var ranked = new List<RankedTarget>();
		foreach (Player player in market.UnsignedProspects)
		{
			int scouted = TryoutScouting.Midpoint(player.Overall, looks, player.Id.GetHashCode());
			double need = RosterNeed.PlayingTimeScore(returning, player.PrimaryPosition, scouted);
			double competition = RosterNeed.BetterOrEqualCount(returning, player.PrimaryPosition, scouted) == 0 ? 1.15 : 1.0;
			double prestigePull = ProgramReputation.IsPowerhouse(school.Profile.Reputation) && scouted >= 66 ? 1.08 : 1.0;
			double alreadyOn = market.InterestFrom(school.Id, player.Id) != null ? 1.12 : 1.0;
			double noise = 0.92 + (rng.NextDouble() * 0.16);
			double score = scouted * (0.70 + (need * 0.35)) * competition * prestigePull * alreadyOn * noise;
			ranked.Add(new RankedTarget(player, score, scouted));
		}

		ranked.Sort((a, b) => b.Score.CompareTo(a.Score));
		return ranked;
	}

	private static int SuggestOffer(
		Player player,
		OrganizationState school,
		int budget,
		int remainingNeed,
		int competingMoney,
		ISeededRandom rng)
	{
		int quality = player.Overall;
		int baseline = quality switch
		{
			>= 72 => 700,
			>= 66 => 500,
			>= 60 => 350,
			>= 54 => 200,
			_ => 0,
		};

		if (ProgramReputation.IsRebuilding(school.Profile.Reputation)
			&& school.Profile.Reputation < 40)
		{
			baseline += 100;
		}
		else if (ProgramReputation.IsPowerhouse(school.Profile.Reputation))
		{
			baseline -= 50;
		}

		if (competingMoney > 0)
		{
			baseline = Math.Max(baseline, competingMoney + rng.Next(0, 3) * 50);
		}

		baseline += rng.Next(-1, 3) * 50;
		baseline = ProgramBudget.SnapOffer(baseline);
		int affordable = remainingNeed <= 1
			? budget
			: Math.Min(budget, budget / Math.Max(1, remainingNeed - 1));
		return Math.Min(baseline, ProgramBudget.SnapOffer(Math.Max(0, affordable)));
	}

	private readonly record struct RankedTarget(Player Player, double Score, int Scouted);
}
