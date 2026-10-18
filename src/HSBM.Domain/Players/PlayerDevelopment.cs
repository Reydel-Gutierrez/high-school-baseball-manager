namespace HSBM.Domain;

public enum TrainingFocus
{
	Balanced = 0,
	Hitting = 1,
	Power = 2,
	Pitching = 3,
	Defense = 4,
	Speed = 5,
}

public enum TrainingIntensity
{
	Light = 0,
	Normal = 1,
	Hard = 2,
}

/// <summary>A coach's standing practice plan for one team (varsity or JV).</summary>
public sealed class TeamTraining
{
	public string TeamId { get; set; } = "";
	public TrainingFocus Focus { get; set; } = TrainingFocus.Balanced;
	public TrainingIntensity Intensity { get; set; } = TrainingIntensity.Normal;
}

public static class TrainingExtensions
{
	public static string ToLabel(this TrainingFocus focus) => focus switch
	{
		TrainingFocus.Hitting => "Hitting",
		TrainingFocus.Power => "Power",
		TrainingFocus.Pitching => "Pitching",
		TrainingFocus.Defense => "Defense",
		TrainingFocus.Speed => "Speed & Baserunning",
		_ => "Balanced",
	};

	public static string ToBlurb(this TrainingFocus focus) => focus switch
	{
		TrainingFocus.Hitting => "Cage work and situational at-bats. Contact and plate discipline grow fastest.",
		TrainingFocus.Power => "Weight room and long tee. Power climbs; contact can lag.",
		TrainingFocus.Pitching => "Bullpens, pitch design, and conditioning. Arms develop fastest; bats slow down.",
		TrainingFocus.Defense => "Infield drills, throwing programs, and catcher blocking. Gloves and arms improve.",
		TrainingFocus.Speed => "Sprint work, jumps, and reads on the bases. Speed and range improve.",
		_ => "A little of everything. No area is neglected.",
	};

	public static string ToLabel(this TrainingIntensity intensity) => intensity switch
	{
		TrainingIntensity.Light => "Light",
		TrainingIntensity.Hard => "Hard",
		_ => "Normal",
	};

	public static string ToBlurb(this TrainingIntensity intensity) => intensity switch
	{
		TrainingIntensity.Light => "Fresh legs, fewer tweaks. Slower growth.",
		TrainingIntensity.Hard => "Faster growth, but practice injuries are more common.",
		_ => "The standard program.",
	};
}

/// <summary>Result of one development tick for a player.</summary>
public sealed record DevelopmentTick(int OverallBefore, int OverallAfter, int PotentialBefore, int PotentialAfter)
{
	public int Gain => OverallAfter - OverallBefore;

	public bool Breakout => PotentialAfter > PotentialBefore;
}

/// <summary>
/// How players grow. Every player closes part of the gap between his
/// current overall and his potential each year; the share depends on his
/// class, personality, playing time, the program's facilities, and the
/// coach's practice plan. Growth is paid out as individual rating points
/// on the attributes his position (and the training focus) values, so the
/// overall formula — not a separate number — reflects the improvement.
/// About 60% of a year's growth arrives in the offseason; the rest is
/// earned week by week in season.
/// </summary>
public static class PlayerDevelopment
{
	public const double OffseasonShare = 0.60;
	public const int SeasonWeeks = 16;

	private const int Attributes = 10;

	/// <summary>Expected overall points gained over a full year (offseason + season).</summary>
	public static double AnnualGrowth(
		Player player,
		TrainingFocus focus,
		TrainingIntensity intensity,
		LetterGrade facilities,
		double playingShare)
	{
		int gap = Math.Max(0, player.Potential - player.Overall);
		if (gap == 0)
		{
			return 0;
		}

		double rate = player.Grade switch
		{
			PlayerGrade.Freshman => 0.42,
			PlayerGrade.Sophomore => 0.38,
			PlayerGrade.Junior => 0.33,
			_ => 0.26,
		};

		double growth = gap * rate
			* PersonalityFactor(player.Personality)
			* FacilitiesFactor(facilities)
			* PlayingTimeFactor(playingShare)
			* IntensityFactor(intensity)
			* FocusFit(player, focus);

		// Small floor so a player with real upside never stalls completely.
		return Math.Max(growth, Math.Min(gap, 0.6));
	}

