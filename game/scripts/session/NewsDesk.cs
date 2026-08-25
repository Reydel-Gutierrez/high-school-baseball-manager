using System;
using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// Clubhouse wire: condition-triggered stories from the live career, or
/// from the hub snapshot once the regular season is on the calendar.
/// </summary>
public static class NewsDesk
{
	public static IReadOnlyList<NewsStory> For(GameSession session)
	{
		session.EnsureNews();
		return session.News;
	}

	public static NewsStory? BannerOn(GameSession session)
	{
		session.EnsureNews();
		foreach (NewsStory story in session.News)
		{
			if (!story.Read)
			{
				return story;
			}
		}

		return null;
	}

	public static int UnreadCount(GameSession session)
	{
		session.EnsureNews();
		int count = 0;
		foreach (NewsStory story in session.News)
		{
			if (!story.Read)
			{
				count++;
			}
		}

		return count;
	}

	public static NewsWorld FromHub(GameSession session)
	{
		var teams = new List<NewsTeamSnapshot>();
		var players = new List<NewsPlayerSnapshot>();
		if (session.CurrentDate >= SeasonCalendar.OpeningDay(GameSession.SeasonYear))
		{
			foreach (HubTeam team in DistrictHubData.Teams)
			{
				HubDistrict district = DistrictHubData.GetDistrict(team.DistrictId);
				teams.Add(new NewsTeamSnapshot(
					team.Id,
					team.Id,
					team.ShortName,
					team.DistrictId,
					district.Name,
					district.Region,
					team.VarsityWins,
					team.VarsityLosses,
					team.RunsFor,
					team.RunsAgainst,
					ParseStreak(team.VarsityStreak)));
			}

			foreach (HubSquadPlayer player in DistrictHubSquads.AllPlayers(TeamLevel.Varsity))
			{
				HubSeasonHitting? hit = player.CurrentHitting;
				HubSeasonPitching? arm = player.CurrentPitching;
				if (hit == null && arm == null)
				{
					continue;
				}

				HubDistrict district = DistrictHubData.GetDistrict(DistrictHubData.GetTeam(player.TeamId).DistrictId);
				players.Add(new NewsPlayerSnapshot(
					StablePlayerId(player),
					player.Name,
					player.TeamId,
					player.TeamShort,
					district.Name,
					PlayerGradeExtensions.FromAbbrev(player.Year),
					player.Position,
					hit?.AtBats ?? 0,
					hit?.Average ?? 0,
					hit?.HomeRuns ?? 0,
					hit?.Rbi ?? 0,
					arm?.Innings ?? 0,
					arm?.Era ?? 0,
					arm?.Strikeouts ?? 0));
			}
		}

		return new NewsWorld
		{
			Date = session.CurrentDate,
			SeasonYear = GameSession.SeasonYear,
			Teams = teams,
			Players = players,
			Districts = DistrictIdentities(),
		};
	}

	private static IReadOnlyList<DistrictIdentity> DistrictIdentities()
	{
		var list = new List<DistrictIdentity>(DistrictHubData.Districts.Count);
		foreach (HubDistrict district in DistrictHubData.Districts)
		{
			list.Add(new DistrictIdentity(district.Id, district.Name, district.Abbreviation, district.Region));
		}

		return list;
	}

	private static int ParseStreak(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
		{
			return 0;
		}

		char kind = char.ToUpperInvariant(value[0]);
		if (kind is not ('W' or 'L'))
		{
			return 0;
		}

		if (!int.TryParse(value.AsSpan(1), out int length) || length <= 0)
		{
			return 0;
		}

		return kind == 'W' ? length : -length;
	}

	private static Guid StablePlayerId(HubSquadPlayer player)
	{
		string key = $"{player.TeamId}:{player.Name}:{player.Year}:{player.Position}";
		byte[] bytes = System.Text.Encoding.UTF8.GetBytes(key);
		int hash = 17;
		foreach (byte b in bytes)
		{
			hash = (hash * 31) + b;
		}

		var guidBytes = new byte[16];
		BitConverter.GetBytes(hash).CopyTo(guidBytes, 0);
		BitConverter.GetBytes(player.Jersey).CopyTo(guidBytes, 4);
		BitConverter.GetBytes(player.Overall).CopyTo(guidBytes, 8);
		return new Guid(guidBytes);
	}
}
