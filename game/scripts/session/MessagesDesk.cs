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
		CollectRecruiting(items, session);
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

	private static void CollectRecruiting(List<InboxMessage> items, GameSession session)
	{
		int year = GameSession.SeasonYear;
		DateOnly date = session.CurrentDate;
		DateOnly tomorrow = date.AddDays(1);
		SeasonEvent openPlay = SeasonCalendar.OpenPlay(year);
		SeasonEvent signing = SeasonCalendar.SigningWeek(year);
		DateOnly deadline = SeasonCalendar.SigningDeadline(year);
		RecruitingMarket market = session.RecruitingMarket;
		string orgId = session.CatalogOrganizationId;
		int pending = market.PendingCount(orgId);
		int remaining = market.RemainingNeed(orgId);

		if (openPlay.End == tomorrow || (openPlay.OccursOn(date) && openPlay.End == date))
		{
			string when = openPlay.End == date ? "today" : "tomorrow";
			Add(
				items,
				session,
				"recruit-decisions",
				InboxChannel.Urgent,
				6,
				$"Tryout roster decisions due {when}",
				"Open Play closes. Signing week is next.",
				$"District Open Play ends {when}. Unsigned players you still want need a look before the pool turns into offers.",
				InboxDestination.Tryouts,
				"OPEN RECRUITING");
		}
		else if (openPlay.OccursOn(date))
		{
			Add(
				items,
				session,
				"recruit-open-play",
				InboxChannel.Scouting,
				7,
				"District Open Play is live",
				$"{market.UnsignedProspects.Count} unsigned players on the board.",
				"Two weeks of background games. Schools scout the same performances. No Game Day — results feed recruiting.",
				InboxDestination.Tryouts,
				"OPEN RECRUITING");
		}

		if (signing.Start == tomorrow)
		{
			Add(
				items,
				session,
				"recruit-signing-soon",
				InboxChannel.Urgent,
				6,
				"Signing week starts tomorrow",
				remaining > 0
					? $"{remaining} incoming spots still open."
					: "Offers go out tomorrow.",
				"Sponsorship offers open tomorrow. Players weigh reputation, playing time, roster competition, and money before they commit.",
				InboxDestination.Tryouts,
				"OPEN RECRUITING");
		}

		if (signing.OccursOn(date) || date == deadline)
		{
			bool lastDay = date == deadline || tomorrow == deadline;
			if (lastDay)
			{
				string when = date == deadline ? "today" : "tomorrow";
				Add(
					items,
					session,
					"recruit-deadline",
					InboxChannel.Urgent,
					6,
					$"Signing deadline {when}",
					pending > 0
						? $"{pending} offers still waiting on answers."
						: remaining > 0
							? $"{remaining} incoming spots still open."
							: "Normal signing closes. Unsigned players hit the pool.",
					"After the deadline, unsigned players enter the available-player pool and the incoming class is posted.",
					InboxDestination.Tryouts,
					"OPEN RECRUITING");
			}
			else if (pending > 0)
			{
				Add(
					items,
					session,
					"recruit-pending-offers",
					InboxChannel.Urgent,
					7,
					$"{pending} recruiting offers still out",
					remaining > 0
						? $"{remaining} incoming spots still open."
						: "Waiting on commitments.",
					"Signing week is open. Offers you have already placed are still waiting. Players decide before the deadline.",
					InboxDestination.Tryouts,
					"OPEN RECRUITING");
			}
			else if (remaining > 0)
			{
				Add(
					items,
					session,
					"recruit-spots",
					InboxChannel.Urgent,
					7,
					$"{remaining} incoming spots still open",
					"Signing week is open. Place offers before the deadline.",
					$"The program still needs {remaining} incoming player{(remaining == 1 ? "" : "s")} against the roster target.",
					InboxDestination.Tryouts,
					"OPEN RECRUITING");
			}
			else
			{
				Add(
					items,
					session,
					"recruit-signing",
					InboxChannel.Scouting,
					8,
					"Signing week is open",
					signing.FormatDates(),
					"Schools make sponsorship offers. Players weigh reputation, playing time, roster competition, and money before committing.",
					InboxDestination.Tryouts,
					"OPEN RECRUITING");
			}
		}

		if (SeasonCalendar.IncomingClass(year).OccursOn(date)
			|| (date == deadline.AddDays(1) && date != deadline))
		{
			Add(
				items,
				session,
				"recruit-incoming",
				InboxChannel.Scouting,
				9,
				"Incoming class is posted",
				"New players, positions, overall, and potential.",
				"The signed incoming class is on the board. Check who committed and where they slot on the roster.",
				InboxDestination.IncomingClass,
				"VIEW INCOMING CLASS");
		}
	}

	private static void CollectScouting(List<InboxMessage> items, GameSession session)
	{
		int year = GameSession.SeasonYear;
		DateOnly date = session.CurrentDate;
		SeasonEvent prelim = SeasonCalendar.PreliminaryScouting(year);
		SeasonEvent reveal = SeasonCalendar.ClassReveal(year);
		SeasonEvent fall = SeasonCalendar.FallScouting(year);
		SeasonEvent evaluation = SeasonCalendar.FallEvaluation(year);
		bool openPlay = SeasonCalendar.OpenPlay(year).OccursOn(date);
		bool scoutingWindow = prelim.OccursOn(date) || reveal.OccursOn(date) || fall.OccursOn(date) || evaluation.OccursOn(date);

		if (prelim.OccursOn(date) && !openPlay)
		{
			Add(
				items,
				session,
				"scout-prelim",
				InboxChannel.Scouting,
				10,
				"Preliminary scouting period is open",
				"Incoming freshmen are partially visible. Ratings are still incomplete.",
				prelim.Summary,
				InboxDestination.PlayerSearch,
				"OPEN PLAYER SEARCH");
		}

		if (reveal.OccursOn(date) && !openPlay)
		{
			Add(
				items,
				session,
				"scout-reveal",
				InboxChannel.Scouting,
				10,
				"Incoming class is on the board",
				"Every district's unsigned class is posted for Open Play.",
				reveal.Summary,
				InboxDestination.Tryouts,
				"OPEN RECRUITING");
		}

		if (fall.OccursOn(date) || evaluation.OccursOn(date))
		{
			Add(
				items,
				session,
				"scout-fall",
				InboxChannel.Scouting,
				11,
				evaluation.OccursOn(date) ? "Fall evaluation is open" : "Fall scouting is open",
				"Next year's incoming class is generating.",
				evaluation.OccursOn(date) ? evaluation.Summary : fall.Summary,
				InboxDestination.PlayerSearch,
				"OPEN PLAYER SEARCH");
		}

		if (openPlay || scoutingWindow)
		{
			var prospects = new List<Player>(session.RecruitingMarket.UnsignedProspects);
			prospects.Sort((a, b) =>
			{
				int overall = b.Overall.CompareTo(a.Overall);
				return overall != 0 ? overall : string.Compare(a.LastName, b.LastName, StringComparison.Ordinal);
			});

			int reports = Math.Min(2, prospects.Count);
			for (int i = 0; i < reports; i++)
			{
				Player prospect = prospects[i];
				Add(
					items,
					session,
					$"scout-report-{prospect.Id:N}",
					InboxChannel.Scouting,
					12,
					$"Report completed on {prospect.FullName}",
					$"{prospect.DisplayPosition}  ·  {prospect.Grade.ToAbbrev()}  ·  {prospect.Overall} OVR",
					$"The desk finished a look at {prospect.FullName}. Present is clearer than projection. He is still unsigned.",
					InboxDestination.Tryouts,
					"OPEN RECRUITING");
			}
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
			"open-play",
			"signing-week",
			"signing-deadline",
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
		InboxDestination.Tryouts => "OPEN RECRUITING",
		InboxDestination.IncomingClass => "VIEW INCOMING CLASS",
		InboxDestination.Graduation => "VIEW GRADUATION",
		InboxDestination.Goals => "OPEN BOARD",
		InboxDestination.Injuries => "OPEN TRAINING ROOM",
		InboxDestination.PlayerSearch => "OPEN PLAYER SEARCH",
		InboxDestination.Targets => "VIEW TARGETS",
		InboxDestination.OpenPool => "OPEN POOL",
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
	OpenPool,
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
