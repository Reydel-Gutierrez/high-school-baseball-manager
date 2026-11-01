namespace HSBM.Domain;

/// <summary>
/// High school pitch-count limits (MHSAA style): at most <see cref="DailyMax"/>
/// pitches in a day, and required calendar days of rest after an outing.
/// A pitcher who reaches the limit mid at-bat may finish that batter.
/// </summary>
public static class PitchCountRules
{
	public const int DailyMax = 105;

	/// <summary>Look-back window: no outing needs more rest than this.</summary>
	public const int MaxRestDays = 3;

	public static int RestDaysAfter(int pitches) => pitches switch
	{
		<= 30 => 0,
		<= 50 => 1,
		<= 75 => 2,
		_ => 3,
	};

	/// <summary>First date the pitcher may throw again after an outing on <paramref name="outing"/>.</summary>
	public static DateOnly EligibleOn(DateOnly outing, int pitches) =>
		outing.AddDays(RestDaysAfter(pitches) + 1);
}

/// <summary>A pitcher's most recent outing and when he is eligible again.</summary>
public sealed record PitcherWorkload(Guid PlayerId, DateOnly LastOuting, int Pitches)
{
	public int RestDays => PitchCountRules.RestDaysAfter(Pitches);

	public DateOnly EligibleOn => PitchCountRules.EligibleOn(LastOuting, Pitches);

	public bool IsRested(DateOnly date) => date >= EligibleOn;

	/// <summary>Days of rest still owed on <paramref name="date"/> (0 when rested).</summary>
	public int DaysUntilRested(DateOnly date) =>
		Math.Max(0, EligibleOn.DayNumber - date.DayNumber);
}

public static class PitcherWorkloads
{
	/// <summary>
	/// Latest outing for each pitcher on <paramref name="teamId"/> in the rest
	/// window before <paramref name="today"/>, read from saved box scores.
	/// Pitchers with no outing in the window are absent (fully rested).
	/// </summary>
	public static Dictionary<Guid, PitcherWorkload> For(GameWorld world, string teamId, DateOnly today)
	{
		var workloads = new Dictionary<Guid, PitcherWorkload>();
		DateOnly from = today.AddDays(-(PitchCountRules.MaxRestDays + 1));
		foreach (ScheduledGame game in world.Games)
		{
			if (game.Status != GameStatus.Completed
				|| game.Date < from
				|| game.Date >= today
				|| !game.Involves(teamId))
			{
				continue;
			}

			GameBoxScore? box = FindBox(world, game.Id);
			if (box == null)
			{
				continue;
			}

			List<BoxScorePitchingLine> lines = game.HomeTeamId == teamId ? box.HomePitching : box.AwayPitching;
			foreach (BoxScorePitchingLine line in lines)
			{
				if (line.Pitches <= 0)
				{
					continue;
				}

				if (workloads.TryGetValue(line.PlayerId, out PitcherWorkload? seen) && seen.LastOuting >= game.Date)
				{
					// Two games on one day count together toward the rest owed.
					if (seen.LastOuting == game.Date)
					{
						workloads[line.PlayerId] = seen with { Pitches = seen.Pitches + line.Pitches };
					}

					continue;
				}

				workloads[line.PlayerId] = new PitcherWorkload(line.PlayerId, game.Date, line.Pitches);
			}
		}

		return workloads;
	}

	/// <summary>Pitchers on <paramref name="teamId"/> who still owe rest on <paramref name="today"/>.</summary>
	public static HashSet<Guid> Resting(GameWorld world, string teamId, DateOnly today)
	{
		var resting = new HashSet<Guid>();
		foreach (PitcherWorkload workload in For(world, teamId, today).Values)
		{
			if (!workload.IsRested(today))
			{
				resting.Add(workload.PlayerId);
			}
		}

		return resting;
	}

	private static GameBoxScore? FindBox(GameWorld world, Guid gameId)
	{
		foreach (GameBoxScore box in world.BoxScores)
		{
			if (box.GameId == gameId)
			{
				return box;
			}
		}

		return null;
	}
}
