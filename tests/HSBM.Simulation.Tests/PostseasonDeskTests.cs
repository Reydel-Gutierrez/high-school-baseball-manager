using HSBM.Domain;
using HSBM.Simulation;

namespace HSBM.Simulation.Tests;

public class PostseasonDeskTests
{
	[Fact]
	public void Selection_day_seeds_eight_and_a_two_oh_series_advances()
	{
		GeneratedWorld generated = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 21, 2026);
		DateOnly selection = SeasonCalendar.PlayoffSelection(2026).Start;
		CareerState state = new()
		{
			Career = new Career
			{
				CurrentSeasonYear = 2026,
				CurrentDate = selection,
				RandomSeed = 21,
				UserOrganizationId = "cascade",
			},
			World = generated.ToGameWorld(selection),
		};
		GiveDistrictSeeds(state);

		PostseasonDesk.Tick(state, selection);
		PostseasonState? bracket = state.World.Postseason;
		Assert.NotNull(bracket);
		Assert.Equal(8, bracket.Qualifiers.Count);
		Assert.Equal(4, bracket.Series.Count(series => series.Round == PostseasonRound.RegionalSemifinal));

		PostseasonSeries series = bracket.Series.First(row => row.Round == PostseasonRound.RegionalSemifinal);
		CompleteSweep(state, series);
		PostseasonDesk.Resolve(state, SeasonCalendar.RegionalSemis(2026).End);

		Assert.True(series.IsComplete);
		Assert.Equal(series.HomeTeamId, series.WinnerTeamId);
		Assert.Contains(
			bracket.Series,
			row => row.Round == PostseasonRound.RegionalSemifinal && !row.IsComplete);
	}

	[Fact]
	public void Both_semis_in_a_region_open_the_regional_final()
	{
		GeneratedWorld generated = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 21, 2026);
		DateOnly selection = SeasonCalendar.PlayoffSelection(2026).Start;
		CareerState state = new()
		{
			Career = new Career
			{
				CurrentSeasonYear = 2026,
				CurrentDate = selection,
				RandomSeed = 21,
				UserOrganizationId = "cascade",
			},
			World = generated.ToGameWorld(selection),
		};
		GiveDistrictSeeds(state);
		PostseasonDesk.Tick(state, selection);
		PostseasonState bracket = state.World.Postseason!;
		foreach (PostseasonSeries series in bracket.SeriesIn(PostseasonRound.RegionalSemifinal, "East"))
		{
			CompleteSweep(state, series);
		}

		PostseasonDesk.Resolve(state, SeasonCalendar.RegionalSemis(2026).End);
		Assert.Single(bracket.SeriesIn(PostseasonRound.RegionalFinal, "East"));
	}

	private static void GiveDistrictSeeds(CareerState state)
	{
		foreach (DistrictIdentity district in state.Districts)
		{
			var varsity = state.Teams
				.Where(team => team.Level == TeamLevel.Varsity
					&& state.OrganizationForTeam(team.Id).DistrictId == district.Id)
				.ToList();
			for (int i = 0; i < varsity.Count; i++)
			{
				TeamSeasonRecord record = state.RecordFor(varsity[i].Id);
				record.SeasonYear = 2026;
				record.Wins = 20 - i;
				record.Losses = i;
				record.RunsFor = 120 - (i * 4);
				record.RunsAgainst = 60 + (i * 3);
			}
		}
	}

	private static void CompleteSweep(CareerState state, PostseasonSeries series)
	{
		for (int i = 0; i < 2; i++)
		{
			var game = new ScheduledGame
			{
				Id = Guid.NewGuid(),
				SeasonYear = 2026,
				Date = SeasonCalendar.RegionalSemis(2026).Start.AddDays(i),
				HomeTeamId = series.HomeTeamId,
				AwayTeamId = series.AwayTeamId,
				TeamLevel = TeamLevel.Varsity,
				GameType = GameType.RegionalTournament,
				Status = GameStatus.Completed,
				HomeScore = 6,
				AwayScore = 2,
				WinningTeamId = series.HomeTeamId,
				LosingTeamId = series.AwayTeamId,
			};
			state.Games.Add(game);
			series.GameIds.Add(game.Id);
		}
	}
}
