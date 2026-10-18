using HSBM.Domain;

namespace HSBM.Simulation;

/// <summary>
/// Runs player development for the whole league: a practice week every
/// Monday from preseason through the playoffs (following each team's
/// training plan), and the winter jump at the baseball-year rollover.
/// </summary>
public static class DevelopmentDesk
{
	public static bool IsTrainingDay(int seasonYear, DateOnly today)
	{
		if (today.DayOfWeek != DayOfWeek.Monday)
		{
			return false;
		}

		SeasonPhase phase = CareerCalendar.PhaseOn(seasonYear, today);
		return phase is SeasonPhase.Preseason or SeasonPhase.RegularSeason or SeasonPhase.Postseason;
	}

	/// <summary>One practice week for every rostered player. Returns practice-injury notices.</summary>
	public static List<PlayerTransaction> Weekly(CareerState state, DateOnly today, ISeededRandom rng)
	{
		var posted = new List<PlayerTransaction>();
		int year = state.Career.CurrentSeasonYear;
		if (!IsTrainingDay(year, today))
		{
			return posted;
		}

		Dictionary<string, int> teamGames = TeamGames(state, year);
		Dictionary<Guid, (int Pa, int Outs)> usage = Usage(state, year);
		foreach (Player player in state.Players)
		{
			if (player.Alumni || player.IsUnsigned || string.IsNullOrEmpty(player.TeamId))
			{
				continue;
			}

			if (player.SeasonStartOverall <= 0)
			{
				player.SeasonStartOverall = player.Overall;
			}

			if (player.Injury != null && player.Injury.ActiveOn(today) && !player.Injury.DayToDay)
			{
				continue;
			}

			TeamTraining plan = state.TrainingFor(player.TeamId);
			LetterGrade facilities = Facilities(state, player.OrganizationId);
			double share = PlayingShare(player, teamGames, usage);
			ISeededRandom personal = rng.Derive(Salt(player.Id));
			PlayerDevelopment.Weekly(player, plan.Focus, plan.Intensity, facilities, share, personal);

			if (player.Injury == null && personal.NextDouble() < PlayerDevelopment.PracticeInjuryChance(plan.Intensity))
			{
				PlayerTransaction? hurt = InjuryDesk.FromPractice(state, player, today, personal);
				if (hurt != null)
				{
					posted.Add(hurt);
				}
			}
		}

		return posted;
	}

	/// <summary>
	/// Winter growth, applied at rollover after seniors graduate and before
	/// grades advance. Each player's "season start" baseline is captured
	/// first so the offseason jump shows up as this year's progress.
	/// </summary>
	public static void Offseason(CareerState state, int completedSeasonYear, ISeededRandom rng)
	{
		Dictionary<string, int> teamGames = TeamGames(state, completedSeasonYear);
		Dictionary<Guid, (int Pa, int Outs)> usage = Usage(state, completedSeasonYear);
		foreach (Player player in state.Players)
		{
			if (player.Alumni || player.IsUnsigned)
			{
				continue;
			}

			player.SeasonStartOverall = player.Overall;
			LetterGrade facilities = Facilities(state, player.OrganizationId);
			double share = PlayingShare(player, teamGames, usage);
			PlayerDevelopment.Offseason(player, facilities, share, rng.Derive(Salt(player.Id)));
		}
	}

	public static double PlayingShare(
		Player player,
		IReadOnlyDictionary<string, int> teamGames,
		IReadOnlyDictionary<Guid, (int Pa, int Outs)> usage)
	{
		int games = teamGames.TryGetValue(player.TeamId, out int g) ? g : 0;
		if (games <= 0)
		{
			// Before anyone has played, assume varsity regulars get the reps.
			return player.TeamLevel == TeamLevel.Varsity ? 0.6 : 0.5;
		}

		usage.TryGetValue(player.Id, out (int Pa, int Outs) used);
		if (player.IsPitcher)
		{
			double expectedOuts = games * 21 * 0.25;
			return Math.Clamp(used.Outs / expectedOuts, 0, 1);
		}

		double expectedPa = games * 3.2;
		return Math.Clamp(used.Pa / expectedPa, 0, 1);
	}

	private static Dictionary<string, int> TeamGames(CareerState state, int year)
	{
		var games = new Dictionary<string, int>(StringComparer.Ordinal);
		foreach (ScheduledGame game in state.Games)
		{
			if (game.SeasonYear != year || game.Status != GameStatus.Completed || game.GameType.IsExhibition())
			{
				continue;
			}

			games[game.HomeTeamId] = games.GetValueOrDefault(game.HomeTeamId) + 1;
			games[game.AwayTeamId] = games.GetValueOrDefault(game.AwayTeamId) + 1;
		}

		return games;
	}

	private static Dictionary<Guid, (int Pa, int Outs)> Usage(CareerState state, int year)
	{
		var usage = new Dictionary<Guid, (int Pa, int Outs)>();
		foreach (BattingSeasonStats line in state.BattingStats)
		{
			if (line.SeasonYear == year)
			{
				(int pa, int outs) = usage.GetValueOrDefault(line.PlayerId);
				usage[line.PlayerId] = (pa + line.PlateAppearances, outs);
			}
		}

		foreach (PitchingSeasonStats line in state.PitchingStats)
		{
			if (line.SeasonYear == year)
			{
				(int pa, int outs) = usage.GetValueOrDefault(line.PlayerId);
				usage[line.PlayerId] = (pa, outs + line.OutsRecorded);
			}
		}

		return usage;
	}

	private static LetterGrade Facilities(CareerState state, string organizationId)
	{
		foreach (OrganizationState org in state.Organizations)
		{
			if (org.Id == organizationId)
			{
				return org.Profile.Facilities;
			}
		}

		return LetterGrade.C;
	}

	private static int Salt(Guid id)
	{
		unchecked
		{
			int hash = 17;
			foreach (byte b in id.ToByteArray())
			{
				hash = hash * 31 + b;
			}

			return hash;
		}
	}
}
