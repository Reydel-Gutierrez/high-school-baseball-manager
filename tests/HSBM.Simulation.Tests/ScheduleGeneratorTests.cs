using HSBM.Domain;
using HSBM.Simulation;

namespace HSBM.Simulation.Tests;

public class ScheduleGeneratorTests
{
	[Fact]
	public void District_games_land_in_district_play_and_non_district_before_that()
	{
		LeagueCatalog catalog = LeagueCatalog.Standard;
		List<ScheduledGame> games = ScheduleGenerator.Generate(
			catalog,
			catalog.Teams,
			2026,
			new SeededRandom(42));

		DateOnly districtStart = SeasonCalendar.ById(2026, "district-play").Start;
		DateOnly districtEnd = SeasonCalendar.RegularSeasonEnd(2026);
		SeasonEvent nonDistrict = SeasonCalendar.NonDistrict(2026);
		SeasonEvent preseason = SeasonCalendar.Preseason(2026);

		Assert.Contains(games, game => game.GameType == GameType.District);
		Assert.Contains(games, game => game.GameType == GameType.NonDistrict);
		Assert.Contains(games, game => game.GameType == GameType.Exhibition);
		Assert.All(games.Where(game => game.GameType == GameType.District), game =>
		{
			Assert.InRange(game.Date, districtStart, districtEnd);
		});
		Assert.All(games.Where(game => game.GameType == GameType.NonDistrict), game =>
		{
			Assert.InRange(game.Date, nonDistrict.Start, nonDistrict.End);
		});
		Assert.All(games.Where(game => game.GameType == GameType.Exhibition), game =>
		{
			Assert.True(game.Date >= preseason.Start);
			Assert.True(game.Date <= preseason.End.AddDays(7));
		});
	}

	[Fact]
	public void Each_district_team_plays_home_and_away_inside_the_district()
	{
		LeagueCatalog catalog = LeagueCatalog.Standard;
		List<ScheduledGame> games = ScheduleGenerator.Generate(
			catalog,
			catalog.Teams,
			2026,
			new SeededRandom(7));

		string cascade = catalog.TeamFor("cascade", TeamLevel.Varsity).Id;
		List<ScheduledGame> district = games
			.Where(game => game.TeamLevel == TeamLevel.Varsity
				&& game.GameType == GameType.District
				&& game.Involves(cascade))
			.ToList();
		Assert.Equal(14, district.Count);
		Assert.Equal(7, district.Count(game => game.HomeTeamId == cascade));
		Assert.Equal(7, district.Count(game => game.AwayTeamId == cascade));
	}

	[Fact]
	public void No_team_plays_itself_or_two_games_on_the_same_day()
	{
		LeagueCatalog catalog = LeagueCatalog.Standard;
		List<ScheduledGame> games = ScheduleGenerator.Generate(
			catalog,
			catalog.Teams,
			2026,
			new SeededRandom(99));

		Assert.Equal(games.Count, games.Select(game => game.Id).Distinct().Count());
		Assert.All(games, game =>
		{
			Assert.NotEqual(game.HomeTeamId, game.AwayTeamId);
			Assert.Equal(GameStatus.Scheduled, game.Status);
			Assert.NotEqual(default, game.Date);
		});

		foreach (IGrouping<string, ScheduledGame> teamGames in games
			.SelectMany(game => new[]
			{
				(TeamId: game.HomeTeamId, Game: game),
				(TeamId: game.AwayTeamId, Game: game),
			})
			.GroupBy(row => row.TeamId, row => row.Game))
		{
			Assert.Equal(teamGames.Count(), teamGames.Select(game => game.Date).Distinct().Count());
		}

		Assert.Contains(games, game => game.TeamLevel == TeamLevel.Varsity);
		Assert.Contains(games, game => game.TeamLevel == TeamLevel.JuniorVarsity);
	}
}
