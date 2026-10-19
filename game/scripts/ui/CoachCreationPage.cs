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
		Control identity = BuildIdentityCard();
		identity.SizeFlagsStretchRatio = 0.75f;
		body.AddChild(identity);
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
		var wrap = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 2.1f,
		};
		wrap.AddThemeConstantOverride("separation", 16);

		// Centre: the studio headshot with a turntable.
		PanelContainer photoCard = CareerCreationChrome.Card();
		photoCard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		var photoCol = new VBoxContainer();
		photoCol.AddThemeConstantOverride("separation", 10);
		photoCard.AddChild(photoCol);
		photoCol.AddChild(CareerCreationChrome.Heading("COACH APPEARANCE", _semibold, 13, CareerCreationChrome.Accent));
		_portrait = new PersonPortrait
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(300, 375),
			Live = true,
		};
		photoCol.AddChild(_portrait);
		var turn = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		turn.AddThemeConstantOverride("separation", 6);
		foreach ((string label, float yaw) in new[] { ("LEFT", -38f), ("FRONT", 0f), ("RIGHT", 38f), ("PROFILE", 80f) })
		{
			float y = yaw;
			turn.AddChild(CareerCreationChrome.Ghost(label, _medium, () =>
			{
				_portrait.Yaw = y;
				_portrait.Refresh();
			}, UiClickSound.Select));
		}

		turn.AddChild(CareerCreationChrome.Ghost("CAP", _medium, () =>
		{
			_portrait.Cap = !_portrait.Cap;
			_portrait.Refresh();
		}, UiClickSound.Select));
		photoCol.AddChild(turn);
		var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		actions.AddThemeConstantOverride("separation", 8);
		actions.AddChild(CareerCreationChrome.Ghost("RANDOMIZE", _semibold, Randomize, UiClickSound.Select));
		actions.AddChild(CareerCreationChrome.Ghost("RESET", _semibold, ResetAppearance, UiClickSound.Back));
		photoCol.AddChild(actions);
		wrap.AddChild(photoCard);

		// Right: the face editor, grouped into tabs of sliders.
		PanelContainer editCard = CareerCreationChrome.Card();
		editCard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		editCard.SizeFlagsStretchRatio = 1.05f;
		var editCol = new VBoxContainer();
		editCol.AddThemeConstantOverride("separation", 8);
		editCard.AddChild(editCol);
		editCol.AddChild(CareerCreationChrome.Heading("FACE CREATOR", _semibold, 13, CareerCreationChrome.Accent));
		var tabs = new HFlowContainer();
		tabs.AddThemeConstantOverride("h_separation", 4);
		tabs.AddThemeConstantOverride("v_separation", 4);
		editCol.AddChild(tabs);
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		editCol.AddChild(scroll);
		_editor = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_editor.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_editor);
		foreach (string group in EditorGroups)
		{
			string g = group;
			var tab = new Button { Text = g.ToUpperInvariant(), ToggleMode = true };
			tab.AddThemeFontOverride("font", _semibold);
			tab.AddThemeFontSizeOverride("font_size", 12);
			tab.Pressed += () => ShowGroup(g);
			_tabs[g] = tab;
			tabs.AddChild(tab);
		}

		wrap.AddChild(editCard);
		ShowGroup(EditorGroups[0]);
		return wrap;
	}

	private static readonly string[] EditorGroups = ["Skin & Heritage", "Head", "Jaw", "Eyes", "Nose", "Mouth", "Hair", "Build"];
	private readonly Dictionary<string, Button> _tabs = new();
	private readonly Dictionary<int, HSlider> _sliders = new();

	private void ShowGroup(string group)
	{
		foreach ((string key, Button tab) in _tabs)
		{
			tab.SetPressedNoSignal(key == group);
		}

		foreach (Node child in _editor.GetChildren())
		{
			_editor.RemoveChild(child);
			child.QueueFree();
		}

		_cycles.Clear();
		_sliders.Clear();
		switch (group)
		{
			case "Skin & Heritage":
				AddCycle<SkinTone>("SKIN TONE", () => Coach.Appearance.SkinTone, value => Coach.Appearance.SkinTone = value);
				AddCycle<EyeColor>("EYE COLOR", () => Coach.Appearance.EyeColor, value => Coach.Appearance.EyeColor = value);
				AddSculptSliders("Heritage");
				_editor.AddChild(Note("Heritage blends real facial structure from different populations. Mix freely."));
				break;
			case "Head":
				AddCycle<FaceShape>("FACE SHAPE", () => Coach.Appearance.FaceShape, value => Coach.Appearance.FaceShape = value);
				AddSculptSliders("Head");
				AddSculptSliders("Cheeks");
				break;
			case "Hair":
				AddCycle<HairStyle>("HAIRCUT", () => Coach.Appearance.HairStyle, value => Coach.Appearance.HairStyle = value);
				AddCycle<HairColor>("HAIR COLOR", () => Coach.Appearance.HairColor, value => Coach.Appearance.HairColor = value);
				AddCycle<FacialHairStyle>("FACIAL HAIR", () => Coach.Appearance.FacialHairStyle, value => Coach.Appearance.FacialHairStyle = value);
				AddCycle<HairColor>("BEARD COLOR", () => Coach.Appearance.FacialHairColor, value => Coach.Appearance.FacialHairColor = value);
				break;
			case "Eyes":
				AddCycle<EyeStyle>("EYE SHAPE", () => Coach.Appearance.EyeStyle, value => Coach.Appearance.EyeStyle = value);
				AddSculptSliders("Eyes");
				break;
			case "Nose":
				AddCycle<NoseStyle>("NOSE SHAPE", () => Coach.Appearance.NoseStyle, value => Coach.Appearance.NoseStyle = value);
				AddSculptSliders("Nose");
				break;
			case "Mouth":
				AddCycle<MouthStyle>("EXPRESSION", () => Coach.Appearance.MouthStyle, value => Coach.Appearance.MouthStyle = value);
				AddSculptSliders("Mouth");
				break;
			case "Build":
				AddCycle<BodyBuild>("BUILD", () => Coach.Appearance.BodyBuild, value => Coach.Appearance.BodyBuild = value);
				AddSculptSliders("Build");
				_editor.AddChild(Note("Age also changes the face: skin, jaw and hairline mature as the years go by."));
				break;
			default:
				AddSculptSliders(group);
				break;
		}

		RefreshEditorLabels();
	}

	private Label Note(string text)
	{
		Label note = CareerCreationChrome.Heading(text, _medium, 12, CareerCreationChrome.TextDim);
		note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		return note;
	}

	private void AddSculptSliders(string group)
	{
		for (int i = 0; i < FaceSculpt.Count; i++)
		{
			FaceSculptSlot slot = FaceSculpt.Slots[i];
			if (slot.Group != group)
			{
				continue;
			}

			int index = i;
			var row = new VBoxContainer();
			row.AddThemeConstantOverride("separation", 0);
			row.AddChild(CareerCreationChrome.Heading(slot.Label.ToUpperInvariant(), _medium, 11, CareerCreationChrome.TextDim));
			var slider = new HSlider
			{
				MinValue = slot.Heritage ? 0 : -100,
				MaxValue = 100,
				Step = 1,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0, 22),
			};
			slider.ValueChanged += value =>
			{
				if (_syncing)
				{
					return;
				}

				int[] sculpt = SculptValues();
				sculpt[index] = (int)value;
				Coach.Appearance.Sculpt = sculpt;
				RefreshPortrait();
			};
			_sliders[index] = slider;
			row.AddChild(slider);
			_editor.AddChild(row);
		}
	}

	/// <summary>The coach's current sculpt, seeded from the appearance the first time it's edited.</summary>
	private int[] SculptValues()
	{
		PersonAppearance a = Coach.Appearance;
		int[] values = FaceSculpt.FromSeed(a.Seed, a.SkinTone);
		if (a.Sculpt is { Length: > 0 } saved)
		{
			Array.Copy(saved, values, Math.Min(saved.Length, values.Length));
		}

		return values;
	}

	private void RefreshPortrait()
	{
		_portrait.Age = Coach.Age;
		_portrait.SetAppearance(Coach.Appearance);
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
		_portrait.Age = Coach.Age;
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
		RefreshPortrait();
		RefreshValidation();
	}

	private void Randomize()
	{
		CareerCreationSession.Current.RandomizeAppearance();
		_portrait.Modulate = new Color(1f, 1f, 1f, 0.35f);
		_portrait.Age = Coach.Age;
		_portrait.SetAppearance(Coach.Appearance);
		Tween tween = CreateTween();
		tween.TweenProperty(_portrait, "modulate:a", 1f, 0.16f);
		RefreshEditorLabels();
	}

	private void ResetAppearance()
	{
		CareerCreationSession.Current.ResetAppearance();
		_portrait.Age = Coach.Age;
		_portrait.SetAppearance(Coach.Appearance);
		RefreshEditorLabels();
	}

	private void RefreshAppearance()
	{
		_portrait.Age = Coach.Age;
		_portrait.SetAppearance(Coach.Appearance);
		RefreshEditorLabels();
	}

	private void RefreshEditorLabels()
	{
		PersonAppearance appearance = Coach.Appearance;
		SetCycle("SKIN TONE", AppearanceCatalog.DisplayName(appearance.SkinTone));
		SetCycle("FACE SHAPE", AppearanceCatalog.DisplayName(appearance.FaceShape));
		SetCycle("HAIRCUT", AppearanceCatalog.DisplayName(appearance.HairStyle));
		SetCycle("HAIR COLOR", AppearanceCatalog.DisplayName(appearance.HairColor));
		SetCycle("BEARD COLOR", AppearanceCatalog.DisplayName(appearance.FacialHairColor));
		SetCycle("EYE COLOR", AppearanceCatalog.DisplayName(appearance.EyeColor));
		SetCycle("EYE SHAPE", AppearanceCatalog.DisplayName(appearance.EyeStyle));
		SetCycle("NOSE SHAPE", AppearanceCatalog.DisplayName(appearance.NoseStyle));
		SetCycle("EXPRESSION", AppearanceCatalog.DisplayName(appearance.MouthStyle));
		SetCycle("FACIAL HAIR", AppearanceCatalog.DisplayName(appearance.FacialHairStyle));
		SetCycle("BUILD", AppearanceCatalog.DisplayName(appearance.BodyBuild));
		bool wasSyncing = _syncing;
		_syncing = true;
		int[] sculpt = SculptValues();
		foreach ((int index, HSlider slider) in _sliders)
		{
			slider.Value = sculpt[index];
		}

		_syncing = wasSyncing;
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
