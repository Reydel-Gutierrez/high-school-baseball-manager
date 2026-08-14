using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// Coach's briefing: who the program is, what this week looks like, and
/// the next contest to prepare for.
/// </summary>
public partial class HomePage : Control
{
	private const int CurrentSeasonYear = 2026;

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

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private ShaderMaterial _circleMaterial = null!;
	private Texture2D _cascadeLogo = null!;
	private Texture2D _marini = null!;
	private Texture2D _tony = null!;
	private Texture2D _iconOff = null!;
	private Texture2D _iconPractice = null!;
	private Texture2D _iconStar = null!;
	private Texture2D _iconPin = null!;
	private Texture2D _iconClock = null!;

	private Label _title = null!;
	private Label _subtitle = null!;
	private Label _recordValue = null!;
	private Label _districtValue = null!;
	private VBoxContainer _contestHost = null!;
	private readonly List<PanelContainer> _dayCards = new();

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_circleMaterial = new ShaderMaterial
		{
			Shader = GD.Load<Shader>("res://assets/ui/circle_crop.gdshader"),
		};
		_cascadeLogo = GD.Load<Texture2D>("res://assets/teams/01_Cascade_Regional_High_School/Cascadelogo.png");
		_marini = GD.Load<Texture2D>("res://assets/teams/01_Cascade_Regional_High_School/Players/Cascade_player_01_Marini.png");
		_tony = GD.Load<Texture2D>("res://assets/teams/02_Great_Lakes_High_School/Players/Great_lakes_player_tony.png");
		_iconOff = GD.Load<Texture2D>("res://assets/ui/icon_off_day.svg");
		_iconPractice = GD.Load<Texture2D>("res://assets/ui/icon_practice.svg");
		_iconStar = GD.Load<Texture2D>("res://assets/ui/icon_schedule_star.svg");
		_iconPin = GD.Load<Texture2D>("res://assets/ui/icon_pin.svg");
		_iconClock = GD.Load<Texture2D>("res://assets/ui/icon_clock.svg");

		_session = GetNode<GameSession>("/root/GameSession");
		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		ApplyActiveTeam();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => ApplyActiveTeam();

	private void ApplyActiveTeam()
	{
		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		_title.Text = _session.Organization.Name.ToUpperInvariant();
		_subtitle.Text = varsity
			? "VARSITY  ·  ESTABLISHED PROGRAM  ·  GREAT LAKES DISTRICT  ·  THURSDAY, APRIL 24"
			: "JV  ·  ESTABLISHED PROGRAM  ·  GREAT LAKES DISTRICT  ·  THURSDAY, APRIL 24";
		_recordValue.Text = varsity ? "18-4" : "12-8";
		_districtValue.Text = varsity ? "1ST" : "4TH";
		_districtValue.AddThemeColorOverride("font_color", varsity ? Green : Accent);

		for (int i = 0; i < _dayCards.Count; i++)
		{
			WeekDay day = PrototypeWeek[i];
			bool dim = day.Level != null && !MatchesActiveLevel(day.Level);
			_dayCards[i].Modulate = dim ? new Color(1f, 1f, 1f, 0.48f) : Colors.White;
		}

		BindContest(varsity);
	}

	private bool MatchesActiveLevel(string level) =>
		_session.ActiveTeam.Level == TeamLevel.Varsity
			? level == "VARSITY"
			: level == "JV";

