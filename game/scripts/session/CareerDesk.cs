using System.Collections.Generic;

/// <summary>
/// Prototype career desk for the coach: inbox, trophy cabinet, and
/// openings at other programs. Replace with persisted career records
/// when save data is wired up.
/// </summary>
public static class CareerDesk
{
	public const string CoachName = DistrictHubData.UserCoachName;
	public const string CoachInitials = "DH";
	public const string Reputation = "Rising";
	public const string CareerRecord = "53-25";
	public const string ThisYearRecord = "18-4";
	public const int SeasonYear = DistrictHubData.SeasonYear;

	public static readonly IReadOnlyList<CareerMail> Inbox =
	[
		new(
			"mail-ad-checkin",
			"Margaret Keene",
			"Athletic Director, Cascade Regional",
			"April check-in — the board is watching the district race",
			"First-year deal, first place. They like the start. They still want the district.",
			"Daniel,\n\nThe board asked me to put this in writing so there is no fog later.\n\nEighteen and four is a first-year start they did not expect when they moved you off the JV bench. Board satisfaction is Average, which is the middle of the scale — not a warning, not a lock. The main goals on the deal are still the ones that matter: finish .500 or better, and classify for the regional.\n\nFriday against Great Lakes will be the loudest night of the month. If that series holds, the Sideline Cup talk will get louder too. I am not asking you to manage the trophy. I am asking you to manage the week.\n\nWe can go through the contract board whenever you want.\n\nMargaret Keene\nAthletic Director",
			"APR 24",
			true,
			CareerMailKind.Contract,
			null),
		new(
			"mail-pinecrest",
			"Carolyn Pitt",
			"Athletic Director, Pinecrest",
			"Confidential — would you take a call this week?",
			"Four and eighteen. The seat is open. They want a name the district already respects.",
			"Coach Hart,\n\nI am writing you directly because the Pinecrest board voted last night to open the manager's seat for 2027, with a chance to start the transition as soon as this summer.\n\nWe are 4-18. The program has been in the Open Pool conversation for two years. I will not dress that up. What I can offer is a three-year deal, a board that will let you pick the staff, and a school that is done pretending a mid-table finish is the plan.\n\nYour name came up because Cascade is 18-4 and you did that in year one. I know you are in the middle of a district race. I am not asking you to walk out this week. I am asking whether you would take a confidential call.\n\nCarolyn Pitt\nAthletic Director, Pinecrest",
			"APR 23",
			true,
			CareerMailKind.JobOffer,
			"job-pinecrest"),
		new(
			"mail-sideline",
			"East Region Office",
			"District awards desk",
			"Sideline Cup watch: you are the name in front",
			"Hart, Voss, Quinn, Delgado. The cup is still in play.",
			"Coach Hart,\n\nThis is not an official ballot. It is the April watch the region office sends to sitting managers so nobody is surprised in May.\n\nYou are the frontrunner for the Sideline Cup after the 18-4 start and the way Cascade has held first in the Great Lakes. Elena Voss (Great Lakes), Marcus Quinn (Port Clinton), and Ray Delgado (Capital City) are the three names behind you.\n\nThe cup tracks the bench, not one Friday. Keep the district in order and this stays on your shelf to lose.\n\nEast Region Office",
			"APR 22",
			true,
			CareerMailKind.Award,
			null),
		new(
			"mail-times",
			"Priya Shah",
			"Capital City Times",
			"Request: ten minutes after Friday's Great Lakes set",
			"They want the first-year story. You can take it or leave it.",
			"Coach Hart,\n\nI cover the four districts for the Times. I would like ten minutes after Friday's series with Great Lakes — not a live hit, a notebook sit-down.\n\nThe angle is the promotion: JV title in 2024, varsity seat in 2026, first place in April. I will not ask you to talk about the Pinecrest rumor if you do not want it on the record.\n\nIf Friday is the wrong night, I can come Monday.\n\nPriya Shah\nCapital City Times",
			"APR 22",
			true,
			CareerMailKind.Media,
			null),
		new(
			"mail-voss",
			"Elena Voss",
			"Head Coach, Great Lakes",
			"Friday. No extra noise.",
			"A rival who still talks like a coach.",
			"Daniel,\n\nFriday should be loud. I wanted this in your box before the week gets stupid.\n\nYour club has earned first place. We are not coming in to make a speech about it. Romano will have the ball. You will have Marini. That is the game.\n\nSee you at the Regional.\n\nElena",
			"APR 21",
			false,
			CareerMailKind.Rival,
			null),
		new(
			"mail-booster",
			"Tom Alvarez",
			"Cascade Booster Club",
			"The porch lights were on after Harbor Ridge",
			"Parents noticing the way the program is carrying itself.",
			"Coach,\n\nI know you do not need another email. I am sending it anyway.\n\nAfter the Harbor Ridge win the lot stayed full. Parents who used to leave in the fifth are staying through the handshake line. That is not the scoreboard. That is the program.\n\nThe booster club is behind this staff. If you need anything for the Great Lakes series — travel, a meal, a quiet room — you have it.\n\nTom Alvarez\nCascade Booster Club",
			"APR 19",
			false,
			CareerMailKind.Booster,
			null),
		new(
			"mail-lakeside",
			"Hugh Barrow",
			"Athletic Director, Lakeside Academy",
			"Open Pool is not a plan — we need a manager",
			"Seven and fifteen. They want out of the bottom before 2027.",
			"Coach Hart,\n\nLakeside and the district office have already had the Open Pool conversation for 2027. I would rather hire a manager than join a pool.\n\nWe are 7-15. The last staff and the board parted on even terms last week. I have a three-year offer and a board that will accept a rebuild if it is honest.\n\nYou are the first call because you know this district and you have already turned a bench into a race. If Cascade is home, I understand. If you want the keys to a program that will let you work, I would like to talk.\n\nHugh Barrow\nAthletic Director, Lakeside Academy",
			"APR 18",
			false,
			CareerMailKind.JobOffer,
			"job-lakeside"),
	];

