using HSBM.Domain;

namespace HSBM.Simulation;

public static class RosterGenerator
{
	private static readonly PlayerPosition[] VarsitySlots =
	[
		PlayerPosition.Catcher,
		PlayerPosition.Catcher,
		PlayerPosition.FirstBase,
		PlayerPosition.SecondBase,
		PlayerPosition.ThirdBase,
		PlayerPosition.Shortstop,
		PlayerPosition.Shortstop,
		PlayerPosition.LeftField,
		PlayerPosition.CenterField,
		PlayerPosition.RightField,
		PlayerPosition.LeftField,
		PlayerPosition.StartingPitcher,
		PlayerPosition.StartingPitcher,
		PlayerPosition.StartingPitcher,
		PlayerPosition.StartingPitcher,
		PlayerPosition.ReliefPitcher,
		PlayerPosition.ReliefPitcher,
		PlayerPosition.ReliefPitcher,
	];

	private static readonly PlayerPosition[] FlexPositions =
	[
		PlayerPosition.Catcher,
		PlayerPosition.SecondBase,
		PlayerPosition.ThirdBase,
		PlayerPosition.CenterField,
		PlayerPosition.StartingPitcher,
		PlayerPosition.ReliefPitcher,
		PlayerPosition.FirstBase,
		PlayerPosition.RightField,
	];

	public static List<Player> GenerateTeam(
		TeamIdentity team,
		ProgramTalentTier talent,
		int seasonYear,
		ISeededRandom rng,
		bool returningOnly = false)
	{
		int cap = team.RosterCap > 0 ? team.RosterCap : LeagueCatalog.TeamRosterCap;
		if (returningOnly)
		{
			cap = team.Level == TeamLevel.Varsity
				? rng.Next(14, 17)
				: rng.Next(12, 16);
		}

		var positions = new List<PlayerPosition>(cap);
		positions.AddRange(VarsitySlots);
		while (positions.Count < cap)
		{
			positions.Add(FlexPositions[rng.Next(0, FlexPositions.Length)]);
		}

		if (positions.Count > cap)
		{
			positions.RemoveRange(cap, positions.Count - cap);
		}

		EnsureCoverage(positions);

		var usedJerseys = new HashSet<int>();
		var players = new List<Player>(cap);
		int modifier = talent.RatingModifier();
		if (team.Level == TeamLevel.JuniorVarsity)
		{
			modifier -= 4;
		}

		for (int i = 0; i < positions.Count; i++)
		{
			players.Add(CreatePlayer(
				team,
				positions[i],
				modifier,
				seasonYear,
				rng,
				usedJerseys,
				returningOnly));
		}

		return players;
	}

	public static Player CreateIncoming(
		TeamIdentity unsignedTeam,
		PlayerPosition position,
		int seasonYear,
		ISeededRandom rng,
		HashSet<string> usedNames)
	{
		bool pitcher = position.IsPitcher();
		PlayerGrade year = rng.NextDouble() < 0.82 ? PlayerGrade.Freshman : PlayerGrade.Sophomore;
		int overallMean = pitcher ? 54 : 56;
		int modifier = TalentDistribution.RollRating(rng, overallMean, pitcher ? 9 : 10) - 55;
		var usedJerseys = new HashSet<int>();
		Player player = CreatePlayer(
			unsignedTeam,
			position,
			modifier,
			seasonYear,
			rng,
			usedJerseys,
			returningOnly: false,
			gradeOverride: year);
		player.OrganizationId = LeagueCatalog.UnsignedOrganizationId;
		player.TeamId = unsignedTeam.Id;
		player.TeamLevel = TeamLevel.JuniorVarsity;
		player.JerseyNumber = 0;
		int upside = rng.Next(year == PlayerGrade.Freshman ? 6 : 4, year == PlayerGrade.Freshman ? 18 : 12);
		player.Potential = PlayerRatings.ClampValue(Math.Max(player.Overall + 1, player.Overall + upside));
		string full = player.FullName;
		int guard = 0;
		while (!usedNames.Add(full) && guard < 40)
		{
			player.FirstName = rng.Pick(NamePool.FirstNames);
			player.LastName = rng.Pick(NamePool.LastNames);
			full = player.FullName;
			guard++;
		}

		return player;
	}