	private void BuildPage()
	{
		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		AddChild(margin);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		margin.AddChild(layout);

		layout.AddChild(BuildHeader());

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
		header.AddThemeConstantOverride("separation", 12);

		var logo = new TextureRect
		{
			Texture = _cascadeLogo,
			CustomMinimumSize = new Vector2(52, 52),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		header.AddChild(logo);

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -2);

		identity.AddChild(MakeText($"HOME  ·  {CurrentSeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		_title = MakeText("CASCADE REGIONAL HIGH SCHOOL", _bold, 26, TextPrimary, HorizontalAlignment.Left);
		identity.AddChild(_title);
		_subtitle = MakeText("VARSITY  ·  GREAT LAKES DISTRICT", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

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
		AddMetric(row, "84%", "BOARD", Green);
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
		};
		titles.AddThemeConstantOverride("separation", -2);
		titles.AddChild(MakeText("THIS WEEK", _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		titles.AddChild(MakeText("APRIL 20  –  APRIL 26", _medium, 11, TextMuted, HorizontalAlignment.Left));
		header.AddChild(titles);

		var next = new VBoxContainer();
		next.AddThemeConstantOverride("separation", -2);
		next.AddChild(MakeText("NEXT MILESTONE", _medium, 10, Accent, HorizontalAlignment.Right));
		next.AddChild(MakeText("ROSTER FREEZE  ·  7 DAYS", _semibold, 12, Urgent, HorizontalAlignment.Right));
		header.AddChild(next);
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
		});
		layout.AddChild(gridWrap);

		var days = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		days.AddThemeConstantOverride("separation", 0);
		gridWrap.AddChild(days);

		_dayCards.Clear();
		for (int i = 0; i < PrototypeWeek.Length; i++)
		{
			bool last = i == PrototypeWeek.Length - 1;
			days.AddChild(BuildDayColumn(PrototypeWeek[i], last));
		}

		layout.AddChild(BuildActions());
		return card;
	}

	private Control BuildDayColumn(WeekDay day, bool last)
	{
		var column = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		column.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = Colors.Transparent,
			ContentMarginLeft = 5,
			ContentMarginTop = 8,
			ContentMarginRight = 5,
			ContentMarginBottom = 8,
			BorderColor = Hairline,
			BorderWidthRight = last ? 0 : 1,
		});

		var stack = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		stack.AddThemeConstantOverride("separation", 6);
		column.AddChild(stack);

		var dayHeader = new HBoxContainer();
		dayHeader.Alignment = BoxContainer.AlignmentMode.Center;
		dayHeader.AddThemeConstantOverride("separation", 4);
		dayHeader.AddChild(MakeText(day.Label, _medium, 11, day.IsToday ? Accent : new Color(0.78f, 0.82f, 0.88f), HorizontalAlignment.Center));
		if (day.IsFeatured)
		{
			dayHeader.AddChild(new TextureRect
			{
				Texture = _iconStar,
				CustomMinimumSize = new Vector2(10, 10),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				Modulate = Accent,
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
			});
		}

		stack.AddChild(dayHeader);
		stack.AddChild(MakeHairline());

		var card = new PanelContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", MakeDayStyle(day.IsToday));
		_dayCards.Add(card);

		var content = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		content.AddThemeConstantOverride("separation", 4);

		if (day.Kind == DayKind.Game)
		{
			content.AddChild(BuildTinyLogo(day.OpponentInitial ?? "?", day.Level == "JV" ? TextMuted : Accent));
			var gameTitle = MakeText(day.Title, _semibold, 11, TextPrimary, HorizontalAlignment.Center);
			gameTitle.AutowrapMode = TextServer.AutowrapMode.Word;
			content.AddChild(gameTitle);
			content.AddChild(MakeText(day.Time ?? string.Empty, _medium, 10, TextMuted, HorizontalAlignment.Center));
			var venueColor = day.Venue == "HOME" ? Green : Accent;
			content.AddChild(MakeText(day.Venue ?? string.Empty, _semibold, 10, venueColor, HorizontalAlignment.Center));
			content.AddChild(MakeText(day.Level ?? string.Empty, _medium, 9, TextMuted, HorizontalAlignment.Center));
		}
		else
		{
			var icon = new TextureRect
			{
				Texture = day.Kind == DayKind.Practice ? _iconPractice : _iconOff,
				CustomMinimumSize = new Vector2(28, 28),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			};
			content.AddChild(icon);
			content.AddChild(MakeText(day.Title, _semibold, 12, TextPrimary, HorizontalAlignment.Center));
			if (!string.IsNullOrEmpty(day.Time))
			{
				content.AddChild(MakeText(day.Time, _medium, 10, TextMuted, HorizontalAlignment.Center));
			}
		}

		card.AddChild(content);
		stack.AddChild(card);
		return column;
	}

