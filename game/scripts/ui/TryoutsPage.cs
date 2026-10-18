using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HSBM.Domain;

/// <summary>
/// High school tryouts. Students at your school sign up; you watch six
/// days of drills (Jan 26–31) while your read on every kid narrows; on
/// Decision Day (Feb 1) each one makes Varsity, makes JV, or is cut.
/// No recruiting, no money, no other schools' kids.
/// </summary>
public partial class TryoutsPage : Control
{
	private const string FilterAll = "all";
	private const string FilterUndecided = "undecided";
	private const string FilterVarsity = "varsity";
	private const string FilterJv = "jv";
	private const string FilterCut = "cut";
	private const string FilterPitchers = "pitchers";

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Blue = new(0.36f, 0.62f, 0.95f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color RowSelected = new(0.956863f, 0.643137f, 0.109804f, 0.14f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;

	private Label _subtitle = null!;
	private Label _statusChip = null!;
	private Label _signedUpValue = null!;
	private Label _varsityValue = null!;
	private Label _jvValue = null!;
	private Label _dayValue = null!;
	private Label _bannerLabel = null!;
	private PanelContainer _banner = null!;
	private VBoxContainer _rowsHost = null!;
	private VBoxContainer _dossierHost = null!;
	private Button _assistantButton = null!;
	private readonly Dictionary<string, Button> _filterButtons = new();

	private string _filter = FilterAll;
	private Guid _selectedId;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.TryoutsChanged += Refresh;
		_session.CareerChanged += Refresh;
		VisibilityChanged += OnVisibilityChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.TryoutsChanged -= Refresh;
			_session.CareerChanged -= Refresh;
		}

		VisibilityChanged -= OnVisibilityChanged;
	}

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
		layout.AddThemeConstantOverride("separation", 6);
		margin.AddChild(layout);
		layout.AddChild(BuildHeader());
		layout.AddChild(BuildBanner());
		layout.AddChild(BuildFilters());

