namespace HSBM.Domain;

/// <summary>
/// A former high-school player: identity, compiled career numbers, and
/// the college destination field later catalogs can bind to.
/// </summary>
public sealed class ProgramGraduate
{
	public Guid PlayerId { get; init; }
	public string FirstName { get; init; } = "";
	public string LastName { get; init; } = "";
	public string Position { get; init; } = "";
	public int Overall { get; init; }
	public int GraduatedYear { get; init; }
	public int Seasons { get; init; }
	public string OrganizationId { get; init; } = "";
	public BattingSeasonStats? CareerBatting { get; init; }
	public PitchingSeasonStats? CareerPitching { get; init; }
	public CollegeCommit Destination { get; init; } = new() { School = "", Path = "" };

	public string FullName => $"{FirstName} {LastName}";

	public bool HasDestination => Destination.IsAssigned;
}

/// <summary>
/// Alumni stay on the world player list with their season stat rows.
/// This compiles that history so the graduation desk and later college
/// systems can read a closed high-school career.
/// </summary>
public static class GraduateHistory
{
	public static IReadOnlyList<ProgramGraduate> For(
		IReadOnlyList<Player> players,
		IReadOnlyList<BattingSeasonStats> batting,
		IReadOnlyList<PitchingSeasonStats> pitching,
		string organizationId,
		int? classYear = null)
	{
		var graduates = new List<ProgramGraduate>();
		foreach (Player player in players)
		{
			if (!player.Alumni
				|| !string.Equals(player.OrganizationId, organizationId, StringComparison.Ordinal))
			{
				continue;
			}

			if (classYear is int year && player.GraduatedYear != year)
			{
				continue;
			}

			graduates.Add(Compile(player, batting, pitching));
		}

		graduates.Sort((a, b) =>
		{
			int year = b.GraduatedYear.CompareTo(a.GraduatedYear);
			return year != 0 ? year : b.Overall.CompareTo(a.Overall);
		});
		return graduates;
	}

	public static IReadOnlyList<ProgramGraduate> Upcoming(
		IReadOnlyList<Player> players,
		IReadOnlyList<BattingSeasonStats> batting,
		IReadOnlyList<PitchingSeasonStats> pitching,
		string organizationId)
	{
		var seniors = new List<ProgramGraduate>();
		foreach (Player player in players)
		{
			if (player.Alumni
				|| player.IsUnsigned
				|| player.Grade != PlayerGrade.Senior
				|| !string.Equals(player.OrganizationId, organizationId, StringComparison.Ordinal))
			{
				continue;
			}

			seniors.Add(Compile(player, batting, pitching));
		}

		seniors.Sort((a, b) => b.Overall.CompareTo(a.Overall));
		return seniors;
	}

	public static ProgramGraduate Compile(
		Player player,
		IReadOnlyList<BattingSeasonStats> batting,
		IReadOnlyList<PitchingSeasonStats> pitching)
	{
		BattingSeasonStats? careerBatting = SumBatting(player.Id, batting);
		PitchingSeasonStats? careerPitching = SumPitching(player.Id, pitching);
		int seasons = CountSeasons(player.Id, batting, pitching);
		if (seasons < 1)
		{
			seasons = Math.Max(1, (int)player.Grade - 8);
		}

		return new ProgramGraduate
		{
			PlayerId = player.Id,
			FirstName = player.FirstName,
			LastName = player.LastName,
			Position = player.DisplayPosition,
			Overall = player.Overall,
			GraduatedYear = player.GraduatedYear,
			Seasons = seasons,
			OrganizationId = player.OrganizationId,
			CareerBatting = careerBatting is { AtBats: > 0 } ? careerBatting : null,
			CareerPitching = HasPitchingLine(careerPitching) ? careerPitching : null,
			Destination = player.CollegeDestination ?? CollegeCatalog.Place(player.FullName, player.Overall, player.Potential),
		};
	}

	private static bool HasPitchingLine(PitchingSeasonStats? row) =>
		row != null && (row.Games > 0 || row.OutsRecorded > 0 || row.Wins > 0);

	private static int CountSeasons(
		Guid playerId,
		IReadOnlyList<BattingSeasonStats> batting,
		IReadOnlyList<PitchingSeasonStats> pitching)
	{
		var years = new HashSet<int>();
		foreach (BattingSeasonStats row in batting)
		{
			if (row.PlayerId == playerId)
			{
				years.Add(row.SeasonYear);
			}
		}

		foreach (PitchingSeasonStats row in pitching)
		{
			if (row.PlayerId == playerId)
			{
				years.Add(row.SeasonYear);
			}
		}

		return years.Count;
	}

	private static BattingSeasonStats? SumBatting(Guid playerId, IReadOnlyList<BattingSeasonStats> rows)
	{
		BattingSeasonStats? total = null;
		foreach (BattingSeasonStats row in rows)
		{
			if (row.PlayerId != playerId)
			{
				continue;
			}

			total ??= new BattingSeasonStats { PlayerId = playerId, TeamId = row.TeamId };
			total.Add(row);
		}

		return total;
	}

	private static PitchingSeasonStats? SumPitching(Guid playerId, IReadOnlyList<PitchingSeasonStats> rows)
	{
		PitchingSeasonStats? total = null;
		foreach (PitchingSeasonStats row in rows)
		{
			if (row.PlayerId != playerId)
			{
				continue;
			}

			total ??= new PitchingSeasonStats { PlayerId = playerId, TeamId = row.TeamId };
			total.Add(row);
		}

		return total;
	}
}
