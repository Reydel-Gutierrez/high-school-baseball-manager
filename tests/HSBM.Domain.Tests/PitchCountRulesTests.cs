using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class PitchCountRulesTests
{
	private static readonly DateOnly Monday = new(2026, 3, 2);

	[Theory]
	[InlineData(1, 0)]
	[InlineData(30, 0)]
	[InlineData(31, 1)]
	[InlineData(50, 1)]
	[InlineData(51, 2)]
	[InlineData(75, 2)]
	[InlineData(76, 3)]
	[InlineData(105, 3)]
	public void Rest_days_follow_the_pitch_count_bands(int pitches, int rest)
	{
		Assert.Equal(rest, PitchCountRules.RestDaysAfter(pitches));
	}

	[Fact]
	public void Eligible_date_counts_full_calendar_days_of_rest()
	{
		// 80 pitches Monday: rest Tue, Wed, Thu; back Friday.
		Assert.Equal(Monday.AddDays(4), PitchCountRules.EligibleOn(Monday, 80));
		// 20 pitches Monday: can go again Tuesday.
		Assert.Equal(Monday.AddDays(1), PitchCountRules.EligibleOn(Monday, 20));

		var workload = new PitcherWorkload(Guid.NewGuid(), Monday, 60);
		Assert.False(workload.IsRested(Monday.AddDays(2)));
		Assert.Equal(1, workload.DaysUntilRested(Monday.AddDays(2)));
		Assert.True(workload.IsRested(Monday.AddDays(3)));
		Assert.Equal(0, workload.DaysUntilRested(Monday.AddDays(5)));
	}

	[Fact]
	public void Workloads_come_from_the_teams_side_of_recent_box_scores()
	{
		Guid ace = Guid.NewGuid();
		Guid reliever = Guid.NewGuid();
		Guid opponentArm = Guid.NewGuid();
		Guid oldStarter = Guid.NewGuid();
		var world = new GameWorld();
		AddGame(world, Monday, home: "north", away: "south",
			homeLines: [Line(ace, 88), Line(reliever, 22)],
			awayLines: [Line(opponentArm, 95)]);
		AddGame(world, Monday.AddDays(-10), home: "north", away: "east",
			homeLines: [Line(oldStarter, 100)],
			awayLines: []);

		Dictionary<Guid, PitcherWorkload> loads = PitcherWorkloads.For(world, "north", Monday.AddDays(2));
		Assert.Equal(88, loads[ace].Pitches);
		Assert.Equal(22, loads[reliever].Pitches);
		Assert.False(loads.ContainsKey(opponentArm));
		Assert.False(loads.ContainsKey(oldStarter));

		HashSet<Guid> resting = PitcherWorkloads.Resting(world, "north", Monday.AddDays(2));
		Assert.Contains(ace, resting);
		Assert.DoesNotContain(reliever, resting);
		Assert.Empty(PitcherWorkloads.Resting(world, "north", Monday.AddDays(4)));
	}

	[Fact]
	public void Two_games_on_one_day_add_up_for_rest()
	{
		Guid arm = Guid.NewGuid();
		var world = new GameWorld();
		AddGame(world, Monday, "north", "south", [Line(arm, 25)], []);
		AddGame(world, Monday, "east", "north", [], [Line(arm, 30)]);

		PitcherWorkload load = PitcherWorkloads.For(world, "north", Monday.AddDays(1))[arm];
		Assert.Equal(55, load.Pitches);
		Assert.Equal(2, load.RestDays);
	}

	private static BoxScorePitchingLine Line(Guid id, int pitches) =>
		new() { PlayerId = id, OutsRecorded = 9, Pitches = pitches };

	private static void AddGame(
		GameWorld world,
		DateOnly date,
		string home,
		string away,
		List<BoxScorePitchingLine> homeLines,
		List<BoxScorePitchingLine> awayLines)
	{
		var game = new ScheduledGame
		{
			Id = Guid.NewGuid(),
			SeasonYear = date.Year,
			Date = date,
			HomeTeamId = home,
			AwayTeamId = away,
			Status = GameStatus.Completed,
		};
		world.Games.Add(game);
		world.BoxScores.Add(new GameBoxScore
		{
			GameId = game.Id,
			HomePitching = homeLines,
			AwayPitching = awayLines,
		});
	}
}
