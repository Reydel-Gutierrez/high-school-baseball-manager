using HSBM.Domain;

namespace HSBM.Simulation;

public static class TryoutCampGenerator
{
	private static readonly PlayerPosition[] Slots =
	[
		PlayerPosition.Shortstop,
		PlayerPosition.StartingPitcher,
		PlayerPosition.CenterField,
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
		PlayerPosition.RightField,
		PlayerPosition.StartingPitcher,
		PlayerPosition.StartingPitcher,
		PlayerPosition.ReliefPitcher,
		PlayerPosition.ReliefPitcher,
		PlayerPosition.ReliefPitcher,
		PlayerPosition.ReliefPitcher,
		PlayerPosition.SecondBase,
		PlayerPosition.ThirdBase,
		PlayerPosition.FirstBase,
		PlayerPosition.Catcher,
		PlayerPosition.CenterField,
		PlayerPosition.LeftField,
		PlayerPosition.RightField,
		PlayerPosition.StartingPitcher,
		PlayerPosition.ReliefPitcher,
		PlayerPosition.SecondBase,
		PlayerPosition.Shortstop,
	];

	public static TryoutCamp Generate(int seasonYear, ISeededRandom rng)
	{
		SeasonEvent window = SeasonCalendar.TryoutWeek(seasonYear);
		var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var prospects = new List<TryoutProspect>(TryoutCamp.InviteCount);

		prospects.Add(CreatePinned(
			rng,
			"Mason",
			"Price",
			PlayerPosition.Shortstop,
			PlayerGrade.Freshman,
			61,
			78,
			LetterGrade.AMinus,
			usedNames));
		prospects.Add(CreatePinned(
			rng,
			"Luis",
			"Moreno",
			PlayerPosition.StartingPitcher,
			PlayerGrade.Freshman,
			58,
			74,
			LetterGrade.B,
			usedNames));
		prospects.Add(CreatePinned(
			rng,
			"Daniel",
			"Brooks",
			PlayerPosition.CenterField,
			PlayerGrade.Sophomore,
			64,
			72,
			LetterGrade.BPlus,
			usedNames));

		for (int i = 3; i < Slots.Length; i++)
		{
			prospects.Add(CreateRolled(rng, Slots[i], i < 24 ? PlayerGrade.Freshman : PlayerGrade.Sophomore, usedNames));
		}

		IReadOnlyList<TryoutGame> games = TryoutGameBuilder.Build(window, prospects);
		return new TryoutCamp(seasonYear, window, prospects, games);
	}

	private static TryoutProspect CreatePinned(
		ISeededRandom rng,
		string first,
		string last,
		PlayerPosition position,
		PlayerGrade year,
		int overall,
		int potential,
		LetterGrade day1,
		HashSet<string> usedNames)
	{
		usedNames.Add($"{first} {last}");
		ThrowingHand throws = position.IsPitcher()
			? (rng.NextDouble() < 0.28 ? ThrowingHand.Left : ThrowingHand.Right)
			: (rng.NextDouble() < 0.12 ? ThrowingHand.Left : ThrowingHand.Right);

		return Build(
			rng.NextGuid(),
			first,
			last,
			position,
			throws,
			RollBats(rng, position.IsPitcher()),
			year,
			overall,
			potential,
			day1,
			TryoutScouting.RollDayGrade(overall, 1, rng),
			TryoutScouting.RollDayGrade(overall, 2, rng),
			rng);
	}

