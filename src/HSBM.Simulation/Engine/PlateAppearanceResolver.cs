using HSBM.Domain;

namespace HSBM.Simulation;

#pragma warning disable CA1720 // Single/Double are baseball outcomes, not CLR type names.
public enum PlateAppearanceOutcome
{
	Strikeout,
	Walk,
	Single,
	Double,
	Triple,
	HomeRun,
	Out,
}
#pragma warning restore CA1720

public static class PlateAppearanceResolver
{
	public static PlateAppearanceOutcome Resolve(
		PlayerRatings batter,
		PlayerRatings pitcher,
		ISeededRandom rng,
		BaseballSimulationConfig config,
		double fatigue = 0)
	{
		double contact = batter.Contact / 100.0;
		double power = batter.Power / 100.0;
		double discipline = batter.PlateDiscipline / 100.0;
		double speed = batter.Speed / 100.0;
		double tired = fatigue < 0 ? 0 : fatigue > 1 ? 1 : fatigue;
		double stuff = pitcher.Stuff / 100.0 * (1.0 - tired);
		double control = pitcher.Control / 100.0 * (1.0 - tired);
		double movement = pitcher.Movement / 100.0 * (1.0 - tired * 0.5);

		double k = Clamp(
			config.BaseStrikeoutRate + config.StrikeoutRatingWeight * (stuff - contact) - 0.04 * discipline,
			config.MinOutcomeRate,
			config.MaxStrikeoutRate);
		double bb = Clamp(
			config.BaseWalkRate + config.WalkRatingWeight * (discipline - control) - 0.03 * stuff,
			config.MinOutcomeRate,
			config.MaxWalkRate);
		double hr = Clamp(
			config.BaseHomeRunRate + config.HomeRunRatingWeight * (power - movement) + 0.008 * contact,
			config.MinOutcomeRate,
			config.MaxHomeRunRate);

		double used = k + bb + hr;
		if (used > 0.72)
		{
			double scale = 0.72 / used;
			k *= scale;
			bb *= scale;
			hr *= scale;
			used = k + bb + hr;
		}

		double inPlay = 1.0 - used;
		double babip = Clamp(
			config.BaseBabip + config.BabipRatingWeight * (contact - movement) + 0.04 * (speed - 0.5),
			0.22,
			0.40);
		double hitRate = inPlay * babip;
		double extra = Math.Max(0.05, power - movement + 0.35);
		double doubles = hitRate * Clamp(config.DoubleShare * extra, 0.08, 0.32);
		double triples = hitRate * Clamp(config.TripleShare + 0.04 * Math.Max(0, speed - 0.55), 0.004, 0.06);
		double singles = Math.Max(0, hitRate - doubles - triples);

		double roll = rng.NextDouble();
		if (roll < k)
		{
			return PlateAppearanceOutcome.Strikeout;
		}

		roll -= k;
		if (roll < bb)
		{
			return PlateAppearanceOutcome.Walk;
		}

		roll -= bb;
		if (roll < hr)
		{
			return PlateAppearanceOutcome.HomeRun;
		}

		roll -= hr;
		if (roll < singles)
		{
			return PlateAppearanceOutcome.Single;
		}

		roll -= singles;
		if (roll < doubles)
		{
			return PlateAppearanceOutcome.Double;
		}

		roll -= doubles;
		if (roll < triples)
		{
			return PlateAppearanceOutcome.Triple;
		}

		return PlateAppearanceOutcome.Out;
	}

	private static double Clamp(double value, double min, double max) =>
		value < min ? min : value > max ? max : value;
}
