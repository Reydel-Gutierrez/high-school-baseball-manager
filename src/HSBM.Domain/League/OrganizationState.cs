namespace HSBM.Domain;

public sealed record DistrictIdentity(
	string Id,
	string Name,
	string Abbreviation,
	string Region);

public sealed class OrganizationState
{
	public OrganizationIdentity Identity { get; set; } = null!;
	public string DistrictId { get; set; } = "";
	public CompetitionLevel CompetitionLevel { get; set; } = CompetitionLevel.HighSchool;
	public ProgramTalentTier TalentTier { get; set; } = ProgramTalentTier.Average;
	public ProgramProfile Profile { get; set; } = new();
	public ProgramCoach? HeadCoach { get; set; }
	public List<ProgramSeasonResult> SeasonHistory { get; set; } = [];
	public List<ProgramRecordHolder> RecordBook { get; set; } = [];
	public List<ProgramHallOfFameInductee> HallOfFame { get; set; } = [];

	public string Id => Identity.Id;
	public string Name => Identity.Name;
	public string ShortName => Identity.ShortName;
	public string DisplayName => OrganizationNaming.DisplayName(Identity.Name);

	public ProgramResources Resources => Profile.Resources;

	public void RefreshTalentFromReputation() =>
		TalentTier = ProgramReputation.TalentBand(Profile.Reputation);
}
