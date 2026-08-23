using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// District Hub awards: six invented trophies. One award is shown at a
/// time, with the current holder and the three closest competitors.
/// </summary>
public partial class AwardsPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color Gold = new(0.92f, 0.74f, 0.28f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _cascadeLogo = null!;

	private readonly Dictionary<string, Button> _awardButtons = new();
	private Label _subtitle = null!;
	private Control _detailHost = null!;
	private string _activeAwardId = DistrictHubData.Awards[0].Id;

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
		ShowAward(_activeAwardId);
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
			? "VARSITY  ·  FOUR DISTRICTS  ·  ONE TROPHY AT A TIME"
			: "JV  ·  FOUR DISTRICTS  ·  ONE TROPHY AT A TIME";
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
		layout.AddChild(BuildAwardSelector());

		_detailHost = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		((HBoxContainer)_detailHost).AddThemeConstantOverride("separation", 8);
		layout.AddChild(_detailHost);
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
		identity.AddChild(MakeText("AWARDS", _bold, 26, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("VARSITY  ·  FOUR DISTRICTS", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);
		return header;
	}

	private Control BuildAwardSelector()
	{
		var row = new HFlowContainer();
		row.AddThemeConstantOverride("h_separation", 4);
		row.AddThemeConstantOverride("v_separation", 4);
		foreach (HubAward award in DistrictHubData.Awards)
		{
			var button = new Button { Text = award.TrophyName.ToUpperInvariant() };
			string id = award.Id;
			button.Pressed += () => ShowAward(id);
			_awardButtons[award.Id] = button;
			row.AddChild(button);
		}

		return row;
	}

	private void ShowAward(string awardId)
	{
		_activeAwardId = awardId;
		UpdateAwardStyles();

		foreach (Node child in _detailHost.GetChildren())
		{
			_detailHost.RemoveChild(child);
			child.QueueFree();
		}

		HubAward award = FindAward(awardId);
		_detailHost.AddChild(BuildHolderCard(award));
		_detailHost.AddChild(BuildCompetitorsCard(award));
	}

	private void UpdateAwardStyles()
	{
		foreach (KeyValuePair<string, Button> pair in _awardButtons)
		{
			bool isActive = pair.Key == _activeAwardId;
			Button button = pair.Value;
			button.Flat = false;
			button.AddThemeFontOverride("font", _semibold);
			button.AddThemeFontSizeOverride("font_size", 12);
			button.AddThemeColorOverride("font_color", isActive ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("font_hover_color", isActive ? TextOnAccent : Colors.White);
			button.AddThemeColorOverride("font_pressed_color", isActive ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("font_focus_color", isActive ? TextOnAccent : Colors.White);
			button.AddThemeStyleboxOverride("normal", MakePill(isActive ? Accent : Colors.Transparent, 10));
			button.AddThemeStyleboxOverride("hover", MakePill(isActive ? AccentHover : HoverBg, 10));
			button.AddThemeStyleboxOverride("pressed", MakePill(isActive ? AccentHover : HoverBg, 10));
			button.AddThemeStyleboxOverride("focus", MakePill(isActive ? AccentHover : HoverBg, 10));
		}
	}

	private Control BuildHolderCard(HubAward award)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.05f,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(12));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		card.AddChild(layout);

		layout.AddChild(MakeText(award.Category.ToUpperInvariant(), _semibold, 11, Accent, HorizontalAlignment.Left));
		layout.AddChild(MakeText(award.TrophyName.ToUpperInvariant(), _bold, 28, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Gold });

		var blurb = MakeText(award.Blurb, _medium, 13, TextMuted, HorizontalAlignment.Left);
		blurb.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(blurb);

		layout.AddChild(BuildTrophyMark(award));
		layout.AddChild(BuildLeaderBlock(award.Leader, award.IsCoachAward));
		return card;
	}

	private Control BuildTrophyMark(HubAward award)
	{
		var mark = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		mark.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.92f, 0.74f, 0.28f, 0.12f),
			BorderColor = Gold,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 16,
			ContentMarginTop = 16,
			ContentMarginRight = 16,
			ContentMarginBottom = 16,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});

		var inner = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		inner.AddThemeConstantOverride("separation", -2);
		mark.AddChild(inner);
		inner.AddChild(MakeText(award.IsCoachAward ? "CUP" : "TROPHY", _semibold, 11, Gold, HorizontalAlignment.Center));
		inner.AddChild(MakeText(award.TrophyName.ToUpperInvariant(), _bold, 22, Gold, HorizontalAlignment.Center));
		inner.AddChild(MakeText("2026  ·  IN PROGRESS", _medium, 11, TextMuted, HorizontalAlignment.Center));
		return mark;
	}

	private Control BuildLeaderBlock(HubAwardCandidate leader, bool coach)
	{
		var block = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		block.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = Accent,
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

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 4);
		block.AddChild(layout);

		layout.AddChild(MakeText(coach ? "CURRENT FRONTRUNNER  ·  COACH" : "CURRENT FRONTRUNNER", _semibold, 10, Accent, HorizontalAlignment.Left));
		layout.AddChild(MakeText(leader.Name.ToUpperInvariant(), _bold, 26, TextPrimary, HorizontalAlignment.Left));

		var meta = new HBoxContainer();
		meta.AddThemeConstantOverride("separation", 10);
		TextureRect? leaderLogo = TeamLogos.TryMakeIcon(leader.TeamId, 22);
		if (leaderLogo != null)
		{
			meta.AddChild(leaderLogo);
		}

		meta.AddChild(MakeText(leader.TeamShort, _semibold, 13, Accent, HorizontalAlignment.Left));
		meta.AddChild(MakeText(coach ? "HEAD COACH" : $"{leader.Position}  ·  {leader.Year}", _medium, 13, TextMuted, HorizontalAlignment.Left));
		layout.AddChild(meta);

		layout.AddChild(MakeText(leader.HeadlineStat, _bold, 22, Green, HorizontalAlignment.Left));
		layout.AddChild(MakeText(leader.Detail, _medium, 12, TextMuted, HorizontalAlignment.Left));
		return block;
	}

	private Control BuildCompetitorsCard(HubAward award)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.15f,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(0));

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
		var headingCopy = new VBoxContainer();
		headingCopy.AddThemeConstantOverride("separation", -2);
		headingCopy.AddChild(MakeText("TOP COMPETITORS", _bold, 16, TextPrimary, HorizontalAlignment.Left));
		headingCopy.AddChild(MakeText("The three closest names in the league chasing this trophy.", _medium, 11, TextMuted, HorizontalAlignment.Left));
		heading.AddChild(headingCopy);
		layout.AddChild(heading);
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		layout.AddChild(BuildCompetitorHeader(award.IsCoachAward));

		for (int i = 0; i < award.Competitors.Count; i++)
		{
			layout.AddChild(BuildCompetitorRow(award.Competitors[i], i + 2, i % 2 == 0, award.IsCoachAward));
		}

		var footer = new PanelContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		footer.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = RowOdd,
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 10,
		});
		footer.AddChild(MakeText(
			"Awards are unofficial until the district season closes. Rankings update with Friday results.",
			_medium,
			12,
			TextMuted,
			HorizontalAlignment.Left));
		layout.AddChild(footer);
		return card;
	}

	private Control BuildCompetitorHeader(bool coach)
	{
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047059f, 0.054902f, 0.070588f, 0.96f),
			BorderColor = CardBorder,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 6,
			ContentMarginRight = 12,
			ContentMarginBottom = 6,
		});

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(MakeFixed("#", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		var name = MakeText(coach ? "COACH" : "PLAYER", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(name);
		columns.AddChild(MakeFixed("TEAM", 110, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed("STAT", 90, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		columns.AddChild(MakeFixed("GAP", 40, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		header.AddChild(columns);
		return header;
	}

	private Control BuildCompetitorRow(HubAwardCandidate candidate, int rank, bool even, bool coach)
	{
		var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = even ? RowEven : RowOdd,
			BorderColor = new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 10,
		});

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(MakeFixed($"{rank}", 28, _bold, 14, TextMuted, HorizontalAlignment.Center));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		identity.AddThemeConstantOverride("separation", -2);
		identity.AddChild(MakeText(candidate.Name, _semibold, 15, TextPrimary, HorizontalAlignment.Left));
		identity.AddChild(MakeText(coach ? "HEAD COACH" : $"{candidate.Position}  ·  {candidate.Year}", _medium, 11, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(identity);

		columns.AddChild(BuildTeamMark(candidate.TeamId, candidate.TeamShort));
		columns.AddChild(MakeFixed(candidate.HeadlineStat, 90, _bold, 13, TextPrimary, HorizontalAlignment.Right));
		columns.AddChild(MakeFixed($"+{candidate.Gap}", 40, _semibold, 13, Hairline, HorizontalAlignment.Right));
		row.AddChild(columns);
		return row;
	}

	private static HubAward FindAward(string id)
	{
		foreach (HubAward award in DistrictHubData.Awards)
		{
			if (award.Id == id)
			{
				return award;
			}
		}

		return DistrictHubData.Awards[0];
	}

	private Control BuildTeamMark(string teamId, string teamShort)
	{
		var mark = new HBoxContainer();
		mark.AddThemeConstantOverride("separation", 6);
		mark.CustomMinimumSize = new Vector2(110, 0);
		TextureRect? logo = TeamLogos.TryMakeIcon(teamId, 20);
		if (logo != null)
		{
			mark.AddChild(logo);
		}

		var label = MakeText(teamShort, _semibold, 12, Accent, HorizontalAlignment.Left);
		label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		mark.AddChild(label);
		return mark;
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
