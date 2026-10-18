using HSBM.Application;
using HSBM.Domain;
using HSBM.Persistence;
using HSBM.Simulation;

namespace HSBM.Persistence.Tests;

public class DevelopmentPersistenceTests
{
	private static string NewDb() => Path.Combine(Path.GetTempPath(), $"hsbm-dev-{Guid.NewGuid():N}.db");

	private static void TryDelete(string path)
	{
		try
		{
			Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch (IOException)
		{
		}
	}

	[Fact]
	public void Training_plan_and_development_fields_survive_a_reload()
	{
		string path = NewDb();
		try
		{
			string teamId;
			Guid playerId;
			using (var store = new SqliteCareerStore(path))
			using (CareerService service = CareerService.Create(store))
			{
				service.CreateCareer(new CreateCareerRequest { Name = "Dev Test", Seed = 777 });
				CareerState state = service.LoadCareer();
				teamId = state.Teams.First(t => t.Level == TeamLevel.Varsity && t.RosterCap > 0).Id;
				Player player = state.Players.First(p => p.TeamId == teamId);
				playerId = player.Id;
				player.DevelopmentProgress = 73;
				player.SeasonStartRating = 4125;
				store.ReplaceAll(state);
				service.SetTraining(teamId, TrainingFocus.Defense, TrainingIntensity.Hard);
			}

			using (var store = new SqliteCareerStore(path))
			using (CareerService reopened = CareerService.Create(store))
			{
				CareerState loaded = reopened.LoadCareer();
				TeamTraining plan = loaded.TrainingFor(teamId);
				Assert.Equal(TrainingFocus.Defense, plan.Focus);
				Assert.Equal(TrainingIntensity.Hard, plan.Intensity);
				Player player = loaded.PlayerById(playerId)!;
				Assert.Equal(73, player.DevelopmentProgress);
				Assert.Equal(4125, player.SeasonStartRating);
			}
		}
		finally
		{
			TryDelete(path);
		}
	}

	[Fact]
	public void A_season_of_practice_raises_league_talent()
	{
		string path = NewDb();
		try
		{
			using var store = new SqliteCareerStore(path);
			using CareerService service = CareerService.Create(store);
			service.CreateCareer(new CreateCareerRequest { Name = "Dev Season", Seed = 4040 });
			CareerState before = service.LoadCareer();
			double startAverage = before.Players.Where(p => !p.IsUnsigned && !p.Alumni && p.Potential > p.Overall).Average(p => p.Overall);
			var tracked = before.Players.Where(p => !p.IsUnsigned && !p.Alumni && p.Potential > p.Overall).Select(p => p.Id).ToHashSet();

			// Run through the season's practice weeks (Feb through May).
			int year = before.Career.CurrentSeasonYear;
			DateOnly stop = new(year, 5, 31);
			int guard = 0;
			while (service.LoadCareer().Career.CurrentDate < stop && guard++ < 200)
			{
				service.AdvanceDay();
			}

			CareerState after = service.LoadCareer();
			double endAverage = after.Players.Where(p => tracked.Contains(p.Id)).Average(p => p.Overall);
			Assert.True(endAverage > startAverage + 0.5, $"league did not develop: {startAverage:0.00} -> {endAverage:0.00}");
		}
		finally
		{
			TryDelete(path);
		}
	}
}
