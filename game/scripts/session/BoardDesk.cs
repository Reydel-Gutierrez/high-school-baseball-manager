using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// The sitting board card: numeric satisfaction, three season rungs,
/// and three program jobs. Home shows the score; this page is the source.
/// </summary>
public static class BoardDesk
{
	public static BoardCard For(GameSession session)
	{
		OrganizationState? program = session.LeagueProgram(session.CatalogOrganizationId)
			?? session.SchoolState(session.CatalogOrganizationId);
		ProgramProfile? profile = program?.Profile;
		SchoolSnapshot school = SchoolProfiles.For(DistrictHubData.UserTeamId, TeamLevel.Varsity, session);
		BoardExpectation minimum = profile?.MinimumExpectation ?? BoardExpectation.WinningSeason;
		BoardExpectation target = profile?.TargetExpectation ?? BoardExpectation.DistrictPlayoffs;
		var context = new SeasonGoalContext(
			session.Wins,
			session.Losses,
			school.DistrictPlace,
			session.WonDistrict,
			session.WonRegional,
			session.WonState,
			session.CurrentDate,
			GameSession.SeasonYear);

		return new BoardCard(
			session.BoardSatisfactionScore,
			session.BoardSatisfaction,
			BoardGoals.ScoreLine(session.BoardSatisfactionScore),
			BoardGoals.SeasonGoals(minimum, target, context),
			BoardGoals.ProgramGoals(
				program?.Profile.Reputation ?? 50,
				profile?.Facilities ?? LetterGrade.C,
				profile?.BaseballResources ?? LetterGrade.C));
	}
}

public sealed record BoardCard(
	int SatisfactionScore,
	BoardSatisfaction Satisfaction,
	string ScoreLine,
	IReadOnlyList<SeasonGoal> SeasonGoals,
	IReadOnlyList<ProgramGoal> ProgramGoals);