	/// <summary>One in-season week of practice and games.</summary>
	public static DevelopmentTick Weekly(
		Player player,
		TrainingFocus focus,
		TrainingIntensity intensity,
		LetterGrade facilities,
		double playingShare,
		ISeededRandom rng)
	{
		double expected = AnnualGrowth(player, focus, intensity, facilities, playingShare)
			* (1 - OffseasonShare) / SeasonWeeks;
		double noise = 0.6 + rng.NextDouble() * 0.8;
		return Apply(player, expected * noise, focus, rng, breakoutChance: BreakoutChance(player, playingShare) / SeasonWeeks);
	}

	/// <summary>Winter work before the new season (applied at rollover, before grades advance).</summary>
	public static DevelopmentTick Offseason(
		Player player,
		LetterGrade facilities,
		double playingShare,
		ISeededRandom rng)
	{
		double expected = AnnualGrowth(player, TrainingFocus.Balanced, TrainingIntensity.Normal, facilities, playingShare)
			* OffseasonShare;
		double noise = 0.7 + rng.NextDouble() * 0.6;
		return Apply(player, expected * noise, TrainingFocus.Balanced, rng, breakoutChance: BreakoutChance(player, playingShare));
	}

	/// <summary>Weekly chance a practice goes wrong for one player.</summary>
	public static double PracticeInjuryChance(TrainingIntensity intensity) => intensity switch
	{
		TrainingIntensity.Light => 0.0012,
		TrainingIntensity.Hard => 0.0090,
		_ => 0.0035,
	};

	/// <summary>Short, human label for how a player is trending.</summary>
	public static string Outlook(Player player)
	{
		int gap = player.Potential - player.Overall;
		if (gap <= 0)
		{
			return "Peaked";
		}

		bool young = player.Grade is PlayerGrade.Freshman or PlayerGrade.Sophomore;
		return gap switch
		{
			>= 12 when young => "High ceiling",
			>= 8 => "Rising",
			>= 4 => "Steady",
			_ => "Near ceiling",
		};
	}

	private static DevelopmentTick Apply(
		Player player,
		double overallPoints,
		TrainingFocus focus,
		ISeededRandom rng,
		double breakoutChance)
	{
		int beforeOverall = player.Overall;
		int beforePotential = player.Potential;
		if (breakoutChance > 0 && rng.NextDouble() < breakoutChance)
		{
			player.Potential = PlayerRatings.ClampValue(player.Potential + rng.Next(2, 6));
		}

		player.DevelopmentProgress += (int)Math.Round(Math.Max(0, overallPoints) * 100);
		double[] weights = AttributeWeights(player.PrimaryPosition, focus);
		int guard = 0;
		while (player.Overall < player.Potential && guard++ < 200)
		{
			int pick = WeightedPick(weights, rng);
			int cost = Math.Max(5, (int)Math.Round(OverallWeight(player.PrimaryPosition, pick) * 100));
			if (player.DevelopmentProgress < cost)
			{
				break;
			}

			if (!TryRaise(player, pick))
			{
				weights[pick] = 0;
				if (Array.TrueForAll(weights, w => w <= 0))
				{
					break;
				}

				continue;
			}

			player.DevelopmentProgress -= cost;
		}

		if (player.Overall >= player.Potential)
		{
			player.DevelopmentProgress = 0;
		}

		return new DevelopmentTick(beforeOverall, player.Overall, beforePotential, player.Potential);
	}

	private static double BreakoutChance(Player player, double playingShare)
	{
		double chance = player.Grade is PlayerGrade.Freshman or PlayerGrade.Sophomore ? 0.06 : 0.03;
		chance *= player.Personality switch
		{
			PlayerTemperament.Coachable or PlayerTemperament.Intense => 1.6,
			PlayerTemperament.Competitive or PlayerTemperament.QuietLeader => 1.25,
			PlayerTemperament.Easygoing => 0.6,
			_ => 1.0,
		};
		return chance * (0.7 + 0.6 * Math.Clamp(playingShare, 0, 1));
	}