	private static void EnsureCoverage(List<PlayerPosition> positions)
	{
		if (!positions.Contains(PlayerPosition.Catcher))
		{
			positions[0] = PlayerPosition.Catcher;
		}

		if (!positions.Contains(PlayerPosition.StartingPitcher))
		{
			positions[1] = PlayerPosition.StartingPitcher;
		}

		if (!positions.Contains(PlayerPosition.ReliefPitcher))
		{
			positions[2] = PlayerPosition.ReliefPitcher;
		}

		if (!positions.Any(p => p.IsInfielder()))
		{
			positions[3] = PlayerPosition.Shortstop;
		}

		if (!positions.Any(p => p.IsOutfielder()))
		{
			positions[4] = PlayerPosition.CenterField;
		}
	}

	private static Player CreatePlayer(
		TeamIdentity team,
		PlayerPosition position,
		int modifier,
		int seasonYear,
		ISeededRandom rng,
		HashSet<int> usedJerseys,
		bool returningOnly = false,
		PlayerGrade? gradeOverride = null)
	{
		bool pitcher = position.IsPitcher();
		PlayerGrade grade = gradeOverride ?? RollGrade(team.Level, rng, returningOnly);
		int age = grade.TypicalAge();
		if (rng.NextDouble() < 0.12)
		{
			age += rng.NextDouble() < 0.5 ? -1 : 1;
		}

		double hittingMean = pitcher ? 32 : 50 + modifier;
		double pitchingMean = pitcher ? 52 + modifier : 28;
		double hittingSpread = pitcher ? 8 : 11;
		double pitchingSpread = pitcher ? 11 : 7;

		var ratings = PlayerRatings.Clamp(
			TalentDistribution.RollRating(rng, hittingMean, hittingSpread),
			TalentDistribution.RollRating(rng, pitcher ? hittingMean - 4 : hittingMean + 1, hittingSpread),
			TalentDistribution.RollRating(rng, hittingMean, hittingSpread),
			TalentDistribution.RollRating(rng, PositionSpeedMean(position, hittingMean), hittingSpread),
			TalentDistribution.RollRating(rng, PositionFieldingMean(position, hittingMean), hittingSpread),
			TalentDistribution.RollRating(rng, PositionArmMean(position, hittingMean), hittingSpread),
			TalentDistribution.RollRating(rng, pitchingMean + (position == PlayerPosition.StartingPitcher ? 1 : 0), pitchingSpread),
			TalentDistribution.RollRating(rng, pitchingMean, pitchingSpread),
			TalentDistribution.RollRating(rng, pitchingMean, pitchingSpread),
			TalentDistribution.RollRating(rng, pitchingMean + (position == PlayerPosition.StartingPitcher ? 4 : -2), pitchingSpread));

		ThrowingHand throws = pitcher
			? (rng.NextDouble() < 0.28 ? ThrowingHand.Left : ThrowingHand.Right)
			: (rng.NextDouble() < 0.12 ? ThrowingHand.Left : ThrowingHand.Right);

		BattingSide bats;
		double batRoll = rng.NextDouble();
		if (throws == ThrowingHand.Left)
		{
			bats = batRoll < 0.75 ? BattingSide.Left : BattingSide.Right;
		}
		else if (batRoll < 0.62)
		{
			bats = BattingSide.Right;
		}
		else if (batRoll < 0.88)
		{
			bats = BattingSide.Left;
		}
		else
		{
			bats = BattingSide.Switch;
		}

		PlayerPosition? secondary = null;
		if (!pitcher && rng.NextDouble() < 0.42)
		{
			secondary = SecondaryFor(position, rng);
		}

		PlayerPhysical physical = PlayerPhysical.Roll(position, ratings, rng);
		PlayerTemperament personality = PlayerPersonality.Roll(rng);
		int overall = PlayerOverall.Calculate(position, ratings);
		RecruitingPriorities priorities = RecruitingPriorities.Roll(overall, rng, personality);

		return new Player
		{
			Id = rng.NextGuid(),
			FirstName = NamePool.FirstNames[rng.Next(0, NamePool.FirstNames.Length)],
			LastName = NamePool.LastNames[rng.Next(0, NamePool.LastNames.Length)],
			GeneratedSeasonYear = seasonYear,
			Bats = bats,
			Throws = throws,
			PrimaryPosition = position,
			SecondaryPosition = secondary,
			Grade = grade,
			Age = age,
			OrganizationId = team.OrganizationId,
			TeamId = team.Id,
			TeamLevel = team.Level,
			JerseyNumber = NextJersey(rng, usedJerseys),
			Ratings = ratings,
			Potential = PlayerRatings.ClampValue(Math.Max(overall + 1, overall + PotentialBump(grade, rng))),
			Physical = physical,
			Personality = personality,
			Appearance = PersonAppearanceGenerator.GeneratePlayer(
				rng.Next(int.MinValue, int.MaxValue),
				physical.Build),
			Priorities = priorities,
		};
	}

