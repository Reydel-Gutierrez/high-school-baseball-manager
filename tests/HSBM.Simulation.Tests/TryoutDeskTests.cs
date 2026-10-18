using HSBM.Domain;
using HSBM.Simulation;

namespace HSBM.Simulation.Tests;

public class TryoutDeskTests
{
	[Fact]
	public void Staff_read_always_contains_the_truth_and_narrows_each_day()
	{
		GeneratedWorld world = new CareerWorldGenerator().Generate(LeagueCatalog.Standard, 7, 2026);
		List<Player> candidates = world.Players.Where(p => p.IsTryoutCandidate).Take(60).ToList();
		foreach (Player player in candidates)
		{
			int previous = int.MaxValue;
			for (int day = 0; day <= TryoutEvaluation.TryoutDays; day++)
			{
				TryoutReport report = TryoutEvaluation.Report(player, day);
				Assert.InRange(player.Overall, report.OverallLow, report.OverallHigh);
				Assert.True(report.PotentialHigh >= report.OverallHigh);
				int width = report.OverallHigh - report.OverallLow;
				Assert.True(width <= previous, $"day {day} widened the read");
				previous = width;
				foreach (DrillGrade grade in report.Drills)
				{
					Assert.True(grade.Low <= grade.High);
				}
			}

			// The read doesn't change if you look twice on the same day.
			Assert.Equal(TryoutEvaluation.Report(player, 3).OverallLabel, TryoutEvaluation.Report(player, 3).OverallLabel);
		}
	}

	[Fact]
	public void Days_seen_follows_tryout_week()
	{
		SeasonEvent week = SeasonCalendar.TryoutWeek(2026);
		Assert.Equal(0, TryoutEvaluation.DaysSeen(2026, week.Start.AddDays(-1)));
		Assert.Equal(1, TryoutEvaluation.DaysSeen(2026, week.Start));
		Assert.Equal(TryoutEvaluation.TryoutDays, TryoutEvaluation.DaysSeen(2026, week.End));
		Assert.Equal(TryoutEvaluation.TryoutDays, TryoutEvaluation.DaysSeen(2026, SeasonCalendar.TryoutDecisionDay(2026)));
	}

	[Fact]
	public void Recommendations_never_promise_more_spots_than_are_open()
	{
		CareerState state = CareerWorldRunner.NewCareer(99, userOrganizationId: "cascade");
		foreach (OrganizationState org in state.Organizations.Where(o => !LeagueCatalog.IsUnsignedOrganization(o.Id)))
		{
			Dictionary<Guid, TryoutCall> calls = TryoutDesk.Recommend(state, org.Id, TryoutEvaluation.TryoutDays);
			List<Player> roster = state.Players.Where(p => p.OrganizationId == org.Id && !p.Alumni).ToList();
			int varsityOpen = LeagueCatalog.TeamRosterCap - roster.Count(p => p.TeamLevel == TeamLevel.Varsity);
			int jvOpen = LeagueCatalog.TeamRosterCap - roster.Count(p => p.TeamLevel == TeamLevel.JuniorVarsity);
			Assert.True(calls.Values.Count(c => c == TryoutCall.Varsity) <= varsityOpen);
			Assert.True(calls.Values.Count(c => c == TryoutCall.JuniorVarsity) <= jvOpen);
			Assert.Equal(state.TryoutCandidates(org.Id).Count(), calls.Count);
		}
	}

	[Fact]
	public void Decision_day_places_every_candidate_and_keeps_rosters_legal()
	{
		CareerState state = CareerWorldRunner.NewCareer(123, userOrganizationId: "cascade");
		DateOnly decision = SeasonCalendar.TryoutDecisionDay(2026);
		List<Player> mine = state.TryoutCandidates("cascade").ToList();
		foreach (Player player in mine)
		{
			player.TryoutCall = TryoutCall.Varsity;
		}

		List<PlayerTransaction> posted = TryoutDesk.Finalize(state, decision);

		Assert.DoesNotContain(state.Players, p => p.IsTryoutCandidate);
		Assert.NotEmpty(posted);
		foreach (TeamIdentity team in state.Teams.Where(t => t.RosterCap > 0))
		{
			Assert.True(state.Players.Count(p => p.TeamId == team.Id) <= team.RosterCap, team.Id);
		}

		Assert.All(mine, player =>
			Assert.True(
				player.TeamId == LeagueCatalog.InactiveTeamId || player.OrganizationId == "cascade",
				player.FullName));
		Assert.DoesNotContain(state.Players, p =>
			!p.Alumni
			&& p.GeneratedSeasonYear == 2026
			&& p.Grade == PlayerGrade.Senior
			&& p.TryoutCall == TryoutCall.JuniorVarsity);
	}

	[Fact]
	public void Rollover_posts_a_fresh_sign_up_sheet()
	{
		CareerState state = CareerWorldRunner.NewCareer(5, userOrganizationId: "cascade");
		TryoutDesk.Finalize(state, SeasonCalendar.TryoutDecisionDay(2026));
		Assert.DoesNotContain(state.Players, p => p.IsTryoutCandidate);

		SeasonRollover.Apply(state, LeagueCatalog.Standard, new SeededRandom(5));

		Assert.Equal(2027, state.Career.CurrentSeasonYear);
		Assert.True(state.TryoutCandidates("cascade").Count() >= 8);
		Assert.DoesNotContain(state.Players, p => p.TeamId == LeagueCatalog.InactiveTeamId);
	}
}