	private static double PersonalityFactor(PlayerTemperament temperament) => temperament switch
	{
		PlayerTemperament.Coachable => 1.25,
		PlayerTemperament.Intense => 1.12,
		PlayerTemperament.Competitive => 1.10,
		PlayerTemperament.QuietLeader => 1.08,
		PlayerTemperament.Composed => 1.0,
		PlayerTemperament.Vocal => 1.0,
		PlayerTemperament.Fiery => 0.95,
		PlayerTemperament.Easygoing => 0.85,
		_ => 1.0,
	};

	private static double FacilitiesFactor(LetterGrade facilities) =>
		0.85 + 0.30 * ((int)facilities / 12.0);

	private static double PlayingTimeFactor(double share) =>
		0.85 + 0.30 * Math.Clamp(share, 0, 1);

	private static double IntensityFactor(TrainingIntensity intensity) => intensity switch
	{
		TrainingIntensity.Light => 0.80,
		TrainingIntensity.Hard => 1.20,
		_ => 1.0,
	};

	/// <summary>Pitchers get little from a hitting-focused week and vice versa.</summary>
	private static double FocusFit(Player player, TrainingFocus focus)
	{
		bool pitcher = player.PrimaryPosition.IsPitcher();
		return focus switch
		{
			TrainingFocus.Balanced => 1.0,
			TrainingFocus.Pitching => pitcher ? 1.2 : 0.75,
			_ => pitcher ? 0.75 : 1.12,
		};
	}

	// Attribute order: 0 Contact, 1 Power, 2 Discipline, 3 Speed, 4 Fielding,
	// 5 Arm, 6 Stuff, 7 Control, 8 Movement, 9 Stamina.
	private static double[] AttributeWeights(PlayerPosition position, TrainingFocus focus)
	{
		var weights = new double[Attributes];
		for (int i = 0; i < Attributes; i++)
		{
			weights[i] = OverallWeight(position, i) * FocusMultiplier(focus, i);
		}

		return weights;
	}

	private static double FocusMultiplier(TrainingFocus focus, int attribute) => (focus, attribute) switch
	{
		(TrainingFocus.Hitting, 0) => 1.7,
		(TrainingFocus.Hitting, 2) => 1.5,
		(TrainingFocus.Hitting, 1) => 1.1,
		(TrainingFocus.Power, 1) => 1.9,
		(TrainingFocus.Power, 0) => 0.85,
		(TrainingFocus.Defense, 4) => 1.8,
		(TrainingFocus.Defense, 5) => 1.5,
		(TrainingFocus.Speed, 3) => 2.0,
		(TrainingFocus.Speed, 4) => 1.2,
		(TrainingFocus.Pitching, >= 6) => 1.4,
		_ => 1.0,
	};

	/// <summary>How much one point of an attribute moves this position's overall.</summary>
	private static double OverallWeight(PlayerPosition position, int attribute) =>
		PlayerOverall.Weight(position, attribute);

	private static int WeightedPick(double[] weights, ISeededRandom rng)
	{
		double total = 0;
		foreach (double w in weights)
		{
			total += w;
		}

		double roll = rng.NextDouble() * total;
		for (int i = 0; i < weights.Length; i++)
		{
			roll -= weights[i];
			if (roll <= 0 && weights[i] > 0)
			{
				return i;
			}
		}

		for (int i = weights.Length - 1; i >= 0; i--)
		{
			if (weights[i] > 0)
			{
				return i;
			}
		}

		return 0;
	}

	private static bool TryRaise(Player player, int attribute)
	{
		PlayerRatings r = player.Ratings;
		int current = attribute switch
		{
			0 => r.Contact,
			1 => r.Power,
			2 => r.PlateDiscipline,
			3 => r.Speed,
			4 => r.Fielding,
			5 => r.ArmStrength,
			6 => r.Stuff,
			7 => r.Control,
			8 => r.Movement,
			_ => r.Stamina,
		};
		if (current >= PlayerRatings.Max)
		{
			return false;
		}

		player.Ratings = attribute switch
		{
			0 => r with { Contact = current + 1 },
			1 => r with { Power = current + 1 },
			2 => r with { PlateDiscipline = current + 1 },
			3 => r with { Speed = current + 1 },
			4 => r with { Fielding = current + 1 },
			5 => r with { ArmStrength = current + 1 },
			6 => r with { Stuff = current + 1 },
			7 => r with { Control = current + 1 },
			8 => r with { Movement = current + 1 },
			_ => r with { Stamina = current + 1 },
		};
		return true;
	}
}
