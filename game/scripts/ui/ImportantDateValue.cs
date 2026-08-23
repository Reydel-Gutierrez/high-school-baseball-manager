using Godot;

/// <summary>
/// Colors important-date value labels white, or red when days remaining is under 8.
/// Attach to each date-value Label and set DaysRemaining in the inspector.
/// </summary>
public partial class ImportantDateValue : Label
{
	private static readonly Color ValueNormal = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color ValueUrgent = new(0.906f, 0.298f, 0.235f, 1f); // #E74C3C

	[Export]
	public int DaysRemaining { get; set; } = 99;

	public override void _Ready()
	{
		ApplyUrgencyColor();
	}

	public void SetDaysRemaining(int daysRemaining)
	{
		DaysRemaining = daysRemaining;
		ApplyUrgencyColor();
	}

	private void ApplyUrgencyColor()
	{
		AddThemeColorOverride("font_color", DaysRemaining < 8 ? ValueUrgent : ValueNormal);
	}
}
