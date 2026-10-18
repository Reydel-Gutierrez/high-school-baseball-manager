using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;
using HSBM.Simulation;

/// <summary>
/// Clubhouse → Training. The coach sets each team's practice focus and
/// intensity, and watches who is growing: overall at the start of the
/// baseball year, now, the gain, and how much ceiling is left.
/// </summary>
public partial class TrainingPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color TextDim = new(0.40f, 0.43f, 0.48f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardFill = new(0.070588f, 0.078431f, 0.094118f, 0.92f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.9f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color RowEven = new(0.08f, 0.088f, 0.105f, 1f);
	private static readonly Color RowOdd = new(0.065f, 0.072f, 0.088f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Label _subtitle = null!;
	private Label _summary = null!;
	private VBoxContainer _focusHost = null!;
	private VBoxContainer _intensityHost = null!;
	private Label _focusBlurb = null!;
	private Label _intensityBlurb = null!;
	private Label _nextPractice = null!;
	private VBoxContainer _rows = null!;
	private bool _built;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_session = GetNode<GameSession>("/root/GameSession");
		Build();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		_session.CareerChanged += Refresh;
		_session.TrainingChanged += Refresh;
		VisibilityChanged += OnVisibilityChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
			_session.CareerChanged -= Refresh;
			_session.TrainingChanged -= Refresh;
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => Refresh();

	private void OnVisibilityChanged()
	{
		if (Visible)
		{
			Refresh();
		}
	}

	private void Build()
	{
		_built = true;
		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		AddChild(margin);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 10);
		margin.AddChild(layout);
		layout.AddChild(BuildHeader());

		var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 10);
		layout.AddChild(body);
		body.AddChild(BuildPlanCard());
		body.AddChild(BuildDevelopmentCard());
	}

	private Control BuildHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);
		header.AddChild(UserProgram.MakeHeaderMark());
		header.AddChild(TeamLogos.MakeHeaderRule(Hairline));
		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -2);
		identity.AddChild(MakeText($"CLUBHOUSE  ·  {GameSession.SeasonYear} SEASON", _medium, 10, Accent));
		identity.AddChild(MakeText("TRAINING", _bold, 26, TextPrimary));
		_subtitle = MakeText("", _medium, 12, new Color(0.72f, 0.76f, 0.82f));
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var summary = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
		summary.AddChild(MakeText("SEASON GROWTH", _medium, 10, Accent, HorizontalAlignment.Right));
		_summary = MakeText("", _bold, 26, TextPrimary, HorizontalAlignment.Right);
		summary.AddChild(_summary);
		header.AddChild(summary);
		return header;
	}

	private Control BuildPlanCard()
	{
		PanelContainer card = MakeCard();
		card.CustomMinimumSize = new Vector2(360, 0);
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		card.AddChild(box);

		box.AddChild(MakeText("PRACTICE FOCUS", _bold, 13, Accent));
		_focusHost = new VBoxContainer();
		_focusHost.AddThemeConstantOverride("separation", 4);
		box.AddChild(_focusHost);
		_focusBlurb = MakeText("", _medium, 13, TextMuted, wrap: true);
		box.AddChild(_focusBlurb);

		box.AddChild(MakeHairline());
		box.AddChild(MakeText("INTENSITY", _bold, 13, Accent));
		_intensityHost = new VBoxContainer();
		_intensityHost.AddThemeConstantOverride("separation", 4);
		box.AddChild(_intensityHost);
		_intensityBlurb = MakeText("", _medium, 13, TextMuted, wrap: true);
		box.AddChild(_intensityBlurb);

		box.AddChild(MakeHairline());
		_nextPractice = MakeText("", _semibold, 13, TextPrimary, wrap: true);
		box.AddChild(_nextPractice);
		box.AddChild(MakeText(
			"Players close part of the gap to their potential every year. Coachable kids, playing time, and good facilities speed it up; about 60% of a year's growth shows up over the winter.",
			_medium,
			12,
			TextDim,
			wrap: true));
		return card;
	}

	private Control BuildDevelopmentCard()
	{
		PanelContainer card = MakeCard();
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		card.AddChild(box);
		box.AddChild(MakeText("PLAYER DEVELOPMENT", _bold, 13, Accent));
		box.AddChild(BuildRow(null, 0, header: true));
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_rows.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(_rows);
		box.AddChild(scroll);
		return card;
	}

	public void Refresh()
	{
		if (!_built || _session.ActiveTeam == null)
		{
			return;
		}

		TeamLevel level = _session.ActiveTeam.Level;
		TeamTraining plan = _session.TrainingFor(level);
		_subtitle.Text = $"{UserProgram.NameUpper}  ·  {_session.ActiveTeam.LevelLabel.ToUpperInvariant()}";

		Clear(_focusHost);
		foreach (TrainingFocus focus in Enum.GetValues<TrainingFocus>())
		{
			TrainingFocus pick = focus;
			_focusHost.AddChild(MakeOption(focus.ToLabel().ToUpperInvariant(), focus == plan.Focus, () => Apply(level, pick, plan.Intensity)));
		}

		_focusBlurb.Text = plan.Focus.ToBlurb();

		Clear(_intensityHost);
		var intensityRow = new HBoxContainer();
		intensityRow.AddThemeConstantOverride("separation", 4);
		foreach (TrainingIntensity intensity in Enum.GetValues<TrainingIntensity>())
		{
			TrainingIntensity pick = intensity;
			Button option = MakeOption(intensity.ToLabel().ToUpperInvariant(), intensity == plan.Intensity, () => Apply(level, plan.Focus, pick));
			option.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			intensityRow.AddChild(option);
		}

		_intensityHost.AddChild(intensityRow);
		_intensityBlurb.Text = plan.Intensity.ToBlurb();
		DateOnly next = _session.NextPracticeDay();
		bool inSeason = DevelopmentDesk.IsTrainingDay(GameSession.SeasonYear, next);
		_nextPractice.Text = inSeason
			? $"Next practice week: {next.ToString("dddd, MMMM d", CultureInfo.InvariantCulture)}"
			: "Practice weeks run Mondays from preseason through the playoffs.";

		List<Player> roster = _session.RosterFor(_session.ActiveTeam.Id);
		roster.Sort((a, b) =>
		{
			int gain = b.SeasonGain.CompareTo(a.SeasonGain);
			return gain != 0 ? gain : (b.Potential - b.Overall).CompareTo(a.Potential - a.Overall);
		});

		Clear(_rows);
		double total = 0;
		for (int i = 0; i < roster.Count; i++)
		{
			total += roster[i].SeasonGain;
			_rows.AddChild(BuildRow(roster[i], i, header: false));
		}

		_summary.Text = total >= 0.05 ? $"+{total:0.0} OVR" : total <= -0.05 ? $"{total:0.0} OVR" : "—";
		_summary.AddThemeColorOverride("font_color", total >= 0.05 ? Green : TextPrimary);
	}

	private void Apply(TeamLevel level, TrainingFocus focus, TrainingIntensity intensity)
	{
		if (_session.SetTraining(level, focus, intensity))
		{
			UiSounds.Play(UiSound.Confirm);
		}
		else
		{
			UiSounds.Play(UiSound.Error);
		}
	}

	private Control BuildRow(Player? player, int index, bool header)
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = header ? CardInner : (index % 2 == 0 ? RowEven : RowOdd),
			ContentMarginLeft = 10,
			ContentMarginRight = 10,
			ContentMarginTop = 6,
			ContentMarginBottom = 6,
		});
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		panel.AddChild(row);

		if (header)
		{
			Cell(row, "PLAYER", 0, TextDim, _semibold, 11, expand: true);
			Cell(row, "POS", 44, TextDim, _semibold, 11);
			Cell(row, "YR", 36, TextDim, _semibold, 11);
			Cell(row, "START", 50, TextDim, _semibold, 11);
			Cell(row, "OVR", 44, TextDim, _semibold, 11);
			Cell(row, "GAIN", 50, TextDim, _semibold, 11);
			Cell(row, "POT", 44, TextDim, _semibold, 11);
			Cell(row, "OUTLOOK", 96, TextDim, _semibold, 11);
			Cell(row, "TYPE", 96, TextDim, _semibold, 11);
			return panel;
		}

		Player p = player!;
		int start = p.SeasonStartRating > 0 ? (int)Math.Round(p.SeasonStartRating / 100.0) : p.Overall;
		double gain = p.SeasonGain;
		Cell(row, p.FullName, 0, TextPrimary, _semibold, 14, expand: true);
		Cell(row, p.PrimaryPosition.ToCode(), 44, TextMuted, _semibold, 13);
		Cell(row, p.Grade.ToAbbrev(), 36, TextMuted, _semibold, 13);
		Cell(row, start.ToString(CultureInfo.InvariantCulture), 50, TextMuted, _semibold, 13);
		Cell(row, p.Overall.ToString(CultureInfo.InvariantCulture), 44, TextPrimary, _bold, 14);
		bool up = gain >= 0.05;
		bool down = gain <= -0.05;
		Cell(row, up ? $"▲ {gain:0.0}" : down ? $"▼ {-gain:0.0}" : "—", 50, up ? Green : down ? Urgent : TextDim, _bold, 13);
		Cell(row, p.Potential.ToString(CultureInfo.InvariantCulture), 44, Accent, _bold, 13);
		string outlook = PlayerDevelopment.Outlook(p);
		Color outlookColor = outlook switch
		{
			"High ceiling" => Green,
			"Rising" => Green,
			"Peaked" => TextDim,
			_ => TextPrimary,
		};
		Cell(row, outlook, 96, outlookColor, _semibold, 13);
		Cell(row, p.Personality.Label(), 96, TextMuted, _medium, 13);
		return panel;
	}

	private void Cell(HBoxContainer row, string text, float width, Color color, FontFile font, int size, bool expand = false)
	{
		Label label = MakeText(text, font, size, color);
		label.CustomMinimumSize = new Vector2(width, 0);
		label.ClipText = true;
		if (expand)
		{
			label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		}

		row.AddChild(label);
	}

	private Button MakeOption(string text, bool selected, Action onPressed)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(0, 34),
			Alignment = HorizontalAlignment.Left,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 14);
		Color fill = selected ? Accent : CardInner;
		Color border = selected ? Accent : CardBorder;
		Color font = selected ? TextOnAccent : TextPrimary;
		button.AddThemeColorOverride("font_color", font);
		button.AddThemeColorOverride("font_hover_color", font);
		button.AddThemeColorOverride("font_pressed_color", font);
		button.AddThemeColorOverride("font_focus_color", font);
		button.AddThemeStyleboxOverride("normal", Box(fill, border));
		button.AddThemeStyleboxOverride("hover", Box(selected ? Accent : HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", Box(selected ? Accent : HoverBg, Accent));
		button.AddThemeStyleboxOverride("focus", Box(fill, Accent));
		button.Pressed += onPressed;
		return button;
	}

	private static StyleBoxFlat Box(Color fill, Color border) => new()
	{
		BgColor = fill,
		BorderColor = border,
		BorderWidthLeft = 1,
		BorderWidthTop = 1,
		BorderWidthRight = 1,
		BorderWidthBottom = 1,
		ContentMarginLeft = 12,
		ContentMarginRight = 12,
		ContentMarginTop = 6,
		ContentMarginBottom = 6,
		CornerRadiusTopLeft = 3,
		CornerRadiusTopRight = 3,
		CornerRadiusBottomRight = 3,
		CornerRadiusBottomLeft = 3,
	};

	private static PanelContainer MakeCard()
	{
		var card = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardFill,
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 14,
			ContentMarginRight = 14,
			ContentMarginTop = 12,
			ContentMarginBottom = 12,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
		});
		return card;
	}

	private static Label MakeText(string text, FontFile font, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left, bool wrap = false)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = align,
			MouseFilter = MouseFilterEnum.Ignore,
			AutowrapMode = wrap ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off,
		};
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static ColorRect MakeHairline() => new()
	{
		CustomMinimumSize = new Vector2(0, 1),
		Color = Hairline,
		MouseFilter = MouseFilterEnum.Ignore,
	};

	private static void Clear(Node host)
	{
		foreach (Node child in host.GetChildren())
		{
			host.RemoveChild(child);
			child.QueueFree();
		}
	}
}
