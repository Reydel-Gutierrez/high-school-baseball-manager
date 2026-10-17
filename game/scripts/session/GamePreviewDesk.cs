using System;
using System.Globalization;
using HSBM.Domain;

/// <summary>
/// Pregame card for a scheduled contest: records, probable starters, and
/// the three team-comparison numbers the preview screen needs before
/// live sim results exist.
/// </summary>
public static class GamePreviewDesk
{
	public static GamePreviewSheet For(CalendarContest game, GameSession session)
	{
		string userId = session.CatalogOrganizationId;
		string homeId = game.Home ? userId : game.OpponentId;
		string awayId = game.Home ? game.OpponentId : userId;
		return new GamePreviewSheet(
			game,
			homeId,
			awayId,
			TeamName(homeId),
			TeamName(awayId),
			RecordLabel(homeId, game.Level, session),
			RecordLabel(awayId, game.Level, session),
			FormatAvg(DistrictHubData.GetTeam(homeId).TeamAvg),
			FormatAvg(DistrictHubData.GetTeam(awayId).TeamAvg),
			FormatEra(DistrictHubData.GetTeam(homeId).TeamEra),
			FormatEra(DistrictHubData.GetTeam(awayId).TeamEra),
			DefenseLabel(homeId, game.Level, session),
			DefenseLabel(awayId, game.Level, session),
			StarterFor(homeId, game.Level, session),
			StarterFor(awayId, game.Level, session));
	}

	private static string TeamName(string teamId) =>
		DistrictHubData.GetTeam(teamId).Name.ToUpperInvariant();

	private static string RecordLabel(string teamId, TeamLevel level, GameSession session)
	{
		TeamSeasonRecord? live = WorldRecord(session, teamId, level);
		if (live != null)
		{
			return FormatRecord(live.Wins, live.Losses, live.DistrictWins, live.DistrictLosses);
		}

		if (string.Equals(teamId, session.CatalogOrganizationId, StringComparison.OrdinalIgnoreCase)
			&& level == TeamLevel.Varsity)
		{
			return session.RecordLabel;
		}

		HubTeam team = DistrictHubData.GetTeam(teamId);
		return $"{team.Wins(level)}\u2013{team.Losses(level)}";
	}

	private static TeamSeasonRecord? WorldRecord(GameSession session, string organizationId, TeamLevel level)
	{
		if (session.World == null)
		{
			return null;
		}

		string teamId = LeagueCatalog.TeamIdFor(organizationId, level);
		foreach (TeamSeasonRecord record in session.World.TeamRecords)
		{
			if (record.TeamId == teamId && record.SeasonYear == GameSession.SeasonYear)
			{
				return record;
			}
		}

		return null;
	}

	private static string FormatRecord(int wins, int losses, int districtWins, int districtLosses)
	{
		if (districtWins + districtLosses <= 0)
		{
			return $"{wins}\u2013{losses}";
		}

		return $"{wins}\u2013{losses} ({districtWins}\u2013{districtLosses})";
	}

	private static string DefenseLabel(string teamId, TeamLevel level, GameSession session) =>
		SchoolProfiles.For(teamId, level, session).Fielding.ToString(CultureInfo.InvariantCulture);

	private static GamePreviewStarter StarterFor(string teamId, TeamLevel level, GameSession session)
	{
		if (string.Equals(teamId, session.CatalogOrganizationId, StringComparison.OrdinalIgnoreCase))
		{
			ClubhousePlayer? assigned = ClubhouseBoard.GetLikelyStarter(session.GetTeam(level));
			if (assigned != null)
			{
				return FromClubhouse(assigned, teamId);
			}
		}

		HubSquadPlayer? arm = BestArm(teamId, level);
		if (arm != null)
		{
			return FromSquad(arm);
		}

		return new GamePreviewStarter(teamId, "TBD", "RHP", "0\u20130", "0.00", null);
	}

	private static HubSquadPlayer? BestArm(string teamId, TeamLevel level)
	{
		HubSquadPlayer? best = null;
		foreach (HubSquadPlayer player in DistrictHubSquads.Arms(teamId, level))
		{
			HubSeasonPitching? pitching = player.CurrentPitching;
			if (pitching == null)
			{
				continue;
			}

			if (best?.CurrentPitching == null
				|| pitching.GamesStarted > best.CurrentPitching.GamesStarted
				|| (pitching.GamesStarted == best.CurrentPitching.GamesStarted
					&& pitching.Era < best.CurrentPitching.Era))
			{
				best = player;
			}
		}

		return best;
	}

	private static GamePreviewStarter FromClubhouse(ClubhousePlayer player, string teamId)
	{
		PitchingLine? pitching = player.Pitching;
		return new GamePreviewStarter(
			teamId,
			player.FullName,
			Hand(player.Position),
			pitching == null ? "0\u20130" : $"{pitching.Wins}\u2013{pitching.Losses}",
			pitching == null ? "0.00" : FormatEra(pitching.Era),
			player.PortraitPath);
	}

	private static GamePreviewStarter FromSquad(HubSquadPlayer player)
	{
		HubSeasonPitching? pitching = player.CurrentPitching;
		return new GamePreviewStarter(
			player.TeamId,
			player.Name,
			Hand(player.Position),
			pitching == null ? "0\u20130" : $"{pitching.Wins}\u2013{pitching.Losses}",
			pitching == null ? "0.00" : FormatEra(pitching.Era),
			player.PortraitPath);
	}

	private static string Hand(string position) =>
		position.Contains("LHP", StringComparison.OrdinalIgnoreCase) ? "LHP" : "RHP";

	private static string FormatAvg(float avg) => avg.ToString(".000", CultureInfo.InvariantCulture);

	private static string FormatEra(float era) => era.ToString("0.00", CultureInfo.InvariantCulture);
}

public sealed record GamePreviewSheet(
	CalendarContest Game,
	string HomeTeamId,
	string AwayTeamId,
	string HomeName,
	string AwayName,
	string HomeRecord,
	string AwayRecord,
	string HomeAvg,
	string AwayAvg,
	string HomeEra,
	string AwayEra,
	string HomeDef,
	string AwayDef,
	GamePreviewStarter HomeStarter,
	GamePreviewStarter AwayStarter);

public sealed record GamePreviewStarter(
	string TeamId,
	string Name,
	string Hand,
	string Record,
	string Era,
	string? PortraitPath);
