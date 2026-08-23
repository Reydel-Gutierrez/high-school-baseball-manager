using System;
using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// Prototype box scores for completed clubhouse games. Final runs come
/// from the schedule; innings, batting, and pitching are derived so the
/// desk can open a full sheet before live sim results exist.
/// </summary>
public static class ClubhouseBoxScore
{
	public const int Innings = 7;

	public static BoxScore For(CalendarContest game)
	{
		HubTeam us = DistrictHubData.GetTeam(DistrictHubData.UserTeamId);
		HubTeam them = DistrictHubData.GetTeam(game.OpponentId);
		int usRuns = game.TeamScore ?? 0;
		int themRuns = game.OpponentScore ?? 0;
		var rng = new Random(Seed(game));

		BoxScoreSide away = BuildSide(
			game.Home ? them : us,
			game.Home ? themRuns : usRuns,
			game.Level,
			rng,
			isHome: false);
		BoxScoreSide home = BuildSide(
			game.Home ? us : them,
			game.Home ? usRuns : themRuns,
			game.Level,
			rng,
			isHome: true);

		bool homeWon = home.Runs > away.Runs;
		bool skippedLast = homeWon && home.InningRuns[Innings - 1] == 0 && Sum(home.InningRuns, Innings - 1) > away.Runs;
		home = home with { SkippedLast = skippedLast };

		BoxScorePitcher winner = DecisionPitcher(homeWon ? home : away, won: true);
		BoxScorePitcher loser = DecisionPitcher(homeWon ? away : home, won: false);
		BoxScorePitcher? save = SavePitcher(homeWon ? home : away, winner);

		return new BoxScore(
			game,
			away,
			home,
			winner.Name,
			loser.Name,
			save?.Name,
			FieldingPlays(homeWon ? home : away, rng));
	}

	private static BoxScoreSide BuildSide(HubTeam team, int runs, TeamLevel level, Random rng, bool isHome)
	{
		int[] innings = SplitRuns(runs, rng);
		int hits = Math.Max(runs + 1, runs + rng.Next(2, 6));
		int errors = rng.Next(0, isHome ? 2 : 3);
		List<BoxScoreBatter> batters = BuildBatters(team.Id, level, runs, hits, rng);
		List<BoxScorePitcher> pitchers = BuildPitchers(team.Id, level, hits, rng);
		return new BoxScoreSide(team.Id, team.Name, team.ShortName, innings, runs, hits, errors, false, batters, pitchers);
	}

	private static List<BoxScoreBatter> BuildBatters(string teamId, TeamLevel level, int runs, int hits, Random rng)
	{
		var lineup = new List<HubSquadPlayer>();
		foreach (HubSquadPlayer player in DistrictHubSquads.Hitters(teamId, level))
		{
			lineup.Add(player);
			if (lineup.Count == 9)
			{
				break;
			}
		}

		int[] allocatedHits = SplitCount(hits, lineup.Count, rng, cap: 4);
		int[] allocatedRuns = SplitCount(runs, lineup.Count, rng, cap: 3);
		int[] allocatedRbi = SplitCount(runs, lineup.Count, rng, cap: 4);
		int homers = rng.Next(0, Math.Min(3, Math.Max(0, runs - 1)) + 1);
		int[] allocatedHr = SplitCount(homers, lineup.Count, rng, cap: 2);
		var batters = new List<BoxScoreBatter>();
		for (int i = 0; i < lineup.Count; i++)
		{
			HubSquadPlayer player = lineup[i];
			int h = allocatedHits[i];
			int hr = Math.Min(allocatedHr[i], h);
			int ab = Math.Max(h + rng.Next(1, 3), 3);
			if (ab > 5)
			{
				ab = 5;
			}

			batters.Add(new BoxScoreBatter(
				player.Name,
				player.LastName.ToUpperInvariant(),
				Slot(player.Position),
				ab,
				allocatedRuns[i],
				h,
				hr,
				allocatedRbi[i]));
		}

		return batters;
	}

	private static List<BoxScorePitcher> BuildPitchers(string teamId, TeamLevel level, int hitsAllowed, Random rng)
	{
		var arms = DistrictHubSquads.Arms(teamId, level);
		HubSquadPlayer starter = arms.Count > 0 ? arms[0] : FallbackArm(teamId);
		HubSquadPlayer? reliever = arms.Count > 1 ? arms[1] : null;

		int starterOuts = 12 + rng.Next(0, 9);
		if (starterOuts > 20)
		{
			starterOuts = 20;
		}

		int rest = 21 - starterOuts;
		int starterHits = Math.Max(0, hitsAllowed - rng.Next(0, 4));
		if (reliever == null || rest <= 0)
		{
			starterOuts = 21;
			return
			[
				new BoxScorePitcher(starter.Name, starter.LastName.ToUpperInvariant(), FormatIp(21), hitsAllowed, rng.Next(0, 3), 4 + rng.Next(0, 6)),
			];
		}

		int relieverHits = Math.Max(0, hitsAllowed - starterHits);
		return
		[
			new BoxScorePitcher(starter.Name, starter.LastName.ToUpperInvariant(), FormatIp(starterOuts), starterHits, rng.Next(0, 3), 3 + rng.Next(0, 6)),
			new BoxScorePitcher(reliever.Name, reliever.LastName.ToUpperInvariant(), FormatIp(rest), relieverHits, rng.Next(0, 2), rng.Next(0, 4)),
		];
	}

