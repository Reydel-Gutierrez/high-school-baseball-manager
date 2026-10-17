namespace HSBM.Domain;

/// <summary>
/// The circuit a world (or a program) competes on. High school is the
/// current game; college is reserved so later catalogs can share the
/// same world, save, and AI controller.
/// </summary>
public enum CompetitionLevel
{
	HighSchool = 0,
	College = 1,
}

public static class CompetitionLevelExtensions
{
	public static string ToLabel(this CompetitionLevel level) => level switch
	{
		CompetitionLevel.College => "College",
		_ => "High School",
	};
}
