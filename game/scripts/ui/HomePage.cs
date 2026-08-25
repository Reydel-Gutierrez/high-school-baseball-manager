using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// Coach's briefing: who the program is, what this week looks like, and
/// the next contest to prepare for.
/// </summary>
public partial class HomePage : Control
{
	private const int CurrentSeasonYear = 2026;
	private const int TickerVisibleLines = 2;
	private const float TickerLineHeight = 22f;
	private const float TickerDwellSeconds = 2.6f;
	private const float TickerSlideSeconds = 0.55f;

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color LakesBlue = new(0.45f, 0.72f, 0.88f, 1f);
	private static readonly Color Important = new(0.45f, 0.72f, 0.88f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private ShaderMaterial _circleMaterial = null!;
	private Texture2D _cascadeLogo = null!;
	private Texture2D _lakesLogo = null!;
	private Texture2D _marini = null!;
	private Texture2D _tony = null!;
	private Texture2D _iconOff = null!;
	private Texture2D _iconPractice = null!;
	private Texture2D _iconStar = null!;
	private Texture2D _iconCalendar = null!;
	private Texture2D _iconPin = null!;
	private Texture2D _iconClock = null!;

	private Label _title = null!;
	private Label _subtitle = null!;
	private Label _recordValue = null!;
	private Label _districtValue = null!;
	private Label _boardValue = null!;
	private VBoxContainer _contestHost = null!;
	private Control _modalLayer = null!;
	private VBoxContainer _modalBody = null!;
	private Label _modalTitle = null!;
	private BoxScoreOverlay _boxScoreOverlay = null!;
	private JobOfferOverlay _jobOfferOverlay = null!;
	private SimulationStoppedOverlay _simulationOverlay = null!;
	private Control _jobOfferBanner = null!;
	private Control _newsTickerSlot = null!;
	private Control _newsTicker = null!;
	private Label _newsTickerKicker = null!;
	private Control _newsTickerViewport = null!;
	private Control _newsTickerTrack = null!;
	private readonly List<TickerItem> _tickerItems = new();
	private readonly List<Control> _tickerRows = new();
	private readonly List<Label> _tickerBullets = new();
	private readonly List<Label> _tickerHeadlines = new();
	private int _tickerIndex;
	private float _tickerClock;
	private bool _tickerSliding;
	private Control _boxScoreLayer = null!;
	private VBoxContainer _boxScoreBody = null!;
	private Label _boxScoreKicker = null!;
	private Label _boxScoreTitle = null!;
	private DateOnly _selectedDate;
	private readonly List<WeekBind> _weekBinds = new();
	private Button _advanceButton = null!;
	private string? _inspectedSchoolId;
	private string? _inspectedPlayerTeamId;
	private string? _inspectedPlayerName;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_circleMaterial = new ShaderMaterial
		{
			Shader = GD.Load<Shader>("res://assets/ui/circle_crop.gdshader"),
		};
		_cascadeLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
		_lakesLogo = TeamLogos.Load(TeamLogos.GreatLakesId)!;
		_marini = GD.Load<Texture2D>("res://assets/teams/01_Cascade_Regional_High_School/Players/Cascade_player_01_Marini.png");
		_tony = GD.Load<Texture2D>("res://assets/teams/02_Great_Lakes_High_School/Players/Great_lakes_player_tony.png");
		_iconOff = UiSvg.Load("res://assets/ui/icon_off_day.svg");
		_iconPractice = UiSvg.Load("res://assets/ui/icon_practice.svg");
		_iconStar = UiSvg.Load("res://assets/ui/icon_schedule_star.svg");
		_iconCalendar = UiSvg.Load("res://assets/ui/icon_calendar.svg");
		_iconPin = UiSvg.Load("res://assets/ui/icon_pin.svg");
		_iconClock = UiSvg.Load("res://assets/ui/icon_clock.svg");

		_session = GetNode<GameSession>("/root/GameSession");
		_selectedDate = _session.CurrentDate;
		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		_session.CareerChanged += ApplyActiveTeam;
		VisibilityChanged += OnVisibilityChanged;
		ApplyActiveTeam();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
			_session.CareerChanged -= ApplyActiveTeam;
		}

		VisibilityChanged -= OnVisibilityChanged;
		if (_jobOfferOverlay != null)
		{
			_jobOfferOverlay.Dismissed -= OnJobOfferDismissed;
			_jobOfferOverlay.Resolved -= OnJobOfferResolved;
		}

		if (_simulationOverlay != null)
		{
			_simulationOverlay.GoToRequested -= OpenInterrupt;
		}

