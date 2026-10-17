namespace HSBM.Domain;

/// <summary>
/// Sitting head coach at a program. The user's coach lives on
/// <see cref="Career"/>; every other school keeps one of these.
/// </summary>
public sealed class ProgramCoach
{
	public Guid Id { get; set; }
	public string OrganizationId { get; set; } = "";
	public CoachProfile Profile { get; set; } = new();

	public string FullName => Profile.FullName;

	public ProgramCoach Clone() => new()
	{
		Id = Id,
		OrganizationId = OrganizationId,
		Profile = Profile.Clone(),
	};
}
