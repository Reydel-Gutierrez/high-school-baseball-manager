namespace HSBM.Domain;

/// <summary>
/// One district's Open Play period. Games are simulated in the background;
/// only aggregated lines are kept for scouting.
/// </summary>
public sealed class DistrictOpenPlay
{
	public DistrictOpenPlay(
		string districtId,
		int seasonYear,
		SeasonEvent window,
		IReadOnlyList<Guid> playerIds,
		IReadOnlyList<OpenPlayLine>? lines = null)
	{
		DistrictId = districtId;
		SeasonYear = seasonYear;
		Window = window;
		PlayerIds = playerIds;
		Lines = lines ?? [];
	}

	public string DistrictId { get; }

	public int SeasonYear { get; }

	public SeasonEvent Window { get; }

	public IReadOnlyList<Guid> PlayerIds { get; }

	public IReadOnlyList<OpenPlayLine> Lines { get; set; }

	public bool Contains(Guid playerId)
	{
		foreach (Guid id in PlayerIds)
		{
			if (id == playerId)
			{
				return true;
			}
		}

		return false;
	}

	public OpenPlayLine? LineFor(Guid playerId)
	{
		foreach (OpenPlayLine line in Lines)
		{
			if (line.PlayerId == playerId)
			{
				return line;
			}
		}

		return null;
	}

	/// <summary>
	/// Stats the staff has actually seen. Games run in the background;
	/// looks accumulate across the two-week window.
	/// </summary>
	public OpenPlayLine? VisibleLine(Guid playerId, DateOnly today)
	{
		OpenPlayLine? full = LineFor(playerId);
		if (full == null)
		{
			return null;
		}

		int looks = LooksOn(today);
		if (looks <= 0)
		{
			return null;
		}

		return full.ScaleToLooks(looks);
	}

	public static int LooksOn(DateOnly today, SeasonEvent window)
	{
		if (today < window.Start)
		{
			return 0;
		}

		if (today >= window.End)
		{
			return LeagueCatalog.OpenPlayGamesPerPlayer;
		}

		int days = today.DayNumber - window.Start.DayNumber;
		int looks = 1 + (days / 3);
		return Math.Clamp(looks, 1, LeagueCatalog.OpenPlayGamesPerPlayer);
	}

	public int LooksOn(DateOnly today) => LooksOn(today, Window);

	public bool IsActive(DateOnly today) => Window.OccursOn(today);
}

public sealed class OpenPlayLine
{
	public required Guid PlayerId { get; init; }
	public TryoutHittingLine Hitting { get; init; } = TryoutHittingLine.Empty;
	public TryoutPitchingLine? Pitching { get; init; }
	public LetterGrade Grade { get; init; } = LetterGrade.C;

	public int Games => Math.Max(Hitting.Games, Pitching?.Games ?? 0);

	public bool GradeKnown(int looks) => looks >= 3 && Games > 0;

	public OpenPlayLine ScaleToLooks(int looks)
	{
		int games = Math.Max(1, Games);
		int visible = Math.Clamp(looks, 0, games);
		if (visible <= 0)
		{
			return new OpenPlayLine { PlayerId = PlayerId };
		}

		if (visible >= games)
		{
			return this;
		}

		double ratio = visible / (double)games;
		return new OpenPlayLine
		{
			PlayerId = PlayerId,
			Hitting = Hitting.Scale(ratio, visible),
			Pitching = Pitching?.Scale(ratio, visible),
			Grade = Grade,
		};
	}
}
