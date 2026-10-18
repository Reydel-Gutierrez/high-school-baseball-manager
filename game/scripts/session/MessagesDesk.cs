using System;
using System.Collections.Generic;
using HSBM.Domain;
using HSBM.Simulation;

/// <summary>
/// Coach inbox: live items that need attention before the next day,
/// separate from the league news wire. Click-through is handled by
/// <see cref="AppNavigator.OpenInboxItem"/>.
/// </summary>
public static class MessagesDesk
{
	public static IReadOnlyList<InboxMessage> For(GameSession session)
	{
		var items = new List<InboxMessage>();
		CollectPendingGates(items, session);
		CollectGameDay(items, session);
		CollectTryouts(items, session);
		CollectRecentDecisions(items, session);
		CollectScouting(items, session);
		CollectBoard(items, session);
		CollectPlayers(items, session);
		CollectCareer(items, session);
		CollectCalendar(items, session);
		items.Sort(Compare);
		return items;
	}

	public static int UnreadCount(GameSession session)
	{
		int count = 0;
		foreach (InboxMessage item in For(session))
		{
			if (item.Unread)
			{
				count++;
			}
		}

		return count;
	}

	public static InboxMessage? BannerOn(GameSession session, InboxDestination? skip = null)
	{
		foreach (InboxMessage item in For(session))
		{
			if (skip != null && item.Destination == skip.Value)
			{
				continue;
			}

			if (item.Unread)
			{
				return item;
			}
		}

		return null;
	}

	private static void CollectPendingGates(List<InboxMessage> items, GameSession session)
	{
		ChampionshipTitle? title = ChampionshipDesk.PendingOn(session);
		if (title != null)
		{
			string name = title.Value switch
			{
				ChampionshipTitle.District => "district championship",
				ChampionshipTitle.Regional => "regional championship",
				ChampionshipTitle.State => "state championship",
				_ => "championship",
			};
			Add(
				items,
				session,
				"gate-championship",
				InboxChannel.Urgent,
				0,
				$"{session.Organization.ShortName} won the {name}",
				"The celebration desk is waiting before you can advance the day.",
				$"The {name} is on the shelf. Walk through the trophy presentation before the calendar moves.",
				InboxDestination.Championship,
				"OPEN CELEBRATION",
				championship: title);
		}

		AwardScope? banquet = AwardsDesk.PendingOn(session);
		if (banquet != null)
		{
			string scope = banquet.Value == AwardScope.State ? "State" : "District";
			Add(
				items,
				session,
				"gate-awards",
				InboxChannel.Urgent,
				1,
				$"{scope} awards ceremony is ready",
				"MVP, pitcher, freshman, all-league, and coach of the year.",
				$"The {scope.ToLowerInvariant()} banquet is on the calendar. The honors are printed. Sit through the ceremony before you advance.",
				InboxDestination.Awards,
				"OPEN CEREMONY",
				award: banquet);
		}

		if (SeasonReviewDesk.PendingOn(session))
		{
			Add(
				items,
				session,
				"gate-review",
				InboxChannel.Board,
				2,
				"Board season review is on your desk",
				"Final grade, budget, and whether the seat is safe.",
				"The athletic board has the year-end card ready: record, district finish, contract target, and what it does to satisfaction and the budget.",
				InboxDestination.SeasonReview,
				"OPEN REVIEW");
		}

		if (HallOfFameDesk.PendingOn(session))
		{
			ProgramHallOfFameInductee? inductee = HallOfFameDesk.ThisYear(session);
			string name = inductee?.PlayerName ?? "a program player";
			Add(
				items,
				session,
				"gate-hof",
				InboxChannel.Player,
				3,
				$"{name} is going on the wall",
				"Program Hall of Fame induction is waiting.",
				$"{name} is this year's inductee. The plaque goes up after board review so the career still matters after graduation.",
				InboxDestination.HallOfFame,
				"OPEN INDUCTION",
				playerName: inductee?.PlayerName);
		}

		if (SeasonRolloverDesk.PendingOn(session))
		{
			Add(
				items,
				session,
				"gate-rollover",
				InboxChannel.Career,
				2,
				"The baseball year is ready to turn",
				"Graduates, awards, and the final record before January 5.",
				"Walk through last season's class, honors, and career developments before the new schedule posts.",
				InboxDestination.SeasonRollover,
				"OPEN YEAR IN REVIEW");
		}
	}