		var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 6);
		layout.AddChild(body);
		body.AddChild(BuildListCard());
		body.AddChild(BuildDossierCard());
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
		identity.AddThemeConstantOverride("separation", -4);
		identity.AddChild(MakeText($"ORGANIZATION  ·  {GameSession.SeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("TRYOUTS", _bold, 22, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("", _medium, 11, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var meta = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		meta.AddThemeConstantOverride("separation", 2);
		_statusChip = MakeText("", _bold, 12, Accent, HorizontalAlignment.Right);
		meta.AddChild(_statusChip);
		var stats = new HBoxContainer();
		stats.AddThemeConstantOverride("separation", 16);
		stats.AddChild(HeaderStat("SIGNED UP", out _signedUpValue));
		stats.AddChild(HeaderStat("VARSITY SPOTS", out _varsityValue));
		stats.AddChild(HeaderStat("JV SPOTS", out _jvValue));
		stats.AddChild(HeaderStat("TRYOUT DAY", out _dayValue));
		meta.AddChild(stats);
		header.AddChild(meta);
		return header;
	}

	private Control HeaderStat(string caption, out Label value)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", -4);
		col.AddChild(MakeText(caption, _medium, 9, TextMuted, HorizontalAlignment.Right));
		value = MakeText("—", _bold, 16, TextPrimary, HorizontalAlignment.Right);
		col.AddChild(value);
		return col;
	}

	private Control BuildBanner()
	{
		_banner = new PanelContainer();
		_banner.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.956863f, 0.643137f, 0.109804f, 0.10f),
			BorderColor = Accent,
			BorderWidthLeft = 2,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		_bannerLabel = MakeText("", _medium, 12, TextPrimary, HorizontalAlignment.Left);
		_bannerLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_banner.AddChild(_bannerLabel);
		return _banner;
	}

	private Control BuildFilters()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		AddFilter(row, FilterAll, "ALL");
		AddFilter(row, FilterUndecided, "UNDECIDED");
		AddFilter(row, FilterVarsity, "VARSITY");
		AddFilter(row, FilterJv, "JV");
		AddFilter(row, FilterCut, "CUT");
		AddFilter(row, FilterPitchers, "PITCHERS");
		row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
		_assistantButton = new Button { Text = "USE ASSISTANT'S CALLS" };
		_assistantButton.TooltipText = "Every undecided kid gets your assistant coach's recommendation.";
		_assistantButton.Pressed += () =>
		{
			_session.AcceptTryoutRecommendations();
			Refresh();
		};
		row.AddChild(_assistantButton);
		var advance = new Button { Text = "ADVANCE DAY" };
		advance.Pressed += () =>
		{
			_session.AdvanceClubhouseDay();
			Refresh();
		};
		row.AddChild(advance);
		return row;
	}

	private void AddFilter(HBoxContainer row, string key, string label)
	{
		var button = new Button { Text = label, ToggleMode = true };
		button.Pressed += () =>
		{
			_filter = key;
			Refresh();
		};
		_filterButtons[key] = button;
		row.AddChild(button);
	}

	private Control BuildListCard()
	{
		var card = MakeCard();
		card.SizeFlagsStretchRatio = 1.55f;
		card.ClipContents = true;
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);
		layout.AddChild(BuildColumnHeader());
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		layout.AddChild(scroll);
		_rowsHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_rowsHost.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(_rowsHost);
		return card;
	}

	private Control BuildColumnHeader()
	{
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047059f, 0.054902f, 0.070588f, 0.96f),
			ContentMarginLeft = 12,
			ContentMarginTop = 7,
			ContentMarginRight = 12,
			ContentMarginBottom = 7,
		});
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		var player = MakeText("PLAYER", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		player.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(player);
		columns.AddChild(Fixed("POS", 36, TextMuted, _semibold, 10));
		columns.AddChild(Fixed("YR", 30, TextMuted, _semibold, 10));
		columns.AddChild(Fixed("HT / WT", 64, TextMuted, _semibold, 10));
		columns.AddChild(Fixed("OVR", 58, TextMuted, _semibold, 10));
		columns.AddChild(Fixed("POT", 58, TextMuted, _semibold, 10));
		columns.AddChild(Fixed("ASST. SAYS", 70, TextMuted, _semibold, 10));
		columns.AddChild(Fixed("YOUR CALL", 76, TextMuted, _semibold, 10));
		header.AddChild(columns);
		return header;
	}

	private Control BuildDossierCard()
	{
		var card = MakeCard();
		card.SizeFlagsStretchRatio = 1.05f;
		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		card.AddChild(scroll);
		_dossierHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_dossierHost.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(Pad(_dossierHost));
		return card;
	}

	private void Refresh()
	{
		if (_rowsHost == null)
		{
			return;
		}

		int year = GameSession.SeasonYear;
		DateOnly today = _session.CurrentDate;
		SeasonEvent week = SeasonCalendar.TryoutWeek(year);
		DateOnly decision = SeasonCalendar.TryoutDecisionDay(year);
		IReadOnlyList<Player> candidates = _session.TryoutCandidates;
		IReadOnlyDictionary<Guid, TryoutCall> advice = _session.TryoutRecommendations();
		int days = _session.TryoutDaysSeen;

		_subtitle.Text = $"{_session.Organization.Name.ToUpperInvariant()}  ·  SIGN-UPS, TRYOUT WEEK, DECISION DAY";
		_statusChip.Text = today < week.Start
			? $"TRYOUTS OPEN {week.Start:MMM d}".ToUpperInvariant()
			: today <= week.End
				? $"TRYOUT WEEK — DAY {days} OF {TryoutEvaluation.TryoutDays}"
				: today == decision ? "DECISION DAY" : "ROSTERS POSTED";
		_statusChip.AddThemeColorOverride("font_color", today == decision ? Urgent : Accent);

		List<Player> roster = _session.OrganizationRoster();
		int varsityOpen = Math.Max(0, LeagueCatalog.TeamRosterCap - roster.Count(p => p.TeamLevel == TeamLevel.Varsity));
		int jvOpen = Math.Max(0, LeagueCatalog.TeamRosterCap - roster.Count(p => p.TeamLevel == TeamLevel.JuniorVarsity));
		int callsVarsity = candidates.Count(p => p.TryoutCall == TryoutCall.Varsity);
		int callsJv = candidates.Count(p => p.TryoutCall == TryoutCall.JuniorVarsity);
		_signedUpValue.Text = $"{candidates.Count}";
		_varsityValue.Text = $"{callsVarsity}/{varsityOpen}";
		_varsityValue.AddThemeColorOverride("font_color", callsVarsity > varsityOpen ? Urgent : TextPrimary);
		_jvValue.Text = $"{callsJv}/{jvOpen}";
		_jvValue.AddThemeColorOverride("font_color", callsJv > jvOpen ? Urgent : TextPrimary);
		_dayValue.Text = $"{days}/{TryoutEvaluation.TryoutDays}";

		int pending = candidates.Count(p => p.TryoutCall == TryoutCall.Pending);
		_assistantButton.Disabled = pending == 0 || today > decision;
		_banner.Visible = true;
		_bannerLabel.Text = candidates.Count == 0
			? today > decision
				? "Decision Day has passed and the lists are up. New sign-ups open when the next baseball year starts in January."
				: "No one has signed up yet. The sign-up sheet opens when the baseball year rolls over in January."
			: days == 0
				? $"{candidates.Count} students signed up. You only have a first look: grade, position, and size. Tryout week ({week.Start:MMM d}–{week.End:MMM d}) narrows every read."
				: today <= decision
					? $"{pending} undecided. Mark each kid Varsity, JV, or Cut before {decision:MMMM d}. Whatever you leave undecided, your assistant decides. More than the open spots and the weakest calls get bumped down a level or cut."
					: "The lists are posted.";

		foreach ((string key, Button button) in _filterButtons)
		{
			button.SetPressedNoSignal(key == _filter);
		}

		Clear(_rowsHost);
		List<Player> visible = candidates
			.Where(Matches)
			.OrderBy(p => p.TryoutCall == TryoutCall.Pending ? 0 : 1)
			.ThenByDescending(p => _session.TryoutReportFor(p).OverallMid)
			.ToList();
		if (_selectedId == Guid.Empty || !candidates.Any(p => p.Id == _selectedId))
		{
			_selectedId = visible.Count > 0 ? visible[0].Id : Guid.Empty;
		}

		for (int i = 0; i < visible.Count; i++)
		{
			Player player = visible[i];
			_rowsHost.AddChild(BuildRow(player, advice, i % 2 == 0));
		}

		if (visible.Count == 0)
		{
			_rowsHost.AddChild(Pad(MakeText("Nobody here.", _medium, 13, TextMuted, HorizontalAlignment.Left)));
		}

		RefreshDossier(candidates, advice, today <= decision);
	}

	private bool Matches(Player player) => _filter switch
	{
		FilterUndecided => player.TryoutCall == TryoutCall.Pending,
		FilterVarsity => player.TryoutCall == TryoutCall.Varsity,
		FilterJv => player.TryoutCall == TryoutCall.JuniorVarsity,
		FilterCut => player.TryoutCall == TryoutCall.Cut,
		FilterPitchers => player.IsPitcher,
		_ => true,
	};

	private Control BuildRow(Player player, IReadOnlyDictionary<Guid, TryoutCall> advice, bool even)
	{
		TryoutReport report = _session.TryoutReportFor(player);
		bool selected = player.Id == _selectedId;
		var row = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
		row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = selected ? RowSelected : even ? RowEven : RowOdd,
			BorderColor = Accent,
			BorderWidthLeft = selected ? 3 : 0,
			ContentMarginLeft = 12,
			ContentMarginTop = 6,
			ContentMarginRight = 12,
			ContentMarginBottom = 6,
		});
		Guid id = player.Id;
		row.GuiInput += input =>
		{
			if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			{
				_selectedId = id;
				UiSounds.Play(UiSound.Select);
				Refresh();
			}
		};

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		var name = MakeText(player.FullName, _semibold, 14, TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		name.ClipText = true;
		columns.AddChild(name);
		columns.AddChild(Fixed(player.PrimaryPosition.ToCode(), 36, TextPrimary));
		columns.AddChild(Fixed(player.Grade.ToAbbrev(), 30, TextMuted));
		columns.AddChild(Fixed($"{player.Physical.HeightInches / 12}'{player.Physical.HeightInches % 12}\" {player.Physical.WeightLbs}", 64, TextMuted, _medium, 12));
		columns.AddChild(Fixed(report.DaysSeen == 0 ? "?" : report.OverallLabel, 58, report.DaysSeen == 0 ? TextMuted : TextPrimary, _bold, 14));
		columns.AddChild(Fixed(report.DaysSeen == 0 ? "?" : report.PotentialLabel, 58, TextMuted, _semibold, 13));
		TryoutCall suggestion = advice.TryGetValue(player.Id, out TryoutCall s) ? s : TryoutCall.Pending;
		columns.AddChild(Fixed(report.DaysSeen == 0 ? "—" : Short(suggestion), 70, CallColor(suggestion), _semibold, 12));
		columns.AddChild(Fixed(player.TryoutCall == TryoutCall.Pending ? "UNDECIDED" : Short(player.TryoutCall), 76, CallColor(player.TryoutCall), _bold, 12));
		foreach (Node child in columns.GetChildren())
		{
			if (child is Control control)
			{
				control.MouseFilter = MouseFilterEnum.Ignore;
			}
		}

		row.AddChild(columns);
		return row;
	}

	private void RefreshDossier(IReadOnlyList<Player> candidates, IReadOnlyDictionary<Guid, TryoutCall> advice, bool editable)
	{
		Clear(_dossierHost);
		Player? player = candidates.FirstOrDefault(p => p.Id == _selectedId);
		if (player == null)
		{
			_dossierHost.AddChild(MakeText("Select a candidate to see the staff's notes.", _medium, 13, TextMuted, HorizontalAlignment.Left));
			return;
		}

		TryoutReport report = _session.TryoutReportFor(player);
		var top = new HBoxContainer();
		top.AddThemeConstantOverride("separation", 14);
		var portrait = new PersonPortrait { CustomMinimumSize = new Vector2(108, 132) };
		portrait.SetPlayer(player);
		top.AddChild(portrait);
		var who = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		who.AddThemeConstantOverride("separation", 0);
		who.AddChild(MakeText($"{player.Grade.ToAbbrev()}  ·  {player.DisplayPosition}  ·  B/T {player.Bats.ToBatsThrowsLabel(player.Throws)}", _semibold, 11, Accent, HorizontalAlignment.Left));
		who.AddChild(MakeText(player.FullName.ToUpperInvariant(), _bold, 26, TextPrimary, HorizontalAlignment.Left));
		who.AddChild(MakeText($"{player.Physical.HeightInches / 12}'{player.Physical.HeightInches % 12}\"  ·  {player.Physical.WeightLbs} lb  ·  Age {player.Age}", _medium, 13, TextMuted, HorizontalAlignment.Left));
		var reads = new HBoxContainer();
		reads.AddThemeConstantOverride("separation", 18);
		reads.AddChild(BigRead("OVERALL", report.DaysSeen == 0 ? "?" : report.OverallLabel));
		reads.AddChild(BigRead("POTENTIAL", report.DaysSeen == 0 ? "?" : report.PotentialLabel));
		who.AddChild(reads);
		top.AddChild(who);
		_dossierHost.AddChild(top);

		var note = MakeText(report.Note, _medium, 13, TextPrimary, HorizontalAlignment.Left);
		note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_dossierHost.AddChild(note);

		_dossierHost.AddChild(MakeText(
			report.DaysSeen == 0 ? "DRILLS  ·  NOT EVALUATED YET" : $"DRILLS  ·  {report.DaysSeen} OF {TryoutEvaluation.TryoutDays} DAYS WATCHED",
			_semibold, 11, TextMuted, HorizontalAlignment.Left));
		foreach (DrillGrade grade in report.Drills)
		{
			_dossierHost.AddChild(DrillBar(grade, report.DaysSeen > 0));
		}

		if (report.DaysSeen > 0 && advice.TryGetValue(player.Id, out TryoutCall suggestion))
		{
			var assistant = MakeText($"Assistant coach: \"{AssistantLine(suggestion)}\"", _medium, 13, CallColor(suggestion), HorizontalAlignment.Left);
			assistant.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_dossierHost.AddChild(assistant);
		}

		if (!editable)
		{
			return;
		}

		_dossierHost.AddChild(MakeText("YOUR CALL", _semibold, 11, TextMuted, HorizontalAlignment.Left));
		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 6);
		Guid id = player.Id;
		actions.AddChild(CallButton("VARSITY", TryoutCall.Varsity, player.TryoutCall, id));
		actions.AddChild(CallButton("JV", TryoutCall.JuniorVarsity, player.TryoutCall, id));
		actions.AddChild(CallButton("CUT", TryoutCall.Cut, player.TryoutCall, id));
		actions.AddChild(CallButton("UNDECIDED", TryoutCall.Pending, player.TryoutCall, id));
		_dossierHost.AddChild(actions);
	}

	private Button CallButton(string label, TryoutCall call, TryoutCall current, Guid playerId)
	{
		bool active = call == current;
		Color tint = CallColor(call);
		var button = new Button { Text = label, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 36) };
		button.AddThemeFontOverride("font", _bold);
		button.AddThemeFontSizeOverride("font_size", 13);
		button.AddThemeColorOverride("font_color", active ? TextOnAccent : TextPrimary);
		button.AddThemeColorOverride("font_hover_color", active ? TextOnAccent : TextPrimary);
		button.AddThemeStyleboxOverride("normal", Pill(active ? tint : CardInner, active ? tint : CardBorder));
		button.AddThemeStyleboxOverride("hover", Pill(active ? tint : new Color(tint, 0.25f), tint));
		button.AddThemeStyleboxOverride("pressed", Pill(tint, tint));
		button.Pressed += () =>
		{
			UiSounds.Play(UiSound.Select);
			_session.SetTryoutCall(playerId, call);
			Refresh();
		};
		return button;
	}

	private Control BigRead(string caption, string value)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", -6);
		col.AddChild(MakeText(caption, _medium, 9, TextMuted, HorizontalAlignment.Left));
		col.AddChild(MakeText(value, _bold, 28, Accent, HorizontalAlignment.Left));
		return col;
	}

	private Control DrillBar(DrillGrade grade, bool seen)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var label = MakeText(grade.Skill.ToUpperInvariant(), _semibold, 11, TextMuted, HorizontalAlignment.Left);
		label.CustomMinimumSize = new Vector2(78, 0);
		row.AddChild(label);
		Control track = RangeTrack(seen ? grade.Low : 0, seen ? grade.High : 100, seen ? Accent : Hairline);
		track.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		track.CustomMinimumSize = new Vector2(0, 14);
		track.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(track);
		row.AddChild(Fixed(seen ? grade.RangeLabel : "?", 52, seen ? TextPrimary : TextMuted, _bold, 13));
		return row;
	}

	private static string AssistantLine(TryoutCall call) => call switch
	{
		TryoutCall.Varsity => "He can help us now. Put him on varsity.",
		TryoutCall.JuniorVarsity => "Not ready for varsity, but I want him in the program. JV.",
		TryoutCall.Cut => "We don't have a spot for him this year.",
		_ => "I need more time with him.",
	};

	private static string Short(TryoutCall call) => call switch
	{
		TryoutCall.Varsity => "VARSITY",
		TryoutCall.JuniorVarsity => "JV",
		TryoutCall.Cut => "CUT",
		_ => "—",
	};

	private static Color CallColor(TryoutCall call) => call switch
	{
		TryoutCall.Varsity => Green,
		TryoutCall.JuniorVarsity => Blue,
		TryoutCall.Cut => Urgent,
		_ => TextMuted,
	};

	private Label Fixed(string text, float width, Color color, FontFile? font = null, int size = 13)
	{
		Label label = MakeText(text, font ?? _medium, size, color, HorizontalAlignment.Center);
		label.CustomMinimumSize = new Vector2(width, 0);
		return label;
	}

	private static Label MakeText(string text, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		var label = new Label { Text = text, HorizontalAlignment = align };
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static MarginContainer Pad(Control child)
	{
		var pad = new MarginContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		pad.AddThemeConstantOverride("margin_left", 14);
		pad.AddThemeConstantOverride("margin_top", 12);
		pad.AddThemeConstantOverride("margin_right", 14);
		pad.AddThemeConstantOverride("margin_bottom", 12);
		pad.AddChild(child);
		return pad;
	}

	private static PanelContainer MakeCard()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.078431f, 0.094118f, 0.94f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		return card;
	}

	private static StyleBoxFlat Pill(Color bg, Color border) => new()
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
		CornerRadiusTopLeft = 4,
		CornerRadiusTopRight = 4,
		CornerRadiusBottomRight = 4,
		CornerRadiusBottomLeft = 4,
	};

	private static void Clear(Node host)
	{
		foreach (Node child in host.GetChildren())
		{
			host.RemoveChild(child);
			child.QueueFree();
		}
	}

	/// <summary>A 1–100 track with the staff's estimate range filled in.</summary>
	private static Control RangeTrack(int low, int high, Color fill)
	{
		var track = new Control { MouseFilter = MouseFilterEnum.Ignore };
		track.Draw += () =>
		{
			Vector2 size = track.Size;
			float h = Mathf.Min(8f, size.Y);
			float y = (size.Y - h) / 2f;
			track.DrawRect(new Rect2(0, y, size.X, h), new Color(1, 1, 1, 0.06f), true);
			for (int tick = 20; tick < 100; tick += 20)
			{
				float x = size.X * tick / 100f;
				track.DrawLine(new Vector2(x, y), new Vector2(x, y + h), new Color(1, 1, 1, 0.10f), 1f);
			}

			float from = size.X * Mathf.Clamp(low, 0, 100) / 100f;
			float to = size.X * Mathf.Clamp(high, 0, 100) / 100f;
			track.DrawRect(new Rect2(from, y, Mathf.Max(3f, to - from), h), fill, true);
		};
		track.Resized += track.QueueRedraw;
		return track;
	}
}
