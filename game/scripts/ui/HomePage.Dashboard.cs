using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// The redesigned Home dashboard (docs/ui-concepts/01_home.png): a team
/// banner with the program's four numbers and a "needs your attention"
/// list, then the next game, a compact week strip and the inbox.
/// </summary>
public partial class HomePage
{
	private BroadcastPanel _hero = null!;
	private TextureRect _heroCrest = null!;
	private Label _repValue = null!;
	private VBoxContainer _attentionList = null!;
	private VBoxContainer _inboxList = null!;
	private Label _weekToday = null!;

	private void BuildDashboard(VBoxContainer layout)
	{
		layout.AddThemeConstantOverride("separation", 12);
		layout.AddChild(BuildHero());

		var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 12);
		layout.AddChild(body);

		_contestHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.45f,
		};
		_contestHost.AddThemeConstantOverride("separation", 0);
		body.AddChild(_contestHost);

		var side = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		side.AddThemeConstantOverride("separation", 12);
		side.AddChild(BuildWeekStrip());
		side.AddChild(BuildInboxCard());
		body.AddChild(side);
	}

	// ── Hero ────────────────────────────────────────────────────────────

	private Control BuildHero()
	{
		_hero = new BroadcastPanel { CustomMinimumSize = new Vector2(0, 178), Stripe = 3f };
		_hero.UseTeam(_session.CatalogOrganizationId);

		var pad = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		pad.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		pad.AddThemeConstantOverride("margin_left", 22);
		pad.AddThemeConstantOverride("margin_right", 16);
		pad.AddThemeConstantOverride("margin_top", 14);
		pad.AddThemeConstantOverride("margin_bottom", 14);
		_hero.AddChild(pad);

		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 22);
		pad.AddChild(row);

		_heroCrest = new TextureRect
		{
			CustomMinimumSize = new Vector2(148, 148),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddChild(_heroCrest);
		_programMark = new ProgramMark(132) { SizeFlagsVertical = SizeFlags.ShrinkCenter };
		row.AddChild(_programMark);

		var id = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		id.AddThemeConstantOverride("separation", -4);
		_seasonKicker = UiKit.Text_("", UiKit.Semibold, 13, UiKit.Gold);
		id.AddChild(_seasonKicker);
		_title = UiKit.Text_("", UiKit.Bold, 52, Colors.White);
		_title.ClipText = true;
		id.AddChild(_title);
		_subtitle = UiKit.Text_("", UiKit.Medium, 16, new Color(1, 1, 1, 0.78f));
		id.AddChild(_subtitle);
		id.AddChild(UiKit.Spacer(0, 10));

		var kpis = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		kpis.AddThemeConstantOverride("separation", 30);
		_recordValue = HeroKpi(kpis, "RECORD");
		_districtValue = HeroKpi(kpis, "DISTRICT");
		_boardValue = HeroKpi(kpis, "BOARD TRUST");
		_repValue = HeroKpi(kpis, "REPUTATION");
		id.AddChild(kpis);
		row.AddChild(id);

		row.AddChild(BuildAttentionCard());
		return _hero;
	}

	private static Label HeroKpi(HBoxContainer row, string caption)
	{
		var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", -4);
		var value = UiKit.Text_("–", UiKit.Bold, 30, Colors.White);
		col.AddChild(value);
		col.AddChild(UiKit.Text_(caption, UiKit.Semibold, 11, new Color(1, 1, 1, 0.62f)));
		row.AddChild(col);
		return value;
	}

	private Control BuildAttentionCard()
	{
		var card = new PanelContainer
		{
			CustomMinimumSize = new Vector2(330, 0),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		card.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.04f, 0.045f, 0.07f, 0.80f), 14, 18, 12, new Color(1, 1, 1, 0.10f), 1));
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 4);
		col.AddChild(UiKit.Text_("NEEDS YOUR ATTENTION", UiKit.Semibold, 11, new Color(1, 1, 1, 0.6f)));
		_attentionList = new VBoxContainer();
		_attentionList.AddThemeConstantOverride("separation", 0);
		col.AddChild(_attentionList);
		card.AddChild(col);
		return card;
	}

	private void RefreshHero(SchoolSnapshot school, bool varsity)
	{
		string orgId = _session.CatalogOrganizationId;
		_hero.UseTeam(orgId);
		Texture2D? crest = TeamBrands.Logo(orgId);
		_heroCrest.Texture = crest;
		_heroCrest.Visible = crest != null;
		_programMark.Visible = crest == null;

		SchoolBrand? brand = TeamBrands.For(orgId);
		string kicker = school.DistrictName.ToUpperInvariant();
		if (!kicker.Contains("DISTRICT", StringComparison.Ordinal))
		{
			kicker += " DISTRICT";
		}

		if (brand != null)
		{
			kicker += "  ·  " + brand.LocationShort.ToUpperInvariant();
		}

		_seasonKicker.Text = kicker;
		_title.Text = (brand != null ? TeamBrands.TeamName(orgId) : _session.Organization.Name).ToUpperInvariant();
		_subtitle.Text = $"Head Coach {_session.CoachName}  ·  {(varsity ? "Varsity" : "Junior Varsity")}  ·  {GameSession.SeasonYear} season";
		_repValue.Text = _session.CoachReputationScore.ToString(CultureInfo.InvariantCulture);
		RebuildAttention();
		RebuildInbox();
	}

	// ── Needs your attention / Inbox ───────────────────────────────────

	private void RebuildAttention()
	{
		Clear(_attentionList);
		IReadOnlyList<InboxMessage> items = MessagesDesk.For(_session);
		int shown = 0;
		foreach (InboxMessage item in items)
		{
			if (shown == 3)
			{
				break;
			}

			if (shown > 0)
			{
				_attentionList.AddChild(Divider(new Color(1, 1, 1, 0.08f)));
			}

			_attentionList.AddChild(AttentionRow(item));
			shown++;
		}

		if (shown == 0)
		{
			_attentionList.AddChild(UiKit.Text_("All caught up. Nothing needs you today.", UiKit.Medium, 14, new Color(1, 1, 1, 0.7f)));
		}
	}

	private Control AttentionRow(InboxMessage item)
	{
		var button = RowButton(() => OpenTickerMessage(item));
		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		row.AddThemeConstantOverride("separation", 10);
		row.AddChild(Dot(ChannelTone(item.Channel), 8));

		var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		text.AddThemeConstantOverride("separation", -3);
		var head = UiKit.Text_(item.Headline, UiKit.Semibold, 15, Colors.White);
		head.ClipText = true;
		head.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		text.AddChild(head);
		var sub = UiKit.Text_(item.Preview, UiKit.Medium, 12, new Color(1, 1, 1, 0.55f));
		sub.ClipText = true;
		sub.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		text.AddChild(sub);
		row.AddChild(text);

		string action = string.IsNullOrWhiteSpace(item.ActionLabel) ? "OPEN" : item.ActionLabel.ToUpperInvariant();
		row.AddChild(UiKit.Text_(action, UiKit.Semibold, 11, UiKit.Gold));
		button.CustomMinimumSize = new Vector2(0, 40);
		button.AddChild(row);
		return button;
	}

	private Control BuildInboxCard()
	{
		var card = UiKit.MakeCard(18, 14);
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		card.ClipContents = true;
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 6);
		col.AddChild(UiKit.CardHeader("INBOX", "ALL MAIL", OpenMessages));
		_inboxList = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		_inboxList.AddThemeConstantOverride("separation", 0);
		col.AddChild(_inboxList);
		card.AddChild(col);
		return card;
	}

	private void RebuildInbox()
	{
		Clear(_inboxList);
		IReadOnlyList<InboxMessage> items = MessagesDesk.For(_session);

		// The banner already shows the first three; the inbox lists what follows.
		int start = items.Count > 6 ? 3 : 0;
		int shown = 0;
		for (int i = start; i < items.Count && shown < 5; i++, shown++)
		{
			_inboxList.AddChild(Divider(UiKit.Line));
			_inboxList.AddChild(InboxRow(items[i]));
		}

		if (shown == 0)
		{
			_inboxList.AddChild(Divider(UiKit.Line));
			_inboxList.AddChild(UiKit.Text_("Inbox is clear.", UiKit.Medium, 14, UiKit.Muted));
		}
	}

	private Control InboxRow(InboxMessage item)
	{
		var button = RowButton(() => OpenTickerMessage(item));
		button.CustomMinimumSize = new Vector2(0, 44);
		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		row.AddThemeConstantOverride("separation", 12);

		var avatar = new PanelContainer { CustomMinimumSize = new Vector2(32, 32), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
		avatar.AddThemeStyleboxOverride("panel", UiKit.Box(new Color("#1d2130"), 9));
		var initials = UiKit.Text_(ChannelInitials(item.Channel), UiKit.Bold, 13, ChannelTone(item.Channel), HorizontalAlignment.Center);
		initials.VerticalAlignment = VerticalAlignment.Center;
		avatar.AddChild(initials);
		row.AddChild(avatar);

		var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		text.AddThemeConstantOverride("separation", -3);
		var head = UiKit.Text_(item.Headline, UiKit.Semibold, 15, UiKit.Text);
		head.ClipText = true;
		head.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		text.AddChild(head);
		var sub = UiKit.Text_($"{item.Channel.ToLabel()[..1]}{item.Channel.ToLabel()[1..].ToLowerInvariant()}  ·  {item.Preview}", UiKit.Medium, 12, UiKit.Muted);
		sub.ClipText = true;
		sub.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		text.AddChild(sub);
		row.AddChild(text);

		if (item.Unread)
		{
			row.AddChild(Dot(UiKit.Gold, 7));
		}
		else
		{
			row.AddChild(UiKit.Text_(item.DateLabel.ToUpperInvariant(), UiKit.Semibold, 11, UiKit.Muted));
		}

		button.AddChild(row);
		return button;
	}

	private static Color ChannelTone(InboxChannel channel) => channel switch
	{
		InboxChannel.Urgent => UiKit.Red,
		InboxChannel.Board => UiKit.Gold,
		InboxChannel.Scouting => LakesBlue,
		InboxChannel.Career => UiKit.Gold,
		_ => UiKit.Green,
	};

	private static string ChannelInitials(InboxChannel channel) => channel switch
	{
		InboxChannel.Urgent => "!",
		InboxChannel.Board => "AD",
		InboxChannel.Scouting => "SC",
		InboxChannel.Player => "PL",
		InboxChannel.Career => "HC",
		_ => "•",
	};

	// ── This week ──────────────────────────────────────────────────────

	private Control BuildWeekStrip()
	{
		var card = UiKit.MakeCard(18, 14);
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 10);
		var header = new HBoxContainer();
		var titles = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		titles.AddThemeConstantOverride("separation", 10);
		titles.AddChild(UiKit.Text_("THIS WEEK", UiKit.Semibold, 13, UiKit.Muted));
		_weekRangeLabel = UiKit.Text_("", UiKit.Medium, 12, new Color(UiKit.Muted, 0.7f));
		titles.AddChild(_weekRangeLabel);
		header.AddChild(titles);
		var link = UiKit.FlatButton("CALENDAR  →", 12, UiKit.Gold);
		link.Pressed += OpenCalendarPage;
		header.AddChild(link);
		col.AddChild(header);

		_weekDaysHost = new HBoxContainer();
		_weekDaysHost.AddThemeConstantOverride("separation", 8);
		col.AddChild(_weekDaysHost);

		_weekToday = UiKit.Text_("", UiKit.Medium, 13, UiKit.Muted);
		_weekToday.ClipText = true;
		_weekToday.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		col.AddChild(_weekToday);
		_milestoneLabel = UiKit.Text_("", UiKit.Medium, 13, UiKit.Muted);
		_milestoneLabel.ClipText = true;
		col.AddChild(_milestoneLabel);
		card.AddChild(col);
		return card;
	}

	private Control BuildDayTile(DateOnly date)
	{
		IReadOnlyList<SeasonEvent> events = SeasonCalendar.On(GameSession.SeasonYear, date);
		IReadOnlyList<CalendarContest> games = _session.ContestsOn(date);
		CalendarContest? game = PrimaryGame(games);
		bool today = date == _session.CurrentDate;

		var tile = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 62),
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = DayTooltip(date, events, game),
		};
		_weekBinds.Add(new WeekBind(tile, date, game?.Level));
		ApplyWeekDayStyle(tile, date);
		tile.GuiInput += ev => OnWeekDayInput(ev, date, events, game);
		tile.MouseEntered += () => ApplyWeekDayStyle(tile, date, hovered: true);
		tile.MouseExited += () => ApplyWeekDayStyle(tile, date);

		Color ink = today ? UiKit.GoldInk : UiKit.Text;
		var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		col.AddThemeConstantOverride("separation", -4);
		col.AddChild(UiKit.Text_(date.ToString("ddd", CultureInfo.InvariantCulture).ToUpperInvariant(), UiKit.Semibold, 11, today ? UiKit.GoldInk : UiKit.Muted, HorizontalAlignment.Center));
		col.AddChild(UiKit.Text_(date.Day.ToString(CultureInfo.InvariantCulture), UiKit.Bold, 24, ink, HorizontalAlignment.Center));

		Color? mark = game != null
			? (game.Level == _session.ActiveTeam.Level ? (today ? UiKit.GoldInk : UiKit.Gold) : UiKit.Muted)
			: events.Count > 0 ? (today ? UiKit.GoldInk : LakesBlue) : null;
		var dotRow = new CenterContainer { CustomMinimumSize = new Vector2(0, 8), MouseFilter = MouseFilterEnum.Ignore };
		if (mark is { } c)
		{
			dotRow.AddChild(Dot(c, 5));
		}

		col.AddChild(dotRow);
		tile.AddChild(col);
		return tile;
	}

	private string DayTooltip(DateOnly date, IReadOnlyList<SeasonEvent> events, CalendarContest? game)
	{
		var lines = new List<string> { date.ToString("dddd, MMMM d", CultureInfo.InvariantCulture) };
		if (game != null)
		{
			lines.Add($"{(game.Home ? "vs" : "@")} {DistrictHubData.GetTeam(game.OpponentId).ShortName} · {game.Time} · {game.Level.ToLabel()}");
		}

		foreach (SeasonEvent item in events)
		{
			lines.Add(item.Name);
		}

		if (game == null && events.Count == 0)
		{
			lines.Add(date.DayOfWeek == DayOfWeek.Sunday ? "Off day" : "Practice · 3:30 PM");
		}

		return string.Join("\n", lines);
	}

	private void RefreshWeekSummary()
	{
		DateOnly today = _session.CurrentDate;
		_weekToday.Text = "";
		var todayLabel = new List<string>();
		CalendarContest? game = PrimaryGame(_session.ContestsOn(today));
		if (game != null)
		{
			todayLabel.Add($"{(game.Home ? "vs" : "@")} {DistrictHubData.GetTeam(game.OpponentId).ShortName} · {game.Time}");
		}

		foreach (SeasonEvent item in SeasonCalendar.On(GameSession.SeasonYear, today))
		{
			todayLabel.Add(item.Name);
			break;
		}

		if (todayLabel.Count == 0)
		{
			todayLabel.Add(today.DayOfWeek == DayOfWeek.Sunday ? "Off day" : "Practice · 3:30 PM");
		}

		_weekToday.Text = "Today   " + string.Join("  ·  ", todayLabel);
		_weekToday.AddThemeColorOverride("font_color", UiKit.Text);
	}

	private void OpenCalendarPage()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowCalendar();
		}
	}

	// ── Next game ──────────────────────────────────────────────────────

	private Control BuildNextGameCard(CalendarContest game)
	{
		GamePreviewSheet sheet = GamePreviewDesk.For(game, _session);
		string userId = _session.CatalogOrganizationId;
		string oppId = game.OpponentId;
		string userRecord = game.Home ? sheet.HomeRecord : sheet.AwayRecord;
		string oppRecord = game.Home ? sheet.AwayRecord : sheet.HomeRecord;
		GamePreviewStarter userArm = game.Home ? sheet.HomeStarter : sheet.AwayStarter;
		GamePreviewStarter oppArm = game.Home ? sheet.AwayStarter : sheet.HomeStarter;
		string homeId = game.Home ? userId : oppId;
		string venue = TeamBrands.For(homeId)?.HomeField ?? $"{DistrictHubData.GetTeam(homeId).ShortName} Field";

		var card = UiKit.MakeCard(20, 16);
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		card.ClipContents = true;
		var col = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		col.AddThemeConstantOverride("separation", 8);
		card.AddChild(col);

		string kicker = ContestKicker(game) + "  ·  " + (game.Home ? "HOME" : "AWAY");
		col.AddChild(UiKit.CardHeader(kicker, "MATCHUP PREVIEW", OpenMatchupPreview));

		var matchup = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		matchup.AddThemeConstantOverride("separation", 8);
		matchup.AddChild(NextGameTeam(userId, userRecord));
		var mid = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		mid.AddThemeConstantOverride("separation", 0);
		mid.AddChild(UiKit.Text_("VS", UiKit.Bold, 18, UiKit.Muted, HorizontalAlignment.Center));
		string day = game.Date.ToString("ddd  ·  MMM d", CultureInfo.InvariantCulture).ToUpperInvariant();
		mid.AddChild(UiKit.Text_(day, UiKit.Bold, 28, UiKit.Text, HorizontalAlignment.Center));
		mid.AddChild(UiKit.Text_($"{game.Time}  ·  {venue}", UiKit.Medium, 13, UiKit.Muted, HorizontalAlignment.Center));
		matchup.AddChild(mid);
		matchup.AddChild(NextGameTeam(oppId, oppRecord));
		col.AddChild(matchup);

		TeamLevel level = game.Level;
		SchoolSnapshot us = SchoolProfiles.For(userId, level, _session);
		SchoolSnapshot them = SchoolProfiles.For(oppId, level, _session);
		Color ours = BarColor(userId, UiKit.Gold);
		Color theirs = BarColor(oppId, UiKit.Red);
		var bars = new VBoxContainer();
		bars.AddThemeConstantOverride("separation", 4);
		bars.AddChild(StrengthRow("PITCHING", us.Pitching, them.Pitching, ours, theirs));
		bars.AddChild(StrengthRow("HITTING", us.Hitting, them.Hitting, ours, theirs));
		bars.AddChild(StrengthRow("DEFENSE", us.Fielding, them.Fielding, ours, theirs));
		col.AddChild(bars);
		col.AddChild(UiKit.Spacer(0, 2));

		var pitchers = new HBoxContainer();
		pitchers.AddThemeConstantOverride("separation", 10);
		pitchers.AddChild(BuildPitcherChip(ToPitcher(userArm, Accent, userId), "OUR STARTER"));
		pitchers.AddChild(BuildPitcherChip(ToPitcher(oppArm, LakesBlue, oppId), "THEIR STARTER"));
		col.AddChild(pitchers);
		return card;
	}

	private static Color BarColor(string teamId, Color fallback)
	{
		if (TeamBrands.For(teamId) == null)
		{
			return fallback;
		}

		(Color primary, Color secondary) = TeamColors.For(teamId);
		Color pick = secondary.Luminance > primary.Luminance ? secondary : primary;
		return pick.Luminance < 0.18f ? pick.Lightened(0.35f) : pick;
	}

	private Control NextGameTeam(string teamId, string record)
	{
		var box = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(150, 0),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		box.AddThemeConstantOverride("separation", 0);
		Texture2D? logo = TeamLogos.Load(teamId);
		if (logo != null)
		{
			var mark = MakeShowcaseLogo(logo, 96);
			mark.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			box.AddChild(mark);
		}
		else
		{
			var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
			center.AddChild(new ProgramMark(84, teamId));
			box.AddChild(center);
		}

		SchoolBrand? brand = TeamBrands.For(teamId);
		string name = brand?.Mascot ?? DistrictHubData.GetTeam(teamId).ShortName;
		box.AddChild(UiKit.Text_(name.ToUpperInvariant(), UiKit.Bold, 22, UiKit.Text, HorizontalAlignment.Center));
		string where = brand?.LocationShort ?? DistrictHubData.GetTeam(teamId).ShortName;
		box.AddChild(UiKit.Text_($"{record}  ·  {where}", UiKit.Medium, 12, UiKit.Muted, HorizontalAlignment.Center));
		SchoolLinks.MakeClickable(box, teamId);
		return box;
	}

	private static Control StrengthRow(string label, int ours, int theirs, Color ourColor, Color theirColor)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		var left = UiKit.Text_(ours.ToString(CultureInfo.InvariantCulture), UiKit.Bold, 18, ours >= theirs ? UiKit.Text : UiKit.Muted);
		left.CustomMinimumSize = new Vector2(30, 0);
		row.AddChild(left);
		var ourBar = UiKit.Meter(ours, 100, ourColor, 0, 6);
		ourBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		ourBar.FillMode = (int)ProgressBar.FillModeEnum.EndToBegin;
		row.AddChild(ourBar);
		var mid = UiKit.Text_(label, UiKit.Semibold, 11, UiKit.Muted, HorizontalAlignment.Center);
		mid.CustomMinimumSize = new Vector2(80, 0);
		row.AddChild(mid);
		var theirBar = UiKit.Meter(theirs, 100, theirColor, 0, 6);
		theirBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(theirBar);
		var right = UiKit.Text_(theirs.ToString(CultureInfo.InvariantCulture), UiKit.Bold, 18, theirs >= ours ? UiKit.Text : UiKit.Muted, HorizontalAlignment.Right);
		right.CustomMinimumSize = new Vector2(30, 0);
		row.AddChild(right);
		return row;
	}

	private Control BuildNoGameCard()
	{
		var card = UiKit.MakeCard(20, 16);
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 6);
		col.AddChild(UiKit.CardHeader("NEXT GAME", "SEASON SCHEDULE", OnSeasonSchedulePressedProxy));
		col.AddChild(UiKit.Text_("No game on the books", UiKit.Bold, 26, UiKit.Text));
		var body = UiKit.Text_("Nothing is scheduled for this squad yet. Keep going with CONTINUE, or check the season schedule.", UiKit.Medium, 14, UiKit.Muted);
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		col.AddChild(body);
		card.AddChild(col);
		return card;
	}

	private void OnSeasonSchedulePressedProxy()
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowSchedule();
		}
	}

	// ── Shared bits ────────────────────────────────────────────────────

	private static Button RowButton(System.Action onPressed)
	{
		var button = new Button
		{
			Flat = true,
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("hover", UiKit.Box(new Color(1, 1, 1, 0.04f), 8));
		button.AddThemeStyleboxOverride("pressed", UiKit.Box(new Color(1, 1, 1, 0.06f), 8));
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.Pressed += onPressed;
		return button;
	}

	private static Control Dot(Color color, float size)
	{
		var dot = new Panel
		{
			CustomMinimumSize = new Vector2(size, size),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		dot.AddThemeStyleboxOverride("panel", UiKit.Box(color, (int)Mathf.Ceil(size / 2f)));
		return dot;
	}

	private static Control Divider(Color color)
	{
		var line = new ColorRect { Color = color, CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore };
		return line;
	}

	private static void Clear(Node host)
	{
		foreach (Node child in host.GetChildren())
		{
			host.RemoveChild(child);
			child.QueueFree();
		}
	}

	/// <summary>Shell dropdown next to CONTINUE: simulate a day, a week, or to the next event.</summary>
	public void Simulate(int days) => OnSimulatePressed(days);

	public void SimulateToNextEvent() => OnSimulateUntilPressed();
}
