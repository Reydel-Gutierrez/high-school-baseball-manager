using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HSBM.Application;
using HSBM.Domain;
using HSBM.Persistence;
using HSBM.Simulation;
using Godot;

/// <summary>
/// Loading screen that plays career world generation as a visible feature
/// before landing on My Career to pick a program.
/// </summary>
public partial class WorldGeneratingPage : Control
{
	private const float MinStepSeconds = 0.42f;

	private readonly string[] _stageLabels =
	[
		WorldGenerationSteps.Programs(32),
		WorldGenerationSteps.Histories,
		WorldGenerationSteps.Varsity,
		WorldGenerationSteps.JuniorVarsity,
		WorldGenerationSteps.Identities,
		WorldGenerationSteps.Schedule,
	];

	private FontFile _bold = null!;
	private FontFile _medium = null!;
	private readonly List<Label> _stageNodes = [];
	private ColorRect _fill = null!;
	private Label _status = null!;
	private int _reportedStep = -1;
	private int _shownStep = -1;
	private float _stepAge;
	private bool _finished;
	private bool _failed;
	private bool _entered;
	private bool _slotReady;

	public override void _Ready()
	{
		CareerCreationSession.Current.StartOrResume();
		CareerCreationSession.Current.State.Step = CareerCreationStep.WorldGeneration;
		_bold = CareerCreationChrome.Bold();
		_medium = CareerCreationChrome.Medium();
		CareerCreationChrome.MountBackground(this);

		MarginContainer margin = CareerCreationChrome.PageMargin();
		AddChild(margin);
		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 16);
		margin.AddChild(root);
		root.AddChild(CareerCreationChrome.Heading("GENERATING BASEBALL WORLD", _bold, 36, CareerCreationChrome.TextPrimary));

		CareerCreationState state = CareerCreationSession.Current.State;
		string seedLine = state.WorldSeed.HasValue ? $"SEED  {state.WorldSeed.Value}" : "SEED  AUTO";
		root.AddChild(CareerCreationChrome.Heading(
			$"{state.WorldName}   ·   {seedLine}",
			_medium,
			15,
			CareerCreationChrome.TextMuted));
		root.AddChild(CareerCreationChrome.Progress(CareerCreationStep.WorldGeneration, _bold, _medium));

		PanelContainer card = CareerCreationChrome.Card();
		root.AddChild(card);
		var body = new VBoxContainer();
		body.AddThemeConstantOverride("separation", 10);
		card.AddChild(body);

		for (int i = 0; i < _stageLabels.Length; i++)
		{
			Label stage = CareerCreationChrome.Heading(_stageLabels[i], _medium, 18, CareerCreationChrome.TextDim);
			_stageNodes.Add(stage);
			body.AddChild(stage);
		}