	private static void CollectGameDay(List<InboxMessage> items, GameSession session)
	{
		CalendarContest? game = PrimaryGame(session);
		if (game == null || game.Completed)
		{
			return;
		}

		HubTeam opponent = DistrictHubData.GetTeam(game.OpponentId);
		string venue = game.Home ? "at home" : "on the road";
		Add(
			items,
			session,
			$"game-{game.Date:yyyyMMdd}-{game.OpponentId}",
			InboxChannel.Urgent,
			5,
			$"Game day — {opponent.ShortName} {venue}",
			$"{game.Time}  ·  {game.Level.ToLabel().ToUpperInvariant()}",
			$"{session.Organization.ShortName} plays {opponent.Name} {venue} today. Lineup and rotation should be set before first pitch.",
			InboxDestination.GamePreview,
			"OPEN PREVIEW",
			game: game);
	}

	private static void CollectTryouts(List<InboxMessage> items, GameSession session)
	{
		int year = GameSession.SeasonYear;
		DateOnly date = session.CurrentDate;
		DateOnly tomorrow = date.AddDays(1);
		SeasonEvent week = SeasonCalendar.TryoutWeek(year);
		DateOnly decision = SeasonCalendar.TryoutDecisionDay(year);
		IReadOnlyList<Player> candidates = session.TryoutCandidates;
		if (candidates.Count == 0 || date > decision)
		{
			return;
		}

		int pending = 0;
		foreach (Player player in candidates)
		{
			if (player.TryoutCall == TryoutCall.Pending)
			{
				pending++;
			}
		}

		if (date < week.Start)
		{
			Add(
				items,
				session,
				$"tryout-signups-{year}",
				InboxChannel.Scouting,
				8,
				$"{candidates.Count} students signed up for tryouts",
				$"Tryout week runs {week.FormatDates()}.",
				"Mostly freshmen, plus a few older kids and transfers. All you have is a first look — grade, position, size. You'll see them play during tryout week.",
				InboxDestination.Tryouts,
				"OPEN TRYOUTS");
			return;
		}

		if (week.OccursOn(date))
		{
			int day = TryoutEvaluation.DaysSeen(year, date);
			Add(
				items,
				session,
				$"tryout-day-{year}-{day}",
				day >= TryoutEvaluation.TryoutDays - 1 ? InboxChannel.Urgent : InboxChannel.Scouting,
				day >= TryoutEvaluation.TryoutDays - 1 ? 6 : 8,
				$"Tryouts, day {day} of {TryoutEvaluation.TryoutDays}",
				pending > 0 ? $"{pending} kids still undecided." : "Every candidate has a call.",
				"Your staff's read on every kid narrows each day. The assistant coach's suggestions get sharper too.",
				InboxDestination.Tryouts,
				"OPEN TRYOUTS");
		}

		if (date == decision || tomorrow == decision)
		{
			string when = date == decision ? "today" : "tomorrow";
			Add(
				items,
				session,
				$"tryout-decision-{year}",
				InboxChannel.Urgent,
				5,
				$"Decision Day is {when}",
				pending > 0 ? $"{pending} kids still undecided." : "Your list is ready to post.",
				"Varsity, JV, or cut. Anyone you leave undecided, your assistant decides. If you call more kids up than you have spots, the weakest calls get bumped down a level or cut.",
				InboxDestination.Tryouts,
				"OPEN TRYOUTS");
		}
	}

