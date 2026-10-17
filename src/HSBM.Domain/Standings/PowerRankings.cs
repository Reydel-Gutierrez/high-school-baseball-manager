using System.Globalization;

namespace HSBM.Domain;

/// <summary>
/// Statewide poll: record, opponent strength, and program reputation.
/// Early in the season reputation still carries the board; by eight
/// games the won-lost and the slate take over.
/// </summary>
public static class PowerRankings
{
	public const int MinGamesForFullWeight = 8;
	public const int NonDistrictGames = 8;
	private const double Laplace = 2.0;
	private const double RunDiffScale = 60.0;

	public static IReadOnlyList<PowerRankingRow> Rank(IReadOnlyList<PowerRankingEntry> teams)
	{
		List<PowerRankingRow> current = RankSnapshot(teams, previous: false);
		List<PowerRankingRow> prior = RankSnapshot(teams, previous: true);
		var previousById = new Dictionary<string, int>(prior.Count, StringComparer.Ordinal);
		foreach (PowerRankingRow row in prior)
		{
			previousById[row.OrganizationId] = row.Rank;
		}

		var ranked = new List<PowerRankingRow>(current.Count);
		foreach (PowerRankingRow row in current)
		{
			int previousRank = previousById.TryGetValue(row.OrganizationId, out int found)
				? found
				: row.Rank;
			ranked.Add(row with
			{
				PreviousRank = previousRank,
				Movement = previousRank - row.Rank,
			});
		}

		return ranked;
	}

	private static List<PowerRankingRow> RankSnapshot(
		IReadOnlyList<PowerRankingEntry> teams,
		bool previous)
	{
		var records = new Dictionary<string, (int Wins, int Losses, int RunsFor, int RunsAgainst)>(
			teams.Count,
			StringComparer.Ordinal);
		foreach (PowerRankingEntry team in teams)
		{
			(int wins, int losses) = previous
				? WithoutCurrentStreak(team.Wins, team.Losses, team.Streak)
				: (team.Wins, team.Losses);
			records[team.OrganizationId] = (wins, losses, team.RunsFor, team.RunsAgainst);
		}

		var scored = new List<(PowerRankingEntry Team, int Wins, int Losses, double Sos, double Score)>(teams.Count);
		foreach (PowerRankingEntry team in teams)
		{
			(int wins, int losses, int runsFor, int runsAgainst) = records[team.OrganizationId];
			double record = SmoothedWinPct(wins, losses);
			double sos = StrengthOfSchedule(team, records);
			double reputation = Math.Clamp(team.Reputation, 0, 100) / 100.0;
			double runDiff = 0.5 + (0.5 * Math.Tanh((runsFor - runsAgainst) / RunDiffScale));
			int games = wins + losses;
			double blend = Math.Clamp(games / (double)MinGamesForFullWeight, 0, 1);
			double wRecord = Lerp(0.22, 0.50, blend);
			double wSos = Lerp(0.18, 0.28, blend);
			double wRep = Lerp(0.52, 0.14, blend);
			double wDiff = 0.08;
			double score = (100.0 * ((wRecord * record) + (wSos * sos) + (wRep * reputation) + (wDiff * runDiff)));
			scored.Add((team, wins, losses, sos, score));
		}

		scored.Sort((a, b) =>
		{
			int scoreCmp = b.Score.CompareTo(a.Score);
			if (scoreCmp != 0)
			{
				return scoreCmp;
			}

			int winCmp = b.Wins.CompareTo(a.Wins);
			if (winCmp != 0)
			{
				return winCmp;
			}

			int lossCmp = a.Losses.CompareTo(b.Losses);
			if (lossCmp != 0)
			{
				return lossCmp;
			}

			int rdCmp = (b.Team.RunsFor - b.Team.RunsAgainst).CompareTo(a.Team.RunsFor - a.Team.RunsAgainst);
			if (rdCmp != 0)
			{
				return rdCmp;
			}

			return string.CompareOrdinal(a.Team.Name, b.Team.Name);
		});

		var rows = new List<PowerRankingRow>(scored.Count);
		for (int i = 0; i < scored.Count; i++)
		{
			(PowerRankingEntry team, int wins, int losses, double sos, double score) = scored[i];
			rows.Add(new PowerRankingRow(
				i + 1,
				i + 1,
				0,
				team.OrganizationId,
				team.Name,
				team.DistrictId,
				team.DistrictName,
				wins,
				losses,
				team.Reputation,
				score,
				sos));
		}

		return rows;
	}

	public static (int Wins, int Losses) WithoutCurrentStreak(int wins, int losses, int streak)
	{
		if (streak > 0)
		{
			return (Math.Max(0, wins - streak), losses);
		}

		if (streak < 0)
		{
			return (wins, Math.Max(0, losses + streak));
		}

		return (wins, losses);
	}

	private static double StrengthOfSchedule(
		PowerRankingEntry team,
		Dictionary<string, (int Wins, int Losses, int RunsFor, int RunsAgainst)> records)
	{
		if (team.OpponentIds.Count == 0)
		{
			return 0.5;
		}

		double sum = 0;
		int count = 0;
		foreach (string opponentId in team.OpponentIds)
		{
			if (!records.TryGetValue(opponentId, out (int Wins, int Losses, int RunsFor, int RunsAgainst) record))
			{
				sum += 0.5;
				count++;
				continue;
			}

			sum += SmoothedWinPct(record.Wins, record.Losses);
			count++;
		}

		return count == 0 ? 0.5 : sum / count;
	}

	private static double SmoothedWinPct(int wins, int losses)
	{
		int games = wins + losses;
		if (games <= 0)
		{
			return 0.5;
		}

		return (wins + Laplace) / (games + (2.0 * Laplace));
	}

	private static double Lerp(double from, double to, double t) => from + ((to - from) * t);
}

public sealed record PowerRankingEntry(
	string OrganizationId,
	string Name,
	string DistrictId,
	string DistrictName,
	int Wins,
	int Losses,
	int RunsFor,
	int RunsAgainst,
	int Reputation,
	int Streak,
	IReadOnlyList<string> OpponentIds);

public sealed record PowerRankingRow(
	int Rank,
	int PreviousRank,
	int Movement,
	string OrganizationId,
	string Name,
	string DistrictId,
	string DistrictName,
	int Wins,
	int Losses,
	int Reputation,
	double Score,
	double StrengthOfSchedule)
{
	public string RecordLabel => $"{Wins}\u2013{Losses}";

	public string MovementLabel => Movement > 0
		? "\u2191"
		: Movement < 0
			? "\u2193"
			: "\u2014";

	public string SosLabel => StrengthOfSchedule.ToString(".000", CultureInfo.InvariantCulture);
}
