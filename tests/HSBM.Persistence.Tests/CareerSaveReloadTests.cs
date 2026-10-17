using HSBM.Application;
using HSBM.Domain;
using HSBM.Persistence;
using HSBM.Simulation;

namespace HSBM.Persistence.Tests;

public class CareerSaveReloadTests
{
	[Fact]
	public void Mid_season_results_reload_without_losing_state()
	{
		string path = NewDb();
		try
		{
			string fingerprint;
			DateOnly date;
			int completed;
			int boxScores;
			int battingPa;
			int pitchingOuts;
			int recordGames;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Reload", Seed = 88, UserOrganizationId = "cascade" });
				service.SetCurrentDate(SeasonCalendar.OpeningDay(2026));
				for (int i = 0; i < 8; i++)
				{
					service.AdvanceDay();
				}

				CareerState state = service.LoadCareer();
				date = state.Career.CurrentDate;
				completed = state.Games.Count(game => game.Status == GameStatus.Completed);
				boxScores = state.BoxScores.Count;
				battingPa = state.BattingStats.Sum(row => row.PlateAppearances);
				pitchingOuts = state.PitchingStats.Sum(row => row.OutsRecorded);
				recordGames = state.TeamRecords.Sum(row => row.Wins + row.Losses);
				Assert.True(completed > 0);
				Assert.Equal(completed, boxScores);
				Assert.True(battingPa > 0);
				Assert.True(pitchingOuts > 0);
				fingerprint = Fingerprint(state);
			}

			using (CareerService reopened = Open(path))
			{
				CareerState loaded = reopened.LoadCareer();
				Assert.Equal(date, loaded.Career.CurrentDate);
				Assert.Equal(date, loaded.World.CurrentDate);
				Assert.Equal(completed, loaded.Games.Count(game => game.Status == GameStatus.Completed));
				Assert.Equal(boxScores, loaded.BoxScores.Count);
				Assert.Equal(battingPa, loaded.BattingStats.Sum(row => row.PlateAppearances));
				Assert.Equal(pitchingOuts, loaded.PitchingStats.Sum(row => row.OutsRecorded));
				Assert.Equal(recordGames, loaded.TeamRecords.Sum(row => row.Wins + row.Losses));
				Assert.Equal(fingerprint, Fingerprint(loaded));
				Assert.All(loaded.Games.Where(game => game.Status == GameStatus.Completed), game =>
				{
					Assert.NotEqual(game.HomeScore, game.AwayScore);
					Assert.NotNull(loaded.BoxScoreFor(game.Id));
				});
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void Simulated_season_reloads_archive_and_next_year_schedule()
	{
		string path = NewDb();
		try
		{
			LeagueSimulationResult simulated;
			using (CareerService service = Open(path))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Year", Seed = 31 });
				simulated = service.SimulateSeasons(1);
				Assert.True(simulated.IntegrityIssues.Count == 0, string.Join(Environment.NewLine, simulated.IntegrityIssues));
				Assert.Equal(2027, service.LoadCareer().Career.CurrentSeasonYear);
			}

			using (CareerService reopened = Open(path))
			{
				CareerState loaded = reopened.LoadCareer();
				Assert.Equal(2027, loaded.Career.CurrentSeasonYear);
				Assert.Equal(new DateOnly(2027, 1, 5), loaded.Career.CurrentDate);
				Assert.Equal(new DateOnly(2027, 1, 5), loaded.World.CurrentDate);
				HistoricalSeason archive = Assert.Single(loaded.World.HistoricalSeasons, row => row.SeasonYear == 2026);
				Assert.Contains(archive.Champions, row => row.Title == "State");
				Assert.Equal(32, archive.Standings.Count);
				Assert.Contains(loaded.Players, player => player.Alumni && player.GraduatedYear == 2026);
				Assert.Contains(loaded.Games, game => game.SeasonYear == 2027 && game.Status == GameStatus.Scheduled);
				Assert.Equal(simulated.League.ActivePlayers, loaded.Players.Count(player => player.IsActive));
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	private static CareerService Open(string path) => CareerService.Create(new SqliteCareerStore(path));

	private static string NewDb() =>
		Path.Combine(Path.GetTempPath(), $"hsbm-test-{Guid.NewGuid():N}.db");

	private static void TryDelete(string path)
	{
		foreach (string file in new[] { path, path + "-wal", path + "-shm" })
		{
			try
			{
				if (File.Exists(file))
				{
					File.Delete(file);
				}
			}
			catch (IOException)
			{
			}
		}
	}

	private static string Fingerprint(CareerState state) =>
		string.Join("|",
		[
			$"{state.Career.CurrentDate:yyyy-MM-dd}:{state.Career.CurrentSeasonYear}:{state.Career.RandomSeed}",
			string.Join(",", state.Players.OrderBy(p => p.Id).Select(p =>
				$"{p.Id:N}:{p.TeamId}:{p.Grade}:{p.Overall}:{p.Alumni}:{p.JerseyNumber}")),
			string.Join(",", state.Games.Where(g => g.Status == GameStatus.Completed).OrderBy(g => g.Id).Select(g =>
				$"{g.Id:N}:{g.HomeScore}-{g.AwayScore}:{g.WinningTeamId}")),
			string.Join(",", state.TeamRecords.OrderBy(r => r.TeamId).ThenBy(r => r.SeasonYear).Select(r =>
				$"{r.TeamId}:{r.SeasonYear}:{r.Wins}-{r.Losses}:{r.RunsFor}-{r.RunsAgainst}")),
			string.Join(",", state.BattingStats.OrderBy(s => s.PlayerId).Select(s =>
				$"{s.PlayerId:N}:{s.PlateAppearances}:{s.Hits}:{s.HomeRuns}")),
			string.Join(",", state.PitchingStats.OrderBy(s => s.PlayerId).Select(s =>
				$"{s.PlayerId:N}:{s.OutsRecorded}:{s.Strikeouts}:{s.EarnedRuns}")),
			state.BoxScores.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
		]);
}
