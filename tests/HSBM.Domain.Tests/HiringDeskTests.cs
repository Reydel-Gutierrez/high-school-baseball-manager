using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class HiringDeskTests
{
	private static readonly ProgramNeeds Contender = HiringDesk.NeedsFor(BoardExpectation.WinDistrict, 70, LetterGrade.B);
	private static readonly ProgramNeeds Rebuild = HiringDesk.NeedsFor(BoardExpectation.Rebuild, 40, LetterGrade.D);

	[Fact]
	public void Contenders_want_to_win_now_and_rebuilds_want_development()
	{
		Assert.Equal(CoachingPitch.WinNow, Contender.Primary);
		Assert.Equal(CoachingPitch.Development, Rebuild.Primary);
	}

	[Fact]
	public void Matching_the_program_scores_a_better_fit()
	{
		double match = HiringDesk.Fit([CoachingPitch.WinNow, CoachingPitch.WinNow, CoachingPitch.WinNow], Contender);
		double miss = HiringDesk.Fit([CoachingPitch.Development, CoachingPitch.Development, CoachingPitch.Recruiting], Contender);
		Assert.Equal(1.0, match, 3);
		Assert.True(miss < 0.5);
	}

	[Fact]
	public void Reputation_record_and_fit_all_raise_the_odds()
	{
		double baseline = HiringDesk.HireChance(45, 55, 0, 0, 0.5, firstHeadJob: false);
		Assert.True(HiringDesk.HireChance(65, 55, 0, 0, 0.5, false) > baseline);
		Assert.True(HiringDesk.HireChance(45, 55, 40, 15, 0.5, false) > baseline);
		Assert.True(HiringDesk.HireChance(45, 55, 0, 0, 1.0, false) > baseline);
		Assert.InRange(HiringDesk.HireChance(1, 99, 0, 30, 0, false), 0.05, 0.05);
	}

	[Fact]
	public void First_job_last_opening_always_hires()
	{
		HiringDecision decision = HiringDesk.Decide(
			"Westlake",
			coachReputation: 30,
			programReputation: 60,
			careerWins: 0,
			careerLosses: 0,
			[CoachingPitch.Recruiting, CoachingPitch.Recruiting, CoachingPitch.Recruiting],
			Contender,
			firstHeadJob: true,
			lastOpening: true,
			new SeededRandom(1));
		Assert.True(decision.Hired);
	}

	[Fact]
	public void Every_interview_has_three_questions_with_distinct_pitches()
	{
		Assert.Equal(3, HiringDesk.Questions.Count);
		Assert.All(HiringDesk.Questions, q => Assert.Equal(q.Answers.Count, q.Answers.Select(a => a.Pitch).Distinct().Count()));
	}
}
