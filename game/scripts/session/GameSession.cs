using System;
using System.Collections.Generic;
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
	public const string CurrentDateLabel = "APR 24";

	public static GameSession Current { get; private set; } = null!;

	public OrganizationIdentity Organization { get; private set; } = null!;
	public TeamIdentity VarsityTeam { get; private set; } = null!;
	public TeamIdentity JuniorVarsityTeam { get; private set; } = null!;
	public TeamIdentity ActiveTeam { get; private set; } = null!;

	/// <summary>
	/// 1-based year on the current deal (1/4 during the first season).
	/// </summary>
	public int ContractYear { get; private set; } = 1;

	public int ContractLength { get; private set; } = 4;

	/// <summary>
	/// Starts at Average. Later seasons should raise or lower this from
	/// met and missed contract goals; job security is derived from it.
	/// </summary>
	public BoardSatisfaction BoardSatisfaction { get; private set; } = BoardSatisfaction.Average;

	public JobSecurity JobSecurity => BoardSatisfaction.ToJobSecurity();

	public bool ShowContractHistory => ContractYear > 1;

	private readonly List<PlayerTransaction> _transactions = new();
	private readonly List<ScoutTarget> _scoutTargets = new();

	public IReadOnlyList<PlayerTransaction> Transactions => _transactions;

	public IReadOnlyList<ScoutTarget> ScoutTargets => _scoutTargets;

	public event Action<TeamIdentity>? ActiveTeamChanged;
	public event Action? TransactionsChanged;
	public event Action? ScoutTargetsChanged;

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
		SeedPrototypeTransactions();
	}

	public bool IsScoutTarget(string teamId, string playerName)
	{
		foreach (ScoutTarget target in _scoutTargets)
		{
			if (target.TeamId == teamId && target.PlayerName == playerName)
			{
				return true;
			}
		}

		return false;
	}

	public void AddScoutTarget(string teamId, string playerName)
	{
		if (teamId == DistrictHubData.UserTeamId || string.IsNullOrEmpty(playerName) || IsScoutTarget(teamId, playerName))
		{
			return;
		}

		_scoutTargets.Insert(0, new ScoutTarget(teamId, playerName));
		ScoutTargetsChanged?.Invoke();
	}

	public void RemoveScoutTarget(string teamId, string playerName)
	{
		int index = _scoutTargets.FindIndex(target => target.TeamId == teamId && target.PlayerName == playerName);
		if (index < 0)
		{
			return;
		}

		_scoutTargets.RemoveAt(index);
		ScoutTargetsChanged?.Invoke();
	}

	public void ToggleScoutTarget(string teamId, string playerName)
	{
		if (IsScoutTarget(teamId, playerName))
		{
			RemoveScoutTarget(teamId, playerName);
			return;
		}

		AddScoutTarget(teamId, playerName);
	}

	public void RecordRelease(string firstName, string lastName, string position, string year, string squad)
	{
		_transactions.Insert(0, PlayerTransaction.Release(
			$"{firstName} {lastName}",
			position,
			year,
			squad,
			ProgramDisplayName,
			CurrentDateLabel));
		TransactionsChanged?.Invoke();
	}

	private string ProgramDisplayName =>
		Organization.Name.Replace(" High School", string.Empty, StringComparison.Ordinal);

	private void SeedPrototypeTransactions()
	{
		const string cascade = "Cascade Regional";
		const string lakes = "Great Lakes High School";
		const string harbor = "Harbor Ridge High School";
		const string riverview = "Riverview High School";

		_transactions.AddRange(
		[
			PlayerTransaction.Trade("Cole Moss", "C", "FR", "JV", cascade, lakes, "APR 22"),
			PlayerTransaction.Trade("Sam Keene", "CF", "FR", "JV", lakes, cascade, "APR 22"),
			PlayerTransaction.Release("Cole Brennan", "OF", "JR", "Varsity", cascade, "APR 21"),
			PlayerTransaction.SignedFromOpenPool("Mario Cruz", "C", "JR", "Varsity", cascade, "APR 20"),
			PlayerTransaction.Release("Mario Cruz", "C", "JR", "Varsity", lakes, "APR 19"),
			PlayerTransaction.SignedFromOpenPool("Jordan Hale", "SS", "SO", "JV", cascade, "APR 18"),
			PlayerTransaction.Release("Jordan Hale", "SS", "SO", "JV", riverview, "APR 17"),
			PlayerTransaction.Release("Marcus Webb", "LHP", "SO", "JV", cascade, "APR 14"),
			PlayerTransaction.Trade("Ian Frost", "SS", "SO", "Varsity", cascade, harbor, "APR 9"),
			PlayerTransaction.Trade("Ben Ortiz", "RHP", "JR", "Varsity", harbor, cascade, "APR 9"),
			PlayerTransaction.SignedFromOpenPool("Andre Kim", "OF", "JR", "Varsity", cascade, "APR 6"),
			PlayerTransaction.Release("Andre Kim", "OF", "JR", "Varsity", harbor, "APR 3"),
			PlayerTransaction.Tryout("Chris Nolan", "2B", "FR", "JV", cascade, "MAR 28"),
			PlayerTransaction.Tryout("Leo Hayes", "RHP", "FR", "JV", cascade, "MAR 28"),
			PlayerTransaction.Tryout("Sam Keene", "CF", "FR", "JV", cascade, "MAR 28"),
			PlayerTransaction.Tryout("Riley Fox", "C", "FR", "JV", cascade, "MAR 28"),
			PlayerTransaction.Tryout("Jonah Parks", "3B", "FR", "JV", cascade, "MAR 27"),
		]);
	}
}

public sealed record ScoutTarget(string TeamId, string PlayerName);
