namespace HSBM.Domain;

public enum TryoutDay
{
	Day1 = 0,
	Day2 = 1,
	Day3 = 2,
	Final = 3,
}

public enum TryoutDecision
{
	Pending = 0,
	Offered = 1,
	Cut = 2,
}

/// <summary>
/// Incoming-class camp: three scrimmages plus a decision board.
/// Present talent is more visible than future projection.
/// </summary>
public sealed class TryoutCamp
{
	public const int InviteCount = 32;
	public const int RosterSpots = 20;

	public TryoutCamp(
		int seasonYear,
		SeasonEvent window,
		IReadOnlyList<TryoutProspect> prospects,
		IReadOnlyList<TryoutGame>? games = null)
	{
		if (prospects.Count != InviteCount)
		{
			throw new ArgumentException($"Tryout camp requires {InviteCount} invited players.", nameof(prospects));
		}

		SeasonYear = seasonYear;
		Window = window;
		Prospects = prospects;
		Games = games ?? [];
	}

	public int SeasonYear { get; }

	public SeasonEvent Window { get; }

	public IReadOnlyList<TryoutProspect> Prospects { get; }

	public IReadOnlyList<TryoutGame> Games { get; }

	public TryoutGame? GameFor(TryoutDay day)
	{
		foreach (TryoutGame game in Games)
		{
			if (game.Day == day)
			{
				return game;
			}
		}

		return null;
	}

	public TryoutGame? GameOn(DateOnly date)
	{
		foreach (TryoutGame game in Games)
		{
			if (game.Date == date)
			{
				return game;
			}
		}

		return null;
	}

	public TryoutGame? GameNumber(int number)
	{
		foreach (TryoutGame game in Games)
		{
			if (game.Number == number)
			{
				return game;
			}
		}

		return null;
	}

	public int OfferedCount
	{
		get
		{
			int count = 0;
			foreach (TryoutProspect prospect in Prospects)
			{
				if (prospect.Decision == TryoutDecision.Offered)
				{
					count++;
				}
			}

			return count;
		}
	}

	public int PendingDecisionCount
	{
		get
		{
			int count = 0;
			foreach (TryoutProspect prospect in Prospects)
			{
				if (prospect.Decision == TryoutDecision.Pending)
				{
					count++;
				}
			}

			return count;
		}
	}

	public bool HasPendingDecisions => PendingDecisionCount > 0;

	public int SpotsRemaining => RosterSpots - OfferedCount;

	public TryoutProspect? Find(Guid id)
	{
		foreach (TryoutProspect prospect in Prospects)
		{
			if (prospect.Id == id)
			{
				return prospect;
			}
		}

		return null;
	}

	public bool TryOffer(Guid id)
	{
		TryoutProspect? prospect = Find(id);
		if (prospect == null)
		{
			return false;
		}

		if (prospect.Decision == TryoutDecision.Offered)
		{
			return true;
		}

		prospect.Decision = TryoutDecision.Offered;
		return true;
	}

	public void Cut(Guid id)
	{
		TryoutProspect? prospect = Find(id);
		if (prospect != null)
		{
			prospect.Decision = TryoutDecision.Cut;
		}
	}

	public void Hold(Guid id)
	{
		TryoutProspect? prospect = Find(id);
		if (prospect != null)
		{
			prospect.Decision = TryoutDecision.Pending;
		}
	}

	public static DateOnly ScrimmageDate(SeasonEvent window, TryoutDay day) => day switch
	{
		TryoutDay.Day1 => window.Start,
		TryoutDay.Day2 => window.Start.AddDays(2),
		TryoutDay.Day3 => window.Start.AddDays(4),
		_ => window.End,
	};

	public static bool HasPlayed(DateOnly today, SeasonEvent window, TryoutDay day) =>
		day == TryoutDay.Final
			? today >= window.End
			: today >= ScrimmageDate(window, day);

	public static TryoutDay HighestUnlocked(DateOnly today, SeasonEvent window)
	{
		if (today < window.Start)
		{
			return TryoutDay.Day1;
		}

		if (today >= window.End)
		{
			return TryoutDay.Final;
		}

		int offset = today.DayNumber - window.Start.DayNumber;
		return offset switch
		{
			0 or 1 => TryoutDay.Day1,
			2 or 3 => TryoutDay.Day2,
			4 or 5 => TryoutDay.Day3,
			_ => TryoutDay.Final,
		};
	}