	private static void CollectRecentDecisions(List<InboxMessage> items, GameSession session)
	{
		DateOnly cutoff = session.CurrentDate.AddDays(-3);
		string orgId = session.CatalogOrganizationId;
		int signings = 0;
		foreach (PlayerTransaction posted in session.Transactions)
		{
			if (posted.Date < cutoff
				|| (!string.IsNullOrEmpty(posted.OrganizationId)
					&& !string.Equals(posted.OrganizationId, orgId, StringComparison.Ordinal)))
			{
				continue;
			}

			if (posted.Kind == PlayerTransactionKind.Tryout)
			{
				signings++;
				Add(
					items,
					session,
					$"decision-{posted.DedupKey}",
					InboxChannel.Player,
					8,
					posted.Headline,
					posted.Detail,
					$"{posted.Headline} He has a jersey and a spot on the {posted.Detail} roster.",
					InboxDestination.Roster,
					"OPEN ROSTER");
			}
			else if (posted.Kind == PlayerTransactionKind.Release)
			{
				Add(
					items,
					session,
					$"decision-{posted.DedupKey}",
					InboxChannel.Board,
					9,
					posted.Headline,
					posted.Detail,
					posted.Headline,
					InboxDestination.Transactions,
					"VIEW TRANSACTIONS");
			}
		}

		_ = signings;
	}

	private static void CollectScouting(List<InboxMessage> items, GameSession session)
	{
		int year = GameSession.SeasonYear;
		DateOnly date = session.CurrentDate;
		SeasonEvent fall = SeasonCalendar.FallScouting(year);
		SeasonEvent evaluation = SeasonCalendar.FallEvaluation(year);

		if (fall.OccursOn(date) || evaluation.OccursOn(date))
		{
			Add(
				items,
				session,
				"scout-fall",
				InboxChannel.Scouting,
				11,
				evaluation.OccursOn(date) ? "Fall evaluation is open" : "Fall scouting is open",
				"Get a look at district opponents before next spring.",
				evaluation.OccursOn(date) ? evaluation.Summary : fall.Summary,
				InboxDestination.PlayerSearch,
				"OPEN PLAYER SEARCH");
		}

		if (session.ScoutTargets.Count > 0)
		{
			ScoutTarget first = session.ScoutTargets[0];
			string extra = session.ScoutTargets.Count == 1
				? first.PlayerName
				: $"{session.ScoutTargets.Count} names on the watch list";
			Add(
				items,
				session,
				"scout-targets",
				InboxChannel.Scouting,
				13,
				session.ScoutTargets.Count == 1
					? $"{first.PlayerName} is on the watch list"
					: $"{session.ScoutTargets.Count} names on the watch list",
				extra,
				"Targets stay pinned until you take them off. Open the list to keep working the names.",
				InboxDestination.Targets,
				"VIEW TARGETS",
				teamId: first.TeamId,
				playerName: first.PlayerName);
		}
	}

	private static void CollectBoard(List<InboxMessage> items, GameSession session)
	{
		int year = GameSession.SeasonYear;
		DateOnly date = session.CurrentDate;
		SeasonEvent midseason = SeasonCalendar.MidseasonReview(year);
		if (midseason.OccursOn(date) || midseason.Start == date.AddDays(1))
		{
			string when = midseason.OccursOn(date) ? "available" : "tomorrow";
			Add(
				items,
				session,
				"board-midseason",
				InboxChannel.Board,
				8,
				when == "available"
					? "Quarter-season evaluation available"
					: "Quarter-season evaluation tomorrow",
				BoardGoals.ScoreLine(session.BoardSatisfactionScore),
				"The board checks the district race, player progression, and whether the contract rungs are still in reach.",
				InboxDestination.Goals,
				"OPEN BOARD");
		}

		if (session.BoardSatisfaction <= BoardSatisfaction.Poor)
		{
			Add(
				items,
				session,
				"board-heat",
				InboxChannel.Board,
				8,
				"The board is getting restless",
				BoardGoals.ScoreLine(session.BoardSatisfactionScore),
				"Satisfaction is in the danger band. The season goals on the deal are the ones that still move the number.",
				InboxDestination.Goals,
				"OPEN BOARD");
		}
	}

