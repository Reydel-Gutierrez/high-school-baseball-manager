using System.Globalization;

namespace HSBM.Domain;

public enum SeasonEventKind
{
	Milestone = 0,
	Window = 1,
}

/// <summary>
/// Fixed baseball-year beats for a season. Dates are calendar dates in
/// the season year unless a window crosses December into January.
/// </summary>
public sealed record SeasonEvent(
	string Id,
	string Name,
	string Summary,
	DateOnly Start,
	DateOnly End,
	SeasonEventKind Kind,
	bool Featured = false)
{
	public bool OccursOn(DateOnly date) => date >= Start && date <= End;

	public bool OverlapsMonth(int year, int month)
	{
		var monthStart = new DateOnly(year, month, 1);
		var monthEnd = monthStart.AddMonths(1).AddDays(-1);
		return Start <= monthEnd && End >= monthStart;
	}

	public string FormatDates()
	{
		if (Start == End)
		{
			return Start.ToString("MMMM d", CultureInfo.InvariantCulture);
		}

		if (Start.Year == End.Year && Start.Month == End.Month)
		{
			return $"{Start.ToString("MMMM", CultureInfo.InvariantCulture)} {Start.Day}–{End.Day}";
		}

		return $"{Start.ToString("MMM d", CultureInfo.InvariantCulture)} – {End.ToString("MMM d", CultureInfo.InvariantCulture)}";
	}
}

public static class SeasonCalendar
{
	public static DateOnly BaseballYearStart(int seasonYear) => new(seasonYear, 1, 5);

	public static DateOnly OpeningDay(int seasonYear) => new(seasonYear, 2, 16);

	public static DateOnly SigningDeadline(int seasonYear) => new(seasonYear, 2, 1);

	public static DateOnly TryoutDecisionDeadline(int seasonYear) => OpenPlay(seasonYear).End;

	public static DateOnly InitialRosterCut(int seasonYear) => new(seasonYear, 2, 8);

	public static DateOnly TransferDeadline(int seasonYear) => new(seasonYear, 4, 1);

	public static DateOnly PromotionDeadline(int seasonYear) => new(seasonYear, 4, 17);

	public static DateOnly RosterFreeze(int seasonYear) => new(seasonYear, 4, 24);

	public static DateOnly RegularSeasonEnd(int seasonYear) => new(seasonYear, 5, 3);

