using System;
using System.Globalization;
using HSBM.Domain;

/// <summary>
/// Year-end board recap. Pulls record, district, postseason, contract
/// target, reputation, budget, and the program's top / breakout players
/// into one sheet the June review screen can present.
/// </summary>
public static class SeasonReviewDesk
{
	public static SeasonReviewSheet For(GameSession session)
	{
		SchoolSnapshot school = SchoolProfiles.For(DistrictHubData.UserTeamId, TeamLevel.Varsity, session);
		OrganizationState? program = session.RecruitingSchool(session.CatalogOrganizationId);
		ProgramProfile? profile = program?.Profile;
		BoardExpectation target = profile?.TargetExpectation ?? BoardExpectation.DistrictPlayoffs;
		int wins = session.Wins;
		int losses = session.Losses;
		int place = school.DistrictPlace;
		bool playoffs = place <= 4 || session.WonDistrict;
		bool targetMet = SeasonReview.TargetMet(
			target,
			wins,
			losses,
			place,
			session.WonDistrict,
			session.WonRegional,
			session.WonState);
		int satisfactionBefore = session.BoardSatisfactionScore;
		int satisfactionAfter = SeasonReview.ClosingSatisfaction(
			satisfactionBefore,
			wins,
			losses,
			place,
			session.WonDistrict,
			session.WonRegional,
			session.WonState,
			targetMet);
		int reputationGain = SeasonReview.ReputationGain(
			wins > losses,
			playoffs,
			session.WonDistrict,
			session.WonRegional,
			session.WonState,
			targetMet);
		int reputationBefore = profile?.Reputation
			?? Math.Clamp(school.Reputation - reputationGain, 20, 95);
		int reputationAfter = SeasonReview.ClosingReputation(reputationBefore, reputationGain);
		int finance = SeasonReview.FinanceDelta(
			wins,
			losses,
			playoffs,
			session.WonDistrict,
			session.WonRegional,
			session.WonState);
		(SeasonReviewPlayer top, SeasonReviewPlayer breakout) = PickPlayers(session);

		return new SeasonReviewSheet(
			GameSession.SeasonYear,
			session.Organization.Name.ToUpperInvariant(),
			session.RecordLabel,
			Ordinal.Format(place),
			place,
			SeasonReview.PostseasonLabel(session.WonDistrict, session.WonRegional, session.WonState, place),
			SeasonReview.TargetLabel(target),
			targetMet,
			satisfactionBefore,
			satisfactionAfter,
			reputationBefore,
			reputationAfter,
			finance,
			SeasonReview.FormatMoneyDelta(finance),
			top,
			breakout,
			"CONTINUE TO OFFSEASON");
	}

	public static bool PendingOn(GameSession session) =>
		!session.SeasonReviewAcknowledged
		&& SeasonCalendar.BoardReviewOn(GameSession.SeasonYear, session.CurrentDate);

	public static void Commit(GameSession session) =>
		session.ApplySeasonReview(For(session));

	private static (SeasonReviewPlayer Top, SeasonReviewPlayer Breakout) PickPlayers(GameSession session)
	{
		ClubhousePlayer? top = FindAwardedRegular(session) ?? BestOverall();
		ClubhousePlayer? breakout = PickBreakout(top);
		return (ToPlayer(top, topLine: true), ToPlayer(breakout, topLine: false));
	}

