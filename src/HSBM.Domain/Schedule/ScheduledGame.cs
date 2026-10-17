namespace HSBM.Domain;

public enum GameStatus
{
	Scheduled = 0,
	Completed = 1,
	Postponed = 2,
}

public enum GameType
{
	District = 0,
	NonDistrict = 1,
	DistrictTournament = 2,
	StateTournament = 3,
	CountyTournament = 4,
	Exhibition = 5,
	RegionalTournament = 6,
}

public static class GameTypeExtensions
{
	public static bool IsPostseason(this GameType type) =>
		type is GameType.DistrictTournament
			or GameType.StateTournament
			or GameType.CountyTournament
			or GameType.RegionalTournament;

	public static bool IsExhibition(this GameType type) => type == GameType.Exhibition;

	public static bool CountsInStandings(this GameType type) =>
		!type.IsExhibition() && !type.IsPostseason();
}

public sealed class ScheduledGame
{
	public Guid Id { get; set; }
	public int SeasonYear { get; set; }
	public DateOnly Date { get; set; }
	public string HomeTeamId { get; set; } = "";
	public string AwayTeamId { get; set; } = "";
	public TeamLevel TeamLevel { get; set; }
	public GameType GameType { get; set; }
	public GameStatus Status { get; set; }

	public int? HomeScore { get; set; }
	public int? AwayScore { get; set; }
	public int? InningsPlayed { get; set; }
	public string? WinningTeamId { get; set; }
	public string? LosingTeamId { get; set; }
	public Guid? WinningPitcherId { get; set; }
	public Guid? LosingPitcherId { get; set; }
	public Guid? SavePitcherId { get; set; }

	public bool Involves(string teamId) =>
		HomeTeamId == teamId || AwayTeamId == teamId;
}
