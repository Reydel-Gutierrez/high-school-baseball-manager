using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// League news desk (concept 16): condition-triggered wire copy from the live
/// season. The selected story runs as a team-branded banner with the
/// subject's face, the next two sit underneath as smaller banners, and the
/// latest wire runs down the right. Header tabs filter to your district or
/// your own team.
/// </summary>
public partial class NewsPage : Control
{
	private const string FeedAll = "ALL";
	private const string FeedMine = "MY TEAM";

	private static readonly Color Blue = new(0.45f, 0.72f, 0.88f, 1f);

	private GameSession _session = null!;

	private Label _subtitle = null!;
	private Control _tabsHost = null!;
	private VBoxContainer _featureHost = null!;
	private VBoxContainer _listHost = null!;
	private string _feed = FeedAll;
	private int _selectedIndex;
	private List<NewsStory> _all = new();
	private List<NewsStory> _stories = new();

	public override void _Ready()
	{
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		_session.CareerChanged += OnCareerChanged;
		VisibilityChanged += OnVisibilityChanged;
		ApplyActiveTeam();
		BindStories();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
			_session.CareerChanged -= OnCareerChanged;
		}

		VisibilityChanged -= OnVisibilityChanged;
	}

	private void OnVisibilityChanged()
	{
		if (IsVisibleInTree())
		{
			BindStories();
			_session.AcknowledgeNews();
		}
	}

	private void OnCareerChanged()
	{
		ApplyActiveTeam();
		if (IsVisibleInTree())
		{
			BindStories();
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => ApplyActiveTeam();

	private void ApplyActiveTeam()
	{
		string level = _session.ActiveTeam.Level == TeamLevel.Varsity ? "Varsity" : "JV";
		_subtitle.Visible = true;
		_subtitle.Text = $"{level}  ·  around the four districts  ·  {_session.CurrentDate.ToString("dddd, MMMM d", CultureInfo.InvariantCulture)}";
	}

	private string UserDistrictName() => DistrictHubData.GetDistrict(DistrictHubData.UserDistrictId).Name.ToUpperInvariant();

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
		_tabsHost = new HBoxContainer();
		layout.AddChild(UiKit.PageHeader($"LEAGUE  ·  {DistrictHubData.SeasonYear}", "NEWS", " ", out _subtitle, _tabsHost));

		var body = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 12);
		layout.AddChild(body);

		_featureHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.4f,
		};
		_featureHost.AddThemeConstantOverride("separation", 12);
		body.AddChild(_featureHost);

		PanelContainer listCard = UiKit.MakeCard(22, 14);
		listCard.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		listCard.SizeFlagsVertical = SizeFlags.ExpandFill;
		var listLayout = new VBoxContainer();
		listLayout.AddThemeConstantOverride("separation", 6);
		listCard.AddChild(listLayout);
		listLayout.AddChild(UiKit.CardHeader("LATEST"));

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		listLayout.AddChild(scroll);

		_listHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_listHost.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(_listHost);
		body.AddChild(listCard);
	}

	private void RebuildTabs()
	{
		Clear(_tabsHost);
		string district = UserDistrictName();
		_tabsHost.AddChild(UiKit.HeaderTabs(
			[
				(FeedAll, () => SetFeed(FeedAll)),
				(district, () => SetFeed(district)),
				(FeedMine, () => SetFeed(FeedMine)),
			],
			_feed == FeedAll || _feed == FeedMine ? _feed : district));
	}

	private void SetFeed(string feed)
	{
		_feed = feed;
		_selectedIndex = 0;
		BindStories();
	}

	private void BindStories()
	{
		_session.EnsureNews();
		_all = new List<NewsStory>(NewsDesk.For(_session));
		_stories = _all.FindAll(InFeed);
		if (_selectedIndex >= _stories.Count)
		{
			_selectedIndex = 0;
		}

		RebuildTabs();
		SelectStory(_selectedIndex);
	}

	private bool InFeed(NewsStory story)
	{
		if (_feed == FeedAll)
		{
			return true;
		}

		string user = UserProgram.OrganizationId;
		string? subject = string.IsNullOrEmpty(story.SubjectOrganizationId) ? null : SchoolProfiles.NormalizeId(story.SubjectOrganizationId);
		if (_feed == FeedMine)
		{
			return subject == user;
		}

		return subject != null
			? DistrictHubData.GetTeam(subject).DistrictId == DistrictHubData.UserDistrictId
			: story.Tag == NewsTag.District;
	}

	private void SelectStory(int index)
	{
		Clear(_featureHost);
		Clear(_listHost);
		if (_stories.Count == 0)
		{
			BindEmpty();
			return;
		}

		_selectedIndex = Math.Clamp(index, 0, _stories.Count - 1);
		for (int i = 0; i < _stories.Count; i++)
		{
			_listHost.AddChild(BuildListRow(_stories[i], i, i == _selectedIndex));
		}

		_featureHost.AddChild(BuildFeature(_stories[_selectedIndex]));

		var seconds = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		seconds.AddThemeConstantOverride("separation", 12);
		int shown = 0;
		for (int i = 1; i < _stories.Count && shown < 2; i++)
		{
			int next = (_selectedIndex + i) % _stories.Count;
			seconds.AddChild(BuildSecondary(_stories[next], next));
			shown++;
		}

		if (shown > 0)
		{
			_featureHost.AddChild(seconds);
		}
	}

	private void BindEmpty()
	{
		PanelContainer card = UiKit.MakeCard(26, 22);
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 6);
		card.AddChild(col);
		col.AddChild(UiKit.Kicker("THE WIRE"));
		col.AddChild(UiKit.Text_("NO COPY YET", UiKit.Bold, 30, UiKit.Text));
		Label body = UiKit.Text_(
			_feed == FeedAll
				? "Stories land when the season does something: a streak, a freshman who sticks, a board that starts asking questions, a bracket that locks."
				: "Nothing on this desk yet. Switch to ALL for the whole wire.",
			UiKit.Medium,
			16,
			UiKit.Muted);
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		col.AddChild(body);
		_featureHost.AddChild(card);
		_listHost.AddChild(UiKit.Text_("Quiet wire.", UiKit.Medium, 14, UiKit.Muted));
	}

	private static string Desk(NewsStory item)
	{
		if (string.IsNullOrEmpty(item.SubjectOrganizationId))
		{
			return "STATE DESK";
		}

		HubTeam team = DistrictHubData.GetTeam(SchoolProfiles.NormalizeId(item.SubjectOrganizationId));
		return DistrictHubData.GetDistrict(team.DistrictId).Name.ToUpperInvariant();
	}

	private string SubjectTeam(NewsStory item) =>
		string.IsNullOrEmpty(item.SubjectOrganizationId) ? UserProgram.OrganizationId : SchoolProfiles.NormalizeId(item.SubjectOrganizationId);

	/// <summary>The lead story: team banner, face of the player it is about, headline, body and the pull line.</summary>
	private Control BuildFeature(NewsStory item)
	{
		string teamId = SubjectTeam(item);
		var banner = new BroadcastPanel
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.5f,
			CustomMinimumSize = new Vector2(0, 220),
		};
		banner.UseTeam(teamId);

		var row = new HBoxContainer();
		row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		row.AddThemeConstantOverride("separation", 24);
		banner.AddChild(row);

		Control? face = string.IsNullOrEmpty(item.SubjectPlayerName)
			? null
			: PlayerFaces.For(teamId, item.SubjectPlayerName, 180, 225, false, _session.ActiveTeam.Level);
		if (face != null)
		{
			face.SizeFlagsVertical = SizeFlags.ShrinkEnd;
			row.AddChild(UiKit.Spacer(8));
			row.AddChild(face);
		}
		else
		{
			row.AddChild(UiKit.Spacer(12));
			Control mark = LeagueUi.TeamBadge(teamId, DistrictHubData.GetTeam(teamId).Name, 150);
			mark.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			row.AddChild(mark);
		}

		var copy = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		copy.AddThemeConstantOverride("separation", 4);
		copy.AddChild(UiKit.Kicker($"{Desk(item)}  ·  {item.DateLabel}  ·  {item.Tag}"));
		Label headline = UiKit.Text_(item.Headline.ToUpperInvariant(), UiKit.Bold, 34, UiKit.Text);
		headline.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		copy.AddChild(headline);
		Label body = UiKit.Text_(item.Body, UiKit.Medium, 16, new Color(0.86f, 0.88f, 0.92f));
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		copy.AddChild(body);
		if (!string.IsNullOrWhiteSpace(item.Pull))
		{
			Label pull = UiKit.Text_("Why it matters:  " + item.Pull, UiKit.Medium, 14, UiKit.Gold);
			pull.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			copy.AddChild(pull);
		}

		Control chips = SchoolLinks.MakeMentionChips(item.Headline + " " + item.Body, UiKit.Semibold);
		if (chips.GetChildCount() > 0)
		{
			copy.AddChild(chips);
		}
		else
		{
			chips.QueueFree();
		}

		row.AddChild(copy);
		row.AddChild(UiKit.Spacer(16));
		return banner;
	}

	/// <summary>A smaller banner for the next stories; click to lead with it.</summary>
	private Control BuildSecondary(NewsStory item, int index)
	{
		string teamId = SubjectTeam(item);
		var banner = new BroadcastPanel
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 110),
			MouseDefaultCursorShape = CursorShape.PointingHand,
			MouseFilter = MouseFilterEnum.Stop,
		};
		banner.UseTeam(teamId);
		banner.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				SelectStory(index);
			}
		};

		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		row.AddThemeConstantOverride("separation", 16);
		banner.AddChild(row);
		Control? face = string.IsNullOrEmpty(item.SubjectPlayerName)
			? null
			: PlayerFaces.For(teamId, item.SubjectPlayerName, 96, 120, false, _session.ActiveTeam.Level);
		row.AddChild(UiKit.Spacer(6));
		if (face != null)
		{
			face.SizeFlagsVertical = SizeFlags.ShrinkEnd;
			face.MouseFilter = MouseFilterEnum.Ignore;
			row.AddChild(face);
		}
		else
		{
			Control mark = LeagueUi.TeamBadge(teamId, DistrictHubData.GetTeam(teamId).Name, 72);
			mark.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			row.AddChild(mark);
		}

		var copy = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
		copy.AddThemeConstantOverride("separation", 0);
		copy.AddChild(UiKit.Kicker($"{Desk(item)}  ·  {item.DateLabel}"));
		Label headline = UiKit.Text_(item.Headline.ToUpperInvariant(), UiKit.Bold, 22, UiKit.Text);
		headline.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		copy.AddChild(headline);
		row.AddChild(copy);
		row.AddChild(UiKit.Spacer(10));
		return banner;
	}

	private Control BuildListRow(NewsStory item, int index, bool selected)
	{
		PanelContainer row = LeagueUi.RowPanel(selected, 7);
		row.MouseFilter = MouseFilterEnum.Stop;
		row.MouseDefaultCursorShape = CursorShape.PointingHand;
		row.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				SelectStory(index);
			}
		};

		var cells = new HBoxContainer();
		cells.AddThemeConstantOverride("separation", 14);
		var tagBox = new HBoxContainer { CustomMinimumSize = new Vector2(96, 0) };
		tagBox.AddChild(MakeTag(item.Tag));
		cells.AddChild(tagBox);

		var copy = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		copy.AddThemeConstantOverride("separation", -3);
		Label headline = UiKit.Text_(item.Headline, UiKit.Semibold, 16, selected ? UiKit.Gold : UiKit.Text);
		headline.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		copy.AddChild(headline);
		copy.AddChild(UiKit.Text_($"{item.DateLabel}  ·  {Desk(item)}", UiKit.Medium, 13, UiKit.Muted));
		cells.AddChild(copy);
		row.AddChild(cells);
		return row;
	}

	private static PanelContainer MakeTag(string tag)
	{
		Color color = tag switch
		{
			NewsTag.Playoffs or NewsTag.Results or NewsTag.District => UiKit.Green,
			NewsTag.Rankings or NewsTag.Calendar or NewsTag.Tryouts => UiKit.Gold,
			NewsTag.Freshman or NewsTag.Pitching or NewsTag.Hitting => Blue,
			NewsTag.Coach or NewsTag.Rivalry => new Color("#ff7a6b"),
			_ => UiKit.Muted,
		};
		var pill = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		pill.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(color, 0.14f), 10, 10, 2));
		pill.AddChild(UiKit.Text_(tag, UiKit.Semibold, 11, color, HorizontalAlignment.Center));
		return pill;
	}

	private static void Clear(Node host)
	{
		foreach (Node child in host.GetChildren())
		{
			host.RemoveChild(child);
			child.QueueFree();
		}
	}
}
