namespace HSBM.Domain;

public enum ProgramTalentTier
{
	Rebuilding = 0,
	BelowAverage = 1,
	Average = 2,
	AboveAverage = 3,
	Powerhouse = 4,
}

public static class ProgramTalentTierExtensions
{
	/// <summary>
	/// Hidden rating shift applied during career generation. Kept modest so
	/// a powerhouse is favored, not an entire roster of 90+ players.
	/// </summary>
	public static int RatingModifier(this ProgramTalentTier tier) => tier switch
	{
		ProgramTalentTier.Powerhouse => 8,
		ProgramTalentTier.AboveAverage => 4,
		ProgramTalentTier.BelowAverage => -4,
		ProgramTalentTier.Rebuilding => -8,
		_ => 0,
	};

	public static string ToLabel(this ProgramTalentTier tier) => tier switch
	{
		ProgramTalentTier.Powerhouse => "Powerhouse",
		ProgramTalentTier.AboveAverage => "Above Average",
		ProgramTalentTier.BelowAverage => "Below Average",
		ProgramTalentTier.Rebuilding => "Rebuilding",
		_ => "Average",
	};

	public static ProgramTalentTier FromReputation(int reputation) =>
		ProgramReputation.TalentBand(reputation);
}
