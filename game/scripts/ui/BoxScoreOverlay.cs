using System.Globalization;
using System;
using Godot;
using HSBM.Domain;

/// <summary>
/// Shared box-score sheet used by regular contests and tryout scrimmages.
/// </summary>
public partial class BoxScoreOverlay : Control
{
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
	private static readonly Color BlueJersey = new(0.22f, 0.42f, 0.78f, 1f);
	private static readonly Color WhiteJersey = new(0.88f, 0.90f, 0.93f, 1f);

	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private VBoxContainer _body = null!;
	private Label _kicker = null!;
	private Label _title = null!;
	private bool _built;

	public override void _Ready()
	{
		EnsureBuilt();
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (!Visible || !ev.IsActionPressed("ui_cancel"))
		{
			return;
		}

		UiSounds.Play(UiSound.Back);
		Close();
		GetViewport().SetInputAsHandled();
	}

	public void Open(BoxScore sheet)
	{
		EnsureBuilt();
		foreach (Node child in _body.GetChildren())
		{
			_body.RemoveChild(child);
			child.QueueFree();
		}

		bool tryout = sheet.Game.IsTryout;
		string matchup = $"{sheet.Away.ShortName}  @  {sheet.Home.ShortName}";
		string kind = tryout ? "TRYOUT" : sheet.Game.Level.ToLabel().ToUpperInvariant();
		_kicker.Text = $"BOX SCORE  ·  {kind}  ·  {sheet.Game.Date.ToString("MMMM d", CultureInfo.InvariantCulture).ToUpperInvariant()}";
		_title.Text = tryout ? sheet.Game.Headline : matchup;

		_body.AddChild(BuildHeader(sheet));
		_body.AddChild(BuildSection("GAME DATA", BuildGameData(sheet)));
		_body.AddChild(BuildSection("LINE SCORE", BuildLineScore(sheet)));
		_body.AddChild(BuildSection($"{sheet.Away.ShortName} BATTING", BuildBattingTable(sheet.Away, tryout)));
		_body.AddChild(BuildSection($"{sheet.Home.ShortName} BATTING", BuildBattingTable(sheet.Home, tryout)));
		_body.AddChild(BuildSection($"{sheet.Away.ShortName} PITCHING", BuildPitchingTable(sheet.Away, tryout)));
		_body.AddChild(BuildSection($"{sheet.Home.ShortName} PITCHING", BuildPitchingTable(sheet.Home, tryout)));

		Visible = true;
		MoveToFront();
	}

	public void Close()
	{
		Visible = false;
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
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var dim = new ColorRect
		{
			Color = new Color(0.02f, 0.03f, 0.04f, 0.78f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		dim.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				Close();
			}
		};
		AddChild(dim);

		var center = new CenterContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(780, 0),
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
		_kicker = MakeText("BOX SCORE", _medium, 10, Accent, HorizontalAlignment.Left);
		_title = MakeText("FINAL", _bold, 20, TextPrimary, HorizontalAlignment.Left);
		titles.AddChild(_kicker);
		titles.AddChild(_title);
		header.AddChild(titles);
		header.AddChild(MakeAction("CLOSE", false, Close));
		layout.AddChild(header);
		layout.AddChild(MakeAccentLine());

		var scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, 460),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		layout.AddChild(scroll);

