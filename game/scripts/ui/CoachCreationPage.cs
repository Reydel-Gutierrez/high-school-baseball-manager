using System;
using System.Collections.Generic;
using HSBM.Application;
using HSBM.Domain;
using Godot;

/// <summary>
/// First new-career stage: coach identity and appearance.
/// </summary>
public partial class CoachCreationPage : Control
{
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private LineEdit _firstName = null!;
	private LineEdit _lastName = null!;
	private LineEdit _hometown = null!;
	private Control _ageStepper = null!;
	private PersonPortrait _portrait = null!;
	private VBoxContainer _editor = null!;
	private Button _continue = null!;
	private Label _hint = null!;
	private bool _syncing;
	private readonly Dictionary<string, Control> _cycles = new();

	public override void _Ready()
	{
		_bold = CareerCreationChrome.Bold();
		_semibold = CareerCreationChrome.SemiBold();
		_medium = CareerCreationChrome.Medium();
		CareerCreationSession.Current.StartOrResume();
		CareerCreationSession.Current.State.Step = CareerCreationStep.CoachIdentity;
		Build();
		LoadFromState();
		RefreshValidation();
		CallDeferred(nameof(FocusFirst));
		CareerCreationChrome.FadeIn(this);
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is not InputEventKey key || !key.Pressed || key.Echo)
		{
			return;
		}

		if (key.Keycode == Key.Escape)
		{
			GoBack();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (key.Keycode is Key.Enter or Key.KpEnter)
		{
			TryContinue();
			GetViewport().SetInputAsHandled();
		}
	}

	private void FocusFirst() => _firstName.GrabFocus();

	private void Build()
	{
		CareerCreationChrome.MountBackground(this);
		MarginContainer margin = CareerCreationChrome.PageMargin();
		AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 14);
		margin.AddChild(root);
		root.AddChild(BuildHeader());

