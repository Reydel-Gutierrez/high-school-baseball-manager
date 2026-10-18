namespace HSBM.Domain;

/// <summary>
/// Persistent game-world root: clock, seed, competition level, schools,
/// teams, players, coaches, schedules, finances, and history.
/// Finances and archives live on each <see cref="OrganizationState"/>.
/// </summary>
public sealed class GameWorld
{
	public int Seed { get; set; }
	public DateOnly CurrentDate { get; set; }
	public int CurrentSeasonYear { get; set; }
	public CompetitionLevel CompetitionLevel { get; set; } = CompetitionLevel.HighSchool;

	public List<DistrictIdentity> Districts { get; set; } = [];
	public List<OrganizationState> Organizations { get; set; } = [];
	public List<TeamIdentity> Teams { get; set; } = [];
	public List<Player> Players { get; set; } = [];
	public List<ProgramCoach> Coaches { get; set; } = [];
	public List<ScheduledGame> Games { get; set; } = [];
	public List<TeamSeasonRecord> TeamRecords { get; set; } = [];
	public List<BattingSeasonStats> BattingStats { get; set; } = [];
	public List<PitchingSeasonStats> PitchingStats { get; set; } = [];
	public List<FieldingSeasonStats> FieldingStats { get; set; } = [];
	public List<TeamLineup> Lineups { get; set; } = [];
	public List<TeamRotation> Rotations { get; set; } = [];
	public List<TeamTraining> Training { get; set; } = [];
	public List<GameBoxScore> BoxScores { get; set; } = [];
	public List<NewsStory> News { get; set; } = [];
	public List<PlayerTransaction> Transactions { get; set; } = [];
	public PostseasonState? Postseason { get; set; }
	public List<SeasonAwards> Awards { get; set; } = [];
	public List<HistoricalSeason> HistoricalSeasons { get; set; } = [];
	public List<string> SeasonProfileCommits { get; set; } = [];

	public IEnumerable<OrganizationState> Programs
	{
		get
		{
			foreach (OrganizationState org in Organizations)
			{
				if (!LeagueCatalog.IsUnsignedOrganization(org.Id))
				{
					yield return org;
				}
			}
		}
	}

	public ProgramCoach? CoachFor(string organizationId)
	{
		foreach (ProgramCoach coach in Coaches)
		{
			if (coach.OrganizationId == organizationId)
			{
				return coach;
			}
		}

		return null;
	}
}
