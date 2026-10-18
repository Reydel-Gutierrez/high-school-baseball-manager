using HSBM.Domain;
using HSBM.Simulation;

namespace HSBM.Simulation.Tests;

public class SeasonProgressionTests
{
	[Fact]
	public void Regular_season_games_update_standings_without_ties()
	{
		CareerState state = CareerWorldRunner.NewCareer(4242);
		DateOnly opening = SeasonCalendar.OpeningDay(2026);
		LeagueSimulationResult progressed = CareerWorldRunner.AdvanceUntil(
			state,
			(live, _) => live.Career.CurrentDate > opening.AddDays(21),
			maxDays: 80);

		List<ScheduledGame> counting = state.Games
			.Where(game =>
				game.SeasonYear == 2026
				&& game.Status == GameStatus.Completed
				&& game.TeamLevel == TeamLevel.Varsity
				&& game.GameType.CountsInStandings())
			.ToList();
		Assert.True(counting.Count >= 16, $"Expected counting games after opening week, found {counting.Count}.");
		Assert.All(counting, game =>
		{
			Assert.NotEqual(game.HomeScore, game.AwayScore);
			Assert.True(game.InningsPlayed >= BaseballSimulationConfig.Default.RegulationInnings);
			Assert.False(string.IsNullOrWhiteSpace(game.WinningTeamId));
			Assert.NotNull(state.BoxScoreFor(game.Id));
		});

		List<StandingEntry> standings = LeagueStandings.FromWorld(state.World, 2026);
		Assert.Equal(32, standings.Count);
		Assert.Contains(standings, row => row.Wins + row.Losses > 0);
		int standingWins = standings.Sum(row => row.Wins);
		int standingLosses = standings.Sum(row => row.Losses);
		Assert.Equal(counting.Count, standingWins);
		Assert.Equal(counting.Count, standingLosses);
		Assert.True(progressed.IntegrityIssues.Count == 0, string.Join(Environment.NewLine, progressed.IntegrityIssues));
	}

	[Fact]
	public void One_full_season_archives_a_state_champion()
	{
		CareerState state = CareerWorldRunner.NewCareer(17);
		LeagueSimulationResult season = CareerWorldRunner.RunSeasons(state, 1);
		Assert.Equal(1, season.SeasonsCompleted);
		Assert.Equal(2027, state.Career.CurrentSeasonYear);
		Assert.Equal(new DateOnly(2027, 1, 5), state.Career.CurrentDate);
		Assert.Single(season.Seasons);
		Assert.False(string.IsNullOrWhiteSpace(season.Seasons[0].StateChampion));
		HistoricalSeason? archive = SeasonArchive.ForYear(state.World, 2026);
		Assert.NotNull(archive);
		Assert.Contains(archive!.Champions, row => row.Title == "State");
		Assert.Contains(state.Players, player => player.Alumni && player.GraduatedYear == 2026);
		Assert.Contains(state.Games, game => game.SeasonYear == 2027 && game.Status == GameStatus.Scheduled);
		Assert.True(season.IntegrityIssues.Count == 0, string.Join(Environment.NewLine, season.IntegrityIssues));
	}
}
