namespace HSBM.Domain;

public enum SeasonGoalTier
{
	Required = 0,
	Target = 1,
	Stretch = 2,
}

public enum GoalProgress
{
	InProgress = 0,
	Met = 1,
	Missed = 2,
}

public sealed record SeasonGoal(
	SeasonGoalTier Tier,
	BoardExpectation Expectation,
	string Title,
	GoalProgress Status);

public sealed record ProgramGoal(string Title);

/// <summary>
/// Record, place, and titles the board uses to grade a season goal.
/// Playoff rungs stay in progress until that trophy date has arrived.
/// </summary>
public sealed record SeasonGoalContext(
	int Wins,
	int Losses,
	int DistrictPlace,
	bool WonDistrict,
	bool WonRegional,
	bool WonState,
	DateOnly CurrentDate,
	int SeasonYear);

/// <summary>
/// The athletic board's short list: a floor, a want, and a dream,
/// plus a handful of program jobs that are not trophy busywork.
/// </summary>
public static class BoardGoals
{
	public const int ProgramGoalCount = 3;

	public static string ScoreLine(int score)
	{
		int clamped = Math.Clamp(score, 5, 99);
		return $"{clamped} \u2014 {SeasonReview.ToSatisfaction(clamped).ToLabel().ToUpperInvariant()}";
	}

	public static string GoalLabel(BoardExpectation expectation) => expectation switch
	{
		BoardExpectation.Rebuild => "Stay Competitive",
		BoardExpectation.WinningSeason => "Winning Record",
		BoardExpectation.Compete => "Compete in District",
		BoardExpectation.DistrictPlayoffs => "Reach Regional",
		BoardExpectation.WinDistrict => "Win District",
		BoardExpectation.StateTitle => "State Championship",
		_ => expectation.ToLabel(),
	};

	public static string TierLabel(SeasonGoalTier tier) => tier switch
	{
		SeasonGoalTier.Required => "Required",
		SeasonGoalTier.Target => "Target",
		SeasonGoalTier.Stretch => "Stretch",
		_ => "Target",
	};

	public static IReadOnlyList<SeasonGoal> SeasonGoals(
		BoardExpectation minimum,
		BoardExpectation target,
		SeasonGoalContext context)
	{
		BoardExpectation required = minimum;
		BoardExpectation want = target < minimum ? minimum : target;
		string stretchTitle = want == BoardExpectation.StateTitle
			? "Repeat as State Champions"
			: GoalLabel(BoardExpectation.StateTitle);

		return
		[
			new(SeasonGoalTier.Required, required, GoalLabel(required), Progress(required, context)),
			new(SeasonGoalTier.Target, want, GoalLabel(want), Progress(want, context)),
			new(SeasonGoalTier.Stretch, BoardExpectation.StateTitle, stretchTitle, Progress(BoardExpectation.StateTitle, context)),
		];
	}

	public static GoalProgress Progress(BoardExpectation expectation, SeasonGoalContext context)
	{
		bool met = SeasonReview.TargetMet(
			expectation,
			context.Wins,
			context.Losses,
			context.DistrictPlace,
			context.WonDistrict,
			context.WonRegional,
			context.WonState);

		DateOnly date = context.CurrentDate;
		int year = context.SeasonYear;
		bool regularOver = date >= SeasonCalendar.RegularSeasonEnd(year);
		bool districtDone = date >= SeasonCalendar.DistrictCelebration(year).Start;
		bool stateDone = date >= SeasonCalendar.ChampionCelebration(year).Start;

		return expectation switch
		{
			BoardExpectation.Rebuild => GoalProgress.Met,
			BoardExpectation.WinningSeason or BoardExpectation.Compete =>
				met ? GoalProgress.Met : regularOver ? GoalProgress.Missed : GoalProgress.InProgress,
			BoardExpectation.DistrictPlayoffs =>
				regularOver
					? (met ? GoalProgress.Met : GoalProgress.Missed)
					: GoalProgress.InProgress,
			BoardExpectation.WinDistrict =>
				districtDone
					? (met ? GoalProgress.Met : GoalProgress.Missed)
					: GoalProgress.InProgress,
			BoardExpectation.StateTitle =>
				stateDone
					? (met ? GoalProgress.Met : GoalProgress.Missed)
					: GoalProgress.InProgress,
			_ => met ? GoalProgress.Met : GoalProgress.InProgress,
		};
	}

	public static IReadOnlyList<ProgramGoal> ProgramGoals(
		int reputation,
		LetterGrade facilities,
		LetterGrade baseballResources)
	{
		var goals = new List<ProgramGoal>(ProgramGoalCount)
		{
			new("Develop 2 underclassmen"),
		};

		if (facilities < LetterGrade.C || baseballResources < LetterGrade.C)
		{
			goals.Add(new("Improve Facilities"));
		}
		else
		{
			goals.Add(new("Maintain Academic Standards"));
		}

		if (ProgramReputation.IsPowerhouse(reputation))
		{
			goals.Add(new("Keep the Pipeline Full"));
		}
		else if (ProgramReputation.IsRebuilding(reputation) || reputation < 40)
		{
			goals.Add(new("Rebuild Pitching Depth"));
		}
		else
		{
			goals.Add(new("Improve Pitching Depth"));
		}

		return goals;
	}

	public static IReadOnlyList<ProgramGoal> ProgramGoals(
		ProgramTalentTier talent,
		LetterGrade facilities,
		int reputation) =>
		ProgramGoals(reputation, facilities, LetterGradeExtensions.FromScore(reputation));
}
