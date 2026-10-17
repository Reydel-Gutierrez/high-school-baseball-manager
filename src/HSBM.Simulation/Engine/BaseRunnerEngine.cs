using HSBM.Domain;

namespace HSBM.Simulation;

public sealed class BasePaths
{
	public Player? First { get; set; }
	public Player? Second { get; set; }
	public Player? Third { get; set; }

	public void Clear()
	{
		First = null;
		Second = null;
		Third = null;
	}
}

public sealed class BaserunningResult
{
	public int Runs { get; init; }
	public List<Player> Scorers { get; init; } = [];
	public bool BatterReached { get; init; }
}

public static class BaseRunnerEngine
{
	public static BaserunningResult Apply(
		BasePaths bases,
		Player batter,
		PlateAppearanceOutcome outcome,
		ISeededRandom rng,
		BaseballSimulationConfig config)
	{
		return outcome switch
		{
			PlateAppearanceOutcome.Walk => ApplyWalk(bases, batter),
			PlateAppearanceOutcome.Single => ApplySingle(bases, batter, rng, config),
			PlateAppearanceOutcome.Double => ApplyDouble(bases, batter, rng, config),
			PlateAppearanceOutcome.Triple => ApplyTriple(bases, batter),
			PlateAppearanceOutcome.HomeRun => ApplyHomeRun(bases, batter),
			_ => new BaserunningResult { Runs = 0, BatterReached = false },
		};
	}

	private static BaserunningResult ApplyWalk(BasePaths bases, Player batter)
	{
		var scorers = new List<Player>();
		Player? first = bases.First;
		Player? second = bases.Second;
		Player? third = bases.Third;

		if (first != null)
		{
			if (second != null)
			{
				if (third != null)
				{
					scorers.Add(third);
				}

				bases.Third = second;
			}

			bases.Second = first;
		}

		bases.First = batter;
		return new BaserunningResult { Runs = scorers.Count, Scorers = scorers, BatterReached = true };
	}

	private static BaserunningResult ApplySingle(
		BasePaths bases,
		Player batter,
		ISeededRandom rng,
		BaseballSimulationConfig config)
	{
		var scorers = new List<Player>();
		Player? third = bases.Third;
		Player? second = bases.Second;
		Player? first = bases.First;
		bases.Clear();

		if (third != null)
		{
			scorers.Add(third);
		}

		if (second != null)
		{
			if (rng.NextDouble() < config.SingleRunnerFromSecondScores)
			{
				scorers.Add(second);
			}
			else
			{
				bases.Third = second;
			}
		}

		if (first != null)
		{
			if (rng.NextDouble() < config.SingleRunnerFromFirstToThird && bases.Third == null)
			{
				bases.Third = first;
			}
			else
			{
				bases.Second = first;
			}
		}

		bases.First = batter;
		return new BaserunningResult { Runs = scorers.Count, Scorers = scorers, BatterReached = true };
	}

	private static BaserunningResult ApplyDouble(
		BasePaths bases,
		Player batter,
		ISeededRandom rng,
		BaseballSimulationConfig config)
	{
		var scorers = new List<Player>();
		Player? third = bases.Third;
		Player? second = bases.Second;
		Player? first = bases.First;
		bases.Clear();

		if (third != null)
		{
			scorers.Add(third);
		}

		if (second != null)
		{
			scorers.Add(second);
		}

		if (first != null)
		{
			if (rng.NextDouble() < config.DoubleRunnerFromFirstScores)
			{
				scorers.Add(first);
			}
			else
			{
				bases.Third = first;
			}
		}

		bases.Second = batter;
		return new BaserunningResult { Runs = scorers.Count, Scorers = scorers, BatterReached = true };
	}

	private static BaserunningResult ApplyTriple(BasePaths bases, Player batter)
	{
		var scorers = new List<Player>();
		if (bases.Third != null) scorers.Add(bases.Third);
		if (bases.Second != null) scorers.Add(bases.Second);
		if (bases.First != null) scorers.Add(bases.First);
		bases.Clear();
		bases.Third = batter;
		return new BaserunningResult { Runs = scorers.Count, Scorers = scorers, BatterReached = true };
	}

	private static BaserunningResult ApplyHomeRun(BasePaths bases, Player batter)
	{
		var scorers = new List<Player>();
		if (bases.Third != null) scorers.Add(bases.Third);
		if (bases.Second != null) scorers.Add(bases.Second);
		if (bases.First != null) scorers.Add(bases.First);
		scorers.Add(batter);
		bases.Clear();
		return new BaserunningResult { Runs = scorers.Count, Scorers = scorers, BatterReached = true };
	}
}