	private static PlayerGrade RollGrade(TeamLevel level, ISeededRandom rng, bool returningOnly)
	{
		double roll = rng.NextDouble();
		if (returningOnly)
		{
			if (level == TeamLevel.Varsity)
			{
				if (roll < 0.22) return PlayerGrade.Sophomore;
				if (roll < 0.62) return PlayerGrade.Junior;
				return PlayerGrade.Senior;
			}

			if (roll < 0.08) return PlayerGrade.Freshman;
			if (roll < 0.55) return PlayerGrade.Sophomore;
			if (roll < 0.88) return PlayerGrade.Junior;
			return PlayerGrade.Senior;
		}

		if (level == TeamLevel.Varsity)
		{
			if (roll < 0.10) return PlayerGrade.Freshman;
			if (roll < 0.30) return PlayerGrade.Sophomore;
			if (roll < 0.65) return PlayerGrade.Junior;
			return PlayerGrade.Senior;
		}

		if (roll < 0.40) return PlayerGrade.Freshman;
		if (roll < 0.75) return PlayerGrade.Sophomore;
		if (roll < 0.95) return PlayerGrade.Junior;
		return PlayerGrade.Senior;
	}

	private static int PotentialBump(PlayerGrade grade, ISeededRandom rng) => grade switch
	{
		PlayerGrade.Freshman => rng.Next(6, 16),
		PlayerGrade.Sophomore => rng.Next(4, 12),
		PlayerGrade.Junior => rng.Next(2, 8),
		_ => rng.Next(1, 5),
	};

	private static double PositionSpeedMean(PlayerPosition position, double baseMean) => position switch
	{
		PlayerPosition.CenterField or PlayerPosition.Shortstop or PlayerPosition.SecondBase => baseMean + 6,
		PlayerPosition.Catcher or PlayerPosition.FirstBase => baseMean - 6,
		_ => baseMean,
	};

	private static double PositionFieldingMean(PlayerPosition position, double baseMean) => position switch
	{
		PlayerPosition.Shortstop or PlayerPosition.Catcher or PlayerPosition.CenterField => baseMean + 5,
		PlayerPosition.FirstBase => baseMean - 4,
		_ => baseMean,
	};

	private static double PositionArmMean(PlayerPosition position, double baseMean) => position switch
	{
		PlayerPosition.Catcher or PlayerPosition.Shortstop or PlayerPosition.RightField or PlayerPosition.ThirdBase => baseMean + 6,
		PlayerPosition.SecondBase or PlayerPosition.LeftField => baseMean - 2,
		_ => baseMean,
	};

	private static PlayerPosition SecondaryFor(PlayerPosition primary, ISeededRandom rng)
	{
		PlayerPosition[] options = primary switch
		{
			PlayerPosition.Catcher => [PlayerPosition.FirstBase],
			PlayerPosition.FirstBase => [PlayerPosition.ThirdBase, PlayerPosition.LeftField],
			PlayerPosition.SecondBase => [PlayerPosition.Shortstop, PlayerPosition.ThirdBase],
			PlayerPosition.Shortstop => [PlayerPosition.SecondBase, PlayerPosition.ThirdBase],
			PlayerPosition.ThirdBase => [PlayerPosition.FirstBase, PlayerPosition.Shortstop],
			PlayerPosition.LeftField => [PlayerPosition.RightField, PlayerPosition.CenterField],
			PlayerPosition.CenterField => [PlayerPosition.LeftField, PlayerPosition.RightField],
			PlayerPosition.RightField => [PlayerPosition.LeftField, PlayerPosition.CenterField],
			_ => [PlayerPosition.LeftField],
		};
		return options[rng.Next(0, options.Length)];
	}

	private static int NextJersey(ISeededRandom rng, HashSet<int> used)
	{
		for (int attempt = 0; attempt < 200; attempt++)
		{
			int number = rng.Next(1, 100);
			if (used.Add(number))
			{
				return number;
			}
		}

		for (int n = 1; n <= 99; n++)
		{
			if (used.Add(n))
			{
				return n;
			}
		}

		return rng.Next(1, 100);
	}
}