	private static BoxScorePitcher DecisionPitcher(BoxScoreSide side, bool won)
	{
		if (won && side.Pitchers.Count > 1 && side.Pitchers[0].Outs < 15)
		{
			return side.Pitchers[1];
		}

		return side.Pitchers[0];
	}

	private static BoxScorePitcher? SavePitcher(BoxScoreSide winning, BoxScorePitcher winner)
	{
		if (winning.Pitchers.Count < 2)
		{
			return null;
		}

		BoxScorePitcher last = winning.Pitchers[^1];
		return last.Name == winner.Name ? null : last;
	}

	private static List<string> FieldingPlays(BoxScoreSide side, Random rng)
	{
		string infielder = side.Batters.Count > 2 ? side.Batters[2].LastName : "HALE";
		string catcher = side.Batters.Count > 1 ? side.Batters[1].LastName : "CRUZ";
		string first = side.Batters.Count > 3 ? side.Batters[3].LastName : "COLE";
		string second = side.Batters.Count > 5 ? side.Batters[5].LastName : "WHITAKER";
		var plays = new List<string>
		{
			$"{infielder} to {second} to {first} \u2014 6-4-3 double play",
			$"{catcher} caught stealing, runner thrown out at second",
		};
		if (rng.Next(0, 2) == 0)
		{
			plays.Add($"E{rng.Next(3, 7)}: throwing error, {infielder}");
		}

		return plays;
	}

	private static int[] SplitRuns(int total, Random rng)
	{
		var innings = new int[Innings];
		int left = total;
		int cursor = 0;
		while (left > 0)
		{
			int inning = cursor % Innings;
			int add = Math.Min(left, rng.Next(1, 4));
			if (innings[inning] + add > 5)
			{
				add = 1;
			}

			innings[inning] += add;
			left -= add;
			cursor++;
			if (cursor > 40)
			{
				innings[0] += left;
				break;
			}
		}

		return innings;
	}

	private static int[] SplitCount(int total, int slots, Random rng, int cap)
	{
		var values = new int[Math.Max(slots, 1)];
		if (slots <= 0 || total <= 0)
		{
			return values;
		}

		int left = total;
		int i = 0;
		while (left > 0)
		{
			int index = i % slots;
			if (values[index] < cap)
			{
				values[index]++;
				left--;
			}

			i++;
			if (i > slots * cap + 8)
			{
				break;
			}
		}

		if (rng.Next(0, 2) == 0 && slots > 2)
		{
			int a = rng.Next(0, slots);
			int b = rng.Next(0, slots);
			(values[a], values[b]) = (values[b], values[a]);
		}

		return values;
	}

	private static HubSquadPlayer FallbackArm(string teamId)
	{
		HubTeam team = DistrictHubData.GetTeam(teamId);
		return new HubSquadPlayer(
			"Staff",
			"Ace",
			1,
			"RHP",
			"SR",
			"R/R",
			70,
			40,
			40,
			40,
			70,
			40,
			"Healthy",
			null,
			team.Id,
			team.ShortName,
			"Varsity",
			[]);
	}

	private static string Slot(string position)
	{
		int slash = position.IndexOf('/');
		string primary = slash > 0 ? position[..slash] : position;
		return primary is "RHP" or "LHP" ? "DH" : primary;
	}

	private static string FormatIp(int outs) => $"{outs / 3}.{outs % 3}";

	private static int Sum(int[] values, int length)
	{
		int total = 0;
		int cap = Math.Min(length, values.Length);
		for (int i = 0; i < cap; i++)
		{
			total += values[i];
		}

		return total;
	}

	private static int Seed(CalendarContest game)
	{
		int hash = 17;
		hash = hash * 31 + game.Date.DayNumber;
		hash = hash * 31 + game.OpponentId.GetHashCode(StringComparison.Ordinal);
		hash = hash * 31 + (int)game.Level;
		hash = hash * 31 + (game.TeamScore ?? 0);
		hash = hash * 31 + (game.OpponentScore ?? 0);
		return hash;
	}
}

public sealed record BoxScore(
	CalendarContest Game,
	BoxScoreSide Away,
	BoxScoreSide Home,
	string WinningPitcher,
	string LosingPitcher,
	string? Save,
	IReadOnlyList<string> FieldingPlays);

public sealed record BoxScoreSide(
	string TeamId,
	string Name,
	string ShortName,
	int[] InningRuns,
	int Runs,
	int Hits,
	int Errors,
	bool SkippedLast,
	IReadOnlyList<BoxScoreBatter> Batters,
	IReadOnlyList<BoxScorePitcher> Pitchers);

public sealed record BoxScoreBatter(string Name, string LastName, string Position, int AtBats, int Runs, int Hits, int HomeRuns, int Rbi);

public sealed record BoxScorePitcher(string Name, string LastName, string Innings, int Hits, int Walks, int Strikeouts)
{
	public int Outs
	{
		get
		{
			string[] parts = Innings.Split('.');
			int whole = parts.Length > 0 && int.TryParse(parts[0], out int innings) ? innings : 0;
			int extra = parts.Length > 1 && int.TryParse(parts[1], out int remainder) ? remainder : 0;
			return whole * 3 + extra;
		}
	}
}
