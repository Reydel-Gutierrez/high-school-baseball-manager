namespace HSBM.Domain;

/// <summary>
/// Fictional college destinations. No real NCAA schools — placement is
/// driven by present talent and upside so seniors leave with a next step.
/// <see cref="CollegeCommit.CollegeId"/> is the stable key a later college
/// catalog can attach a program to.
/// </summary>
public sealed class CollegeCommit
{
	public string CollegeId { get; init; } = "";
	public required string School { get; init; }
	public required string Path { get; init; }
	public CompetitionLevel CompetitionLevel { get; init; } = CompetitionLevel.College;

	public string Id => string.IsNullOrWhiteSpace(CollegeId) ? CollegeCatalog.IdOf(School) : CollegeId;

	public string Label => School;

	public bool IsAssigned => !string.IsNullOrWhiteSpace(School);
}

public static class CollegeCatalog
{
	private static readonly string[] Power =
	[
		"Lakeshore State",
		"Northbridge University",
		"Gulf Coast University",
		"Iron Range State",
		"Capital Valley University",
	];

	private static readonly string[] MidMajor =
	[
		"Cedar Ridge College",
		"Harborview University",
		"Pinecrest State",
		"Two Rivers College",
		"Sandbar University",
	];

	private static readonly string[] Small =
	[
		"Maplewood College",
		"Cloquet Tech",
		"Vermilion College",
		"Westlake Institute",
	];

	private static readonly string[] Juco =
	[
		"Riverbend College",
		"Westshore JC",
		"Mesabi Valley College",
		"Harbor Point JC",
	];

	public static CollegeCommit Place(string playerName, int overall, int potential)
	{
		int peak = Math.Max(overall, (overall + Math.Max(overall, potential)) / 2);
		int salt = StableHash(playerName);
		if (peak >= 82)
		{
			return Commit(Power[Math.Abs(salt) % Power.Length], "Division I");
		}

		if (peak >= 72)
		{
			return Commit(MidMajor[Math.Abs(salt) % MidMajor.Length], "Division I");
		}

		if (peak >= 64)
		{
			return Commit(Small[Math.Abs(salt) % Small.Length], "Division II");
		}

		return Commit(Juco[Math.Abs(salt) % Juco.Length], "Junior college");
	}

	public static string IdOf(string school)
	{
		if (string.IsNullOrWhiteSpace(school))
		{
			return "";
		}

		var slug = new char[school.Length];
		int n = 0;
		bool dash = false;
		foreach (char c in school.Trim())
		{
			if (char.IsAsciiLetterOrDigit(c))
			{
				slug[n++] = char.ToLowerInvariant(c);
				dash = false;
			}
			else if (n > 0 && !dash)
			{
				slug[n++] = '-';
				dash = true;
			}
		}

		if (n > 0 && slug[n - 1] == '-')
		{
			n--;
		}

		return n == 0 ? "" : new string(slug, 0, n);
	}

	private static CollegeCommit Commit(string school, string path) => new()
	{
		CollegeId = IdOf(school),
		School = school,
		Path = path,
		CompetitionLevel = CompetitionLevel.College,
	};

	private static int StableHash(string value)
	{
		unchecked
		{
			int hash = 17;
			foreach (char c in value)
			{
				hash = (hash * 31) + c;
			}

			return hash;
		}
	}
}
