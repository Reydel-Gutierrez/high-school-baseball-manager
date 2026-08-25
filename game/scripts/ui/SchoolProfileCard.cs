using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// Side-card for a school profile: overview, roster, schedule, history,
/// and records.
/// </summary>
public partial class SchoolProfileCard : PanelContainer
{
	private const string TabOverview = "overview";
	private const string TabRoster = "roster";
	private const string TabSchedule = "schedule";
	private const string TabHistory = "history";
	private const string TabRecords = "records";

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color BarBg = new(0.121569f, 0.137255f, 0.160784f, 1f);

	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private GameSession _session = null!;
	private VBoxContainer _body = null!;
	private string _teamId = DistrictHubData.UserTeamId;
	private TeamLevel _level = TeamLevel.Varsity;
	private string _tab = TabOverview;
	private SchoolSnapshot _snapshot = null!;
	private Action? _closeHandler;

	public bool FrameVisible { get; set; } = true;

	public void SetCloseHandler(Action? handler)
	{
		_closeHandler = handler;
		if (_body != null && _snapshot != null)
		{
			Rebuild();
		}
	}

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_session = GetNode<GameSession>("/root/GameSession");
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ExpandFill;
		ClipContents = true;
		AddThemeStyleboxOverride("panel", FrameVisible ? MakeCard() : new StyleBoxEmpty());