	public static bool IsUnlocked(DateOnly today, SeasonEvent window, TryoutDay day) =>
		day <= HighestUnlocked(today, window);

	public static int LooksThrough(TryoutDay day) => day switch
	{
		TryoutDay.Day1 => 1,
		TryoutDay.Day2 => 2,
		TryoutDay.Day3 => 3,
		_ => 3,
	};
}

public sealed class TryoutHittingLine
{
	public int Games { get; init; }
	public int AtBats { get; init; }
	public int Runs { get; init; }
	public int Hits { get; init; }
	public int Doubles { get; init; }
	public int Triples { get; init; }
	public int HomeRuns { get; init; }
	public int Rbi { get; init; }
	public int Walks { get; init; }
	public int Strikeouts { get; init; }
	public int StolenBases { get; init; }

	public int TotalBases =>
		Math.Max(0, Hits - Doubles - Triples - HomeRuns) + (2 * Doubles) + (3 * Triples) + (4 * HomeRuns);

	public float Average => AtBats == 0 ? 0f : (float)Hits / AtBats;

	public float OnBase
	{
		get
		{
			int denom = AtBats + Walks;
			return denom == 0 ? 0f : (float)(Hits + Walks) / denom;
		}
	}

	public float Slugging => AtBats == 0 ? 0f : (float)TotalBases / AtBats;

	public static TryoutHittingLine Empty { get; } = new();

	public TryoutHittingLine Combine(TryoutHittingLine other) =>
		new()
		{
			Games = Games + other.Games,
			AtBats = AtBats + other.AtBats,
			Runs = Runs + other.Runs,
			Hits = Hits + other.Hits,
			Doubles = Doubles + other.Doubles,
			Triples = Triples + other.Triples,
			HomeRuns = HomeRuns + other.HomeRuns,
			Rbi = Rbi + other.Rbi,
			Walks = Walks + other.Walks,
			Strikeouts = Strikeouts + other.Strikeouts,
			StolenBases = StolenBases + other.StolenBases,
		};

	public TryoutHittingLine Scale(double ratio, int games)
	{
		ratio = Math.Clamp(ratio, 0.0, 1.0);
		return new TryoutHittingLine
		{
			Games = Math.Max(0, games),
			AtBats = ScaleCount(AtBats, ratio),
			Runs = ScaleCount(Runs, ratio),
			Hits = ScaleCount(Hits, ratio),
			Doubles = ScaleCount(Doubles, ratio),
			Triples = ScaleCount(Triples, ratio),
			HomeRuns = ScaleCount(HomeRuns, ratio),
			Rbi = ScaleCount(Rbi, ratio),
			Walks = ScaleCount(Walks, ratio),
			Strikeouts = ScaleCount(Strikeouts, ratio),
			StolenBases = ScaleCount(StolenBases, ratio),
		};
	}

	private static int ScaleCount(int value, double ratio) =>
		(int)Math.Round(value * ratio, MidpointRounding.AwayFromZero);
}

public sealed class TryoutPitchingLine
{
	public int Games { get; init; }
	public int GamesStarted { get; init; }
	public int Wins { get; init; }
	public int Losses { get; init; }
	public int Saves { get; init; }
	public int Outs { get; init; }
	public int Hits { get; init; }
	public int Runs { get; init; }
	public int EarnedRuns { get; init; }
	public int Walks { get; init; }
	public int Strikeouts { get; init; }
	public int HomeRuns { get; init; }

	public float Innings => Outs / 3f;

	public string InningsLabel => $"{Outs / 3}.{Outs % 3}";

	public float Era => Outs == 0 ? 0f : EarnedRuns * 27f / Outs;

	public float Whip => Outs == 0 ? 0f : (Walks + Hits) * 3f / Outs;

	public static TryoutPitchingLine Empty { get; } = new();

	public TryoutPitchingLine Combine(TryoutPitchingLine other) =>
		new()
		{
			Games = Games + other.Games,
			GamesStarted = GamesStarted + other.GamesStarted,
			Wins = Wins + other.Wins,
			Losses = Losses + other.Losses,
			Saves = Saves + other.Saves,
			Outs = Outs + other.Outs,
			Hits = Hits + other.Hits,
			Runs = Runs + other.Runs,
			EarnedRuns = EarnedRuns + other.EarnedRuns,
			Walks = Walks + other.Walks,
			Strikeouts = Strikeouts + other.Strikeouts,
			HomeRuns = HomeRuns + other.HomeRuns,
		};

