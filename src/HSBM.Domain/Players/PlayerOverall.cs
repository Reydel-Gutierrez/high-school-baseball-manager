namespace HSBM.Domain;

/// <summary>
/// Isolated overall formula. Rebalance here rather than duplicating weights.
/// </summary>
public static class PlayerOverall
{
	public static int Calculate(PlayerPosition position, PlayerRatings ratings)
	{
		if (position.IsPitcher())
		{
			return Round(
				ratings.Stuff * 0.32 +
				ratings.Control * 0.28 +
				ratings.Movement * 0.25 +
				ratings.Stamina * 0.15);
		}

		var (contact, power, discipline, speed, fielding, arm) = HittingWeights(position);
		return Round(
			ratings.Contact * contact +
			ratings.Power * power +
			ratings.PlateDiscipline * discipline +
			ratings.Speed * speed +
			ratings.Fielding * fielding +
			ratings.ArmStrength * arm);
	}

	/// <summary>
	/// Weight of one attribute in this position's overall. Attribute order:
	/// 0 Contact, 1 Power, 2 Discipline, 3 Speed, 4 Fielding, 5 Arm,
	/// 6 Stuff, 7 Control, 8 Movement, 9 Stamina.
	/// </summary>
	public static double Weight(PlayerPosition position, int attribute)
	{
		if (position.IsPitcher())
		{
			return attribute switch
			{
				6 => 0.32,
				7 => 0.28,
				8 => 0.25,
				9 => 0.15,
				_ => 0,
			};
		}

		var (contact, power, discipline, speed, fielding, arm) = HittingWeights(position);
		return attribute switch
		{
			0 => contact,
			1 => power,
			2 => discipline,
			3 => speed,
			4 => fielding,
			5 => arm,
			_ => 0,
		};
	}

	/// <summary>Unrounded overall, for showing small development steps.</summary>
	public static double Exact(PlayerPosition position, PlayerRatings ratings)
	{
		double total = 0;
		for (int attribute = 0; attribute < 10; attribute++)
		{
			total += Weight(position, attribute) * Attribute(ratings, attribute);
		}

		return total;
	}

	public static int Attribute(PlayerRatings ratings, int attribute) => attribute switch
	{
		0 => ratings.Contact,
		1 => ratings.Power,
		2 => ratings.PlateDiscipline,
		3 => ratings.Speed,
		4 => ratings.Fielding,
		5 => ratings.ArmStrength,
		6 => ratings.Stuff,
		7 => ratings.Control,
		8 => ratings.Movement,
		_ => ratings.Stamina,
	};

	public static double OffensiveRating(PlayerRatings ratings) =>
		ratings.Contact * 0.40 +
		ratings.Power * 0.30 +
		ratings.PlateDiscipline * 0.20 +
		ratings.Speed * 0.10;

	public static double PitchingRating(PlayerRatings ratings) =>
		ratings.Stuff * 0.32 +
		ratings.Control * 0.28 +
		ratings.Movement * 0.25 +
		ratings.Stamina * 0.15;

	private static (double Contact, double Power, double Discipline, double Speed, double Fielding, double Arm)
		HittingWeights(PlayerPosition position) => position switch
	{
		PlayerPosition.Shortstop => (0.18, 0.10, 0.10, 0.12, 0.28, 0.22),
		PlayerPosition.SecondBase => (0.22, 0.10, 0.12, 0.14, 0.26, 0.16),
		PlayerPosition.ThirdBase => (0.20, 0.18, 0.12, 0.08, 0.20, 0.22),
		PlayerPosition.FirstBase => (0.28, 0.28, 0.16, 0.06, 0.14, 0.08),
		PlayerPosition.Catcher => (0.18, 0.14, 0.12, 0.06, 0.26, 0.24),
		PlayerPosition.CenterField => (0.18, 0.10, 0.10, 0.24, 0.24, 0.14),
		PlayerPosition.RightField => (0.22, 0.22, 0.12, 0.12, 0.12, 0.20),
		PlayerPosition.LeftField => (0.24, 0.22, 0.14, 0.14, 0.14, 0.12),
		_ => (0.22, 0.18, 0.14, 0.12, 0.18, 0.16),
	};

	private static int Round(double value)
	{
		int rounded = (int)System.Math.Round(value, System.MidpointRounding.AwayFromZero);
		return PlayerRatings.ClampValue(rounded);
	}
}