		_body = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_body.AddThemeConstantOverride("separation", 0);
		AddChild(_body);
		Bind(_teamId, _level);
	}

	public void Bind(string teamId, TeamLevel level)
	{
		_teamId = SchoolProfiles.NormalizeId(teamId);
		_level = level;
		_session ??= GetNode<GameSession>("/root/GameSession");
		AddThemeStyleboxOverride("panel", FrameVisible ? MakeCard() : new StyleBoxEmpty());
		_snapshot = SchoolProfiles.For(_teamId, _level, _session);
		Rebuild();
	}

	private void Rebuild()
	{
		if (_body == null || _snapshot == null)
		{
			return;
		}

		foreach (Node child in _body.GetChildren())
		{
			_body.RemoveChild(child);
			child.QueueFree();
		}

		_body.AddChild(BuildHeader());
		_body.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		_body.AddChild(BuildTabs());

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		_body.AddChild(scroll);
		var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		pad.AddThemeConstantOverride("margin_left", 12);
		pad.AddThemeConstantOverride("margin_top", 10);
		pad.AddThemeConstantOverride("margin_right", 12);
		pad.AddThemeConstantOverride("margin_bottom", 12);
		scroll.AddChild(pad);
		var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		content.AddThemeConstantOverride("separation", 10);
		pad.AddChild(content);

		switch (_tab)
		{
			case TabRoster:
				BuildRoster(content);
				break;
			case TabSchedule:
				BuildSchedule(content);
				break;
			case TabHistory:
				BuildHistory(content);
				break;
			case TabRecords:
				BuildRecords(content);
				break;
			default:
				BuildOverview(content);
				break;
		}
	}

	private Control BuildHeader()
	{
		var header = new MarginContainer();
		header.AddThemeConstantOverride("margin_left", 12);
		header.AddThemeConstantOverride("margin_top", 10);
		header.AddThemeConstantOverride("margin_right", 12);
		header.AddThemeConstantOverride("margin_bottom", 8);
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 4);
		header.AddChild(layout);

		var identity = new HBoxContainer();
		identity.AddThemeConstantOverride("separation", 10);
		TextureRect? logo = TeamLogos.TryMakeIcon(_snapshot.TeamId, 52);
		if (logo != null)
		{
			identity.AddChild(logo);
		}

		var names = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		names.AddThemeConstantOverride("separation", -2);
		names.AddChild(MakeText(_snapshot.DistrictName.ToUpperInvariant(), _medium, 10, Accent, HorizontalAlignment.Left));
		var title = MakeText(_snapshot.FullName.ToUpperInvariant(), _bold, 22, TextPrimary, HorizontalAlignment.Left);
		title.AutowrapMode = TextServer.AutowrapMode.Word;
		names.AddChild(title);
		names.AddChild(MakeText(
			$"{_snapshot.RecordLabel}   |   {_snapshot.PlaceLabel}",
			_semibold,
			13,
			TextPrimary,
			HorizontalAlignment.Left));
		identity.AddChild(names);
		if (_closeHandler != null)
		{
			identity.AddChild(MakeCloseButton());
		}

		layout.AddChild(identity);
		layout.AddChild(MakeText($"Program Reputation: {_snapshot.Reputation}", _semibold, 13, ReputationColor(_snapshot.Reputation), HorizontalAlignment.Left));
		return header;
	}

	private Button MakeCloseButton()
	{
		var button = new Button
		{
			Text = "CLOSE",
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 11);
		button.AddThemeColorOverride("font_color", TextOnAccent);
		button.AddThemeColorOverride("font_hover_color", TextOnAccent);
		button.AddThemeStyleboxOverride("normal", MakePill(Accent));
		button.AddThemeStyleboxOverride("hover", MakePill(AccentHover));
		button.AddThemeStyleboxOverride("pressed", MakePill(AccentHover));
		button.Pressed += () => _closeHandler?.Invoke();
		return button;
	}

	private Control BuildTabs()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 2);
		AddTab(row, TabOverview, "OVERVIEW");
		AddTab(row, TabRoster, "ROSTER");
		AddTab(row, TabSchedule, "SCHEDULE");
		AddTab(row, TabHistory, "HISTORY");
		AddTab(row, TabRecords, "RECORDS");
		return row;
	}

	private void AddTab(HBoxContainer row, string key, string label)
	{
		bool active = _tab == key;
		var button = new Button
		{
			Text = label,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 11);
		button.AddThemeColorOverride("font_color", active ? TextOnAccent : TextPrimary);
		button.AddThemeColorOverride("font_hover_color", active ? TextOnAccent : Colors.White);
		button.AddThemeStyleboxOverride("normal", MakePill(active ? Accent : Colors.Transparent));
		button.AddThemeStyleboxOverride("hover", MakePill(active ? AccentHover : new Color(0.184314f, 0.203922f, 0.239216f, 1f)));
		button.AddThemeStyleboxOverride("pressed", MakePill(active ? AccentHover : Colors.Transparent));
		string captured = key;
		button.Pressed += () =>
		{
			_tab = captured;
			Rebuild();
		};
		row.AddChild(button);
	}

	private void BuildOverview(VBoxContainer content)
	{
		content.AddChild(MakeSection("PROGRAM"));
		content.AddChild(MakeStat("Reputation", _snapshot.Reputation.ToString(), ReputationColor(_snapshot.Reputation)));
		content.AddChild(MakeStat("Facilities", _snapshot.Facilities.ToLabel(), GradeColor(_snapshot.Facilities)));
		content.AddChild(MakeStat("Recent Form", _snapshot.RecentForm, TextPrimary));
		content.AddChild(MakeStat("Board Status", _snapshot.BoardStatus, TextPrimary));
		if (_snapshot.BaseballBudget > 0)
		{
			content.AddChild(MakeSection("PROGRAM BUDGET"));
			if (_snapshot.CoachSalary > 0)
			{
				content.AddChild(MakeStat("Head Coach Salary", PlayerAgreement.FormatMoney(_snapshot.CoachSalary), Accent));
			}

			content.AddChild(MakeStat("Annual Budget", PlayerAgreement.FormatMoney(_snapshot.BaseballBudget), TextPrimary));
			content.AddChild(MakeStat("Recruiting", PlayerAgreement.FormatMoney(_snapshot.SponsorshipCommitted), TextPrimary));
			content.AddChild(MakeStat("Available", PlayerAgreement.FormatMoney(_snapshot.BudgetAvailable), TextPrimary));
			content.AddChild(MakeStat("Recruiting Pool", PlayerAgreement.FormatMoney(_snapshot.RecruitingAvailable), Accent));
		}

		content.AddChild(MakeSection("TEAM STRENGTH"));
		content.AddChild(MakeMeter("Pitching", _snapshot.Pitching));
		content.AddChild(MakeMeter("Hitting", _snapshot.Hitting));
		content.AddChild(MakeMeter("Fielding", _snapshot.Fielding));

		content.AddChild(MakeSection("PROGRAM HISTORY"));
		content.AddChild(MakeStat("State Titles", _snapshot.StateTitles.ToString(), TextPrimary));
		content.AddChild(MakeStat("Regional Titles", _snapshot.RegionalTitles.ToString(), TextPrimary));
		content.AddChild(MakeStat("District Titles", _snapshot.DistrictTitles.ToString(), TextPrimary));
	}

	private void BuildRoster(VBoxContainer content)
	{
		content.AddChild(MakeSection($"{_snapshot.Name.ToUpperInvariant()}  ·  {_level.ToLabel().ToUpperInvariant()}"));
		int index = 0;
		foreach (HubSquadPlayer player in _snapshot.Roster)
		{
			content.AddChild(BuildRosterRow(player, index % 2 == 0));
			index++;
		}
	}

	private Control BuildRosterRow(HubSquadPlayer player, bool even)
	{
		var row = new PanelContainer();
		row.AddThemeStyleboxOverride("panel", MakeRow(even));
		PlayerLinks.MakeClickable(row, _snapshot.TeamId, player.Name);
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		row.AddChild(columns);
		columns.AddChild(MakeFixed($"#{player.Jersey}", 36, _bold, 13, TextPrimary, HorizontalAlignment.Left));
		var name = MakeText(player.Name, _semibold, 13, TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(name);
		columns.AddChild(MakeFixed(player.Position, 36, _semibold, 12, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed(player.Year, 28, _medium, 12, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed($"{player.Overall}", 32, _bold, 14, ReputationColor(player.Overall), HorizontalAlignment.Right));
		return row;
	}

	private void BuildSchedule(VBoxContainer content)
	{
		content.AddChild(MakeSection("SCHEDULE"));
		if (_snapshot.Schedule.Count == 0)
		{
			content.AddChild(MakeText("No games on the board yet.", _medium, 13, TextMuted, HorizontalAlignment.Left));
			return;
		}

		int index = 0;
		foreach (SchoolGame game in _snapshot.Schedule)
		{
			content.AddChild(BuildGameRow(game, index % 2 == 0));
			index++;
		}
	}

	private Control BuildGameRow(SchoolGame game, bool even)
	{
		var row = new PanelContainer();
		row.AddThemeStyleboxOverride("panel", MakeRow(even));
		SchoolLinks.MakeClickable(row, game.OpponentId);
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		row.AddChild(layout);
		layout.AddChild(MakeText(
			game.Date.ToString("MMM d", CultureInfo.InvariantCulture).ToUpperInvariant(),
			_medium,
			10,
			Accent,
			HorizontalAlignment.Left));
		var line = new HBoxContainer();
		line.AddThemeConstantOverride("separation", 8);
		var vs = MakeText(
			$"{(game.Home ? "VS" : "@")}  {game.OpponentName.ToUpperInvariant()}",
			_semibold,
			13,
			TextPrimary,
			HorizontalAlignment.Left);
		vs.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		line.AddChild(vs);
		Color resultColor = game.ResultLabel.StartsWith('W') ? Green : game.ResultLabel.StartsWith('L') ? Urgent : TextMuted;
		line.AddChild(MakeText(game.ResultLabel, _bold, 13, resultColor, HorizontalAlignment.Right));
		layout.AddChild(line);
		layout.AddChild(MakeText(game.Time, _medium, 11, TextMuted, HorizontalAlignment.Left));
		return row;
	}

	private void BuildHistory(VBoxContainer content)
	{
		content.AddChild(MakeSection("PROGRAM HISTORY"));
		content.AddChild(BuildHistoryHeader());
		if (_snapshot.History.Count == 0)
		{
			content.AddChild(MakeText("No completed seasons on file.", _medium, 13, TextMuted, HorizontalAlignment.Left));
			return;
		}

		int index = 0;
		foreach (SchoolSeason season in _snapshot.History)
		{
			content.AddChild(BuildHistoryRow(season, index % 2 == 0));
			index++;
		}
	}

	private Control BuildHistoryHeader()
	{
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", MakeRow(true));
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 6);
		header.AddChild(columns);
		columns.AddChild(MakeFixed("YEAR", 44, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed("RECORD", 52, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed("DISTRICT", 56, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		var post = MakeText("POSTSEASON", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		post.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(post);
		return header;
	}

	private Control BuildHistoryRow(SchoolSeason season, bool even)
	{
		var row = new PanelContainer();
		row.AddThemeStyleboxOverride("panel", MakeRow(even));
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 6);
		row.AddChild(columns);
		columns.AddChild(MakeFixed(season.Year.ToString(), 44, _bold, 13, TextPrimary, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed($"{season.Wins}\u2013{season.Losses}", 52, _semibold, 13, TextPrimary, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed(Ordinal.Format(season.DistrictFinish), 56, _semibold, 13, TextPrimary, HorizontalAlignment.Center));
		var postseason = MakeText(season.Postseason, _medium, 13, PostseasonColor(season.Postseason), HorizontalAlignment.Left);
		postseason.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(postseason);
		return row;
	}

	private void BuildRecords(VBoxContainer content)
	{
		content.AddChild(MakeSection("PROGRAM RECORDS"));
		content.AddChild(MakeText("CAREER", _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		if (_snapshot.Records.Count == 0)
		{
			content.AddChild(MakeText("No career records on file yet.", _medium, 13, TextMuted, HorizontalAlignment.Left));
			return;
		}

		foreach (SchoolRecordGroup group in _snapshot.Records)
		{
			content.AddChild(MakeText(group.Category.ToUpperInvariant(), _semibold, 11, Accent, HorizontalAlignment.Left));
			int index = 0;
			foreach (SchoolRecordEntry entry in group.Leaders)
			{
				content.AddChild(BuildRecordRow(entry, index % 2 == 0));
				index++;
			}
		}
	}

	private Control BuildRecordRow(SchoolRecordEntry entry, bool even)
	{
		var row = new PanelContainer();
		row.AddThemeStyleboxOverride("panel", MakeRow(even));
		if (!string.Equals(entry.PlayerName, "Program", StringComparison.OrdinalIgnoreCase))
		{
			PlayerLinks.MakeClickable(row, _snapshot.TeamId, entry.PlayerName);
		}

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		row.AddChild(columns);
		columns.AddChild(MakeFixed($"{entry.Rank}.", 22, _bold, 13, TextMuted, HorizontalAlignment.Left));
		var name = MakeText(entry.PlayerName, _semibold, 13, TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(name);
		columns.AddChild(MakeText(entry.Value.ToString(CultureInfo.InvariantCulture), _bold, 14, TextPrimary, HorizontalAlignment.Right));
		return row;
	}

	private static Color PostseasonColor(string label)
	{
		if (string.IsNullOrWhiteSpace(label) || label == ProgramPostseason.None)
		{
			return TextMuted;
		}

		if (label.Contains("Champion", StringComparison.OrdinalIgnoreCase)
			|| label.Contains("State", StringComparison.OrdinalIgnoreCase))
		{
			return Green;
		}

		if (label.Contains("Regional", StringComparison.OrdinalIgnoreCase)
			|| label.Contains("Final", StringComparison.OrdinalIgnoreCase))
		{
			return Accent;
		}

		return TextPrimary;
	}

	private Control MakeSection(string title)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 4);
		box.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = Hairline });
		box.AddChild(MakeText(title, _semibold, 11, Accent, HorizontalAlignment.Left));
		return box;
	}

	private Control MakeStat(string label, string value, Color valueColor)
	{
		var row = new HBoxContainer();
		var left = MakeText(label, _medium, 13, TextMuted, HorizontalAlignment.Left);
		left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(left);
		row.AddChild(MakeText(value, _bold, 14, valueColor, HorizontalAlignment.Right));
		return row;
	}

	private Control MakeMeter(string label, int value)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 2);
		var row = new HBoxContainer();
		var left = MakeText(label, _medium, 13, TextMuted, HorizontalAlignment.Left);
		left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(left);
		row.AddChild(MakeText(value.ToString(), _bold, 16, ReputationColor(value), HorizontalAlignment.Right));
		box.AddChild(row);
		var bar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0, 6),
			MaxValue = 99,
			Value = value,
			ShowPercentage = false,
		};
		bar.AddThemeStyleboxOverride("background", MakeFlat(BarBg, 3));
		bar.AddThemeStyleboxOverride("fill", MakeFlat(ReputationColor(value), 3));
		box.AddChild(bar);
		return box;
	}

	private Label MakeFixed(string text, float width, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		Label label = MakeText(text, font, size, color, align);
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

	private static Color ReputationColor(int value) =>
		value >= 80 ? Green : value >= 60 ? Accent : TextMuted;

	private static Color GradeColor(LetterGrade grade) =>
		grade >= LetterGrade.AMinus ? Green : grade >= LetterGrade.BMinus ? Accent : TextMuted;

	private static StyleBoxFlat MakeCard()
	{
		return new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		};
	}

	private static StyleBoxFlat MakeRow(bool even)
	{
		return new StyleBoxFlat
		{
			BgColor = even ? RowEven : RowOdd,
			ContentMarginLeft = 8,
			ContentMarginTop = 6,
			ContentMarginRight = 8,
			ContentMarginBottom = 6,
		};
	}

	private static StyleBoxFlat MakePill(Color bg)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			ContentMarginLeft = 8,
			ContentMarginTop = 6,
			ContentMarginRight = 8,
			ContentMarginBottom = 6,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
		};
	}

	private static StyleBoxFlat MakeFlat(Color bg, int radius)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomRight = radius,
			CornerRadiusBottomLeft = radius,
		};
	}
}
