using System;
using System.Text.Json.Serialization;

/// <summary>
/// Display metadata for one saved coaching career. The full world lives
/// in the career SQLite file; this is the slot list.
/// </summary>
public sealed class CareerSaveSlot
{
	public string Id { get; set; } = "";

	public string CoachName { get; set; } = "";

	/// <summary>
	/// Optional binder label from Save As. Empty means show the coach name.
	/// </summary>
	public string FileLabel { get; set; } = "";

	public string SchoolName { get; set; } = "";

	public string SchoolShortName { get; set; } = "";

	public string OrganizationId { get; set; } = "";

	public int SeasonNumber { get; set; } = 1;

	public int SeasonYear { get; set; } = 2026;

	public int Wins { get; set; }

	public int Losses { get; set; }

	public int WorldSeed { get; set; }

	public string CompetitionLevel { get; set; } = "HighSchool";

	public string Status { get; set; } = "Regular Season";

	public DateTime LastPlayedUtc { get; set; }

	[JsonIgnore]
	public string DisplayName =>
		string.IsNullOrWhiteSpace(FileLabel) ? CoachName : FileLabel;

	public string RecordLabel => $"{Wins}\u2013{Losses}";

	public string SeasonLine => $"Season {SeasonNumber}  ·  {SeasonYear}";

	public string LastPlayedLabel => LastPlayedUtc == default
		? "Never"
		: LastPlayedUtc.ToLocalTime().ToString("MMM d, yyyy");
}
