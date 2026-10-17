namespace HSBM.Domain;

public enum RosterAssignment
{
	Unassigned = 0,
	Varsity = 1,
	JuniorVarsity = 2,
}

/// <summary>
/// Program roster limits. After Open Play a school may carry two extra
/// players (42). The initial roster cut locks the program at 40.
/// Typical lock shape is 20 varsity, 18 JV, and 2 unassigned.
/// </summary>
public static class ProgramRosterRules
{
	public const int OrganizationCap = LeagueCatalog.OrganizationRosterCap;
	public const int TemporaryCap = LeagueCatalog.TemporaryRosterCap;
	public const int VarsityCap = LeagueCatalog.TeamRosterCap;
	public const int JuniorVarsityCap = LeagueCatalog.TeamRosterCap;
	public const int UnassignedSlots = LeagueCatalog.UnassignedSlots;
	public const int TemporaryExtras = TemporaryCap - OrganizationCap;

	public static bool ExtrasAllowed(DateOnly today, int seasonYear) =>
		today < SeasonCalendar.InitialRosterCut(seasonYear);

	public static int RegisteredCap(DateOnly today, int seasonYear) =>
		ExtrasAllowed(today, seasonYear) ? TemporaryCap : OrganizationCap;

	public static int UnassignedCap(DateOnly today, int seasonYear) =>
		ExtrasAllowed(today, seasonYear) ? UnassignedSlots + TemporaryExtras : UnassignedSlots;

	public static string AssignmentLabel(RosterAssignment assignment) => assignment switch
	{
		RosterAssignment.Varsity => "VARSITY",
		RosterAssignment.JuniorVarsity => "JV",
		_ => "UNASSIGNED",
	};
}
