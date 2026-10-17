using Godot;
using HSBM.Domain;

/// <summary>
/// Athletic board card: numeric satisfaction, three season rungs, and
/// three program jobs. Home shows the score; this page is the source.
/// </summary>
public partial class GoalsPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color FairOrange = new(0.95f, 0.55f, 0.22f, 1f);
	private static readonly Color GoodLime = new(0.55f, 0.82f, 0.32f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _cascadeLogo = null!;

	private Label _subtitle = null!;
	private Label _scoreValue = null!;
	private Label _scoreCopy = null!;
	private VBoxContainer _seasonHost = null!;
	private VBoxContainer _programHost = null!;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_cascadeLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnSessionChanged;
		_session.CareerChanged += OnSessionChanged;
		_session.ContractStateChanged += OnSessionChanged;
		VisibilityChanged += OnVisibilityChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnSessionChanged;
			_session.CareerChanged -= OnSessionChanged;
			_session.ContractStateChanged -= OnSessionChanged;
		}

		VisibilityChanged -= OnVisibilityChanged;
	}

	private void OnSessionChanged() => Refresh();

	private void OnSessionChanged(TeamIdentity _) => Refresh();

	private void OnVisibilityChanged()
	{
		if (IsVisibleInTree())
		{
			Refresh();
		}
	}

	private void BuildPage()
	{
		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		AddChild(margin);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		margin.AddChild(layout);

		layout.AddChild(BuildHeader());
		layout.AddChild(BuildSatisfactionCard());

		var body = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 8);
		layout.AddChild(body);
		body.AddChild(BuildSeasonCard());
		body.AddChild(BuildProgramCard());
	}

	private Control BuildHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);

		header.AddChild(TeamLogos.MakeHeaderMark(_cascadeLogo));
		header.AddChild(TeamLogos.MakeHeaderRule(Hairline));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -4);
		identity.AddChild(MakeText($"ORGANIZATION  ·  {GameSession.SeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("BOARD & GOALS", _bold, 22, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("CASCADE REGIONAL HIGH SCHOOL  ·  THE BOARD IS WATCHING", _medium, 11, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);
		return header;
	}

	private Control BuildSatisfactionCard()
	{
		var card = MakeCard(false);
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 2);
		card.AddChild(layout);

		layout.AddChild(MakeText("BOARD SATISFACTION", _semibold, 11, Accent, HorizontalAlignment.Left));
		_scoreValue = MakeText("62 — AVERAGE", _bold, 42, Accent, HorizontalAlignment.Left);
		layout.AddChild(_scoreValue);
		layout.AddChild(MakeAccentLine());
		_scoreCopy = MakeText("", _medium, 12, TextMuted, HorizontalAlignment.Left);
		_scoreCopy.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(_scoreCopy);
		return card;
	}

	private Control BuildSeasonCard()
	{
		var card = MakeCard(true);
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		card.AddChild(layout);

		layout.AddChild(MakeText("SEASON GOALS", _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(MakeAccentLine());

		_seasonHost = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_seasonHost.AddThemeConstantOverride("separation", 8);
		layout.AddChild(_seasonHost);
		return card;
	}

	private Control BuildProgramCard()
	{
		var card = MakeCard(true);
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		card.AddChild(layout);

		layout.AddChild(MakeText("PROGRAM GOALS", _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(MakeAccentLine());

		_programHost = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_programHost.AddThemeConstantOverride("separation", 8);
		layout.AddChild(_programHost);
		return card;
	}

	private void Refresh()
	{
		if (_seasonHost == null)
		{
			return;
		}

		BoardCard board = BoardDesk.For(_session);
		_subtitle.Text = $"{_session.Organization.Name.ToUpperInvariant()}  ·  THE BOARD IS WATCHING";
		_scoreValue.Text = board.ScoreLine;
		_scoreValue.AddThemeColorOverride("font_color", SatisfactionColor(board.Satisfaction));
		_scoreCopy.Text = SatisfactionCopy(board.Satisfaction);

		foreach (Node child in _seasonHost.GetChildren())
		{
			child.QueueFree();
		}

		foreach (SeasonGoal goal in board.SeasonGoals)
		{
			_seasonHost.AddChild(BuildSeasonRow(goal));
		}

		foreach (Node child in _programHost.GetChildren())
		{
			child.QueueFree();
		}

		foreach (ProgramGoal goal in board.ProgramGoals)
		{
			_programHost.AddChild(BuildProgramRow(goal));
		}
	}

	private Control BuildSeasonRow(SeasonGoal goal)
	{
		var row = new PanelContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = goal.Status == GoalProgress.Met ? Green : Hairline,
			BorderWidthLeft = goal.Status == GoalProgress.Met ? 2 : 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 14,
			ContentMarginTop = 10,
			ContentMarginRight = 14,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		});

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 12);
		row.AddChild(columns);

		var copy = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		copy.AddThemeConstantOverride("separation", -2);
		copy.AddChild(MakeText(BoardGoals.TierLabel(goal.Tier).ToUpperInvariant(), _semibold, 11, Accent, HorizontalAlignment.Left));
		copy.AddChild(MakeText(goal.Title, _bold, 24, TextPrimary, HorizontalAlignment.Left));
		columns.AddChild(copy);

		Label mark = MakeText(StatusMark(goal.Status), _bold, 26, StatusColor(goal.Status), HorizontalAlignment.Right);
		mark.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		columns.AddChild(mark);
		return row;
	}

	private Control BuildProgramRow(ProgramGoal goal)
	{
		var row = new PanelContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = Hairline,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 14,
			ContentMarginTop = 12,
			ContentMarginRight = 14,
			ContentMarginBottom = 12,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		});

		var copy = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		copy.AddThemeConstantOverride("separation", 0);
		copy.AddChild(MakeText(goal.Title, _bold, 22, TextPrimary, HorizontalAlignment.Left));
		row.AddChild(copy);
		return row;
	}

	private static PanelContainer MakeCard(bool expand)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = expand ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.078431f, 0.094118f, 0.94f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 14,
			ContentMarginTop = 12,
			ContentMarginRight = 14,
			ContentMarginBottom = 12,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		});
		return card;
	}

	private static ColorRect MakeAccentLine()
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 2),
			Color = Accent,
		};
	}

	private static Label MakeText(string text, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = align,
		};
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static Color SatisfactionColor(BoardSatisfaction satisfaction) => satisfaction switch
	{
		BoardSatisfaction.Poor => Urgent,
		BoardSatisfaction.Fair => FairOrange,
		BoardSatisfaction.Good => GoodLime,
		BoardSatisfaction.Excellent => Green,
		_ => Accent,
	};

	private static string SatisfactionCopy(BoardSatisfaction satisfaction) => satisfaction switch
	{
		BoardSatisfaction.Poor => "The board is unhappy. Miss the required goal and the seat is in play.",
		BoardSatisfaction.Fair => "The board wants a turnaround. Hit the required goal to stabilize standing.",
		BoardSatisfaction.Good => "The board likes the direction. Keep hitting the target to lock this in.",
		BoardSatisfaction.Excellent => "The board is fully behind this staff.",
		_ => "The board is in the middle. The required goal holds the seat; the stretch is the dream.",
	};

	private static string StatusMark(GoalProgress status) => status switch
	{
		GoalProgress.Met => "✓",
		GoalProgress.Missed => "✕",
		_ => "",
	};

	private static Color StatusColor(GoalProgress status) => status switch
	{
		GoalProgress.Met => Green,
		GoalProgress.Missed => Urgent,
		_ => TextMuted,
	};
}
