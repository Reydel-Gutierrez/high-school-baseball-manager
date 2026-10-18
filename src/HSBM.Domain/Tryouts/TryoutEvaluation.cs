namespace HSBM.Domain;

/// <summary>A coach's read on one skill: a range that narrows as tryouts go on.</summary>
public sealed record DrillGrade(string Skill, int Low, int High)
{
	public int Mid => (Low + High) / 2;

	public string RangeLabel => Low == High ? $"{Low}" : $"{Low}–{High}";
}

/// <summary>Everything the coaching staff knows about a tryout candidate today.</summary>
public sealed record TryoutReport(
	Guid PlayerId,
	int DaysSeen,
	int OverallLow,
	int OverallHigh,
	int PotentialLow,
	int PotentialHigh,
	IReadOnlyList<DrillGrade> Drills,
	string Note)
{
	public int OverallMid => (OverallLow + OverallHigh) / 2;

	public int PotentialMid => (PotentialLow + PotentialHigh) / 2;

	public string OverallLabel => OverallLow == OverallHigh ? $"{OverallLow}" : $"{OverallLow}–{OverallHigh}";

	public string PotentialLabel => PotentialLow == PotentialHigh ? $"{PotentialLow}" : $"{PotentialLow}–{PotentialHigh}";
}

/// <summary>
/// High school tryouts: six days of drills and scrimmages. Before tryouts
/// the staff only has a first impression; every day of work narrows the
/// read on a kid. The error is deterministic per player and skill (the
/// same kid always "looks" the same way to your staff), so it can't be
/// re-rolled, and it shrinks toward the truth as days go by.
/// </summary>
public static class TryoutEvaluation
{
	public const int TryoutDays = 6;

	/// <summary>How many tryout days the staff has watched by this date (0–6).</summary>
	public static int DaysSeen(int seasonYear, DateOnly today)
	{
		SeasonEvent week = SeasonCalendar.TryoutWeek(seasonYear);
		if (today < week.Start)
		{
			return 0;
		}

		if (today > week.End)
		{
			return TryoutDays;
		}

		return Math.Clamp(today.DayNumber - week.Start.DayNumber + 1, 1, TryoutDays);
	}

	/// <summary>Width of the estimate range for a skill after this many days.</summary>
	public static int Spread(int daysSeen) => daysSeen switch
	{
		<= 0 => 16,
		1 => 11,
		2 => 8,
		3 => 6,
		4 => 4,
		5 => 3,
		_ => 2,
	};

	public static TryoutReport Report(Player player, int daysSeen)
	{
		int spread = Spread(daysSeen);
		var drills = new List<DrillGrade>();
		if (player.PrimaryPosition.IsPitcher())
		{
			drills.Add(Grade(player, "Velocity", player.Ratings.Stuff, spread, 6));
			drills.Add(Grade(player, "Command", player.Ratings.Control, spread, 7));
			drills.Add(Grade(player, "Movement", player.Ratings.Movement, spread, 8));
			drills.Add(Grade(player, "Endurance", player.Ratings.Stamina, spread, 9));
		}
		else
		{
			drills.Add(Grade(player, "Contact", player.Ratings.Contact, spread, 0));
			drills.Add(Grade(player, "Power", player.Ratings.Power, spread, 1));
			drills.Add(Grade(player, "Eye", player.Ratings.PlateDiscipline, spread, 2));
			drills.Add(Grade(player, "Speed", player.Ratings.Speed, spread, 3));
			drills.Add(Grade(player, "Glove", player.Ratings.Fielding, spread, 4));
			drills.Add(Grade(player, "Arm", player.Ratings.ArmStrength, spread, 5));
		}

		DrillGrade overall = Grade(player, "Overall", player.Overall, spread, 20);
		int potentialSpread = spread + (daysSeen >= TryoutDays ? 4 : 8);
		DrillGrade potential = Grade(player, "Potential", Math.Max(player.Potential, player.Overall), potentialSpread, 21);
		int potentialLow = Math.Max(potential.Low, overall.Low);
		int potentialHigh = Math.Max(potential.High, overall.High);
		return new TryoutReport(
			player.Id,
			daysSeen,
			overall.Low,
			overall.High,
			potentialLow,
			potentialHigh,
			drills,
			Note(player, daysSeen));
	}

	private static DrillGrade Grade(Player player, string skill, int truth, int spread, int salt)
	{
		// Stable per player+skill bias in [-1, 1]: how this kid "reads" to your staff.
		double bias = Bias(player.Id, salt);
		double center = truth + bias * spread * 0.6;
		int low = PlayerRatings.ClampValue((int)Math.Round(center - spread / 2.0));
		int high = PlayerRatings.ClampValue(low + spread);
		if (truth < low)
		{
			low = truth;
		}
		else if (truth > high)
		{
			high = truth;
		}

		return new DrillGrade(skill, low, high);
	}

	private static double Bias(Guid id, int salt)
	{
		unchecked
		{
			int hash = 17 + salt * 7919;
			foreach (byte b in id.ToByteArray())
			{
				hash = hash * 31 + b;
			}

			return ((hash & 0x7FFFFFFF) % 2001) / 1000.0 - 1.0;
		}
	}

	private static string Note(Player player, int daysSeen)
	{
		string build = player.Physical.HeightInches >= 74 ? "Tall, long-limbed"
			: player.Physical.HeightInches <= 67 ? "Undersized"
			: "Average build";
		if (daysSeen <= 0)
		{
			return $"{build} {GradeWord(player.Grade)}. Signed up as a {player.PrimaryPosition.ToCode()}. Nobody has seen him play yet.";
		}

		string attitude = player.Personality switch
		{
			PlayerTemperament.Coachable => "Takes coaching well — fixes things between reps.",
			PlayerTemperament.Intense => "Competes on every rep. Hates losing a drill.",
			PlayerTemperament.Competitive => "Wants the ball in big spots.",
			PlayerTemperament.QuietLeader => "Quiet, but the other kids follow him.",
			PlayerTemperament.Vocal => "Loud in the dugout. Keeps energy up.",
			PlayerTemperament.Fiery => "Emotional. Great when it's going well.",
			PlayerTemperament.Easygoing => "Relaxed — maybe too relaxed.",
			_ => "Even-keeled. Nothing rattles him.",
		};
		return daysSeen >= 3 ? $"{build}. {attitude}" : $"{build}. Still getting a feel for him.";
	}

	private static string GradeWord(PlayerGrade grade) => grade switch
	{
		PlayerGrade.Freshman => "freshman",
		PlayerGrade.Sophomore => "sophomore",
		PlayerGrade.Junior => "junior",
		_ => "senior",
	};
}
