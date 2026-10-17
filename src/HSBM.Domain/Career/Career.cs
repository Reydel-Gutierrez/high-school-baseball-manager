using System.Globalization;

namespace HSBM.Domain;

public sealed class Career
{
	public const int CurrentSaveVersion = 1;

	public Guid Id { get; set; }
	public string Name { get; set; } = "";
	public DateTimeOffset CreatedAt { get; set; }
	public DateOnly CurrentDate { get; set; }
	public int CurrentSeasonYear { get; set; }
	public string UserOrganizationId { get; set; } = "";
	public int RandomSeed { get; set; }
	public CompetitionLevel CompetitionLevel { get; set; } = CompetitionLevel.HighSchool;
	public int SaveVersion { get; set; } = CurrentSaveVersion;

	public int WorldSeed
	{
		get => RandomSeed;
		set => RandomSeed = value;
	}

	public CoachProfile Coach { get; set; } = new();
	public int Reputation { get; set; } = CoachReputation.Starting;
	public int CareerWins { get; set; }
	public int CareerLosses { get; set; }
	public int DistrictTitles { get; set; }
	public int RegionalTitles { get; set; }
	public int StateTitles { get; set; }
	public int SeasonsCoached { get; set; }
	public int BoardSatisfactionScore { get; set; } =
		SeasonReview.OpeningSatisfaction(BoardSatisfaction.Average);
	public int ConsecutivePoorSeasons { get; set; }
	public List<CoachStint> Stints { get; set; } = [];
	public List<CoachSeason> Seasons { get; set; } = [];
	public CoachContract Contract { get; set; } = new();
	public List<CoachingJobOffer> JobOffers { get; set; } = [];

	public bool HasSignedContract => !string.IsNullOrWhiteSpace(UserOrganizationId);

	public IReadOnlyList<CoachingJobOffer> PendingJobOffers =>
		JobOffers.Where(offer => offer.IsPending).ToList();

	public int CareerGames => CareerWins + CareerLosses;

	public double WinningPercentage => CareerGames == 0
		? 0
		: CareerWins / (double)CareerGames;

	public string RecordLabel => $"{CareerWins}\u2013{CareerLosses}";

	public string WinPctLabel => CareerGames == 0
		? "\u2014"
		: WinningPercentage.ToString(".000", CultureInfo.InvariantCulture);

	public string SeasonLabel => SeasonsCoached <= 0
		? "SEEKING A SEAT"
		: $"{Ordinal.Format(SeasonsCoached)} SEASON";

	public string DisplayName =>
		string.IsNullOrWhiteSpace(Coach.FullName) ? Name : Coach.FullName;

	public void CloseCurrentStint(int endYear)
	{
		foreach (CoachStint stint in Stints)
		{
			if (stint.EndYear is null or 0)
			{
				stint.EndYear = endYear;
			}
		}
	}

	public CoachStint? CurrentStint =>
		Stints.LastOrDefault(stint => stint.IsCurrent);

	public void OpenStint(string organizationId, string schoolName, int startYear)
	{
		CloseCurrentStint(startYear - 1);
		ConsecutivePoorSeasons = 0;
		BoardSatisfactionScore = SeasonReview.OpeningSatisfaction(BoardSatisfaction.Average);
		Stints.Add(new CoachStint
		{
			OrganizationId = organizationId,
			SchoolName = schoolName,
			StartYear = startYear,
		});
	}

	/// <summary>
	/// Fills a missing first stop so a signed career never shows a leftover
	/// prototype résumé. Does not close seats or reset board standing.
	/// </summary>
	public bool EnsureCurrentStint(string schoolName)
	{
		if (!HasSignedContract || Stints.Count > 0)
		{
			return false;
		}

		string name = string.IsNullOrWhiteSpace(schoolName) ? UserOrganizationId : schoolName.Trim();
		int year = CurrentSeasonYear > 0 ? CurrentSeasonYear : 2026;
		Stints.Add(new CoachStint
		{
			OrganizationId = UserOrganizationId,
			SchoolName = name,
			StartYear = year,
		});
		if (string.IsNullOrWhiteSpace(Contract.SchoolName))
		{
			Contract.SchoolName = name;
			Contract.OrganizationId = UserOrganizationId;
		}

		return true;
	}

	public void AddSeason(int wins, int losses, bool wonDistrict, bool wonRegional, bool wonState, bool targetMet) =>
		AddSeason(
			new ProgramSeasonResult
			{
				OrganizationId = CurrentStint?.OrganizationId ?? UserOrganizationId,
				SeasonYear = CurrentSeasonYear,
				Wins = wins,
				Losses = losses,
				RegularWins = wins,
				RegularLosses = losses,
				WonDistrict = wonDistrict,
				WonRegional = wonRegional,
				WonState = wonState,
				Postseason = ProgramPostseason.HistoryLabel(wonDistrict, wonRegional, wonState, wonDistrict ? 1 : 8),
			},
			CurrentStint?.SchoolName ?? SchoolNameFromContract(),
			targetMet);

	public void AddSeason(ProgramSeasonResult season, string schoolName, bool targetMet)
	{
		CareerWins += Math.Max(0, season.Wins);
		CareerLosses += Math.Max(0, season.Losses);
		if (season.WonDistrict)
		{
			DistrictTitles++;
		}

		if (season.WonRegional)
		{
			RegionalTitles++;
		}

		if (season.WonState)
		{
			StateTitles++;
		}

		SeasonsCoached++;
		Contract.TickSeason();
		Reputation = CoachReputation.AfterSeason(
			Reputation,
			season.Wins > season.Losses,
			season.WonDistrict || season.WonRegional || season.WonState,
			season.WonDistrict,
			season.WonRegional,
			season.WonState,
			targetMet);
		CoachSeason record = CoachSeason.From(season, schoolName);
		Seasons.Add(record);
		CurrentStint?.AddSeason(season);
	}

	public int SeasonsAtCurrentSchool()
	{
		CoachStint? stint = CurrentStint;
		if (stint == null)
		{
			return 0;
		}

		int count = 0;
		foreach (CoachSeason season in Seasons)
		{
			if (season.OrganizationId == stint.OrganizationId)
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Offseason hires start the following baseball year when this season
	/// is already on the résumé at the current school.
	/// </summary>
	public int NextStintStartYear()
	{
		int year = CurrentSeasonYear;
		string org = UserOrganizationId;
		foreach (CoachSeason season in Seasons)
		{
			if (season.SeasonYear == year
				&& (string.IsNullOrWhiteSpace(org)
					|| string.Equals(season.OrganizationId, org, StringComparison.Ordinal)))
			{
				return year + 1;
			}
		}

		return year;
	}

	private string SchoolNameFromContract() =>
		string.IsNullOrWhiteSpace(Contract.SchoolName) ? "" : Contract.SchoolName;
}
