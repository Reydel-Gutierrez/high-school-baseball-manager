using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// League world map: four districts, 32 programs, then drill into a
/// district or a school. Standings, rankings, and the rest of the league
/// desk stay one click away in the same menu.
/// </summary>
public partial class LeagueOverviewPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color UserRow = new(0.956863f, 0.643137f, 0.109804f, 0.14f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;

	private Label _subtitle = null!;
	private Label _countValue = null!;
	private Label _countLabel = null!;
	private HBoxContainer _filterRow = null!;
	private Control _bodyHost = null!;
	private SchoolProfileCard? _schoolCard;
	private string? _focusedDistrict;
	private string _selectedSchoolId = DistrictHubData.UserTeamId;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		VisibilityChanged += OnVisibilityChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
		}

		VisibilityChanged -= OnVisibilityChanged;
	}

	public void ShowWorld()
	{
		_focusedDistrict = null;
		Refresh();
	}

	public void ShowDistrict(string districtId)
	{
		_focusedDistrict = districtId;
		List<HubStandingRow> rows = DistrictHubData.StandingsFor(districtId, Level());
		if (rows.Count > 0)
		{
			bool keep = false;
			foreach (HubStandingRow row in rows)
			{
				if (row.Team.Id == _selectedSchoolId)
				{
					keep = true;
					break;
				}
			}

			if (!keep)
			{
				_selectedSchoolId = rows[0].Team.Id;
			}
		}

		Refresh();
	}

	public void SelectSchool(string teamId)
	{
		_selectedSchoolId = SchoolProfiles.NormalizeId(teamId);
		HubTeam team = DistrictHubData.GetTeam(_selectedSchoolId);
		_focusedDistrict = team.DistrictId;
		Refresh();
	}

	private void OnActiveTeamChanged(TeamIdentity _) => Refresh();

	private void OnVisibilityChanged()
	{
		if (Visible)
		{
			Refresh();
		}
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

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		margin.AddChild(layout);

		layout.AddChild(BuildHeader());

		_filterRow = new HBoxContainer();
		_filterRow.AddThemeConstantOverride("separation", 4);
		layout.AddChild(_filterRow);

		_bodyHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		layout.AddChild(_bodyHost);
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
		identity.AddThemeConstantOverride("separation", -2);
		identity.AddChild(MakeText($"LEAGUE  ·  {DistrictHubData.SeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("HIGH SCHOOL BASEBALL", _bold, 26, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("FOUR DISTRICTS  ·  32 PROGRAMS", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var countBlock = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		countBlock.AddThemeConstantOverride("separation", -4);
		_countValue = MakeText("32", _bold, 28, TextPrimary, HorizontalAlignment.Right);
		countBlock.AddChild(_countValue);
		_countLabel = MakeText("PROGRAMS", _semibold, 11, TextMuted, HorizontalAlignment.Right);
		countBlock.AddChild(_countLabel);
		header.AddChild(countBlock);
		return header;
	}

	private void Refresh()
	{
		if (_bodyHost == null || _session == null)
		{
			return;
		}

		ApplyHeaderCopy();
		RebuildFilters();
		RebuildBody();
	}

	private void ApplyHeaderCopy()
	{
		bool varsity = Level() == TeamLevel.Varsity;
		string level = varsity ? "VARSITY" : "JV";
		if (_focusedDistrict == null)
		{
			_subtitle.Text = $"{level}  ·  FOUR DISTRICTS  ·  32 PROGRAMS";
			_countValue.Text = $"{DistrictHubData.Teams.Count}";
			_countLabel.Text = "PROGRAMS";
			return;
		}

		HubDistrict district = DistrictHubData.GetDistrict(_focusedDistrict);
		_subtitle.Text = $"{level}  ·  {district.Name.ToUpperInvariant()} DISTRICT  ·  {district.Region.ToUpperInvariant()} REGION";
		_countValue.Text = "8";
		_countLabel.Text = "PROGRAMS";
	}

	private void RebuildFilters()
	{
		foreach (Node child in _filterRow.GetChildren())
		{
			_filterRow.RemoveChild(child);
			child.QueueFree();
		}

		_filterRow.Visible = true;

		AddFilter("ALL DISTRICTS", ShowWorld, _focusedDistrict == null);

		foreach (HubDistrict district in DistrictHubData.Districts)
		{
			string id = district.Id;
			AddFilter(district.Name.ToUpperInvariant(), () => ShowDistrict(id), _focusedDistrict == id);
		}

		if (_focusedDistrict != null)
		{
			string districtId = _focusedDistrict;
			var standings = new Button { Text = "STANDINGS" };
			standings.Pressed += () => OpenDistrictStandings(districtId);
			StyleActionPill(standings);
			_filterRow.AddChild(standings);
		}
	}

	private void AddFilter(string label, System.Action onPressed, bool active)
	{
		var button = new Button { Text = label };
		button.Pressed += onPressed;
		StylePill(button, active);
		_filterRow.AddChild(button);
	}

	private void RebuildBody()
	{
		foreach (Node child in _bodyHost.GetChildren())
		{
			_bodyHost.RemoveChild(child);
			child.QueueFree();
		}

		_schoolCard = null;

		if (_focusedDistrict == null)
		{
			_bodyHost.AddChild(BuildWorldBody());
			return;
		}

		_bodyHost.AddChild(BuildDistrictBody());
	}

	private Control BuildWorldBody()
	{
		var layout = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		layout.AddThemeConstantOverride("separation", 8);

		layout.AddChild(BuildRegionRow("EAST", DistrictHubData.Districts[0], DistrictHubData.Districts[1]));
		layout.AddChild(BuildRegionRow("WEST", DistrictHubData.Districts[2], DistrictHubData.Districts[3]));
		return layout;
	}

	private Control BuildRegionRow(string region, HubDistrict left, HubDistrict right)
	{
		var block = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		block.AddThemeConstantOverride("separation", 4);
		block.AddChild(MakeText(region + " REGION", _semibold, 11, Accent, HorizontalAlignment.Left));

		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		row.AddThemeConstantOverride("separation", 8);
		row.AddChild(BuildDistrictCard(left));
		row.AddChild(BuildDistrictCard(right));
		block.AddChild(row);
		return block;
	}

	private Control BuildDistrictCard(HubDistrict district)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(0));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);

		layout.AddChild(BuildDistrictHeading(district, compact: true));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		layout.AddChild(BuildCompactColumnHeader());

		List<HubStandingRow> standings = DistrictHubData.StandingsFor(district.Id, Level());
		string userId = UserTeamId();
		for (int i = 0; i < standings.Count; i++)
		{
			layout.AddChild(BuildCompactRow(standings[i], i % 2 == 0, userId));
		}

		return card;
	}

	private Control BuildDistrictBody()
	{
		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		row.AddThemeConstantOverride("separation", 8);

		row.AddChild(BuildDistrictTable(_focusedDistrict!));

		_schoolCard = new SchoolProfileCard
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1f,
		};
		row.AddChild(_schoolCard);
		_schoolCard.Bind(_selectedSchoolId, Level());
		return row;
	}

	private Control BuildDistrictTable(string districtId)
	{
		HubDistrict district = DistrictHubData.GetDistrict(districtId);
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.55f,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(0));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);

		layout.AddChild(BuildDistrictHeading(district, compact: false));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		layout.AddChild(BuildFullColumnHeader());

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);

		var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		rows.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(rows);

		List<HubStandingRow> standings = DistrictHubData.StandingsFor(districtId, Level());
		string userId = UserTeamId();
		for (int i = 0; i < standings.Count; i++)
		{
			rows.AddChild(BuildFullRow(standings[i], i % 2 == 0, userId));
		}

		return card;
	}

	private Control BuildDistrictHeading(HubDistrict district, bool compact)
	{
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047059f, 0.054902f, 0.070588f, 0.96f),
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		});
		header.MouseFilter = MouseFilterEnum.Stop;
		header.MouseDefaultCursorShape = CursorShape.PointingHand;
		string districtId = district.Id;
		header.GuiInput += (InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				ShowDistrict(districtId);
				header.AcceptEvent();
			}
		};

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		row.AddChild(MakeText(district.Name.ToUpperInvariant() + " DISTRICT", _bold, compact ? 15 : 16, TextPrimary, HorizontalAlignment.Left));
		row.AddChild(MakeText(district.Region.ToUpperInvariant(), _semibold, 12, TextMuted, HorizontalAlignment.Left));
		var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddChild(spacer);

		List<HubStandingRow> standings = DistrictHubData.StandingsFor(district.Id, Level());
		if (standings.Count > 0)
		{
			row.AddChild(MakeText(standings[0].Team.ShortName + "  " + standings[0].Team.Record(Level()), _semibold, 12, Accent, HorizontalAlignment.Right));
		}

		if (district.Id == DistrictHubData.GetTeam(UserTeamId()).DistrictId)
		{
			row.AddChild(MakeTag("YOUR DISTRICT", Accent, TextOnAccent));
		}

		header.AddChild(row);
		return header;
	}

	private Control BuildCompactColumnHeader()
	{
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", ColumnHeaderStyle());
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		columns.AddChild(MakeFixed("#", 22, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		var team = MakeText("PROGRAM", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		team.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(team);
		columns.AddChild(MakeFixed("W-L", 52, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		header.AddChild(columns);
		return header;
	}

	private Control BuildFullColumnHeader()
	{
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", ColumnHeaderStyle());
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(MakeFixed("#", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		var team = MakeText("PROGRAM", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		team.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(team);
		columns.AddChild(MakeFixed("W-L", 56, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		columns.AddChild(MakeFixed("PCT", 48, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		columns.AddChild(MakeFixed("GB", 40, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		columns.AddChild(MakeFixed("STR", 44, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		header.AddChild(columns);
		return header;
	}

	private Control BuildCompactRow(HubStandingRow row, bool even, string userId)
	{
		PanelContainer panel = MakeRowPanel(row.Team.Id, even, userId);
		SchoolLinks.MakeClickable(panel, row.Team.Id);

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		columns.AddChild(MakeFixed($"{row.Rank}", 22, _bold, 13, row.Qualifies ? Green : TextMuted, HorizontalAlignment.Center));
		columns.AddChild(BuildTeamIdentity(row.Team.Id, row.Team.ShortName, row.Team.Id == userId, 13, 22));
		columns.AddChild(MakeFixed(row.Team.Record(Level()), 52, _bold, 13, TextPrimary, HorizontalAlignment.Right));
		panel.AddChild(columns);
		return panel;
	}

	private Control BuildFullRow(HubStandingRow row, bool even, string userId)
	{
		PanelContainer panel = MakeRowPanel(row.Team.Id, even, userId);
		SchoolLinks.MakeClickable(panel, row.Team.Id);

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(MakeFixed($"{row.Rank}", 28, _bold, 14, row.Qualifies ? Green : TextMuted, HorizontalAlignment.Center));
		columns.AddChild(BuildTeamIdentity(row.Team.Id, row.Team.Name.ToUpperInvariant(), row.Team.Id == userId, 15, 26));
		columns.AddChild(MakeFixed(row.Team.Record(Level()), 56, _bold, 15, TextPrimary, HorizontalAlignment.Right));
		columns.AddChild(MakeFixed(row.Pct, 48, _semibold, 14, TextMuted, HorizontalAlignment.Right));
		columns.AddChild(MakeFixed(row.GamesBack, 40, _semibold, 14, TextMuted, HorizontalAlignment.Right));
		columns.AddChild(MakeFixed(row.Streak, 44, _bold, 13, row.Streak.StartsWith('W') ? Green : new Color(0.906f, 0.298f, 0.235f), HorizontalAlignment.Right));
		panel.AddChild(columns);
		return panel;
	}

	private PanelContainer MakeRowPanel(string teamId, bool even, string userId)
	{
		bool user = teamId == userId;
		bool selected = teamId == _selectedSchoolId && _focusedDistrict != null;
		var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = selected || user ? UserRow : even ? RowEven : RowOdd,
			BorderColor = selected || user ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = selected || user ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 7,
			ContentMarginRight = 12,
			ContentMarginBottom = 7,
		});
		return panel;
	}

	private Control BuildTeamIdentity(string teamId, string name, bool user, int fontSize, float logoSize)
	{
		var identity = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		identity.AddThemeConstantOverride("separation", 8);
		TextureRect? logo = TeamLogos.TryMakeIcon(teamId, logoSize);
		if (logo != null)
		{
			identity.AddChild(logo);
		}

		var label = MakeText(name.ToUpperInvariant(), user ? _bold : _semibold, fontSize, user ? Accent : TextPrimary, HorizontalAlignment.Left);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		identity.AddChild(label);
		return identity;
	}

	private void OpenDistrictStandings(string districtId)
	{
		Node? node = this;
		while (node != null)
		{
			if (node is AppNavigator navigator)
			{
				navigator.ShowStandingsForDistrict(districtId);
				return;
			}

			node = node.GetParent();
		}
	}

	private TeamLevel Level() => _session.ActiveTeam.Level;

	private string UserTeamId()
	{
		string catalog = SchoolProfiles.NormalizeId(_session.CatalogOrganizationId);
		return string.IsNullOrWhiteSpace(catalog) ? DistrictHubData.UserTeamId : catalog;
	}

	private void StylePill(Button button, bool isActive)
	{
		button.Flat = false;
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", isActive ? TextOnAccent : TextPrimary);
		button.AddThemeColorOverride("font_hover_color", isActive ? TextOnAccent : Colors.White);
		button.AddThemeColorOverride("font_pressed_color", isActive ? TextOnAccent : TextPrimary);
		button.AddThemeColorOverride("font_focus_color", isActive ? TextOnAccent : Colors.White);
		button.AddThemeStyleboxOverride("normal", MakePill(isActive ? Accent : Colors.Transparent, 12));
		button.AddThemeStyleboxOverride("hover", MakePill(isActive ? AccentHover : HoverBg, 12));
		button.AddThemeStyleboxOverride("pressed", MakePill(isActive ? AccentHover : HoverBg, 12));
		button.AddThemeStyleboxOverride("focus", MakePill(isActive ? AccentHover : HoverBg, 12));
	}

	private void StyleActionPill(Button button)
	{
		button.Flat = false;
		button.MouseDefaultCursorShape = CursorShape.PointingHand;
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeStyleboxOverride("normal", MakePill(Colors.Transparent, 12));
		button.AddThemeStyleboxOverride("hover", MakePill(HoverBg, 12));
		button.AddThemeStyleboxOverride("pressed", MakePill(HoverBg, 12));
		button.AddThemeStyleboxOverride("focus", MakePill(HoverBg, 12));
	}

	private PanelContainer MakeTag(string text, Color border, Color font)
	{
		var pill = new PanelContainer();
		pill.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(border.R, border.G, border.B, 1f),
			BorderColor = border,
			ContentMarginLeft = 7,
			ContentMarginTop = 3,
			ContentMarginRight = 7,
			ContentMarginBottom = 3,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		pill.AddChild(MakeText(text, _bold, 10, font, HorizontalAlignment.Center));
		return pill;
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

	private static StyleBoxFlat ColumnHeaderStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.047059f, 0.054902f, 0.070588f, 0.96f),
			BorderColor = CardBorder,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 7,
			ContentMarginRight = 12,
			ContentMarginBottom = 7,
		};
	}

	private static StyleBoxFlat MakeCardStyle(float pad)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.078431f, 0.094118f, 0.94f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = pad,
			ContentMarginTop = pad,
			ContentMarginRight = pad,
			ContentMarginBottom = pad,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		};
	}

	private static StyleBoxFlat MakePill(Color bg, float padX, float padY = 5)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			DrawCenter = true,
			ContentMarginLeft = padX,
			ContentMarginTop = padY,
			ContentMarginRight = padX,
			ContentMarginBottom = padY,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerDetail = 5,
		};
	}
}
