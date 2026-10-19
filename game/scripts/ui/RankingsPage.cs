using System.Collections.Generic;
using System.Linq;
using Godot;
using HSBM.Domain;

/// <summary>
/// Rankings &amp; awards (concept 15): the statewide power poll of all 32
/// programs on the left; on the right, the state award races (who leads and
/// where your best candidate stands) and last season's winners.
/// </summary>
public partial class RankingsPage : Control
{
	private GameSession _session = null!;

	private Label _subtitle = null!;
	private Label _pollTitle = null!;
	private VBoxContainer _rowsHost = null!;
	private VBoxContainer _racesHost = null!;
	private VBoxContainer _winnersHost = null!;
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

	private void OnActiveTeamChanged(TeamIdentity _) => Refresh();

	private void OnVisibilityChanged()
	{
		if (Visible)
		{
			Refresh();
		}
	}

	public void SelectSchool(string teamId)
	{
		_boundUserProgram = UserProgram.OrganizationId;
		_selectedSchoolId = SchoolProfiles.NormalizeId(teamId);
		Refresh();
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
	}

	public void Refresh()
	{
		SyncUserSelection();
		if (_rowsHost == null || _session == null || !IsVisibleInTree())
		{
			return;
		}

		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		_subtitle.Visible = true;
		_subtitle.Text = varsity
			? "Varsity  ·  record, opponent strength and reputation  ·  updates with every result"
			: "JV  ·  same 32 programs, junior varsity records";

		IReadOnlyList<PowerRankingRow> rows = RankingsDesk.For(_session);
		string userId = UserTeamId();
		_pollTitle.Text = "STATE POWER RANKINGS";

		Clear(_rowsHost);
		foreach (PowerRankingRow row in rows)
		{
			_rowsHost.AddChild(BuildRow(row, userId));
		}

		RebuildRaces(userId);
		RebuildWinners();
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

		layout.AddChild(UiKit.PageHeader($"LEAGUE  ·  {DistrictHubData.SeasonYear}", "RANKINGS & AWARDS", " ", out _subtitle));

		var bodyRow = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		bodyRow.AddThemeConstantOverride("separation", 12);
		layout.AddChild(bodyRow);

		bodyRow.AddChild(BuildPollCard());

		var side = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		side.AddThemeConstantOverride("separation", 12);
		bodyRow.AddChild(side);

		side.AddChild(BuildSideCard("AWARD RACES  ·  STATE", "ALL AWARDS", OpenAwards, out _racesHost, 1.3f));
		side.AddChild(BuildSideCard("LAST SEASON'S WINNERS", null, null, out _winnersHost, 1f));
	}

	private Control BuildPollCard()
	{
		PanelContainer card = UiKit.MakeCard(22, 14);
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		card.SizeFlagsStretchRatio = 1.2f;

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);

		var heading = new HBoxContainer();
		_pollTitle = UiKit.Text_("STATE POWER RANKINGS", UiKit.Semibold, 13, UiKit.Muted);
		_pollTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		heading.AddChild(_pollTitle);
		heading.AddChild(UiKit.Text_("32 PROGRAMS", UiKit.Semibold, 12, UiKit.Muted));
		layout.AddChild(heading);
		layout.AddChild(UiKit.Spacer(0, 6));

		var header = new MarginContainer();
		header.AddThemeConstantOverride("margin_left", 8);
		header.AddThemeConstantOverride("margin_right", 8);
		header.AddThemeConstantOverride("margin_bottom", 4);
		header.AddChild(LeagueUi.ColumnHeader(
			("RK", 28, HorizontalAlignment.Right),
			("PROGRAM", 0, HorizontalAlignment.Left),
			("DISTRICT", 120, HorizontalAlignment.Left),
			("W-L", 56, HorizontalAlignment.Center),
			("SOS", 48, HorizontalAlignment.Center),
			("MOVE", 44, HorizontalAlignment.Right)));
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

