using System;
using System.Collections.Generic;
using Godot;
using HSBM.Application;
using HSBM.Domain;

/// <summary>
/// The interview room. Three questions from the athletic director; the
/// coach's answers are scored against what the program needs, then the
/// school calls back with an offer or a polite no.
/// </summary>
public partial class InterviewOverlay : CanvasLayer
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.62f, 0.65f, 0.70f, 1f);
	private static readonly Color Accent = new(0.772549f, 0.635294f, 0.325490f, 1f);
	private static readonly Color AccentHover = new(0.850980f, 0.729412f, 0.443137f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardFill = new(0.070588f, 0.078431f, 0.094118f, 0.98f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 1f);
	private static readonly Color CardBorder = new(0.137255f, 0.152941f, 0.200000f, 1f);

	private GameSession _session = null!;
	private JobListing _job = null!;
	private ProgramNeeds _needs = null!;
	private readonly List<CoachingPitch> _pitches = new();
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private VBoxContainer _body = null!;

	/// <summary>Raised when the coach accepts the offer. The board signs him.</summary>
	public event Action<string>? AcceptedOffer;

	public void Begin(GameSession session, JobListing job)
	{
		Layer = 60;
		_session = session;
		_job = job;
		_needs = session.NeedsFor(job);
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");

		var root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
		root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		var dim = new ColorRect { Color = new Color(0.01f, 0.012f, 0.02f, 0.82f) };
		dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(dim);

		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(center);

		var card = new PanelContainer { CustomMinimumSize = new Vector2(760, 0) };
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardFill,
			BorderColor = Accent,
			BorderWidthTop = 2,
			BorderWidthLeft = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 28,
			ContentMarginRight = 28,
			ContentMarginTop = 22,
			ContentMarginBottom = 22,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ShadowColor = new Color(0, 0, 0, 0.5f),
			ShadowSize = 18,
		});
		center.AddChild(card);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 12);
		card.AddChild(layout);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);
		header.AddChild(new ProgramMark(64, job.OrganizationId));
		var titles = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		titles.AddThemeConstantOverride("separation", 0);
		titles.AddChild(Text("HEAD COACH INTERVIEW", _semibold, 12, Accent));
		titles.AddChild(Text(job.SchoolFullName.ToUpperInvariant(), _bold, 28, TextPrimary));
		titles.AddChild(Text($"{job.DistrictName} District  ·  Reputation {job.Reputation}  ·  Board wants: {job.TargetExpectation.ToLabel()}", _medium, 13, TextMuted));
		header.AddChild(titles);
		layout.AddChild(header);

		var situation = new PanelContainer();
		situation.AddThemeStyleboxOverride("panel", Inner());
		situation.AddChild(Text($"THE SITUATION — {_needs.Summary}", _semibold, 14, TextPrimary, wrap: true));
		layout.AddChild(situation);

		_body = new VBoxContainer();
		_body.AddThemeConstantOverride("separation", 8);
		layout.AddChild(_body);
		ShowQuestion(0);
	}

	private void ShowQuestion(int index)
	{
		Clear(_body);
		if (index >= HiringDesk.Questions.Count)
		{
			ShowDecision();
			return;
		}

		InterviewQuestion question = HiringDesk.Questions[index];
		_body.AddChild(Text($"QUESTION {index + 1} OF {HiringDesk.Questions.Count}", _semibold, 11, TextMuted));
		_body.AddChild(Text(question.Prompt, _bold, 22, TextPrimary, wrap: true));
		foreach (InterviewAnswer answer in question.Answers)
		{
			InterviewAnswer pick = answer;
			_body.AddChild(Choice(answer.Text, () =>
			{
				UiSounds.Play(UiSound.Select);
				_pitches.Add(pick.Pitch);
				ShowQuestion(index + 1);
			}));
		}

		_body.AddChild(Action("WITHDRAW", primary: false, () => Close()));
	}

	private void ShowDecision()
	{
		HiringDecision decision = _session.Interview(_job, _pitches);
		UiSounds.Play(decision.Hired ? UiSound.Confirm : UiSound.Error);
		_body.AddChild(Text(decision.Hired ? "OFFER ON THE TABLE" : "DECISION", _semibold, 12, decision.Hired ? Green : Urgent));
		_body.AddChild(Text(decision.Headline, _bold, 28, TextPrimary, wrap: true));
		_body.AddChild(Text(decision.Detail, _medium, 15, TextMuted, wrap: true));
		if (decision.Hired)
		{
			_body.AddChild(Text($"Contract: {_job.ContractTermLabel}  ·  {_job.CoachSalaryLabel} per year  ·  Board target: {_job.TargetExpectation.ToLabel()}", _semibold, 15, Accent, wrap: true));
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);
			Button sign = Action("SIGN THE CONTRACT", primary: true, () =>
			{
				string org = _job.OrganizationId;
				Close();
				AcceptedOffer?.Invoke(org);
			});
			sign.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			row.AddChild(sign);
			row.AddChild(Action("TURN IT DOWN", primary: false, () => Close()));
			_body.AddChild(row);
		}
		else
		{
			_body.AddChild(Text("That door is closed until next year's carousel. Win somewhere, and the phone rings differently.", _medium, 14, TextMuted, wrap: true));
			_body.AddChild(Action("BACK TO THE JOB MARKET", primary: true, () => Close()));
		}
	}

	private void Close()
	{
		QueueFree();
	}

	private Button Choice(string text, Action onPressed)
	{
		var button = new Button
		{
			Text = text,
			Alignment = HorizontalAlignment.Left,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0, 42),
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 16);
		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeColorOverride("font_hover_color", TextPrimary);
		button.AddThemeStyleboxOverride("normal", Outlined(CardInner, CardBorder));
		button.AddThemeStyleboxOverride("hover", Outlined(HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", Outlined(HoverBg, Accent));
		button.AddThemeStyleboxOverride("focus", Outlined(HoverBg, Accent));
		button.Pressed += onPressed;
		return button;
	}

	private Button Action(string text, bool primary, Action onPressed)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 40) };
		button.AddThemeFontOverride("font", _bold);
		button.AddThemeFontSizeOverride("font_size", 15);
		Color font = primary ? TextOnAccent : TextPrimary;
		button.AddThemeColorOverride("font_color", font);
		button.AddThemeColorOverride("font_hover_color", font);
		button.AddThemeColorOverride("font_pressed_color", font);
		button.AddThemeStyleboxOverride("normal", primary ? Filled(Accent) : Outlined(CardInner, CardBorder));
		button.AddThemeStyleboxOverride("hover", primary ? Filled(AccentHover) : Outlined(HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", primary ? Filled(AccentHover) : Outlined(HoverBg, Accent));
		button.AddThemeStyleboxOverride("focus", primary ? Filled(AccentHover) : Outlined(HoverBg, Accent));
		button.Pressed += onPressed;
		return button;
	}

	private static StyleBoxFlat Inner() => new()
	{
		BgColor = CardInner,
		BorderColor = CardBorder,
		BorderWidthLeft = 3,
		ContentMarginLeft = 14,
		ContentMarginRight = 14,
		ContentMarginTop = 10,
		ContentMarginBottom = 10,
	};

	private static StyleBoxFlat Filled(Color fill) => new()
	{
		BgColor = fill,
		ContentMarginLeft = 16,
		ContentMarginRight = 16,
		ContentMarginTop = 8,
		ContentMarginBottom = 8,
		CornerRadiusTopLeft = 8,
		CornerRadiusTopRight = 8,
		CornerRadiusBottomLeft = 8,
		CornerRadiusBottomRight = 8,
	};

	private static StyleBoxFlat Outlined(Color fill, Color border)
	{
		StyleBoxFlat box = Filled(fill);
		box.BorderColor = border;
		box.BorderWidthLeft = 1;
		box.BorderWidthTop = 1;
		box.BorderWidthRight = 1;
		box.BorderWidthBottom = 1;
		return box;
	}

	private static Label Text(string text, FontFile font, int size, Color color, bool wrap = false)
	{
		var label = new Label
		{
			Text = text,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
		};
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static void Clear(Node host)
	{
		foreach (Node child in host.GetChildren())
		{
			host.RemoveChild(child);
			child.QueueFree();
		}
	}
}
