namespace HSBM.Domain;

/// <summary>
/// Athletic board's view of the current manager. Starts in the middle of
/// the scale and moves as contract goals are met or missed.
/// </summary>
public enum BoardSatisfaction
{
	Poor = 0,
	Fair = 1,
	Average = 2,
	Good = 3,
	Excellent = 4,
}

public enum JobSecurity
{
	AtRisk = 0,
	Average = 1,
	Secured = 2,
}

public static class BoardSatisfactionExtensions
{
	public static string ToLabel(this BoardSatisfaction satisfaction) => satisfaction switch
	{
		BoardSatisfaction.Poor => "Poor",
		BoardSatisfaction.Fair => "Fair",
		BoardSatisfaction.Good => "Good",
		BoardSatisfaction.Excellent => "Excellent",
		_ => "Average",
	};

	public static JobSecurity ToJobSecurity(this BoardSatisfaction satisfaction) => satisfaction switch
	{
		BoardSatisfaction.Poor or BoardSatisfaction.Fair => JobSecurity.AtRisk,
		BoardSatisfaction.Good or BoardSatisfaction.Excellent => JobSecurity.Secured,
		_ => JobSecurity.Average,
	};

	public static string ToLabel(this JobSecurity security) => security switch
	{
		JobSecurity.AtRisk => "At Risk",
		JobSecurity.Secured => "Secured",
		_ => "Average",
	};
}
