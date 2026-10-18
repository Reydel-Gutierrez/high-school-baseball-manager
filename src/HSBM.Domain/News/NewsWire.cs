using System.Globalization;

namespace HSBM.Domain;

/// <summary>
/// Turns standings, streaks, player lines, and calendar beats into wire
/// copy. Stories only fire when the underlying condition is true.
/// </summary>
public static class NewsWire
{
	public const int MaxStoriesPerPass = 5;
	public const int MinGamesForRankings = 8;
	public const int MinGamesForCoachHeat = 12;
	public const int MinStreak = 5;
	public const int MinAtBatsForFreshman = 15;
	public const double FreshmanAverage = 0.320;
	public const double CoachHeatWinPct = 0.350;
	public const int CoachHeatLossGap = 6;
	public const double AceEra = 2.10;
	public const double AceInnings = 18;
	public const double FreshmanAceEra = 2.40;
	public const double FreshmanAceInnings = 12;

	private static readonly int[] StreakBuckets = [5, 8, 11];

	public static IReadOnlyList<NewsStory> Publish(CareerState state)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (NewsStory story in state.News)
		{
			if (!string.IsNullOrWhiteSpace(story.DedupKey))
			{
				seen.Add(story.DedupKey);
			}
		}

		List<NewsStory> fresh = Discover(NewsWorld.FromCareer(state), state.News);
		foreach (NewsStory story in fresh)
		{
			if (!string.IsNullOrWhiteSpace(story.DedupKey))
			{
				seen.Add(story.DedupKey);
			}
		}

		fresh.AddRange(SigningStories(state, seen));
		fresh.AddRange(ResultStories(state, seen));
		fresh.AddRange(ChampionshipStories(state, seen));
		fresh.AddRange(FiringStories(state, seen));
		fresh.AddRange(PerformanceStories(state, seen));
		fresh.AddRange(RecordStories(state, seen));
		if (fresh.Count == 0)
		{
			return fresh;
		}

