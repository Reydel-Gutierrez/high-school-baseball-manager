using HSBM.Application;
using HSBM.Domain;
using Godot;

/// <summary>
/// Placeholder for the coaching-background stage. Back restores coach creation.
/// </summary>
public partial class CoachBackgroundPage : Control
{
	public override void _Ready()
	{
		CareerCreationSession.Current.StartOrResume();
		CareerCreationSession.Current.State.Step = CareerCreationStep.CoachBackground;
		FontFile bold = CareerCreationChrome.Bold();
		FontFile semibold = CareerCreationChrome.SemiBold();
		FontFile medium = CareerCreationChrome.Medium();
		CareerCreationChrome.MountBackground(this);

		MarginContainer margin = CareerCreationChrome.PageMargin();
		AddChild(margin);
		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 14);
		margin.AddChild(root);
		root.AddChild(CareerCreationChrome.Heading("COACH BACKGROUND", bold, 36, CareerCreationChrome.TextPrimary));
		root.AddChild(CareerCreationChrome.Heading(
			"Choose the coaching experience that shaped your career.",
			medium,
			15,
			CareerCreationChrome.TextMuted));
		root.AddChild(CareerCreationChrome.Progress(CareerCreationStep.CoachBackground, bold, medium));

		PanelContainer card = CareerCreationChrome.Card();
		root.AddChild(card);
		var body = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 10);
		card.AddChild(body);

		CoachProfile coach = CareerCreationSession.Current.State.Coach;
		string name = string.IsNullOrWhiteSpace(coach.FullName) ? "YOUR COACH" : coach.FullName.ToUpperInvariant();
		body.AddChild(CareerCreationChrome.Heading(name, bold, 28, CareerCreationChrome.TextPrimary, HorizontalAlignment.Center));
		body.AddChild(CareerCreationChrome.Heading("COMING NEXT", semibold, 14, CareerCreationChrome.Accent, HorizontalAlignment.Center));
		body.AddChild(CareerCreationChrome.Heading(
			"Background archetypes and starting reputation will live here.\nYour coach identity and portrait are already saved in this session.",
			medium,
			15,
			CareerCreationChrome.TextMuted,
			HorizontalAlignment.Center));

		var footer = new HBoxContainer();
		footer.AddThemeConstantOverride("separation", 10);
		Button back = CareerCreationChrome.Ghost("BACK", semibold, GoBack, UiClickSound.Back);
		footer.AddChild(back);
		footer.AddChild(CareerCreationChrome.Flex());
		root.AddChild(footer);
		CareerCreationChrome.FadeIn(this);
		CallDeferred(nameof(FocusBack), back);
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			GoBack();
			GetViewport().SetInputAsHandled();
		}
	}

	private void FocusBack(Button back) => back.GrabFocus();

	private void GoBack()
	{
		CareerCreationSession.Current.State.Step = CareerCreationStep.CoachIdentity;
		GetTree().ChangeSceneToFile(CareerCreationChrome.CoachCreationScene);
	}
}