	private static ClubhousePlayer? FindAwardedRegular(GameSession session)
	{
		AwardsCeremony banquet = AwardsDesk.For(session, AwardScope.District);
		foreach (AwardHonor honor in banquet.Honors)
		{
			if (honor.IsCoachAward || honor.IsTeamAward || honor.Winner == null)
			{
				continue;
			}

			if (!string.Equals(honor.Winner.TeamId, DistrictHubData.UserTeamId, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			ClubhousePlayer? match = FindByName(honor.Winner.Name);
			if (match != null)
			{
				return match;
			}
		}

		return null;
	}

	private static ClubhousePlayer? BestOverall()
	{
		ClubhousePlayer? best = null;
		foreach (ClubhousePlayer player in ClubhouseSquad.ForTeam(TeamLevel.Varsity))
		{
			if (best == null || player.Overall > best.Overall)
			{
				best = player;
			}
		}

		return best;
	}

	private static ClubhousePlayer? PickBreakout(ClubhousePlayer? top)
	{
		ClubhousePlayer? pick = null;
		int bestScore = int.MinValue;
		foreach (ClubhousePlayer player in ClubhouseSquad.ForTeam(TeamLevel.Varsity))
		{
			if (top != null && string.Equals(player.FullName, top.FullName, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (player.Year.Equals("SR", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			int score = BreakoutScore(player);
			if (score > bestScore)
			{
				bestScore = score;
				pick = player;
			}
		}

		if (pick != null)
		{
			return pick;
		}

		foreach (ClubhousePlayer player in ClubhouseSquad.ForTeam(TeamLevel.Varsity))
		{
			if (top != null && string.Equals(player.FullName, top.FullName, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			if (pick == null || player.Overall > pick.Overall)
			{
				pick = player;
			}
		}

		return pick;
	}

	private static int BreakoutScore(ClubhousePlayer player)
	{
		PlayerProfileSnapshot? profile = PlayerProfiles.Find(
			DistrictHubData.UserTeamId,
			player.FullName,
			TeamLevel.Varsity);
		int potential = profile?.Potential ?? player.Overall;
		int score = (potential - player.Overall) * 3 + player.Overall;
		if (profile != null && profile.Development.Equals("Breakout", StringComparison.OrdinalIgnoreCase))
		{
			score += 25;
		}

		if (player.Year.Equals("FR", StringComparison.OrdinalIgnoreCase))
		{
			score += 4;
		}
		else if (player.Year.Equals("SO", StringComparison.OrdinalIgnoreCase))
		{
			score += 2;
		}

		return score;
	}

	private static ClubhousePlayer? FindByName(string fullName)
	{
		foreach (ClubhousePlayer player in ClubhouseSquad.ForTeam(TeamLevel.Varsity))
		{
			if (string.Equals(player.FullName, fullName, StringComparison.OrdinalIgnoreCase))
			{
				return player;
			}
		}

		return null;
	}

	private static SeasonReviewPlayer ToPlayer(ClubhousePlayer? player, bool topLine)
	{
		if (player == null)
		{
			return new SeasonReviewPlayer("—", "", "", "No varsity regular named.", null);
		}

		return new SeasonReviewPlayer(
			player.FullName,
			player.Position,
			player.Year,
			topLine ? TopLine(player) : BreakoutLine(player),
			player.PortraitPath);
	}

	private static string TopLine(ClubhousePlayer player)
	{
		if (player.Pitching != null && (player.Hitting == null || player.IsPitcher))
		{
			PitchingLine line = player.Pitching;
			return string.Create(
				CultureInfo.InvariantCulture,
				$"{player.Position}  ·  {player.Year}  ·  {line.Wins}-{line.Losses}, {line.Era:0.00} ERA");
		}

		if (player.Hitting != null)
		{
			HittingLine line = player.Hitting;
			return string.Create(
				CultureInfo.InvariantCulture,
				$"{player.Position}  ·  {player.Year}  ·  {FormatAvg(line.Average)} AVG, {line.HomeRuns} HR");
		}

		return $"{player.Position}  ·  {player.Year}  ·  {player.Overall} OVR";
	}

	private static string BreakoutLine(ClubhousePlayer player)
	{
		PlayerProfileSnapshot? profile = PlayerProfiles.Find(
			DistrictHubData.UserTeamId,
			player.FullName,
			TeamLevel.Varsity);
		string growth = profile == null
			? $"{player.Overall} OVR"
			: $"{player.Overall} OVR  ·  {profile.Potential} POT  ·  {profile.Development}";
		return $"{player.Position}  ·  {player.Year}  ·  {growth}";
	}

	private static string FormatAvg(float value)
	{
		string raw = value.ToString("0.000", CultureInfo.InvariantCulture);
		return raw.StartsWith('0') ? raw[1..] : raw;
	}
}

public sealed record SeasonReviewSheet(
	int SeasonYear,
	string SchoolName,
	string RecordLabel,
	string DistrictPlaceLabel,
	int DistrictPlace,
	string PostseasonLabel,
	string TargetLabel,
	bool TargetMet,
	int SatisfactionBefore,
	int SatisfactionAfter,
	int ReputationBefore,
	int ReputationAfter,
	int FinanceDelta,
	string FinanceLabel,
	SeasonReviewPlayer TopPlayer,
	SeasonReviewPlayer BreakoutPlayer,
	string ContinueLabel);

public sealed record SeasonReviewPlayer(
	string Name,
	string Position,
	string Year,
	string Line,
	string? PortraitPath);