	private Control BuildActions()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		row.AddChild(MakeAction("ADVANCE DAY", true, 1.35f));
		row.AddChild(MakeAction("SIM TO GAME", false, 1f));
		row.AddChild(MakeAction("SIM WEEK", false, 1f));
		row.AddChild(MakeAction("SIM TO MILESTONE", false, 1f));
		return row;
	}

	private Button MakeAction(string text, bool primary, float stretch)
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
			null,
			"GL",
			new PitcherCard("MARINI", "Luca", "RHP", "5-0", "1.95", "28", _marini, Accent),
			new PitcherCard("TONY", "Great Lakes", "RHP", "2-2", "3.14", "22", _tony, LakesBlue));
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
			null,
			"GL",
			new PitcherCard("GRANT", "Miles", "RHP", "1-2", "5.06", "12", null, Accent),
			new PitcherCard("HALE", "Great Lakes", "LHP", "2-1", "3.80", "16", null, LakesBlue));
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
			SizeFlagsStretchRatio = 1.1f,
		};
		matchup.AddThemeConstantOverride("separation", 8);
		matchup.AddChild(BuildTeamBlock(homeName, homeRecord, homeLogo, null, Accent));
		var vs = MakeText("VS", _medium, 12, TextMuted, HorizontalAlignment.Center);
		vs.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		matchup.AddChild(vs);
		matchup.AddChild(BuildTeamBlock(awayName, awayRecord, awayLogo, awayInitial, LakesBlue));
		layout.AddChild(matchup);

		layout.AddChild(BuildMetaRow(_iconClock, when));
		layout.AddChild(BuildMetaRow(_iconPin, venue));
		layout.AddChild(MakeHairline());

		var pitchersTitle = MakeText("PROBABLE PITCHERS", _semibold, 11, TextMuted, HorizontalAlignment.Left);
		layout.AddChild(pitchersTitle);

		var pitchers = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.2f,
		};
		pitchers.AddThemeConstantOverride("separation", 6);
		pitchers.AddChild(BuildPitcherChip(homePitcher));
		pitchers.AddChild(BuildPitcherChip(awayPitcher));
		layout.AddChild(pitchers);

		layout.AddChild(MakeAction("VIEW MATCHUP PREVIEW", false, 1f));
		return card;
	}

	private Control BuildTeamBlock(string name, string record, Texture2D? logo, string? initial, Color accent)
	{
		var box = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		box.AddThemeConstantOverride("separation", 4);
		if (logo != null)
		{
			box.AddChild(new TextureRect
			{
				Texture = logo,
				CustomMinimumSize = new Vector2(48, 48),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			});
		}
		else
		{
			box.AddChild(BuildTinyLogo(initial ?? "?", accent, 48));
		}

		var nameLabel = MakeText(name, _semibold, 12, TextPrimary, HorizontalAlignment.Center);
		nameLabel.AutowrapMode = TextServer.AutowrapMode.Word;
		box.AddChild(nameLabel);
		box.AddChild(MakeText(record, _medium, 11, TextMuted, HorizontalAlignment.Center));
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

	private TextureRect MakeIcon(Texture2D texture, float size)
	{
		return new TextureRect
		{
			Texture = texture,
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
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

	private static StyleBoxFlat MakeDayStyle(bool today)
	{
		return new StyleBoxFlat
		{
			BgColor = today ? new Color(0.956863f, 0.643137f, 0.109804f, 0.12f) : CardInner,
			BorderColor = today ? Accent : new Color(0.22f, 0.24f, 0.28f, 1f),
			BorderWidthLeft = today ? 2 : 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 6,
			ContentMarginTop = 10,
			ContentMarginRight = 6,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
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

	private static readonly WeekDay[] PrototypeWeek =
	[
		new("SUN 20", DayKind.Off, "OFF DAY", null, null, null, false, false, null),
		new("MON 21", DayKind.Practice, "PRACTICE", "3:30 PM", null, null, false, false, null),
		new("TUE 22", DayKind.Game, "vs RIVERVIEW", "7:00 PM", "HOME", "VARSITY", false, false, "R"),
		new("WED 23", DayKind.Game, "vs RIVERVIEW", "4:00 PM", "AWAY", "JV", false, false, "R"),
		new("THU 24", DayKind.Practice, "PRACTICE", "3:30 PM", null, null, true, false, null),
		new("FRI 25", DayKind.Game, "vs GREAT LAKES", "7:00 PM", "HOME", "VARSITY", false, true, "GL"),
		new("SAT 26", DayKind.Game, "vs GREAT LAKES", "11:00 AM", "HOME", "JV", false, false, "GL"),
	];

	private enum DayKind
	{
		Off,
		Practice,
		Game,
	}

	private sealed record WeekDay(
		string Label,
		DayKind Kind,
		string Title,
		string? Time,
		string? Venue,
		string? Level,
		bool IsToday,
		bool IsFeatured,
		string? OpponentInitial);

	private sealed record PitcherCard(
		string LastName,
		string FirstName,
		string Hand,
		string Record,
		string Era,
		string Strikeouts,
		Texture2D? Portrait,
		Color Accent);
}
