using System;
using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// League awards desk and year-end ceremony. During the season this
/// is the unofficial ballot. On awards night it becomes the banquet:
/// District MVP, Pitcher of the Year, Freshman of the Year, All-District,
/// Coach of the Year, and state honors after the championship.
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

	private readonly Dictionary<string, Button> _honorButtons = new();
	private readonly Dictionary<AwardScope, Button> _scopeButtons = new();
	private Label _kicker = null!;
	private Label _title = null!;
	private Label _subtitle = null!;
	private Control _scopeRow = null!;
	private HFlowContainer _honorRow = null!;
	private Control _detailHost = null!;
	private HBoxContainer _actions = null!;
	private AwardScope _scope = AwardScope.District;
	private string _activeHonorId = "";
	private bool _ceremony;
	private bool _built;

	public event Action? Closed;
	public event Action<AwardScope>? Continued;

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

	public override void _UnhandledInput(InputEvent ev)
	{
		if (!_ceremony || !Visible || !ev.IsActionPressed("ui_cancel"))
		{
			return;
		}

		GoBack();
		GetViewport().SetInputAsHandled();
	}

	public void BindCeremony(AwardScope scope)
	{
		_ceremony = true;
		_scope = scope;
		_activeHonorId = "";
		if (_built)
		{
			Refresh();
		}
	}

	public void ShowHub()
	{
		_ceremony = false;
		_scope = AwardScope.District;
		_activeHonorId = "";
		if (_built)
		{
			Refresh();
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => Refresh();

	private void OnVisibilityChanged()
	{
		if (IsVisibleInTree())
		{
			Refresh();
		}
	}

	private void BuildPage()
	{
		_built = true;
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
		_scopeRow = BuildScopeRow();
		layout.AddChild(_scopeRow);

		_honorRow = new HFlowContainer();
		_honorRow.AddThemeConstantOverride("h_separation", 4);
		_honorRow.AddThemeConstantOverride("v_separation", 4);
		layout.AddChild(_honorRow);

		_detailHost = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		((HBoxContainer)_detailHost).AddThemeConstantOverride("separation", 8);
		layout.AddChild(_detailHost);

		_actions = new HBoxContainer();
		_actions.AddThemeConstantOverride("separation", 12);
		_actions.AddChild(MakeAction("GO BACK", false, 0.45f, GoBack));
		_actions.AddChild(MakeAction("CONTINUE", true, 1.55f, Continue));
		layout.AddChild(_actions);
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
		_kicker = MakeText("LEAGUE", _medium, 10, Accent, HorizontalAlignment.Left);
		identity.AddChild(_kicker);
		_title = MakeText("AWARDS", _bold, 26, TextPrimary, HorizontalAlignment.Left);
		identity.AddChild(_title);
		_subtitle = MakeText("VARSITY", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);
		return header;
	}

	private Control BuildScopeRow()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		AddScopeButton(row, AwardScope.District, "DISTRICT");
		AddScopeButton(row, AwardScope.State, "STATE");
		return row;
	}

	private void AddScopeButton(Control row, AwardScope scope, string label)
	{
		var button = new Button { Text = label };
		button.Pressed += () => ShowScope(scope);
		_scopeButtons[scope] = button;
		row.AddChild(button);
	}

	private void ShowScope(AwardScope scope)
	{
		if (_ceremony)
		{
			return;
		}

		_scope = scope;
		_activeHonorId = "";
		Refresh();
	}

	private void ShowHonor(string honorId)
	{
		_activeHonorId = honorId;
		Refresh();
	}

	private void Refresh()
	{
		if (!_built)
		{
			return;
		}

		AwardsCeremony ceremony = AwardsDesk.For(_session, _scope);
		if (string.IsNullOrEmpty(_activeHonorId) || FindHonor(ceremony, _activeHonorId) == null)
		{
			_activeHonorId = ceremony.Honors.Count == 0 ? "" : ceremony.Honors[0].Id;
		}

		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		_kicker.Text = _ceremony
			? $"{ceremony.ScopeLabel}  ·  YEAR-END CEREMONY"
			: $"LEAGUE  ·  {GameSession.SeasonYear} SEASON";
		_title.Text = _ceremony ? ceremony.Headline : "AWARDS";
		string ballot = ceremony.Official ? "OFFICIAL" : "UNOFFICIAL BALLOT";
		_subtitle.Text = varsity
			? $"VARSITY  ·  {ceremony.ScopeLabel}  ·  {ballot}"
			: $"JV  ·  {ceremony.ScopeLabel}  ·  {ballot}";
		_scopeRow.Visible = !_ceremony;
		_actions.Visible = _ceremony;
		if (_ceremony && _actions.GetChildCount() >= 2 && _actions.GetChild(1) is Button continueButton)
		{
			continueButton.Text = ceremony.ContinueLabel;
		}

		foreach (KeyValuePair<AwardScope, Button> pair in _scopeButtons)
		{
			StyleButton(pair.Value, pair.Key == _scope, _ceremony && pair.Key != _scope);
		}

		RebuildHonorButtons(ceremony);
		RebuildDetails(ceremony);
	}

	private void RebuildHonorButtons(AwardsCeremony ceremony)
	{
		foreach (Node child in _honorRow.GetChildren())
		{
			child.QueueFree();
		}

		_honorButtons.Clear();
		foreach (AwardHonor honor in ceremony.Honors)
		{
			var button = new Button { Text = honor.Name.ToUpperInvariant() };
			string id = honor.Id;
			button.Pressed += () => ShowHonor(id);
			_honorButtons[honor.Id] = button;
			_honorRow.AddChild(button);
		}

		CallDeferred(nameof(StyleHonorButtons));
	}

	private void StyleHonorButtons()
	{
		foreach (KeyValuePair<string, Button> pair in _honorButtons)
		{
			if (IsInstanceValid(pair.Value))
			{
				StyleButton(pair.Value, pair.Key == _activeHonorId, false);
			}
		}
	}

	private void StyleButton(Button button, bool isActive, bool locked)
	{
		button.Disabled = locked;
		button.Flat = false;
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		Color text = isActive ? TextOnAccent : TextPrimary;
		button.AddThemeColorOverride("font_color", text);
		button.AddThemeColorOverride("font_hover_color", isActive ? TextOnAccent : Colors.White);
		button.AddThemeColorOverride("font_pressed_color", text);
		button.AddThemeColorOverride("font_focus_color", isActive ? TextOnAccent : Colors.White);
		button.AddThemeColorOverride("font_disabled_color", TextMuted);
		button.AddThemeStyleboxOverride("normal", MakePill(isActive ? Accent : Colors.Transparent, 10));
		button.AddThemeStyleboxOverride("hover", MakePill(isActive ? AccentHover : HoverBg, 10));
		button.AddThemeStyleboxOverride("pressed", MakePill(isActive ? AccentHover : HoverBg, 10));
		button.AddThemeStyleboxOverride("focus", MakePill(isActive ? AccentHover : HoverBg, 10));
		button.AddThemeStyleboxOverride("disabled", MakePill(Colors.Transparent, 10));
	}

	private void RebuildDetails(AwardsCeremony ceremony)
	{
		foreach (Node child in _detailHost.GetChildren())
		{
			_detailHost.RemoveChild(child);
			child.QueueFree();
		}

		AwardHonor? honor = FindHonor(ceremony, _activeHonorId);
		if (honor == null)
		{
			return;
		}

		if (honor.IsTeamAward)
		{
			_detailHost.AddChild(BuildIntroCard(ceremony, honor));
			_detailHost.AddChild(BuildTeamCard(ceremony, honor));
			return;
		}

		_detailHost.AddChild(BuildHolderCard(ceremony, honor));
		_detailHost.AddChild(BuildFieldCard(ceremony, honor));
	}

	private Control BuildIntroCard(AwardsCeremony ceremony, AwardHonor honor)
	{
		var card = MakeCard(1.05f, 12);
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		card.AddChild(layout);
		layout.AddChild(MakeText(
			ceremony.Official ? "FIRST TEAM" : "PROJECTED FIRST TEAM",
			_semibold,
			11,
			Accent,
			HorizontalAlignment.Left));
		layout.AddChild(MakeText(honor.Name.ToUpperInvariant(), _bold, 28, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Gold });
		var blurb = MakeText(honor.Blurb, _medium, 13, TextMuted, HorizontalAlignment.Left);
		blurb.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(blurb);
		layout.AddChild(BuildTrophyMark(ceremony, honor));
		return card;
	}

	private Control BuildHolderCard(AwardsCeremony ceremony, AwardHonor honor)
	{
		var card = MakeCard(1.05f, 12);
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		card.AddChild(layout);
		layout.AddChild(MakeText(honor.Name.ToUpperInvariant(), _semibold, 11, Accent, HorizontalAlignment.Left));
		layout.AddChild(MakeText(honor.Name.ToUpperInvariant(), _bold, 28, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Gold });
		var blurb = MakeText(honor.Blurb, _medium, 13, TextMuted, HorizontalAlignment.Left);
		blurb.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(blurb);
		layout.AddChild(BuildTrophyMark(ceremony, honor));
		if (honor.Winner != null)
		{
			layout.AddChild(BuildWinnerBlock(honor.Winner, honor.IsCoachAward, ceremony.Official));
		}

		return card;
	}

	private Control BuildTrophyMark(AwardsCeremony ceremony, AwardHonor honor)
	{
		var mark = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
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
		inner.AddChild(MakeText(
			honor.IsTeamAward ? "FIRST TEAM" : honor.IsCoachAward ? "CUP" : "TROPHY",
			_semibold,
			11,
			Gold,
			HorizontalAlignment.Center));
		inner.AddChild(MakeText(honor.Name.ToUpperInvariant(), _bold, 22, Gold, HorizontalAlignment.Center));
		inner.AddChild(MakeText(
			$"{ceremony.SeasonYear}  ·  {(ceremony.Official ? "OFFICIAL" : "IN PROGRESS")}",
			_medium,
			11,
			TextMuted,
			HorizontalAlignment.Center));
		return mark;
	}

	private Control BuildWinnerBlock(AwardHonoree winner, bool coach, bool official)
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
		layout.AddChild(MakeText(
			official
				? coach ? "COACH OF THE YEAR" : "WINNER"
				: coach ? "CURRENT FRONTRUNNER  ·  COACH" : "CURRENT FRONTRUNNER",
			_semibold,
			10,
			Accent,
			HorizontalAlignment.Left));
		var nameLabel = MakeText(winner.Name.ToUpperInvariant(), _bold, 26, TextPrimary, HorizontalAlignment.Left);
		if (!coach)
		{
			PlayerLinks.MakeClickable(nameLabel, winner.TeamId, winner.Name);
		}

		layout.AddChild(nameLabel);

		var meta = new HBoxContainer();
		meta.AddThemeConstantOverride("separation", 10);
		TextureRect? logo = TeamLogos.TryMakeIcon(winner.TeamId, 22);
		if (logo != null)
		{
			meta.AddChild(logo);
		}

		meta.AddChild(MakeText(winner.TeamShort, _semibold, 13, Accent, HorizontalAlignment.Left));
		meta.AddChild(MakeText(
			coach ? "HEAD COACH" : $"{winner.Position}  ·  {winner.Year}",
			_medium,
			13,
			TextMuted,
			HorizontalAlignment.Left));
		layout.AddChild(meta);
		layout.AddChild(MakeText(winner.HeadlineStat, _bold, 22, Green, HorizontalAlignment.Left));
		layout.AddChild(MakeText(winner.Detail, _medium, 12, TextMuted, HorizontalAlignment.Left));
		return block;
	}

	private Control BuildFieldCard(AwardsCeremony ceremony, AwardHonor honor)
	{
		var card = MakeCard(1.15f, 0);
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);
		layout.AddChild(BuildSectionHead(
			ceremony.Official ? "THE FIELD" : "TOP COMPETITORS",
			ceremony.Official
				? "The names on the ballot behind this trophy."
				: "The three closest names chasing this trophy."));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		layout.AddChild(BuildFieldHeader(honor.IsCoachAward));
		for (int i = 0; i < honor.Field.Count; i++)
		{
			layout.AddChild(BuildFieldRow(honor.Field[i], i + 2, i % 2 == 0, honor.IsCoachAward));
		}

		layout.AddChild(BuildFooter(ceremony.Official
			? "Ballots are closed. These are the official year-end honors."
			: "Awards stay unofficial until the district season closes. Rankings update with Friday results."));
		return card;
	}

	private Control BuildTeamCard(AwardsCeremony ceremony, AwardHonor honor)
	{
		var card = MakeCard(1.15f, 0);
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);
		layout.AddChild(BuildSectionHead(
			ceremony.Official ? "THE TEAM" : "PROJECTED TEAM",
			ceremony.Official
				? "First-team selections by position."
				: "The first-team board as it stands today."));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		layout.AddChild(BuildTeamHeader());
		for (int i = 0; i < honor.Field.Count; i++)
		{
			layout.AddChild(BuildTeamRow(honor.Field[i], i % 2 == 0));
		}

		layout.AddChild(BuildFooter(ceremony.Official
			? "First-team honors are final for this baseball year."
			: "The first team is unofficial until awards night."));
		return card;
	}

	private Control BuildSectionHead(string title, string blurb)
	{
		var heading = new PanelContainer();
		heading.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047059f, 0.054902f, 0.070588f, 0.96f),
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		});
		var copy = new VBoxContainer();
		copy.AddThemeConstantOverride("separation", -2);
		copy.AddChild(MakeText(title, _bold, 16, TextPrimary, HorizontalAlignment.Left));
		copy.AddChild(MakeText(blurb, _medium, 11, TextMuted, HorizontalAlignment.Left));
		heading.AddChild(copy);
		return heading;
	}

	private Control BuildFieldHeader(bool coach)
	{
		var header = MakeListHeader();
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(MakeFixed("#", 28, _semibold, 10, TextMuted, HorizontalAlignment.Center));
		var name = MakeText(coach ? "COACH" : "PLAYER", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(name);
		columns.AddChild(MakeFixed("TEAM", 110, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed("STAT", 90, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		header.AddChild(columns);
		return header;
	}

	private Control BuildTeamHeader()
	{
		var header = MakeListHeader();
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(MakeFixed("POS", 40, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		var name = MakeText("PLAYER", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(name);
		columns.AddChild(MakeFixed("TEAM", 110, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(MakeFixed("STAT", 90, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		header.AddChild(columns);
		return header;
	}

	private static PanelContainer MakeListHeader()
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
		return header;
	}

	private Control BuildFieldRow(AwardHonoree candidate, int rank, bool even, bool coach)
	{
		var row = MakeRow(even);
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(MakeFixed($"{rank}", 28, _bold, 14, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(BuildIdentity(candidate, coach));
		columns.AddChild(BuildTeamMark(candidate.TeamId, candidate.TeamShort));
		columns.AddChild(MakeFixed(candidate.HeadlineStat, 90, _bold, 13, TextPrimary, HorizontalAlignment.Right));
		row.AddChild(columns);
		return row;
	}

	private Control BuildTeamRow(AwardHonoree member, bool even)
	{
		var row = MakeRow(even);
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(MakeFixed(member.Position, 40, _bold, 13, Accent, HorizontalAlignment.Left));
		columns.AddChild(BuildIdentity(member, false));
		columns.AddChild(BuildTeamMark(member.TeamId, member.TeamShort));
		columns.AddChild(MakeFixed(member.HeadlineStat, 90, _bold, 13, TextPrimary, HorizontalAlignment.Right));
		row.AddChild(columns);
		return row;
	}

	private Control BuildIdentity(AwardHonoree person, bool coach)
	{
		var identity = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		identity.AddThemeConstantOverride("separation", -2);
		var name = MakeText(person.Name, _semibold, 15, TextPrimary, HorizontalAlignment.Left);
		if (!coach)
		{
			PlayerLinks.MakeClickable(name, person.TeamId, person.Name);
		}

		identity.AddChild(name);
		identity.AddChild(MakeText(
			coach ? "HEAD COACH" : $"{person.Position}  ·  {person.Year}",
			_medium,
			11,
			TextMuted,
			HorizontalAlignment.Left));
		return identity;
	}

	private Control BuildFooter(string copy)
	{
		var footer = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		footer.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = RowOdd,
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 10,
		});
		var label = MakeText(copy, _medium, 12, TextMuted, HorizontalAlignment.Left);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		footer.AddChild(label);
		return footer;
	}

	private PanelContainer MakeCard(float stretch, float pad)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = stretch,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(pad));
		return card;
	}

	private static PanelContainer MakeRow(bool even)
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
		return row;
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
		SchoolLinks.MakeClickable(mark, teamId);
		return mark;
	}

	private static AwardHonor? FindHonor(AwardsCeremony ceremony, string id)
	{
		foreach (AwardHonor honor in ceremony.Honors)
		{
			if (honor.Id == id)
			{
				return honor;
			}
		}

		return ceremony.Honors.Count == 0 ? null : ceremony.Honors[0];
	}

	private void Continue()
	{
		UiSounds.Play(UiSound.Navigate);
		Continued?.Invoke(_scope);
	}

	private void GoBack()
	{
		UiSounds.Play(UiSound.Back);
		Closed?.Invoke();
	}

	private Button MakeAction(string text, bool primary, float stretch, Action onPressed)
	{
		var button = new Button
		{
			Text = text,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = stretch,
			CustomMinimumSize = new Vector2(0, 44),
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 15);
		if (primary)
		{
			button.AddThemeColorOverride("font_color", TextOnAccent);
			button.AddThemeColorOverride("font_hover_color", TextOnAccent);
			button.AddThemeColorOverride("font_pressed_color", TextOnAccent);
			button.AddThemeColorOverride("font_focus_color", TextOnAccent);
			button.AddThemeStyleboxOverride("normal", MakeFilled(Accent));
			button.AddThemeStyleboxOverride("hover", MakeFilled(AccentHover));
			button.AddThemeStyleboxOverride("pressed", MakeFilled(AccentHover));
			button.AddThemeStyleboxOverride("focus", MakeFilled(AccentHover));
		}
		else
		{
			button.AddThemeColorOverride("font_color", TextPrimary);
			button.AddThemeStyleboxOverride("normal", MakeOutlined(CardInner, CardBorder));
			button.AddThemeStyleboxOverride("hover", MakeOutlined(HoverBg, Accent));
			button.AddThemeStyleboxOverride("pressed", MakeOutlined(HoverBg, Accent));
			button.AddThemeStyleboxOverride("focus", MakeOutlined(HoverBg, Accent));
		}

		button.Pressed += onPressed;
		return button;
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

	private static StyleBoxFlat MakeFilled(Color bg) =>
		new()
		{
			BgColor = bg,
			ContentMarginLeft = 16,
			ContentMarginTop = 10,
			ContentMarginRight = 16,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
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
			ContentMarginLeft = 16,
			ContentMarginTop = 10,
			ContentMarginRight = 16,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};
}
