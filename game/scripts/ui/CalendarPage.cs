using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// Monthly baseball-year calendar: important dates, populated games,
/// and a day desk that explains what is on the selected date.
/// </summary>
public partial class CalendarPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Important = new(0.45f, 0.72f, 0.88f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly string[] Weekdays = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _cascadeLogo = null!;
	private Texture2D _iconCalendar = null!;
	private Texture2D _iconStar = null!;
	private Texture2D _iconPin = null!;
	private Texture2D _iconClock = null!;

	private Label _subtitle = null!;
	private Label _monthTitle = null!;
	private Control _gridHost = null!;
	private ScrollContainer _detailScroll = null!;
	private VBoxContainer _detailHost = null!;
	private VBoxContainer _profileHost = null!;
	private Button _prevButton = null!;
	private Button _nextButton = null!;

	private BoxScoreOverlay _boxScoreOverlay = null!;
	private int _viewYear;
	private int _viewMonth;
	private DateOnly _selected;
	private readonly Dictionary<DateOnly, PanelContainer> _cells = new();

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_cascadeLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
		_iconCalendar = UiSvg.Load("res://assets/ui/icon_calendar.svg");
		_iconStar = UiSvg.Load("res://assets/ui/icon_schedule_star.svg");
		_iconPin = UiSvg.Load("res://assets/ui/icon_pin.svg");
		_iconClock = UiSvg.Load("res://assets/ui/icon_clock.svg");
		_session = GetNode<GameSession>("/root/GameSession");

		_selected = _session.CurrentDate;
		_viewYear = _selected.Year;
		_viewMonth = _selected.Month;

		BuildPage();
		_boxScoreOverlay = new BoxScoreOverlay();
		AddChild(_boxScoreOverlay);
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		ApplyActiveTeam();
		RebuildMonth();
		BindDetail();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
		}
	}

	public void FocusCurrentDate()
	{
		SelectDate(_session.CurrentDate, jumpMonth: true);
	}

	public void ShowSchool(string teamId)
	{
		ShowProfileCard(schoolId: SchoolProfiles.NormalizeId(teamId), playerTeamId: null, playerName: null);
	}

	public void ShowPlayer(string teamId, string playerName)
	{
		string id = string.IsNullOrWhiteSpace(teamId) ? string.Empty : SchoolProfiles.NormalizeId(teamId);
		ShowProfileCard(schoolId: null, playerTeamId: id, playerName: playerName);
	}

	private void ShowProfileCard(string? schoolId, string? playerTeamId, string? playerName)
	{
		ClearProfileHost();
		_detailScroll.Visible = false;
		_profileHost.Visible = true;

		if (!string.IsNullOrEmpty(playerName))
		{
			var card = new PlayerProfileCard
			{
				FrameVisible = false,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill,
			};
			card.SetCloseHandler(CloseProfile);
			_profileHost.AddChild(card);
			PlayerProfileSnapshot? snapshot = PlayerProfiles.Find(
				playerTeamId ?? string.Empty,
				playerName,
				_session.ActiveTeam.Level);
			if (snapshot != null)
			{
				card.Bind(snapshot);
			}

			return;
		}

		var school = new SchoolProfileCard
		{
			FrameVisible = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		school.SetCloseHandler(CloseProfile);
		_profileHost.AddChild(school);
		school.Bind(schoolId ?? DistrictHubData.UserTeamId, _session.ActiveTeam.Level);
	}

	private void CloseProfile()
	{
		ClearProfileHost();
		_profileHost.Visible = false;
		_detailScroll.Visible = true;
	}

	private void ClearProfileHost()
	{
		while (_profileHost.GetChildCount() > 0)
		{
			Node child = _profileHost.GetChild(0);
			_profileHost.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void OnDeskTabPicked(string key)
	{
		if (key != SeasonDeskTabs.Schedule)
		{
			return;
		}

		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowSchedule();
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => ApplyActiveTeam();

	private void ApplyActiveTeam()
	{
		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		_subtitle.Text = varsity
			? $"VARSITY  ·  {_session.Organization.Name.ToUpperInvariant()}  ·  {_session.CurrentDate.ToString("dddd, MMMM d", CultureInfo.InvariantCulture).ToUpperInvariant()}"
			: $"JV  ·  {_session.Organization.Name.ToUpperInvariant()}  ·  {_session.CurrentDate.ToString("dddd, MMMM d", CultureInfo.InvariantCulture).ToUpperInvariant()}";
	}

	private void BuildPage()
	{
		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 6);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_right", 6);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		AddChild(margin);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		margin.AddChild(layout);
		layout.AddChild(BuildHeader());
		layout.AddChild(SeasonDeskTabs.Build(_semibold, SeasonDeskTabs.Calendar, OnDeskTabPicked));

		var body = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 8);
		layout.AddChild(body);

		var monthCard = MakeCard(8, 6);
		monthCard.ClipContents = true;
		monthCard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		monthCard.SizeFlagsVertical = SizeFlags.ExpandFill;
		monthCard.SizeFlagsStretchRatio = 1.7f;
		body.AddChild(monthCard);

		var monthLayout = new VBoxContainer();
		monthLayout.AddThemeConstantOverride("separation", 6);
		monthCard.AddChild(monthLayout);
		monthLayout.AddChild(BuildMonthHeader());
		monthLayout.AddChild(MakeAccentLine());
		monthLayout.AddChild(BuildWeekdayRow());

		_gridHost = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_gridHost.Resized += LayoutGrid;
		monthLayout.AddChild(_gridHost);
		monthLayout.AddChild(BuildLegend());

		var detailCard = MakeCard(12, 10);
		detailCard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		detailCard.SizeFlagsVertical = SizeFlags.ExpandFill;
		body.AddChild(detailCard);

		var stack = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		detailCard.AddChild(stack);

		_detailScroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_detailScroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		stack.AddChild(_detailScroll);

		_detailHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_detailHost.AddThemeConstantOverride("separation", 8);
		_detailScroll.AddChild(_detailHost);

		_profileHost = new VBoxContainer
		{
			Visible = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_profileHost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_profileHost.AddThemeConstantOverride("separation", 0);
		stack.AddChild(_profileHost);
	}

	private Control BuildHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);
		header.AddChild(TeamLogos.MakeHeaderMark(_cascadeLogo, 52));
		header.AddChild(TeamLogos.MakeHeaderRule(Hairline));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -2);
		identity.AddChild(MakeText($"HOME  ·  {GameSession.SeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("CALENDAR", _bold, 26, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("VARSITY  ·  GREAT LAKES DISTRICT", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);
		return header;
	}

	private Control BuildMonthHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);

		_prevButton = MakeChromeButton("<", ShiftMonth, -1);
		header.AddChild(_prevButton);

		_monthTitle = MakeText("APRIL 2026", _bold, 18, TextPrimary, HorizontalAlignment.Center);
		_monthTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(_monthTitle);

		_nextButton = MakeChromeButton(">", ShiftMonth, 1);
		header.AddChild(_nextButton);
		header.AddChild(MakeChromeButton("TODAY", _ => FocusCurrentDate(), 0, wide: true));
		return header;
	}

	private Button MakeChromeButton(string text, Action<int> pressed, int delta, bool wide = false)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(wide ? 72 : 32, 28),
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 13);
		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, CardBorder));
		button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("focus", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("disabled", MakeOutlinedButton(CardInner, Hairline));
		button.Pressed += () => pressed(delta);
		return button;
	}

	private Control BuildWeekdayRow()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		foreach (string day in Weekdays)
		{
			var label = MakeText(day, _semibold, 11, TextMuted, HorizontalAlignment.Center);
			label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			row.AddChild(label);
		}

		return row;
	}

	private Control BuildLegend()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 14);
		row.AddChild(MakeLegendItem(_iconStar, Important, "IMPORTANT DATE"));
		row.AddChild(MakeLegendItem(null, Green, "GAME"));
		row.AddChild(MakeText("CLICK A DATE TO SEE WHAT IS ON THE DESK", _medium, 11, TextMuted, HorizontalAlignment.Left));
		return row;
	}

	private Control MakeLegendItem(Texture2D? icon, Color color, string text)
	{
		var item = new HBoxContainer();
		item.AddThemeConstantOverride("separation", 6);
		if (icon != null)
		{
			item.AddChild(MakeIcon(icon, 12, color));
		}
		else
		{
			item.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(8, 8),
				Color = color,
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
			});
		}

		item.AddChild(MakeText(text, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		return item;
	}

	private void ShiftMonth(int delta)
	{
		var cursor = new DateOnly(_viewYear, _viewMonth, 1).AddMonths(delta);
		if (!CanView(cursor.Year, cursor.Month))
		{
			return;
		}

		_viewYear = cursor.Year;
		_viewMonth = cursor.Month;
		int day = Math.Min(_selected.Day, DateTime.DaysInMonth(_viewYear, _viewMonth));
		_selected = new DateOnly(_viewYear, _viewMonth, day);
		RebuildMonth();
		BindDetail();
	}

	private bool CanView(int year, int month)
	{
		int seasonYear = GameSession.SeasonYear;
		if (year < seasonYear)
		{
			return false;
		}

		if (year == seasonYear)
		{
			return true;
		}

		return year == seasonYear + 1 && month == 1;
	}

	private void RebuildMonth()
	{
		foreach (Node child in _gridHost.GetChildren())
		{
			_gridHost.RemoveChild(child);
			child.QueueFree();
		}

		_cells.Clear();
		_monthTitle.Text = new DateOnly(_viewYear, _viewMonth, 1)
			.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
			.ToUpperInvariant();

		var first = new DateOnly(_viewYear, _viewMonth, 1);
		int startOffset = (int)first.DayOfWeek;
		DateOnly cursor = first.AddDays(-startOffset);

		for (int i = 0; i < 42; i++)
		{
			_gridHost.AddChild(BuildDayCell(cursor));
			cursor = cursor.AddDays(1);
		}

		DateOnly previous = first.AddMonths(-1);
		DateOnly next = first.AddMonths(1);
		_prevButton.Disabled = !CanView(previous.Year, previous.Month);
		_nextButton.Disabled = !CanView(next.Year, next.Month);
		LayoutGrid();
		Callable.From(LayoutGrid).CallDeferred();
	}

	private void LayoutGrid()
	{
		if (_gridHost == null || _gridHost.GetChildCount() == 0)
		{
			return;
		}

		Vector2 size = _gridHost.Size;
		if (size.X < 8 || size.Y < 8)
		{
			return;
		}

		const int columns = 7;
		const int rows = 6;
		const float gap = 3f;
		float cellWidth = (size.X - gap * (columns - 1)) / columns;
		float cellHeight = (size.Y - gap * (rows - 1)) / rows;

		for (int i = 0; i < _gridHost.GetChildCount(); i++)
		{
			if (_gridHost.GetChild(i) is not Control cell)
			{
				continue;
			}

			int column = i % columns;
			int row = i / columns;
			cell.AnchorLeft = 0;
			cell.AnchorTop = 0;
			cell.AnchorRight = 0;
			cell.AnchorBottom = 0;
			cell.Position = new Vector2(column * (cellWidth + gap), row * (cellHeight + gap));
			cell.Size = new Vector2(cellWidth, cellHeight);
		}
	}

	private Control BuildDayCell(DateOnly date)
	{
		bool inMonth = date.Month == _viewMonth && date.Year == _viewYear;
		IReadOnlyList<SeasonEvent> events = inMonth ? SeasonCalendar.On(GameSession.SeasonYear, date) : [];
		IReadOnlyList<CalendarContest> games = inMonth ? _session.ContestsOn(date) : [];
		bool hasEvent = events.Count > 0;
		bool featured = false;
		foreach (SeasonEvent item in events)
		{
			if (item.Featured)
			{
				featured = true;
				break;
			}
		}

		var cell = new PanelContainer
		{
			ClipContents = true,
			MouseDefaultCursorShape = inMonth ? CursorShape.PointingHand : CursorShape.Arrow,
		};
		ApplyCellStyle(cell, date);
		if (inMonth)
		{
			_cells[date] = cell;
			DateOnly captured = date;
			cell.GuiInput += ev => OnCellInput(ev, captured);
			cell.MouseEntered += () => ApplyCellStyle(cell, captured, hovered: true);
			cell.MouseExited += () => ApplyCellStyle(cell, captured);
		}

		var root = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		cell.AddChild(root);

		CalendarContest? logoGame = FindLogoGame(games);
		if (logoGame != null)
		{
			root.AddChild(MakeCellLogo(logoGame.OpponentId));
		}

		var overlay = new MarginContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorRight = 1f,
			AnchorBottom = 1f,
		};
		overlay.AddThemeConstantOverride("margin_left", 4);
		overlay.AddThemeConstantOverride("margin_top", 4);
		overlay.AddThemeConstantOverride("margin_right", 4);
		overlay.AddThemeConstantOverride("margin_bottom", 4);
		root.AddChild(overlay);

		var stack = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		stack.AddThemeConstantOverride("separation", 2);
		overlay.AddChild(stack);

		var top = new HBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		top.AddThemeConstantOverride("separation", 4);
		var dayLabel = MakeText(date.Day.ToString(CultureInfo.InvariantCulture), _bold, 13, TextPrimary, HorizontalAlignment.Left);
		dayLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		top.AddChild(dayLabel);
		if (hasEvent)
		{
			top.AddChild(MakeIcon(featured ? _iconStar : _iconCalendar, 12, Important));
		}

		stack.AddChild(top);

		if (logoGame == null)
		{
			int shown = 0;
			foreach (CalendarContest game in games)
			{
				if (shown >= 2)
				{
					break;
				}

				string mark = game.IsTryout ? "TRY" : ClubhouseCalendar.OpponentMark(game.OpponentId);
				string level = game.IsTryout ? "CAMP" : game.Level == TeamLevel.Varsity ? "V" : "JV";
				Color color = game.IsTryout ? Accent : game.Completed ? (game.Won ? Green : Urgent) : (game.Home ? Green : Accent);
				var line = MakeText($"{level} {mark}", _semibold, 10, color, HorizontalAlignment.Left);
				stack.AddChild(line);
				shown++;
			}
		}
		else
		{
			stack.AddChild(new Control
			{
				SizeFlagsVertical = SizeFlags.ExpandFill,
				MouseFilter = MouseFilterEnum.Ignore,
			});
			if (logoGame.Completed)
			{
				stack.AddChild(MakeResultMark(logoGame));
			}
		}

		if (hasEvent && games.Count == 0)
		{
			SeasonEvent labelEvent = events[0];
			foreach (SeasonEvent item in events)
			{
				if (item.Featured || item.Start == item.End)
				{
					labelEvent = item;
					break;
				}
			}

			int span = labelEvent.End.DayNumber - labelEvent.Start.DayNumber;
			if (labelEvent.Featured || span <= 14)
			{
				var name = MakeText(labelEvent.Name.ToUpperInvariant(), _medium, 9, Accent, HorizontalAlignment.Left);
				name.AutowrapMode = TextServer.AutowrapMode.Word;
				name.ClipText = true;
				stack.AddChild(name);
			}
		}

		return cell;
	}

	private CalendarContest? FindLogoGame(IReadOnlyList<CalendarContest> games)
	{
		CalendarContest? fallback = null;
		foreach (CalendarContest game in games)
		{
			if (game.IsTryout || TeamLogos.Load(game.OpponentId) == null)
			{
				continue;
			}

			if (MatchesActiveLevel(game.Level))
			{
				return game;
			}

			fallback ??= game;
		}

		return fallback;
	}

	private static TextureRect MakeCellLogo(string opponentId)
	{
		return new TextureRect
		{
			Texture = TeamLogos.Load(opponentId),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorRight = 1f,
			AnchorBottom = 1f,
			OffsetLeft = 2,
			OffsetTop = 2,
			OffsetRight = -2,
			OffsetBottom = -2,
		};
	}

	private Control MakeResultMark(CalendarContest game)
	{
		string text = game.Won ? "W" : game.TeamScore == game.OpponentScore ? "T" : "L";
		Color color = text == "W" ? Green : text == "L" ? Urgent : Accent;
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.End,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		var badge = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		badge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.055f, 0.07f, 0.82f),
			BorderColor = color,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 5,
			ContentMarginTop = 1,
			ContentMarginRight = 5,
			ContentMarginBottom = 1,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		badge.AddChild(MakeText(text, _bold, 13, color, HorizontalAlignment.Center));
		row.AddChild(badge);
		return row;
	}

	private void OnCellInput(InputEvent ev, DateOnly date)
	{
		if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
		{
			SelectDate(date, jumpMonth: false);
		}
	}

	private void SelectDate(DateOnly date, bool jumpMonth)
	{
		if (jumpMonth && (date.Year != _viewYear || date.Month != _viewMonth))
		{
			if (!CanView(date.Year, date.Month))
			{
				return;
			}

			_viewYear = date.Year;
			_viewMonth = date.Month;
			_selected = date;
			RebuildMonth();
			BindDetail();
			return;
		}

		_selected = date;
		foreach (KeyValuePair<DateOnly, PanelContainer> pair in _cells)
		{
			ApplyCellStyle(pair.Value, pair.Key);
		}

		BindDetail();
	}

	private void ApplyCellStyle(PanelContainer cell, DateOnly date, bool hovered = false)
	{
		bool today = date == _session.CurrentDate;
		bool selected = date == _selected;
		bool past = date < _session.CurrentDate;
		bool inMonth = date.Month == _viewMonth && date.Year == _viewYear;
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
			ContentMarginLeft = 3,
			ContentMarginTop = 3,
			ContentMarginRight = 3,
			ContentMarginBottom = 3,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		});

		if (!inMonth)
		{
			cell.Modulate = new Color(1f, 1f, 1f, 0.34f);
		}
		else if (past)
		{
			cell.Modulate = new Color(1f, 1f, 1f, hovered || selected ? 0.72f : 0.52f);
		}
		else
		{
			cell.Modulate = Colors.White;
		}
	}

	private void BindDetail()
	{
		CloseProfile();
		foreach (Node child in _detailHost.GetChildren())
		{
			_detailHost.RemoveChild(child);
			child.QueueFree();
		}

		IReadOnlyList<SeasonEvent> events = SeasonCalendar.On(GameSession.SeasonYear, _selected);
		IReadOnlyList<CalendarContest> games = _session.ContestsOn(_selected);
		bool today = _selected == _session.CurrentDate;

		_detailHost.AddChild(MakeText(today ? "TODAY" : "SELECTED DATE", _medium, 10, Accent, HorizontalAlignment.Left));
		_detailHost.AddChild(MakeText(
			_selected.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture).ToUpperInvariant(),
			_bold,
			20,
			TextPrimary,
			HorizontalAlignment.Left));
		_detailHost.AddChild(MakeAccentLine());

		if (events.Count == 0 && games.Count == 0)
		{
			_detailHost.AddChild(MakeText("No program events or games on this date.", _medium, 13, TextMuted, HorizontalAlignment.Left));
		}

		if (events.Count > 0)
		{
			_detailHost.AddChild(MakeText("IMPORTANT DATES", _semibold, 11, TextMuted, HorizontalAlignment.Left));
			foreach (SeasonEvent item in events)
			{
				_detailHost.AddChild(BuildEventCard(item));
			}
		}

		if (games.Count > 0)
		{
			_detailHost.AddChild(MakeText("GAMES", _semibold, 11, TextMuted, HorizontalAlignment.Left));
			foreach (CalendarContest game in games)
			{
				_detailHost.AddChild(BuildGameCard(game));
			}
		}

		IReadOnlyList<SeasonEvent> monthEvents = SeasonCalendar.InMonth(GameSession.SeasonYear, _viewYear, _viewMonth);
		if (monthEvents.Count == 0)
		{
			return;
		}

		_detailHost.AddChild(MakeHairline());
		_detailHost.AddChild(MakeText("THIS MONTH", _semibold, 11, TextMuted, HorizontalAlignment.Left));
		foreach (SeasonEvent item in monthEvents)
		{
			_detailHost.AddChild(BuildMonthRow(item));
		}
	}

	private Control BuildEventCard(SeasonEvent item)
	{
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", MakeInnerCard(item.Featured));
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 4);
		card.AddChild(box);

		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 6);
		titleRow.AddChild(MakeIcon(item.Featured ? _iconStar : _iconCalendar, 14, Important));
		var title = MakeText(item.Name.ToUpperInvariant(), _bold, 14, item.Featured ? Accent : TextPrimary, HorizontalAlignment.Left);
		title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		title.AutowrapMode = TextServer.AutowrapMode.Word;
		titleRow.AddChild(title);
		box.AddChild(titleRow);

		box.AddChild(MakeText(item.FormatDates().ToUpperInvariant(), _semibold, 11, TextMuted, HorizontalAlignment.Left));
		var body = MakeText(item.Summary, _medium, 13, new Color(0.78f, 0.81f, 0.86f), HorizontalAlignment.Left);
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
				box.AddChild(MakeEventLink("READ JOB OFFER", OpenJobOffer));
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

	private void OpenJobMarket()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowCareerJobMarket();
		}
	}

	private void OpenJobOffer()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowJobOffer();
		}
	}

	private Control BuildGameCard(CalendarContest game)
	{
		HubTeam opponent = DistrictHubData.GetTeam(game.OpponentId);
		bool dim = !MatchesActiveLevel(game.Level);
		var card = new PanelContainer
		{
			Modulate = dim ? new Color(1f, 1f, 1f, 0.55f) : Colors.White,
		};
		card.AddThemeStyleboxOverride("panel", MakeInnerCard(false));

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		card.AddChild(box);

		var top = new HBoxContainer();
		top.AddThemeConstantOverride("separation", 8);
		Texture2D? logo = TeamLogos.Load(game.OpponentId);
		if (logo != null)
		{
			top.AddChild(TeamLogos.MakeIcon(game.OpponentId, 36));
		}

		var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		names.AddThemeConstantOverride("separation", -2);
		names.AddChild(MakeText(game.Exhibition ? "EXHIBITION" : game.Level.ToLabel().ToUpperInvariant(), _medium, 10, Accent, HorizontalAlignment.Left));
		names.AddChild(MakeText((game.Home ? "VS " : "@ ") + opponent.ShortName, _bold, 16, TextPrimary, HorizontalAlignment.Left));
		top.AddChild(names);

		var result = MakeText(game.ResultLabel, _bold, 14, game.Completed ? (game.Won ? Green : Urgent) : TextMuted, HorizontalAlignment.Right);
		result.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		top.AddChild(result);
		box.AddChild(top);

		var meta = new HBoxContainer();
		meta.AddThemeConstantOverride("separation", 12);
		meta.AddChild(BuildMeta(_iconClock, game.Time));
		meta.AddChild(BuildMeta(_iconPin, game.VenueLabel));
		box.AddChild(meta);
		SchoolLinks.MakeClickable(card, game.OpponentId);
		return card;
	}

	private Control BuildMonthRow(SeasonEvent item)
	{
		bool active = item.OccursOn(_selected);
		var row = new PanelContainer
		{
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = active ? new Color(0.956863f, 0.643137f, 0.109804f, 0.12f) : Colors.Transparent,
			ContentMarginLeft = 6,
			ContentMarginTop = 5,
			ContentMarginRight = 6,
			ContentMarginBottom = 5,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		row.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				SelectDate(item.OccursOn(_selected) ? _selected : ClampToMonth(item), jumpMonth: false);
			}
		};

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		columns.AddChild(MakeIcon(item.Featured ? _iconStar : _iconCalendar, 12, Important));
		var date = MakeText(item.FormatDates().ToUpperInvariant(), _semibold, 10, TextMuted, HorizontalAlignment.Left);
		date.CustomMinimumSize = new Vector2(110, 0);
		columns.AddChild(date);
		var name = MakeText(item.Name, _semibold, 12, active ? Accent : TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		name.AutowrapMode = TextServer.AutowrapMode.Word;
		columns.AddChild(name);
		row.AddChild(columns);
		return row;
	}

	private DateOnly ClampToMonth(SeasonEvent item)
	{
		var monthStart = new DateOnly(_viewYear, _viewMonth, 1);
		var monthEnd = monthStart.AddMonths(1).AddDays(-1);
		if (item.Start > monthStart)
		{
			return item.Start;
		}

		return monthStart <= item.End ? monthStart : item.End;
	}

	private bool MatchesActiveLevel(TeamLevel level) => _session.ActiveTeam.Level == level;

	private Control BuildMeta(Texture2D icon, string text)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		row.AddChild(MakeIcon(icon, 12, new Color(0.9f, 0.92f, 0.95f, 0.92f)));
		row.AddChild(MakeText(text, _semibold, 11, TextPrimary, HorizontalAlignment.Left));
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
			MouseFilter = MouseFilterEnum.Ignore,
		};
	}

	private PanelContainer MakeCard(float padX, float padY)
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
			ContentMarginLeft = padX,
			ContentMarginTop = padY,
			ContentMarginRight = padX,
			ContentMarginBottom = padY,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		});
		return card;
	}

	private static StyleBoxFlat MakeInnerCard(bool featured)
	{
		return new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = featured ? Accent : Hairline,
			BorderWidthLeft = featured ? 2 : 1,
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
			ContentMarginLeft = 10,
			ContentMarginTop = 6,
			ContentMarginRight = 10,
			ContentMarginBottom = 6,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};
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
}