	public static readonly IReadOnlyList<CareerTrophy> Trophies =
	[
		new("jv-district-2024", "JV District Title", "2024", "Cascade JV. First year as head coach of that bench.", TrophyState.Won),
		new("rising-2025", "Rising Coach", "2025", "District honor for the JV seat before the varsity promotion.", TrophyState.Won),
		new("sideline-2026", "Sideline Cup", "2026", "Coach of the Year. You are the name in front.", TrophyState.InProgress),
		new("gl-district-2026", "Great Lakes Title", "2026", "Varsity district championship. First place, still in play.", TrophyState.InProgress),
		new("regional", "Regional Championship", "—", "Win the East Regional. Empty until a May run is real.", TrophyState.Empty),
		new("state", "State Title", "—", "The one every cabinet in this state is built around.", TrophyState.Empty),
		new("coty", "Coach of the Year", "—", "The official ballot. Different from the Sideline Cup watch.", TrophyState.Empty),
		new("program", "Program Builder", "—", "A long stay, a full cupboard, and a school that outlasts one hot April.", TrophyState.Empty),
	];

	public static readonly IReadOnlyList<CareerStop> Timeline =
	[
		new(2023, "JV Assistant", "Cascade Regional", "Joined the staff."),
		new(2024, "JV Head Coach", "Cascade Regional", "19-11 · district title."),
		new(2025, "JV Head Coach", "Cascade Regional", "16-10 · promoted in June."),
		new(2026, "Head Coach", "Cascade Regional", "Year 1 of 4 · 18-4."),
	];

	public static readonly IReadOnlyList<CareerJob> Openings =
	[
		new(
			"job-pinecrest",
			"Pinecrest",
			"Great Lakes",
			"4-18",
			"Board opened the seat last night",
			"3-year deal",
			"A free-fall rebuild. Full say on staff, and a board that is done pretending.",
			"REBUILD"),
		new(
			"job-lakeside",
			"Lakeside Academy",
			"Great Lakes",
			"7-15",
			"Mutual split with the last staff",
			"3-year deal",
			"They would rather hire than join the Open Pool. Honest rebuild, same district.",
			"REBUILD"),
		new(
			"job-two-harbors",
			"Two Harbors",
			"Iron Range",
			"4-18",
			"Interim after an April firing",
			"2-year deal",
			"A long drive and a thin cupboard. The board wants a name before summer.",
			"REBUILD"),
		new(
			"job-cloquet",
			"Cloquet",
			"Iron Range",
			"10-12",
			"Coach retiring at the end of the season",
			"4-year deal",
			"A .500 program that wants continuity, not a teardown. The cleanest opening on the board.",
			"FIT"),
	];

	public static CareerJob? FindJob(string id)
	{
		foreach (CareerJob job in Openings)
		{
			if (job.Id == id)
			{
				return job;
			}
		}

		return null;
	}

	public static CareerMail ConfirmationFor(CareerJob job) =>
		new(
			$"mail-applied-{job.Id}",
			job.School + " Athletic Office",
			"Application desk",
			$"We have your name for the {job.School} seat",
			"Application received. They will be in touch after Friday.",
			$"Coach Hart,\n\nThis confirms we have your application for the {job.School} manager's seat ({job.Contract.ToLowerInvariant()}).\n\nThe board will not ask you to leave Cascade in the middle of a district race. If they want a conversation, it will be after Friday's set — and it will stay confidential until you say otherwise.\n\nThank you for trusting us with the inquiry.\n\n{job.School} Athletic Office",
			"APR 24",
			true,
			CareerMailKind.Confirmation,
			job.Id);
}

public enum CareerMailKind
{
	Contract,
	JobOffer,
	Award,
	Media,
	Rival,
	Booster,
	Confirmation,
}

public enum TrophyState
{
	Won,
	InProgress,
	Empty,
}

public sealed record CareerMail(
	string Id,
	string From,
	string Role,
	string Subject,
	string Preview,
	string Body,
	string DateLabel,
	bool Unread,
	CareerMailKind Kind,
	string? JobId);

public sealed record CareerTrophy(
	string Id,
	string Name,
	string Year,
	string Detail,
	TrophyState State);

public sealed record CareerStop(
	int Year,
	string Title,
	string School,
	string Note);

public sealed record CareerJob(
	string Id,
	string School,
	string District,
	string Record,
	string Opening,
	string Contract,
	string Pitch,
	string Fit);