	public static IReadOnlyList<SeasonEvent> ForSeason(int seasonYear)
	{
		int next = seasonYear + 1;
		int previous = seasonYear - 1;

		return
		[
			Milestone(
				"winter-break-open",
				"Winter Break",
				"Quiet period before the new baseball year. No games, no roster moves.",
				new DateOnly(previous, 12, 20),
				new DateOnly(seasonYear, 1, 4)),
			Milestone(
				"new-year",
				"New Baseball Year",
				"The season officially rolls over. Players advance a grade, last year's seniors are gone, the budget resets, and board goals are generated.",
				new DateOnly(seasonYear, 1, 5)),
			Window(
				"prelim-scouting",
				"Preliminary Scouting Period",
				"Incoming freshmen and new players become partially visible. Ratings are still incomplete.",
				new DateOnly(seasonYear, 1, 5),
				new DateOnly(seasonYear, 1, 11)),
			Milestone(
				"class-reveal",
				"Incoming Class Reveal",
				"Every district's unsigned incoming class is posted for Open Play. Schools start scouting the same pool.",
				new DateOnly(seasonYear, 1, 12)),
			Window(
				"open-play",
				"District Open Play",
				"Two weeks of background games among ~200 unsigned players. Schools scout the same performances. No Game Day — results feed recruiting.",
				new DateOnly(seasonYear, 1, 12),
				new DateOnly(seasonYear, 1, 25),
				featured: true),
			Window(
				"signing-week",
				"Signing Week",
				"Schools make sponsorship offers. Players weigh reputation, playing time, roster competition, and money before committing.",
				new DateOnly(seasonYear, 1, 26),
				new DateOnly(seasonYear, 2, 1),
				featured: true),
			Milestone(
				"signing-deadline",
				"Signing Deadline",
				"Normal signing closes. Unsigned players enter the available-player pool. The program's incoming class is posted.",
				new DateOnly(seasonYear, 2, 1)),
			Milestone(
				"incoming-class",
				"Incoming Class",
				"The signed incoming class is posted: new players, positions, overall, and potential.",
				new DateOnly(seasonYear, 2, 2),
				featured: true),
			Window(
				"final-roster-week",
				"Final Roster Week",
				"Set Varsity and JV assignments, lineups, pitching rotation, and captaincy.",
				new DateOnly(seasonYear, 2, 2),
				new DateOnly(seasonYear, 2, 8)),
			Milestone(
				"roster-cut",
				"Initial Roster Cut",
				"Schools must be under roster limits. Players cut here become free or available prospects.",
				new DateOnly(seasonYear, 2, 8)),
			Window(
				"preseason",
				"Preseason",
				"Practices plus two or three optional exhibition games. Stats do not count.",
				new DateOnly(seasonYear, 2, 9),
				new DateOnly(seasonYear, 2, 15)),
			Milestone(
				"opening-day",
				"Opening Day",
				"Regular season officially begins.",
				new DateOnly(seasonYear, 2, 16),
				featured: true),
			Window(
				"non-district",
				"Non-District Schedule",
				"Early games against schools from other districts. Useful for rankings, development, and scouting.",
				new DateOnly(seasonYear, 2, 16),
				new DateOnly(seasonYear, 3, 8)),
			Milestone(
				"district-play",
				"District Play Begins",
				"Games within the team's 8-school district begin counting heavily toward postseason qualification.",
				new DateOnly(seasonYear, 3, 9)),
			Milestone(
				"midseason-review",
				"Midseason Review",
				"The board evaluates performance. Player progression updates. District predictions and news are generated.",
				new DateOnly(seasonYear, 3, 30)),
			Milestone(
				"transfer-deadline",
				"Transfer/Acquisition Deadline",
				"Last point where eligible outside players can join another school's active roster.",
				new DateOnly(seasonYear, 4, 1)),
			Milestone(
				"eligibility-check",
				"Varsity Eligibility Check",
				"Academic and roster eligibility checkpoint. Room for gameplay events later.",
				new DateOnly(seasonYear, 4, 10)),
			Milestone(
				"promotion-deadline",
				"Final JV → Varsity Promotion Deadline",
				"After this, the postseason-eligible varsity roster becomes much harder to modify.",
				new DateOnly(seasonYear, 4, 17)),
			Milestone(
				"roster-freeze",
				"Postseason Roster Freeze",
				"Playoff roster locks here.",
				new DateOnly(seasonYear, 4, 24),
				featured: true),
			Window(
				"final-weekend",
				"Final Regular Season Weekend",
				"Last district games of the year.",
				new DateOnly(seasonYear, 5, 1),
				new DateOnly(seasonYear, 5, 3)),
			Milestone(
				"season-end",
				"Regular Season Ends",
				"Standings are finalized.",
				new DateOnly(seasonYear, 5, 3),
				featured: true),
			Milestone(
				"district-celebration",
				"District Champion Celebration",
				"Trophy presentation for the district title. The Regional bracket is next.",
				new DateOnly(seasonYear, 5, 3),
				featured: true),
			Milestone(
				"district-awards",
				"District Awards",
				"District MVP, Pitcher of the Year, Freshman of the Year, All-District Team, and Coach of the Year.",
				new DateOnly(seasonYear, 5, 4),
				featured: true),
			Milestone(
				"playoff-selection",
				"Playoff Selection Day",
				"Top two teams from each district qualify. Regional brackets are revealed.",
				new DateOnly(seasonYear, 5, 5),
				featured: true),
			Window(
				"regional-semis",
				"Regional Semifinals — Best of 3",
				"Cross-district matchups.",
				new DateOnly(seasonYear, 5, 7),
				new DateOnly(seasonYear, 5, 10),
				featured: true),
			Window(
				"regional-final",
				"Regional Final — Best of 3",
				"The remaining two schools in each Regional play.",
				new DateOnly(seasonYear, 5, 12),
				new DateOnly(seasonYear, 5, 15),
				featured: true),
			Milestone(
				"regional-celebration",
				"Regional Champion Celebration",
				"East or West Regional trophy. State is next.",
				new DateOnly(seasonYear, 5, 16),
				featured: true),
			Window(
				"state-championship",
				"State Championship — Best of 3",
				"The two Regional champions determine the state champion.",
				new DateOnly(seasonYear, 5, 18),
				new DateOnly(seasonYear, 5, 22),
				featured: true),
			Milestone(
				"champion-celebration",
				"State Champion Celebration",
				"Trophy, history update, school prestige gain, and records.",
				new DateOnly(seasonYear, 5, 23),
				featured: true),
			Window(
				"state-awards",
				"State Awards",
				"State MVP, Pitcher of the Year, Freshman of the Year, All-State Team, and Coach of the Year.",
				new DateOnly(seasonYear, 5, 24),
				new DateOnly(seasonYear, 5, 27),
				featured: true),
			Milestone(
				"senior-farewell",
				"Senior Farewell",
				"Seniors permanently leave the active roster. Career stats are archived.",
				new DateOnly(seasonYear, 5, 28)),
			Milestone(
				"graduation",
				"Graduation",
				"Seniors leave the program. Career numbers are archived and college destinations are announced.",
				new DateOnly(seasonYear, 6, 1),
				featured: true),
			Milestone(
				"board-review",
				"Board Season Review",
				"Final grade for the manager. Budget and prestige changes, plus potential firing, extensions, or offers.",
				new DateOnly(seasonYear, 6, 5),
				featured: true),
			Milestone(
				"hall-of-fame",
				"Program Hall of Fame",
				"One player is inducted. The plaque goes up so careers still matter after graduation.",
				new DateOnly(seasonYear, 6, 6),
				featured: true),
			Milestone(
				"coaching-carousel",
				"Coaching Carousel",
				"Other schools may fire or hire managers. Player movement rumors can begin.",
				new DateOnly(seasonYear, 6, 10)),
			Window(
				"summer-development",
				"Summer Development",
				"Players gain offseason development. Optional camps and scouting events.",
				new DateOnly(seasonYear, 6, 15),
				new DateOnly(seasonYear, 7, 15)),
			Milestone(
				"fall-scouting",
				"Fall Scouting Opens",
				"The incoming class starts generating behind the scenes.",
				new DateOnly(seasonYear, 8, 1)),
			Window(
				"fall-evaluation",
				"Fall Evaluation Period",
				"Scouts slowly discover next year's freshmen.",
				new DateOnly(seasonYear, 9, 1),
				new DateOnly(seasonYear, 10, 31)),
			Milestone(
				"budget-planning",
				"Program Budget Planning",
				"Next year's projected budget, sponsor, and support information becomes available.",
				new DateOnly(seasonYear, 11, 1)),
			Milestone(
				"preseason-expectations",
				"Preseason Expectations",
				"Early rankings, board expectations, and district outlook.",
				new DateOnly(seasonYear, 12, 1)),
			Milestone(
				"winter-break-close",
				"Winter Break",
				"Quiet period before the next baseball year.",
				new DateOnly(seasonYear, 12, 20),
				new DateOnly(next, 1, 4)),
		];
	}

