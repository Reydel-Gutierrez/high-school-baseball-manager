namespace HSBM.Domain;

/// <summary>
/// How a future college athletic director would read a high-school
/// résumé. College UI is not built; this dossier is the hook those
/// programs will call when they start hiring.
/// </summary>
public sealed class CollegeCoachDossier
{
	public int Score { get; init; }
	public int Reputation { get; init; }
	public int HighSchoolSeasons { get; init; }
	public int CareerWins { get; init; }
	public int CareerLosses { get; init; }
	public int ChampionshipCount { get; init; }
	public CompetitionLevel HighestLevel { get; init; } = CompetitionLevel.HighSchool;
	public bool Eligible { get; init; }
	public IReadOnlyList<string> Notes { get; init; } = [];

	public string RecordLabel => $"{CareerWins}\u2013{CareerLosses}";
}

public static class CollegeCoachEligibility
{
	public const int MinimumHighSchoolSeasons = 3;
	public const int MinimumReputation = 55;
	public const int ReachSlack = 10;

	public static CollegeCoachDossier Evaluate(Career career)
	{
		int seasons = Math.Max(0, career.SeasonsCoached);
		int championships = career.DistrictTitles + career.RegionalTitles + career.StateTitles;
		int score = CoachReputation.Clamp(career.Reputation);
		score += Math.Min(18, seasons * 2);
		if (career.CareerGames > 0)
		{
			score += Math.Min(12, career.CareerWins / 12);
			if (career.WinningPercentage >= 0.6)
			{
				score += 6;
			}
		}

		score += Math.Min(8, career.DistrictTitles * 2);
		score += Math.Min(12, career.RegionalTitles * 4);
		score += Math.Min(16, career.StateTitles * 8);
		score = Math.Clamp(score, CoachReputation.Min, CoachReputation.Max);

		var notes = new List<string>();
		bool eligible = true;
		if (seasons < MinimumHighSchoolSeasons)
		{
			eligible = false;
			notes.Add($"Needs {MinimumHighSchoolSeasons - seasons} more high-school season(s).");
		}

		if (career.Reputation < MinimumReputation)
		{
			eligible = false;
			notes.Add("High-school reputation is not yet on the college circuit.");
		}

		if (eligible)
		{
			notes.Add("Eligible for a college board to evaluate.");
		}

		return new CollegeCoachDossier
		{
			Score = score,
			Reputation = CoachReputation.Clamp(career.Reputation),
			HighSchoolSeasons = seasons,
			CareerWins = career.CareerWins,
			CareerLosses = career.CareerLosses,
			ChampionshipCount = championships,
			HighestLevel = career.CompetitionLevel == CompetitionLevel.College
				? CompetitionLevel.College
				: CompetitionLevel.HighSchool,
			Eligible = eligible,
			Notes = notes,
		};
	}

	/// <summary>
	/// Whether a college program of this prestige would take the call.
	/// Prestige uses the same 1–99 band as high-school reputation.
	/// </summary>
	public static bool WouldConsider(Career career, int programPrestige)
	{
		CollegeCoachDossier dossier = Evaluate(career);
		if (!dossier.Eligible)
		{
			return false;
		}

		return programPrestige <= dossier.Score + ReachSlack;
	}
}
