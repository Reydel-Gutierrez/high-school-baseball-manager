using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// League statewide power poll. One table of all 32 programs,
/// ranked by record, opponent strength, and reputation.
/// </summary>
public partial class RankingsPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color UserRow = new(0.956863f, 0.643137f, 0.109804f, 0.14f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _cascadeLogo = null!;

	private Label _subtitle = null!;
	private Label _countValue = null!;
	private Label _countLabel = null!;
	private VBoxContainer _rowsHost = null!;
	private SchoolProfileCard _schoolCard = null!;
	private string _selectedSchoolId = DistrictHubData.UserTeamId;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_cascadeLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
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
		_selectedSchoolId = SchoolProfiles.NormalizeId(teamId);
		Refresh();
	}

	public void Refresh()
	{
		if (_rowsHost == null || _session == null)
		{
			return;
		}

		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		_subtitle.Text = varsity
			? "VARSITY  ·  RECORD, OPPONENT STRENGTH, AND REPUTATION"
			: "JV  ·  SAME 32 PROGRAMS  ·  JUNIOR VARSITY RECORDS";

		IReadOnlyList<PowerRankingRow> rows = RankingsDesk.For(_session);
		string userId = UserTeamId();
		int userRank = 0;
		foreach (PowerRankingRow row in rows)
		{
			if (row.OrganizationId == userId)
			{
				userRank = row.Rank;
				break;
			}
		}

		_countValue.Text = userRank > 0 ? $"#{userRank}" : $"{rows.Count}";
		_countLabel.Text = userRank > 0 ? "YOUR RANK" : "TEAMS";

		foreach (Node child in _rowsHost.GetChildren())
		{
			_rowsHost.RemoveChild(child);
			child.QueueFree();
		}

		for (int i = 0; i < rows.Count; i++)
		{
			_rowsHost.AddChild(BuildRow(rows[i], i % 2 == 0, userId));
		}

		_schoolCard.Bind(_selectedSchoolId, _session.ActiveTeam.Level);
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

		var bodyRow = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		bodyRow.AddThemeConstantOverride("separation", 8);
		layout.AddChild(bodyRow);

		bodyRow.AddChild(BuildListCard());

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
		header.AddChild(TeamLogos.MakeHeaderMark(_cascadeLogo));
		header.AddChild(TeamLogos.MakeHeaderRule(Hairline));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -2);
		identity.AddChild(MakeText($"LEAGUE  ·  {DistrictHubData.SeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("POWER RANKINGS", _bold, 26, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("VARSITY  ·  RECORD, OPPONENT STRENGTH, AND REPUTATION", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var countBlock = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		countBlock.AddThemeConstantOverride("separation", -4);
		_countValue = MakeText("#1", _bold, 28, TextPrimary, HorizontalAlignment.Right);
		countBlock.AddChild(_countValue);
		_countLabel = MakeText("YOUR RANK", _semibold, 11, TextMuted, HorizontalAlignment.Right);
		countBlock.AddChild(_countLabel);
		header.AddChild(countBlock);
		return header;
	}

	private Control BuildListCard()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.55f,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle());

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);

		var heading = new PanelContainer();
		heading.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047059f, 0.054902f, 0.070588f, 0.96f),
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		});
		var headingRow = new HBoxContainer();
		headingRow.AddThemeConstantOverride("separation", 10);
		headingRow.AddChild(MakeText("STATE POWER RANKINGS", _bold, 16, TextPrimary, HorizontalAlignment.Left));
		var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		headingRow.AddChild(spacer);
		headingRow.AddChild(MakeText("32 TEAMS", _semibold, 12, TextMuted, HorizontalAlignment.Right));
		heading.AddChild(headingRow);
		layout.AddChild(heading);
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		layout.AddChild(BuildColumnHeader());

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

	private Control BuildColumnHeader()
	{
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047059f, 0.054902f, 0.070588f, 0.96f),
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
		columns.AddChild(MakeFixed("", 22, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		var team = MakeText("TEAM", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		team.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(team);
		columns.AddChild(MakeFixed("DISTRICT", 110, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed("W-L", 56, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		columns.AddChild(MakeFixed("SOS", 48, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		header.AddChild(columns);
		return header;
	}

	private Control BuildRow(PowerRankingRow row, bool even, string userId)
	{
		bool user = row.OrganizationId == userId;
		bool selected = row.OrganizationId == _selectedSchoolId;
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
		SchoolLinks.MakeClickable(panel, row.OrganizationId);

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(MakeFixed($"{row.Rank}", 28, _bold, 14, row.Rank <= 4 ? Accent : TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeFixed(row.MovementLabel, 22, _bold, 16, MovementColor(row.Movement), HorizontalAlignment.Center));

		var identity = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		identity.AddThemeConstantOverride("separation", 8);
		TextureRect? logo = TeamLogos.TryMakeIcon(row.OrganizationId, 24);
		if (logo != null)
		{
			identity.AddChild(logo);
		}

		var name = MakeText(row.Name.ToUpperInvariant(), user ? _bold : _semibold, 15, user ? Accent : TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		identity.AddChild(name);
		columns.AddChild(identity);

		columns.AddChild(MakeFixed(row.DistrictName.ToUpperInvariant(), 110, _semibold, 13, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed(row.RecordLabel, 56, _bold, 15, TextPrimary, HorizontalAlignment.Right));
		columns.AddChild(MakeFixed(row.SosLabel, 48, _semibold, 13, TextMuted, HorizontalAlignment.Right));
		panel.AddChild(columns);
		return panel;
	}

	private string UserTeamId()
	{
		string catalog = SchoolProfiles.NormalizeId(_session.CatalogOrganizationId);
		return string.IsNullOrWhiteSpace(catalog) ? DistrictHubData.UserTeamId : catalog;
	}

	private static Color MovementColor(int movement)
	{
		if (movement > 0)
		{
			return Green;
		}

		if (movement < 0)
		{
			return Urgent;
		}

		return TextMuted;
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

	private static StyleBoxFlat MakeCardStyle()
	{
		return new StyleBoxFlat
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
			CornerDetail = 3,
		};
	}
}