	private static void CollectPlayers(List<InboxMessage> items, GameSession session)
	{
		AddInjured(items, session, TeamLevel.Varsity);
		AddInjured(items, session, TeamLevel.JuniorVarsity);

		SeasonEvent graduation = SeasonCalendar.Graduation(GameSession.SeasonYear);
		SeasonEvent farewell = SeasonCalendar.SeniorFarewell(GameSession.SeasonYear);
		DateOnly date = session.CurrentDate;
		if (graduation.OccursOn(date) || farewell.OccursOn(date) || graduation.Start == date.AddDays(1))
		{
			string when = graduation.OccursOn(date) || farewell.OccursOn(date) ? "today" : "tomorrow";
			Add(
				items,
				session,
				"player-graduation",
				InboxChannel.Player,
				9,
				when == "today" ? "Graduation is on the calendar" : "Graduation is tomorrow",
				"Seniors leave. College destinations post.",
				"Seniors permanently leave the active roster. Career numbers are archived and destinations are announced.",
				InboxDestination.Graduation,
				"VIEW GRADUATION");
		}
	}

	private static void AddInjured(List<InboxMessage> items, GameSession session, TeamLevel level)
	{
		foreach (ClubhousePlayer player in ClubhouseSquad.Injured(level))
		{
			string squad = level == TeamLevel.Varsity ? "Varsity" : "JV";
			Add(
				items,
				session,
				$"player-injury-{player.Jersey}-{squad}",
				InboxChannel.Player,
				11,
				$"{player.FullName} is {player.Status.ToLowerInvariant()}",
				$"{player.InjuryLabel}  ·  {player.TimelineLabel}  ·  {squad}",
				$"{player.FullName} ({player.Position}, {player.Year}) is {player.Status.ToLowerInvariant()} with a {player.InjuryLabel.ToLowerInvariant()}. Expected window: {player.TimelineLabel}.",
				InboxDestination.Injuries,
				"OPEN TRAINING ROOM",
				teamId: DistrictHubData.UserTeamId,
				playerName: player.FullName);
		}
	}

	private static void CollectCareer(List<InboxMessage> items, GameSession session)
	{
		if (JobOfferDesk.HasPending(session))
		{
			CoachingJobOffer? offer = JobOfferDesk.Next(session);
			string school = offer?.SchoolName ?? "Another program";
			Add(
				items,
				session,
				$"career-offer-{offer?.Id:N}",
				InboxChannel.Career,
				4,
				$"{school} requests an interview",
				offer == null
					? "A school wants you in the chair."
					: $"{offer.TermLabel}  ·  {offer.SalaryLabel} / year",
				offer == null
					? "An inbound call is waiting. Read the offer before you advance."
					: $"{offer.SchoolFullName} has a {offer.TermLabel.ToLowerInvariant()} deal on paper at {offer.SalaryLabel} a year. They want a conversation while you still have a seat.",
				InboxDestination.JobOffer,
				"READ OFFER");
		}

		if (JobMarketDesk.PendingOn(session))
		{
			Add(
				items,
				session,
				"career-market",
				InboxChannel.Career,
				4,
				"The coaching carousel is open",
				$"{session.AvailableJobs.Count} seats posted.",
				"Other programs are hiring. You can take a call or keep the current chair. The desk stays optional.",
				InboxDestination.JobMarket,
				"VIEW JOB MARKET");
		}
	}

