using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// League standings. District uses one table at a time with district
/// pills. Regional is one East or West bracket. State is only the title game.
/// </summary>
public partial class StandingsPage : Control
{
	private const string ViewDistrict = "district";
	private const string ViewRegional = "regional";
	private const string ViewState = "state";
	private const string RegionalEast = "east";
	private const string RegionalWest = "west";

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.772549f, 0.635294f, 0.325490f, 1f);
	private static readonly Color AccentHover = new(0.850980f, 0.729412f, 0.443137f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardInner = new(0.090196f, 0.105882f, 0.141176f, 1f);
	private static readonly Color CardBorder = new(0.137255f, 0.152941f, 0.200000f, 1f);
	private static readonly Color Hairline = new(0.17f, 0.19f, 0.24f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color UserRow = new(0.772549f, 0.635294f, 0.325490f, 0.14f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;

	private readonly Dictionary<string, Button> _stageButtons = new();
	private readonly Dictionary<string, Button> _subButtons = new();

	private Label _subtitle = null!;
	private Label _countValue = null!;
	private Label _countLabel = null!;
	private HBoxContainer _subFilterRow = null!;
	private VBoxContainer _bodyHost = null!;
	private SchoolProfileCard _schoolCard = null!;
	private string _selectedSchoolId = DistrictHubData.UserTeamId;
	private string _activeView = ViewDistrict;
	private string _activeDistrict = DistrictHubData.UserDistrictId;
	private string _activeRegional = RegionalEast;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		_session.CareerChanged += Refresh;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
			_session.CareerChanged -= Refresh;
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => Refresh();

	public void SelectSchool(string teamId)
	{
		_boundUserProgram = UserProgram.OrganizationId;
		_selectedSchoolId = SchoolProfiles.NormalizeId(teamId);
		HubTeam team = DistrictHubData.GetTeam(_selectedSchoolId);
		_activeDistrict = team.DistrictId;
		_activeView = ViewDistrict;
		Refresh();
	}

	public void ShowDistrictTable(string districtId)
	{
		_boundUserProgram = UserProgram.OrganizationId;
		_activeDistrict = districtId;
		_activeView = ViewDistrict;
		Refresh();
	}

	public void ShowLeagueTable()
	{
		_activeView = ViewDistrict;
		Refresh();
	}

	public void ShowPlayoffBracket()
	{
		_boundUserProgram = UserProgram.OrganizationId;
		HubDistrict district = DistrictHubData.GetDistrict(DistrictHubData.GetTeam(_selectedSchoolId).DistrictId);
		_activeRegional = string.Equals(district.Region, "West", System.StringComparison.OrdinalIgnoreCase)
			? RegionalWest
			: RegionalEast;
		_activeView = ViewRegional;
		Refresh();
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
		layout.AddChild(BuildStageFilters());

		_subFilterRow = new HBoxContainer();
		_subFilterRow.AddThemeConstantOverride("separation", 4);
		layout.AddChild(_subFilterRow);

		var bodyRow = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		bodyRow.AddThemeConstantOverride("separation", 8);
		layout.AddChild(bodyRow);

		_bodyHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.55f,
		};
		bodyRow.AddChild(_bodyHost);

		_schoolCard = new SchoolProfileCard
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1f,
		};
		bodyRow.AddChild(_schoolCard);
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
		identity.AddChild(MakeText($"LEAGUE  ·  {DistrictHubData.SeasonYear} SEASON", _semibold, 12, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("STANDINGS", _bold, 36, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("VARSITY  ·  GREAT LAKES DISTRICT", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var countBlock = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		countBlock.AddThemeConstantOverride("separation", -4);
		_countValue = MakeText("8", _bold, 28, TextPrimary, HorizontalAlignment.Right);
		countBlock.AddChild(_countValue);
		_countLabel = MakeText("TEAMS", _semibold, 11, TextMuted, HorizontalAlignment.Right);
		countBlock.AddChild(_countLabel);
		header.AddChild(countBlock);
		return header;
	}

	private Control BuildStageFilters()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		AddStage(row, ViewDistrict, "DISTRICT");
		AddStage(row, ViewRegional, "REGIONAL PLAYOFF");
		AddStage(row, ViewState, "STATE");
		return row;
	}

	private void AddStage(HBoxContainer row, string key, string label)
	{
		var button = new Button { Text = label };
		button.Pressed += () =>
		{
			_activeView = key;
			Refresh();
		};
		_stageButtons[key] = button;
		row.AddChild(button);
	}


	private string _boundUserProgram = "";

	/// <summary>Re-center on the user's school whenever the active career changes.</summary>
	private void SyncUserSelection()
	{
		string user = UserProgram.OrganizationId;
		if (_boundUserProgram == user)
		{
			return;
		}

		_boundUserProgram = user;
		_selectedSchoolId = user;
		_activeDistrict = UserProgram.DistrictId;
	}

	private void Refresh()
	{
		SyncUserSelection();
		if (_bodyHost == null || _session == null)
		{
			return;
		}

		ApplyHeaderCopy();
		StylePills(_stageButtons, _activeView);
		RebuildSubFilters();
		RebuildBody();
	}

	private void ApplyHeaderCopy()
	{
		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		string level = varsity ? "VARSITY" : "JV";

		if (_activeView == ViewDistrict)
		{
			HubDistrict district = DistrictHubData.GetDistrict(_activeDistrict);
			_subtitle.Text = $"{level}  ·  {district.Name.ToUpperInvariant()} DISTRICT  ·  TOP TWO QUALIFY FOR REGIONAL";
			_countValue.Text = "8";
			_countLabel.Text = "TEAMS";
			return;
		}

		if (_activeView == ViewRegional)
		{
			bool east = _activeRegional == RegionalEast;
			_subtitle.Text = east
				? $"{level}  ·  EAST REGIONAL  ·  GREAT LAKES vs NORTH SHORE"
				: $"{level}  ·  WEST REGIONAL  ·  CAPITAL VALLEY vs IRON RANGE";
			_countValue.Text = "4";
			_countLabel.Text = "TEAMS";
			return;
		}

		_subtitle.Text = $"{level}  ·  STATE CHAMPIONSHIP  ·  EAST CHAMPION vs WEST CHAMPION";
		_countValue.Text = "2";
		_countLabel.Text = "TEAMS";
	}

	private void RebuildSubFilters()
	{
		foreach (Node child in _subFilterRow.GetChildren())
		{
			_subFilterRow.RemoveChild(child);
			child.QueueFree();
		}

		_subButtons.Clear();
		_subFilterRow.Visible = _activeView != ViewState;

		if (_activeView == ViewDistrict)
		{
			foreach (HubDistrict district in DistrictHubData.Districts)
			{
				AddSubFilter(district.Id, district.Name.ToUpperInvariant(), () =>
				{
					_activeDistrict = district.Id;
					Refresh();
				});
			}

			StylePills(_subButtons, _activeDistrict);
			return;
		}

		if (_activeView == ViewRegional)
		{
			AddSubFilter(RegionalEast, "EAST", () =>
			{
				_activeRegional = RegionalEast;
				Refresh();
			});
			AddSubFilter(RegionalWest, "WEST", () =>
			{
				_activeRegional = RegionalWest;
				Refresh();
			});
			StylePills(_subButtons, _activeRegional);
		}
	}

	private void AddSubFilter(string key, string label, System.Action onPressed)
	{
		var button = new Button { Text = label };
		button.Pressed += onPressed;
		_subButtons[key] = button;
		_subFilterRow.AddChild(button);
	}

	private void RebuildBody()
	{
		foreach (Node child in _bodyHost.GetChildren())
		{
			_bodyHost.RemoveChild(child);
			child.QueueFree();
		}

		Control body = _activeView switch
		{
			ViewRegional => BuildRegionalBody(),
			ViewState => BuildStateBody(),
			_ => BuildDistrictBody(),
		};
		body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		body.SizeFlagsVertical = SizeFlags.ExpandFill;
		_bodyHost.AddChild(body);
		_schoolCard.Bind(_selectedSchoolId, _session.ActiveTeam.Level);
	}

	private Control BuildDistrictBody()
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

		layout.AddChild(BuildTableHeading());
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		layout.AddChild(BuildStandingsColumnHeader());

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

		List<HubStandingRow> standings = DistrictHubData.StandingsFor(_activeDistrict, _session.ActiveTeam.Level);
		for (int i = 0; i < standings.Count; i++)
		{
			rows.AddChild(BuildStandingRow(standings[i], i % 2 == 0));
			if (i == 1)
			{
				rows.AddChild(BuildPlayoffLine());
			}
		}

		return card;
	}

	private Control BuildTableHeading()
	{
		HubDistrict district = DistrictHubData.GetDistrict(_activeDistrict);
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		});

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		row.AddChild(MakeText(district.Name.ToUpperInvariant() + " DISTRICT", _bold, 16, TextPrimary, HorizontalAlignment.Left));
		row.AddChild(MakeText(district.Region.ToUpperInvariant() + " REGION", _semibold, 12, TextMuted, HorizontalAlignment.Left));
		var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddChild(spacer);
		if (district.Id == DistrictHubData.UserDistrictId)
		{
			row.AddChild(MakeTag("YOUR DISTRICT", Accent, TextOnAccent));
		}

		header.AddChild(row);
		return header;
	}

	private Control BuildStandingsColumnHeader()
	{
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			BorderColor = CardBorder,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 7,
			ContentMarginRight = 12,
			ContentMarginBottom = 7,
		});

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(MakeFixed("#", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		var team = MakeText("TEAM", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		team.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(team);
		columns.AddChild(MakeFixed("W", 40, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("L", 40, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("PCT", 52, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("GB", 44, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("RF", 44, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("RA", 44, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("STR", 48, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed("STATUS", 88, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		header.AddChild(columns);
		return header;
	}

	private Control BuildStandingRow(HubStandingRow row, bool even)
	{
		bool user = row.Team.IsUserTeam;
		bool selected = row.Team.Id == _selectedSchoolId;
		var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = selected || user ? UserRow : even ? RowEven : RowOdd,
			BorderColor = selected || user ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = selected || user ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		});
		SchoolLinks.MakeClickable(panel, row.Team.Id);

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(MakeFixed($"{row.Rank}", 28, _bold, 14, row.Qualifies ? Green : TextMuted, HorizontalAlignment.Center));
		columns.AddChild(BuildTeamIdentity(row.Team.Id, row.Team.Name.ToUpperInvariant(), user));

		columns.AddChild(MakeFixed($"{row.Wins}", 40, _bold, 14, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed($"{row.Losses}", 40, _semibold, 14, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed(row.Pct, 52, _semibold, 14, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed(row.GamesBack, 44, _semibold, 14, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed($"{row.Team.RunsFor}", 44, _semibold, 14, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed($"{row.Team.RunsAgainst}", 44, _semibold, 14, TextMuted, HorizontalAlignment.Center));

		Color streakColor = row.Streak.StartsWith('W') ? Green : Urgent;
		columns.AddChild(MakeFixed(row.Streak, 48, _bold, 13, streakColor, HorizontalAlignment.Center));
		Color raceColor = row.Race switch
		{
			RaceStatus.Clinched => Green,
			RaceStatus.Eliminated => TextMuted,
			_ => Accent,
		};
		columns.AddChild(MakeFixed(row.Race.ToLabel(), 88, _bold, 11, raceColor, HorizontalAlignment.Right));

		panel.AddChild(columns);
		return panel;
	}

	private Control BuildPlayoffLine()
	{
		var wrap = new PanelContainer();
		wrap.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.239216f, 0.862745f, 0.450980f, 0.12f),
			ContentMarginLeft = 12,
			ContentMarginTop = 4,
			ContentMarginRight = 12,
			ContentMarginBottom = 4,
		});
		wrap.AddChild(MakeText("PLAYOFF LINE  ·  1ST AND 2ND ADVANCE TO THE REGIONAL", _semibold, 10, Green, HorizontalAlignment.Center));
		return wrap;
	}

	private Control BuildRegionalBody()
	{
		PostseasonState? live = LiveBracket();
		if (live != null)
		{
			return BuildLiveRegional(live);
		}

		bool east = _activeRegional == RegionalEast;
		TeamLevel level = _session.ActiveTeam.Level;
		List<HubTeam> leftDistrict = DistrictHubData.PlayoffField(
			east ? DistrictHubData.DistrictGreatLakes : DistrictHubData.DistrictCapitalValley,
			level);
		List<HubTeam> rightDistrict = DistrictHubData.PlayoffField(
			east ? DistrictHubData.DistrictNorthShore : DistrictHubData.DistrictIronRange,
			level);

		string leftAbbr = east ? "GL" : "CV";
		string rightAbbr = east ? "NS" : "IR";
		string title = east ? "EAST REGIONAL" : "WEST REGIONAL";
		string matchup = east ? "Great Lakes District vs North Shore District" : "Capital Valley District vs Iron Range District";
		string semiDate = east ? "MAY 14" : "MAY 15";
		string finalDate = east ? "MAY 16" : "MAY 17";

		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(12));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 10);
		card.AddChild(layout);

		layout.AddChild(MakeText(title, _bold, 18, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(MakeText(matchup.ToUpperInvariant() + "  ·  1ST VS 2ND OF THE PAIRED DISTRICT", _medium, 12, TextMuted, HorizontalAlignment.Left));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });

		var bracket = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		bracket.AddThemeConstantOverride("separation", 16);
		layout.AddChild(bracket);

		var round1 = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.2f,
		};
		round1.AddThemeConstantOverride("separation", 12);
		round1.AddChild(MakeText("ROUND 1  ·  SEMIFINALS", _semibold, 11, Accent, HorizontalAlignment.Left));
		round1.AddChild(BuildMatchCard(
			$"{semiDate}  ·  GAME 1",
			leftDistrict[0],
			$"{leftAbbr} 1",
			rightDistrict[1],
			$"{rightAbbr} 2"));
		round1.AddChild(BuildMatchCard(
			$"{semiDate}  ·  GAME 2",
			rightDistrict[0],
			$"{rightAbbr} 1",
			leftDistrict[1],
			$"{leftAbbr} 2"));
		bracket.AddChild(round1);

		bracket.AddChild(BuildConnectorColumn());

		var finalCol = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		finalCol.AddThemeConstantOverride("separation", 12);
		finalCol.AddChild(MakeText("REGIONAL FINAL", _semibold, 11, Accent, HorizontalAlignment.Left));
		finalCol.AddChild(BuildTbdMatchCard($"{finalDate}  ·  WINNER GAME 1 vs WINNER GAME 2", "GAME 1 WINNER", "GAME 2 WINNER"));
		finalCol.AddChild(BuildChampionSlot(east ? "EAST CHAMPION" : "WEST CHAMPION", "TBD", "Advances to the state title"));
		bracket.AddChild(finalCol);

		return card;
	}

	private Control BuildStateBody()
	{
		PostseasonState? live = LiveBracket();
		if (live != null)
		{
			return BuildLiveState(live);
		}

		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(16));

		var layout = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		layout.AddThemeConstantOverride("separation", 14);
		card.AddChild(layout);

		layout.AddChild(MakeText("STATE CHAMPIONSHIP", _bold, 22, TextPrimary, HorizontalAlignment.Center));
		layout.AddChild(MakeText("THE TWO REGIONAL CHAMPIONS MEET FOR THE TITLE", _medium, 13, TextMuted, HorizontalAlignment.Center));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });

		var match = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		match.AddThemeConstantOverride("separation", 16);
		layout.AddChild(match);

		match.AddChild(BuildChampionEntry("EAST CHAMPION", "Winner of the East Regional", "GREAT LAKES vs NORTH SHORE"));
		match.AddChild(MakeText("VS", _bold, 20, Accent, HorizontalAlignment.Center));
		match.AddChild(BuildChampionEntry("WEST CHAMPION", "Winner of the West Regional", "CAPITAL VALLEY vs IRON RANGE"));

		layout.AddChild(BuildTbdMatchCard("JUN 6  ·  STATE TITLE GAME", "EAST CHAMPION", "WEST CHAMPION"));
		layout.AddChild(BuildChampionSlot("STATE CHAMPION", "TBD", "One game. Winner takes the state."));
		return card;
	}

	private PostseasonState? LiveBracket()
	{
		PostseasonState? bracket = _session.World?.Postseason;
		return bracket != null && bracket.Qualifiers.Count > 0 ? bracket : null;
	}

	private Control BuildLiveRegional(PostseasonState bracket)
	{
		bool east = _activeRegional == RegionalEast;
		string region = east ? "East" : "West";
		int year = GameSession.SeasonYear;
		string title = east ? "EAST REGIONAL" : "WEST REGIONAL";
		string matchup = east ? "Great Lakes District vs North Shore District" : "Capital Valley District vs Iron Range District";

		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(12));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 10);
		card.AddChild(layout);

		layout.AddChild(MakeText(title, _bold, 18, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(MakeText(matchup.ToUpperInvariant() + "  ·  BEST OF THREE", _medium, 12, TextMuted, HorizontalAlignment.Left));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });

		var bracketRow = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		bracketRow.AddThemeConstantOverride("separation", 16);
		layout.AddChild(bracketRow);

		var round1 = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.2f,
		};
		round1.AddThemeConstantOverride("separation", 12);
		round1.AddChild(MakeText("ROUND 1  ·  SEMIFINALS", _semibold, 11, Accent, HorizontalAlignment.Left));
		IReadOnlyList<PostseasonSeries> semis = bracket.SeriesIn(PostseasonRound.RegionalSemifinal, region);
		if (semis.Count == 0)
		{
			round1.AddChild(BuildTbdMatchCard($"{FormatDate(SeasonCalendar.RegionalSemis(year).Start)}  ·  GAME 1", "DISTRICT 1", "DISTRICT 2"));
			round1.AddChild(BuildTbdMatchCard($"{FormatDate(SeasonCalendar.RegionalSemis(year).Start)}  ·  GAME 2", "DISTRICT 1", "DISTRICT 2"));
		}
		else
		{
			foreach (PostseasonSeries series in semis)
			{
				round1.AddChild(BuildSeriesCard(bracket, series, SeasonCalendar.RegionalSemis(year).Start));
			}
		}

		bracketRow.AddChild(round1);
		bracketRow.AddChild(BuildConnectorColumn());

		var finalCol = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		finalCol.AddThemeConstantOverride("separation", 12);
		finalCol.AddChild(MakeText("REGIONAL FINAL", _semibold, 11, Accent, HorizontalAlignment.Left));
		IReadOnlyList<PostseasonSeries> finals = bracket.SeriesIn(PostseasonRound.RegionalFinal, region);
		if (finals.Count > 0)
		{
			finalCol.AddChild(BuildSeriesCard(bracket, finals[0], SeasonCalendar.RegionalFinal(year).Start));
		}
		else
		{
			finalCol.AddChild(BuildTbdMatchCard($"{FormatDate(SeasonCalendar.RegionalFinal(year).Start)}  ·  WINNER GAME 1 vs WINNER GAME 2", "GAME 1 WINNER", "GAME 2 WINNER"));
		}

		string champId = east ? bracket.EastChampionTeamId : bracket.WestChampionTeamId;
		string champName = string.IsNullOrEmpty(champId) ? "TBD" : HubFor(champId).ShortName.ToUpperInvariant();
		finalCol.AddChild(BuildChampionSlot(east ? "EAST CHAMPION" : "WEST CHAMPION", champName, "Advances to the state title"));
		bracketRow.AddChild(finalCol);
		return card;
	}

	private Control BuildLiveState(PostseasonState bracket)
	{
		int year = GameSession.SeasonYear;
		string eastName = string.IsNullOrEmpty(bracket.EastChampionTeamId)
			? "EAST CHAMPION"
			: HubFor(bracket.EastChampionTeamId).ShortName.ToUpperInvariant();
		string westName = string.IsNullOrEmpty(bracket.WestChampionTeamId)
			? "WEST CHAMPION"
			: HubFor(bracket.WestChampionTeamId).ShortName.ToUpperInvariant();
		string stateName = string.IsNullOrEmpty(bracket.StateChampionTeamId)
			? "TBD"
			: HubFor(bracket.StateChampionTeamId).ShortName.ToUpperInvariant();

		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(16));

		var layout = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		layout.AddThemeConstantOverride("separation", 14);
		card.AddChild(layout);

		layout.AddChild(MakeText("STATE CHAMPIONSHIP", _bold, 22, TextPrimary, HorizontalAlignment.Center));
		layout.AddChild(MakeText("THE TWO REGIONAL CHAMPIONS MEET FOR THE TITLE", _medium, 13, TextMuted, HorizontalAlignment.Center));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });

		var match = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		match.AddThemeConstantOverride("separation", 16);
		layout.AddChild(match);

		match.AddChild(BuildChampionEntry("EAST CHAMPION", eastName, "GREAT LAKES vs NORTH SHORE"));
		match.AddChild(MakeText("VS", _bold, 20, Accent, HorizontalAlignment.Center));
		match.AddChild(BuildChampionEntry("WEST CHAMPION", westName, "CAPITAL VALLEY vs IRON RANGE"));

		IReadOnlyList<PostseasonSeries> state = bracket.SeriesIn(PostseasonRound.StateChampionship);
		if (state.Count > 0)
		{
			layout.AddChild(BuildSeriesCard(bracket, state[0], SeasonCalendar.StateChampionship(year).Start));
		}
		else
		{
			layout.AddChild(BuildTbdMatchCard($"{FormatDate(SeasonCalendar.StateChampionship(year).Start)}  ·  STATE TITLE", eastName, westName));
		}

		layout.AddChild(BuildChampionSlot("STATE CHAMPION", stateName, "Best of three. Winner takes the state."));
		return card;
	}

	private Control BuildSeriesCard(PostseasonState bracket, PostseasonSeries series, DateOnly windowStart)
	{
		HubTeam home = HubFor(series.HomeTeamId);
		HubTeam away = HubFor(series.AwayTeamId);
		PostseasonSeed? homeSeed = bracket.SeedFor(series.HomeTeamId);
		PostseasonSeed? awaySeed = bracket.SeedFor(series.AwayTeamId);
		string homeTag = SeedTag(homeSeed, "H");
		string awayTag = SeedTag(awaySeed, "A");
		string label = series.IsComplete
			? $"FINAL  {series.HomeWins}\u2013{series.AwayWins}"
			: series.GamesPlayed > 0
				? $"SERIES  {series.HomeWins}\u2013{series.AwayWins}"
				: $"{FormatDate(windowStart)}  ·  BEST OF 3";
		return BuildMatchCard(label, home, homeTag, away, awayTag);
	}

	private HubTeam HubFor(string teamId)
	{
		string orgId = teamId;
		GameWorld? world = _session.World;
		if (world != null)
		{
			foreach (TeamIdentity team in world.Teams)
			{
				if (team.Id == teamId)
				{
					orgId = team.OrganizationId;
					break;
				}
			}
		}

		return DistrictHubData.GetTeam(orgId);
	}

	private static string SeedTag(PostseasonSeed? seed, string fallback)
	{
		if (seed == null)
		{
			return fallback;
		}

		HubDistrict district = DistrictHubData.GetDistrict(seed.DistrictId);
		return $"{district.Abbreviation} {seed.Seed}";
	}

	private static string FormatDate(DateOnly date) =>
		date.ToString("MMM d", CultureInfo.InvariantCulture).ToUpperInvariant();

	private Control BuildChampionEntry(string title, string detail, string path)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(240, 0),
		};
		card.AddThemeStyleboxOverride("panel", MakeInnerCard(false));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 4);
		card.AddChild(layout);
		layout.AddChild(MakeText(title, _bold, 16, TextPrimary, HorizontalAlignment.Center));
		layout.AddChild(MakeText(detail, _medium, 12, TextMuted, HorizontalAlignment.Center));
		layout.AddChild(MakeText(path, _semibold, 11, Accent, HorizontalAlignment.Center));
		return card;
	}

	private Control BuildMatchCard(string label, HubTeam home, string homeSeed, HubTeam away, string awaySeed)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", MakeInnerCard(home.IsUserTeam || away.IsUserTeam));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		card.AddChild(layout);
		layout.AddChild(MakeText(label, _semibold, 11, Accent, HorizontalAlignment.Left));
		layout.AddChild(BuildSeedRow(home, homeSeed));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = Hairline });
		layout.AddChild(BuildSeedRow(away, awaySeed));
		return card;
	}

	private Control BuildTbdMatchCard(string label, string top, string bottom)
	{
		var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		card.AddThemeStyleboxOverride("panel", MakeInnerCard(false));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		card.AddChild(layout);
		layout.AddChild(MakeText(label, _semibold, 11, Accent, HorizontalAlignment.Left));
		layout.AddChild(BuildTbdRow(top));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = Hairline });
		layout.AddChild(BuildTbdRow(bottom));
		return card;
	}

	private Control BuildSeedRow(HubTeam team, string seed)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		row.AddChild(MakeTag(seed, team.IsUserTeam ? Accent : Hairline, team.IsUserTeam ? TextOnAccent : TextMuted));
		row.AddChild(BuildTeamIdentity(team.Id, team.Name.ToUpperInvariant(), team.IsUserTeam, 16));
		row.AddChild(MakeText(team.Record(_session.ActiveTeam.Level), _semibold, 14, TextMuted, HorizontalAlignment.Right));
		SchoolLinks.MakeClickable(row, team.Id);
		return row;
	}

	private Control BuildTbdRow(string label)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 10);
		row.AddChild(MakeTag("TBD", Hairline, TextMuted));
		var name = MakeText(label, _semibold, 16, TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(name);
		return row;
	}

	private Control BuildChampionSlot(string title, string value, string detail)
	{
		var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.772549f, 0.635294f, 0.325490f, 0.12f),
			BorderColor = Accent,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 12,
			ContentMarginRight = 12,
			ContentMarginBottom = 12,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
		});

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 2);
		card.AddChild(layout);
		layout.AddChild(MakeText(title, _semibold, 11, Accent, HorizontalAlignment.Center));
		layout.AddChild(MakeText(value, _bold, 24, TextPrimary, HorizontalAlignment.Center));
		layout.AddChild(MakeText(detail, _medium, 12, TextMuted, HorizontalAlignment.Center));
		return card;
	}

	private Control BuildConnectorColumn()
	{
		var col = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(12, 0),
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		col.AddChild(new ColorRect
		{
			CustomMinimumSize = new Vector2(2, 120),
			Color = Accent,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
		});
		return col;
	}

	private void StylePills(Dictionary<string, Button> buttons, string active)
	{
		foreach (KeyValuePair<string, Button> pair in buttons)
		{
			bool isActive = pair.Key == active;
			Button button = pair.Value;
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
	}

	private Control BuildTeamIdentity(string teamId, string name, bool user, int fontSize = 14)
	{
		var identity = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		identity.AddThemeConstantOverride("separation", 8);
		TextureRect? logo = TeamLogos.TryMakeIcon(teamId, 28);
		if (logo != null)
		{
			identity.AddChild(logo);
		}

		var label = MakeText(name, user ? _bold : _semibold, fontSize, user ? Accent : TextPrimary, HorizontalAlignment.Left);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		identity.AddChild(label);
		return identity;
	}

	private PanelContainer MakeTag(string text, Color border, Color font)
	{
		var pill = new PanelContainer();
		pill.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(border.R, border.G, border.B, border == Accent ? 1f : 0.2f),
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 7,
			ContentMarginTop = 3,
			ContentMarginRight = 7,
			ContentMarginBottom = 3,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
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

	private static StyleBoxFlat MakeCardStyle(float pad)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = pad,
			ContentMarginTop = pad,
			ContentMarginRight = pad,
			ContentMarginBottom = pad,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerDetail = 3,
		};
	}

	private static StyleBoxFlat MakeInnerCard(bool user)
	{
		return new StyleBoxFlat
		{
			BgColor = user ? UserRow : CardInner,
			BorderColor = user ? Accent : Hairline,
			BorderWidthLeft = user ? 2 : 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
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
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerDetail = 5,
		};
	}
}
