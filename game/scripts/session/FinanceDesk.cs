using System.Collections.Generic;
using HSBM.Domain;

/// <summary>
/// Current school's baseball budget, allocations, and local sponsors.
/// </summary>
public static class FinanceDesk
{
	public static ProgramFinanceSnapshot Current(GameSession session)
	{
		if (session == null || !session.HasSignedContract)
		{
			return ProgramFinances.Empty;
		}

		OrganizationState? program = session.LeagueProgram(session.CatalogOrganizationId);
		if (program == null)
		{
			return ProgramFinances.Empty;
		}

		IReadOnlyList<PlayerAgreement> agreements = session.RecruitingMarket.Agreements;
		int salary = session.AnnualSalary;
		return ProgramFinances.For(program, agreements, salary);
	}
}
