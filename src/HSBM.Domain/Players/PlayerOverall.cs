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
