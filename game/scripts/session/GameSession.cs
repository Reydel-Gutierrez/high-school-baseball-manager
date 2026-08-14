using System;
using Godot;
using HSBM.Domain;

/// <summary>
/// Autoload that holds the coach's organization and which of its teams is
/// currently active (Varsity or JV). Screens should bind to ActiveTeam so
/// roster, schedule, and other pages all populate from the same selection.
/// </summary>
[GlobalClass]
public partial class GameSession : Node
{
	public static GameSession Current { get; private set; } = null!;

	public OrganizationIdentity Organization { get; private set; } = null!;
	public TeamIdentity VarsityTeam { get; private set; } = null!;
	public TeamIdentity JuniorVarsityTeam { get; private set; } = null!;
	public TeamIdentity ActiveTeam { get; private set; } = null!;

	public event Action<TeamIdentity>? ActiveTeamChanged;

	public override void _EnterTree()
	{
		Current = this;
		LoadPrototypeProgram();
	}

	public TeamIdentity GetTeam(TeamLevel level) =>
		level == TeamLevel.JuniorVarsity ? JuniorVarsityTeam : VarsityTeam;

	public void SetActiveLevel(TeamLevel level)
	{
		TeamIdentity next = GetTeam(level);
		if (ActiveTeam != null && ActiveTeam.Id == next.Id)
		{
			return;
		}

		ActiveTeam = next;
		ActiveTeamChanged?.Invoke(ActiveTeam);
	}

	public void ToggleActiveLevel()
	{
		SetActiveLevel(ActiveTeam.Level == TeamLevel.Varsity
			? TeamLevel.JuniorVarsity
			: TeamLevel.Varsity);
	}

	/// <summary>
	/// Prototype Cascade program. Replace with persisted organization + team
	/// records when career/save data is wired up; keep ActiveTeam as the
	/// single source of truth for UI.
	/// </summary>
	private void LoadPrototypeProgram()
	{
		Organization = new OrganizationIdentity(
			"org-cascade-regional",
			"Cascade Regional High School",
			"Cascade",
			40);

		VarsityTeam = new TeamIdentity(
			"team-cascade-varsity",
			Organization.Id,
			TeamLevel.Varsity,
			20);

		JuniorVarsityTeam = new TeamIdentity(
			"team-cascade-jv",
			Organization.Id,
			TeamLevel.JuniorVarsity,
			20);

		ActiveTeam = VarsityTeam;
	}
}