	public static SeasonEvent OpenPlay(int seasonYear) => EventById(seasonYear, "open-play");

	public static SeasonEvent SigningWeek(int seasonYear) => EventById(seasonYear, "signing-week");

	public static SeasonEvent TryoutWeek(int seasonYear) => OpenPlay(seasonYear);

	public static SeasonEvent IncomingClass(int seasonYear) => EventById(seasonYear, "incoming-class");

	public static SeasonEvent Graduation(int seasonYear) => EventById(seasonYear, "graduation");

	public static SeasonEvent SeniorFarewell(int seasonYear) => EventById(seasonYear, "senior-farewell");

	public static SeasonEvent DistrictCelebration(int seasonYear) => EventById(seasonYear, "district-celebration");

	public static SeasonEvent RegionalCelebration(int seasonYear) => EventById(seasonYear, "regional-celebration");

	public static SeasonEvent ChampionCelebration(int seasonYear) => EventById(seasonYear, "champion-celebration");

	public static SeasonEvent DistrictAwards(int seasonYear) => EventById(seasonYear, "district-awards");

	public static SeasonEvent StateAwards(int seasonYear) => EventById(seasonYear, "state-awards");

	public static SeasonEvent BoardReview(int seasonYear) => EventById(seasonYear, "board-review");

	public static SeasonEvent HallOfFame(int seasonYear) => EventById(seasonYear, "hall-of-fame");

	public static SeasonEvent CoachingCarousel(int seasonYear) => EventById(seasonYear, "coaching-carousel");

	public static SeasonEvent PlayoffSelection(int seasonYear) => EventById(seasonYear, "playoff-selection");

	public static SeasonEvent RegionalSemis(int seasonYear) => EventById(seasonYear, "regional-semis");

	public static SeasonEvent RegionalFinal(int seasonYear) => EventById(seasonYear, "regional-final");

	public static SeasonEvent StateChampionship(int seasonYear) => EventById(seasonYear, "state-championship");

	public static bool IsPlayoffWindow(int seasonYear, DateOnly date) =>
		RegionalSemis(seasonYear).OccursOn(date)
		|| RegionalFinal(seasonYear).OccursOn(date)
		|| StateChampionship(seasonYear).OccursOn(date);

	public static SeasonEvent PreliminaryScouting(int seasonYear) => EventById(seasonYear, "prelim-scouting");

	public static SeasonEvent ClassReveal(int seasonYear) => EventById(seasonYear, "class-reveal");