		StopNewsTicker();
	}

	public override void _Process(double delta)
	{
		TickNewsTicker((float)delta);
	}

	private void OnVisibilityChanged()
	{
		if (IsVisibleInTree())
		{
			RefreshNewsBanner();
			CallDeferred(nameof(PresentPendingCelebration));
		}
		else
		{
			StopNewsTicker();
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => ApplyActiveTeam();

	public void ShowSchool(string teamId)
	{
		_inspectedSchoolId = SchoolProfiles.NormalizeId(teamId);
		_inspectedPlayerTeamId = null;
		_inspectedPlayerName = null;
		BindContest(_session.ActiveTeam.Level == TeamLevel.Varsity);
	}

	public void ShowPlayer(string teamId, string playerName)
	{
		_inspectedSchoolId = null;
		_inspectedPlayerTeamId = string.IsNullOrWhiteSpace(teamId) ? string.Empty : SchoolProfiles.NormalizeId(teamId);
		_inspectedPlayerName = playerName;
		BindContest(_session.ActiveTeam.Level == TeamLevel.Varsity);
	}

	private void CloseInspectedProfile()
	{
		_inspectedSchoolId = null;
		_inspectedPlayerTeamId = null;
		_inspectedPlayerName = null;
		BindContest(_session.ActiveTeam.Level == TeamLevel.Varsity);
	}

	private void ApplyActiveTeam()
	{
		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		_title.Text = _session.Organization.Name.ToUpperInvariant();
		_subtitle.Text = varsity
			? $"VARSITY  ·  ESTABLISHED PROGRAM  ·  GREAT LAKES DISTRICT  ·  {_session.CurrentDate.ToString("dddd, MMMM d", CultureInfo.InvariantCulture).ToUpperInvariant()}"
			: $"JV  ·  ESTABLISHED PROGRAM  ·  GREAT LAKES DISTRICT  ·  {_session.CurrentDate.ToString("dddd, MMMM d", CultureInfo.InvariantCulture).ToUpperInvariant()}";
		_recordValue.Text = varsity ? _session.RecordLabel : "12–8";
		_districtValue.Text = varsity ? "1ST" : "4TH";
		_districtValue.AddThemeColorOverride("font_color", varsity ? Green : Accent);
		_boardValue.Text = _session.BoardSatisfactionScore.ToString(CultureInfo.InvariantCulture);
		_boardValue.AddThemeColorOverride("font_color", BoardSatisfactionColor(_session.BoardSatisfaction));
		RefreshWeekCards();
		RefreshAdvanceButton();
		BindContest(varsity);
		RefreshJobOfferBanner();
		RefreshNewsBanner();
		CallDeferred(nameof(PresentPendingCelebration));
	}

	private void BuildPage()
	{
		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		AddChild(margin);
		AddChild(BuildModalLayer());
		_boxScoreOverlay = new BoxScoreOverlay();
		AddChild(_boxScoreOverlay);
		_jobOfferOverlay = new JobOfferOverlay();
		_jobOfferOverlay.Dismissed += OnJobOfferDismissed;
		_jobOfferOverlay.Resolved += OnJobOfferResolved;
		AddChild(_jobOfferOverlay);
		_simulationOverlay = new SimulationStoppedOverlay();
		_simulationOverlay.GoToRequested += OpenInterrupt;
		AddChild(_simulationOverlay);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		margin.AddChild(layout);

		layout.AddChild(BuildHeader());
		_jobOfferBanner = new Control { Visible = false };

		var body = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 8);
		layout.AddChild(body);

		var week = BuildWeekCard();
		week.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		week.SizeFlagsVertical = SizeFlags.ExpandFill;
		week.SizeFlagsStretchRatio = 1.7f;
		body.AddChild(week);

		_contestHost = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(340, 0),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1f,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_contestHost.AddThemeConstantOverride("separation", 0);
		body.AddChild(_contestHost);
	}

	private Control BuildHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);

		header.AddChild(TeamLogos.MakeHeaderMark(_cascadeLogo));
		header.AddChild(TeamLogos.MakeHeaderRule(Hairline));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.Fill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -2);

		identity.AddChild(MakeText($"HOME  ·  {CurrentSeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		_title = MakeText("CASCADE REGIONAL HIGH SCHOOL", _bold, 26, TextPrimary, HorizontalAlignment.Left);
		identity.AddChild(_title);
		_subtitle = MakeText("VARSITY  ·  GREAT LAKES DISTRICT", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		_newsTickerSlot = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		_newsTickerSlot.AddThemeConstantOverride("separation", 14);
		_newsTickerSlot.AddChild(TeamLogos.MakeHeaderRule(Hairline));
		_newsTicker = BuildNewsTicker();
		_newsTickerSlot.AddChild(_newsTicker);
		_newsTickerSlot.AddChild(TeamLogos.MakeHeaderRule(Hairline));
		header.AddChild(_newsTickerSlot);

		var metrics = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		metrics.AddThemeConstantOverride("separation", 4);
		metrics.AddChild(MakeText("PROGRAM PULSE", _medium, 10, Accent, HorizontalAlignment.Right));

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		row.Alignment = BoxContainer.AlignmentMode.End;

		_recordValue = AddMetric(row, "18-4", "RECORD", TextPrimary);
		_districtValue = AddMetric(row, "1ST", "DISTRICT", Green);
		_boardValue = AddMetric(row, "62", "BOARD", Accent);
		metrics.AddChild(row);
		header.AddChild(metrics);

		return header;
	}

	private Label AddMetric(HBoxContainer row, string value, string label, Color valueColor)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", -4);
		col.Alignment = BoxContainer.AlignmentMode.Center;
		var amount = MakeText(value, _bold, 28, valueColor, HorizontalAlignment.Right);
		col.AddChild(amount);
		col.AddChild(MakeText(label, _semibold, 11, TextMuted, HorizontalAlignment.Right));
		row.AddChild(col);
		return amount;
	}

	private Control BuildWeekCard()
	{
		var card = MakeCard();
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		card.AddChild(layout);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);

		var titles = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			SizeFlagsStretchRatio = 1f,
		};
		titles.AddThemeConstantOverride("separation", -2);
		DateOnly sunday = WeekStart(_session.CurrentDate);
		DateOnly saturday = sunday.AddDays(6);
		titles.AddChild(MakeText("THIS WEEK", _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		titles.AddChild(MakeText(
			$"{sunday.ToString("MMMM d", CultureInfo.InvariantCulture)}  –  {saturday.ToString("MMMM d", CultureInfo.InvariantCulture)}".ToUpperInvariant(),
			_medium,
			11,
			TextMuted,
			HorizontalAlignment.Left));
		header.AddChild(titles);

		(string name, string when, Color color) milestone = NextMilestone();
		var next = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			SizeFlagsStretchRatio = 1f,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		next.AddThemeConstantOverride("separation", -2);
		next.AddChild(MakeText("NEXT MILESTONE", _medium, 10, Accent, HorizontalAlignment.Center));
		next.AddChild(MakeText($"{milestone.name}  ·  {milestone.when}", _semibold, 12, milestone.color, HorizontalAlignment.Center));
		header.AddChild(next);

		var actions = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			SizeFlagsStretchRatio = 1f,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		actions.AddThemeConstantOverride("separation", 6);
		actions.AddChild(MakeMonthlyCalendarButton());
		actions.AddChild(MakeSeasonScheduleButton());
		header.AddChild(actions);
		layout.AddChild(header);
		layout.AddChild(MakeAccentLine());

		var gridWrap = new PanelContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		gridWrap.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.045f, 0.055f, 0.55f),
			BorderColor = Hairline,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 4,
			ContentMarginTop = 4,
			ContentMarginRight = 4,
			ContentMarginBottom = 4,
		});
		layout.AddChild(gridWrap);

		var days = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		days.AddThemeConstantOverride("separation", 4);
		gridWrap.AddChild(days);

		_weekBinds.Clear();
		for (int i = 0; i < 7; i++)
		{
			days.AddChild(BuildDayColumn(sunday.AddDays(i)));
		}

		layout.AddChild(BuildActions());
		return card;
	}

	private Control BuildDayColumn(DateOnly date)
	{
		IReadOnlyList<SeasonEvent> events = SeasonCalendar.On(GameSession.SeasonYear, date);
		IReadOnlyList<CalendarContest> games = _session.ContestsOn(date);
		CalendarContest? game = PrimaryGame(games);
		bool today = date == _session.CurrentDate;

		var column = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		column.AddThemeConstantOverride("separation", 4);

		var dayHeader = new HBoxContainer();
		dayHeader.Alignment = BoxContainer.AlignmentMode.Center;
		dayHeader.AddThemeConstantOverride("separation", 4);
		dayHeader.AddChild(MakeText(
			date.ToString("ddd d", CultureInfo.InvariantCulture).ToUpperInvariant(),
			_semibold,
			11,
			today ? Accent : new Color(0.78f, 0.82f, 0.88f),
			HorizontalAlignment.Center));
		if (events.Count > 0)
		{
			SeasonEvent first = FeaturedOrFirst(events);
			dayHeader.AddChild(MakeIcon(first.Featured ? _iconStar : _iconCalendar, 11, Important));
		}

		column.AddChild(dayHeader);

		var card = new PanelContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			ClipContents = true,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		_weekBinds.Add(new WeekBind(card, date, game?.Level));
		ApplyWeekDayStyle(card, date);
		DateOnly captured = date;
		card.GuiInput += ev => OnWeekDayInput(ev, captured, events, game);
		card.MouseEntered += () => ApplyWeekDayStyle(card, captured, hovered: true);
		card.MouseExited += () => ApplyWeekDayStyle(card, captured);

		var shell = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		shell.AddThemeConstantOverride("separation", 6);
		card.AddChild(shell);

		var content = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		content.AddThemeConstantOverride("separation", 2);
		shell.AddChild(content);

		if (game != null)
		{
			BindGameDay(content, game);
		}
		else if (events.Count > 0)
		{
			SeasonEvent first = FeaturedOrFirst(events);
			content.AddChild(MakeIcon(first.Featured ? _iconStar : _iconCalendar, 26, Important));
			var title = MakeText(first.Name.ToUpperInvariant(), _semibold, 12, TextPrimary, HorizontalAlignment.Center);
			title.AutowrapMode = TextServer.AutowrapMode.Word;
			content.AddChild(title);
			content.AddChild(MakeText(first.FormatDates().ToUpperInvariant(), _medium, 10, Important, HorizontalAlignment.Center));
			content.AddChild(MakeText(date.DayOfWeek == DayOfWeek.Sunday ? "OFF DAY" : "PRACTICE", _medium, 10, TextMuted, HorizontalAlignment.Center));
			if (date.DayOfWeek is not DayOfWeek.Sunday)
			{
				content.AddChild(MakeText("3:30 PM", _medium, 10, TextMuted, HorizontalAlignment.Center));
			}
		}
		else
		{
			bool off = date.DayOfWeek == DayOfWeek.Sunday;
			content.AddChild(new TextureRect
			{
				Texture = off ? _iconOff : _iconPractice,
				CustomMinimumSize = new Vector2(28, 28),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
				MouseFilter = MouseFilterEnum.Ignore,
			});
			content.AddChild(MakeText(off ? "OFF DAY" : "PRACTICE", _semibold, 12, TextPrimary, HorizontalAlignment.Center));
			if (!off)
			{
				content.AddChild(MakeText("3:30 PM", _medium, 10, TextMuted, HorizontalAlignment.Center));
			}
		}

		if (game != null && events.Count > 0)
		{
			shell.AddChild(BuildImportantChip(date, events));
		}
		else if (events.Count > 0 && game == null)
		{
			content.AddChild(MakeText("TAP FOR DETAILS", _medium, 9, Important, HorizontalAlignment.Center));
		}

		column.AddChild(card);
		return column;
	}

	private void BindGameDay(VBoxContainer content, CalendarContest game)
	{
		HubTeam opponent = DistrictHubData.GetTeam(game.OpponentId);

		var identity = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", 2);
		identity.AddChild(MakeTeamMark(game.OpponentId, 40, game.Level == TeamLevel.JuniorVarsity ? TextMuted : Accent));

		var gameTitle = MakeText((game.Home ? "VS " : "@ ") + opponent.ShortName, _semibold, 11, TextPrimary, HorizontalAlignment.Center);
		gameTitle.AutowrapMode = TextServer.AutowrapMode.Word;
		identity.AddChild(gameTitle);
		if (game.Completed)
		{
			identity.AddChild(MakeResultMark(game));
		}

		content.AddChild(identity);
		content.AddChild(MakeText(game.Time, _medium, 10, TextMuted, HorizontalAlignment.Center));
		content.AddChild(MakeText(game.VenueLabel, _semibold, 10, game.Home ? Green : Accent, HorizontalAlignment.Center));
		content.AddChild(MakeText(game.Exhibition ? "EXHIBITION" : game.Level.ToLabel().ToUpperInvariant(), _medium, 9, TextMuted, HorizontalAlignment.Center));
		if (game.Completed)
		{
			content.AddChild(MakeBoxScoreButton(game));
		}
	}

	private Control BuildImportantChip(DateOnly date, IReadOnlyList<SeasonEvent> events)
	{
		SeasonEvent first = FeaturedOrFirst(events);
		var chip = new PanelContainer
		{
			MouseDefaultCursorShape = CursorShape.PointingHand,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(Important.R, Important.G, Important.B, 0.14f),
			BorderColor = Important,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 6,
			ContentMarginTop = 4,
			ContentMarginRight = 6,
			ContentMarginBottom = 4,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		chip.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				SelectWeekDay(date);
				OpenEventModal(date, events);
				GetViewport().SetInputAsHandled();
			}
		};

		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", 4);
		row.AddChild(MakeIcon(first.Featured ? _iconStar : _iconCalendar, 11, Important));
		var name = MakeText(first.Name.ToUpperInvariant(), _semibold, 9, Important, HorizontalAlignment.Center);
		name.AutowrapMode = TextServer.AutowrapMode.Word;
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(name);
		chip.AddChild(row);
		return chip;
	}

	private Control BuildActions()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		_advanceButton = MakeAction("ADVANCE DAY", true, 1.35f, OnAdvancePressed);
		row.AddChild(_advanceButton);
		row.AddChild(MakeAction("SIMULATE DAY", false, 1f, () => OnSimulatePressed(1)));
		row.AddChild(MakeAction("SIMULATE WEEK", false, 1f, () => OnSimulatePressed(7)));
		RefreshAdvanceButton();
		return row;
	}

	private void RefreshAdvanceButton()
	{
		if (_advanceButton == null)
		{
			return;
		}

		CalendarContest? game = PrimaryGame(_session.ContestsOn(_session.CurrentDate));
		if (ChampionshipDesk.PendingOn(_session) != null)
		{
			_advanceButton.Text = "CELEBRATE";
			return;
		}

		if (AwardsDesk.PendingOn(_session) != null)
		{
			_advanceButton.Text = "AWARDS";
			return;
		}

		if (SeasonReviewDesk.PendingOn(_session))
		{
			_advanceButton.Text = "REVIEW";
			return;
		}

		if (HallOfFameDesk.PendingOn(_session))
		{
			_advanceButton.Text = "HALL OF FAME";
			return;
		}

		if (JobOfferDesk.HasPending(_session))
		{
			_advanceButton.Text = "JOB OFFER";
			return;
		}

		if (JobMarketDesk.PendingOn(_session))
		{
			_advanceButton.Text = "JOB MARKET";
			return;
		}

		AdvanceDayInterrupt? stop = SimulationDesk.InterruptOn(_session);
		if (stop != null)
		{
			_advanceButton.Text = stop.Kind switch
			{
				AdvanceDayInterruptKind.TryoutDecision => "TRYOUTS",
				AdvanceDayInterruptKind.SigningDeadline => "SIGNING",
				AdvanceDayInterruptKind.RosterDeadline => "ROSTER",
				AdvanceDayInterruptKind.PlayoffGame => "PLAY",
				_ => "ADVANCE DAY",
			};
			return;
		}

		_advanceButton.Text = game != null && !game.Completed ? "PLAY" : "ADVANCE DAY";
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (_modalLayer != null && _modalLayer.Visible && ev.IsActionPressed("ui_cancel"))
		{
			UiSounds.Play(UiSound.Back);
			CloseEventModal();
			GetViewport().SetInputAsHandled();
		}
		else if (_boxScoreOverlay != null && _boxScoreOverlay.Visible && ev.IsActionPressed("ui_cancel"))
		{
			UiSounds.Play(UiSound.Back);
			_boxScoreOverlay.Close();
			GetViewport().SetInputAsHandled();
		}
		else if (_simulationOverlay != null && _simulationOverlay.Visible && ev.IsActionPressed("ui_cancel"))
		{
			UiSounds.Play(UiSound.Back);
			_simulationOverlay.Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnWeekDayInput(InputEvent ev, DateOnly date, IReadOnlyList<SeasonEvent> events, CalendarContest? game)
	{
		if (ev is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
		{
			return;
		}

		SelectWeekDay(date);
		if (events.Count > 0)
		{
			OpenEventModal(date, events);
		}
		else if (game != null && game.Completed)
		{
			OpenBoxScore(game);
		}
		else if (game != null)
		{
			OpenGamePreview(game);
		}
	}

	private void SelectWeekDay(DateOnly date)
	{
		_selectedDate = date;
		_inspectedSchoolId = null;
		RefreshWeekCards();
		BindContest(_session.ActiveTeam.Level == TeamLevel.Varsity);
	}

	private void RefreshWeekCards()
	{
		foreach (WeekBind bind in _weekBinds)
		{
			ApplyWeekDayStyle(bind.Card, bind.Date);
		}
	}

	private void ApplyWeekDayStyle(PanelContainer cell, DateOnly date, bool hovered = false)
	{
		bool today = date == _session.CurrentDate;
		bool selected = date == _selectedDate;
		bool past = date < _session.CurrentDate;
		WeekBind? bind = null;
		foreach (WeekBind item in _weekBinds)
		{
			if (item.Date == date)
			{
				bind = item;
				break;
			}
		}

		int borderWidth = selected ? 3 : today || hovered ? 2 : 1;
		Color border = today ? Accent : selected ? TextPrimary : hovered ? new Color(0.72f, 0.76f, 0.82f) : Hairline;
		cell.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = border,
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			ContentMarginLeft = 6,
			ContentMarginTop = 8,
			ContentMarginRight = 6,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		});

		float alpha = 1f;
		if (past)
		{
			alpha = hovered || selected ? 0.72f : 0.52f;
		}
		else if (bind?.Level != null && bind.Level != _session.ActiveTeam.Level)
		{
			alpha = 0.48f;
		}

		cell.Modulate = new Color(1f, 1f, 1f, alpha);
	}

	private Control BuildModalLayer()
	{
		_modalLayer = new Control
		{
			Visible = false,
			MouseFilter = MouseFilterEnum.Stop,
		};
		_modalLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var dim = new ColorRect
		{
			Color = new Color(0.02f, 0.03f, 0.04f, 0.72f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		dim.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				CloseEventModal();
			}
		};
		_modalLayer.AddChild(dim);

		var center = new CenterContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_modalLayer.AddChild(center);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(460, 0),
			MouseFilter = MouseFilterEnum.Stop,
		};
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.078431f, 0.094118f, 0.98f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 16,
			ContentMarginTop = 14,
			ContentMarginRight = 16,
			ContentMarginBottom = 16,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		center.AddChild(panel);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 10);
		panel.AddChild(layout);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		titles.AddThemeConstantOverride("separation", -2);
		titles.AddChild(MakeText("IMPORTANT DATE", _medium, 10, Important, HorizontalAlignment.Left));
		_modalTitle = MakeText("DATE", _bold, 20, TextPrimary, HorizontalAlignment.Left);
		titles.AddChild(_modalTitle);
		header.AddChild(titles);
		header.AddChild(MakeAction("CLOSE", false, 0f, CloseEventModal));
		layout.AddChild(header);
		layout.AddChild(MakeAccentLine());

		var scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, 280),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		layout.AddChild(scroll);

		_modalBody = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_modalBody.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(_modalBody);
		return _modalLayer;
	}

	private void OpenEventModal(DateOnly date, IReadOnlyList<SeasonEvent> events)
	{
		foreach (Node child in _modalBody.GetChildren())
		{
			_modalBody.RemoveChild(child);
			child.QueueFree();
		}

		_modalTitle.Text = date.ToString("dddd, MMMM d", CultureInfo.InvariantCulture).ToUpperInvariant();
		foreach (SeasonEvent item in events)
		{
			_modalBody.AddChild(BuildModalEventCard(item));
		}

		IReadOnlyList<CalendarContest> games = _session.ContestsOn(date);
		if (games.Count > 0)
		{
			_modalBody.AddChild(MakeText("GAMES", _semibold, 11, TextMuted, HorizontalAlignment.Left));
			foreach (CalendarContest game in games)
			{
				string line = $"{game.Level.ToLabel().ToUpperInvariant()}  {(game.Home ? "VS" : "@")}  {DistrictHubData.GetTeam(game.OpponentId).ShortName}  ·  {game.Time}  ·  {game.ResultLabel}";
				_modalBody.AddChild(MakeText(line, _medium, 12, TextPrimary, HorizontalAlignment.Left));
			}
		}

		_modalLayer.Visible = true;
		_modalLayer.MoveToFront();
	}

	private void CloseEventModal()
	{
		if (_modalLayer != null)
		{
			_modalLayer.Visible = false;
		}
	}

	private void CloseBoxScore()
	{
		_boxScoreOverlay?.Close();
	}

	private void OpenBoxScore(CalendarContest game)
	{
		CloseEventModal();
		_boxScoreOverlay.Open(ClubhouseBoxScore.For(game));
	}

	private Control BuildBoxScoreHeader(BoxScore sheet)
	{
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", MakeInnerCard(14, 12));
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		row.AddThemeConstantOverride("separation", 18);
		card.AddChild(row);

		row.AddChild(BuildMatchupClub(sheet.Away, "AWAY"));
		var score = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		score.AddThemeConstantOverride("separation", -4);
		Color result = sheet.Game.Won ? Green : sheet.Game.TeamScore == sheet.Game.OpponentScore ? Accent : Urgent;
		score.AddChild(MakeText("FINAL", _medium, 10, TextMuted, HorizontalAlignment.Center));
		score.AddChild(MakeText($"{sheet.Away.Runs}   -   {sheet.Home.Runs}", _bold, 32, result, HorizontalAlignment.Center));
		score.AddChild(MakeText($"{sheet.Game.Time}  ·  {sheet.Game.VenueLabel}", _semibold, 11, TextMuted, HorizontalAlignment.Center));
		row.AddChild(score);
		row.AddChild(BuildMatchupClub(sheet.Home, "HOME"));
		return card;
	}

	private Control BuildMatchupClub(BoxScoreSide side, string venue)
	{
		var col = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		col.AddThemeConstantOverride("separation", 4);
		col.AddChild(MakeTeamMark(side.TeamId, 56, Accent));
		col.AddChild(MakeText(side.ShortName, _bold, 16, TextPrimary, HorizontalAlignment.Center));
		col.AddChild(MakeText(venue, _medium, 10, TextMuted, HorizontalAlignment.Center));
		return col;
	}

	private Control BuildSection(string title, Control body)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		box.AddChild(MakeText(title, _semibold, 11, Accent, HorizontalAlignment.Left));
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", MakeInnerCard(10, 8));
		card.AddChild(body);
		box.AddChild(card);
		return box;
	}

	private Control BuildLineScore(BoxScore sheet)
	{
		var grid = new GridContainer
		{
			Columns = ClubhouseBoxScore.Innings + 4,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		grid.AddThemeConstantOverride("h_separation", 6);
		grid.AddThemeConstantOverride("v_separation", 4);

		AddLineCell(grid, string.Empty, _medium, TextMuted, 90, HorizontalAlignment.Left);
		for (int inning = 1; inning <= ClubhouseBoxScore.Innings; inning++)
		{
			AddLineCell(grid, inning.ToString(CultureInfo.InvariantCulture), _semibold, TextMuted, 28, HorizontalAlignment.Center);
		}

		AddLineCell(grid, "R", _bold, Accent, 32, HorizontalAlignment.Center);
		AddLineCell(grid, "H", _bold, Accent, 32, HorizontalAlignment.Center);
		AddLineCell(grid, "E", _bold, Accent, 32, HorizontalAlignment.Center);

		AddLineScoreRow(grid, sheet.Away, false);
		AddLineScoreRow(grid, sheet.Home, true);
		return grid;
	}

	private void AddLineScoreRow(GridContainer grid, BoxScoreSide side, bool home)
	{
		AddLineCell(grid, side.ShortName, _semibold, TextPrimary, 90, HorizontalAlignment.Left);
		for (int i = 0; i < ClubhouseBoxScore.Innings; i++)
		{
			string value = home && side.SkippedLast && i == ClubhouseBoxScore.Innings - 1
				? "X"
				: side.InningRuns[i].ToString(CultureInfo.InvariantCulture);
			AddLineCell(grid, value, _medium, TextPrimary, 28, HorizontalAlignment.Center);
		}

		AddLineCell(grid, side.Runs.ToString(CultureInfo.InvariantCulture), _bold, TextPrimary, 32, HorizontalAlignment.Center);
		AddLineCell(grid, side.Hits.ToString(CultureInfo.InvariantCulture), _semibold, TextPrimary, 32, HorizontalAlignment.Center);
		AddLineCell(grid, side.Errors.ToString(CultureInfo.InvariantCulture), _semibold, TextPrimary, 32, HorizontalAlignment.Center);
	}

	private void AddLineCell(GridContainer grid, string text, FontFile font, Color color, float width, HorizontalAlignment align)
	{
		var label = MakeText(text, font, 12, color, align);
		label.CustomMinimumSize = new Vector2(width, 0);
		grid.AddChild(label);
	}

	private Control BuildBattingTable(BoxScoreSide side)
	{
		var grid = new GridContainer
		{
			Columns = 7,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		grid.AddThemeConstantOverride("h_separation", 8);
		grid.AddThemeConstantOverride("v_separation", 3);
		AddBattingRow(grid, "PLAYER", "POS", "AB", "R", "H", "HR", "RBI", _semibold, TextMuted, true);
		foreach (BoxScoreBatter batter in side.Batters)
		{
			AddBattingRow(
				grid,
				batter.Name.ToUpperInvariant(),
				batter.Position,
				batter.AtBats.ToString(CultureInfo.InvariantCulture),
				batter.Runs.ToString(CultureInfo.InvariantCulture),
				batter.Hits.ToString(CultureInfo.InvariantCulture),
				batter.HomeRuns.ToString(CultureInfo.InvariantCulture),
				batter.Rbi.ToString(CultureInfo.InvariantCulture),
				_medium,
				TextPrimary,
				false,
				side.TeamId,
				batter.Name);
		}

		return grid;
	}

	private void AddBattingRow(
		GridContainer grid,
		string name,
		string pos,
		string ab,
		string runs,
		string hits,
		string hr,
		string rbi,
		FontFile font,
		Color color,
		bool header,
		string teamId = "",
		string playerName = "")
	{
		var player = MakeText(name, font, header ? 11 : 12, color, HorizontalAlignment.Left);
		player.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		if (!header && playerName.Length > 0)
		{
			PlayerLinks.MakeClickable(player, teamId, playerName);
		}

		grid.AddChild(player);
		grid.AddChild(MakeFixed(pos, 36, font, color));
		grid.AddChild(MakeFixed(ab, 28, font, color));
		grid.AddChild(MakeFixed(runs, 28, font, color));
		grid.AddChild(MakeFixed(hits, 28, font, color));
		grid.AddChild(MakeFixed(hr, 28, font, color));
		grid.AddChild(MakeFixed(rbi, 36, font, color));
	}

	private Control BuildPitchingTable(BoxScoreSide side)
	{
		var grid = new GridContainer
		{
			Columns = 5,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		grid.AddThemeConstantOverride("h_separation", 8);
		grid.AddThemeConstantOverride("v_separation", 3);
		AddPitchingRow(grid, "PITCHER", "IP", "H", "BB", "K", _semibold, TextMuted);
		foreach (BoxScorePitcher pitcher in side.Pitchers)
		{
			AddPitchingRow(
				grid,
				pitcher.Name.ToUpperInvariant(),
				pitcher.Innings,
				pitcher.Hits.ToString(CultureInfo.InvariantCulture),
				pitcher.Walks.ToString(CultureInfo.InvariantCulture),
				pitcher.Strikeouts.ToString(CultureInfo.InvariantCulture),
				_medium,
				TextPrimary,
				side.TeamId,
				pitcher.Name);
		}

		return grid;
	}

	private void AddPitchingRow(
		GridContainer grid,
		string name,
		string ip,
		string hits,
		string walks,
		string k,
		FontFile font,
		Color color,
		string teamId = "",
		string playerName = "")
	{
		var player = MakeText(name, font, 12, color, HorizontalAlignment.Left);
		player.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		if (playerName.Length > 0)
		{
			PlayerLinks.MakeClickable(player, teamId, playerName);
		}

		grid.AddChild(player);
		grid.AddChild(MakeFixed(ip, 40, font, color));
		grid.AddChild(MakeFixed(hits, 28, font, color));
		grid.AddChild(MakeFixed(walks, 28, font, color));
		grid.AddChild(MakeFixed(k, 28, font, color));
	}

	private Control BuildGameData(BoxScore sheet)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		box.AddChild(MakeText($"WP:  {sheet.WinningPitcher.ToUpperInvariant()}", _semibold, 13, Green, HorizontalAlignment.Left));
		box.AddChild(MakeText($"LP:  {sheet.LosingPitcher.ToUpperInvariant()}", _semibold, 13, Urgent, HorizontalAlignment.Left));
		if (!string.IsNullOrEmpty(sheet.Save))
		{
			box.AddChild(MakeText($"SV:  {sheet.Save.ToUpperInvariant()}", _semibold, 13, Accent, HorizontalAlignment.Left));
		}

		box.AddChild(MakeText("FIELDING", _medium, 10, TextMuted, HorizontalAlignment.Left));
		foreach (string play in sheet.FieldingPlays)
		{
			var line = MakeText(play, _medium, 12, TextPrimary, HorizontalAlignment.Left);
			line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			box.AddChild(line);
		}

		return box;
	}

	private Label MakeFixed(string text, float width, FontFile font, Color color)
	{
		var label = MakeText(text, font, 12, color, HorizontalAlignment.Center);
		label.CustomMinimumSize = new Vector2(width, 0);
		return label;
	}

	private Button MakeBoxScoreButton(CalendarContest game)
	{
		var button = new Button
		{
			Text = "BOX SCORE",
			FocusMode = FocusModeEnum.All,
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(0, 22),
			TooltipText = "View the full box score",
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 9);
		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeStyleboxOverride("normal", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 6,
			ContentMarginTop = 3,
			ContentMarginRight = 6,
			ContentMarginBottom = 3,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("focus", MakeOutlinedButton(HoverBg, Accent));
		CalendarContest captured = game;
		button.Pressed += () =>
		{
			OpenBoxScore(captured);
			GetViewport().SetInputAsHandled();
		};
		return button;
	}

	private Control MakeTeamMark(string teamId, float size, Color fallbackColor)
	{
		TextureRect? logo = TeamLogos.TryMakeIcon(teamId, size);
		if (logo != null)
		{
			logo.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
			logo.MouseFilter = MouseFilterEnum.Ignore;
			SchoolLinks.MakeClickable(logo, teamId);
			return logo;
		}

		Control fallback = BuildTinyLogo(ClubhouseCalendar.OpponentMark(teamId), fallbackColor, size);
		SchoolLinks.MakeClickable(fallback, teamId);
		return fallback;
	}

	private Control BuildModalEventCard(SeasonEvent item)
	{
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", MakeInnerCard(10, 8));
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 4);
		card.AddChild(box);

		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 6);
		titleRow.AddChild(MakeIcon(item.Featured ? _iconStar : _iconCalendar, 16, Important));
		var title = MakeText(item.Name.ToUpperInvariant(), _bold, 16, TextPrimary, HorizontalAlignment.Left);
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		title.AutowrapMode = TextServer.AutowrapMode.Word;
		titleRow.AddChild(title);
		box.AddChild(titleRow);
		box.AddChild(MakeText(item.FormatDates().ToUpperInvariant(), _semibold, 11, TextMuted, HorizontalAlignment.Left));
		var body = MakeText(item.Summary, _medium, 14, new Color(0.78f, 0.81f, 0.86f), HorizontalAlignment.Left);
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		box.AddChild(body);
		if (item.Id is "open-play" or "signing-week")
		{
			box.AddChild(MakeEventLink("OPEN RECRUITING", OpenTryouts));
		}

		if (item.Id is "incoming-class" or "signing-deadline")
		{
			box.AddChild(MakeEventLink("VIEW INCOMING CLASS", OpenIncomingClass));
		}

		if (item.Id is "graduation" or "senior-farewell")
		{
			box.AddChild(MakeEventLink("VIEW GRADUATION", OpenGraduation));
		}

		ChampionshipTitle? celebration = SeasonCalendar.ChampionshipForEvent(item.Id);
		if (celebration != null)
		{
			box.AddChild(MakeEventLink("VIEW CELEBRATION", () => OpenChampionship(celebration.Value)));
		}

		AwardScope? banquet = SeasonCalendar.AwardCeremonyForEvent(item.Id);
		if (banquet != null)
		{
			box.AddChild(MakeEventLink("VIEW CEREMONY", () => OpenAwardsCeremony(banquet.Value)));
		}

		if (item.Id == "board-review")
		{
			box.AddChild(MakeEventLink("VIEW SEASON REVIEW", OpenSeasonReview));
		}

		if (item.Id == "hall-of-fame")
		{
			box.AddChild(MakeEventLink("VIEW HALL OF FAME", OpenHallOfFame));
		}

		if (item.Id == "coaching-carousel")
		{
			if (JobOfferDesk.HasPending(_session))
			{
				box.AddChild(MakeEventLink("READ JOB OFFER", ShowJobOffer));
			}

			box.AddChild(MakeEventLink("VIEW JOB MARKET", OpenJobMarket));
		}

		return card;
	}

	private Button MakeEventLink(string text, Action pressed)
	{
		var button = new Button { Text = text };
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, CardBorder));
		button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("focus", MakeOutlinedButton(HoverBg, Accent));
		button.Pressed += pressed;
		return button;
	}

	private void OpenTryouts()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowTryouts();
		}
	}

	private void OpenIncomingClass()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowIncomingClass();
		}
	}

	private void OpenGraduation()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowGraduation();
		}
	}

	private void OpenChampionship(ChampionshipTitle title)
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowChampionship(title);
		}
	}

	private void OpenAwardsCeremony(AwardScope scope)
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowAwardsCeremony(scope);
		}
	}

	private void OpenSeasonReview()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowSeasonReview();
		}
	}

	private void OpenHallOfFameInduction()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowHallOfFameInduction();
		}
	}

	private void OpenHallOfFame()
	{
		if (GetTree().CurrentScene is not AppNavigator navigator)
		{
			return;
		}

		if (HallOfFameDesk.PendingOn(_session))
		{
			navigator.ShowHallOfFameInduction();
			return;
		}

		navigator.ShowCareerHallOfFame();
	}

	public void ShowJobOffer()
	{
		if (!JobOfferDesk.HasPending(_session) || _jobOfferOverlay == null)
		{
			return;
		}

		RefreshJobOfferBanner();
		_jobOfferBanner.Visible = false;
		_jobOfferOverlay.Present();
	}

	private void OnJobOfferDismissed()
	{
		_session.AcknowledgeJobOffer();
		RefreshJobOfferBanner();
		RefreshAdvanceButton();
	}

	private void OnJobOfferResolved()
	{
		RefreshJobOfferBanner();
		RefreshAdvanceButton();
	}

	private void RefreshJobOfferBanner()
	{
		if (_jobOfferBanner == null)
		{
			return;
		}

		_jobOfferBanner.Visible = JobOfferDesk.BannerOn(_session)
			&& (_jobOfferOverlay == null || !_jobOfferOverlay.Visible);
	}

	private void RefreshNewsBanner()
	{
		if (_newsTicker == null || _newsTickerSlot == null)
		{
			return;
		}

		StopNewsTicker();
		_tickerItems.Clear();

		foreach (InboxMessage item in MessagesDesk.For(_session))
		{
			InboxMessage captured = item;
			_tickerItems.Add(new TickerItem(
				item.Headline,
				item.Unread,
				TickerAccent(item.Channel),
				() => OpenTickerMessage(captured)));
		}

		foreach (NewsStory story in NewsDesk.For(_session))
		{
			_tickerItems.Add(new TickerItem(
				story.Headline,
				!story.Read,
				LakesBlue,
				OpenNews));
		}

		_tickerIndex = 0;
		for (int i = 0; i < _tickerItems.Count; i++)
		{
			if (_tickerItems[i].Unread)
			{
				_tickerIndex = i;
				break;
			}
		}

		int unread = 0;
		foreach (TickerItem item in _tickerItems)
		{
			if (item.Unread)
			{
				unread++;
			}
		}

		_newsTickerKicker.Text = unread > 1
			? $"UPDATES  ·  {unread} NEW"
			: unread == 1 ? "UPDATES  ·  NEW" : "UPDATES";

		PaintTickerRows();
		CallDeferred(nameof(LayoutTickerTrack));
	}

	private Control BuildNewsTicker()
	{
		var pad = new MarginContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = "Open this update",
		};
		pad.AddThemeConstantOverride("margin_left", 2);
		pad.AddThemeConstantOverride("margin_right", 2);
		pad.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				OpenTickerCurrent();
			}
		};

		var shell = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		shell.AddThemeConstantOverride("separation", 2);
		pad.AddChild(shell);

		_newsTickerKicker = MakeText("UPDATES", _medium, 10, Accent, HorizontalAlignment.Left);
		shell.AddChild(_newsTickerKicker);

		_newsTickerViewport = new Control
		{
			ClipContents = true,
			CustomMinimumSize = new Vector2(180, TickerLineHeight * TickerVisibleLines),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		shell.AddChild(_newsTickerViewport);

		_newsTickerTrack = new Control
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_newsTickerViewport.AddChild(_newsTickerTrack);
		_newsTickerViewport.Resized += LayoutTickerTrack;

		int slots = TickerVisibleLines + 1;
		for (int i = 0; i < slots; i++)
		{
			var row = new HBoxContainer
			{
				CustomMinimumSize = new Vector2(0, TickerLineHeight),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			row.AddThemeConstantOverride("separation", 6);
			var bullet = MakeText("•", _bold, 14, Accent, HorizontalAlignment.Left);
			var headline = MakeText("", _semibold, 13, TextPrimary, HorizontalAlignment.Left);
			headline.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			headline.ClipText = true;
			headline.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			row.AddChild(bullet);
			row.AddChild(headline);
			_newsTickerTrack.AddChild(row);
			_tickerRows.Add(row);
			_tickerBullets.Add(bullet);
			_tickerHeadlines.Add(headline);
		}

		return pad;
	}

	private void PaintTickerRows()
	{
		for (int i = 0; i < _tickerRows.Count; i++)
		{
			if (_tickerItems.Count == 0)
			{
				_tickerBullets[i].Text = i == 0 ? "•" : "";
				_tickerBullets[i].AddThemeColorOverride("font_color", TextMuted);
				_tickerHeadlines[i].Text = i == 0 ? "NO UPDATES" : "";
				_tickerHeadlines[i].AddThemeFontOverride("font", _medium);
				_tickerHeadlines[i].AddThemeColorOverride("font_color", TextMuted);
			}
			else
			{
				TickerItem item = _tickerItems[WrapTickerIndex(_tickerIndex + i)];
				_tickerBullets[i].Text = "•";
				_tickerBullets[i].AddThemeColorOverride("font_color", item.Unread ? item.Accent : TextMuted);
				_tickerHeadlines[i].Text = item.Headline.ToUpperInvariant();
				_tickerHeadlines[i].AddThemeFontOverride("font", item.Unread ? _semibold : _medium);
				_tickerHeadlines[i].AddThemeColorOverride("font_color", item.Unread ? TextPrimary : new Color(0.72f, 0.76f, 0.82f));
			}

			_tickerRows[i].Modulate = i >= TickerVisibleLines
				? new Color(1f, 1f, 1f, 0f)
				: Colors.White;
		}

		_newsTickerTrack.Position = Vector2.Zero;
		LayoutTickerTrack();
	}

	private void LayoutTickerTrack()
	{
		if (_newsTickerViewport == null || _newsTickerTrack == null)
		{
			return;
		}

		float width = _newsTickerViewport.Size.X;
		if (width <= 0f)
		{
			return;
		}

		_newsTickerTrack.Size = new Vector2(width, TickerLineHeight * (TickerVisibleLines + 1));
		for (int i = 0; i < _tickerRows.Count; i++)
		{
			_tickerRows[i].Position = new Vector2(0f, i * TickerLineHeight);
			_tickerRows[i].Size = new Vector2(width, TickerLineHeight);
		}
	}

	private void TickNewsTicker(float delta)
	{
		if (_tickerItems.Count <= 1 || !IsVisibleInTree() || _newsTickerTrack == null)
		{
			return;
		}

		_tickerClock += delta;
		if (!_tickerSliding)
		{
			if (_tickerClock >= TickerDwellSeconds)
			{
				_tickerSliding = true;
				_tickerClock = 0f;
			}

			return;
		}

		float t = Mathf.Clamp(_tickerClock / TickerSlideSeconds, 0f, 1f);
		float eased = t * t * (3f - 2f * t);
		_newsTickerTrack.Position = new Vector2(0f, -TickerLineHeight * eased);
		if (_tickerRows.Count > 0)
		{
			_tickerRows[0].Modulate = new Color(1f, 1f, 1f, 1f - eased);
		}

		if (_tickerRows.Count > TickerVisibleLines)
		{
			_tickerRows[TickerVisibleLines].Modulate = new Color(1f, 1f, 1f, eased);
		}

		if (t >= 1f)
		{
			_tickerIndex = WrapTickerIndex(_tickerIndex + 1);
			_tickerSliding = false;
			_tickerClock = 0f;
			PaintTickerRows();
		}
	}

	private int WrapTickerIndex(int index)
	{
		int count = _tickerItems.Count;
		if (count <= 0)
		{
			return 0;
		}

		int wrapped = index % count;
		return wrapped < 0 ? wrapped + count : wrapped;
	}

	private void StopNewsTicker()
	{
		_tickerClock = 0f;
		_tickerSliding = false;
		if (_newsTickerTrack != null)
		{
			_newsTickerTrack.Position = Vector2.Zero;
		}
	}

	private void OpenTickerCurrent()
	{
		if (_tickerItems.Count == 0)
		{
			OpenMessages();
			return;
		}

		_tickerItems[WrapTickerIndex(_tickerIndex)].Open();
	}

	private void OpenTickerMessage(InboxMessage item)
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.OpenInboxItem(item);
		}
	}

	private static Color TickerAccent(InboxChannel channel) => channel switch
	{
		InboxChannel.Urgent => Urgent,
		InboxChannel.Player => Accent,
		InboxChannel.Scouting => LakesBlue,
		InboxChannel.Board => Accent,
		InboxChannel.Career => Accent,
		_ => LakesBlue,
	};

	private void OpenMessages()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowMessages();
		}
	}

	private void OpenNews()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowNews();
		}
	}

	private void OpenJobMarket()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowCareerJobMarket();
		}
	}

	private void PresentPendingCelebration()
	{
		if (!IsVisibleInTree()
			|| (_jobOfferOverlay != null && _jobOfferOverlay.Visible)
			|| (_simulationOverlay != null && _simulationOverlay.Visible))
		{
			return;
		}

		ChampionshipTitle? pending = ChampionshipDesk.PendingOn(_session);
		if (pending != null)
		{
			OpenChampionship(pending.Value);
			return;
		}

		AwardScope? banquet = AwardsDesk.PendingOn(_session);
		if (banquet != null)
		{
			OpenAwardsCeremony(banquet.Value);
			return;
		}

		if (SeasonReviewDesk.PendingOn(_session))
		{
			OpenSeasonReview();
			return;
		}

		if (HallOfFameDesk.PendingOn(_session))
		{
			OpenHallOfFameInduction();
			return;
		}

		if (JobOfferDesk.PendingOn(_session))
		{
			ShowJobOffer();
			return;
		}

		if (JobMarketDesk.PendingOn(_session) && !JobOfferDesk.HasPending(_session))
		{
			OpenJobMarket();
		}
	}

	private Control MakeResultMark(CalendarContest game)
	{
		Color color = game.Won ? Green : game.TeamScore == game.OpponentScore ? Accent : Urgent;
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddChild(MakeText(game.ResultLabel, _bold, 13, color, HorizontalAlignment.Center));
		return row;
	}

	private TextureRect MakeIcon(Texture2D texture, float size, Color modulate)
	{
		return new TextureRect
		{
			Texture = texture,
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = TextureFilterEnum.LinearWithMipmaps,
			Modulate = modulate,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
	}

	private static DateOnly WeekStart(DateOnly date) => date.AddDays(-(int)date.DayOfWeek);

	private CalendarContest? PrimaryGame(IReadOnlyList<CalendarContest> games)
	{
		CalendarContest? fallback = null;
		foreach (CalendarContest game in games)
		{
			if (game.Level == _session.ActiveTeam.Level)
			{
				return game;
			}

			fallback ??= game;
		}

		return fallback;
	}

	private static SeasonEvent FeaturedOrFirst(IReadOnlyList<SeasonEvent> events)
	{
		foreach (SeasonEvent item in events)
		{
			if (item.Featured)
			{
				return item;
			}
		}

		return events[0];
	}

	private (string Name, string When, Color Color) NextMilestone()
	{
		DateOnly today = _session.CurrentDate;
		foreach (SeasonEvent item in SeasonCalendar.ForSeason(GameSession.SeasonYear))
		{
			if (item.End < today)
			{
				continue;
			}

			int days = Math.Max(0, item.Start.DayNumber - today.DayNumber);
			string when = item.OccursOn(today)
				? item.Start == item.End ? "TODAY" : "IN PROGRESS"
				: days == 1 ? "1 DAY" : $"{days} DAYS";
			Color color = days < 8 ? Urgent : TextPrimary;
			return (item.Name.ToUpperInvariant(), when, color);
		}

		return ("NONE", string.Empty, TextMuted);
	}

	private Button MakeMonthlyCalendarButton()
	{
		var button = new Button
		{
			Text = "MONTHLY CALENDAR",
			Icon = _iconCalendar,
			ExpandIcon = true,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			CustomMinimumSize = new Vector2(0, 28),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			TooltipText = "Open the season calendar",
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 11);
		button.AddThemeConstantOverride("icon_max_width", 14);
		button.AddThemeConstantOverride("h_separation", 6);
		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeColorOverride("font_pressed_color", TextPrimary);
		button.AddThemeColorOverride("font_focus_color", Colors.White);
		button.AddThemeColorOverride("icon_normal_color", TextPrimary);
		button.AddThemeColorOverride("icon_hover_color", Colors.White);
		button.AddThemeColorOverride("icon_pressed_color", TextPrimary);
		button.AddThemeColorOverride("icon_focus_color", Colors.White);
		button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, CardBorder));
		button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("focus", MakeOutlinedButton(HoverBg, Accent));
		button.Pressed += OpenMonthlyCalendar;
		return button;
	}

	private void OpenMonthlyCalendar()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowCalendar();
		}
	}

	private Button MakeSeasonScheduleButton()
	{
		var button = new Button
		{
			Text = "SEASON SCHEDULE",
			Icon = _iconStar,
			ExpandIcon = true,
			FocusMode = FocusModeEnum.All,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			CustomMinimumSize = new Vector2(0, 28),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			TooltipText = "Open the season results table",
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 11);
		button.AddThemeConstantOverride("icon_max_width", 14);
		button.AddThemeConstantOverride("h_separation", 6);
		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeColorOverride("font_pressed_color", TextPrimary);
		button.AddThemeColorOverride("font_focus_color", Colors.White);
		button.AddThemeColorOverride("icon_normal_color", TextPrimary);
		button.AddThemeColorOverride("icon_hover_color", Colors.White);
		button.AddThemeColorOverride("icon_pressed_color", TextPrimary);
		button.AddThemeColorOverride("icon_focus_color", Colors.White);
		button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, CardBorder));
		button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("focus", MakeOutlinedButton(HoverBg, Accent));
		button.Pressed += OpenSeasonSchedule;
		return button;
	}

	private void OpenSeasonSchedule()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowSchedule();
		}
	}

	private void OnAdvancePressed()
	{
		ChampionshipTitle? pending = ChampionshipDesk.PendingOn(_session);
		if (pending != null)
		{
			OpenChampionship(pending.Value);
			return;
		}

		AwardScope? banquet = AwardsDesk.PendingOn(_session);
		if (banquet != null)
		{
			OpenAwardsCeremony(banquet.Value);
			return;
		}

		if (SeasonReviewDesk.PendingOn(_session))
		{
			OpenSeasonReview();
			return;
		}

		if (HallOfFameDesk.PendingOn(_session))
		{
			OpenHallOfFameInduction();
			return;
		}

		if (JobOfferDesk.HasPending(_session))
		{
			ShowJobOffer();
			return;
		}

		if (JobMarketDesk.PendingOn(_session))
		{
			OpenJobMarket();
			return;
		}

		AdvanceDayInterrupt? stop = SimulationDesk.InterruptOn(_session);
		if (stop != null)
		{
			OpenInterrupt(stop);
			return;
		}

		CalendarContest? game = PrimaryGame(_session.ContestsOn(_session.CurrentDate));
		if (game != null && !game.Completed)
		{
			OpenGamePreview(game);
			return;
		}

		OnSimulatePressed(1);
	}

	private void OnSimulatePressed(int days)
	{
		SimulationRun run = _session.SimulateDays(days);
		_selectedDate = _session.CurrentDate;
		ApplyActiveTeam();
		if (run.Interrupt != null)
		{
			_simulationOverlay.Present(run.Interrupt);
			return;
		}

		if (run.DaysAdvanced > 0)
		{
			UiSounds.Play(UiSound.AdvanceDay);
		}
	}

	private void OpenInterrupt(AdvanceDayInterrupt interrupt)
	{
		switch (interrupt.Kind)
		{
			case AdvanceDayInterruptKind.TryoutDecision:
			case AdvanceDayInterruptKind.SigningDeadline:
				OpenTryouts();
				break;
			case AdvanceDayInterruptKind.RosterDeadline:
				if (GetTree().CurrentScene is AppNavigator rosterNav)
				{
					rosterNav.ShowRoster();
				}

				break;
			case AdvanceDayInterruptKind.PlayoffGame:
				OpenPlayoff();
				break;
			case AdvanceDayInterruptKind.JobOffer:
				ShowJobOffer();
				break;
		}
	}

	private void OpenPlayoff()
	{
		CalendarContest? game = PrimaryGame(_session.ContestsOn(_session.CurrentDate));
		if (game != null && !game.Completed)
		{
			OpenGamePreview(game);
			return;
		}

		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowPlayoffs();
		}
	}

	private void OpenMatchupPreview()
	{
		CalendarContest? game = PreviewContest();
		if (game != null)
		{
			OpenGamePreview(game);
		}
	}

	private void OpenGamePreview(CalendarContest game)
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowGamePreview(game);
		}
	}

	private CalendarContest? PreviewContest()
	{
		CalendarContest? selected = PrimaryGame(_session.ContestsOn(_selectedDate));
		if (selected != null && !selected.Completed)
		{
			return selected;
		}

		CalendarContest? today = PrimaryGame(_session.ContestsOn(_session.CurrentDate));
		if (today != null && !today.Completed)
		{
			return today;
		}

		foreach (CalendarContest game in ClubhouseCalendar.Contests)
		{
			if (game.Level == _session.ActiveTeam.Level && !game.Completed && game.Date >= _session.CurrentDate)
			{
				return game;
			}
		}

		return today ?? selected;
	}

	private Button MakeAction(string text, bool primary, float stretch, System.Action? onPressed = null)
	{
		var button = new Button
		{
			Text = text,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = stretch,
			CustomMinimumSize = new Vector2(0, 32),
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		if (primary)
		{
			button.AddThemeColorOverride("font_color", TextOnAccent);
			button.AddThemeColorOverride("font_hover_color", TextOnAccent);
			button.AddThemeColorOverride("font_pressed_color", TextOnAccent);
			button.AddThemeColorOverride("font_focus_color", TextOnAccent);
			button.AddThemeStyleboxOverride("normal", MakeFilledButton(Accent));
			button.AddThemeStyleboxOverride("hover", MakeFilledButton(AccentHover));
			button.AddThemeStyleboxOverride("pressed", MakeFilledButton(AccentHover));
			button.AddThemeStyleboxOverride("focus", MakeFilledButton(AccentHover));
		}
		else
		{
			button.AddThemeColorOverride("font_color", TextPrimary);
			button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, CardBorder));
			button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
			button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
			button.AddThemeStyleboxOverride("focus", MakeOutlinedButton(HoverBg, Accent));
		}

		if (onPressed != null)
		{
			button.Pressed += onPressed;
		}

		return button;
	}

	private void BindContest(bool varsity)
	{
		while (_contestHost.GetChildCount() > 0)
		{
			Node child = _contestHost.GetChild(0);
			_contestHost.RemoveChild(child);
			child.QueueFree();
		}

		if (_inspectedPlayerTeamId != null && !string.IsNullOrEmpty(_inspectedPlayerName))
		{
			var card = new PlayerProfileCard
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill,
			};
			card.SetCloseHandler(CloseInspectedProfile);
			_contestHost.AddChild(card);
			PlayerProfileSnapshot? snapshot = PlayerProfiles.Find(
				_inspectedPlayerTeamId,
				_inspectedPlayerName,
				varsity ? TeamLevel.Varsity : TeamLevel.JuniorVarsity);
			if (snapshot != null)
			{
				card.Bind(snapshot);
			}

			return;
		}

		if (_inspectedSchoolId != null)
		{
			var card = new SchoolProfileCard
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill,
			};
			card.SetCloseHandler(CloseInspectedProfile);
			_contestHost.AddChild(card);
			card.Bind(_inspectedSchoolId, varsity ? TeamLevel.Varsity : TeamLevel.JuniorVarsity);
			return;
		}

		_contestHost.AddChild(varsity ? BuildVarsityContest() : BuildJvContest());
	}

	private Control BuildVarsityContest()
	{
		return BuildContestCard(
			"TOMORROW NIGHT",
			"FRIDAY, APRIL 25  ·  7:00 PM",
			"CASCADE FIELD",
			"HOME",
			"CASCADE",
			"18-4 (8-2)",
			_cascadeLogo,
			"GREAT LAKES",
			"14-8 (6-4)",
			_lakesLogo,
			"GL",
			new PitcherCard("MARINI", "Luca", "RHP", "5-0", "1.95", "28", _marini, Accent, DistrictHubData.UserTeamId, "Luca Marini"),
			new PitcherCard("TONY", "Great Lakes", "RHP", "2-2", "3.14", "22", _tony, LakesBlue, "great-lakes", "Tony Romano"));
	}

	private Control BuildJvContest()
	{
		return BuildContestCard(
			"NEXT JV CONTEST",
			"SATURDAY, APRIL 26  ·  11:00 AM",
			"CASCADE FIELD",
			"HOME",
			"CASCADE JV",
			"12-8 (5-5)",
			_cascadeLogo,
			"GREAT LAKES JV",
			"11-9 (4-6)",
			_lakesLogo,
			"GL",
			new PitcherCard("GRANT", "Miles", "RHP", "1-2", "5.06", "12", null, Accent, DistrictHubData.UserTeamId, "Miles Grant"),
			new PitcherCard("HALE", "Great Lakes", "LHP", "2-1", "3.80", "16", null, LakesBlue, "great-lakes", "Gavin Hale"));
	}

	private Control BuildContestCard(
		string kicker,
		string when,
		string venue,
		string homeAway,
		string homeName,
		string homeRecord,
		Texture2D homeLogo,
		string awayName,
		string awayRecord,
		Texture2D? awayLogo,
		string awayInitial,
		PitcherCard homePitcher,
		PitcherCard awayPitcher)
	{
		var card = MakeCard();
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		var layout = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		layout.AddThemeConstantOverride("separation", 8);
		card.AddChild(layout);

		var header = new HBoxContainer();
		var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		titles.AddThemeConstantOverride("separation", -2);
		titles.AddChild(MakeText(kicker, _medium, 10, Accent, HorizontalAlignment.Left));
		titles.AddChild(MakeText("NEXT CONTEST", _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		header.AddChild(titles);
		header.AddChild(MakeText(homeAway, _semibold, 11, homeAway == "HOME" ? Green : Accent, HorizontalAlignment.Right));
		layout.AddChild(header);
		layout.AddChild(MakeAccentLine());

		var matchup = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.7f,
		};
		matchup.AddThemeConstantOverride("separation", 10);
		matchup.AddChild(BuildTeamBlock(homeName, homeRecord, homeLogo, null, Accent, DistrictHubData.UserTeamId));
		var vs = MakeText("VS", _bold, 16, TextMuted, HorizontalAlignment.Center);
		vs.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		matchup.AddChild(vs);
		matchup.AddChild(BuildTeamBlock(awayName, awayRecord, awayLogo, awayInitial, LakesBlue, "great-lakes"));
		layout.AddChild(matchup);

		layout.AddChild(BuildMetaRow(_iconClock, when));
		layout.AddChild(BuildMetaRow(_iconPin, venue));
		layout.AddChild(MakeHairline());

		var pitchersTitle = MakeText("PROBABLE PITCHERS", _semibold, 11, TextMuted, HorizontalAlignment.Left);
		layout.AddChild(pitchersTitle);

		var pitchers = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 0.85f,
		};
		pitchers.AddThemeConstantOverride("separation", 6);
		pitchers.AddChild(BuildPitcherChip(homePitcher));
		pitchers.AddChild(BuildPitcherChip(awayPitcher));
		layout.AddChild(pitchers);

		layout.AddChild(MakeAction("VIEW MATCHUP PREVIEW", false, 1f, OpenMatchupPreview));
		return card;
	}

	private Control BuildTeamBlock(string name, string record, Texture2D? logo, string? initial, Color accent, string? teamId = null)
	{
		var box = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		box.AddThemeConstantOverride("separation", 6);
		if (logo != null)
		{
			box.AddChild(MakeShowcaseLogo(logo, 128));
		}
		else
		{
			box.AddChild(BuildTinyLogo(initial ?? "?", accent, 96));
		}

		var nameLabel = MakeText(name, _bold, 16, TextPrimary, HorizontalAlignment.Center);
		nameLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		box.AddChild(nameLabel);
		box.AddChild(MakeText(record, _medium, 11, TextMuted, HorizontalAlignment.Center));
		if (!string.IsNullOrEmpty(teamId))
		{
			SchoolLinks.MakeClickable(box, teamId);
		}

		return box;
	}

	private Control BuildPitcherChip(PitcherCard pitcher)
	{
		var chip = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		chip.AddThemeStyleboxOverride("panel", MakeInnerCard(8, 8));
		PlayerLinks.MakeClickable(chip, pitcher.TeamId, pitcher.PlayerName);

		var box = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		box.AddThemeConstantOverride("separation", 3);
		chip.AddChild(box);

		box.AddChild(MakeText(pitcher.Hand, _semibold, 9, pitcher.Accent, HorizontalAlignment.Center));
		box.AddChild(BuildPortrait(pitcher.Portrait, pitcher.LastName[..1], 44));
		box.AddChild(MakeText(pitcher.LastName, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
		box.AddChild(MakeText(pitcher.FirstName, _medium, 10, TextMuted, HorizontalAlignment.Center));

		var stats = new HBoxContainer();
		stats.AddThemeConstantOverride("separation", 0);
		stats.AddChild(BuildMiniStat(pitcher.Record, "W-L"));
		stats.AddChild(BuildMiniStat(pitcher.Era, "ERA"));
		stats.AddChild(BuildMiniStat(pitcher.Strikeouts, "K"));
		box.AddChild(stats);
		return chip;
	}

	private Control BuildMiniStat(string value, string label)
	{
		var col = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		col.AddThemeConstantOverride("separation", -2);
		col.AddChild(MakeText(value, _bold, 13, TextPrimary, HorizontalAlignment.Center));
		col.AddChild(MakeText(label, _medium, 8, TextMuted, HorizontalAlignment.Center));
		return col;
	}

	private Control BuildMetaRow(Texture2D icon, string text)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		row.AddChild(MakeIcon(icon, 12));
		row.AddChild(MakeText(text, _semibold, 11, TextPrimary, HorizontalAlignment.Left));
		return row;
	}

	private Control BuildPortrait(Texture2D? texture, string initial, float size)
	{
		var wrap = new Control
		{
			CustomMinimumSize = new Vector2(size, size),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};

		if (texture != null)
		{
			var portrait = new TextureRect
			{
				Texture = texture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				Material = _circleMaterial,
				CustomMinimumSize = new Vector2(size, size),
			};
			portrait.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			wrap.AddChild(portrait);
			return wrap;
		}

		wrap.AddChild(BuildTinyLogo(initial, Accent, size));
		return wrap;
	}

	private Control BuildTinyLogo(string initial, Color accent, float size = 32)
	{
		int radius = Mathf.RoundToInt(size / 2f);
		var fallback = new PanelContainer
		{
			CustomMinimumSize = new Vector2(size, size),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
		};
		fallback.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = HoverBg,
			BorderColor = accent,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerDetail = 8,
		});
		var label = MakeText(initial, _bold, Mathf.RoundToInt(size * 0.34f), accent, HorizontalAlignment.Center);
		label.VerticalAlignment = VerticalAlignment.Center;
		fallback.AddChild(label);
		return fallback;
	}

	private static TextureRect MakeShowcaseLogo(Texture2D texture, float minSize)
	{
		return new TextureRect
		{
			Texture = texture,
			CustomMinimumSize = new Vector2(minSize, minSize),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
	}

	private TextureRect MakeIcon(Texture2D texture, float size)
	{
		return new TextureRect
		{
			Texture = texture,
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = TextureFilterEnum.LinearWithMipmaps,
			Modulate = new Color(0.9f, 0.92f, 0.95f, 0.92f),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
	}

	private PanelContainer MakeCard()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
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
			ContentMarginLeft = 10,
			ContentMarginTop = 8,
			ContentMarginRight = 10,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		});
		return card;
	}

	private static Color BoardSatisfactionColor(BoardSatisfaction satisfaction) => satisfaction switch
	{
		BoardSatisfaction.Poor => Urgent,
		BoardSatisfaction.Fair => new Color(0.95f, 0.55f, 0.22f, 1f),
		BoardSatisfaction.Good => new Color(0.55f, 0.82f, 0.32f, 1f),
		BoardSatisfaction.Excellent => Green,
		_ => Accent,
	};

	private static Label MakeText(string text, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = align,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static ColorRect MakeAccentLine()
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 2),
			Color = Accent,
		};
	}

	private static ColorRect MakeHairline()
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 1),
			Color = Hairline,
		};
	}

	private static StyleBoxFlat MakeInnerCard(float padX, float padY)
	{
		return new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = Hairline,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = padX,
			ContentMarginTop = padY,
			ContentMarginRight = padX,
			ContentMarginBottom = padY,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		};
	}

	private static StyleBoxFlat MakeFilledButton(Color bg)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};
	}

	private static StyleBoxFlat MakeOutlinedButton(Color bg, Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};
	}

	private sealed record TickerItem(string Headline, bool Unread, Color Accent, Action Open);

	private sealed record WeekBind(PanelContainer Card, DateOnly Date, TeamLevel? Level);

	private sealed record PitcherCard(
		string LastName,
		string FirstName,
		string Hand,
		string Record,
		string Era,
		string Strikeouts,
		Texture2D? Portrait,
		Color Accent,
		string TeamId,
		string PlayerName);
}