	public TryoutPitchingLine Scale(double ratio, int games)
	{
		ratio = Math.Clamp(ratio, 0.0, 1.0);
		return new TryoutPitchingLine
		{
			Games = Math.Max(0, games),
			GamesStarted = ScaleCount(GamesStarted, ratio),
			Wins = ScaleCount(Wins, ratio),
			Losses = ScaleCount(Losses, ratio),
			Saves = ScaleCount(Saves, ratio),
			Outs = ScaleCount(Outs, ratio),
			Hits = ScaleCount(Hits, ratio),
			Runs = ScaleCount(Runs, ratio),
			EarnedRuns = ScaleCount(EarnedRuns, ratio),
			Walks = ScaleCount(Walks, ratio),
			Strikeouts = ScaleCount(Strikeouts, ratio),
			HomeRuns = ScaleCount(HomeRuns, ratio),
		};
	}

	private static int ScaleCount(int value, double ratio) =>
		(int)Math.Round(value * ratio, MidpointRounding.AwayFromZero);
}

public sealed class TryoutProspect
{
	public required Guid Id { get; init; }
	public required string FirstName { get; init; }
	public required string LastName { get; init; }
	public required PlayerPosition Position { get; init; }
	public required ThrowingHand Throws { get; init; }
	public BattingSide Bats { get; init; } = BattingSide.Right;
	public required PlayerGrade Year { get; init; }
	public required int TrueOverall { get; init; }
	public required int TruePotential { get; init; }
	public required LetterGrade Day1Grade { get; init; }
	public required LetterGrade Day2Grade { get; init; }
	public required LetterGrade Day3Grade { get; init; }
	public required string Day1Note { get; init; }
	public required string Day2Note { get; init; }
	public required string Day3Note { get; init; }
	public TryoutHittingLine Day1Hitting { get; init; } = TryoutHittingLine.Empty;
	public TryoutHittingLine Day2Hitting { get; init; } = TryoutHittingLine.Empty;
	public TryoutHittingLine Day3Hitting { get; init; } = TryoutHittingLine.Empty;
	public TryoutPitchingLine? Day1Pitching { get; init; }
	public TryoutPitchingLine? Day2Pitching { get; init; }
	public TryoutPitchingLine? Day3Pitching { get; init; }
	public TryoutDecision Decision { get; set; }

	public string FullName => $"{FirstName} {LastName}";

	public string PositionLabel => Position.IsPitcher() ? "P" : Position.ToCode();

	public LetterGrade GradeOn(TryoutDay day) => day switch
	{
		TryoutDay.Day2 => Day2Grade,
		TryoutDay.Day3 => Day3Grade,
		TryoutDay.Final => CompositeGrade(),
		_ => Day1Grade,
	};

	public LetterGrade CompositeGrade()
	{
		double score = (ScoreOf(Day1Grade) + ScoreOf(Day2Grade) + ScoreOf(Day3Grade)) / 3.0;
		return LetterGradeExtensions.FromScore(score);
	}

	public IReadOnlyList<string> NotesThrough(TryoutDay day)
	{
		return day switch
		{
			TryoutDay.Day1 => [Day1Note],
			TryoutDay.Day2 => [Day1Note, Day2Note],
			_ => [Day1Note, Day2Note, Day3Note],
		};
	}

	public TryoutHittingLine HittingOn(TryoutDay day) => day switch
	{
		TryoutDay.Day2 => Day2Hitting,
		TryoutDay.Day3 => Day3Hitting,
		TryoutDay.Final => HittingThrough(TryoutDay.Day3),
		_ => Day1Hitting,
	};

	public TryoutHittingLine HittingThrough(TryoutDay day)
	{
		TryoutHittingLine total = Day1Hitting;
		if (day is TryoutDay.Day2 or TryoutDay.Day3 or TryoutDay.Final)
		{
			total = total.Combine(Day2Hitting);
		}

		if (day is TryoutDay.Day3 or TryoutDay.Final)
		{
			total = total.Combine(Day3Hitting);
		}

		return total;
	}

	public TryoutPitchingLine? PitchingOn(TryoutDay day) => day switch
	{
		TryoutDay.Day2 => Day2Pitching,
		TryoutDay.Day3 => Day3Pitching,
		TryoutDay.Final => PitchingThrough(TryoutDay.Day3),
		_ => Day1Pitching,
	};

