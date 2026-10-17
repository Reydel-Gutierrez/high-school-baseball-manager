using HSBM.Domain;

namespace HSBM.Application;

public enum CareerCreationStep
{
	CoachIdentity = 0,
	CoachBackground = 1,
	WorldGeneration = 2,
	JobSelection = 3,
}

/// <summary>
/// In-memory draft while the player walks through new-career setup.
/// Not written to SQLite until the career is confirmed.
/// </summary>
public sealed class CareerCreationState
{
	public CareerCreationStep Step { get; set; } = CareerCreationStep.CoachIdentity;

	public CoachProfile Coach { get; set; } = new();

	public string? SelectedBackgroundId { get; set; }

	public string WorldName { get; set; } = "";

	public int? WorldSeed { get; set; }

	public SimulationDetail SimulationDetail { get; set; } = SimulationDetail.Standard;

	public string? SelectedOrganizationId { get; set; }

	public int AppearanceRoll { get; set; }

	public bool HasStarted { get; set; }

	public CoachIdentityValidation ValidateCoach() => CoachIdentity.Validate(Coach);

	public CareerCreationState Clone() => new()
	{
		Step = Step,
		Coach = Coach.Clone(),
		SelectedBackgroundId = SelectedBackgroundId,
		WorldName = WorldName,
		WorldSeed = WorldSeed,
		SimulationDetail = SimulationDetail,
		SelectedOrganizationId = SelectedOrganizationId,
		AppearanceRoll = AppearanceRoll,
		HasStarted = HasStarted,
	};
}
