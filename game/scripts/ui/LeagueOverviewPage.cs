using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// District directory (concept 12): pick one of the four districts (yours by
/// default) from the header tabs to see its eight programs, with the selected
/// school's profile beside them. Standings live on their own section tab.
/// </summary>
public partial class LeagueOverviewPage : Control
{
	private GameSession _session = null!;

	private Label _subtitle = null!;
	private HBoxContainer _filterRow = null!;
	private Control _bodyHost = null!;
	private SchoolProfileCard? _schoolCard;
	private string? _focusedDistrict;
	private string _selectedSchoolId = DistrictHubData.UserTeamId;

	public override void _Ready()
	{
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

	/// <summary>Open the directory on the user's own district.</summary>
	public void ShowWorld()
	{
		_boundUserProgram = UserProgram.OrganizationId;
		_selectedSchoolId = UserTeamId();
		_focusedDistrict = UserDistrictId();
		Refresh();
	}

	private string UserDistrictId() => DistrictHubData.GetTeam(UserTeamId()).DistrictId;

	public void ShowDistrict(string districtId)
	{
		_boundUserProgram = UserProgram.OrganizationId;
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
		_boundUserProgram = UserProgram.OrganizationId;
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
		layout.AddThemeConstantOverride("separation", 12);
		margin.AddChild(layout);

		_filterRow = new HBoxContainer();
		layout.AddChild(UiKit.PageHeader($"LEAGUE  ·  {DistrictHubData.SeasonYear}", "DISTRICTS", " ", out _subtitle, _filterRow));

		_bodyHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		layout.AddChild(_bodyHost);
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
		_focusedDistrict = null;
	}

	private void Refresh()
	{
		SyncUserSelection();
		if (_bodyHost == null || _session == null)
		{
			return;
		}

		_focusedDistrict ??= UserDistrictId();
		ApplyHeaderCopy();
		RebuildFilters();
		RebuildBody();
	}

	private void ApplyHeaderCopy()
	{
		string level = Level() == TeamLevel.Varsity ? "Varsity" : "JV";
		_subtitle.Text = $"{level}  ·  four districts, eight programs each";
	}

	private void RebuildFilters()
	{
		foreach (Node child in _filterRow.GetChildren())
		{
			_filterRow.RemoveChild(child);
			child.QueueFree();
		}

		var tabs = new List<(string Label, System.Action Open)>();
		foreach (HubDistrict district in DistrictHubData.Districts)
		{
			string id = district.Id;
			tabs.Add((district.Name.ToUpperInvariant(), () => ShowDistrict(id)));
		}

		_filterRow.AddChild(UiKit.HeaderTabs(tabs, DistrictHubData.GetDistrict(_focusedDistrict!).Name.ToUpperInvariant()));
	}

	private void RebuildBody()
	{
		foreach (Node child in _bodyHost.GetChildren())
		{
			_bodyHost.RemoveChild(child);
			child.QueueFree();
		}

		_schoolCard = null;

		_bodyHost.AddChild(BuildDistrictBody());
	}

	private Control BuildDistrictBody()
	{
		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		row.AddThemeConstantOverride("separation", 12);

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
		PanelContainer card = UiKit.MakeCard(22, 16);
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		card.SizeFlagsStretchRatio = 1.2f;

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);

		string title = $"{district.Name.ToUpperInvariant()} DISTRICT  ·  {district.Region.ToUpperInvariant()} REGION";
		if (district.Id == UserDistrictId())
		{
			title += "  ·  YOUR DISTRICT";
		}

		layout.AddChild(UiKit.CardHeader(title, "STANDINGS", () => OpenDistrictStandings(districtId)));
		layout.AddChild(UiKit.Spacer(0, 6));
		var header = new MarginContainer();
		header.AddThemeConstantOverride("margin_left", 8);
		header.AddThemeConstantOverride("margin_right", 8);
		header.AddThemeConstantOverride("margin_bottom", 4);
		header.AddChild(LeagueUi.ColumnHeader(
			("#", 22, HorizontalAlignment.Right),
			("PROGRAM", 0, HorizontalAlignment.Left),
			("W-L", 56, HorizontalAlignment.Center),
			("PCT", 52, HorizontalAlignment.Center),
			("GB", 40, HorizontalAlignment.Center),
			("STRK", 44, HorizontalAlignment.Center)));
		layout.AddChild(header);
		layout.AddChild(UiKit.Hairline());

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

		string userId = UserTeamId();
		foreach (HubStandingRow row in DistrictHubData.StandingsFor(districtId, Level()))
		{
			rows.AddChild(BuildFullRow(row, userId));
		}

		return card;
	}

	private Control BuildFullRow(HubStandingRow row, string userId)
	{
		bool user = row.Team.Id == userId;
		bool selected = row.Team.Id == _selectedSchoolId;
		PanelContainer panel = LeagueUi.RowPanel(user || selected, 7);
		SchoolLinks.MakeClickable(panel, row.Team.Id);

		var cells = new HBoxContainer();
		cells.AddThemeConstantOverride("separation", 10);
		cells.AddChild(UiKit.Cell($"{row.Rank}", 22, UiKit.Semibold, 16, UiKit.Muted, HorizontalAlignment.Right));
		var identity = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		identity.AddThemeConstantOverride("separation", 10);
		identity.AddChild(LeagueUi.TeamBadge(row.Team.Id, row.Team.Name, 28));
		identity.AddChild(UiKit.Cell(row.Team.Name.ToUpperInvariant(), 0, UiKit.Bold, 17, user ? UiKit.Gold : UiKit.Text));
		cells.AddChild(identity);
		cells.AddChild(UiKit.Cell(row.Team.Record(Level()), 56, UiKit.Bold, 16, UiKit.Text, HorizontalAlignment.Center));
		cells.AddChild(UiKit.Cell(row.Pct, 52, UiKit.Medium, 15, UiKit.Muted, HorizontalAlignment.Center));
		cells.AddChild(UiKit.Cell(row.GamesBack, 40, UiKit.Medium, 15, UiKit.Muted, HorizontalAlignment.Center));
		Color streak = row.Streak.StartsWith('W') ? UiKit.Green : row.Streak.StartsWith('L') ? UiKit.Red : UiKit.Muted;
		cells.AddChild(UiKit.Cell(string.IsNullOrEmpty(row.Streak) ? "–" : row.Streak, 44, UiKit.Bold, 15, streak, HorizontalAlignment.Center));
		panel.AddChild(cells);
		return panel;
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
}