		var body = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 16);
		root.AddChild(body);
		body.AddChild(BuildIdentityCard());
		body.AddChild(BuildAppearanceCard());
		root.AddChild(BuildFooter());
	}

	private Control BuildHeader()
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 4);
		col.AddChild(CareerCreationChrome.Heading("CREATE YOUR COACH", _bold, 36, CareerCreationChrome.TextPrimary));
		col.AddChild(CareerCreationChrome.Heading(
			"Build the coach who will lead your program.",
			_medium,
			15,
			CareerCreationChrome.TextMuted));
		col.AddChild(CareerCreationChrome.Progress(CareerCreationStep.CoachIdentity, _bold, _medium));
		return col;
	}

	private Control BuildIdentityCard()
	{
		PanelContainer card = CareerCreationChrome.Card();
		card.SizeFlagsStretchRatio = 0.92f;
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 12);
		card.AddChild(layout);
		layout.AddChild(CareerCreationChrome.Heading("COACH IDENTITY", _semibold, 13, CareerCreationChrome.Accent));
		layout.AddChild(Labeled("FIRST NAME", _firstName = CareerCreationChrome.Field(_semibold, "First name", CoachIdentity.MaxNameLength)));
		layout.AddChild(Labeled("LAST NAME", _lastName = CareerCreationChrome.Field(_semibold, "Last name", CoachIdentity.MaxNameLength)));
		_ageStepper = CareerCreationChrome.Stepper("34", _semibold, () => ChangeAge(-1), () => ChangeAge(1));
		layout.AddChild(Labeled("AGE", _ageStepper));
		layout.AddChild(Labeled("HOMETOWN", _hometown = CareerCreationChrome.Field(_semibold, "Miami, FL", CoachIdentity.MaxHometownLength)));
		_firstName.TextChanged += _ => OnIdentityChanged();
		_lastName.TextChanged += _ => OnIdentityChanged();
		_hometown.TextChanged += _ => OnIdentityChanged();
		_firstName.TextSubmitted += _ => _lastName.GrabFocus();
		_lastName.TextSubmitted += _ => _hometown.GrabFocus();
		_hometown.TextSubmitted += _ => TryContinue();
		layout.AddChild(CareerCreationChrome.Flex());
		_hint = CareerCreationChrome.Heading("", _medium, 13, CareerCreationChrome.TextDim);
		_hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(_hint);
		return card;
	}

	private Control BuildAppearanceCard()
	{
		PanelContainer card = CareerCreationChrome.Card();
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 10);
		card.AddChild(layout);
		layout.AddChild(CareerCreationChrome.Heading("COACH APPEARANCE", _semibold, 13, CareerCreationChrome.Accent));

		_portrait = new PersonPortrait
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(220, 280),
		};
		layout.AddChild(_portrait);

		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 8);
		actions.AddChild(CareerCreationChrome.Ghost("RANDOMIZE", _semibold, Randomize, UiClickSound.Select));
		actions.AddChild(CareerCreationChrome.Ghost("EDIT APPEARANCE", _semibold, ToggleEditor, UiClickSound.Navigate));
		layout.AddChild(actions);

		_editor = new VBoxContainer { Visible = false };
		_editor.AddThemeConstantOverride("separation", 6);
		layout.AddChild(_editor);
		AddCycle<SkinTone>("SKIN", () => Coach.Appearance.SkinTone, value => Coach.Appearance.SkinTone = value);
		AddCycle<FaceShape>("FACE", () => Coach.Appearance.FaceShape, value => Coach.Appearance.FaceShape = value);
		AddCycle<HairStyle>("HAIR", () => Coach.Appearance.HairStyle, value => Coach.Appearance.HairStyle = value);
		AddCycle<HairColor>("HAIR COLOR", () => Coach.Appearance.HairColor, value => Coach.Appearance.HairColor = value);
		AddCycle<EyeColor>("EYES", () => Coach.Appearance.EyeColor, value => Coach.Appearance.EyeColor = value);
		AddCycle<FacialHairStyle>("FACIAL HAIR", () => Coach.Appearance.FacialHairStyle, value => Coach.Appearance.FacialHairStyle = value);
		AddCycle<AccessoryStyle>("ACCESSORY", () => Coach.Appearance.Accessory, value => Coach.Appearance.Accessory = value);
		AddCycle<BodyBuild>("BUILD", () => Coach.Appearance.BodyBuild, value => Coach.Appearance.BodyBuild = value);
		AddCycle<OutfitStyle>("OUTFIT", () => Coach.Appearance.OutfitStyle, value => Coach.Appearance.OutfitStyle = value);

		var editorActions = new HBoxContainer();
		editorActions.AddThemeConstantOverride("separation", 8);
		editorActions.AddChild(CareerCreationChrome.Ghost("RANDOMIZE ALL", _medium, Randomize, UiClickSound.Select));
		editorActions.AddChild(CareerCreationChrome.Ghost("RESET", _medium, ResetAppearance, UiClickSound.Back));
		_editor.AddChild(editorActions);
		return card;
	}

	private Control BuildFooter()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		row.AddChild(CareerCreationChrome.Ghost("BACK", _semibold, GoBack, UiClickSound.Back));
		row.AddChild(CareerCreationChrome.Flex());
		_continue = CareerCreationChrome.Primary("CONTINUE", _bold, TryContinue, UiClickSound.Confirm);
		row.AddChild(_continue);
		return row;
	}

	private static Control Labeled(string label, Control field)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 4);
		col.AddChild(CareerCreationChrome.Heading(label, CareerCreationChrome.Medium(), 11, CareerCreationChrome.TextDim));
		col.AddChild(field);
		return col;
	}

	private void AddCycle<T>(string label, Func<T> get, Action<T> set) where T : struct, Enum
	{
		Control stepper = CareerCreationChrome.Stepper(
			AppearanceCatalog.DisplayName(get()),
			_semibold,
			() =>
			{
				set(AppearanceCatalog.Cycle(get(), -1));
				RefreshAppearance();
			},
			() =>
			{
				set(AppearanceCatalog.Cycle(get(), 1));
				RefreshAppearance();
			});
		_cycles[label] = stepper;
		_editor.AddChild(Labeled(label, stepper));
	}

	private CoachProfile Coach => CareerCreationSession.Current.State.Coach;

	private void LoadFromState()
	{
		_syncing = true;
		_firstName.Text = Coach.FirstName;
		_lastName.Text = Coach.LastName;
		_hometown.Text = Coach.Hometown;
		CareerCreationChrome.SetStepperValue(_ageStepper, Coach.Age.ToString());
		_portrait.SetAppearance(Coach.Appearance);
		_syncing = false;
		RefreshEditorLabels();
	}

	private void OnIdentityChanged()
	{
		if (_syncing)
		{
			return;
		}

		Coach.FirstName = _firstName.Text;
		Coach.LastName = _lastName.Text;
		Coach.Hometown = _hometown.Text;
		RefreshValidation();
	}

	private void ChangeAge(int delta)
	{
		Coach.Age = CoachIdentity.ClampAge(Coach.Age + delta);
		CareerCreationChrome.SetStepperValue(_ageStepper, Coach.Age.ToString());
		RefreshValidation();
	}

	private void Randomize()
	{
		CareerCreationSession.Current.RandomizeAppearance();
		_portrait.Modulate = new Color(1f, 1f, 1f, 0.35f);
		_portrait.SetAppearance(Coach.Appearance);
		Tween tween = CreateTween();
		tween.TweenProperty(_portrait, "modulate:a", 1f, 0.16f);
		RefreshEditorLabels();
	}

	private void ResetAppearance()
	{
		CareerCreationSession.Current.ResetAppearance();
		_portrait.SetAppearance(Coach.Appearance);
		RefreshEditorLabels();
	}

	private void ToggleEditor() => _editor.Visible = !_editor.Visible;

	private void RefreshAppearance()
	{
		_portrait.SetAppearance(Coach.Appearance);
		RefreshEditorLabels();
	}

	private void RefreshEditorLabels()
	{
		PersonAppearance appearance = Coach.Appearance;
		SetCycle("SKIN", AppearanceCatalog.DisplayName(appearance.SkinTone));
		SetCycle("FACE", AppearanceCatalog.DisplayName(appearance.FaceShape));
		SetCycle("HAIR", AppearanceCatalog.DisplayName(appearance.HairStyle));
		SetCycle("HAIR COLOR", AppearanceCatalog.DisplayName(appearance.HairColor));
		SetCycle("EYES", AppearanceCatalog.DisplayName(appearance.EyeColor));
		SetCycle("FACIAL HAIR", AppearanceCatalog.DisplayName(appearance.FacialHairStyle));
		SetCycle("ACCESSORY", AppearanceCatalog.DisplayName(appearance.Accessory));
		SetCycle("BUILD", AppearanceCatalog.DisplayName(appearance.BodyBuild));
		SetCycle("OUTFIT", AppearanceCatalog.DisplayName(appearance.OutfitStyle));
	}

	private void SetCycle(string key, string value)
	{
		if (_cycles.TryGetValue(key, out Control? stepper))
		{
			CareerCreationChrome.SetStepperValue(stepper, value);
		}
	}

	private void RefreshValidation()
	{
		CoachIdentityValidation result = CareerCreationSession.Current.State.ValidateCoach();
		_continue.Disabled = !result.IsValid;
		_hint.Text = result.IsValid
			? "Identity locked in. Continue when the portrait feels right."
			: result.Hint;
		_hint.AddThemeColorOverride("font_color", result.IsValid ? CareerCreationChrome.TextMuted : CareerCreationChrome.TextDim);
	}

	private void TryContinue()
	{
		RefreshValidation();
		if (_continue.Disabled)
		{
			UiSounds.Play(UiSound.Error);
			return;
		}

		CareerCreationSession.Current.State.Step = CareerCreationStep.WorldGeneration;
		GetTree().ChangeSceneToFile(CareerCreationChrome.WorldSetupScene);
	}

	private void GoBack()
	{
		GetTree().ChangeSceneToFile(CareerCreationChrome.MainMenuScene);
	}
}
