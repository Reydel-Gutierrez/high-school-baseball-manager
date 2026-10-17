namespace HSBM.Domain;

/// <summary>
/// Coach name on the circuit. Athletic directors use this — not program
/// reputation — when they decide whether a vacancy is even on the table.
/// </summary>
public static class CoachReputation
{
	public const int Min = 1;
	public const int Max = 99;
	public const int Starting = 42;

	/// <summary>
	/// How far above the coach a program can sit and still take the call.
	/// </summary>
	public const int ReachSlack = 12;

	public static int Clamp(int value)
	{
		if (value < Min)
		{
			return Min;
		}

		return value > Max ? Max : value;
	}

	public static string ToLabel(int value) => Clamp(value) switch
	{
		>= 85 => "Elite",
		>= 70 => "Respected",
		>= 55 => "Established",
		>= 40 => "Rising",
		_ => "Unknown",
	};

	public static bool CanReach(int coachReputation, int programReputation) =>
		programReputation <= Clamp(coachReputation) + ReachSlack;

	public static int AfterSeason(
		int opening,
		bool winning,
		bool playoffs,
		bool wonDistrict,
		bool wonRegional,
		bool wonState,
		bool targetMet)
	{
		int gain = SeasonReview.ReputationGain(
			winning,
			playoffs,
			wonDistrict,
			wonRegional,
			wonState,
			targetMet);
		return Clamp(opening + gain);
	}

	/// <summary>
	/// Boards talk. A firing follows the coach to the next interview.
	/// </summary>
	public static int AfterDismissal(int opening) => Clamp(opening - 6);
}
