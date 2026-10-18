namespace HSBM.Domain;

public sealed class Player
{
	public Guid Id { get; set; }
	public string FirstName { get; set; } = "";
	public string LastName { get; set; } = "";
	public int GeneratedSeasonYear { get; set; }
	public BattingSide Bats { get; set; }
	public ThrowingHand Throws { get; set; }
	public PlayerPosition PrimaryPosition { get; set; }
	public PlayerPosition? SecondaryPosition { get; set; }
	public PlayerGrade Grade { get; set; }
	public int Age { get; set; }
	public string OrganizationId { get; set; } = "";
	public string TeamId { get; set; } = "";
	public TeamLevel TeamLevel { get; set; }
	public int JerseyNumber { get; set; }
	public PlayerRatings Ratings { get; set; }
	public int Potential { get; set; }
	public PlayerPhysical Physical { get; set; }
	public PlayerTemperament Personality { get; set; }
	public PersonAppearance Appearance { get; set; } = new();
	public RecruitingPriorities Priorities { get; set; } = RecruitingPriorities.Balanced;
	public PlayerInjury? Injury { get; set; }
	public bool Alumni { get; set; }
	public int GraduatedYear { get; set; }
	public string CollegeId { get; set; } = "";
	public string CollegeSchool { get; set; } = "";
	public string CollegePath { get; set; } = "";

	/// <summary>Banked growth (hundredths of an overall point) not yet paid out as rating points.</summary>
	public int DevelopmentProgress { get; set; }

	/// <summary>
	/// Exact (unrounded) overall when the current baseball year began, in
	/// hundredths; 0 if unknown. Drives "this season" development deltas.
	/// </summary>
	public int SeasonStartRating { get; set; }

	public double ExactOverall => PlayerOverall.Exact(PrimaryPosition, Ratings);

	/// <summary>Overall points gained since the baseball year began (fractional).</summary>
	public double SeasonGain => SeasonStartRating > 0 ? ExactOverall - SeasonStartRating / 100.0 : 0;

	public void MarkSeasonStart() => SeasonStartRating = (int)Math.Round(ExactOverall * 100);

	public string FullName => $"{FirstName} {LastName}";

	public int Overall => PlayerOverall.Calculate(PrimaryPosition, Ratings);

	public string DisplayPosition => PrimaryPosition.ToDisplayLabel(Throws);

	public bool IsPitcher => PrimaryPosition.IsPitcher();

	public bool IsUnsigned => LeagueCatalog.IsUnsignedOrganization(OrganizationId);

	public bool IsActive => !Alumni && !IsUnsigned;

	public int RemainingEligibility => Grade.RemainingEligibility();

	public bool IsInjured => Injury != null;

	public bool CanPlay(DateOnly today) =>
		Injury == null || Injury.DayToDay || today >= Injury.AvailableOn;

	public bool IsUnavailable(DateOnly today) => !CanPlay(today);

	public bool HasCollegeDestination =>
		!string.IsNullOrWhiteSpace(CollegeId) || !string.IsNullOrWhiteSpace(CollegeSchool);

	public CollegeCommit? CollegeDestination => HasCollegeDestination
		? new CollegeCommit
		{
			CollegeId = string.IsNullOrWhiteSpace(CollegeId) ? CollegeCatalog.IdOf(CollegeSchool) : CollegeId,
			School = CollegeSchool,
			Path = CollegePath,
			CompetitionLevel = CompetitionLevel.College,
		}
		: null;

	public void Graduate(int seasonYear, CollegeCommit destination)
	{
		Alumni = true;
		Injury = null;
		GraduatedYear = seasonYear;
		CollegeId = destination.Id;
		CollegeSchool = destination.School;
		CollegePath = destination.Path;
	}
}
