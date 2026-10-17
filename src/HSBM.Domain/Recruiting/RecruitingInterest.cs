namespace HSBM.Domain;

/// <summary>
/// A program that is pursuing an incoming player before (or without)
/// putting an offer on paper.
/// </summary>
public sealed class RecruitingInterest
{
	public Guid PlayerId { get; set; }
	public string OrganizationId { get; set; } = "";
	public int SeasonYear { get; set; }
	public DateOnly NotedOn { get; set; }
	public int Heat { get; set; } = 1;

	public static string Format(IReadOnlyList<string> schoolNames)
	{
		if (schoolNames.Count == 0)
		{
			return "No schools on him yet.";
		}

		if (schoolNames.Count == 1)
		{
			return $"{schoolNames[0]} is on him.";
		}

		if (schoolNames.Count == 2)
		{
			return $"{schoolNames[0]} and {schoolNames[1]} are on him.";
		}

		return $"{schoolNames.Count} schools are on him.";
	}

	public static string HeatLabel(int heat) =>
		Math.Clamp(heat, 1, 5) switch
		{
			>= 4 => "Heavy",
			3 => "Serious",
			2 => "Watching",
			_ => "Look",
		};
}

/// <summary>
/// A name the user has pinned for recruiting. Open Play prospects use
/// <see cref="PlayerId"/>; roster watch-list names can stay name-keyed.
/// </summary>
public sealed class RecruitingTarget
{
	public Guid PlayerId { get; set; }
	public string TeamId { get; set; } = "";
	public string PlayerName { get; set; } = "";
	public int SeasonYear { get; set; }

	public bool IsProspect => PlayerId != Guid.Empty;
}
