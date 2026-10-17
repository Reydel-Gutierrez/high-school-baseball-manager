namespace HSBM.Domain;

public enum RaceStatus
{
	/// <summary>Still mathematically able to finish top two, and not yet guaranteed.</summary>
	Alive = 0,

	/// <summary>Guaranteed a top-two finish no matter how remaining games go.</summary>
	Clinched = 1,

	/// <summary>Cannot finish top two even by winning out.</summary>
	Eliminated = 2,
}

public static class RaceStatusExtensions
{
	public static string ToLabel(this RaceStatus status) => status switch
	{
		RaceStatus.Clinched => "CLINCHED",
		RaceStatus.Eliminated => "ELIMINATED",
		_ => "IN THE HUNT",
	};
}

/// <summary>
/// Regional-qualification math for one district. Ranking is by total wins
/// (see <see cref="LeagueStandings.Compare"/>), so a team's ceiling is its
/// wins plus every remaining standings game. Tie-breaks are treated
/// conservatively: a tie never clinches and never eliminates.
/// </summary>
public static class QualificationRace
{
	public static Dictionary<string, RaceStatus> For(
		IReadOnlyList<StandingEntry> district,
		IReadOnlyDictionary<string, int> remainingGames,
		int qualifiers = LeagueStandings.QualifiersPerDistrict)
	{
		var result = new Dictionary<string, RaceStatus>(StringComparer.Ordinal);
		bool seasonDone = true;
		foreach (StandingEntry row in district)
		{
			if (Remaining(remainingGames, row.TeamId) > 0)
			{
				seasonDone = false;
				break;
			}
		}

		foreach (StandingEntry team in district)
		{
			if (seasonDone)
			{
				result[team.TeamId] = team.Rank > 0 && team.Rank <= qualifiers ? RaceStatus.Clinched : RaceStatus.Eliminated;
				continue;
			}

			int ceiling = team.Wins + Remaining(remainingGames, team.TeamId);
			int canCatch = 0;
			int alreadyAhead = 0;
			foreach (StandingEntry other in district)
			{
				if (other.TeamId == team.TeamId)
				{
					continue;
				}

				int otherCeiling = other.Wins + Remaining(remainingGames, other.TeamId);
				if (otherCeiling >= team.Wins)
				{
					canCatch++;
				}

				if (other.Wins > ceiling)
				{
					alreadyAhead++;
				}
			}

			if (alreadyAhead >= qualifiers)
			{
				result[team.TeamId] = RaceStatus.Eliminated;
			}
			else if (canCatch < qualifiers)
			{
				result[team.TeamId] = RaceStatus.Clinched;
			}
			else
			{
				result[team.TeamId] = RaceStatus.Alive;
			}
		}

		return result;
	}

	/// <summary>Uncompleted regular-season (standings) games per team id.</summary>
	public static Dictionary<string, int> RemainingGames(GameWorld world, int seasonYear, TeamLevel level)
	{
		var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (ScheduledGame game in world.Games)
		{
			if (game.SeasonYear != seasonYear
				|| game.TeamLevel != level
				|| game.Status == GameStatus.Completed
				|| !game.GameType.CountsInStandings())
			{
				continue;
			}

			remaining[game.HomeTeamId] = remaining.GetValueOrDefault(game.HomeTeamId) + 1;
			remaining[game.AwayTeamId] = remaining.GetValueOrDefault(game.AwayTeamId) + 1;
		}

		return remaining;
	}

	/// <summary>Race status for every team in the league, keyed by team id.</summary>
	public static Dictionary<string, RaceStatus> FromWorld(GameWorld world, int seasonYear, TeamLevel level = TeamLevel.Varsity)
	{
		List<StandingEntry> ranked = LeagueStandings.FromWorld(world, seasonYear, level);
		Dictionary<string, int> remaining = RemainingGames(world, seasonYear, level);
		var result = new Dictionary<string, RaceStatus>(StringComparer.Ordinal);
		foreach (IGrouping<string, StandingEntry> district in ranked.GroupBy(row => row.DistrictId))
		{
			foreach (KeyValuePair<string, RaceStatus> pair in For(district.ToList(), remaining))
			{
				result[pair.Key] = pair.Value;
			}
		}

		return result;
	}

	private static int Remaining(IReadOnlyDictionary<string, int> remaining, string teamId) =>
		remaining.TryGetValue(teamId, out int count) ? count : 0;
}
