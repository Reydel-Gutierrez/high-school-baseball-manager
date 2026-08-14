using System;

namespace HSBM.Domain;

public enum TeamLevel
{
	Varsity = 0,
	JuniorVarsity = 1,
}

public sealed record OrganizationIdentity(
	string Id,
	string Name,
	string ShortName,
	int RosterCap);

public sealed record TeamIdentity(
	string Id,
	string OrganizationId,
	TeamLevel Level,
	int RosterCap)
{
	public string LevelLabel => Level.ToLabel();

	public string RosterTitle => $"{LevelLabel} Roster";
}

public static class TeamLevelExtensions
{
	public static string ToLabel(this TeamLevel level) =>
		level == TeamLevel.JuniorVarsity ? "JV" : "Varsity";

	public static string ToSquadKey(this TeamLevel level) => level.ToLabel();

	public static bool MatchesSquad(this TeamLevel level, string? squad) =>
		string.Equals(level.ToSquadKey(), squad, StringComparison.OrdinalIgnoreCase);
}
