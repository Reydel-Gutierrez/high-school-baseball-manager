using HSBM.Domain;

namespace HSBM.Domain.Tests;

public class ProgramFinancesTests
{
	[Theory]
	[InlineData(18_000)]
	[InlineData(40_000)]
	[InlineData(47_250)]
	[InlineData(95_000)]
	public void Allocations_sum_to_the_baseball_budget(int budget)
	{
		IReadOnlyList<BudgetLine> lines = ProgramFinances.Allocations(budget);
		int total = 0;
		foreach (BudgetLine line in lines)
		{
			total += line.Amount;
			Assert.True(line.Amount >= 0);
		}

		Assert.Equal(budget, total);
		Assert.Equal(5, lines.Count);
		Assert.Equal("Equipment", lines[0].Label);
		Assert.Equal("Travel", lines[1].Label);
		Assert.Equal("Facilities", lines[2].Label);
		Assert.Equal("Scouting", lines[3].Label);
		Assert.Equal("Misc.", lines[4].Label);
		Assert.Equal(ProgramBudget.RecruitingPool(budget), lines[3].Amount);
	}

	[Fact]
	public void Snapshot_available_matches_operations_budget()
	{
		var program = new OrganizationState
		{
			Identity = new OrganizationIdentity("cascade", "Cascade Regional High School", "Cascade", 40),
			DistrictId = "great-lakes",
			Profile = new ProgramProfile { BaseballBudget = 40_000 },
		};
		var deals = new List<PlayerAgreement>
		{
			new()
			{
				OrganizationId = "cascade",
				AnnualSponsorship = 900,
				YearsRemaining = 2,
			},
		};
		int salary = CoachSalary.From(78, 40_000);
		ProgramFinanceSnapshot sheet = ProgramFinances.For(program, deals, salary);

		Assert.Equal(40_000, sheet.BaseballBudget);
		Assert.Equal(ProgramBudget.Available(program.Profile, deals, "cascade", salary), sheet.Available);
		Assert.Equal(900, sheet.RecruitingCommitted);
		Assert.Equal(salary, sheet.CoachSalary);
		Assert.Equal(40_000, sheet.AllocatedTotal);
	}

	[Fact]
	public void Sponsors_stay_lightweight_and_stable()
	{
		IReadOnlyList<ProgramSponsor> first = ProgramFinances.Sponsors(
			"cascade",
			"Cascade",
			"Great Lakes",
			40_000);
		IReadOnlyList<ProgramSponsor> again = ProgramFinances.Sponsors(
			"cascade",
			"Cascade",
			"Great Lakes",
			40_000);

		Assert.Equal(2, first.Count);
		Assert.Equal(first, again);
		Assert.Contains(first, s => !s.InKind && s.AnnualAmount > 0);
		Assert.Contains(first, s => s.InKind && s.Name == "Local Sports Supply");
		Assert.Equal("Equipment sponsorship", first.Single(s => s.InKind).AmountLabel);
		Assert.EndsWith(" / season", first.Single(s => !s.InKind).AmountLabel);
	}

	[Fact]
	public void Bigger_programs_pick_up_a_third_sponsor()
	{
		IReadOnlyList<ProgramSponsor> sponsors = ProgramFinances.Sponsors(
			"capital-city",
			"Capital City",
			"Capital Valley",
			72_000);
		Assert.Equal(3, sponsors.Count);
	}
}