	public static SeasonEvent FallScouting(int seasonYear) => EventById(seasonYear, "fall-scouting");

	public static SeasonEvent FallEvaluation(int seasonYear) => EventById(seasonYear, "fall-evaluation");

	public static SeasonEvent MidseasonReview(int seasonYear) => EventById(seasonYear, "midseason-review");

	public static SeasonEvent FinalRosterWeek(int seasonYear) => EventById(seasonYear, "final-roster-week");

	public static SeasonEvent Preseason(int seasonYear) => EventById(seasonYear, "preseason");

	public static SeasonEvent NonDistrict(int seasonYear) => EventById(seasonYear, "non-district");

	public static SeasonPhase PhaseOn(int seasonYear, DateOnly date) =>
		CareerCalendar.PhaseOn(seasonYear, date);

	public static bool IsRolloverDay(int seasonYear, DateOnly date) =>
		CareerCalendar.IsRolloverDay(seasonYear, date);

	public static SeasonEvent BudgetPlanning(int seasonYear) => EventById(seasonYear, "budget-planning");

	public static bool BoardReviewOn(int seasonYear, DateOnly date) =>
		BoardReview(seasonYear).OccursOn(date);

	public static bool HallOfFameOn(int seasonYear, DateOnly date) =>
		HallOfFame(seasonYear).OccursOn(date);

	public static bool CoachingCarouselOn(int seasonYear, DateOnly date) =>
		CoachingCarousel(seasonYear).OccursOn(date);

	public static AwardScope? AwardCeremonyOn(int seasonYear, DateOnly date)
	{
		if (DistrictAwards(seasonYear).OccursOn(date))
		{
			return AwardScope.District;
		}

		if (StateAwards(seasonYear).OccursOn(date))
		{
			return AwardScope.State;
		}

		return null;
	}

	public static AwardScope? AwardCeremonyForEvent(string eventId) =>
		eventId switch
		{
			"district-awards" => AwardScope.District,
			"state-awards" => AwardScope.State,
			_ => null,
		};

	public static ChampionshipTitle? ChampionshipOn(int seasonYear, DateOnly date)
	{
		if (DistrictCelebration(seasonYear).OccursOn(date))
		{
			return ChampionshipTitle.District;
		}

		if (RegionalCelebration(seasonYear).OccursOn(date))
		{
			return ChampionshipTitle.Regional;
		}

		if (ChampionCelebration(seasonYear).OccursOn(date))
		{
			return ChampionshipTitle.State;
		}

		return null;
	}

	public static ChampionshipTitle? ChampionshipForEvent(string eventId) =>
		eventId switch
		{
			"district-celebration" => ChampionshipTitle.District,
			"regional-celebration" => ChampionshipTitle.Regional,
			"champion-celebration" => ChampionshipTitle.State,
			_ => null,
		};

	public static DateOnly OpenPlayStart(int seasonYear) => OpenPlay(seasonYear).Start;

	public static SeasonEvent ById(int seasonYear, string id) => EventById(seasonYear, id);

	private static SeasonEvent EventById(int seasonYear, string id)
	{
		foreach (SeasonEvent item in ForSeason(seasonYear))
		{
			if (item.Id == id)
			{
				return item;
			}
		}

		throw new InvalidOperationException($"Season event '{id}' is missing from the calendar.");
	}

	public static IReadOnlyList<SeasonEvent> On(int seasonYear, DateOnly date)
	{
		var matches = new List<SeasonEvent>();
		foreach (SeasonEvent item in ForSeason(seasonYear))
		{
			if (item.OccursOn(date))
			{
				matches.Add(item);
			}
		}

		return matches;
	}

	public static IReadOnlyList<SeasonEvent> InMonth(int seasonYear, int year, int month)
	{
		var matches = new List<SeasonEvent>();
		foreach (SeasonEvent item in ForSeason(seasonYear))
		{
			if (item.OverlapsMonth(year, month))
			{
				matches.Add(item);
			}
		}

		return matches;
	}

	private static SeasonEvent Milestone(
		string id,
		string name,
		string summary,
		DateOnly day,
		bool featured = false) =>
		new(id, name, summary, day, day, SeasonEventKind.Milestone, featured);

	private static SeasonEvent Milestone(
		string id,
		string name,
		string summary,
		DateOnly start,
		DateOnly end,
		bool featured = false) =>
		new(id, name, summary, start, end, SeasonEventKind.Milestone, featured);

	private static SeasonEvent Window(
		string id,
		string name,
		string summary,
		DateOnly start,
		DateOnly end,
		bool featured = false) =>
		new(id, name, summary, start, end, SeasonEventKind.Window, featured);
}
