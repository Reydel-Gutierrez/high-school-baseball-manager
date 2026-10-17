using System.Linq;
using HSBM.Application;
using Godot;

/// <summary>
/// New-career world setup. Generates the baseball world after coach identity;
/// job selection happens on My Career once the save exists.
/// </summary>
public partial class WorldSetupPage : Control
{
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private LineEdit _worldName = null!;
	private LineEdit _seed = null!;
	private Control _detailStepper = null!;
	private Button _generate = null!;
	private Label _hint = null!;

	public override void _Ready()
	{
		CareerCreationSession.Current.StartOrResume();
		CareerCreationSession.Current.State.Step = CareerCreationStep.WorldGeneration;
		_bold = CareerCreationChrome.Bold();
		_semibold = CareerCreationChrome.SemiBold();
		_medium = CareerCreationChrome.Medium();
		CareerCreationChrome.MountBackground(this);

		MarginContainer margin = CareerCreationChrome.PageMargin();
		AddChild(margin);
		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 14);
		margin.AddChild(root);
		root.AddChild(CareerCreationChrome.Heading("CREATE BASEBALL WORLD", _bold, 36, CareerCreationChrome.TextPrimary));
		root.AddChild(CareerCreationChrome.Heading(
			"A new baseball world is being created.",
			_medium,
			15,
			CareerCreationChrome.TextMuted));
		root.AddChild(CareerCreationChrome.Progress(CareerCreationStep.WorldGeneration, _bold, _medium));

		PanelContainer card = CareerCreationChrome.Card();
		root.AddChild(card);
		var body = new VBoxContainer();
		body.AddThemeConstantOverride("separation", 14);
		card.AddChild(body);
		body.AddChild(CareerCreationChrome.Heading("WORLD SETTINGS", _semibold, 13, CareerCreationChrome.Accent));
		body.AddChild(CareerCreationChrome.Heading(
			"Program reputations, histories, and the player population are generated automatically.",
			_medium,
			14,
			CareerCreationChrome.TextMuted));

		CareerCreationState state = CareerCreationSession.Current.State;
		if (string.IsNullOrWhiteSpace(state.WorldName))
		{
			int existing = CareerLibrary.ListOrdered().Count(slot => !slot.Id.StartsWith("sample-", System.StringComparison.Ordinal));
			state.WorldName = WorldSetup.SuggestWorldName(state.Coach.FirstName, existing);
		}

		_worldName = CareerCreationChrome.Field(_semibold, "World name", WorldSetup.MaxWorldNameLength);
		_worldName.Text = state.WorldName;
		body.AddChild(Labeled("WORLD NAME", _worldName));

		_seed = CareerCreationChrome.Field(_semibold, WorldSetup.AutoSeedLabel, 16);
		_seed.Text = state.WorldSeed.HasValue ? state.WorldSeed.Value.ToString() : WorldSetup.AutoSeedLabel;
		body.AddChild(Labeled("WORLD SEED", _seed));

		_detailStepper = CareerCreationChrome.Stepper(
			WorldSetup.SimulationDetailLabel(state.SimulationDetail),
			_semibold,
			() => { },
			() => { });
		body.AddChild(Labeled("SIMULATION DETAIL", _detailStepper));
		body.AddChild(CareerCreationChrome.Flex());
		_hint = CareerCreationChrome.Heading(
			"Generate the world, then pick a program on My Career.",
			_medium,
			13,
			CareerCreationChrome.TextDim);
		_hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		body.AddChild(_hint);

		var footer = new HBoxContainer();
		footer.AddThemeConstantOverride("separation", 10);
		footer.AddChild(CareerCreationChrome.Ghost("BACK", _semibold, GoBack, UiClickSound.Back));
		footer.AddChild(CareerCreationChrome.Flex());
		_generate = CareerCreationChrome.Primary("GENERATE WORLD", _bold, TryGenerate, UiClickSound.Confirm);
		footer.AddChild(_generate);
		root.AddChild(footer);

		_worldName.TextChanged += _ => RefreshValidation();
		_seed.TextChanged += _ => RefreshValidation();
		_worldName.TextSubmitted += _ => _seed.GrabFocus();
		_seed.TextSubmitted += _ => TryGenerate();
		CareerCreationChrome.FadeIn(this);
		CallDeferred(nameof(FocusName));
		RefreshValidation();
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			GoBack();
			GetViewport().SetInputAsHandled();
		}
	}

	private void FocusName() => _worldName.GrabFocus();

	private static Control Labeled(string label, Control field)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 4);
		col.AddChild(CareerCreationChrome.Heading(label, CareerCreationChrome.Medium(), 11, CareerCreationChrome.TextDim));
		col.AddChild(field);
		return col;
	}

	private void RefreshValidation()
	{
		string? nameError = WorldSetup.ValidateWorldName(_worldName.Text);
		bool seedOk = WorldSetup.TryParseSeed(_seed.Text, out _);
		_generate.Disabled = nameError != null || !seedOk;
		if (nameError != null)
		{
			_hint.Text = nameError;
		}
		else if (!seedOk)
		{
			_hint.Text = "World seed must be AUTO or a whole number.";
		}
		else
		{
			_hint.Text = "Generate the world, then pick a program on My Career.";
		}

		_hint.AddThemeColorOverride(
			"font_color",
			_generate.Disabled ? CareerCreationChrome.TextDim : CareerCreationChrome.TextMuted);
	}

	private void TryGenerate()
	{
		RefreshValidation();
		if (_generate.Disabled)
		{
			UiSounds.Play(UiSound.Error);
			return;
		}

		CareerCreationState state = CareerCreationSession.Current.State;
		state.WorldName = _worldName.Text.Trim();
		WorldSetup.TryParseSeed(_seed.Text, out int? parsed);
		state.WorldSeed = WorldSetup.ResolveSeed(parsed, state.Coach.Id);
		state.SimulationDetail = SimulationDetail.Standard;
		state.Step = CareerCreationStep.WorldGeneration;
		GetTree().ChangeSceneToFile(CareerCreationChrome.WorldGeneratingScene);
	}

	private void GoBack()
	{
		CareerCreationSession.Current.State.WorldName = _worldName.Text.Trim();
		CareerCreationSession.Current.State.Step = CareerCreationStep.CoachIdentity;
		GetTree().ChangeSceneToFile(CareerCreationChrome.CoachCreationScene);
	}
}
