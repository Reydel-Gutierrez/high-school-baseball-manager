using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// District Hub news desk: prototype notes on the district race, award
/// watches, and the projected regional field.
/// </summary>
public partial class NewsPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color LakesBlue = new(0.45f, 0.72f, 0.88f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _cascadeLogo = null!;

	private Label _subtitle = null!;
	private VBoxContainer _featureHost = null!;
	private VBoxContainer _listHost = null!;
	private int _selectedIndex;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_cascadeLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		ApplyActiveTeam();
		SelectStory(0);
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => ApplyActiveTeam();

	private void ApplyActiveTeam()
	{
		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		_subtitle.Text = varsity
			? "VARSITY  ·  GREAT LAKES DESK  ·  THURSDAY, APRIL 24"
			: "JV  ·  GREAT LAKES DESK  ·  THURSDAY, APRIL 24";
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
			BgColor = new Color(0.047059f, 0.054902f, 0.070588f, 0.96f),
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

		RebuildList();
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
		identity.AddChild(MakeText($"DISTRICT HUB  ·  {DistrictHubData.SeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("NEWS", _bold, 26, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("VARSITY  ·  GREAT LAKES DESK", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);
		return header;
	}

	private void RebuildList()
	{
		foreach (Node child in _listHost.GetChildren())
		{
			_listHost.RemoveChild(child);
			child.QueueFree();
		}

		IReadOnlyList<HubNewsItem> items = DistrictHubData.News;
		for (int i = 0; i < items.Count; i++)
		{
			int index = i;
			_listHost.AddChild(BuildListRow(items[i], i, index == _selectedIndex));
		}
	}

	private Control BuildListRow(HubNewsItem item, int index, bool selected)
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

		var date = MakeText(item.Date, _semibold, 11, TextMuted, HorizontalAlignment.Left);
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
		_selectedIndex = index;
		RebuildList();
		BindFeature(DistrictHubData.News[index]);
	}

	private void BindFeature(HubNewsItem item)
	{
		foreach (Node child in _featureHost.GetChildren())
		{
			_featureHost.RemoveChild(child);
			child.QueueFree();
		}

		_featureHost.AddChild(MakeTag(item.Tag));
		_featureHost.AddChild(MakeText(item.Date.ToUpperInvariant() + "  ·  GREAT LAKES DESK", _semibold, 11, TextMuted, HorizontalAlignment.Left));

		var headline = MakeText(item.Headline.ToUpperInvariant(), _bold, 26, TextPrimary, HorizontalAlignment.Left);
		headline.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_featureHost.AddChild(headline);
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
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		var pullCopy = new VBoxContainer();
		pullCopy.AddThemeConstantOverride("separation", 4);
		pullCopy.AddChild(MakeText("WHY IT MATTERS", _semibold, 10, Accent, HorizontalAlignment.Left));
		var pullBody = MakeText(
			"Cascade is 18-4 and first in the Great Lakes. The East Regional, if the field locked today, would send the Regional into a semi against Sandusky.",
			_medium,
			13,
			TextMuted,
			HorizontalAlignment.Left);
		pullBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		pullCopy.AddChild(pullBody);
		pull.AddChild(pullCopy);
		_featureHost.AddChild(pull);
	}

	private PanelContainer MakeTag(string tag)
	{
		Color color = tag switch
		{
			"AWARDS" => Accent,
			"PLAYOFFS" => Green,
			"PITCHING" or "HITTING" => LakesBlue,
			"RIVALRY" => new Color(0.906f, 0.298f, 0.235f, 1f),
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
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		pill.AddChild(MakeText(tag, _bold, 10, color, HorizontalAlignment.Center));
		return pill;
	}

	private static StyleBoxFlat MakeRowStyle(bool selected, bool even)
	{
		return new StyleBoxFlat
		{
			BgColor = selected ? new Color(0.956863f, 0.643137f, 0.109804f, 0.14f) : even ? RowEven : RowOdd,
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
}