		body.AddChild(CareerCreationChrome.Flex());
		var track = new PanelContainer();
		track.CustomMinimumSize = new Vector2(0, 18);
		track.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.06f, 0.10f, 0.95f),
			BorderColor = CareerCreationChrome.CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			ContentMarginLeft = 2,
			ContentMarginTop = 2,
			ContentMarginRight = 2,
			ContentMarginBottom = 2,
		});
		body.AddChild(track);
		var barHost = new Control { CustomMinimumSize = new Vector2(0, 12) };
		track.AddChild(barHost);
		_fill = new ColorRect
		{
			Color = CareerCreationChrome.Accent,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_fill.SetAnchor(Side.Left, 0);
		_fill.SetAnchor(Side.Top, 0);
		_fill.SetAnchor(Side.Bottom, 1);
		_fill.SetAnchor(Side.Right, 0.04f);
		barHost.AddChild(_fill);

		_status = CareerCreationChrome.Heading("Creating programs...", _medium, 13, CareerCreationChrome.TextMuted);
		body.AddChild(_status);

		CareerCreationChrome.FadeIn(this);
		RefreshStages();
		StartGeneration();
	}

	public override void _Process(double delta)
	{
		if (_failed || _entered)
		{
			return;
		}

		_stepAge += (float)delta;
		bool canAdvance = _stepAge >= MinStepSeconds && (_reportedStep > _shownStep || (_finished && _shownStep < _stageLabels.Length - 1));
		if (canAdvance)
		{
			_shownStep++;
			_stepAge = 0f;
			RefreshStages();
		}

		if (_finished && _slotReady && !_failed && _shownStep >= _stageLabels.Length - 1 && _stepAge >= MinStepSeconds)
		{
			EnterClubhouse();
		}
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (_failed && ev is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
		{
			GetTree().ChangeSceneToFile(CareerCreationChrome.WorldSetupScene);
			GetViewport().SetInputAsHandled();
		}
	}

	private void RefreshStages()
	{
		for (int i = 0; i < _stageNodes.Count; i++)
		{
			bool current = i == _shownStep;
			bool done = i < _shownStep;
			_stageNodes[i].AddThemeColorOverride(
				"font_color",
				current ? CareerCreationChrome.TextPrimary : done ? CareerCreationChrome.TextMuted : CareerCreationChrome.TextDim);
			_stageNodes[i].AddThemeFontSizeOverride("font_size", current ? 20 : 16);
		}

		float fraction = _shownStep < 0 ? 0.04f : (_shownStep + 1f) / _stageLabels.Length;
		if (_finished && _shownStep >= _stageLabels.Length - 1)
		{
			fraction = 1f;
		}

		_fill.SetAnchor(Side.Right, Mathf.Clamp(fraction, 0.04f, 1f));
		if (_shownStep >= 0 && _shownStep < _stageLabels.Length)
		{
			_status.Text = _stageLabels[_shownStep];
		}
	}

	private void StartGeneration()
	{
		CareerCreationState draft = CareerCreationSession.Current.State;
		string worldName = string.IsNullOrWhiteSpace(draft.WorldName) ? "New Career" : draft.WorldName.Trim();
		int seed = draft.WorldSeed ?? WorldSetup.ResolveSeed(null, draft.Coach.Id);
		draft.WorldSeed = seed;
		string slotId = Guid.NewGuid().ToString("N");
		string dbPath = Path.Combine(OS.GetUserDataDir(), "careers", slotId + ".db");

		_ = Task.Run(() =>
		{
			try
			{
				using SqliteCareerStore store = new(dbPath);
				using CareerService service = CareerService.Create(store);
				service.CreateCareer(
					new CreateCareerRequest
					{
						Name = worldName,
						Seed = seed,
						Coach = draft.Coach.Clone(),
					},
					progress => CallDeferred(nameof(OnProgress), progress.Label, progress.StepIndex, progress.StepCount));
				CallDeferred(nameof(OnGenerated), slotId, worldName);
			}
			catch (Exception ex)
			{
				CallDeferred(nameof(OnFailed), ex.Message);
			}
		});
	}

	private void OnProgress(string label, int stepIndex, int stepCount)
	{
		if (stepIndex > _reportedStep)
		{
			_reportedStep = stepIndex;
		}

		if (stepIndex >= 0 && stepIndex < _stageLabels.Length)
		{
			_stageLabels[stepIndex] = label;
			_stageNodes[stepIndex].Text = label;
		}

		_status.Text = label;
		_ = stepCount;
	}

	private void OnGenerated(string slotId, string worldName)
	{
		CoachProfile coach = CareerCreationSession.Current.State.Coach;
		CareerSaveSlot slot = GameSession.Current.CaptureCareerSlot();
		slot.Id = slotId;
		slot.CoachName = string.IsNullOrWhiteSpace(coach.FullName) ? worldName : coach.FullName;
		slot.Wins = 0;
		slot.Losses = 0;
		slot.SeasonNumber = 1;
		slot.SeasonYear = GameSession.SeasonYear;
		slot.WorldSeed = CareerCreationSession.Current.State.WorldSeed ?? 0;
		slot.CompetitionLevel = CompetitionLevel.HighSchool.ToLabel();
		slot.SchoolName = "Unsigned";
		slot.SchoolShortName = "";
		slot.OrganizationId = "";
		slot.Status = "Job Market";
		CareerLibrary.Upsert(slot);
		GameSession.Current.ApplyCareerSlot(slot);
		_slotReady = true;
		_finished = true;
		_status.Text = "World ready. Opening the job market...";
	}

	private void OnFailed(string message)
	{
		_failed = true;
		_status.Text = "World generation failed. Press Esc to go back.";
		_status.AddThemeColorOverride("font_color", new Color(0.90f, 0.40f, 0.36f));
		GD.PushError($"World generation failed: {message}");
	}

	private void EnterClubhouse()
	{
		if (_entered || !_slotReady)
		{
			return;
		}

		_entered = true;
		CareerCreationSession.Current.Clear();
		AudioManager.Current.StopAmbience();
		GetTree().ChangeSceneToFile(CareerCreationChrome.ClubhouseScene);
	}
}