		_body = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_body.AddThemeConstantOverride("separation", 12);
		scroll.AddChild(_body);
	}

	private Control BuildHeader(BoxScore sheet)
	{
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", MakeInnerCard(14, 12));
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		row.AddThemeConstantOverride("separation", 18);
		card.AddChild(row);

		row.AddChild(BuildClub(sheet.Away, "AWAY"));
		var score = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		score.AddThemeConstantOverride("separation", -4);
		Color result = sheet.Game.IsTryout
			? Accent
			: sheet.Game.Won ? Green : sheet.Game.TeamScore == sheet.Game.OpponentScore ? Accent : Urgent;
		score.AddChild(MakeText("FINAL", _medium, 10, TextMuted, HorizontalAlignment.Center));
		score.AddChild(MakeText($"{sheet.Away.Runs}   -   {sheet.Home.Runs}", _bold, 32, result, HorizontalAlignment.Center));
		score.AddChild(MakeText($"{sheet.Game.Time}  ·  {sheet.Game.VenueLabel}", _semibold, 11, TextMuted, HorizontalAlignment.Center));
		row.AddChild(score);
		row.AddChild(BuildClub(sheet.Home, "HOME"));
		return card;
	}

	private Control BuildClub(BoxScoreSide side, string venue)
	{
		var col = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		col.AddThemeConstantOverride("separation", 4);
		col.AddChild(MakeTeamMark(side.TeamId, 56));
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
		int innings = Math.Max(sheet.Away.InningRuns.Length, sheet.Home.InningRuns.Length);
		if (innings < 1)
		{
			innings = ClubhouseBoxScore.Innings;
		}

		var grid = new GridContainer
		{
			Columns = innings + 4,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		grid.AddThemeConstantOverride("h_separation", 6);
		grid.AddThemeConstantOverride("v_separation", 4);

		AddLineCell(grid, string.Empty, _medium, TextMuted, 90, HorizontalAlignment.Left);
		for (int inning = 1; inning <= innings; inning++)
		{
			AddLineCell(grid, inning.ToString(CultureInfo.InvariantCulture), _semibold, TextMuted, 28, HorizontalAlignment.Center);
		}

		AddLineCell(grid, "R", _bold, Accent, 32, HorizontalAlignment.Center);
		AddLineCell(grid, "H", _bold, Accent, 32, HorizontalAlignment.Center);
		AddLineCell(grid, "E", _bold, Accent, 32, HorizontalAlignment.Center);
		AddLineScoreRow(grid, sheet.Away, false, innings);
		AddLineScoreRow(grid, sheet.Home, true, innings);
		return grid;
	}

	private void AddLineScoreRow(GridContainer grid, BoxScoreSide side, bool home, int innings)
	{
		AddLineCell(grid, side.ShortName, _semibold, TextPrimary, 90, HorizontalAlignment.Left);
		for (int i = 0; i < innings; i++)
		{
			string value = home && side.SkippedLast && i == innings - 1
				? "X"
				: i < side.InningRuns.Length
					? side.InningRuns[i].ToString(CultureInfo.InvariantCulture)
					: "0";
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

	private Control BuildBattingTable(BoxScoreSide side, bool tryout)
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
				tryout ? string.Empty : side.TeamId,
				tryout ? string.Empty : batter.Name);
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

	private Control BuildPitchingTable(BoxScoreSide side, bool tryout)
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
				tryout ? string.Empty : side.TeamId,
				tryout ? string.Empty : pitcher.Name);
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

	private Control MakeTeamMark(string teamId, float size)
	{
		if (teamId == TryoutGame.BlueId || teamId == TryoutGame.WhiteId)
		{
			bool blue = teamId == TryoutGame.BlueId;
			var mark = new PanelContainer
			{
				CustomMinimumSize = new Vector2(size, size),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			mark.AddThemeStyleboxOverride("panel", new StyleBoxFlat
			{
				BgColor = blue ? BlueJersey : WhiteJersey,
				CornerRadiusTopLeft = 4,
				CornerRadiusTopRight = 4,
				CornerRadiusBottomRight = 4,
				CornerRadiusBottomLeft = 4,
			});
			var label = MakeText(blue ? "BLU" : "WHT", _bold, 14, blue ? Colors.White : new Color(0.12f, 0.14f, 0.18f), HorizontalAlignment.Center);
			label.VerticalAlignment = VerticalAlignment.Center;
			mark.AddChild(label);
			return mark;
		}

		TextureRect? logo = TeamLogos.TryMakeIcon(teamId, size);
		if (logo != null)
		{
			logo.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
			logo.MouseFilter = MouseFilterEnum.Ignore;
			SchoolLinks.MakeClickable(logo, teamId);
			return logo;
		}

		var fallback = MakeText(ClubhouseCalendar.OpponentMark(teamId), _bold, 14, Accent, HorizontalAlignment.Center);
		fallback.CustomMinimumSize = new Vector2(size, size);
		fallback.VerticalAlignment = VerticalAlignment.Center;
		SchoolLinks.MakeClickable(fallback, teamId);
		return fallback;
	}

	private Label MakeFixed(string text, float width, FontFile font, Color color)
	{
		Label label = MakeText(text, font, 12, color, HorizontalAlignment.Center);
		label.CustomMinimumSize = new Vector2(width, 0);
		return label;
	}

	private Button MakeAction(string text, bool primary, System.Action pressed)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(0, 32),
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		if (primary)
		{
			button.AddThemeColorOverride("font_color", TextOnAccent);
			button.AddThemeStyleboxOverride("normal", MakeFilled(Accent));
			button.AddThemeStyleboxOverride("hover", MakeFilled(AccentHover));
			button.AddThemeStyleboxOverride("pressed", MakeFilled(AccentHover));
		}
		else
		{
			button.AddThemeColorOverride("font_color", TextPrimary);
			button.AddThemeStyleboxOverride("normal", MakeOutlined(CardInner, CardBorder));
			button.AddThemeStyleboxOverride("hover", MakeOutlined(HoverBg, Accent));
			button.AddThemeStyleboxOverride("pressed", MakeOutlined(HoverBg, Accent));
		}

		button.Pressed += pressed;
		return button;
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

	private static ColorRect MakeAccentLine() =>
		new()
		{
			CustomMinimumSize = new Vector2(0, 2),
			Color = Accent,
		};

	private static StyleBoxFlat MakeInnerCard(float padX, float padY) =>
		new()
		{
			BgColor = CardInner,
			ContentMarginLeft = padX,
			ContentMarginTop = padY,
			ContentMarginRight = padX,
			ContentMarginBottom = padY,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		};

	private static StyleBoxFlat MakeFilled(Color bg) =>
		new()
		{
			BgColor = bg,
			ContentMarginLeft = 12,
			ContentMarginTop = 6,
			ContentMarginRight = 12,
			ContentMarginBottom = 6,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		};

	private static StyleBoxFlat MakeOutlined(Color bg, Color border) =>
		new()
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 6,
			ContentMarginRight = 12,
			ContentMarginBottom = 6,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		};
}
