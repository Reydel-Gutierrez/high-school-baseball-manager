namespace HSBM.Domain;

/// <summary>
/// How an incoming player weighs competing schools. Money is always present
/// and never the majority of the decision.
/// </summary>
public enum RecruitingArchetype
{
	Balanced = 0,
	PrestigeSeeker = 1,
	PlayingTimeFirst = 2,
	UnderdogPath = 3,
	StabilitySeeker = 4,
	DevelopmentFirst = 5,
	OpportunityNow = 6,
}

public sealed class RecruitingPriorities
{
	public RecruitingArchetype Archetype { get; init; } = RecruitingArchetype.Balanced;
	public int Reputation { get; init; } = 14;
	public int PlayingTime { get; init; } = 14;
	public int RosterCompetition { get; init; } = 12;
	public int RecentSuccess { get; init; } = 12;
	public int ProgramStability { get; init; } = 12;
	public int VarsityOpportunity { get; init; } = 12;
	public int FinancialOffer { get; init; } = 12;
	public int Facilities { get; init; } = 12;

	public static RecruitingPriorities Balanced { get; } = For(RecruitingArchetype.Balanced);

	public string Hook => Archetype switch
	{
		RecruitingArchetype.PrestigeSeeker => "Wants a program that already wins",
		RecruitingArchetype.PlayingTimeFirst => "Will take a clearer path to the lineup",
		RecruitingArchetype.UnderdogPath => "Prefers a thinner roster over a powerhouse",
		RecruitingArchetype.StabilitySeeker => "Looks for a staff with a name that will stay",
		RecruitingArchetype.DevelopmentFirst => "Cares about location, resources, and how he will be coached",
		RecruitingArchetype.OpportunityNow => "Wants varsity as soon as he is ready",
		_ => "Weighs playing time, prestige, coaching, and sponsorship before he commits",
	};

	public string Label => Archetype switch
	{
		RecruitingArchetype.PrestigeSeeker => "Prestige",
		RecruitingArchetype.PlayingTimeFirst => "Playing time",
		RecruitingArchetype.UnderdogPath => "Roster opportunity",
		RecruitingArchetype.StabilitySeeker => "Coaching",
		RecruitingArchetype.DevelopmentFirst => "Location / resources",
		RecruitingArchetype.OpportunityNow => "Playing time",
		_ => "Balanced",
	};

	public static RecruitingPriorities For(RecruitingArchetype archetype) => archetype switch
	{
		RecruitingArchetype.PrestigeSeeker => new()
		{
			Archetype = archetype,
			Reputation = 28,
			PlayingTime = 10,
			RosterCompetition = 8,
			RecentSuccess = 18,
			ProgramStability = 10,
			VarsityOpportunity = 8,
			FinancialOffer = 8,
			Facilities = 10,
		},
		RecruitingArchetype.PlayingTimeFirst => new()
		{
			Archetype = archetype,
			Reputation = 8,
			PlayingTime = 28,
			RosterCompetition = 18,
			RecentSuccess = 6,
			ProgramStability = 8,
			VarsityOpportunity = 16,
			FinancialOffer = 10,
			Facilities = 6,
		},
		RecruitingArchetype.UnderdogPath => new()
		{
			Archetype = archetype,
			Reputation = 4,
			PlayingTime = 22,
			RosterCompetition = 16,
			RecentSuccess = 4,
			ProgramStability = 12,
			VarsityOpportunity = 18,
			FinancialOffer = 12,
			Facilities = 12,
		},
		RecruitingArchetype.StabilitySeeker => new()
		{
			Archetype = archetype,
			Reputation = 12,
			PlayingTime = 10,
			RosterCompetition = 8,
			RecentSuccess = 10,
			ProgramStability = 28,
			VarsityOpportunity = 10,
			FinancialOffer = 8,
			Facilities = 14,
		},
		RecruitingArchetype.DevelopmentFirst => new()
		{
			Archetype = archetype,
			Reputation = 14,
			PlayingTime = 12,
			RosterCompetition = 10,
			RecentSuccess = 8,
			ProgramStability = 16,
			VarsityOpportunity = 10,
			FinancialOffer = 8,
			Facilities = 22,
		},
		RecruitingArchetype.OpportunityNow => new()
		{
			Archetype = archetype,
			Reputation = 8,
			PlayingTime = 20,
			RosterCompetition = 14,
			RecentSuccess = 6,
			ProgramStability = 8,
			VarsityOpportunity = 30,
			FinancialOffer = 8,
			Facilities = 6,
		},
		_ => new() { Archetype = RecruitingArchetype.Balanced },
	};

	public static RecruitingPriorities Roll(int overall, ISeededRandom rng) =>
		Roll(overall, rng, PlayerTemperament.Composed);

	public static RecruitingPriorities Roll(int overall, ISeededRandom rng, PlayerTemperament personality)
	{
		if (rng.NextDouble() < 0.52)
		{
			return For(personality.PreferredArchetype());
		}

		double roll = rng.NextDouble();
		RecruitingArchetype archetype;
		if (overall >= 70)
		{
			archetype = roll switch
			{
				< 0.42 => RecruitingArchetype.PrestigeSeeker,
				< 0.58 => RecruitingArchetype.OpportunityNow,
				< 0.72 => RecruitingArchetype.Balanced,
				< 0.82 => RecruitingArchetype.DevelopmentFirst,
				< 0.92 => RecruitingArchetype.PlayingTimeFirst,
				_ => RecruitingArchetype.UnderdogPath,
			};
		}
		else if (overall >= 58)
		{
			archetype = roll switch
			{
				< 0.18 => RecruitingArchetype.PrestigeSeeker,
				< 0.36 => RecruitingArchetype.PlayingTimeFirst,
				< 0.50 => RecruitingArchetype.OpportunityNow,
				< 0.64 => RecruitingArchetype.Balanced,
				< 0.78 => RecruitingArchetype.DevelopmentFirst,
				< 0.90 => RecruitingArchetype.StabilitySeeker,
				_ => RecruitingArchetype.UnderdogPath,
			};
		}
		else
		{
			archetype = roll switch
			{
				< 0.08 => RecruitingArchetype.PrestigeSeeker,
				< 0.38 => RecruitingArchetype.PlayingTimeFirst,
				< 0.56 => RecruitingArchetype.UnderdogPath,
				< 0.70 => RecruitingArchetype.OpportunityNow,
				< 0.82 => RecruitingArchetype.Balanced,
				< 0.92 => RecruitingArchetype.StabilitySeeker,
				_ => RecruitingArchetype.DevelopmentFirst,
			};
		}

		return For(archetype);
	}
}
