using HSBM.Domain;
using Godot;

/// <summary>
/// Reusable portrait view. Bind an appearance and it redraws at any size.
/// </summary>
public partial class PersonPortrait : Control
{
	private static readonly IPortraitRenderer Renderer = new CompositePortraitRenderer();
	private PersonAppearance _appearance = new();

	public PersonPortrait()
	{
		ClipContents = true;
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(96, 120);
	}

	public PersonAppearance Appearance
	{
		get => _appearance;
		set => SetAppearance(value);
	}

	public void SetAppearance(PersonAppearance appearance)
	{
		_appearance = appearance?.Clone() ?? new PersonAppearance();
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		Renderer.Draw(this, _appearance, new Rect2(Vector2.Zero, Size));
	}
}
