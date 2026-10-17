using HSBM.Domain;
using HSBM.Simulation;

namespace HSBM.Simulation.Tests;

public class WorldGenerationTests
{
	[Fact]
	public void Generated_world_has_thirty_two_programs_in_four_districts()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 2026, 2026);
		Assert.Equal(32, world.Organizations.Count(org => !LeagueCatalog.IsUnsignedOrganization(org.Id)));
		Assert.Equal(LeagueCatalog.DistrictCount, world.Districts.Count);
		Assert.Contains(world.Organizations, org => org.Id == LeagueCatalog.UnsignedOrganizationId);
		foreach (DistrictIdentity district in world.Districts)
		{
			int schools = world.Organizations.Count(org =>
				org.DistrictId == district.Id && !LeagueCatalog.IsUnsignedOrganization(org.Id));
			Assert.Equal(LeagueCatalog.SchoolsPerDistrict, schools);
			Assert.Contains(world.Teams, team => team.Id == LeagueCatalog.OpenPlayTeamId(district.Id));
		}

		Assert.Equal(32, world.Coaches.Count);
		Assert.All(world.Organizations.Where(org => !LeagueCatalog.IsUnsignedOrganization(org.Id)), org =>
		{
			Assert.Equal(LeagueCatalog.Standard.DistrictIdFor(org.Id), org.DistrictId);
			Assert.NotNull(org.HeadCoach);
			Assert.Contains(world.Teams, team => team.OrganizationId == org.Id && team.Level == TeamLevel.Varsity);
			Assert.Contains(world.Teams, team => team.OrganizationId == org.Id && team.Level == TeamLevel.JuniorVarsity);
		});
	}

	[Fact]
	public void Roster_constraints_hold_for_every_playable_team()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 77, 2026);
		foreach (TeamIdentity team in world.Teams.Where(t => t.RosterCap > 0))
		{
			List<Player> roster = world.Players.Where(p => p.TeamId == team.Id).ToList();
			Assert.InRange(roster.Count, WorldIntegrity.MinPlayableRoster, team.RosterCap);
			Assert.Equal(roster.Count, roster.Select(p => p.JerseyNumber).Distinct().Count());
			Assert.Contains(roster, p => p.PrimaryPosition == PlayerPosition.Catcher);
			Assert.Contains(roster, p => p.IsPitcher);
			Assert.Contains(roster, p => p.PrimaryPosition.IsInfielder());
			Assert.Contains(roster, p => p.PrimaryPosition.IsOutfielder());
			Assert.All(roster, p =>
			{
				Assert.Equal(team.OrganizationId, p.OrganizationId);
				Assert.Equal(team.Level, p.TeamLevel);
				Assert.InRange((int)p.Grade, (int)PlayerGrade.Freshman, (int)PlayerGrade.Senior);
				Assert.Equal(PlayerOverall.Calculate(p.PrimaryPosition, p.Ratings), p.Overall);
			});
		}

		foreach (OrganizationState org in world.Organizations.Where(o => !LeagueCatalog.IsUnsignedOrganization(o.Id)))
		{
			int registered = world.Players.Count(p => p.OrganizationId == org.Id && !p.Alumni);
			Assert.True(registered <= LeagueCatalog.OrganizationRosterCap, $"{org.Id} starts over the 40-man cap.");
		}

		IReadOnlyList<string> issues = WorldIntegrity.InspectGenerated(world);
		Assert.True(issues.Count == 0, string.Join(Environment.NewLine, issues));
	}

	[Fact]
	public void District_open_play_posts_a_full_unsigned_class()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 11, 2026);
		Assert.Equal(LeagueCatalog.DistrictCount, world.OpenPlay.Count);
		foreach (DistrictOpenPlay session in world.OpenPlay)
		{
			Assert.Equal(LeagueCatalog.OpenPlayTargetPlayers, session.Lines.Count);
			List<Player> pool = world.Players
				.Where(p => p.TeamId == LeagueCatalog.OpenPlayTeamId(session.DistrictId))
				.ToList();
			Assert.Equal(session.Lines.Count, pool.Count);
			Assert.All(pool, player => Assert.True(player.IsUnsigned));
		}
	}
}
