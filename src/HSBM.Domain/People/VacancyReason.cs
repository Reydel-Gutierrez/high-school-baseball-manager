namespace HSBM.Domain;

/// <summary>
/// Why the varsity chair is empty. Athletic directors post these, not the
/// licensing office.
/// </summary>
public enum VacancyReason
{
	Retirement = 0,
	NonRenewal = 1,
	Resignation = 2,
	Reassignment = 3,
	Dismissal = 4,
}

public static class VacancyReasonExtensions
{
	public static string ToLabel(this VacancyReason reason) => reason switch
	{
		VacancyReason.Retirement => "Previous coach retired",
		VacancyReason.NonRenewal => "Board declined to renew the previous staff",
		VacancyReason.Resignation => "Previous coach resigned",
		VacancyReason.Reassignment => "Athletic director reassigned the last coach",
		VacancyReason.Dismissal => "Previous coach dismissed",
		_ => "Head-coaching vacancy posted",
	};
}
