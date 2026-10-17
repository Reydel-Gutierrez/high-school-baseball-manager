namespace HSBM.Domain;

/// <summary>
/// On-field temperament. Recruiting weights stay on
/// <see cref="RecruitingPriorities"/>; this leans school decisions and
/// how the player carries himself on a roster.
/// </summary>
public enum PlayerTemperament
{
	Composed = 0,
	Competitive = 1,
	Vocal = 2,
	QuietLeader = 3,
	Fiery = 4,
	Coachable = 5,
	Easygoing = 6,
	Intense = 7,
}

public static class PlayerPersonality
{
	public static PlayerTemperament Roll(ISeededRandom rng) =>
		rng.Pick(Enum.GetValues<PlayerTemperament>());

	public static string Label(this PlayerTemperament temperament) => temperament switch
	{
		PlayerTemperament.Competitive => "Competitive",
		PlayerTemperament.Vocal => "Vocal",
		PlayerTemperament.QuietLeader => "Quiet Leader",
		PlayerTemperament.Fiery => "Fiery",
		PlayerTemperament.Coachable => "Coachable",
		PlayerTemperament.Easygoing => "Easygoing",
		PlayerTemperament.Intense => "Intense",
		_ => "Composed",
	};

	public static string CareerHook(this PlayerTemperament temperament) => temperament switch
	{
		PlayerTemperament.Competitive => "Keeps score of every slight and every win",
		PlayerTemperament.Vocal => "Sets the tone in the dugout whether the staff wants it or not",
		PlayerTemperament.QuietLeader => "Leads by how he works, not by how loud he is",
		PlayerTemperament.Fiery => "Plays angry and does not hide it",
		PlayerTemperament.Coachable => "Takes instruction and actually uses it",
		PlayerTemperament.Easygoing => "Stays loose even when the program is tight",
		PlayerTemperament.Intense => "Treats every day like a tryout",
		_ => "Does not get rattled when the inning gets loud",
	};

	/// <summary>
	/// Soft recruiting lean. Not a second set of weights — a nudge on top.
	/// </summary>
	public static RecruitingArchetype PreferredArchetype(this PlayerTemperament temperament) => temperament switch
	{
		PlayerTemperament.Competitive => RecruitingArchetype.PrestigeSeeker,
		PlayerTemperament.Vocal => RecruitingArchetype.PrestigeSeeker,
		PlayerTemperament.QuietLeader => RecruitingArchetype.DevelopmentFirst,
		PlayerTemperament.Fiery => RecruitingArchetype.PlayingTimeFirst,
		PlayerTemperament.Coachable => RecruitingArchetype.DevelopmentFirst,
		PlayerTemperament.Easygoing => RecruitingArchetype.StabilitySeeker,
		PlayerTemperament.Intense => RecruitingArchetype.OpportunityNow,
		_ => RecruitingArchetype.Balanced,
	};

	public static double RecruitingBias(
		this PlayerTemperament temperament,
		double reputation,
		double playingTime,
		double facilities,
		double stability,
		double varsity,
		double success) => temperament switch
	{
		PlayerTemperament.Competitive => (reputation * 0.55) + (success * 0.45),
		PlayerTemperament.Vocal => (reputation * 0.40) + (success * 0.60),
		PlayerTemperament.QuietLeader => (facilities * 0.55) + (stability * 0.45),
		PlayerTemperament.Fiery => (playingTime * 0.70) + (varsity * 0.30),
		PlayerTemperament.Coachable => (facilities * 0.75) + (stability * 0.25),
		PlayerTemperament.Easygoing => (stability * 0.70) + (facilities * 0.30),
		PlayerTemperament.Intense => (varsity * 0.65) + (playingTime * 0.35),
		_ => 0,
	};
}
