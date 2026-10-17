namespace HSBM.Domain;

/// <summary>
/// One program plaque. A school inducts a single player each year,
/// usually a just-graduated senior, so careers still hang on the wall.
/// </summary>
public sealed class ProgramHallOfFameInductee
{
	public string OrganizationId { get; set; } = "";
	public int InductedYear { get; set; }
	public Guid? PlayerId { get; set; }
	public string PlayerName { get; set; } = "";
	public string Position { get; set; } = "";
	public int FirstSeasonYear { get; set; }
	public int LastSeasonYear { get; set; }
	public string CareerLine { get; set; } = "";
	public string Honors { get; set; } = "";

	public string YearsLabel => FirstSeasonYear == LastSeasonYear
		? FirstSeasonYear.ToString(System.Globalization.CultureInfo.InvariantCulture)
		: string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{FirstSeasonYear}\u2013{LastSeasonYear}");

	public IReadOnlyList<string> HonorLines => ProgramHallOfFame.SplitHonors(Honors);
}

public static class ProgramHallOfFame
{
	public const char HonorSeparator = '|';

	public static void ReplaceYear(List<ProgramHallOfFameInductee> hall, ProgramHallOfFameInductee inductee)
	{
		hall.RemoveAll(row =>
			row.OrganizationId == inductee.OrganizationId && row.InductedYear == inductee.InductedYear);
		hall.Add(inductee);
		hall.Sort((a, b) => a.InductedYear.CompareTo(b.InductedYear));
	}

	public static string JoinHonors(IEnumerable<string> honors)
	{
		var unique = new List<string>();
		foreach (string honor in honors)
		{
			string trimmed = honor.Trim();
			if (trimmed.Length == 0)
			{
				continue;
			}

			bool seen = false;
			foreach (string existing in unique)
			{
				if (string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase))
				{
					seen = true;
					break;
				}
			}

			if (!seen)
			{
				unique.Add(trimmed);
			}
		}

		return string.Join(HonorSeparator, unique);
	}

	public static IReadOnlyList<string> SplitHonors(string? honors)
	{
		if (string.IsNullOrWhiteSpace(honors))
		{
			return [];
		}

		var lines = new List<string>();
		foreach (string part in honors.Split(HonorSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			lines.Add(part);
		}

		return lines;
	}

	public static string BattingLine(double average) =>
		string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Career {average:.000} AVG");

	public static string PitchingLine(int wins, int losses, double era) =>
		string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Career {wins}\u2013{losses}, {era:0.00} ERA");

	public static int FirstSeasonYear(int lastSeasonYear, int years) =>
		lastSeasonYear - Math.Max(1, years) + 1;

	public static int YearsFromGrade(PlayerGrade grade, int generatedSeasonYear, int lastSeasonYear)
	{
		int fromGrade = 5 - Math.Max(1, grade.RemainingEligibility());
		int fromGenerated = lastSeasonYear - generatedSeasonYear + 1;
		int years = generatedSeasonYear > 0 ? fromGenerated : fromGrade;
		return Math.Clamp(years, 1, 4);
	}
}
