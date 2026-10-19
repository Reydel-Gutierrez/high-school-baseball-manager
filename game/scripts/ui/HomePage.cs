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
	private Texture2D _iconOff = null!;
	private Texture2D _iconPractice = null!;
	private Texture2D _iconStar = null!;
	private Texture2D _iconCalendar = null!;
	private Texture2D _iconPin = null!;
	private Texture2D _iconClock = null!;

	private Label _title = null!;
	private Label _subtitle = null!;
	private Label _seasonKicker = null!;
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
	private SimulationRecapOverlay _recapOverlay = null!;
	private ProgramMark _programMark = null!;
	private Label _weekRangeLabel = null!;
	private Label _milestoneLabel = null!;
	private HBoxContainer _weekDaysHost = null!;
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

		if (_recapOverlay != null)
		{
			_recapOverlay.Closed -= PresentPendingCelebration;
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
			ApplyActiveTeam();
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
		SchoolSnapshot school = SchoolProfiles.For(
			_session.CatalogOrganizationId,
			varsity ? TeamLevel.Varsity : TeamLevel.JuniorVarsity,
			_session);
		_recordValue.Text = school.RecordLabel;
		_districtValue.Text = Ordinal.Format(school.DistrictPlace);
		_districtValue.AddThemeColorOverride("font_color", school.DistrictPlace <= 2 ? UiKit.Green : Colors.White);
		_boardValue.Text = _session.BoardSatisfactionScore.ToString(CultureInfo.InvariantCulture);
		_boardValue.AddThemeColorOverride("font_color", BoardSatisfactionColor(_session.BoardSatisfaction));
		_programMark?.Refresh();
		RefreshHero(school, varsity);

		RebuildWeek();
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
		margin.AddThemeConstantOverride("margin_left", 22);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_right", 22);
		margin.AddThemeConstantOverride("margin_bottom", 16);
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
		_recapOverlay = new SimulationRecapOverlay();
		_recapOverlay.Closed += PresentPendingCelebration;
		AddChild(_recapOverlay);

		var layout = new VBoxContainer();
		margin.AddChild(layout);
		BuildDashboard(layout);

		// Kept off-screen: the shell's CONTINUE reads its label, and the
		// ticker/job-offer plumbing still feeds the overlays.
		_advanceButton = new Button { Visible = false, Text = "ADVANCE DAY" };
		AddChild(_advanceButton);
		_jobOfferBanner = new Control { Visible = false };
		_newsTickerSlot = new HBoxContainer { Visible = false };
		_newsTicker = BuildNewsTicker();
		_newsTickerSlot.AddChild(_newsTicker);
		AddChild(_newsTickerSlot);
	}

	private void RebuildWeek()
	{
		if (_weekDaysHost == null)
		{
			return;
		}

		DateOnly sunday = WeekStart(_session.CurrentDate);
		DateOnly saturday = sunday.AddDays(6);
		_weekRangeLabel.Text =
			$"{sunday.ToString("MMM d", CultureInfo.InvariantCulture)} – {saturday.ToString("MMM d", CultureInfo.InvariantCulture)}";
		(string name, string when, Color color) milestone = NextMilestone();
		_milestoneLabel.Text = $"Next    {milestone.name}  ·  {milestone.when}";
		_milestoneLabel.AddThemeColorOverride("font_color", milestone.color);
		RebuildWeekDays();
		RefreshWeekSummary();
	}

	private void RebuildWeekDays()
	{
		foreach (Node child in _weekDaysHost.GetChildren())
		{
			_weekDaysHost.RemoveChild(child);
			child.QueueFree();
		}

		_weekBinds.Clear();
		DateOnly sunday = WeekStart(_session.CurrentDate);
		for (int i = 0; i < 7; i++)
		{
			_weekDaysHost.AddChild(BuildDayTile(sunday.AddDays(i)));
		}
	}

	/// <summary>Label for the shell's gold CONTINUE button (PLAY, TRYOUTS, ADVANCE DAY…).</summary>
	public string ContinueLabel => _advanceButton?.Text ?? "ADVANCE DAY";

	/// <summary>The shell's CONTINUE: same as the old Advance Day button.</summary>
	public void Continue() => OnAdvancePressed();

	public void RefreshContinue() => RefreshAdvanceButton();

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

		if (SeasonRolloverDesk.PendingOn(_session))
		{
			_advanceButton.Text = "NEW YEAR";
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
		else if (_recapOverlay != null && _recapOverlay.Visible && ev.IsActionPressed("ui_cancel"))
		{
			UiSounds.Play(UiSound.Back);
			_recapOverlay.Close();
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
		bool selected = date == _selectedDate && !today;
		bool past = date < _session.CurrentDate;
		Color bg = today ? UiKit.Gold : hovered ? new Color("#1d2230") : UiKit.Selected;
		cell.AddThemeStyleboxOverride("panel", UiKit.Box(bg, 10, 4, 6, selected ? UiKit.Text : null, selected ? 1 : 0));
		cell.Modulate = new Color(1f, 1f, 1f, past && !hovered ? 0.55f : 1f);
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
		if (item.Id is "class-reveal" or "winter-workouts" or "tryout-week" or "tryout-decisions")
		{
			box.AddChild(MakeEventLink("OPEN TRYOUTS", OpenTryouts));
		}

		if (item.Id is "incoming-class")
		{
			box.AddChild(MakeEventLink("VIEW ROSTER", OpenIncomingClass));
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

		if (item.Id is "new-year" or "winter-break-open")
		{
			if (SeasonRolloverDesk.PendingOn(_session))
			{
				box.AddChild(MakeEventLink("VIEW YEAR IN REVIEW", OpenSeasonRollover));
			}
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

	private void OpenSeasonRollover()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowSeasonRollover();
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
			|| (_simulationOverlay != null && _simulationOverlay.Visible)
			|| (_recapOverlay != null && _recapOverlay.Visible))
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

		if (SeasonRolloverDesk.PendingOn(_session))
		{
			OpenSeasonRollover();
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
		SeasonEvent? next = CareerCalendar.NextImportant(GameSession.SeasonYear, today);
		if (next == null)
		{
			return ("NONE", string.Empty, TextMuted);
		}

		int days = Math.Max(0, next.Start.DayNumber - today.DayNumber);
		string when = next.OccursOn(today)
			? next.Start == next.End ? "TODAY" : "IN PROGRESS"
			: days == 1 ? "1 DAY" : $"{days} DAYS";
		Color color = days < 8 ? Urgent : TextPrimary;
		return (next.Name.ToUpperInvariant(), when, color);
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

		if (SeasonRolloverDesk.PendingOn(_session))
		{
			OpenSeasonRollover();
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
		PresentSimulationRun(run);
	}

	private void OnSimulateUntilPressed()
	{
		SimulationRun run = _session.SimulateUntilNextEvent();
		PresentSimulationRun(run);
		if (run.Interrupt != null)
		{
			return;
		}

		CalendarContest? game = PrimaryGame(_session.ContestsOn(_session.CurrentDate));
		if (game != null && !game.Completed)
		{
			OpenGamePreview(game);
		}
	}

	public void ApplySimulationRun(SimulationRun run) => PresentSimulationRun(run);

	private void PresentSimulationRun(SimulationRun run)
	{
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

		if (GameSettings.ShowSimulationRecap && run.HasRecap && !SimulationDesk.HasCeremony(_session))
		{
			_recapOverlay.Present(run.Recap!);
		}
	}

	private void OpenInterrupt(AdvanceDayInterrupt interrupt)
	{
		switch (interrupt.Kind)
		{
			case AdvanceDayInterruptKind.TryoutDecision:
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

		foreach (CalendarContest game in _session.SeasonContests(_session.ActiveTeam.Level))
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

		_contestHost.AddChild(BuildUpcomingContest());
	}

	private Control BuildUpcomingContest()
	{
		CalendarContest? game = PreviewContest();
		return game == null ? BuildNoGameCard() : BuildNextGameCard(game);
	}

	private Control BuildEmptyContest()
	{
		var card = MakeCard();
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		var layout = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		layout.AddThemeConstantOverride("separation", 8);
		card.AddChild(layout);
		layout.AddChild(MakeText("SCHEDULE", _medium, 10, Accent, HorizontalAlignment.Left));
		layout.AddChild(MakeText("NO CONTEST SCHEDULED", _semibold, 16, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(MakeAccentLine());
		var body = MakeText(
			"Nothing on the books for this squad yet. Check the season schedule or advance the calendar.",
			_medium,
			14,
			new Color(0.78f, 0.81f, 0.86f),
			HorizontalAlignment.Left);
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(body);
		return card;
	}

	private static string ContestKicker(CalendarContest game)
	{
		if (game.Completed)
		{
			return "LAST CONTEST";
		}

		DateOnly today = GameSession.Current.CurrentDate;
		if (game.Date == today)
		{
			return game.Level == TeamLevel.JuniorVarsity ? "JV TODAY" : "TODAY";
		}

		if (game.Date == today.AddDays(1))
		{
			return game.Level == TeamLevel.JuniorVarsity ? "JV TOMORROW" : "TOMORROW";
		}

		return game.Level == TeamLevel.JuniorVarsity ? "NEXT JV CONTEST" : "NEXT CONTEST";
	}

	private PitcherCard ToPitcher(GamePreviewStarter starter, Color accent, string teamId)
	{
		string[] parts = starter.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		string last = parts.Length == 0 ? "TBD" : parts[^1];
		string first = parts.Length <= 1 ? starter.Name : string.Join(" ", parts[..^1]);
		Texture2D? portrait = null;
		if (!string.IsNullOrEmpty(starter.PortraitPath) && ResourceLoader.Exists(starter.PortraitPath))
		{
			portrait = GD.Load<Texture2D>(starter.PortraitPath);
		}

		string strikeouts = "0";
		HubSquadPlayer? player = DistrictHubSquads.FindPlayer(teamId, starter.Name, _session.ActiveTeam.Level);
		if (player?.CurrentPitching != null)
		{
			strikeouts = player.CurrentPitching.Strikeouts.ToString(CultureInfo.InvariantCulture);
		}

		return new PitcherCard(
			last.ToUpperInvariant(),
			first,
			starter.Hand,
			starter.Record,
			starter.Era,
			strikeouts,
			portrait,
			accent,
			teamId,
			starter.Name);
	}

	private Control BuildVarsityContest()
	{
		return BuildUpcomingContest();
	}

	private Control BuildJvContest()
	{
		return BuildUpcomingContest();
	}

	/// <summary>
	/// Probable starter as a mini broadcast banner: team-color backdrop with
	/// the jersey number, the capped headshot standing on the trim stripe,
	/// name and line on the right.
	/// </summary>
	private Control BuildPitcherChip(PitcherCard pitcher, string kicker = "")
	{
		(Color primary, Color secondary) = TeamColors.For(pitcher.TeamId);
		Player? player = string.IsNullOrWhiteSpace(pitcher.TeamId)
			? null
			: _session.FindWorldPlayer(pitcher.TeamId, pitcher.PlayerName, _session.ActiveTeam.Level);
		var chip = new BroadcastPanel
		{
			Primary = primary,
			Secondary = secondary,
			Background = TeamBrands.Banner(pitcher.TeamId),
			Number = player is { JerseyNumber: > 0 } ? player.JerseyNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) : "",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 118),
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		PlayerLinks.MakeClickable(chip, pitcher.TeamId, pitcher.PlayerName);

		// Headshot: bottom-left, full height, standing on the stripe.
		PersonPortrait? face = PlayerFaces.For(pitcher.TeamId, pitcher.PlayerName, 120, 150, circle: false, level: _session.ActiveTeam.Level);
		if (face != null)
		{
			face.Backdrop = false;
			face.AnchorLeft = 0;
			face.AnchorRight = 0.46f;
			face.AnchorTop = 0;
			face.AnchorBottom = 1;
			face.OffsetLeft = 2;
			face.OffsetTop = 8;
			face.OffsetRight = 0;
			face.OffsetBottom = -3;
			face.CustomMinimumSize = Vector2.Zero;
			chip.AddChild(face);
		}
		else
		{
			Control fallback = BuildPortrait(pitcher.Portrait, pitcher.LastName[..1], 64);
			fallback.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterLeft);
			fallback.OffsetLeft = 18;
			chip.AddChild(fallback);
		}

		var info = new VBoxContainer
		{
			AnchorLeft = 0.46f,
			AnchorRight = 1,
			AnchorTop = 0,
			AnchorBottom = 1,
			OffsetLeft = 6,
			OffsetTop = 10,
			OffsetRight = -10,
			OffsetBottom = -10,
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		info.AddThemeConstantOverride("separation", 0);
		chip.AddChild(info);

		var hand = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Ignore };
		hand.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0, 0, 0, 0.35f),
			ContentMarginLeft = 6,
			ContentMarginRight = 6,
			ContentMarginTop = 1,
			ContentMarginBottom = 1,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
		});
		hand.AddChild(MakeText(string.IsNullOrEmpty(kicker) ? pitcher.Hand : $"{kicker}  ·  {pitcher.Hand}", _semibold, 10, UiKit.Gold, HorizontalAlignment.Left));
		info.AddChild(hand);
		info.AddChild(MakeText(pitcher.FirstName.ToUpperInvariant(), _semibold, 11, new Color(1, 1, 1, 0.8f), HorizontalAlignment.Left));
		var last = MakeText(pitcher.LastName.ToUpperInvariant(), _bold, 22, Colors.White, HorizontalAlignment.Left);
		last.ClipText = true;
		info.AddChild(last);
		info.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), MouseFilter = MouseFilterEnum.Ignore });

		var stats = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		stats.AddThemeConstantOverride("separation", 4);
		stats.AddChild(BuildBannerStat(pitcher.Record, "W-L"));
		stats.AddChild(BuildBannerStat(pitcher.Era, "ERA"));
		stats.AddChild(BuildBannerStat(pitcher.Strikeouts, "K"));
		info.AddChild(stats);
		return chip;
	}

	private Control BuildBannerStat(string value, string label)
	{
		var cell = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
		cell.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0, 0, 0, 0.32f),
			ContentMarginTop = 3,
			ContentMarginBottom = 3,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
		});
		var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", -2);
		col.AddChild(MakeText(value, _bold, 15, Colors.White, HorizontalAlignment.Center));
		col.AddChild(MakeText(label, _medium, 9, new Color(1, 1, 1, 0.6f), HorizontalAlignment.Center));
		cell.AddChild(col);
		return cell;
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