		state.News.AddRange(fresh);
		return fresh;
	}

	public static List<NewsStory> Discover(NewsWorld world, IReadOnlyList<NewsStory> existing)
	{
		var seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (NewsStory story in existing)
		{
			if (!string.IsNullOrWhiteSpace(story.DedupKey))
			{
				seen.Add(story.DedupKey);
			}
		}

		var candidates = new List<NewsStory>();
		AddCalendar(candidates, world, seen);
		AddRankingsAndStreaks(candidates, world, seen);
		AddProjectedField(candidates, world, seen);
		AddCoachHeat(candidates, world, seen);
		AddFreshmen(candidates, world, seen);
		AddDistrictRaces(candidates, world, seen);
		AddAces(candidates, world, seen);

		candidates.Sort((a, b) => Priority(a.Tag).CompareTo(Priority(b.Tag)));
		if (candidates.Count > MaxStoriesPerPass)
		{
			candidates.RemoveRange(MaxStoriesPerPass, candidates.Count - MaxStoriesPerPass);
		}

		return candidates;
	}

	private static List<NewsStory> SigningStories(CareerState state, HashSet<string> seen)
	{
		var stories = new List<NewsStory>();
		DateOnly today = state.Career.CurrentDate;
		DateOnly signedOn = today.AddDays(-1);
		string userOrg = state.Career.UserOrganizationId;
		var notable = new List<(PlayerTransaction Entry, Player Player, OrganizationState School)>();
		foreach (PlayerTransaction entry in state.Transactions)
		{
			if (entry.Date != signedOn || entry.Kind != PlayerTransactionKind.Tryout || entry.PlayerId == Guid.Empty)
			{
				continue;
			}

			Player? player = state.PlayerById(entry.PlayerId);
			if (player == null)
			{
				continue;
			}

			OrganizationState school;
			try
			{
				school = state.OrganizationById(entry.OrganizationId);
			}
			catch (KeyNotFoundException)
			{
				continue;
			}

			notable.Add((entry, player, school));
		}

		notable.Sort((a, b) =>
		{
			int user = (b.School.Id == userOrg).CompareTo(a.School.Id == userOrg);
			if (user != 0)
			{
				return user;
			}

			return b.Player.Overall.CompareTo(a.Player.Overall);
		});

		int posted = 0;
		foreach ((PlayerTransaction entry, Player player, OrganizationState school) in notable)
		{
			if (posted >= 3)
			{
				break;
			}

			if (school.Id != userOrg && player.Overall < 66)
			{
				continue;
			}

			string key = $"tryout:{player.Id:N}:{signedOn:yyyyMMdd}";
			if (!seen.Add(key))
			{
				continue;
			}

			bool varsity = player.TeamLevel == TeamLevel.Varsity;
			bool young = player.Grade <= PlayerGrade.Sophomore;
			string headline = varsity && young
				? $"{player.FullName} makes {school.ShortName} varsity as a {GradeWord(player.Grade)}."
				: $"{player.FullName} earns a spot at {school.ShortName}.";
			stories.Add(Story(
				signedOn,
				NewsTag.Tryouts,
				headline,
				$"{player.FullName}, a {player.Grade.ToAbbrev()} {player.DisplayPosition}, came out of tryouts on the {school.ShortName} {player.TeamLevel.ToLabel()} roster.",
				varsity && young
					? "Underclassmen don't usually crack a varsity lineup card. This one forced the issue in tryout week."
					: "Every roster in the district was built the same way: whoever showed up, and whoever was good enough.",
				key,
				school.Id,
				player.FullName));
			posted++;
		}

		return stories;
	}

	private static List<NewsStory> ResultStories(CareerState state, HashSet<string> seen)
	{
		var stories = new List<NewsStory>();
		DateOnly yesterday = state.Career.CurrentDate.AddDays(-1);
		int posted = 0;
		foreach (ScheduledGame game in state.Games)
		{
			if (posted >= 3)
			{
				break;
			}

			if (game.Date != yesterday
				|| game.Status != GameStatus.Completed
				|| game.TeamLevel != TeamLevel.Varsity
				|| game.GameType.IsExhibition()
				|| game.HomeScore is null
				|| game.AwayScore is null)
			{
				continue;
			}

			int margin = Math.Abs(game.HomeScore.Value - game.AwayScore.Value);
			bool extra = (game.InningsPlayed ?? 9) > 9;
			bool playoff = game.GameType.IsPostseason();
			if (!playoff && margin < 8 && !extra)
			{
				continue;
			}

			string key = $"result:{game.Id:N}";
			if (!seen.Add(key))
			{
				continue;
			}

			string winnerId = game.WinningTeamId ?? "";
			string loserId = game.LosingTeamId ?? "";
			if (string.IsNullOrEmpty(winnerId) || string.IsNullOrEmpty(loserId))
			{
				continue;
			}

			OrganizationState winner = state.OrganizationForTeam(winnerId);
			OrganizationState loser = state.OrganizationForTeam(loserId);
			int winScore = winnerId == game.HomeTeamId ? game.HomeScore.Value : game.AwayScore.Value;
			int loseScore = winnerId == game.HomeTeamId ? game.AwayScore.Value : game.HomeScore.Value;
			string tag = playoff ? NewsTag.Playoffs : NewsTag.Results;
			string headline = playoff
				? $"{winner.ShortName} takes a playoff game from {loser.ShortName}, {winScore}\u2013{loseScore}."
				: extra
					? $"{winner.ShortName} outlasts {loser.ShortName} in extras, {winScore}\u2013{loseScore}."
					: $"{winner.ShortName} rolls {loser.ShortName}, {winScore}\u2013{loseScore}.";
			stories.Add(Story(
				yesterday,
				tag,
				headline,
				playoff
					? "Best-of-three series are won in nights like this. One more and someone is moving on."
					: extra
						? "Extra innings in April are a statement. The district notices who finishes them."
						: "A margin that large is a ranking game and a warning to the rest of the district.",
				playoff
					? "Playoff baseball is a different sport. The next game is the one that ends a season."
					: "Blowouts and extras are the nights the wire writes down.",
				key,
				winner.Id));
			posted++;
		}

		return stories;
	}

	private static List<NewsStory> ChampionshipStories(CareerState state, HashSet<string> seen)
	{
		var stories = new List<NewsStory>();
		PostseasonState? bracket = state.World.Postseason;
		if (bracket == null || bracket.SeasonYear != state.Career.CurrentSeasonYear)
		{
			return stories;
		}

		DateOnly yesterday = state.Career.CurrentDate.AddDays(-1);
		foreach (PostseasonSeries series in bracket.Series)
		{
			if (!series.IsComplete || string.IsNullOrEmpty(series.WinnerTeamId))
			{
				continue;
			}

			DateOnly last = LastGameDate(state, series);
			if (last != yesterday)
			{
				continue;
			}

			string key = $"title:{series.Id:N}";
			if (!seen.Add(key))
			{
				continue;
			}

			OrganizationState winner = state.OrganizationForTeam(series.WinnerTeamId);
			(string headline, string body, string pull) = series.Round switch
			{
				PostseasonRound.StateChampionship => (
					$"{winner.ShortName} wins the state championship.",
					$"{winner.ShortName} closed the best-of-three and owns the last trophy of {bracket.SeasonYear}.",
					"State titles hang in the gym and follow a program for years."),
				PostseasonRound.RegionalFinal => (
					$"{winner.ShortName} wins the {series.Region} Regional.",
					$"{winner.ShortName} is through to the state championship series.",
					"Two regional champions. One state title."),
				_ => (
					$"{winner.ShortName} advances in the {series.Region} Regional.",
					$"{winner.ShortName} took the best-of-three and is into the regional final.",
					"Win this round and state is one series away."),
			};
			stories.Add(Story(
				yesterday,
				NewsTag.Playoffs,
				headline,
				body,
				pull,
				key,
				winner.Id));
		}

		foreach (StandingEntry row in LeagueStandings.FromWorld(state.World, state.Career.CurrentSeasonYear))
		{
			if (row.Rank != 1 || yesterday != SeasonCalendar.RegularSeasonEnd(state.Career.CurrentSeasonYear))
			{
				continue;
			}

			string key = $"district-title:{row.OrganizationId}:{state.Career.CurrentSeasonYear}";
			if (!seen.Add(key))
			{
				continue;
			}

			string districtName = row.DistrictId;
			foreach (DistrictIdentity district in state.Districts)
			{
				if (district.Id == row.DistrictId)
				{
					districtName = district.Name;
					break;
				}
			}

			stories.Add(Story(
				yesterday,
				NewsTag.Playoffs,
				$"{row.Name} are {districtName} district champions.",
				$"{row.Name} finished first at {row.Wins}\u2013{row.Losses}. The regional field is the top two.",
				"District titles are regular-season first place. The playoffs start next.",
				key,
				row.OrganizationId));
		}

		return stories;
	}

	private static DateOnly LastGameDate(CareerState state, PostseasonSeries series)
	{
		DateOnly last = default;
		foreach (Guid gameId in series.GameIds)
		{
			foreach (ScheduledGame game in state.Games)
			{
				if (game.Id == gameId && game.Status == GameStatus.Completed && game.Date > last)
				{
					last = game.Date;
				}
			}
		}

		return last;
	}

	private static List<NewsStory> FiringStories(CareerState state, HashSet<string> seen)
	{
		var stories = new List<NewsStory>();
		int posted = 0;
		foreach (OrganizationState org in state.Organizations)
		{
			if (posted >= 3
				|| LeagueCatalog.IsUnsignedOrganization(org.Id)
				|| !org.Profile.HasOpening
				|| org.Profile.VacancyReason != VacancyReason.Dismissal)
			{
				continue;
			}

			string key = $"firing:{org.Id}:{state.Career.CurrentSeasonYear}";
			if (!seen.Add(key))
			{
				continue;
			}

			stories.Add(Story(
				state.Career.CurrentDate,
				NewsTag.Coach,
				$"{org.ShortName} dismisses its head coach.",
				$"{org.ShortName} finished {org.Profile.LastSeasonWins}\u2013{org.Profile.LastSeasonLosses} and the board opened the chair.",
				"Programs that miss the minimum in May start the carousel in June.",
				key,
				org.Id));
			posted++;
		}

		return stories;
	}

	private static List<NewsStory> PerformanceStories(CareerState state, HashSet<string> seen)
	{
		var stories = new List<NewsStory>();
		DateOnly yesterday = state.Career.CurrentDate.AddDays(-1);
		int posted = 0;
		foreach (ScheduledGame game in state.Games)
		{
			if (posted >= 2
				|| game.Date != yesterday
				|| game.Status != GameStatus.Completed
				|| game.TeamLevel != TeamLevel.Varsity)
			{
				continue;
			}

			GameBoxScore? box = state.BoxScoreFor(game.Id);
			if (box == null)
			{
				continue;
			}

			foreach (BoxScoreBattingLine line in box.HomeBatting.Concat(box.AwayBatting))
			{
				if (posted >= 2 || line.HomeRuns < 3)
				{
					continue;
				}

				string key = $"game-hr:{game.Id:N}:{line.PlayerId:N}";
				if (!seen.Add(key))
				{
					continue;
				}

				Player? player = state.PlayerById(line.PlayerId);
				if (player == null)
				{
					continue;
				}

				stories.Add(Story(
					yesterday,
					NewsTag.Hitting,
					$"{player.FullName} launched {line.HomeRuns} home runs.",
					$"{player.FullName} went deep {line.HomeRuns} times for {SchoolName(state, player.OrganizationId)}.",
					"Nights like that follow a hitter the rest of the spring.",
					key,
					player.OrganizationId,
					player.FullName));
				posted++;
			}

			foreach (BoxScorePitchingLine line in box.HomePitching.Concat(box.AwayPitching))
			{
				if (posted >= 2 || line.Strikeouts < 12)
				{
					continue;
				}

				string key = $"game-k:{game.Id:N}:{line.PlayerId:N}";
				if (!seen.Add(key))
				{
					continue;
				}

				Player? player = state.PlayerById(line.PlayerId);
				if (player == null)
				{
					continue;
				}

				stories.Add(Story(
					yesterday,
					NewsTag.Pitching,
					$"{player.FullName} punched out {line.Strikeouts}.",
					$"{player.FullName} piled up {line.Strikeouts} strikeouts for {SchoolName(state, player.OrganizationId)}.",
					"A double-digit strikeout night is how aces get named.",
					key,
					player.OrganizationId,
					player.FullName));
				posted++;
			}
		}

		return stories;
	}

	private static List<NewsStory> RecordStories(CareerState state, HashSet<string> seen)
	{
		var stories = new List<NewsStory>();
		int posted = 0;
		foreach (OrganizationState org in state.Organizations)
		{
			if (posted >= 2 || LeagueCatalog.IsUnsignedOrganization(org.Id))
			{
				continue;
			}

			foreach (ProgramRecordHolder holder in org.RecordBook)
			{
				if (posted >= 2 || holder.Rank != 1 || holder.PlayerId is not Guid playerId)
				{
					continue;
				}

				Player? player = state.PlayerById(playerId);
				if (player == null || player.Alumni)
				{
					continue;
				}

				string key = $"record:{org.Id}:{holder.Category}:{playerId:N}";
				if (!seen.Add(key))
				{
					continue;
				}

				stories.Add(Story(
					state.Career.CurrentDate,
					holder.Category.IsPitching() ? NewsTag.Pitching : NewsTag.Hitting,
					$"{player.FullName} owns the {org.ShortName} {holder.Category.ToLabel().ToLowerInvariant()} record.",
					$"{player.FullName} is first on the program board at {holder.Value} {holder.Category.ToLabel().ToLowerInvariant()}.",
					"Program records are why careers still matter after graduation.",
					key,
					org.Id,
					player.FullName));
				posted++;
			}
		}

		return stories;
	}

	private static string SchoolName(CareerState state, string organizationId)
	{
		try
		{
			return state.OrganizationById(organizationId).ShortName;
		}
		catch (KeyNotFoundException)
		{
			return "the club";
		}
	}

	private static int Priority(string tag) => tag switch
	{
		NewsTag.Playoffs => 0,
		NewsTag.Results => 1,
		NewsTag.Rankings => 2,
		NewsTag.Tryouts => 2,
		NewsTag.Calendar => 3,
		NewsTag.Coach => 3,
		NewsTag.Freshman => 4,
		NewsTag.Rivalry => 5,
		NewsTag.Pitching => 6,
		NewsTag.District => 7,
		_ => 8,
	};

	private static void AddCalendar(List<NewsStory> stories, NewsWorld world, HashSet<string> seen)
	{
		foreach (SeasonEvent item in SeasonCalendar.ForSeason(world.SeasonYear))
		{
			if (item.Start != world.Date)
			{
				continue;
			}

			string key = $"calendar:{item.Id}:{world.SeasonYear}";
			if (!seen.Add(key))
			{
				continue;
			}

			(string tag, string headline, string body, string pull) = CalendarCopy(item, world);
			stories.Add(Story(world.Date, tag, headline, body, pull, key));
		}
	}

	private static (string Tag, string Headline, string Body, string Pull) CalendarCopy(
		SeasonEvent item,
		NewsWorld world)
	{
		return item.Id switch
		{
			"tryout-week" => (
				NewsTag.Calendar,
				$"Tryouts open at every school in {world.SeasonYear}.",
				"Freshmen, returning JV kids, and a few transfers are on the field together. Six days of drills, bullpens, and scrimmages decide who makes it.",
				"Coaches see what a kid can do with a bat in his hands. The sign-up sheet only told them his name and his shoe size."),
			"tryout-decisions" => (
				NewsTag.Calendar,
				"Decision Day: the lists go up.",
				"Varsity, JV, or cut. Every school posts its rosters today.",
				"Some kids will be back next year. Some will find another sport. That's high school baseball."),
			"incoming-class" => (
				NewsTag.Calendar,
				"Rosters are posted.",
				"Every program's new names, positions, and remaining eligibility are on the board.",
				"Roster week starts now. The varsity that opens in February is built from this list."),
			"opening-day" => (
				NewsTag.Calendar,
				$"{world.SeasonYear} opening day is on the books.",
				"Non-district games count. Rankings, development, and the first look at Friday-night rotations all start here.",
				"The wire will start tracking streaks, breakouts, and boards as soon as the first week lands."),
			"district-play" => (
				NewsTag.District,
				"District play is underway.",
				"League games now carry the postseason. Top two in each eight-school district qualify for the regional.",
				"Every series inside the district is a seed. Non-district wins still fill the ranking, but they no longer decide May."),
			"midseason-review" => (
				NewsTag.District,
				"Midseason boards are out.",
				MidseasonBody(world),
				"This is where a hot three weeks still moves a seed, and where a cold one starts the coaching questions."),
			"season-end" => (
				NewsTag.Playoffs,
				"Regular season is final.",
				"Standings are locked. District champions and the second seeds are the regional field if selection day were today.",
				PlayoffPull(world)),
			"playoff-selection" => (
				NewsTag.Playoffs,
				"Regional brackets are official.",
				PlayoffBody(world),
				"Top two from each district. The rest of the state is done until next year."),
			"state-championship" => (
				NewsTag.Playoffs,
				"State tournament field is finalized.",
				"The East and West regional champions open a best-of-three for the state title.",
				"Two programs left. Everyone else is watching."),
			_ => (
				item.Featured ? NewsTag.Calendar : NewsTag.Note,
				item.Name.EndsWith('.') ? item.Name : item.Name + ".",
				item.Summary,
				item.Featured
					? "A baseball-year beat that moves rosters, boards, or the postseason."
					: "On the calendar, and now on the wire."),
		};
	}

	private static void AddRankingsAndStreaks(List<NewsStory> stories, NewsWorld world, HashSet<string> seen)
	{
		List<NewsTeamSnapshot> ranked = Ranked(world.Teams);
		if (ranked.Count == 0)
		{
			return;
		}

		NewsTeamSnapshot leader = ranked[0];
		if (leader.Games >= MinGamesForRankings && leader.Streak >= MinStreak)
		{
			string key = $"rank-one:{leader.OrganizationId}:{world.SeasonYear}";
			if (seen.Add(key))
			{
				NewsTeamSnapshot? chase = ranked.Count > 1 ? ranked[1] : null;
				string streak = StreakLabel(leader.Streak);
				stories.Add(Story(
					world.Date,
					NewsTag.Rankings,
					$"{leader.ShortName} climbs to #1 after {streak}.",
					$"{leader.ShortName} is {leader.RecordLabel} and alone at the top of the state. The {streak} is the longest active run among the four districts.",
					chase == null
						? $"{leader.ShortName} has separation. The next loss is the first ranking argument."
						: $"{chase.ShortName} sits {chase.RecordLabel} in second. A slip this week would hand the ranking right back.",
					key,
					leader.OrganizationId));
			}
		}
		else if (leader.Games >= MinGamesForRankings)
		{
			string key = $"rank-one:{leader.OrganizationId}:{world.SeasonYear}";
			if (seen.Add(key))
			{
				stories.Add(Story(
					world.Date,
					NewsTag.Rankings,
					$"{leader.ShortName} sits #1 at {leader.RecordLabel}.",
					$"{leader.ShortName} owns the state ranking on wins, then run differential. The rest of the field is chasing from behind.",
					"Being #1 in April is a target. Holding it through district weekends is the actual story.",
					key,
					leader.OrganizationId));
			}
		}

		foreach (NewsTeamSnapshot team in ranked)
		{
			if (Math.Abs(team.Streak) < MinStreak)
			{
				continue;
			}

			int bucket = StreakBucket(Math.Abs(team.Streak));
			if (bucket == 0)
			{
				continue;
			}

			bool winning = team.Streak > 0;
			if (winning && team.OrganizationId == leader.OrganizationId && leader.Games >= MinGamesForRankings)
			{
				continue;
			}

			string key = $"{(winning ? "win" : "loss")}-streak:{team.OrganizationId}:{world.SeasonYear}:{bucket}";
			if (!seen.Add(key))
			{
				continue;
			}

			if (winning)
			{
				int districtRank = DistrictRank(world, team);
				stories.Add(Story(
					world.Date,
					NewsTag.District,
					$"{team.ShortName} rides a {StreakLabel(team.Streak)}.",
					$"{team.ShortName} is {team.RecordLabel} in the {team.DistrictName} after {bucket} straight wins. The district race is moving with them.",
					districtRank == 1
						? $"That streak has {team.ShortName} first in the {team.DistrictName}."
						: $"{team.ShortName} is {Ordinal.Format(districtRank)} in the {team.DistrictName}. Keep this up and the seed changes.",
					key,
					team.OrganizationId));
			}
			else
			{
				stories.Add(Story(
					world.Date,
					NewsTag.Note,
					$"{team.ShortName} drops {bucket} straight, sits {team.RecordLabel}.",
					$"The skid has {team.ShortName} looking up in the {team.DistrictName}. Boards notice losing streaks that last a week.",
					team.Games >= MinGamesForCoachHeat && team.WinningPercentage <= CoachHeatWinPct
						? "This is the stretch that turns into a coaching question if it does not stop."
						: "One weekend can end a skid. Two more make it a story the district repeats.",
					key,
					team.OrganizationId));
			}
		}
	}

	private static void AddProjectedField(List<NewsStory> stories, NewsWorld world, HashSet<string> seen)
	{
		if (world.Date != SeasonCalendar.RegularSeasonEnd(world.SeasonYear))
		{
			return;
		}

		string key = $"playoff-field:{world.SeasonYear}";
		if (!seen.Add(key))
		{
			return;
		}

		List<NewsTeamSnapshot> field = Qualifiers(world);
		if (field.Count < 8)
		{
			return;
		}

		stories.Add(Story(
			world.Date,
			NewsTag.Playoffs,
			"If the regional started today, this would be the field.",
			PlayoffBody(world),
			PlayoffPull(world),
			key));
	}

	private static void AddCoachHeat(List<NewsStory> stories, NewsWorld world, HashSet<string> seen)
	{
		NewsTeamSnapshot? worst = null;
		foreach (NewsTeamSnapshot team in world.Teams)
		{
			if (team.Games < MinGamesForCoachHeat)
			{
				continue;
			}

			if (team.WinningPercentage > CoachHeatWinPct && team.Losses < team.Wins + CoachHeatLossGap)
			{
				continue;
			}

			if (worst == null
				|| team.WinningPercentage < worst.WinningPercentage
				|| (team.WinningPercentage == worst.WinningPercentage && team.Losses > worst.Losses))
			{
				worst = team;
			}
		}

		if (worst == null)
		{
			return;
		}

		string key = $"coach-heat:{worst.OrganizationId}:{world.SeasonYear}";
		if (!seen.Add(key))
		{
			return;
		}

		stories.Add(Story(
			world.Date,
			NewsTag.Coach,
			$"{worst.ShortName} coach under pressure after {worst.RecordLabel} start.",
			$"{worst.ShortName} is {worst.RecordLabel} in the {worst.DistrictName}. The board's minimum is a competitive season, and this is not it.",
			"Programs that sit this far under .500 in April start hearing from the people who hired the coach.",
			key,
			worst.OrganizationId));
	}

	private static void AddFreshmen(List<NewsStory> stories, NewsWorld world, HashSet<string> seen)
	{
		NewsPlayerSnapshot? hitter = null;
		foreach (NewsPlayerSnapshot player in world.Players)
		{
			if (player.Grade != PlayerGrade.Freshman || player.AtBats < MinAtBatsForFreshman)
			{
				continue;
			}

			if (player.Average < FreshmanAverage && player.HomeRuns < 4)
			{
				continue;
			}

			if (hitter == null
				|| player.Average > hitter.Average
				|| (player.Average == hitter.Average && player.HomeRuns > hitter.HomeRuns))
			{
				hitter = player;
			}
		}

		if (hitter != null)
		{
			string key = $"freshman:{hitter.PlayerId:N}";
			if (seen.Add(key))
			{
				string avg = hitter.Average.ToString(".000", CultureInfo.InvariantCulture);
				stories.Add(Story(
					world.Date,
					NewsTag.Freshman,
					$"Freshman {hitter.FullName} emerging at {hitter.TeamShortName}.",
					$"{hitter.FullName} is hitting {avg} as a first-year varsity regular in the {hitter.DistrictName}, with {hitter.HomeRuns} home runs and {hitter.Rbi} RBI.",
					"Freshmen who stick in the middle of a lineup this early become the program's next four years.",
					key,
					hitter.OrganizationId,
					hitter.FullName));
			}
		}

		NewsPlayerSnapshot? arm = null;
		foreach (NewsPlayerSnapshot player in world.Players)
		{
			if (player.Grade != PlayerGrade.Freshman || player.Innings < FreshmanAceInnings)
			{
				continue;
			}

			if (player.Era > FreshmanAceEra)
			{
				continue;
			}

			if (arm == null || player.Era < arm.Era)
			{
				arm = player;
			}
		}

		if (arm != null)
		{
			string key = $"freshman-arm:{arm.PlayerId:N}";
			if (seen.Add(key))
			{
				stories.Add(Story(
					world.Date,
					NewsTag.Pitching,
					$"Freshman {arm.FullName} is already a Friday option at {arm.TeamShortName}.",
					$"{arm.FullName} has a {arm.Era.ToString("0.00", CultureInfo.InvariantCulture)} ERA over {arm.Innings.ToString("0.0", CultureInfo.InvariantCulture)} innings as a first-year varsity arm.",
					"A freshman who can throw strikes in April is a rotation piece by the time this class is senior.",
					key,
					arm.OrganizationId,
					arm.FullName));
			}
		}
	}

	private static void AddDistrictRaces(List<NewsStory> stories, NewsWorld world, HashSet<string> seen)
	{
		foreach (DistrictIdentity district in world.Districts)
		{
			List<NewsTeamSnapshot> field = world.Teams
				.Where(team => team.DistrictId == district.Id)
				.OrderByDescending(team => team.Wins)
				.ThenBy(team => team.Losses)
				.ThenByDescending(team => team.RunDifferential)
				.ToList();
			if (field.Count < 2 || field[0].Games < 10)
			{
				continue;
			}

			NewsTeamSnapshot first = field[0];
			NewsTeamSnapshot second = field[1];
			float gamesBack = ((first.Wins - second.Wins) + (second.Losses - first.Losses)) / 2f;
			if (gamesBack > 1.01f)
			{
				continue;
			}

			string key = $"race:{district.Id}:{world.SeasonYear}";
			if (!seen.Add(key))
			{
				continue;
			}

			stories.Add(Story(
				world.Date,
				NewsTag.Rivalry,
				$"{district.Name} is a two-team race: {first.ShortName} and {second.ShortName}.",
				$"{first.ShortName} is {first.RecordLabel}. {second.ShortName} is {second.RecordLabel}, {FormatGamesBack(gamesBack)} back. The rest of the {district.Name} is looking up.",
				"Top two qualify. A weekend series between these two is a seed, not a tune-up.",
				key,
				first.OrganizationId));
		}
	}

	private static void AddAces(List<NewsStory> stories, NewsWorld world, HashSet<string> seen)
	{
		NewsPlayerSnapshot? ace = null;
		foreach (NewsPlayerSnapshot player in world.Players)
		{
			if (player.Innings < AceInnings || player.Era > AceEra)
			{
				continue;
			}

			if (ace == null || player.Era < ace.Era || (player.Era == ace.Era && player.Strikeouts > ace.Strikeouts))
			{
				ace = player;
			}
		}

		if (ace == null)
		{
			return;
		}

		string key = $"ace:{ace.PlayerId:N}";
		if (!seen.Add(key))
		{
			return;
		}

		stories.Add(Story(
			world.Date,
			NewsTag.Pitching,
			$"{ace.FullName} owns the ERA lead at {ace.TeamShortName}.",
			$"{ace.FullName} is at {ace.Era.ToString("0.00", CultureInfo.InvariantCulture)} across {ace.Innings.ToString("0.0", CultureInfo.InvariantCulture)} innings, with {ace.Strikeouts} strikeouts. Nobody else in the four districts is in that neighborhood.",
			"Friday nights and district titles run through the arm at the front of the rotation.",
			key,
			ace.OrganizationId,
			ace.FullName));
	}

	private static string MidseasonBody(NewsWorld world)
	{
		List<string> leaders = [];
		foreach (DistrictIdentity district in world.Districts)
		{
			NewsTeamSnapshot? first = Ranked(world.Teams.Where(team => team.DistrictId == district.Id)).FirstOrDefault();
			if (first != null && first.Games > 0)
			{
				leaders.Add($"{first.ShortName} in the {district.Name} at {first.RecordLabel}");
			}
		}

		if (leaders.Count == 0)
		{
			return "Boards are checking records, progression, and whether the district race is already two teams.";
		}

		return "First place as of the review: " + string.Join("; ", leaders) + ".";
	}

	private static string PlayoffBody(NewsWorld world)
	{
		var lines = new List<string>();
		foreach (DistrictIdentity district in world.Districts)
		{
			List<NewsTeamSnapshot> field = Ranked(world.Teams.Where(team => team.DistrictId == district.Id));
			if (field.Count < 2)
			{
				continue;
			}

			lines.Add($"{district.Name}: {field[0].ShortName} and {field[1].ShortName}");
		}

		return lines.Count == 0
			? "Top two from each district qualify."
			: string.Join(". ", lines) + ".";
	}

	private static string PlayoffPull(NewsWorld world)
	{
		List<NewsTeamSnapshot> field = Qualifiers(world);
		if (field.Count < 2)
		{
			return "Eight teams. Two regionals. One state final.";
		}

		return $"{field[0].ShortName} and {field[1].ShortName} headline the board. The other six earned the same ticket.";
	}

	private static List<NewsTeamSnapshot> Qualifiers(NewsWorld world)
	{
		var field = new List<NewsTeamSnapshot>();
		foreach (DistrictIdentity district in world.Districts)
		{
			List<NewsTeamSnapshot> ranked = Ranked(world.Teams.Where(team => team.DistrictId == district.Id));
			for (int i = 0; i < ranked.Count && i < 2; i++)
			{
				field.Add(ranked[i]);
			}
		}

		return field;
	}

	private static List<NewsTeamSnapshot> Ranked(IEnumerable<NewsTeamSnapshot> teams)
	{
		return teams
			.OrderByDescending(team => team.Wins)
			.ThenBy(team => team.Losses)
			.ThenByDescending(team => team.RunDifferential)
			.ThenBy(team => team.ShortName, StringComparer.Ordinal)
			.ToList();
	}

	private static int DistrictRank(NewsWorld world, NewsTeamSnapshot team)
	{
		List<NewsTeamSnapshot> field = Ranked(world.Teams.Where(item => item.DistrictId == team.DistrictId));
		for (int i = 0; i < field.Count; i++)
		{
			if (field[i].OrganizationId == team.OrganizationId)
			{
				return i + 1;
			}
		}

		return field.Count;
	}

	private static int StreakBucket(int length)
	{
		int bucket = 0;
		foreach (int mark in StreakBuckets)
		{
			if (length >= mark)
			{
				bucket = mark;
			}
		}

		return bucket;
	}

	private static string StreakLabel(int streak)
	{
		int n = Math.Abs(streak);
		return streak > 0
			? $"{n}-game winning streak"
			: $"{n}-game losing streak";
	}

	private static string FormatGamesBack(float gamesBack)
	{
		if (gamesBack <= 0.01f)
		{
			return "tied";
		}

		string value = gamesBack.ToString("0.0", CultureInfo.InvariantCulture).Replace(".0", "", StringComparison.Ordinal);
		return value == "1" ? "one game" : $"{value} games";
	}

	private static NewsStory Story(
		DateOnly date,
		string tag,
		string headline,
		string body,
		string pull,
		string key,
		string? organizationId = null,
		string? playerName = null) =>
		new()
		{
			Id = Guid.NewGuid(),
			Date = date,
			Tag = tag,
			Headline = headline,
			Body = body,
			Pull = pull,
			DedupKey = key,
			SubjectOrganizationId = organizationId,
			SubjectPlayerName = playerName,
		};

	private static string GradeWord(PlayerGrade grade) => grade switch
	{
		PlayerGrade.Freshman => "freshman",
		PlayerGrade.Sophomore => "sophomore",
		PlayerGrade.Junior => "junior",
		_ => "senior",
	};
}