	private static TryoutProspect CreateRolled(
		ISeededRandom rng,
		PlayerPosition position,
		PlayerGrade year,
		HashSet<string> usedNames)
	{
		string first = rng.Pick(NamePool.FirstNames);
		string last = rng.Pick(NamePool.LastNames);
		string full = $"{first} {last}";
		int guard = 0;
		while (!usedNames.Add(full) && guard < 40)
		{
			first = rng.Pick(NamePool.FirstNames);
			last = rng.Pick(NamePool.LastNames);
			full = $"{first} {last}";
			guard++;
		}

		bool pitcher = position.IsPitcher();
		int overall = TalentDistribution.RollRating(rng, pitcher ? 54 : 56, pitcher ? 9 : 10);
		int upside = rng.Next(4, year == PlayerGrade.Freshman ? 17 : 12);
		int potential = PlayerRatings.ClampValue(overall + upside);
		ThrowingHand throws = pitcher
			? (rng.NextDouble() < 0.28 ? ThrowingHand.Left : ThrowingHand.Right)
			: (rng.NextDouble() < 0.12 ? ThrowingHand.Left : ThrowingHand.Right);

		return Build(
			rng.NextGuid(),
			first,
			last,
			position,
			throws,
			RollBats(rng, pitcher),
			year,
			overall,
			potential,
			TryoutScouting.RollDayGrade(overall, 0, rng),
			TryoutScouting.RollDayGrade(overall, 1, rng),
			TryoutScouting.RollDayGrade(overall, 2, rng),
			rng);
	}

	private static TryoutProspect Build(
		Guid id,
		string first,
		string last,
		PlayerPosition position,
		ThrowingHand throws,
		BattingSide bats,
		PlayerGrade year,
		int overall,
		int potential,
		LetterGrade day1,
		LetterGrade day2,
		LetterGrade day3,
		ISeededRandom rng) =>
		new()
		{
			Id = id,
			FirstName = first,
			LastName = last,
			Position = position,
			Throws = throws,
			Bats = bats,
			Year = year,
			TrueOverall = overall,
			TruePotential = potential,
			Day1Grade = day1,
			Day2Grade = day2,
			Day3Grade = day3,
			Day1Note = NoteFor(position, 0, rng),
			Day2Note = NoteFor(position, 1, rng),
			Day3Note = NoteFor(position, 2, rng),
			Day1Hitting = RollHitting(overall, day1, rng),
			Day2Hitting = RollHitting(overall, day2, rng),
			Day3Hitting = RollHitting(overall, day3, rng),
			Day1Pitching = position.IsPitcher() ? RollPitching(overall, day1, position, rng) : null,
			Day2Pitching = position.IsPitcher() ? RollPitching(overall, day2, position, rng) : null,
			Day3Pitching = position.IsPitcher() ? RollPitching(overall, day3, position, rng) : null,
		};

	private static BattingSide RollBats(ISeededRandom rng, bool pitcher)
	{
		double roll = rng.NextDouble();
		if (!pitcher && roll < 0.06)
		{
			return BattingSide.Switch;
		}

		return roll < (pitcher ? 0.18 : 0.28) ? BattingSide.Left : BattingSide.Right;
	}

	private static TryoutHittingLine RollHitting(int overall, LetterGrade grade, ISeededRandom rng)
	{
		int atBats = rng.Next(3, 6);
		double hitChance = Math.Clamp(0.16 + ((overall - 45) * 0.0045) + GradeBias(grade), 0.08, 0.48);
		int hits = 0;
		int doubles = 0;
		int triples = 0;
		int homeRuns = 0;
		for (int i = 0; i < atBats; i++)
		{
			if (rng.NextDouble() > hitChance)
			{
				continue;
			}

			hits++;
			double extra = rng.NextDouble();
			if (extra > 0.92 && overall >= 58)
			{
				homeRuns++;
			}
			else if (extra > 0.82)
			{
				doubles++;
			}
			else if (extra > 0.78 && overall >= 55)
			{
				triples++;
			}
		}

		hits = Math.Min(atBats, hits);
		int extras = doubles + triples + homeRuns;
		if (extras > hits)
		{
			doubles = 0;
			triples = 0;
			homeRuns = Math.Min(hits, homeRuns);
		}

		int walks = rng.NextDouble() < 0.18 + GradeBias(grade) ? 1 : 0;
		int strikeouts = rng.Next(0, 1 + (overall < 55 ? 2 : 1));
		strikeouts = Math.Min(atBats - hits, strikeouts);
		int runs = rng.Next(0, hits + walks > 0 ? 2 : 1);
		int rbi = rng.Next(0, hits + homeRuns + 1);
		int stolen = overall >= 60 && rng.NextDouble() < 0.22 ? 1 : 0;
		return new TryoutHittingLine
		{
			Games = 1,
			AtBats = atBats,
			Runs = runs,
			Hits = hits,
			Doubles = doubles,
			Triples = triples,
			HomeRuns = homeRuns,
			Rbi = rbi,
			Walks = walks,
			Strikeouts = strikeouts,
			StolenBases = stolen,
		};
	}