	private static void CollectCalendar(List<InboxMessage> items, GameSession session)
	{
		int year = GameSession.SeasonYear;
		DateOnly date = session.CurrentDate;
		var covered = new HashSet<string>(StringComparer.Ordinal)
		{
			"winter-workouts",
			"tryout-week",
			"tryout-decisions",
			"incoming-class",
			"class-reveal",
			"prelim-scouting",
			"fall-scouting",
			"fall-evaluation",
			"midseason-review",
			"board-review",
			"hall-of-fame",
			"coaching-carousel",
			"graduation",
			"senior-farewell",
			"district-celebration",
			"regional-celebration",
			"champion-celebration",
			"district-awards",
			"state-awards",
			"roster-freeze",
			"roster-cut",
			"final-roster-week",
			"budget-planning",
		};

		foreach (SeasonEvent item in SeasonCalendar.On(year, date))
		{
			if (covered.Contains(item.Id) || !item.Featured)
			{
				continue;
			}

			InboxDestination destination = DestinationForEvent(item.Id);
			Add(
				items,
				session,
				$"cal-{item.Id}",
				ChannelForEvent(item.Id),
				14,
				item.Name,
				item.FormatDates(),
				item.Summary,
				destination,
				ActionLabelFor(destination));
		}

		DateOnly tomorrow = date.AddDays(1);
		foreach (SeasonEvent item in SeasonCalendar.On(year, tomorrow))
		{
			if (covered.Contains(item.Id) || item.Start != tomorrow)
			{
				continue;
			}

			if (!item.Featured && item.Kind != SeasonEventKind.Milestone)
			{
				continue;
			}

			InboxDestination destination = DestinationForEvent(item.Id);
			Add(
				items,
				session,
				$"cal-soon-{item.Id}",
				item.Featured ? InboxChannel.Urgent : ChannelForEvent(item.Id),
				14,
				$"{item.Name} is tomorrow",
				item.Summary,
				item.Summary,
				destination,
				ActionLabelFor(destination));
		}

		SeasonEvent freeze = SeasonCalendar.ById(year, "roster-freeze");
		SeasonEvent cut = SeasonCalendar.ById(year, "roster-cut");
		SeasonEvent rosterWeek = SeasonCalendar.FinalRosterWeek(year);
		if (freeze.OccursOn(date) || freeze.Start == tomorrow)
		{
			Add(
				items,
				session,
				"cal-freeze",
				InboxChannel.Urgent,
				8,
				freeze.OccursOn(date) ? "Postseason roster freeze is today" : "Postseason roster freeze is tomorrow",
				"Playoff roster locks here.",
				freeze.Summary,
				InboxDestination.Roster,
				"OPEN ROSTER");
		}
		else if (cut.OccursOn(date) || cut.Start == tomorrow)
		{
			Add(
				items,
				session,
				"cal-cut",
				InboxChannel.Urgent,
				8,
				cut.OccursOn(date) ? "Initial roster cut is today" : "Initial roster cut is tomorrow",
				"Schools must be under roster limits.",
				cut.Summary,
				InboxDestination.Roster,
				"OPEN ROSTER");
		}
		else if (rosterWeek.OccursOn(date))
		{
			Add(
				items,
				session,
				"cal-roster-week",
				InboxChannel.Urgent,
				10,
				"Final roster week is open",
				"Varsity and JV assignments, lineups, rotation, captaincy.",
				rosterWeek.Summary,
				InboxDestination.Roster,
				"OPEN ROSTER");
		}

		SeasonEvent budget = SeasonCalendar.BudgetPlanning(year);
		if (budget.OccursOn(date))
		{
			Add(
				items,
				session,
				"cal-budget",
				InboxChannel.Board,
				14,
				"Program budget planning is open",
				budget.Summary,
				budget.Summary,
				InboxDestination.Budget,
				"OPEN BUDGET");
		}
	}

	private static InboxDestination DestinationForEvent(string eventId) => eventId switch
	{
		"opening-day" or "final-weekend" or "playoff-selection" or "regional-semis" or "regional-final" or "state-championship" => InboxDestination.Schedule,
		"roster-cut" or "final-roster-week" or "roster-freeze" => InboxDestination.Roster,
		"budget-planning" => InboxDestination.Budget,
		"preseason-expectations" => InboxDestination.Goals,
		_ => InboxDestination.Calendar,
	};

	private static InboxChannel ChannelForEvent(string eventId) => eventId switch
	{
		"opening-day" or "final-weekend" or "playoff-selection" or "roster-freeze" or "roster-cut" => InboxChannel.Urgent,
		"budget-planning" or "preseason-expectations" => InboxChannel.Board,
		_ => InboxChannel.Urgent,
	};

