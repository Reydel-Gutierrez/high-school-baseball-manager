using System.Collections.Generic;

/// <summary>
/// The coach's career desk: letters built from the live inbox and a
/// trophy cabinet built from the career record. Nothing here is scripted.
/// </summary>
public static class CareerDesk
{
	private const int WinsMilestone = 100;

	public static List<CareerMail> MailFor(GameSession session)
	{
		var mail = new List<CareerMail>();
		foreach (InboxMessage item in MessagesDesk.For(session))
		{
			mail.Add(new CareerMail(
				SenderFor(item.Channel),
				item.Channel.ToLabel(),
				item.Headline,
				item.Preview,
				item.Body,
				item.DateLabel,
				item.Unread,
				item));
		}

		return mail;
	}

	public static IReadOnlyList<CareerTrophy> TrophiesFor(GameSession session)
	{
		int wins = session.DisplayCareerWins;
		return
		[
			Title("District Title", session.CareerDistrictTitles, "Win the district race."),
			Title("Regional Championship", session.CareerRegionalTitles, "Win the regional bracket."),
			Title("State Title", session.CareerStateTitles, "The one every cabinet in this state is built around."),
			new(
				$"{WinsMilestone} Career Wins",
				wins >= WinsMilestone ? "REACHED" : $"{wins} / {WinsMilestone}",
				"A long stay and a winning program.",
				wins >= WinsMilestone ? TrophyState.Won : TrophyState.Empty),
		];
	}

	private static CareerTrophy Title(string name, int count, string goal) =>
		count > 0
			? new(name, count == 1 ? "WON" : $"WON ×{count}", goal, TrophyState.Won)
			: new(name, "—", goal, TrophyState.Empty);

	private static string SenderFor(InboxChannel channel) => channel switch
	{
		InboxChannel.Urgent => "Coaching Staff",
		InboxChannel.Scouting => "Scouting Desk",
		InboxChannel.Board => "Athletic Office",
		InboxChannel.Player => "Clubhouse",
		InboxChannel.Career => "Career Desk",
		_ => "Inbox",
	};
}

public enum TrophyState
{
	Won,
	InProgress,
	Empty,
}

/// <summary>A letter on the career desk; <see cref="Source"/> is the inbox item it shows.</summary>
public sealed record CareerMail(
	string From,
	string Role,
	string Subject,
	string Preview,
	string Body,
	string DateLabel,
	bool Unread,
	InboxMessage Source);

public sealed record CareerTrophy(
	string Name,
	string Year,
	string Detail,
	TrophyState State);
