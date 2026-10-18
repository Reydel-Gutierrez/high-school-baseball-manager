using HSBM.Domain;
using HSBM.Simulation;

namespace HSBM.Simulation.Tests;

public class WorldRosterDeskTests
{
	[Fact]
	public void Cut_day_releases_when_a_school_is_over_cap()
	{
		CareerState state = World(new DateOnly(2026, 2, 8));
		AddRoster(state, "great-lakes", TeamLevel.Varsity, 21, 50);
		AddRoster(state, "great-lakes", TeamLevel.JuniorVarsity, 20, 45);
		Assert.Equal(41, Count("great-lakes", state));

		List<PlayerTransaction> posted = WorldRosterDesk.AdvanceDay(state, state.Career.CurrentDate, new SeededRandom(3));

		Assert.Contains(posted, t => t.Kind == PlayerTransactionKind.Release && t.Headline.Contains("Great Lakes released"));
		Assert.Equal(40, Count("great-lakes", state));
		Assert.Contains(state.Players, p => p.IsUnsigned);
	}

	[Fact]
	public void Roster_week_promotes_a_better_jv_player_into_a_varsity_hole()
	{
		CareerState state = World(new DateOnly(2026, 2, 3));
		AddRoster(state, "great-lakes", TeamLevel.Varsity, 19, 52);
		Player torres = AddPlayer(state, "great-lakes", TeamLevel.JuniorVarsity, "Luis", "Torres", 72);

		List<PlayerTransaction> posted = WorldRosterDesk.AdvanceDay(state, state.Career.CurrentDate, new SeededRandom(11));

		PlayerTransaction? promote = posted.FirstOrDefault(t => t.Kind == PlayerTransactionKind.Promotion);
		Assert.NotNull(promote);
		Assert.Equal("Great Lakes promoted Luis Torres to Varsity.", promote.Headline);
		Assert.Equal(TeamLevel.Varsity, torres.TeamLevel);
		Assert.Equal("great-lakes-varsity", torres.TeamId);
	}

	[Fact]
	public void Under_cap_school_never_signs_a_kid_who_is_not_on_a_team()
	{
		CareerState state = World(new DateOnly(2026, 2, 20));
		AddRoster(state, "great-lakes", TeamLevel.Varsity, 20, 55);
		AddRoster(state, "great-lakes", TeamLevel.JuniorVarsity, 18, 48);
		Player mason = AddUnsigned(state, "Mason", "Young", 64);

		WorldRosterDesk.AdvanceDay(state, state.Career.CurrentDate, new SeededRandom(21));

		Assert.True(mason.IsUnsigned);
		Assert.Equal(LeagueCatalog.InactiveTeamId, mason.TeamId);
	}

	[Fact]
	public void User_school_is_left_alone()
	{
		CareerState state = World(new DateOnly(2026, 2, 8));
		AddRoster(state, "cascade", TeamLevel.Varsity, 21, 50);
		AddRoster(state, "cascade", TeamLevel.JuniorVarsity, 20, 44);

		List<PlayerTransaction> posted = WorldRosterDesk.AdvanceDay(state, state.Career.CurrentDate, new SeededRandom(7));

		Assert.Empty(posted);
		Assert.Equal(41, Count("cascade", state));
	}

	[Fact]
	public void Ai_controller_makes_roster_moves_for_non_user_schools_only()
	{
		CareerState state = World(new DateOnly(2026, 2, 8));
		AddRoster(state, "cascade", TeamLevel.Varsity, 21, 50);
		AddRoster(state, "cascade", TeamLevel.JuniorVarsity, 20, 44);
		AddRoster(state, "great-lakes", TeamLevel.Varsity, 21, 50);
		AddRoster(state, "great-lakes", TeamLevel.JuniorVarsity, 20, 45);

		List<PlayerTransaction> posted = AiProgramController.AdvanceDay(state, state.Career.CurrentDate, new SeededRandom(3));

		Assert.Contains(posted, t => t.Headline.Contains("Great Lakes released"));
		Assert.Equal(41, Count("cascade", state));
		Assert.Equal(40, Count("great-lakes", state));
	}

