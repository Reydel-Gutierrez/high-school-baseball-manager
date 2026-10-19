using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;
using HSBM.Simulation;

/// <summary>
/// Game day, live. The coach watches plate appearances unfold and makes the
/// calls — steals, bunts, intentional walks, pinch hitters, and pitching
/// changes — or lets the assistant run the club. The finished game flows
/// back into the career exactly like a simulated one.
/// </summary>
public partial class LiveGamePage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color TextDim = new(0.40f, 0.43f, 0.48f, 1f);
	private static readonly Color Accent = new(0.772549f, 0.635294f, 0.325490f, 1f);
	private static readonly Color AccentHover = new(0.850980f, 0.729412f, 0.443137f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color Opponent = new(0.42f, 0.64f, 0.92f, 1f);
	private static readonly Color CardInner = new(0.090196f, 0.105882f, 0.141176f, 1f);
	private static readonly Color CardFill = new(0.070588f, 0.082353f, 0.109804f, 1f);
	private static readonly Color CardBorder = new(0.137255f, 0.152941f, 0.200000f, 1f);
	private static readonly Color Hairline = new(0.17f, 0.19f, 0.24f, 1f);

	private static readonly float[] Speeds = [1.4f, 0.8f, 0.35f];
	private static readonly string[] SpeedLabels = ["1×", "2×", "4×"];

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private bool _built;

	private LiveGame? _live;
	private CalendarContest? _contest;
	private bool _userHome;
	private string _userShort = "";
	private string _oppShort = "";

	private Label _headline = null!;
	private GridContainer _linescore = null!;
	private DiamondView _diamond = null!;
	private Label _situation = null!;
	private VBoxContainer _batterCard = null!;
	private VBoxContainer _pitcherCard = null!;
	private VBoxContainer _feed = null!;
	private ScrollContainer _feedScroll = null!;
	private VBoxContainer _managerBody = null!;
	private Label _managerTitle = null!;
	private CheckButton _assistantToggle = null!;
	private HBoxContainer _controls = null!;
	private Button _nextButton = null!;
	private Button _halfButton = null!;
	private Button _autoButton = null!;
	private Button _speedButton = null!;
	private Button _simButton = null!;
	private Button _finishButton = null!;
	private Godot.Timer _autoTimer = null!;
	private int _speedIndex;
	private int _feedCount;
	private string _managerMode = "";
	private bool _followFeed = true;

	public event Action<SimulationRun>? Completed;

	public override void _Ready() => EnsureBuilt();

	/// <summary>Begin a live game for the contest. Returns false if it cannot be played live.</summary>
	public bool Begin(CalendarContest contest)
	{
		EnsureBuilt();
		LiveGame? live = _session.StartLiveGame(contest);
		if (live == null || _session.ActiveTeam == null)
		{
			return false;
		}

		_live = live;
		_contest = contest;
		_userHome = live.HomeTeamId == _session.ActiveTeam.Id;
		_userShort = UserProgram.ShortName.ToUpperInvariant();
		_oppShort = DistrictHubData.GetTeam(contest.OpponentId).ShortName.ToUpperInvariant();
		_feedCount = 0;
		_managerMode = "";
		_followFeed = true;
		foreach (Node child in _feed.GetChildren())
		{
			child.QueueFree();
		}

		_assistantToggle.SetPressedNoSignal(false);
		StopAuto();
		string when = contest.Date.ToString("ddd, MMM d", CultureInfo.InvariantCulture).ToUpperInvariant();
		string matchup = _userHome ? $"{_oppShort} @ {_userShort}" : $"{_userShort} @ {_oppShort}";
		_headline.Text = $"{contest.Level.ToLabel().ToUpperInvariant()}  ·  {when}  ·  {matchup}";
		AppendPlays(live.Log);
		RefreshAll();
		return true;
	}

	private void EnsureBuilt()
	{
		if (_built)
		{
			return;
		}

		_built = true;
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_session = GetNode<GameSession>("/root/GameSession");
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var backdrop = new TextureRect
		{
			Texture = GD.Load<Texture2D>("res://assets/teams/01_Cascade_Regional_High_School/stadium.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			Modulate = new Color(0.30f, 0.30f, 0.33f, 0.55f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(backdrop);

		backdrop.Visible = false;
		var dim = new ColorRect { Color = UiKit.Bg, MouseFilter = MouseFilterEnum.Ignore };
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 22);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_right", 22);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		AddChild(margin);

		var shell = new VBoxContainer();
		shell.AddThemeConstantOverride("separation", 10);
		margin.AddChild(shell);

		shell.AddChild(BuildHeader());
		shell.AddChild(BuildLinescorePanel());

		var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 10);
		shell.AddChild(body);
		body.AddChild(BuildFieldColumn());
		body.AddChild(BuildFeedColumn());
		body.AddChild(BuildManagerColumn());

		shell.AddChild(BuildControls());

		_autoTimer = new Godot.Timer { OneShot = false, WaitTime = Speeds[0] };
		_autoTimer.Timeout += OnAutoTick;
		AddChild(_autoTimer);
	}

	// ── Layout ──────────────────────────────────────────────────────────

	private Control BuildHeader()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);

		var pill = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
		pill.AddThemeStyleboxOverride("panel", Rounded(Urgent, 3, 8, 3));
		pill.AddChild(MakeText("● LIVE", _bold, 13, TextPrimary, HorizontalAlignment.Center));
		row.AddChild(pill);

		_headline = MakeText("", _bold, 22, TextPrimary, HorizontalAlignment.Left);
		_headline.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(_headline);

		_assistantToggle = new CheckButton
		{
			Text = "ASSISTANT MANAGES MY CLUB",
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			TooltipText = "When on, your assistant handles steals and bullpen moves for your team.",
		};
		_assistantToggle.AddThemeFontOverride("font", _semibold);
		_assistantToggle.AddThemeFontSizeOverride("font_size", 13);
		_assistantToggle.AddThemeColorOverride("font_color", TextMuted);
		_assistantToggle.Toggled += OnAssistantToggled;
		row.AddChild(_assistantToggle);
		return row;
	}

	private Control BuildLinescorePanel()
	{
		var card = MakeCard(14, 10);
		_linescore = new GridContainer { Columns = 11 };
		_linescore.AddThemeConstantOverride("h_separation", 0);
		_linescore.AddThemeConstantOverride("v_separation", 4);
		card.AddChild(_linescore);
		return card;
	}

	private Control BuildFieldColumn()
	{
		var column = new VBoxContainer { CustomMinimumSize = new Vector2(400, 0) };
		column.AddThemeConstantOverride("separation", 10);

		var fieldCard = MakeCard(12, 10);
		var fieldBox = new VBoxContainer();
		fieldBox.AddThemeConstantOverride("separation", 4);
		_situation = MakeText("", _bold, 15, TextPrimary, HorizontalAlignment.Center);
		fieldBox.AddChild(_situation);
		_diamond = new DiamondView { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		fieldBox.AddChild(_diamond);
		fieldCard.AddChild(fieldBox);
		column.AddChild(fieldCard);

		var matchupScroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		var matchup = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		matchup.AddThemeConstantOverride("separation", 10);
		matchupScroll.AddChild(matchup);

		(_batterPanel, _batterCard, _batterFace) = BuildPlayerBanner();
		matchup.AddChild(_batterPanel);
		(_pitcherPanel, _pitcherCard, _pitcherFace) = BuildPlayerBanner();
		matchup.AddChild(_pitcherPanel);

		column.AddChild(matchupScroll);
		return column;
	}

	private BroadcastPanel _batterPanel = null!;
	private BroadcastPanel _pitcherPanel = null!;
	private Control _batterFace = null!;
	private Control _pitcherFace = null!;

	/// <summary>Broadcast banner for the batter or pitcher: team art, headshot on the right, line on the left.</summary>
	private static (BroadcastPanel Panel, VBoxContainer Body, Control Face) BuildPlayerBanner()
	{
		var panel = new BroadcastPanel { CustomMinimumSize = new Vector2(0, 150), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var face = new Control { MouseFilter = MouseFilterEnum.Ignore, AnchorLeft = 0.60f, AnchorRight = 1f, AnchorTop = 0f, AnchorBottom = 1f, OffsetTop = 8, OffsetBottom = -3 };
		panel.AddChild(face);
		var body = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0f,
			AnchorRight = 0.66f,
			AnchorTop = 0f,
			AnchorBottom = 1f,
			OffsetLeft = 12,
			OffsetTop = 10,
			OffsetRight = 0,
			OffsetBottom = -10,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		body.AddThemeConstantOverride("separation", 2);
		panel.AddChild(body);
		return (panel, body, face);
	}

	private void SetBannerFace(Control host, Player player)
	{
		Clear(host);
		PersonPortrait? face = PlayerFaces.For(player, 130, 160);
		if (face == null)
		{
			return;
		}

		face.Backdrop = false;
		face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		face.CustomMinimumSize = Vector2.Zero;
		face.MouseFilter = MouseFilterEnum.Ignore;
		host.AddChild(face);
	}

	private Control BuildFeedColumn()
	{
		var card = MakeCard(14, 12);
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		box.AddChild(MakeText("PLAY-BY-PLAY", _bold, 13, Accent, HorizontalAlignment.Left));
		box.AddChild(MakeHairline());
		_feedScroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_feed = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_feed.AddThemeConstantOverride("separation", 4);
		_feedScroll.AddChild(_feed);
		box.AddChild(_feedScroll);
		// Follow the newest play: when content grows, pin to the bottom
		// (unless the coach has scrolled up to read back).
		ScrollBar feedBar = _feedScroll.GetVScrollBar();
		feedBar.Changed += () =>
		{
			if (_followFeed)
			{
				_feedScroll.ScrollVertical = (int)feedBar.MaxValue;
			}
		};
		feedBar.Scrolling += () =>
		{
			_followFeed = feedBar.Value + feedBar.Page >= feedBar.MaxValue - 4;
		};
		card.AddChild(box);
		return card;
	}

	private Control BuildManagerColumn()
	{
		var card = MakeCard(14, 12);
		card.CustomMinimumSize = new Vector2(300, 0);
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		box.AddChild(MakeText("MANAGER", _bold, 13, Accent, HorizontalAlignment.Left));
		_managerTitle = MakeText("", _bold, 17, TextPrimary, HorizontalAlignment.Left);
		_managerTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		box.AddChild(_managerTitle);
		box.AddChild(MakeHairline());
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_managerBody = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_managerBody.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(_managerBody);
		box.AddChild(scroll);
		card.AddChild(box);
		return card;
	}

	private Control BuildControls()
	{
		_controls = new HBoxContainer();
		_controls.AddThemeConstantOverride("separation", 8);
		_nextButton = MakeAction("NEXT BATTER", true, 1.3f, OnNext);
		_halfButton = MakeAction("FINISH HALF-INNING", false, 1.1f, OnHalf);
		_autoButton = MakeAction("▶  AUTO-PLAY", false, 1f, ToggleAuto);
		_speedButton = MakeAction(SpeedLabels[0], false, 0f, CycleSpeed, 64);
		_simButton = MakeAction("SIM TO FINAL", false, 1f, OnSimToEnd);
		_finishButton = MakeAction("FINISH GAME", true, 1.3f, OnFinish);
		_controls.AddChild(_nextButton);
		_controls.AddChild(_halfButton);
		_controls.AddChild(_autoButton);
		_controls.AddChild(_speedButton);
		_controls.AddChild(_simButton);
		_controls.AddChild(_finishButton);
		return _controls;
	}

	// ── Flow ────────────────────────────────────────────────────────────

	private void OnNext()
	{
		if (_live == null || _live.IsFinal)
		{
			return;
		}

		UiSounds.Play(UiSound.Select);
		AppendPlays(_live.Step());
		RefreshAll();
	}

	private void OnHalf()
	{
		if (_live == null || _live.IsFinal)
		{
			return;
		}

		UiSounds.Play(UiSound.Select);
		AppendPlays(_live.StepHalfInning());
		RefreshAll();
	}

	private void OnSimToEnd()
	{
		if (_live == null || _live.IsFinal)
		{
			return;
		}

		StopAuto();
		int before = _live.Log.Count;
		_live.RunToEnd();
		var rest = new List<LivePlay>();
		for (int i = before; i < _live.Log.Count; i++)
		{
			rest.Add(_live.Log[i]);
		}

		AppendPlays(rest);
		RefreshAll();
	}

	private void OnFinish()
	{
		if (_live == null || !_live.IsFinal)
		{
			return;
		}

		UiSounds.Play(UiSound.Confirm);
		GameSimulationResult result = _live.Finish();
		_live = null;
		SimulationRun run = _session.FinishLiveGame(result);
		Completed?.Invoke(run);
	}

	private void ToggleAuto()
	{
		if (_live == null || _live.IsFinal)
		{
			return;
		}

		if (_autoTimer.IsStopped())
		{
			_autoTimer.WaitTime = Speeds[_speedIndex];
			_autoTimer.Start();
		}
		else
		{
			_autoTimer.Stop();
		}

		RefreshControls();
	}

	private void StopAuto()
	{
		if (_autoTimer != null && !_autoTimer.IsStopped())
		{
			_autoTimer.Stop();
		}
	}

	private void CycleSpeed()
	{
		_speedIndex = (_speedIndex + 1) % Speeds.Length;
		_speedButton.Text = SpeedLabels[_speedIndex];
		_autoTimer.WaitTime = Speeds[_speedIndex];
	}

	private void OnAutoTick()
	{
		if (_live == null || _live.IsFinal)
		{
			StopAuto();
			RefreshControls();
			return;
		}

		AppendPlays(_live.Step());
		RefreshAll();
	}

	private void OnAssistantToggled(bool on)
	{
		if (_live == null)
		{
			return;
		}

		if (_userHome)
		{
			_live.AutoManageHome = on;
		}
		else
		{
			_live.AutoManageAway = on;
		}

		RefreshManager();
	}

	// ── Rendering ───────────────────────────────────────────────────────

	private void RefreshAll()
	{
		if (_live == null)
		{
			return;
		}

		RefreshLinescore();
		RefreshField();
		RefreshMatchup();
		RefreshManager();
		RefreshControls();
	}

	private void RefreshLinescore()
	{
		LiveGame live = _live!;
		foreach (Node child in _linescore.GetChildren())
		{
			_linescore.RemoveChild(child);
			child.QueueFree();
		}

		int innings = Math.Max(live.RegulationInnings, Math.Max(live.HomeInningRuns.Count, live.AwayInningRuns.Count));
		_linescore.Columns = 1 + innings + 3;
		AddCell("", 150, TextMuted, HorizontalAlignment.Left);
		for (int i = 1; i <= innings; i++)
		{
			bool current = !live.IsFinal && live.HalfInProgress && live.Inning == i;
			AddCell(i.ToString(CultureInfo.InvariantCulture), 40, current ? Accent : TextMuted, HorizontalAlignment.Center);
		}

		AddCell("R", 44, TextPrimary, HorizontalAlignment.Center);
		AddCell("H", 44, TextMuted, HorizontalAlignment.Center);
		AddCell("E", 44, TextMuted, HorizontalAlignment.Center);

		AddTeamRow(away: true, innings);
		AddTeamRow(away: false, innings);
	}

	private void AddTeamRow(bool away, int innings)
	{
		LiveGame live = _live!;
		bool user = away != _userHome;
		string name = away
			? (_userHome ? _oppShort : _userShort)
			: (_userHome ? _userShort : _oppShort);
		AddCell(name, 150, user ? Accent : Opponent, HorizontalAlignment.Left, _bold, 17);
		IReadOnlyList<int> runs = away ? live.AwayInningRuns : live.HomeInningRuns;
		for (int i = 0; i < innings; i++)
		{
			string text = i < runs.Count ? runs[i].ToString(CultureInfo.InvariantCulture) : (live.IsFinal && !away && i == innings - 1 ? "X" : "");
			bool battingNow = !live.IsFinal && live.HalfInProgress && live.Inning == i + 1 && live.TopHalf == away;
			AddCell(text, 40, battingNow ? Accent : TextPrimary, HorizontalAlignment.Center, _semibold, 17);
		}

		int score = away ? live.AwayScore : live.HomeScore;
		AddCell(score.ToString(CultureInfo.InvariantCulture), 44, TextPrimary, HorizontalAlignment.Center, _bold, 20);
		AddCell((away ? live.AwayHits : live.HomeHits).ToString(CultureInfo.InvariantCulture), 44, TextMuted, HorizontalAlignment.Center, _semibold, 17);
		AddCell((away ? live.AwayErrors : live.HomeErrors).ToString(CultureInfo.InvariantCulture), 44, TextMuted, HorizontalAlignment.Center, _semibold, 17);
	}

	private void AddCell(string text, float width, Color color, HorizontalAlignment align, FontFile? font = null, int size = 13)
	{
		Label label = MakeText(text, font ?? _semibold, size, color, align);
		label.CustomMinimumSize = new Vector2(width, 0);
		_linescore.AddChild(label);
	}

	private void RefreshField()
	{
		LiveGame live = _live!;
		_diamond.SetState(live.OnFirst != null, live.OnSecond != null, live.OnThird != null, live.HalfInProgress ? live.Outs : 0);
		if (live.IsFinal)
		{
			bool userWon = _userHome ? live.HomeScore > live.AwayScore : live.AwayScore > live.HomeScore;
			_situation.Text = userWon ? "FINAL  ·  WIN" : "FINAL  ·  LOSS";
			_situation.AddThemeColorOverride("font_color", userWon ? Green : Urgent);
			return;
		}

		string half = live.TopHalf ? "TOP" : "BOTTOM";
		_situation.Text = $"{half} {Ordinal(live.Inning)}  ·  {(live.HalfInProgress ? live.Outs : 0)} OUT";
		_situation.AddThemeColorOverride("font_color", TextPrimary);
	}

	private void RefreshMatchup()
	{
		LiveGame live = _live!;
		Clear(_batterCard);
		Clear(_pitcherCard);
		if (live.IsFinal)
		{
			_batterCard.AddChild(MakeText("GAME OVER", _bold, 13, TextMuted, HorizontalAlignment.Left));
			_batterCard.AddChild(MakeText("Finish the game to post the result, update stats, and move to tomorrow.", _medium, 13, TextMuted, HorizontalAlignment.Left, wrap: true));
			_pitcherPanel.Visible = false;
			Clear(_batterFace);
			return;
		}

		_pitcherPanel.Visible = true;
		bool homeBatting = live.HomeBatting;
		bool userBatting = homeBatting == _userHome;
		Player batter = live.NextBatter;
		PlayerGameBatting? line = live.BattingLine(batter.Id);
		string userOrg = _session.CatalogOrganizationId;
		string oppOrg = _contest?.OpponentId ?? "";
		_batterPanel.UseTeam(userBatting ? userOrg : oppOrg);
		_batterPanel.Number = batter.JerseyNumber > 0 ? batter.JerseyNumber.ToString(CultureInfo.InvariantCulture) : "";
		SetBannerFace(_batterFace, batter);
		_batterCard.AddChild(MakeText($"AT BAT  ·  {(userBatting ? _userShort : _oppShort)}", _bold, 11, userBatting ? Accent : Opponent, HorizontalAlignment.Left));
		_batterCard.AddChild(MakeText(batter.FullName.ToUpperInvariant(), _bold, 24, TextPrimary, HorizontalAlignment.Left));
		_batterCard.AddChild(MakeText($"{SlotPosition(live, batter)}  ·  Bats {batter.Bats.ToAbbrev()}  ·  Today {TodayBatting(line)}", _medium, 13, new Color(1, 1, 1, 0.78f), HorizontalAlignment.Left));
		_batterCard.AddChild(RatingRow("CON", batter.Ratings.Contact, "POW", batter.Ratings.Power));
		_batterCard.AddChild(RatingRow("EYE", batter.Ratings.PlateDiscipline, "SPD", batter.Ratings.Speed));

		bool homePitching = !homeBatting;
		bool userPitching = homePitching == _userHome;
		Player pitcher = live.Pitcher(homePitching);
		int pitches = live.PitchCount(homePitching);
		int limit = Math.Max(1, live.PitchLimit(homePitching));
		PlayerGamePitching? arm = live.PitchingLine(homePitching, pitcher.Id);
		_pitcherPanel.UseTeam(userPitching ? userOrg : oppOrg);
		_pitcherPanel.Number = pitcher.JerseyNumber > 0 ? pitcher.JerseyNumber.ToString(CultureInfo.InvariantCulture) : "";
		SetBannerFace(_pitcherFace, pitcher);
		_pitcherCard.AddChild(MakeText($"ON THE MOUND  ·  {(userPitching ? _userShort : _oppShort)}", _bold, 11, userPitching ? Accent : Opponent, HorizontalAlignment.Left));
		_pitcherCard.AddChild(MakeText(pitcher.FullName.ToUpperInvariant(), _bold, 24, TextPrimary, HorizontalAlignment.Left));
		_pitcherCard.AddChild(MakeText($"{pitcher.Throws.ToAbbrev()}HP  ·  {TodayPitching(arm)}", _medium, 13, new Color(1, 1, 1, 0.78f), HorizontalAlignment.Left));
		_pitcherCard.AddChild(RatingRow("STF", pitcher.Ratings.Stuff, "CTL", pitcher.Ratings.Control));
		_pitcherCard.AddChild(RatingRow("MOV", pitcher.Ratings.Movement, "STA", pitcher.Ratings.Stamina));
		_pitcherCard.AddChild(PitchCountBar(pitches, limit, live.Fatigue(homePitching)));
	}

	private void RefreshManager()
	{
		LiveGame live = _live!;
		Clear(_managerBody);
		if (live.IsFinal)
		{
			_managerTitle.Text = "Game final.";
			return;
		}

		bool assistant = _assistantToggle.ButtonPressed;
		bool userBatting = live.HomeBatting == _userHome;
		_managerTitle.Text = userBatting ? "Your club is at the plate." : "Your club is in the field.";
		if (assistant)
		{
			_managerBody.AddChild(MakeText("Your assistant is making the calls. Turn the switch off to manage yourself.", _medium, 13, TextMuted, HorizontalAlignment.Left, wrap: true));
		}

		if (userBatting)
		{
			Button steal = MakeAction(StealLabel(live), false, 1f, OnSteal);
			steal.Disabled = !live.CanSteal;
			_managerBody.AddChild(steal);
			Button bunt = MakeAction(live.BuntQueued ? "BUNT CALLED ✓" : "SACRIFICE BUNT", live.BuntQueued, 1f, OnBunt);
			bunt.Disabled = !live.CanBunt;
			_managerBody.AddChild(bunt);
			_managerBody.AddChild(MakeAction(_managerMode == "pinch" ? "HIDE BENCH" : "PINCH HITTER…", false, 1f, () => ToggleMode("pinch")));
			if (_managerMode == "pinch")
			{
				AddBenchList(live);
			}
		}
		else
		{
			_managerBody.AddChild(MakeAction(live.WalkQueued ? "INTENTIONAL WALK ✓" : "INTENTIONAL WALK", live.WalkQueued, 1f, OnWalk));
			_managerBody.AddChild(MakeAction(_managerMode == "pen" ? "HIDE BULLPEN" : "CALL TO THE BULLPEN…", false, 1f, () => ToggleMode("pen")));
			if (_managerMode == "pen")
			{
				AddBullpenList(live);
			}
		}
	}

	private void AddBenchList(LiveGame live)
	{
		IReadOnlyList<Player> bench = live.AvailableBench(_userHome);
		if (bench.Count == 0)
		{
			_managerBody.AddChild(MakeText("No position players left on the bench.", _medium, 13, TextMuted, HorizontalAlignment.Left, wrap: true));
			return;
		}

		_managerBody.AddChild(MakeText($"Hit for {live.NextBatter.LastName}:", _semibold, 13, TextMuted, HorizontalAlignment.Left));
		foreach (Player player in bench)
		{
			Player pick = player;
			string label = $"{player.LastName.ToUpperInvariant()}  {player.PrimaryPosition.ToCode()}   CON {player.Ratings.Contact}  POW {player.Ratings.Power}";
			_managerBody.AddChild(MakeAction(label, false, 1f, () => OnPinchHit(pick)));
		}
	}

	private void AddBullpenList(LiveGame live)
	{
		IReadOnlyList<Player> pen = live.AvailableRelievers(_userHome);
		if (pen.Count == 0)
		{
			_managerBody.AddChild(MakeText("The bullpen is empty.", _medium, 13, TextMuted, HorizontalAlignment.Left, wrap: true));
			return;
		}

		foreach (Player arm in pen)
		{
			Player pick = arm;
			string label = $"{arm.LastName.ToUpperInvariant()}  {arm.Throws.ToAbbrev()}HP   STF {arm.Ratings.Stuff}  CTL {arm.Ratings.Control}";
			_managerBody.AddChild(MakeAction(label, false, 1f, () => OnChangePitcher(pick)));
		}
	}

	private void RefreshControls()
	{
		bool final = _live == null || _live.IsFinal;
		_nextButton.Visible = !final;
		_halfButton.Visible = !final;
		_autoButton.Visible = !final;
		_speedButton.Visible = !final;
		_simButton.Visible = !final;
		_finishButton.Visible = final && _live != null;
		_autoButton.Text = _autoTimer.IsStopped() ? "▶  AUTO-PLAY" : "❚❚  PAUSE";
	}

	private void AppendPlays(IReadOnlyList<LivePlay> plays)
	{
		foreach (LivePlay play in plays)
		{
			_feed.AddChild(BuildPlayRow(play));
			_feedCount++;
		}

		if (plays.Count > 0 && _followFeed)
		{
			CallDeferred(MethodName.ScrollFeedToBottom);
		}
	}

	private void ScrollFeedToBottom()
	{
		_feedScroll.ScrollVertical = (int)_feedScroll.GetVScrollBar().MaxValue;
	}

	private Control BuildPlayRow(LivePlay play)
	{
		if (play.Kind == LivePlayKind.HalfInning)
		{
			var header = new PanelContainer();
			header.AddThemeStyleboxOverride("panel", Rounded(HoverBg, 3, 8, 4));
			bool userBatting = play.HomeTeamBatting == _userHome;
			string team = userBatting ? _userShort : _oppShort;
			header.AddChild(MakeText($"{play.Text.ToUpperInvariant().TrimEnd('.')}  ·  {team} BATTING", _bold, 12, userBatting ? Accent : Opponent, HorizontalAlignment.Left));
			return header;
		}

		Color color = play.Kind switch
		{
			LivePlayKind.Final => Green,
			LivePlayKind.PitchingChange or LivePlayKind.PinchHitter => Opponent,
			_ when play.RunsScored > 0 => Accent,
			_ => TextPrimary,
		};

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		string tag = play.Kind == LivePlayKind.Final ? "F" : $"{(play.TopHalf ? "▲" : "▼")}{play.Inning}";
		Label tagLabel = MakeText(tag, _semibold, 12, TextDim, HorizontalAlignment.Left);
		tagLabel.CustomMinimumSize = new Vector2(30, 0);
		row.AddChild(tagLabel);
		Label text = MakeText(play.Text, play.RunsScored > 0 || play.Kind == LivePlayKind.Final ? _bold : _medium, 15, color, HorizontalAlignment.Left, wrap: true);
		text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(text);
		string score = play.RunsScored > 0 || play.Kind == LivePlayKind.Final ? $"{play.AwayScore}–{play.HomeScore}" : $"{play.Outs} out";
		row.AddChild(MakeText(score, _semibold, 12, play.RunsScored > 0 ? Accent : TextDim, HorizontalAlignment.Right));
		return row;
	}

	// ── Manager actions ─────────────────────────────────────────────────

	private void OnSteal()
	{
		if (_live == null)
		{
			return;
		}

		UiSounds.Play(UiSound.Confirm);
		AppendPlays(_live.Steal());
		RefreshAll();
	}

	private void OnBunt()
	{
		if (_live == null)
		{
			return;
		}

		if (_live.BuntQueued)
		{
			_live.ClearQueuedCalls();
		}
		else
		{
			_live.QueueBunt();
		}

		RefreshManager();
	}

	private void OnWalk()
	{
		if (_live == null)
		{
			return;
		}

		if (_live.WalkQueued)
		{
			_live.ClearQueuedCalls();
		}
		else
		{
			_live.QueueIntentionalWalk();
		}

		RefreshManager();
	}

	private void OnPinchHit(Player player)
	{
		if (_live == null || !_live.PinchHit(player))
		{
			return;
		}

		UiSounds.Play(UiSound.Confirm);
		_managerMode = "";
		AppendPlays([_live.Log[^1]]);
		RefreshAll();
	}

	private void OnChangePitcher(Player arm)
	{
		if (_live == null || !_live.ChangePitcher(_userHome, arm))
		{
			return;
		}

		UiSounds.Play(UiSound.Confirm);
		_managerMode = "";
		AppendPlays([_live.Log[^1]]);
		RefreshAll();
	}

	private void ToggleMode(string mode)
	{
		_managerMode = _managerMode == mode ? "" : mode;
		RefreshManager();
	}

	// ── Small builders ──────────────────────────────────────────────────

	private static string StealLabel(LiveGame live)
	{
		if (live.OnFirst != null && live.OnSecond == null)
		{
			return $"SEND {live.OnFirst.LastName.ToUpperInvariant()} (STEAL 2ND)";
		}

		if (live.OnSecond != null && live.OnThird == null)
		{
			return $"SEND {live.OnSecond.LastName.ToUpperInvariant()} (STEAL 3RD)";
		}

		return "STEAL";
	}

	private static string SlotPosition(LiveGame live, Player batter)
	{
		foreach (LineupBatter slot in live.Lineup(live.HomeBatting))
		{
			if (slot.Player.Id == batter.Id)
			{
				return slot.FieldPosition.ToCode();
			}
		}

		return batter.PrimaryPosition.ToCode();
	}

	private static string TodayBatting(PlayerGameBatting? line)
	{
		if (line == null || line.PlateAppearances == 0)
		{
			return "first trip";
		}

		var parts = new List<string> { $"{line.Hits}-{line.AtBats}" };
		if (line.HomeRuns > 0)
		{
			parts.Add($"{line.HomeRuns} HR");
		}

		if (line.RunsBattedIn > 0)
		{
			parts.Add($"{line.RunsBattedIn} RBI");
		}

		if (line.Walks > 0)
		{
			parts.Add($"{line.Walks} BB");
		}

		return string.Join(", ", parts);
	}

	private static string TodayPitching(PlayerGamePitching? line)
	{
		if (line == null)
		{
			return "just entered";
		}

		string ip = $"{line.OutsRecorded / 3}.{line.OutsRecorded % 3}";
		return $"{ip} IP  {line.HitsAllowed} H  {line.RunsAllowed} R  {line.Walks} BB  {line.Strikeouts} K";
	}

	private Control RatingRow(string leftLabel, int left, string rightLabel, int right)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		row.AddChild(RatingChip(leftLabel, left));
		row.AddChild(RatingChip(rightLabel, right));
		return row;
	}

	private Control RatingChip(string label, int value)
	{
		var box = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		box.AddThemeConstantOverride("separation", 6);
		Label name = MakeText(label, _semibold, 12, TextDim, HorizontalAlignment.Left);
		name.CustomMinimumSize = new Vector2(30, 0);
		box.AddChild(name);
		var bar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = 100,
			Value = value,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0, 8),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		bar.AddThemeStyleboxOverride("background", Rounded(new Color(0.16f, 0.17f, 0.20f), 2, 0, 0));
		bar.AddThemeStyleboxOverride("fill", Rounded(RatingColor(value), 2, 0, 0));
		box.AddChild(bar);
		box.AddChild(MakeText(value.ToString(CultureInfo.InvariantCulture), _bold, 13, RatingColor(value), HorizontalAlignment.Right));
		return box;
	}

	private Control PitchCountBar(int pitches, int limit, double fatigue)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 2);
		Color color = pitches >= limit || fatigue >= 0.9 ? Urgent : pitches >= limit * 0.8 || fatigue >= 0.7 ? Accent : Green;
		string status = pitches >= limit ? "  ·  OVER HIS LIMIT" : fatigue >= 0.7 ? "  ·  TIRING" : "";
		box.AddChild(MakeText($"PITCHES {pitches} / ~{limit}{status}", _semibold, 12, color, HorizontalAlignment.Left));
		var bar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = Math.Max(limit, pitches),
			Value = pitches,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0, 6),
		};
		bar.AddThemeStyleboxOverride("background", Rounded(new Color(0.16f, 0.17f, 0.20f), 2, 0, 0));
		bar.AddThemeStyleboxOverride("fill", Rounded(color, 2, 0, 0));
		box.AddChild(bar);
		return box;
	}

	private static Color RatingColor(int value) => value switch
	{
		>= 70 => Green,
		>= 55 => Accent,
		>= 40 => TextPrimary,
		_ => Urgent,
	};

	private static string Ordinal(int n) => n switch
	{
		1 => "1ST",
		2 => "2ND",
		3 => "3RD",
		_ => $"{n}TH",
	};

	private static void Clear(Node host)
	{
		foreach (Node child in host.GetChildren())
		{
			host.RemoveChild(child);
			child.QueueFree();
		}
	}

	private PanelContainer MakeCard(float padX, float padY)
	{
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardFill,
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = padX,
			ContentMarginTop = padY,
			ContentMarginRight = padX,
			ContentMarginBottom = padY,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10,
		});
		return card;
	}

	private static StyleBoxFlat Rounded(Color bg, int radius, float padX, float padY) => new()
	{
		BgColor = bg,
		ContentMarginLeft = padX,
		ContentMarginRight = padX,
		ContentMarginTop = padY,
		ContentMarginBottom = padY,
		CornerRadiusTopLeft = radius,
		CornerRadiusTopRight = radius,
		CornerRadiusBottomRight = radius,
		CornerRadiusBottomLeft = radius,
	};

	private Button MakeAction(string text, bool primary, float stretch, Action onPressed, float minWidth = 0)
	{
		var button = new Button
		{
			Text = text,
			SizeFlagsHorizontal = stretch > 0 ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
			SizeFlagsStretchRatio = Math.Max(stretch, 0.01f),
			CustomMinimumSize = new Vector2(minWidth, 38),
			ClipText = true,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 14);
		if (primary)
		{
			button.AddThemeColorOverride("font_color", TextOnAccent);
			button.AddThemeColorOverride("font_hover_color", TextOnAccent);
			button.AddThemeColorOverride("font_pressed_color", TextOnAccent);
			button.AddThemeColorOverride("font_focus_color", TextOnAccent);
			button.AddThemeStyleboxOverride("normal", Filled(Accent));
			button.AddThemeStyleboxOverride("hover", Filled(AccentHover));
			button.AddThemeStyleboxOverride("pressed", Filled(AccentHover));
			button.AddThemeStyleboxOverride("focus", Filled(AccentHover));
		}
		else
		{
			button.AddThemeColorOverride("font_color", TextPrimary);
			button.AddThemeColorOverride("font_disabled_color", TextDim);
			button.AddThemeStyleboxOverride("normal", Outlined(CardInner, CardBorder));
			button.AddThemeStyleboxOverride("hover", Outlined(HoverBg, Accent));
			button.AddThemeStyleboxOverride("pressed", Outlined(HoverBg, Accent));
			button.AddThemeStyleboxOverride("focus", Outlined(HoverBg, Accent));
			button.AddThemeStyleboxOverride("disabled", Outlined(CardInner, Hairline));
		}

		button.Pressed += onPressed;
		return button;
	}

	private static StyleBoxFlat Filled(Color bg)
	{
		StyleBoxFlat box = Rounded(bg, 3, 14, 8);
		return box;
	}

	private static StyleBoxFlat Outlined(Color bg, Color border)
	{
		StyleBoxFlat box = Rounded(bg, 3, 14, 8);
		box.BorderColor = border;
		box.BorderWidthLeft = 1;
		box.BorderWidthTop = 1;
		box.BorderWidthRight = 1;
		box.BorderWidthBottom = 1;
		return box;
	}

	private static Label MakeText(string text, FontFile font, int size, Color color, HorizontalAlignment align, bool wrap = false)
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
}
