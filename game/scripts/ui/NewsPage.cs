using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// League news desk: condition-triggered wire copy from the live
/// season, not a static briefing.
/// </summary>
public partial class NewsPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.772549f, 0.635294f, 0.325490f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color CardInner = new(0.090196f, 0.105882f, 0.141176f, 1f);
	private static readonly Color CardBorder = new(0.137255f, 0.152941f, 0.200000f, 1f);
	private static readonly Color Hairline = new(0.17f, 0.19f, 0.24f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color LakesBlue = new(0.45f, 0.72f, 0.88f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;

	private Label _subtitle = null!;
	private VBoxContainer _featureHost = null!;
	private VBoxContainer _listHost = null!;
	private int _selectedIndex;
	private List<NewsStory> _stories = new();

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
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
		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		string desk = DistrictHubData.GetDistrict(DistrictHubData.UserDistrictId).Name.ToUpperInvariant();
		_subtitle.Text = varsity
			? $"VARSITY  ·  {desk} DESK  ·  {_session.CurrentDate.ToString("dddd, MMMM d", CultureInfo.InvariantCulture).ToUpperInvariant()}"
			: $"JV  ·  {desk} DESK  ·  {_session.CurrentDate.ToString("dddd, MMMM d", CultureInfo.InvariantCulture).ToUpperInvariant()}";
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

		var body = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 8);
		layout.AddChild(body);

		var featureCard = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.15f,
			ClipContents = true,
		};
		featureCard.AddThemeStyleboxOverride("panel", MakeCardStyle(12));
		_featureHost = new VBoxContainer();
		_featureHost.AddThemeConstantOverride("separation", 8);
		featureCard.AddChild(_featureHost);
		body.AddChild(featureCard);

		var listCard = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		listCard.AddThemeStyleboxOverride("panel", MakeCardStyle(0));

		var listLayout = new VBoxContainer();
		listLayout.AddThemeConstantOverride("separation", 0);
		listCard.AddChild(listLayout);

		var listHeading = new PanelContainer();
		listHeading.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		});
		listHeading.AddChild(MakeText("THE WIRE", _bold, 14, TextPrimary, HorizontalAlignment.Left));
		listLayout.AddChild(listHeading);
		listLayout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });

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
		identity.AddChild(MakeText("NEWS", _bold, 36, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("VARSITY  ·  GREAT LAKES DESK", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);
		return header;
	}

	private void BindStories()
	{
		_session.EnsureNews();
		_stories = new List<NewsStory>(NewsDesk.For(_session));
		if (_selectedIndex >= _stories.Count)
		{
			_selectedIndex = 0;
		}

		RebuildList();
		if (_stories.Count > 0)
		{
			SelectStory(_selectedIndex);
		}
		else
		{
			BindEmpty();
		}
	}

	private void RebuildList()
	{
		foreach (Node child in _listHost.GetChildren())
		{
			_listHost.RemoveChild(child);
			child.QueueFree();
		}

		for (int i = 0; i < _stories.Count; i++)
		{
			int index = i;
			_listHost.AddChild(BuildListRow(_stories[i], i, index == _selectedIndex));
		}
	}

	private Control BuildListRow(NewsStory item, int index, bool selected)
	{
		var row = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop,
		};
		row.AddThemeStyleboxOverride("panel", MakeRowStyle(selected, index % 2 == 0));
		row.GuiInput += (InputEvent evt) =>
		{
			if (evt is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				SelectStory(index);
			}
		};
		row.MouseEntered += () =>
		{
			if (!selected)
			{
				row.AddThemeStyleboxOverride("panel", MakeRowStyle(true, true));
			}
		};
		row.MouseExited += () => row.AddThemeStyleboxOverride("panel", MakeRowStyle(selected, index % 2 == 0));

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);

		var date = MakeText(item.DateLabel, _semibold, 11, TextMuted, HorizontalAlignment.Left);
		date.CustomMinimumSize = new Vector2(52, 0);
		columns.AddChild(date);

		var copy = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		copy.AddThemeConstantOverride("separation", 2);
		copy.AddChild(MakeTag(item.Tag));
		var headline = MakeText(item.Headline, selected ? _bold : _semibold, 13, selected ? Accent : TextPrimary, HorizontalAlignment.Left);
		headline.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		copy.AddChild(headline);
		columns.AddChild(copy);

		row.AddChild(columns);
		return row;
	}

	private void SelectStory(int index)
	{
		if (_stories.Count == 0)
		{
			BindEmpty();
			return;
		}

		_selectedIndex = Math.Clamp(index, 0, _stories.Count - 1);
		RebuildList();
		BindFeature(_stories[_selectedIndex]);
	}

	private void BindEmpty()
	{
		foreach (Node child in _featureHost.GetChildren())
		{
			_featureHost.RemoveChild(child);
			child.QueueFree();
		}

		_featureHost.AddChild(MakeTag(NewsTag.Note));
		_featureHost.AddChild(MakeText("THE WIRE", _semibold, 11, TextMuted, HorizontalAlignment.Left));
		var headline = MakeText("NO COPY YET", _bold, 26, TextPrimary, HorizontalAlignment.Left);
		_featureHost.AddChild(headline);
		var body = MakeText(
			"Stories land when the season does something: a streak, a freshman who sticks, a board that starts asking questions, a bracket that locks.",
			_medium,
			15,
			new Color(0.78f, 0.81f, 0.86f),
			HorizontalAlignment.Left);
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_featureHost.AddChild(body);
	}

	private void BindFeature(NewsStory item)
	{
		foreach (Node child in _featureHost.GetChildren())
		{
			_featureHost.RemoveChild(child);
			child.QueueFree();
		}

		_featureHost.AddChild(MakeTag(item.Tag));
		string desk = "STATE DESK";
		if (!string.IsNullOrEmpty(item.SubjectOrganizationId))
		{
			HubTeam mentioned = DistrictHubData.GetTeam(SchoolProfiles.NormalizeId(item.SubjectOrganizationId));
			desk = DistrictHubData.GetDistrict(mentioned.DistrictId).Name.ToUpperInvariant() + " DESK";
		}

		_featureHost.AddChild(MakeText(item.DateLabel + "  ·  " + desk, _semibold, 11, TextMuted, HorizontalAlignment.Left));

		var headline = MakeText(item.Headline.ToUpperInvariant(), _bold, 26, TextPrimary, HorizontalAlignment.Left);
		headline.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_featureHost.AddChild(headline);
		Control chips = SchoolLinks.MakeMentionChips(item.Headline + " " + item.Body, _semibold);
		if (chips.GetChildCount() > 0)
		{
			_featureHost.AddChild(chips);
		}
		_featureHost.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });

		var body = MakeText(item.Body, _medium, 15, new Color(0.78f, 0.81f, 0.86f), HorizontalAlignment.Left);
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_featureHost.AddChild(body);

		var pull = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		pull.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = Hairline,
			BorderWidthLeft = 2,
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
		});
		var pullCopy = new VBoxContainer();
		pullCopy.AddThemeConstantOverride("separation", 4);
		pullCopy.AddChild(MakeText("WHY IT MATTERS", _semibold, 10, Accent, HorizontalAlignment.Left));
		var pullBody = MakeText(item.Pull, _medium, 13, TextMuted, HorizontalAlignment.Left);
		pullBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		pullCopy.AddChild(pullBody);
		pull.AddChild(pullCopy);
		_featureHost.AddChild(pull);
	}

	private PanelContainer MakeTag(string tag)
	{
		Color color = tag switch
		{
			NewsTag.Playoffs => Green,
			NewsTag.Rankings => Accent,
			NewsTag.Freshman => LakesBlue,
			NewsTag.Coach => new Color(0.906f, 0.298f, 0.235f, 1f),
			NewsTag.Pitching or NewsTag.Hitting => LakesBlue,
			NewsTag.Rivalry => new Color(0.906f, 0.298f, 0.235f, 1f),
			NewsTag.Calendar => Accent,
			NewsTag.District => Green,
			NewsTag.Results => Green,
			_ => Hairline,
		};
		var pill = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
		};
		pill.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(color.R, color.G, color.B, 0.16f),
			BorderColor = color,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 7,
			ContentMarginTop = 2,
			ContentMarginRight = 7,
			ContentMarginBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
		});
		pill.AddChild(MakeText(tag, _bold, 10, color, HorizontalAlignment.Center));
		return pill;
	}

	private static StyleBoxFlat MakeRowStyle(bool selected, bool even)
	{
		return new StyleBoxFlat
		{
			BgColor = selected ? new Color(0.772549f, 0.635294f, 0.325490f, 0.14f) : even ? RowEven : RowOdd,
			BorderColor = selected ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = selected ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 10,
		};
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
}