	private static CareerState World(DateOnly date)
	{
		var cascade = School("cascade", "Cascade Regional High School", "Cascade");
		var lakes = School("great-lakes", "Great Lakes High School", "Great Lakes");
		var unsigned = new OrganizationState
		{
			Identity = new OrganizationIdentity(LeagueCatalog.UnsignedOrganizationId, "Unsigned Prospects", "Unsigned", 0),
			DistrictId = "great-lakes",
		};

		return new CareerState
		{
			Career = new Career
			{
				UserOrganizationId = "cascade",
				CurrentSeasonYear = 2026,
				CurrentDate = date,
			},
			Organizations = [cascade, lakes, unsigned],
			Teams =
			[
				new TeamIdentity("cascade-varsity", "cascade", TeamLevel.Varsity, 20),
				new TeamIdentity("cascade-jv", "cascade", TeamLevel.JuniorVarsity, 20),
				new TeamIdentity("great-lakes-varsity", "great-lakes", TeamLevel.Varsity, 20),
				new TeamIdentity("great-lakes-jv", "great-lakes", TeamLevel.JuniorVarsity, 20),
				new TeamIdentity(LeagueCatalog.InactiveTeamId, LeagueCatalog.UnsignedOrganizationId, TeamLevel.JuniorVarsity, 0),
			],
			Players = [],
			Transactions = [],
		};
	}

	private static OrganizationState School(string id, string name, string shortName) =>
		new()
		{
			Identity = new OrganizationIdentity(id, name, shortName, 40),
			DistrictId = "great-lakes",
			TalentTier = ProgramTalentTier.Average,
		};

	private static void AddRoster(CareerState state, string orgId, TeamLevel level, int count, int overall)
	{
		for (int i = 0; i < count; i++)
		{
			AddPlayer(state, orgId, level, "Player", $"{orgId.Replace("-", "", StringComparison.Ordinal)}{level}{i:00}", overall);
		}
	}

	private static Player AddPlayer(
		CareerState state,
		string orgId,
		TeamLevel level,
		string first,
		string last,
		int overall)
	{
		string teamId = level == TeamLevel.Varsity ? $"{orgId}-varsity" : $"{orgId}-jv";
		var player = new Player
		{
			Id = Guid.NewGuid(),
			FirstName = first,
			LastName = last,
			PrimaryPosition = PlayerPosition.Shortstop,
			Grade = PlayerGrade.Junior,
			OrganizationId = orgId,
			TeamId = teamId,
			TeamLevel = level,
			JerseyNumber = state.Players.Count + 1,
			Ratings = PlayerRatings.Clamp(overall, overall, overall, overall, overall, overall, 30, 30, 30, 30),
		};
		state.Players.Add(player);
		return player;
	}

	private static Player AddUnsigned(CareerState state, string first, string last, int overall)
	{
		var player = new Player
		{
			Id = Guid.NewGuid(),
			FirstName = first,
			LastName = last,
			PrimaryPosition = PlayerPosition.Shortstop,
			Grade = PlayerGrade.Freshman,
			OrganizationId = LeagueCatalog.UnsignedOrganizationId,
			TeamId = LeagueCatalog.InactiveTeamId,
			TeamLevel = TeamLevel.JuniorVarsity,
			Ratings = PlayerRatings.Clamp(overall, overall, overall, overall, overall, overall, 30, 30, 30, 30),
		};
		state.Players.Add(player);
		return player;
	}

	private static int Count(string organizationId, CareerState state)
	{
		int count = 0;
		foreach (Player player in state.Players)
		{
			if (!player.IsUnsigned && player.OrganizationId == organizationId)
			{
				count++;
			}
		}

		return count;
	}
}
