using HSBM.Domain;

namespace HSBM.Application;

public interface ICareerStore : IDisposable
{
	string DatabasePath { get; }
	bool HasCareer();
	void ReplaceAll(CareerState state);
	CareerState Load();
}

public sealed class CreateCareerRequest
{
	public string Name { get; init; } = "New Career";
	public int Seed { get; init; }
	public string? UserOrganizationId { get; init; }
	public int SeasonYear { get; init; } = LeagueCatalog.DefaultSeasonYear;
	public CoachProfile? Coach { get; init; }
	public CompetitionLevel CompetitionLevel { get; init; } = CompetitionLevel.HighSchool;
}

public sealed class CreateCareerResult
{
	public required Guid CareerId { get; init; }
	public required string Name { get; init; }
	public required int Seed { get; init; }
	public required DateOnly CurrentDate { get; init; }
	public required int PlayerCount { get; init; }
	public required int GameCount { get; init; }
	public required string UserOrganizationId { get; init; }
}

public sealed class GameSummary
{
	public required Guid GameId { get; init; }
	public required DateOnly Date { get; init; }
	public required string AwayTeamId { get; init; }
	public required string HomeTeamId { get; init; }
	public required int AwayScore { get; init; }
	public required int HomeScore { get; init; }
	public required int InningsPlayed { get; init; }
	public required TeamLevel TeamLevel { get; init; }
	public required GameType GameType { get; init; }
	public required string WinningTeamId { get; init; }

	public bool Involves(string teamId) =>
		HomeTeamId == teamId || AwayTeamId == teamId;
}

public sealed class AdvanceDayResult
{
	public required DateOnly PreviousDate { get; init; }
	public required DateOnly NewDate { get; init; }
	public required IReadOnlyList<GameSummary> GamesPlayed { get; init; }
	public IReadOnlyList<NewsStory> StoriesPublished { get; init; } = [];
	public IReadOnlyList<PlayerTransaction> TransactionsPosted { get; init; } = [];
	public SeasonPhase Phase { get; init; }
	public bool RolledOver { get; init; }
}

public sealed record StandingRow
{
	public required int Rank { get; init; }
	public required string TeamId { get; init; }
	public required string OrganizationId { get; init; }
	public required string Name { get; init; }
	public required string DistrictId { get; init; }
	public required int Wins { get; init; }
	public required int Losses { get; init; }
	public required int DistrictWins { get; init; }
	public required int DistrictLosses { get; init; }
	public required int RunsFor { get; init; }
	public required int RunsAgainst { get; init; }
	public required double WinningPercentage { get; init; }
}

public sealed class BattingLeaderRow
{
	public required Guid PlayerId { get; init; }
	public required string Name { get; init; }
	public required string TeamId { get; init; }
	public required string Position { get; init; }
	public required BattingSeasonStats Stats { get; init; }
}

public sealed class PitchingLeaderRow
{
	public required Guid PlayerId { get; init; }
	public required string Name { get; init; }
	public required string TeamId { get; init; }
	public required string Position { get; init; }
	public required PitchingSeasonStats Stats { get; init; }
}
