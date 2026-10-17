using HSBM.Domain;

namespace HSBM.Application;

public enum SimulationDetail
{
	Standard = 0,
}

public static class WorldSetup
{
	public const int MaxWorldNameLength = 40;
	public const string AutoSeedLabel = "AUTO";

	public static string SuggestWorldName(string firstName, int existingCareerCount)
	{
		int number = Math.Max(1, existingCareerCount + 1);
		string prefix = string.IsNullOrWhiteSpace(firstName) ? "Career" : firstName.Trim();
		return $"{prefix} Career {number:00}";
	}

	public static string SimulationDetailLabel(SimulationDetail detail) => detail switch
	{
		_ => "Standard",
	};

	public static bool TryParseSeed(string text, out int? seed)
	{
		string trimmed = text.Trim();
		if (trimmed.Length == 0 || trimmed.Equals(AutoSeedLabel, StringComparison.OrdinalIgnoreCase))
		{
			seed = null;
			return true;
		}

		if (int.TryParse(trimmed, out int value))
		{
			seed = value;
			return true;
		}

		seed = null;
		return false;
	}

	public static int ResolveSeed(int? seed, Guid coachId) =>
		seed ?? SeededRandom.Mix(unchecked((int)0xA10B411), coachId);

	public static string? ValidateWorldName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "World name is required.";
		}

		if (name.Trim().Length > MaxWorldNameLength)
		{
			return "World name is too long.";
		}

		return null;
	}
}