	public TryoutPitchingLine? PitchingThrough(TryoutDay day)
	{
		TryoutPitchingLine? total = Day1Pitching;
		if (day is TryoutDay.Day2 or TryoutDay.Day3 or TryoutDay.Final)
		{
			total = CombinePitching(total, Day2Pitching);
		}

		if (day is TryoutDay.Day3 or TryoutDay.Final)
		{
			total = CombinePitching(total, Day3Pitching);
		}

		return total;
	}

	private static TryoutPitchingLine? CombinePitching(TryoutPitchingLine? left, TryoutPitchingLine? right)
	{
		if (left == null)
		{
			return right;
		}

		return right == null ? left : left.Combine(right);
	}

	private static double ScoreOf(LetterGrade grade) => grade switch
	{
		LetterGrade.APlus => 92,
		LetterGrade.A => 85,
		LetterGrade.AMinus => 79,
		LetterGrade.BPlus => 73,
		LetterGrade.B => 68,
		LetterGrade.BMinus => 63,
		LetterGrade.CPlus => 58,
		LetterGrade.C => 53,
		LetterGrade.CMinus => 48,
		LetterGrade.DPlus => 43,
		LetterGrade.D => 38,
		LetterGrade.DMinus => 33,
		_ => 20,
	};
}

/// <summary>
/// Tryouts are evaluation and scouting. Potential is never a perfect number.
/// </summary>
public static class TryoutScouting
{
	public static string FormatOverall(int trueOverall, int looks, int salt)
	{
		(int low, int high) = OverallBand(trueOverall, looks, salt);
		return $"{low}-{high}";
	}

	public static int Midpoint(int trueOverall, int looks, int salt)
	{
		(int low, int high) = OverallBand(trueOverall, looks, salt);
		return (low + high) / 2;
	}

	public static (int Low, int High) OverallBand(int trueOverall, int looks, int salt)
	{
		int spread = looks <= 1 ? 12 : looks == 2 ? 8 : looks == 3 ? 5 : looks == 4 ? 4 : 3;
		var rng = new SeededRandom(salt ^ 9181);
		int low = PlayerRatings.ClampValue(trueOverall - spread + rng.Next(0, 4));
		int high = PlayerRatings.ClampValue(trueOverall + rng.Next(0, looks <= 1 ? 2 : 1));
		low = Math.Min(low, trueOverall);
		high = Math.Max(high, trueOverall);
		if (low == high)
		{
			low = PlayerRatings.ClampValue(low - 3);
			high = PlayerRatings.ClampValue(high + 2);
		}

		return (low, high);
	}

	public static string FormatOverall(TryoutProspect prospect, TryoutDay day) =>
		FormatOverall(prospect.TrueOverall, TryoutCamp.LooksThrough(day), StableSalt(prospect.Id));

	public static string FormatPotential(int trueOverall, int truePotential, int looks, int salt)
	{
		if (looks <= 1)
		{
			return "?";
		}

		int upside = truePotential - trueOverall;
		int neededLooks = upside >= 12 ? 3 : 2;
		if (looks < neededLooks)
		{
			return "?";
		}

		int spread = looks <= 3 ? 14 : looks == 4 ? 10 : 7;
		var rng = new SeededRandom(salt);
		int low = PlayerRatings.ClampValue(truePotential - spread + rng.Next(0, 4));
		int high = PlayerRatings.ClampValue(truePotential + rng.Next(0, 4));
		low = Math.Min(low, truePotential - 2);
		high = Math.Max(high, truePotential);
		low = PlayerRatings.ClampValue(low);
		high = PlayerRatings.ClampValue(high);
		if (low >= high)
		{
			low = PlayerRatings.ClampValue(truePotential - 8);
			high = PlayerRatings.ClampValue(truePotential + 3);
		}

		return $"{low}-{high}";
	}

	public static string FormatPotential(TryoutProspect prospect, TryoutDay day) =>
		FormatPotential(
			prospect.TrueOverall,
			prospect.TruePotential,
			TryoutCamp.LooksThrough(day),
			StableSalt(prospect.Id));

	public static LetterGrade RollDayGrade(int overall, int day, ISeededRandom rng)
	{
		int noise = rng.Next(-10, 13) + (day == 2 ? 1 : 0);
		return LetterGradeExtensions.FromScore(overall + noise);
	}

	private static int StableSalt(Guid id)
	{
		byte[] bytes = id.ToByteArray();
		unchecked
		{
			int hash = 17;
			foreach (byte b in bytes)
			{
				hash = (hash * 31) + b;
			}

			return hash;
		}
	}
}
