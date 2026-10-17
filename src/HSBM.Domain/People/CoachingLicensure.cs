namespace HSBM.Domain;

/// <summary>
/// State-association copy for a newly issued varsity baseball license.
/// A license clears a coach to be hired; it does not place them.
/// </summary>
public static class CoachingLicensure
{
	public const string Association = "State High School Athletic Association";
	public const string Office = "Office of Coaching Licensure";
	public const string Director = "Helen Ward";
	public const string DirectorTitle = "Director, Office of Coaching Licensure";

	public static string LastNameOf(string fullName)
	{
		string trimmed = fullName.Trim();
		if (string.IsNullOrEmpty(trimmed))
		{
			return "Coach";
		}

		int space = trimmed.LastIndexOf(' ');
		return space < 0 ? trimmed : trimmed[(space + 1)..];
	}

	public static string LetterBody(string coachName)
	{
		string last = LastNameOf(coachName);
		return
			$"Coach {last}:\n\n" +
			"The Association has issued your High School Baseball Head Coach license. " +
			"Your credentials are now on file with this office and with the district athletic administrators. " +
			"The required background review is complete, and your certification in first aid, concussion protocol, " +
			"and the fundamentals of coaching has been accepted.\n\n" +
			"A license authorizes you to be employed as a varsity head coach. It is not an appointment. " +
			"Athletic directors post vacancies, and boards of education make the hire. " +
			"Programs that already have a sitting staff will not open the chair for a first-year license.\n\n" +
			"This office is forwarding the current vacancies that will consider a newly licensed candidate. " +
			"Close this letter to open the Job Market. Review each posting, then accept the seat you intend to take. " +
			"Once you sign, we will notify the remaining athletic directors that you are no longer available.\n\n" +
			"Respectfully,\n\n" +
			$"{Director}\n{DirectorTitle}\n{Association}";
	}
}