	private static TryoutPitchingLine RollPitching(
		int overall,
		LetterGrade grade,
		PlayerPosition position,
		ISeededRandom rng)
	{
		bool starter = position == PlayerPosition.StartingPitcher;
		int outs = starter ? rng.Next(6, 13) : rng.Next(3, 7);
		int strikeouts = Math.Clamp((int)Math.Round(outs * (0.22 + ((overall - 50) * 0.008) + GradeBias(grade))), 0, outs);
		int walks = rng.Next(0, overall >= 60 ? 2 : 3);
		int hits = rng.Next(starter ? 1 : 0, starter ? 5 : 3);
		int earned = Math.Min(hits, rng.Next(0, 1 + (grade <= LetterGrade.CPlus ? 2 : 1)));
		bool win = starter && earned <= 1 && rng.NextDouble() < 0.35;
		bool loss = starter && !win && earned >= 2 && rng.NextDouble() < 0.4;
		bool save = !starter && earned == 0 && rng.NextDouble() < 0.2;
		return new TryoutPitchingLine
		{
			Games = 1,
			GamesStarted = starter ? 1 : 0,
			Wins = win ? 1 : 0,
			Losses = loss ? 1 : 0,
			Saves = save ? 1 : 0,
			Outs = outs,
			Hits = hits,
			Runs = earned + (rng.NextDouble() < 0.2 && earned > 0 ? 1 : 0),
			EarnedRuns = earned,
			Walks = walks,
			Strikeouts = strikeouts,
			HomeRuns = earned >= 2 && rng.NextDouble() < 0.25 ? 1 : 0,
		};
	}

	private static double GradeBias(LetterGrade grade) => grade switch
	{
		>= LetterGrade.AMinus => 0.08,
		>= LetterGrade.BMinus => 0.02,
		>= LetterGrade.C => -0.03,
		_ => -0.07,
	};

	private static string NoteFor(PlayerPosition position, int day, ISeededRandom rng)
	{
		string[] pool = (position.IsPitcher(), day) switch
		{
			(true, 0) =>
			[
				"Live arm in the first scrimmage. Command came and went.",
				"Fastball played. Secondary pitches were still hunting a lane.",
				"Competed. The shape is there; the count-leverage is not yet.",
			],
			(true, 1) =>
			[
				"Better against the second time through. Still leaking arm-side.",
				"Changeup showed. Breaking ball is the remaining question.",
				"Held velocity. The staff is split on the future projection.",
			],
			(true, 2) =>
			[
				"Third look did not solve the ceiling. Present is clearer than future.",
				"Filled the zone late. Upside is still a guess from this camp.",
				"Enough to keep arguing. Nobody is writing a firm future grade.",
			],
			(_, 0) =>
			[
				"Clean first actions. The bat speed showed in BP more than the game.",
				"Took a good at-bat in the first scrimmage. Still expanding down.",
				"The glove is ahead of the bat. That is a tryout, not a projection.",
			],
			(_, 1) =>
			[
				"Jumped a first-pitch fastball. Two-strike approach is still raw.",
				"Went first to third. The tools blink; the future grade does not.",
				"Better route on the second day. Hit tool still has fog around it.",
			],
			_ =>
			[
				"Third scrimmage did not close the book. Ceiling stays cloudy.",
				"Showed enough to stay in the conversation. Potential is not a number yet.",
				"The present is in front of us. The projection still needs a longer look.",
			],
		};

		return rng.Pick(pool);
	}
}
