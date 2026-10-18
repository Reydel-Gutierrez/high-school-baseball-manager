using System;
using System.Collections.Generic;
using HSBM.Application;
using HSBM.Domain;
using HSBM.Simulation;

/// <summary>
/// Hard stops for Advance Day / Simulate Day / Simulate Week.
/// Regular games can be auto-simmed; these cannot be skipped.
/// </summary>
public static class SimulationDesk
{
	public static AdvanceDayInterrupt? InterruptOn(GameSession session) =>
		AdvanceDayGate.Evaluate(Snapshot(session));

	public static bool HasCeremony(GameSession session) =>
		ChampionshipDesk.PendingOn(session) != null
		|| AwardsDesk.PendingOn(session) != null
		|| SeasonReviewDesk.PendingOn(session)
		|| HallOfFameDesk.PendingOn(session)
		|| JobMarketDesk.PendingOn(session)
		|| SeasonRolloverDesk.PendingOn(session);

	public static AdvanceDayGateState Snapshot(GameSession session)
	{
		DateOnly today = session.CurrentDate;
		int year = GameSession.SeasonYear;
		List<Player> roster = session.OrganizationRoster();
		int varsity = 0;
		int jv = 0;
		foreach (Player player in roster)
		{
			if (player.TeamLevel == TeamLevel.Varsity)
			{
				varsity++;
			}
			else
			{
				jv++;
			}
		}

		return new AdvanceDayGateState(
			today,
			year,
			session.TryoutDecisionsPending,
			varsity > LeagueCatalog.TeamRosterCap || jv > LeagueCatalog.TeamRosterCap,
			SeasonCalendar.IsPlayoffWindow(year, today) && StillPlaying(session, today),
			JobOfferDesk.HasPending(session));
	}

	internal static SimulationRecap BuildRecap(GameSession session, DayAdvanceSignals signals, int daysAdvanced)
	{
		var items = new List<SimulationRecapItem>();
		if (daysAdvanced <= 0 && signals.Games.Count == 0 && signals.Transactions.Count == 0)
		{
			return new SimulationRecap(daysAdvanced, signals.StartedOn, session.CurrentDate, items);
		}

		string varsityId = session.VarsityTeam?.Id ?? "";
		string jvId = session.JuniorVarsityTeam?.Id ?? "";
		string orgId = session.CatalogOrganizationId;

		foreach (GameSummary game in signals.Games)
		{
			if (!game.Involves(varsityId) && !game.Involves(jvId))
			{
				continue;
			}

			bool userHome = game.HomeTeamId == varsityId || game.HomeTeamId == jvId;
			string userTeam = userHome ? game.HomeTeamId : game.AwayTeamId;
			string opponentTeam = userHome ? game.AwayTeamId : game.HomeTeamId;
			int us = userHome ? game.HomeScore : game.AwayScore;
			int them = userHome ? game.AwayScore : game.HomeScore;
			bool won = game.WinningTeamId == userTeam;
			string opponent = DistrictHubData.GetTeam(opponentTeam).ShortName;
			if (session.World != null)
			{
				opponent = DistrictHubData.GetTeam(
					CareerCalendar.OrganizationIdForTeam(session.World, opponentTeam)).ShortName;
			}
			string level = game.TeamLevel == TeamLevel.JuniorVarsity ? "JV" : "Varsity";
			string mark = won ? "W" : "L";
			items.Add(new SimulationRecapItem(
				SimulationRecapKind.Game,
				game.Date,
				$"{mark} {us}\u2013{them} {(userHome ? "vs" : "@")} {opponent}",
				$"{level}  ·  {game.Date.ToString("MMM d")}"));
		}

		foreach (PlayerTransaction posted in signals.Transactions)
		{
			if (!string.IsNullOrEmpty(posted.OrganizationId)
				&& !string.Equals(posted.OrganizationId, orgId, StringComparison.Ordinal))
			{
				continue;
			}

			SimulationRecapKind kind = posted.Kind switch
			{
				PlayerTransactionKind.Injury => SimulationRecapKind.Injury,
				PlayerTransactionKind.Graduation => SimulationRecapKind.Graduation,
				PlayerTransactionKind.Tryout or PlayerTransactionKind.OpenPool => SimulationRecapKind.Signing,
				_ => SimulationRecapKind.Transaction,
			};
			items.Add(new SimulationRecapItem(kind, posted.Date, posted.Headline, posted.Detail));
		}

		if (signals.RolledOver)
		{
			items.Add(new SimulationRecapItem(
				SimulationRecapKind.Rollover,
				session.CurrentDate,
				"A new baseball year is on the calendar",
				$"{GameSession.SeasonYear} class, schedule, and records are posted."));
		}

		int cap = 12;
		if (items.Count > cap)
		{
			items.RemoveRange(cap, items.Count - cap);
		}

		return new SimulationRecap(daysAdvanced, signals.StartedOn, session.CurrentDate, items);
	}

	private static bool StillPlaying(GameSession session, DateOnly date)
	{
		if (session.World == null || session.VarsityTeam == null)
		{
			int year = GameSession.SeasonYear;
			if (SeasonCalendar.StateChampionship(year).OccursOn(date))
			{
				return session.WonRegional;
			}

			if (SeasonCalendar.RegionalSemis(year).OccursOn(date)
				|| SeasonCalendar.RegionalFinal(year).OccursOn(date))
			{
				return session.WonDistrict;
			}

			return false;
		}

		return PostseasonDesk.UserHasGameToday(
			new CareerState { World = session.World },
			session.VarsityTeam.Id,
			date);
	}
}

public sealed record SimulationRun(int DaysAdvanced, AdvanceDayInterrupt? Interrupt, SimulationRecap? Recap = null)
{
	public bool Stopped => Interrupt != null;

	public bool HasRecap => Recap != null && Recap.Items.Count > 0;
}

public enum SimulationRecapKind
{
	Game,
	Injury,
	Signing,
	Transaction,
	Graduation,
	Rollover,
}

public sealed record SimulationRecapItem(
	SimulationRecapKind Kind,
	DateOnly Date,
	string Headline,
	string Detail);

public sealed record SimulationRecap(
	int DaysAdvanced,
	DateOnly From,
	DateOnly To,
	IReadOnlyList<SimulationRecapItem> Items)
{
	public string RangeLabel =>
		From == To
			? From.ToString("MMMM d")
			: $"{From.ToString("MMM d")}  –  {To.ToString("MMM d")}";
}
