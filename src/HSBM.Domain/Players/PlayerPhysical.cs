namespace HSBM.Domain;

/// <summary>
/// Measurable body profile. Portrait <see cref="BodyBuild"/> is stored on
/// <see cref="PersonAppearance"/>; this is height, weight, and strength.
/// </summary>
public readonly record struct PlayerPhysical(int HeightInches, int WeightLbs, int Strength, BodyBuild Build)
{
	public const int MinHeightInches = 64;
	public const int MaxHeightInches = 82;
	public const int MinWeightLbs = 130;
	public const int MaxWeightLbs = 265;

	public static PlayerPhysical Typical(PlayerPosition position) =>
		new(TypicalHeight(position), TypicalWeight(position), 50, BodyBuild.Average);

	public string HeightLabel
	{
		get
		{
			int inches = Math.Clamp(HeightInches, MinHeightInches, MaxHeightInches);
			return $"{inches / 12}'{inches % 12}\"";
		}
	}

	public static PlayerPhysical Roll(PlayerPosition position, PlayerRatings ratings, ISeededRandom rng)
	{
		int height = TypicalHeight(position) + rng.Next(-2, 3);
		if (rng.NextDouble() < 0.08)
		{
			height += rng.NextDouble() < 0.5 ? -2 : 3;
		}

		height = Math.Clamp(height, MinHeightInches, MaxHeightInches);
		BodyBuild build = RollBuild(position, ratings, rng);
		int strength = StrengthFor(position, ratings, build, rng);
		int weight = WeightFor(height, build, strength, rng);
		return new PlayerPhysical(height, weight, strength, build);
	}

	private static int TypicalHeight(PlayerPosition position) => position switch
	{
		PlayerPosition.FirstBase or PlayerPosition.StartingPitcher => 74,
		PlayerPosition.ThirdBase or PlayerPosition.ReliefPitcher => 73,
		PlayerPosition.Catcher or PlayerPosition.RightField => 72,
		PlayerPosition.LeftField => 71,
		PlayerPosition.CenterField => 70,
		PlayerPosition.Shortstop or PlayerPosition.SecondBase => 69,
		_ => 71,
	};

	private static int TypicalWeight(PlayerPosition position) =>
		WeightFor(TypicalHeight(position), BodyBuild.Average, 50, rng: null);

	private static BodyBuild RollBuild(PlayerPosition position, PlayerRatings ratings, ISeededRandom rng)
	{
		double power = ratings.Power;
		double speed = ratings.Speed;
		if (position.IsPitcher())
		{
			power = (ratings.Stamina + ratings.Stuff) / 2.0;
		}

		double roll = rng.NextDouble();
		if (speed >= 78 && power < 62)
		{
			return roll < 0.72 ? BodyBuild.Slim : BodyBuild.Athletic;
		}

		if (power >= 78 && speed < 55)
		{
			return roll < 0.62 ? BodyBuild.Stocky : BodyBuild.Athletic;
		}

		if (roll < 0.18)
		{
			return BodyBuild.Slim;
		}

		if (roll < 0.62)
		{
			return BodyBuild.Athletic;
		}

		return roll < 0.86 ? BodyBuild.Average : BodyBuild.Stocky;
	}

	private static int StrengthFor(
		PlayerPosition position,
		PlayerRatings ratings,
		BodyBuild build,
		ISeededRandom rng)
	{
		int core = position.IsPitcher()
			? (ratings.Stamina + ratings.ArmStrength) / 2
			: (ratings.Power + ratings.ArmStrength) / 2;
		int shift = build switch
		{
			BodyBuild.Stocky => 6,
			BodyBuild.Athletic => 3,
			BodyBuild.Slim => -5,
			_ => 0,
		};
		return PlayerRatings.ClampValue(core + shift + rng.Next(-3, 4));
	}

	private static int WeightFor(int heightInches, BodyBuild build, int strength, ISeededRandom? rng)
	{
		int baseWeight = 155 + ((heightInches - 68) * 5);
		int buildShift = build switch
		{
			BodyBuild.Stocky => 22,
			BodyBuild.Athletic => 8,
			BodyBuild.Slim => -14,
			_ => 0,
		};
		int strengthShift = (strength - 50) / 4;
		int jitter = rng?.Next(-6, 7) ?? 0;
		return Math.Clamp(baseWeight + buildShift + strengthShift + jitter, MinWeightLbs, MaxWeightLbs);
	}
}
