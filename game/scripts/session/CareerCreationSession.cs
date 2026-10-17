using HSBM.Application;
using HSBM.Domain;
using HSBM.Simulation;
using Godot;

/// <summary>
/// Holds the in-progress new-career draft between creation screens
/// and the main menu. Nothing here is written to a save file.
/// </summary>
public partial class CareerCreationSession : Node
{
	public static CareerCreationSession Current { get; private set; } = null!;

	public CareerCreationState State { get; private set; } = new();

	public override void _EnterTree()
	{
		Current = this;
	}

	public void StartOrResume()
	{
		if (!State.HasStarted)
		{
			BeginFresh();
		}
	}

	public void BeginFresh()
	{
		Guid id = Guid.NewGuid();
		int seed = SeededRandom.Mix(0xC0A7C4, id);
		State = new CareerCreationState
		{
			HasStarted = true,
			Step = CareerCreationStep.CoachIdentity,
			Coach = new CoachProfile
			{
				Id = id,
				Age = CoachIdentity.DefaultAge,
				Appearance = PersonAppearanceGenerator.GenerateCoach(seed),
			},
		};
	}

	public void RandomizeAppearance()
	{
		State.AppearanceRoll++;
		int seed = SeededRandom.Mix(State.AppearanceRoll, State.Coach.Id);
		State.Coach.Appearance = PersonAppearanceGenerator.GenerateCoach(seed);
	}

	public void ResetAppearance()
	{
		int seed = State.Coach.Appearance.Seed;
		State.Coach.Appearance = PersonAppearanceGenerator.GenerateCoach(seed);
	}

	public void Clear()
	{
		State = new CareerCreationState();
	}
}
