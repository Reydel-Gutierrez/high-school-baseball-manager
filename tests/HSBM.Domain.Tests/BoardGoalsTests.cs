using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class BoardGoalsTests
{
	[Fact]
	public void Score_line_pairs_the_number_with_the_band()
	{
		Assert.Equal("76 — GOOD", BoardGoals.ScoreLine(76));
		Assert.Equal("62 — AVERAGE", BoardGoals.ScoreLine(62));
		Assert.Equal("32 — POOR", BoardGoals.ScoreLine(32));
	}

	[Fact]
	public void Typical_board_is_winning_record_regional_and_state()
	{
		IReadOnlyList<SeasonGoal> goals = BoardGoals.SeasonGoals(
			BoardExpectation.WinningSeason,
			BoardExpectation.DistrictPlayoffs,
			AprilContext());

		Assert.Equal(3, goals.Count);
		Assert.Equal("Required", BoardGoals.TierLabel(goals[0].Tier));
		Assert.Equal("Winning Record", goals[0].Title);
		Assert.Equal(GoalProgress.Met, goals[0].Status);
		Assert.Equal("Reach Regional", goals[1].Title);
		Assert.Equal(GoalProgress.InProgress, goals[1].Status);
		Assert.Equal("State Championship", goals[2].Title);
		Assert.Equal(GoalProgress.InProgress, goals[2].Status);
	}

	[Fact]
	public void Stretch_is_always_state_and_does_not_add_busywork_rungs()
	{
		IReadOnlyList<SeasonGoal> goals = BoardGoals.SeasonGoals(
			BoardExpectation.DistrictPlayoffs,
			BoardExpectation.WinDistrict,
			AprilContext());

		Assert.Equal("Reach Regional", goals[0].Title);
		Assert.Equal("Win District", goals[1].Title);
		Assert.Equal("State Championship", goals[2].Title);
	}

	[Fact]
	public void Powerhouse_stretch_is_a_repeat_not_a_fourth_trophy()
	{
		IReadOnlyList<SeasonGoal> goals = BoardGoals.SeasonGoals(
			BoardExpectation.WinDistrict,
			BoardExpectation.StateTitle,
			AprilContext());

		Assert.Equal("Win District", goals[0].Title);
		Assert.Equal("State Championship", goals[1].Title);
		Assert.Equal("Repeat as State Champions", goals[2].Title);
		Assert.Equal(GoalProgress.InProgress, goals[2].Status);
	}

	[Fact]
	public void State_title_stays_open_until_the_trophy_date()
	{
		var won = new SeasonGoalContext(20, 6, 1, true, true, true, new DateOnly(2026, 5, 22), 2026);
		Assert.Equal(GoalProgress.InProgress, BoardGoals.Progress(BoardExpectation.StateTitle, won));

		var after = won with { CurrentDate = new DateOnly(2026, 5, 23) };
		Assert.Equal(GoalProgress.Met, BoardGoals.Progress(BoardExpectation.StateTitle, after));

		var missed = after with { WonState = false };
		Assert.Equal(GoalProgress.Missed, BoardGoals.Progress(BoardExpectation.StateTitle, missed));
	}

	[Fact]
	public void Regional_rung_waits_for_the_regular_season_to_end()
	{
		SeasonGoalContext april = AprilContext() with { DistrictPlace = 1 };
		Assert.Equal(GoalProgress.InProgress, BoardGoals.Progress(BoardExpectation.DistrictPlayoffs, april));

		SeasonGoalContext after = april with { CurrentDate = SeasonCalendar.RegularSeasonEnd(2026) };
		Assert.Equal(GoalProgress.Met, BoardGoals.Progress(BoardExpectation.DistrictPlayoffs, after));
	}

	[Fact]
	public void Program_goals_stay_at_three_and_match_an_average_school()
	{
		IReadOnlyList<ProgramGoal> goals = BoardGoals.ProgramGoals(
			ProgramTalentTier.Average,
			LetterGrade.C,
			58);

		Assert.Equal(BoardGoals.ProgramGoalCount, goals.Count);
		Assert.Equal("Develop 2 underclassmen", goals[0].Title);
		Assert.Equal("Maintain Academic Standards", goals[1].Title);
		Assert.Equal("Improve Pitching Depth", goals[2].Title);
	}

	[Fact]
	public void Weak_programs_swap_in_facilities_and_pitching_rebuild()
	{
		IReadOnlyList<ProgramGoal> goals = BoardGoals.ProgramGoals(
			ProgramTalentTier.Rebuilding,
			LetterGrade.CMinus,
			28);

		Assert.Equal(3, goals.Count);
		Assert.Equal("Improve Facilities", goals[1].Title);
		Assert.Equal("Rebuild Pitching Depth", goals[2].Title);
	}

	private static SeasonGoalContext AprilContext() =>
		new(18, 4, 1, true, true, true, new DateOnly(2026, 4, 24), 2026);
}
