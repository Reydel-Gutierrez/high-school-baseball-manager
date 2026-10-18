namespace HSBM.Domain;

/// <summary>What a candidate sells in the interview room.</summary>
public enum CoachingPitch
{
	WinNow = 0,
	Development = 1,
	Culture = 2,
	Recruiting = 3,
}

public sealed record InterviewAnswer(string Text, CoachingPitch Pitch);

public sealed record InterviewQuestion(string Prompt, IReadOnlyList<InterviewAnswer> Answers);

/// <summary>What an athletic director is looking for, read from the program's situation.</summary>
public sealed record ProgramNeeds(CoachingPitch Primary, CoachingPitch Secondary, string Summary);

public sealed record HiringDecision(bool Hired, double Chance, double Fit, string Headline, string Detail);

/// <summary>
/// Head-coach hiring. An athletic director weighs the candidate's name on
/// the circuit against the program's stature, his record, and how well his
/// interview pitch matched what the school needs right now. A first-time
/// head coach is never shut out of the profession entirely: once every
/// other opening has passed on him, the last school standing says yes.
/// </summary>
public static class HiringDesk
{
	public static IReadOnlyList<InterviewQuestion> Questions { get; } =
	[
		new(
			"\"What does year one look like under you?\"",
			[
				new("We compete for the district right away. This roster can win now.", CoachingPitch.WinNow),
				new("Every player on this roster is better in May than in February. Wins follow growth.", CoachingPitch.Development),
				new("We set the standard: practice habits, accountability, how we carry ourselves.", CoachingPitch.Culture),
				new("I'm in every gym and travel field in the county. We change who wants to play here.", CoachingPitch.Recruiting),
			]),
		new(
			"\"Your best player is a senior who skips practice. What do you do?\"",
			[
				new("He plays Friday if he's our best chance to win. I deal with the rest privately.", CoachingPitch.WinNow),
				new("He sits until he earns it back. The underclassmen are watching.", CoachingPitch.Culture),
				new("I sit down with him and find out what's going on. Then we build a plan together.", CoachingPitch.Development),
				new("I make sure the freshmen who are working see that their reps are coming.", CoachingPitch.Recruiting),
			]),
		new(
			"\"Where does this program need to be in three years?\"",
			[
				new("Playing in the state tournament.", CoachingPitch.WinNow),
				new("Sending kids to college ball every spring.", CoachingPitch.Development),
				new("The program every family in town wants their son in.", CoachingPitch.Recruiting),
				new("Known for how we play: hard, clean, together.", CoachingPitch.Culture),
			]),
	];

	/// <summary>Read the room: what does this school want from its next coach?</summary>
	public static ProgramNeeds NeedsFor(BoardExpectation target, int programReputation, LetterGrade rosterOutlook)
	{
		bool strongRoster = rosterOutlook >= LetterGrade.BMinus;
		bool weakRoster = rosterOutlook <= LetterGrade.CMinus;
		bool bigProgram = programReputation >= 65;

		return target switch
		{
			>= BoardExpectation.WinDistrict when strongRoster =>
				new(CoachingPitch.WinNow, CoachingPitch.Culture, "A loaded roster and a board that expects banners."),
			>= BoardExpectation.WinDistrict =>
				new(CoachingPitch.WinNow, CoachingPitch.Recruiting, "The board expects to contend, talent or not."),
			BoardExpectation.DistrictPlayoffs when strongRoster =>
				new(CoachingPitch.WinNow, CoachingPitch.Development, "Enough talent to make a run if someone pushes the right buttons."),
			BoardExpectation.DistrictPlayoffs =>
				new(CoachingPitch.Culture, CoachingPitch.WinNow, "A program that expects playoff baseball and has drifted."),
			BoardExpectation.Rebuild =>
				new(CoachingPitch.Development, bigProgram ? CoachingPitch.Culture : CoachingPitch.Recruiting, "A rebuild. The board wants a teacher, not a miracle."),
			_ when weakRoster =>
				new(CoachingPitch.Development, CoachingPitch.Recruiting, "A thin roster that needs to be built from the ground up."),
			_ when bigProgram =>
				new(CoachingPitch.Culture, CoachingPitch.Development, "A proud program that wants its identity back."),
			_ => new(CoachingPitch.Recruiting, CoachingPitch.Culture, "A small school that has to win the talent battle first."),
		};
	}

	/// <summary>0 (pitched the opposite) to 1 (said exactly what the AD wanted to hear).</summary>
	public static double Fit(IReadOnlyList<CoachingPitch> pitches, ProgramNeeds needs)
	{
		if (pitches.Count == 0)
		{
			return 0.5;
		}

		double score = 0;
		foreach (CoachingPitch pitch in pitches)
		{
			score += pitch == needs.Primary ? 1.0 : pitch == needs.Secondary ? 0.6 : 0.15;
		}

		return Math.Clamp(score / pitches.Count, 0, 1);
	}

	public static double HireChance(
		int coachReputation,
		int programReputation,
		int careerWins,
		int careerLosses,
		double fit,
		bool firstHeadJob)
	{
		double reputationEdge = (CoachReputation.Clamp(coachReputation) - programReputation) / 40.0;
		int games = careerWins + careerLosses;
		double record = games == 0 ? 0 : ((double)careerWins / games - 0.5) * 0.5;
		double chance = 0.45 + reputationEdge + record + (fit - 0.5) * 0.6;
		if (firstHeadJob)
		{
			// Nobody has a résumé yet: the interview carries a rookie.
			chance = Math.Max(chance, 0.25 + 0.35 * fit);
		}

		return Math.Clamp(chance, 0.05, 0.95);
	}

	public static HiringDecision Decide(
		string schoolName,
		int coachReputation,
		int programReputation,
		int careerWins,
		int careerLosses,
		IReadOnlyList<CoachingPitch> pitches,
		ProgramNeeds needs,
		bool firstHeadJob,
		bool lastOpening,
		ISeededRandom rng)
	{
		double fit = Fit(pitches, needs);
		double chance = HireChance(coachReputation, programReputation, careerWins, careerLosses, fit, firstHeadJob);
		if (firstHeadJob && lastOpening)
		{
			chance = 1.0;
		}

		bool hired = rng.NextDouble() < chance;
		string tone = fit switch
		{
			>= 0.8 => "You said exactly what the room wanted to hear.",
			>= 0.55 => "The interview went well enough.",
			_ => "The AD's questions kept circling back to things you didn't say.",
		};

		return hired
			? new HiringDecision(true, chance, fit, $"{schoolName} wants you.", $"{tone} The athletic director called back with an offer.")
			: new HiringDecision(false, chance, fit, $"{schoolName} went another direction.", $"{tone} They hired a candidate with a longer résumé.");
	}
}