	private static string ActionLabelFor(InboxDestination destination) => destination switch
	{
		InboxDestination.Tryouts => "OPEN TRYOUTS",
		InboxDestination.IncomingClass => "OPEN ROSTER",
		InboxDestination.Graduation => "VIEW GRADUATION",
		InboxDestination.Goals => "OPEN BOARD",
		InboxDestination.Injuries => "OPEN TRAINING ROOM",
		InboxDestination.PlayerSearch => "OPEN PLAYER SEARCH",
		InboxDestination.Targets => "VIEW TARGETS",
		InboxDestination.JobMarket => "VIEW JOB MARKET",
		InboxDestination.JobOffer => "READ OFFER",
		InboxDestination.Championship => "OPEN CELEBRATION",
		InboxDestination.Awards => "OPEN CEREMONY",
		InboxDestination.SeasonReview => "OPEN REVIEW",
		InboxDestination.HallOfFame => "OPEN INDUCTION",
		InboxDestination.GamePreview => "OPEN PREVIEW",
		InboxDestination.Calendar => "OPEN CALENDAR",
		InboxDestination.Schedule => "OPEN SCHEDULE",
		InboxDestination.Roster => "OPEN ROSTER",
		InboxDestination.Lineups => "OPEN LINEUPS",
		InboxDestination.Budget => "OPEN BUDGET",
		InboxDestination.PlayerProfile => "OPEN PROFILE",
		InboxDestination.Transactions => "VIEW TRANSACTIONS",
		InboxDestination.SeasonRollover => "OPEN YEAR IN REVIEW",
		_ => "OPEN",
	};

	private static void Add(
		List<InboxMessage> items,
		GameSession session,
		string id,
		InboxChannel channel,
		int priority,
		string headline,
		string preview,
		string body,
		InboxDestination destination,
		string actionLabel,
		ChampionshipTitle? championship = null,
		AwardScope? award = null,
		string? teamId = null,
		string? playerName = null,
		CalendarContest? game = null)
	{
		items.Add(new InboxMessage(
			id,
			channel,
			headline,
			preview,
			body,
			session.CurrentDateLabel,
			!session.MessageRead(id),
			priority,
			destination,
			actionLabel,
			championship,
			award,
			teamId,
			playerName,
			game));
	}

	private static CalendarContest? PrimaryGame(GameSession session)
	{
		CalendarContest? fallback = null;
		foreach (CalendarContest game in session.ContestsOn(session.CurrentDate))
		{
			if (game.Level == session.ActiveTeam.Level)
			{
				return game;
			}

			fallback ??= game;
		}

		return fallback;
	}

	private static int Compare(InboxMessage a, InboxMessage b)
	{
		int unread = b.Unread.CompareTo(a.Unread);
		if (unread != 0)
		{
			return unread;
		}

		int priority = a.Priority.CompareTo(b.Priority);
		if (priority != 0)
		{
			return priority;
		}

		int channel = a.Channel.CompareTo(b.Channel);
		return channel != 0 ? channel : string.Compare(a.Id, b.Id, StringComparison.Ordinal);
	}
}

public enum InboxChannel
{
	Urgent = 0,
	Scouting = 1,
	Board = 2,
	Player = 3,
	Career = 4,
}

public enum InboxDestination
{
	Tryouts,
	IncomingClass,
	Graduation,
	Goals,
	Injuries,
	PlayerSearch,
	Targets,
	JobMarket,
	JobOffer,
	Championship,
	Awards,
	SeasonReview,
	HallOfFame,
	GamePreview,
	Calendar,
	Schedule,
	Roster,
	Lineups,
	Budget,
	PlayerProfile,
	Transactions,
	SeasonRollover,
}

public sealed record InboxMessage(
	string Id,
	InboxChannel Channel,
	string Headline,
	string Preview,
	string Body,
	string DateLabel,
	bool Unread,
	int Priority,
	InboxDestination Destination,
	string ActionLabel,
	ChampionshipTitle? Championship,
	AwardScope? Award,
	string? TeamId,
	string? PlayerName,
	CalendarContest? Game);

public static class InboxChannelExtensions
{
	public static string ToLabel(this InboxChannel channel) => channel switch
	{
		InboxChannel.Urgent => "URGENT",
		InboxChannel.Scouting => "SCOUTING",
		InboxChannel.Board => "BOARD",
		InboxChannel.Player => "PLAYER",
		InboxChannel.Career => "CAREER",
		_ => "INBOX",
	};
}