		_rowsHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_rowsHost.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(_rowsHost);
		return card;
	}

	private static Control BuildSideCard(string title, string? link, System.Action? onLink, out VBoxContainer host, float stretch)
	{
		PanelContainer card = UiKit.MakeCard(22, 14);
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		card.SizeFlagsStretchRatio = stretch;
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 6);
		card.AddChild(col);
		col.AddChild(UiKit.CardHeader(title, link, onLink));
		host = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		host.AddThemeConstantOverride("separation", 0);
		col.AddChild(host);
		return card;
	}

	private Control BuildRow(PowerRankingRow row, string userId)
	{
		bool user = row.OrganizationId == userId;
		PanelContainer panel = LeagueUi.RowPanel(user || row.OrganizationId == _selectedSchoolId, 5);
		SchoolLinks.MakeClickable(panel, row.OrganizationId);

		var cells = new HBoxContainer();
		cells.AddThemeConstantOverride("separation", 10);
		cells.AddChild(UiKit.Cell($"{row.Rank}", 28, UiKit.Bold, 18, row.Rank <= 4 ? UiKit.Gold : UiKit.Text, HorizontalAlignment.Right));
		var identity = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		identity.AddThemeConstantOverride("separation", 10);
		identity.AddChild(LeagueUi.TeamBadge(row.OrganizationId, row.Name, 22));
		identity.AddChild(UiKit.Cell(row.Name.ToUpperInvariant(), 0, UiKit.Bold, 16, user ? UiKit.Gold : UiKit.Text));
		cells.AddChild(identity);
		cells.AddChild(UiKit.Cell(row.DistrictName, 120, UiKit.Medium, 15, UiKit.Muted));
		cells.AddChild(UiKit.Cell(row.RecordLabel, 56, UiKit.Bold, 16, UiKit.Text, HorizontalAlignment.Center));
		cells.AddChild(UiKit.Cell(row.SosLabel, 48, UiKit.Medium, 14, UiKit.Muted, HorizontalAlignment.Center));
		string move = row.Movement > 0 ? $"▲{row.Movement}" : row.Movement < 0 ? $"▼{-row.Movement}" : "–";
		Color moveColor = row.Movement > 0 ? UiKit.Green : row.Movement < 0 ? UiKit.Red : UiKit.Muted;
		cells.AddChild(UiKit.Cell(move, 44, UiKit.Bold, 15, moveColor, HorizontalAlignment.Right));
		panel.AddChild(cells);
		return panel;
	}

	/// <summary>
	/// One line per individual state honor: the current leader (or winner),
	/// and a pill with where the user's best candidate sits in the field.
	/// </summary>
	private void RebuildRaces(string userId)
	{
		Clear(_racesHost);
		AwardsCeremony ceremony = AwardsDesk.For(_session, AwardScope.State);
		foreach (AwardHonor honor in ceremony.Honors)
		{
			if (honor.IsTeamAward)
			{
				continue;
			}

			AwardHonoree? leader = honor.Winner ?? honor.Field.FirstOrDefault();
			int userPlace = 0;
			for (int i = 0; i < honor.Field.Count; i++)
			{
				if (SchoolProfiles.NormalizeId(honor.Field[i].TeamId) == userId)
				{
					userPlace = i + 1;
					break;
				}
			}

			var line = new HBoxContainer();
			line.AddThemeConstantOverride("separation", 12);
			if (leader != null && !honor.IsCoachAward)
			{
				Control? face = PlayerFaces.For(leader.TeamId, leader.Name, 40, 40, true, _session.ActiveTeam.Level);
				line.AddChild(face ?? LeagueUi.TeamBadge(leader.TeamId, leader.TeamShort, 40));
			}
			else
			{
				line.AddChild(leader == null ? UiKit.Spacer(40, 40) : LeagueUi.TeamBadge(leader.TeamId, leader.TeamShort, 40));
			}

			var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
			text.AddThemeConstantOverride("separation", -4);
			text.AddChild(UiKit.Text_(honor.Name, UiKit.Semibold, 17, UiKit.Text));
			string detail = leader == null
				? "No front-runner yet"
				: $"{leader.Name}  ·  {leader.TeamShort}  ·  {leader.HeadlineStat}";
			Label detailLabel = UiKit.Text_(detail, UiKit.Medium, 14, UiKit.Muted);
			detailLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			detailLabel.ClipText = true;
			text.AddChild(detailLabel);
			line.AddChild(text);

			if (userPlace > 0)
			{
				line.AddChild(LeagueUi.SmallPill($"YOU {Ordinal(userPlace)}", userPlace == 1 ? UiKit.Tone.Good : userPlace <= 3 ? UiKit.Tone.Warn : UiKit.Tone.Neutral));
			}

			var row = new PanelContainer();
			row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
			{
				BgColor = Colors.Transparent,
				BorderColor = UiKit.Line,
				BorderWidthBottom = 1,
				ContentMarginTop = 6,
				ContentMarginBottom = 6,
			});
			row.AddChild(line);
			_racesHost.AddChild(row);
		}
	}

	/// <summary>State champion and the state award winners from the archived season.</summary>
	private void RebuildWinners()
	{
		Clear(_winnersHost);
		int last = GameSession.SeasonYear - 1;
		HistoricalSeason? season = _session.World?.HistoricalSeasons.FirstOrDefault(s => s.SeasonYear == last);
		var entries = new List<(string Title, string Detail)>();
		if (season != null)
		{
			foreach (HistoricalChampion champion in season.Champions)
			{
				if (champion.Title.Contains("State", System.StringComparison.OrdinalIgnoreCase))
				{
					entries.Add(("State Champion", champion.Name));
				}
			}

			foreach (SeasonHonor honor in season.StateAwards?.Honors ?? [])
			{
				if (!honor.IsTeamAward && honor.Winner != null)
				{
					entries.Add((honor.Name, $"{honor.Winner.Name}  ·  {honor.Winner.TeamShort}"));
				}
			}
		}

		if (entries.Count == 0)
		{
			Label empty = UiKit.Text_($"No {last} season on file. Winners show here once a season has been played to the state banquet.", UiKit.Medium, 14, UiKit.Muted);
			empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_winnersHost.AddChild(empty);
			return;
		}

		foreach ((string title, string detail) in entries)
		{
			var col = new VBoxContainer();
			col.AddThemeConstantOverride("separation", -4);
			col.AddChild(UiKit.Text_(title, UiKit.Semibold, 16, UiKit.Text));
			col.AddChild(UiKit.Text_(detail, UiKit.Medium, 14, UiKit.Muted));
			var row = new PanelContainer();
			row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
			{
				BgColor = Colors.Transparent,
				BorderColor = UiKit.Line,
				BorderWidthBottom = 1,
				ContentMarginTop = 5,
				ContentMarginBottom = 5,
			});
			row.AddChild(col);
			_winnersHost.AddChild(row);
		}
	}

	private void OpenAwards() => LeagueUi.Navigator(this)?.ShowAwards();

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

	private string UserTeamId()
	{
		string catalog = SchoolProfiles.NormalizeId(_session.CatalogOrganizationId);
		return string.IsNullOrWhiteSpace(catalog) ? DistrictHubData.UserTeamId : catalog;
	}
}
