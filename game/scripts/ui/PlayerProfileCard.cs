using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// Shared player profile: overview, attributes, statistics, history, and
/// scouting. Used as a side card on roster/stats/scouting and in the
/// right-side inspector dock.
/// </summary>
public partial class PlayerProfileCard : PanelContainer
{
	private const string TabOverview = "overview";
	private const string TabAttributes = "attributes";
	private const string TabStatistics = "statistics";
	private const string TabHistory = "history";
	private const string TabScouting = "scouting";

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
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);

	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private ShaderMaterial _circleMaterial = null!;
	private VBoxContainer _root = null!;
	private PlayerProfileSnapshot? _snapshot;
	private string _tab = TabOverview;
	private int _seasonYear = DistrictHubData.SeasonYear;
	private string? _actionText;
	private Action? _actionHandler;
	private Action? _closeHandler;

	public bool FrameVisible { get; set; } = true;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		var shader = GD.Load<Shader>("res://assets/ui/circle_crop.gdshader");
		_circleMaterial = new ShaderMaterial { Shader = shader };
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ExpandFill;
		ClipContents = true;
		AddThemeStyleboxOverride("panel", FrameVisible ? MakeCard() : new StyleBoxEmpty());

		_root = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_root.AddThemeConstantOverride("separation", 0);
		AddChild(_root);
		if (_snapshot != null)
		{
			Rebuild();
		}
	}

	public void SetCloseHandler(Action? handler)
	{
		_closeHandler = handler;
		if (_root != null && _snapshot != null)
		{
			Rebuild();
		}
	}

	public void SetAction(string? text, Action? handler)
	{
		_actionText = text;
		_actionHandler = handler;
	}

	public void Bind(PlayerProfileSnapshot snapshot)
	{
		_snapshot = snapshot;
		_tab = TabOverview;
		_seasonYear = snapshot.Seasons.Count == 0
			? DistrictHubData.SeasonYear
			: snapshot.Seasons[^1].Year;
		AddThemeStyleboxOverride("panel", FrameVisible ? MakeCard() : new StyleBoxEmpty());
		if (_root != null)
		{
			Rebuild();
		}
	}

	public void Bind(HubSquadPlayer player, bool captain = false) => Bind(PlayerProfiles.From(player, captain));

	private void Rebuild()
	{
		if (_root == null || _snapshot == null)
		{
			return;
		}

		foreach (Node child in _root.GetChildren())
		{
			_root.RemoveChild(child);
			child.QueueFree();
		}

		_root.AddChild(BuildHeader(_snapshot));
		_root.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		_root.AddChild(BuildTabs());

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		_root.AddChild(scroll);
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
			case TabAttributes:
				BuildAttributes(content, _snapshot);
				break;
			case TabStatistics:
				BuildStatistics(content, _snapshot);
				break;
			case TabHistory:
				BuildHistory(content, _snapshot);
				break;
			case TabScouting:
				BuildScouting(content, _snapshot);
				break;
			default:
				BuildOverview(content, _snapshot);
				break;
		}
	}

	private Control BuildHeader(PlayerProfileSnapshot player)
	{
		var header = new MarginContainer();
		header.AddThemeConstantOverride("margin_left", 12);
		header.AddThemeConstantOverride("margin_top", 10);
		header.AddThemeConstantOverride("margin_right", 12);
		header.AddThemeConstantOverride("margin_bottom", 8);
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		header.AddChild(row);
		row.AddChild(BuildPortrait(player, 64));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		identity.AddThemeConstantOverride("separation", 1);
		var name = MakeText(player.DisplayName, _bold, 22, TextPrimary, HorizontalAlignment.Left);
		name.AutowrapMode = TextServer.AutowrapMode.Word;
		identity.AddChild(name);
		identity.AddChild(MakeText(
			$"{player.Position}  |  {player.YearLabel}  |  {player.BatsThrows}",
			_semibold,
			13,
			TextPrimary,
			HorizontalAlignment.Left));
		var school = MakeText(player.TeamShort.ToUpperInvariant(), _medium, 11, Accent, HorizontalAlignment.Left);
		if (!string.IsNullOrEmpty(player.TeamId))
		{
			SchoolLinks.MakeClickable(school, player.TeamId);
		}

		identity.AddChild(school);
		row.AddChild(identity);

		if (_closeHandler != null)
		{
			row.AddChild(MakeCloseButton());
		}

		if (!string.IsNullOrEmpty(_actionText) && _actionHandler != null)
		{
			row.AddChild(BuildActionButton(_actionText, _actionHandler));
		}

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
		button.AddThemeStyleboxOverride("normal", MakeFilled(Accent, 12, 8));
		button.AddThemeStyleboxOverride("hover", MakeFilled(AccentHover, 12, 8));
		button.AddThemeStyleboxOverride("pressed", MakeFilled(AccentHover, 12, 8));
		button.Pressed += () => _closeHandler?.Invoke();
		return button;
	}

	private Button BuildActionButton(string text, Action handler)
	{
		var button = new Button
		{
			Text = text,
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 11);
		button.AddThemeColorOverride("font_color", TextOnAccent);
		button.AddThemeColorOverride("font_hover_color", TextOnAccent);
		button.AddThemeStyleboxOverride("normal", MakeFilled(Accent, 12, 8));
		button.AddThemeStyleboxOverride("hover", MakeFilled(AccentHover, 12, 8));
		button.AddThemeStyleboxOverride("pressed", MakeFilled(AccentHover, 12, 8));
		button.Pressed += handler;
		return button;
	}

	private Control BuildTabs()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 2);
		AddTab(row, TabOverview, "OVERVIEW");
		AddTab(row, TabAttributes, "ATTRIBUTES");
		AddTab(row, TabStatistics, "STATISTICS");
		AddTab(row, TabHistory, "HISTORY");
		AddTab(row, TabScouting, "SCOUTING");
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
		button.AddThemeStyleboxOverride("hover", MakePill(active ? AccentHover : HoverBg));
		button.AddThemeStyleboxOverride("pressed", MakePill(active ? AccentHover : Colors.Transparent));
		string captured = key;
		button.Pressed += () =>
		{
			_tab = captured;
			Rebuild();
		};
		row.AddChild(button);
	}

	private void BuildOverview(VBoxContainer content, PlayerProfileSnapshot player)
	{
		content.AddChild(MakeText(player.DisplayName, _bold, 26, TextPrimary, HorizontalAlignment.Left));
		content.AddChild(MakeText(
			$"{player.Position}  |  {player.YearLabel}  |  {player.BatsThrows}",
			_semibold,
			14,
			TextPrimary,
			HorizontalAlignment.Left));
		content.AddChild(MakeText(
			player.Fogged ? "?  |  ? lbs" : $"{player.HeightLabel}  |  {player.WeightLbs} lbs",
			_medium,
			13,
			TextMuted,
			HorizontalAlignment.Left));

		var grades = new HBoxContainer();
		grades.AddThemeConstantOverride("separation", 16);
		grades.AddChild(BuildGrade("OVR", player.OverallLabel ?? player.Overall.ToString(CultureInfo.InvariantCulture), player.Overall));
		grades.AddChild(BuildGrade("POT", player.PotentialLabel ?? player.Potential.ToString(CultureInfo.InvariantCulture), player.Potential));
		content.AddChild(grades);

		content.AddChild(MakeSection("PERSONALITY"));
		content.AddChild(MakeText(player.Fogged && player.RecruitingHook == null ? "?" : player.Personality, _bold, 18, TextPrimary, HorizontalAlignment.Left));
		if (!string.IsNullOrEmpty(player.RecruitingHook))
		{
			content.AddChild(MakeText(player.RecruitingHook, _medium, 13, TextMuted, HorizontalAlignment.Left));
		}

		content.AddChild(MakeSection("DEVELOPMENT"));
		content.AddChild(MakeText(player.Fogged && player.RecruitingHook == null ? "?" : player.Development, _bold, 18, TextPrimary, HorizontalAlignment.Left));
		content.AddChild(MakeSection("HEALTH"));
		content.AddChild(MakeText(player.Fogged ? "?" : player.Health, _bold, 18, player.Fogged ? TextMuted : HealthColor(player.Health), HorizontalAlignment.Left));
		if (player.Agreement != null && player.Agreement.AnnualSponsorship > 0)
		{
			content.AddChild(MakeSection("SPONSORSHIP"));
			content.AddChild(MakeText(
				$"{player.Agreement.AnnualLabel} / season",
				_bold,
				18,
				Accent,
				HorizontalAlignment.Left));
		}
		if (player.IsCaptain)
		{
			content.AddChild(MakeText("CAPTAIN", _semibold, 12, Accent, HorizontalAlignment.Left));
		}
	}

	private Control BuildGrade(string label, int value) =>
		BuildGrade(label, value.ToString(CultureInfo.InvariantCulture), value);

	private Control BuildGrade(string label, string text, int colorSource)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 0);
		box.AddChild(MakeText(label, _semibold, 11, Accent, HorizontalAlignment.Left));
		Color color = text is "?" or "—" ? TextMuted : RatingColor(colorSource);
		int size = text.Length > 4 ? 22 : 32;
		box.AddChild(MakeText(text, _bold, size, color, HorizontalAlignment.Left));
		return box;
	}

	private void BuildAttributes(VBoxContainer content, PlayerProfileSnapshot player)
	{
		content.AddChild(MakeSection("HITTING"));
		content.AddChild(MakeMeter("Contact R", player.Hitting.ContactRight, player.Fogged));
		content.AddChild(MakeMeter("Contact L", player.Hitting.ContactLeft, player.Fogged));
		content.AddChild(MakeMeter("Power", player.Hitting.Power, player.Fogged));
		content.AddChild(MakeMeter("Plate Vision", player.Hitting.PlateVision, player.Fogged));

		content.AddChild(MakeSection("FIELDING"));
		content.AddChild(MakeMeter("Fielding", player.Fielding.Fielding, player.Fogged));
		content.AddChild(MakeMeter("Arm", player.Fielding.Arm, player.Fogged));
		content.AddChild(MakeMeter("Reaction", player.Fielding.Reaction, player.Fogged));

		content.AddChild(MakeSection("PHYSICAL"));
		content.AddChild(MakeMeter("Speed", player.Physical.Speed, player.Fogged));
		content.AddChild(MakeMeter("Strength", player.Physical.Strength, player.Fogged));

		if (player.Pitching != null)
		{
			content.AddChild(MakeSection("PITCHING"));
			content.AddChild(MakeMeter("Stuff", player.Pitching.Stuff, player.Fogged));
			content.AddChild(MakeMeter("Control", player.Pitching.Control, player.Fogged));
			content.AddChild(MakeMeter("Movement", player.Pitching.Movement, player.Fogged));
			content.AddChild(MakeMeter("Stamina", player.Pitching.Stamina, player.Fogged));
		}
	}

	private void BuildStatistics(VBoxContainer content, PlayerProfileSnapshot player)
	{
		if (player.Seasons.Count == 0)
		{
			content.AddChild(MakeText(player.Fogged ? "No tryout stats yet." : "No season stats on file.", _medium, 13, TextMuted, HorizontalAlignment.Left));
			return;
		}

		HubPlayerSeason? season = FindSeason(player, _seasonYear);
		if (season == null)
		{
			season = player.Seasons[^1];
			_seasonYear = season.Year;
		}

		var title = new HBoxContainer();
		title.AddThemeConstantOverride("separation", 8);
		title.AddChild(MakeText(player.StatsHeader ?? $"{season.Year} SEASON", _semibold, 13, TextPrimary, HorizontalAlignment.Left));
		if (player.Seasons.Count > 1)
		{
			title.AddChild(BuildYearPills(player));
		}

		var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		title.AddChild(spacer);
		title.AddChild(MakeText(
			season.Pitching != null ? "PITCHING" : "BATTING",
			_medium,
			10,
			Accent,
			HorizontalAlignment.Right));
		content.AddChild(title);
		content.AddChild(MakeText(season.OrganizationName.ToUpperInvariant(), _medium, 12, TextMuted, HorizontalAlignment.Left));

		if (season.Pitching != null)
		{
			content.AddChild(BuildPitchingSheet(season.Pitching));
		}

		if (season.Hitting != null)
		{
			if (season.Pitching != null)
			{
				content.AddChild(MakeText("BATTING", _medium, 10, Accent, HorizontalAlignment.Left));
			}

			content.AddChild(BuildHittingSheet(season.Hitting, season.Pitching == null));
		}
	}

	private Control BuildYearPills(PlayerProfileSnapshot player)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 3);
		foreach (HubPlayerSeason season in player.Seasons)
		{
			int year = season.Year;
			bool active = year == _seasonYear;
			var button = new Button
			{
				Text = year.ToString(CultureInfo.InvariantCulture),
				MouseDefaultCursorShape = CursorShape.PointingHand,
			};
			button.AddThemeFontOverride("font", _semibold);
			button.AddThemeFontSizeOverride("font_size", 11);
			button.AddThemeColorOverride("font_color", active ? TextOnAccent : TextPrimary);
			button.AddThemeStyleboxOverride("normal", MakePill(active ? Accent : Colors.Transparent));
			button.AddThemeStyleboxOverride("hover", MakePill(active ? AccentHover : HoverBg));
			button.Pressed += () =>
			{
				_seasonYear = year;
				Rebuild();
			};
			row.AddChild(button);
		}

		return row;
	}

	private static HubPlayerSeason? FindSeason(PlayerProfileSnapshot player, int year)
	{
		foreach (HubPlayerSeason season in player.Seasons)
		{
			if (season.Year == year)
			{
				return season;
			}
		}

		return null;
	}

	private Control BuildHittingSheet(HubSeasonHitting stats, bool headline)
	{
		var sheet = new VBoxContainer();
		sheet.AddThemeConstantOverride("separation", 10);
		if (headline)
		{
			sheet.AddChild(BuildHeadline(
			[
				(stats.Average.ToString(".000", CultureInfo.InvariantCulture), "Average"),
				(stats.HomeRuns.ToString(CultureInfo.InvariantCulture), "Home Runs"),
				(stats.Rbi.ToString(CultureInfo.InvariantCulture), "RBI"),
				(stats.Ops.ToString(".000", CultureInfo.InvariantCulture), "OPS"),
			]));
		}

		sheet.AddChild(BuildBox(
		[
			("G", stats.Games),
			("AB", stats.AtBats),
			("R", stats.Runs),
			("H", stats.Hits),
			("2B", stats.Doubles),
			("3B", stats.Triples),
			("HR", stats.HomeRuns),
			("RBI", stats.Rbi),
			("BB", stats.Walks),
			("SO", stats.Strikeouts),
			("SB", stats.StolenBases),
		]));
		sheet.AddChild(BuildTextBox(
		[
			("AVG", stats.Average.ToString(".000", CultureInfo.InvariantCulture)),
			("OBP", stats.OnBase.ToString(".000", CultureInfo.InvariantCulture)),
			("SLG", stats.Slugging.ToString(".000", CultureInfo.InvariantCulture)),
			("OPS", stats.Ops.ToString(".000", CultureInfo.InvariantCulture)),
		]));
		return sheet;
	}

	private Control BuildPitchingSheet(HubSeasonPitching stats)
	{
		var sheet = new VBoxContainer();
		sheet.AddThemeConstantOverride("separation", 10);
		sheet.AddChild(BuildHeadline(
		[
			(stats.Era.ToString("0.00", CultureInfo.InvariantCulture), "ERA"),
			($"{stats.Wins}-{stats.Losses}", "W-L"),
			(stats.Strikeouts.ToString(CultureInfo.InvariantCulture), "Strikeouts"),
			(stats.Whip.ToString("0.00", CultureInfo.InvariantCulture), "WHIP"),
		]));
		sheet.AddChild(BuildBox(
		[
			("G", stats.Games),
			("GS", stats.GamesStarted),
			("IP", stats.Innings),
			("H", stats.Hits),
			("R", stats.Runs),
			("ER", stats.EarnedRuns),
			("BB", stats.Walks),
			("SO", stats.Strikeouts),
			("HR", stats.HomeRuns),
			("SV", stats.Saves),
		]));
		return sheet;
	}

	private Control BuildHeadline(List<(string Value, string Label)> stats)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 0);
		for (int i = 0; i < stats.Count; i++)
		{
			if (i > 0)
			{
				row.AddChild(new ColorRect
				{
					CustomMinimumSize = new Vector2(1, 0),
					Color = Hairline,
					SizeFlagsVertical = SizeFlags.ExpandFill,
				});
			}

			(string value, string label) = stats[i];
			var col = new VBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				Alignment = BoxContainer.AlignmentMode.Center,
			};
			col.AddThemeConstantOverride("separation", 0);
			col.AddChild(MakeText(value, _bold, 24, TextPrimary, HorizontalAlignment.Center));
			col.AddChild(MakeText(label.ToUpperInvariant(), _medium, 10, TextMuted, HorizontalAlignment.Center));
			row.AddChild(col);
		}

		return row;
	}

	private Control BuildBox(List<(string Abbr, object Value)> columns)
	{
		var items = new List<(string, string)>(columns.Count);
		foreach ((string abbr, object value) in columns)
		{
			items.Add((abbr, FormatStat(value)));
		}

		return BuildTextBox(items);
	}

	private Control BuildTextBox(List<(string Abbr, string Value)> columns)
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", MakeInner(8, 8));
		var table = new VBoxContainer();
		table.AddThemeConstantOverride("separation", 4);
		panel.AddChild(table);
		var headers = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var values = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		foreach ((string abbr, string value) in columns)
		{
			var header = MakeText(abbr, _semibold, 10, TextMuted, HorizontalAlignment.Center);
			header.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			headers.AddChild(header);
			var amount = MakeText(value, _bold, 14, TextPrimary, HorizontalAlignment.Center);
			amount.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			values.AddChild(amount);
		}

		table.AddChild(headers);
		table.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = Hairline });
		table.AddChild(values);
		return panel;
	}

	private void BuildHistory(VBoxContainer content, PlayerProfileSnapshot player)
	{
		content.AddChild(MakeSection("PLAYER HISTORY"));
		if (player.History.Count == 0)
		{
			content.AddChild(MakeText(
				player.Fogged ? "No file on this prospect." : "No history on file.",
				_medium,
				13,
				TextMuted,
				HorizontalAlignment.Left));
			return;
		}
		foreach (PlayerHistoryBeat beat in player.History)
		{
			var row = new PanelContainer();
			row.AddThemeStyleboxOverride("panel", MakeRow(false));
			var box = new VBoxContainer();
			box.AddThemeConstantOverride("separation", 0);
			row.AddChild(box);
			var top = new HBoxContainer();
			var year = MakeText(beat.Year, _bold, 14, TextPrimary, HorizontalAlignment.Left);
			year.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			top.AddChild(year);
			top.AddChild(MakeText(beat.Organization.ToUpperInvariant(), _semibold, 12, Accent, HorizontalAlignment.Right));
			box.AddChild(top);
			var detail = MakeText(beat.Detail, _medium, 12, TextMuted, HorizontalAlignment.Left);
			detail.AutowrapMode = TextServer.AutowrapMode.Word;
			box.AddChild(detail);
			content.AddChild(row);
		}
	}

	private void BuildScouting(VBoxContainer content, PlayerProfileSnapshot player)
	{
		PlayerScoutReport scout = player.Scout;
		content.AddChild(MakeSection("ROLE"));
		content.AddChild(MakeText(scout.Role, _bold, 18, TextPrimary, HorizontalAlignment.Left));
		content.AddChild(MakeSection("PROJECTION"));
		content.AddChild(MakeText(scout.Projection, _semibold, 15, Accent, HorizontalAlignment.Left));
		content.AddChild(MakeSection("REPORT"));
		var summary = MakeText(scout.Summary, _medium, 13, TextPrimary, HorizontalAlignment.Left);
		summary.AutowrapMode = TextServer.AutowrapMode.Word;
		content.AddChild(summary);
		content.AddChild(MakeSection("COMPARABLE"));
		content.AddChild(MakeText(scout.Comparable, _semibold, 14, TextMuted, HorizontalAlignment.Left));
		content.AddChild(MakeStat("Present Grade", player.Fogged ? "?" : scout.PresentGrade.ToString(CultureInfo.InvariantCulture), player.Fogged ? TextMuted : RatingColor(scout.PresentGrade)));
		content.AddChild(MakeStat("Future Grade", player.PotentialLabel ?? player.Potential.ToString(CultureInfo.InvariantCulture), player.Fogged ? TextMuted : RatingColor(player.Potential)));
	}

	private Control BuildPortrait(PlayerProfileSnapshot player, float size)
	{
		var wrap = new Control { CustomMinimumSize = new Vector2(size, size) };
		wrap.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		wrap.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		if (!string.IsNullOrEmpty(player.PortraitPath))
		{
			var portrait = new TextureRect
			{
				Texture = GD.Load<Texture2D>(player.PortraitPath),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				Material = _circleMaterial,
				CustomMinimumSize = new Vector2(size, size),
			};
			portrait.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			wrap.AddChild(portrait);
			return wrap;
		}

		int radius = Mathf.RoundToInt(size / 2f);
		var fallback = new PanelContainer();
		fallback.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		fallback.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = HoverBg,
			BorderColor = Accent,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomRight = radius,
			CornerRadiusBottomLeft = radius,
		});
		var initial = MakeText(
			player.LastName.Length == 0 ? "?" : player.LastName[..1].ToUpperInvariant(),
			_bold,
			Mathf.RoundToInt(size * 0.38f),
			Accent,
			HorizontalAlignment.Center);
		initial.VerticalAlignment = VerticalAlignment.Center;
		fallback.AddChild(initial);
		wrap.AddChild(fallback);
		return wrap;
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

	private Control MakeMeter(string label, int value, bool fogged = false)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 2);
		var row = new HBoxContainer();
		var left = MakeText(label, _medium, 13, TextMuted, HorizontalAlignment.Left);
		left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(left);
		row.AddChild(MakeText(
			fogged ? "?" : value.ToString(CultureInfo.InvariantCulture),
			_bold,
			16,
			fogged ? TextMuted : RatingColor(value),
			HorizontalAlignment.Right));
		box.AddChild(row);
		var bar = new ProgressBar
		{
			CustomMinimumSize = new Vector2(0, 6),
			MaxValue = 99,
			Value = fogged ? 0 : value,
			ShowPercentage = false,
		};
		bar.AddThemeStyleboxOverride("background", MakeFlat(BarBg, 3));
		bar.AddThemeStyleboxOverride("fill", MakeFlat(fogged ? Hairline : RatingColor(value), 3));
		box.AddChild(bar);
		return box;
	}

	private static Label MakeText(string text, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		var label = new Label { Text = text, HorizontalAlignment = align };
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static string FormatStat(object value) =>
		value switch
		{
			float number => number.ToString("0.0", CultureInfo.InvariantCulture),
			int integer => integer.ToString(CultureInfo.InvariantCulture),
			_ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
		};

	private static Color RatingColor(int value) =>
		value >= 80 ? Green : value >= 60 ? Accent : TextMuted;

	private static Color HealthColor(string health) =>
		health.Equals("Healthy", StringComparison.OrdinalIgnoreCase) ? Green
			: health.Contains("Day", StringComparison.OrdinalIgnoreCase) ? Accent
			: Urgent;

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
		};
	}

	private static StyleBoxFlat MakeInner(float padX, float padY)
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

	private static StyleBoxFlat MakeFilled(Color bg, float padX, float padY)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			ContentMarginLeft = padX,
			ContentMarginTop = padY,
			ContentMarginRight = padX,
			ContentMarginBottom = padY,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
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
