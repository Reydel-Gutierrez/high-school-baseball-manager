using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;
using HSBM.Simulation;

/// <summary>
/// District Open Play scouting board and Signing Week recruiting.
/// User and AI schools share the same offer/decision rules.
/// </summary>
public partial class TryoutsPage : Control
{
	private const string FilterAll = "all";
	private const string FilterUnsigned = "unsigned";
	private const string FilterOffered = "offered";
	private const string FilterTargets = "targets";
	private const string FilterSigned = "signed";
	private const string FilterPitchers = "pitchers";

	private static readonly int[] OfferAmounts = [0, 250, 450, 650, 900, 1_200];

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color RowSelected = new(0.956863f, 0.643137f, 0.109804f, 0.14f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _cascadeLogo = null!;

	private Label _title = null!;
	private Label _subtitle = null!;
	private Label _statusChip = null!;
	private Label _poolCaption = null!;
	private Label _poolValue = null!;
	private Label _signedCaption = null!;
	private Label _signedValue = null!;
	private Label _budgetCaption = null!;
	private Label _budgetValue = null!;
	private Label _windowCaption = null!;
	private Label _windowValue = null!;
	private Label _warningLabel = null!;
	private Control _warningBanner = null!;
	private VBoxContainer _rowsHost = null!;
	private VBoxContainer _dossierHost = null!;
	private Button _advanceButton = null!;
	private Button _skipButton = null!;
	private readonly Dictionary<string, Button> _filterButtons = new();

	private string _filter = FilterAll;
	private Guid _selectedId;
	private int _offerAmount = 450;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_cascadeLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		_session.TryoutsChanged += RefreshBoard;
		VisibilityChanged += OnVisibilityChanged;
		ApplyActiveTeam();
		RefreshBoard();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
			_session.TryoutsChanged -= RefreshBoard;
		}

		VisibilityChanged -= OnVisibilityChanged;
	}

	private void OnVisibilityChanged()
	{
		if (IsVisibleInTree())
		{
			RefreshBoard();
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => ApplyActiveTeam();

	private void ApplyActiveTeam()
	{
		_subtitle.Text = $"{_session.Organization.Name.ToUpperInvariant()}  ·  DISTRICT OPEN PLAY";
	}

	private void BuildPage()
	{
		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 8);
		margin.AddThemeConstantOverride("margin_top", 6);
		margin.AddThemeConstantOverride("margin_right", 8);
		margin.AddThemeConstantOverride("margin_bottom", 6);
		AddChild(margin);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		margin.AddChild(layout);
		layout.AddChild(BuildHeader());
		layout.AddChild(BuildFilters());
		layout.AddChild(BuildWarning());

		var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 6);
		layout.AddChild(body);
		body.AddChild(BuildListCard());
		body.AddChild(BuildDossierCard());
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
		identity.AddThemeConstantOverride("separation", -4);
		identity.AddChild(MakeText($"ORGANIZATION  ·  {GameSession.SeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		_title = MakeText("DISTRICT OPEN PLAY", _bold, 22, TextPrimary, HorizontalAlignment.Left);
		identity.AddChild(_title);
		_subtitle = MakeText("CASCADE REGIONAL HIGH SCHOOL", _medium, 11, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);
		header.AddChild(BuildHeaderMeta());
		return header;
	}

	private Control BuildHeaderMeta()
	{
		var meta = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		meta.AddThemeConstantOverride("separation", 2);

		_statusChip = MakeText("SIGNING WEEK", _bold, 12, Accent, HorizontalAlignment.Right);
		meta.AddChild(_statusChip);

		var stats = new HBoxContainer();
		stats.AddThemeConstantOverride("separation", 14);
		stats.AddChild(BuildHeaderStat("POOL", "200", out _poolCaption, out _poolValue));
		stats.AddChild(BuildHeaderStat("SIGNED", "0/10", out _signedCaption, out _signedValue));
		stats.AddChild(BuildHeaderStat("BUDGET", "$0", out _budgetCaption, out _budgetValue));
		stats.AddChild(BuildHeaderStat("WINDOW", "JAN 26–FEB 1", out _windowCaption, out _windowValue));
		meta.AddChild(stats);
		return meta;
	}

	private Control BuildHeaderStat(string label, string value, out Label captionLabel, out Label valueLabel)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", -4);
		captionLabel = MakeText(label, _medium, 9, TextMuted, HorizontalAlignment.Right);
		col.AddChild(captionLabel);
		valueLabel = MakeText(value, _bold, 16, TextPrimary, HorizontalAlignment.Right);
		col.AddChild(valueLabel);
		return col;
	}

	private Control BuildWarning()
	{
		_warningBanner = new PanelContainer { Visible = false };
		_warningBanner.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.906f, 0.298f, 0.235f, 0.14f),
			BorderColor = Urgent,
			BorderWidthLeft = 2,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		_warningLabel = MakeText(string.Empty, _medium, 12, TextPrimary, HorizontalAlignment.Left);
		_warningLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_warningBanner.AddChild(_warningLabel);
		return _warningBanner;
	}

	private Control BuildFilters()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		AddFilter(row, FilterAll, "ALL");
		AddFilter(row, FilterUnsigned, "UNSIGNED");
		AddFilter(row, FilterOffered, "YOUR OFFERS");
		AddFilter(row, FilterTargets, "TARGETS");
		AddFilter(row, FilterSigned, "SIGNED");
		AddFilter(row, FilterPitchers, "PITCHERS");
		var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddChild(spacer);
		_skipButton = new Button { Text = "CONTINUE TO SIGNING WEEK" };
		_skipButton.Pressed += () =>
		{
			_session.ContinueToSigningWeek();
			RefreshBoard();
		};
		row.AddChild(_skipButton);
		_advanceButton = new Button { Text = "ADVANCE DAY" };
		_advanceButton.Pressed += () =>
		{
			_session.AdvanceClubhouseDay();
			RefreshBoard();
		};
		row.AddChild(_advanceButton);
		return row;
	}

	private void AddFilter(HBoxContainer row, string key, string label)
	{
		var button = new Button { Text = label };
		button.Pressed += () =>
		{
			_filter = key;
			RefreshBoard();
		};
		_filterButtons[key] = button;
		row.AddChild(button);
	}

	private Control BuildListCard()
	{
		var card = MakeCard(true);
		card.SizeFlagsStretchRatio = 1.55f;
		card.ClipContents = true;
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);
		layout.AddChild(BuildColumnHeader());
		layout.AddChild(new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 2),
			Color = Accent,
		});
		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
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
			ContentMarginLeft = 12,
			ContentMarginTop = 7,
			ContentMarginRight = 12,
			ContentMarginBottom = 7,
		});
		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		var player = MakeText("PLAYER", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		player.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(player);
		columns.AddChild(MakeFixed("POS", 36, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("YEAR", 36, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("OVR", 56, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("POT", 56, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("G", 28, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("ON", 36, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("OFFERS", 52, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("STATUS", 78, TextMuted, _semibold, 10));
		header.AddChild(columns);
		return header;
	}

	private Control BuildDossierCard()
	{
		var card = MakeCard(true);
		card.SizeFlagsStretchRatio = 1.05f;
		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		card.AddChild(scroll);
		_dossierHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_dossierHost.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(PadBody(_dossierHost));
		return card;
	}

	private void RefreshBoard()
	{
		if (_rowsHost == null)
		{
			return;
		}

		RecruitingMarket market = _session.RecruitingMarket;
		DistrictOpenPlay openPlay = _session.OpenPlay;
		DateOnly today = _session.CurrentDate;
		string orgId = _session.CatalogOrganizationId;
		OrganizationState? school = _session.RecruitingSchool(orgId);
		int signed = market.SignedCount(orgId);
		int target = market.IncomingTarget(orgId);
		int available = school == null
			? 0
			: ProgramBudget.RecruitingAvailable(school.Profile, market.Agreements, orgId, market.Offers);

		bool openPlayActive = openPlay.Window.OccursOn(today);
		bool signing = SeasonCalendar.SigningWeek(GameSession.SeasonYear).OccursOn(today)
			|| today == SeasonCalendar.SigningDeadline(GameSession.SeasonYear);
		bool afterSigning = today > SeasonCalendar.SigningDeadline(GameSession.SeasonYear);
		_title.Text = signing ? "SIGNING WEEK" : "DISTRICT OPEN PLAY";
		_statusChip.Text = StatusCopy(today, openPlay.Window, signing);
		_statusChip.AddThemeColorOverride("font_color", signing || openPlayActive ? Accent : Green);
		_poolCaption.Text = "POOL";
		_poolValue.Text = market.UnsignedProspects.Count.ToString(CultureInfo.InvariantCulture);
		_signedCaption.Text = "SIGNED";
		_signedValue.Text = $"{signed}/{Math.Max(target, signed)}";
		_budgetCaption.Text = "BUDGET";
		_budgetValue.Text = PlayerAgreement.FormatMoney(available);
		_windowCaption.Text = signing ? "SIGNING" : "OPEN PLAY";
		_windowValue.Text = signing
			? SeasonCalendar.SigningWeek(GameSession.SeasonYear).FormatDates().ToUpperInvariant()
			: openPlay.Window.FormatDates().ToUpperInvariant();

		_advanceButton.Visible = !afterSigning;
		_advanceButton.Text = signing
			? (today >= SeasonCalendar.SigningDeadline(GameSession.SeasonYear) ? "CLOSE SIGNING" : "ADVANCE SIGNING DAY")
			: "ADVANCE DAY";
		_skipButton.Visible = today < SeasonCalendar.SigningWeek(GameSession.SeasonYear).Start;

		int pendingUser = 0;
		foreach (RecruitingOffer offer in market.Offers)
		{
			if (offer.IsActive && offer.OrganizationId == orgId)
			{
				pendingUser++;
			}
		}

		bool showWarn = (signing && pendingUser > 0) || !string.IsNullOrEmpty(_session.LastRecruitingError);
		_warningBanner.Visible = showWarn;
		_warningLabel.Text = !string.IsNullOrEmpty(_session.LastRecruitingError)
			? _session.LastRecruitingError
			: showWarn
				? "Other district schools are recruiting the same players. Offers sit until the player decides — money is only one factor."
				: string.Empty;

		foreach (KeyValuePair<string, Button> pair in _filterButtons)
		{
			bool on = pair.Key == _filter;
			pair.Value.AddThemeColorOverride("font_color", on ? TextOnAccent : TextPrimary);
			pair.Value.AddThemeStyleboxOverride("normal", MakePill(on ? Accent : CardInner, 10));
			pair.Value.AddThemeStyleboxOverride("hover", MakePill(on ? AccentHover : HoverBg, 10));
		}

		List<Player> rows = VisiblePlayers(market);
		if (rows.Count > 0 && ( _selectedId == Guid.Empty || rows.TrueForAll(p => p.Id != _selectedId)))
		{
			_selectedId = rows[0].Id;
		}

		foreach (Node child in _rowsHost.GetChildren())
		{
			child.QueueFree();
		}

		for (int i = 0; i < rows.Count; i++)
		{
			_rowsHost.AddChild(BuildRow(rows[i], openPlay, market, today, i % 2 == 0));
		}

		RebuildDossier(market, openPlay, today, school, signing, available);
	}

	private List<Player> VisiblePlayers(RecruitingMarket market)
	{
		string orgId = _session.CatalogOrganizationId;
		var list = new List<Player>();
		foreach (Player player in market.Prospects)
		{
			PlayerAgreement? agreement = market.AgreementFor(player.Id);
			RecruitingOffer? yours = market.PendingOffer(orgId, player.Id);
			bool signed = agreement != null;
			bool unsigned = player.IsUnsigned && !signed;
			bool offered = yours != null;
			bool targeted = _session.IsScoutTarget(player.Id);
			bool pitcher = player.IsPitcher;
			bool include = _filter switch
			{
				FilterUnsigned => unsigned,
				FilterOffered => offered,
				FilterTargets => targeted,
				FilterSigned => signed && agreement!.OrganizationId == orgId,
				FilterPitchers => pitcher,
				_ => true,
			};
			if (include)
			{
				list.Add(player);
			}
		}

		list.Sort((a, b) =>
		{
			int looks = Math.Max(1, _session.OpenPlay.LooksOn(_session.CurrentDate));
			int scouted = TryoutScouting.Midpoint(b.Overall, looks, b.Id.GetHashCode())
				.CompareTo(TryoutScouting.Midpoint(a.Overall, looks, a.Id.GetHashCode()));
			return scouted != 0 ? scouted : string.Compare(a.LastName, b.LastName, StringComparison.Ordinal);
		});
		return list;
	}

	private Control BuildRow(Player player, DistrictOpenPlay openPlay, RecruitingMarket market, DateOnly today, bool even)
	{
		bool selected = player.Id == _selectedId;
		var row = new PanelContainer { MouseDefaultCursorShape = CursorShape.PointingHand };
		row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = selected ? RowSelected : even ? RowEven : RowOdd,
			BorderColor = selected ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		});
		Guid id = player.Id;
		row.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				_selectedId = id;
				RefreshBoard();
			}
		};

		int looks = Math.Max(1, openPlay.LooksOn(today));
		OpenPlayLine? line = openPlay.VisibleLine(player.Id, today);
		string ovr = TryoutScouting.FormatOverall(player.Overall, looks, player.Id.GetHashCode());
		string pot = TryoutScouting.FormatPotential(player.Overall, player.Potential, looks, player.Id.GetHashCode());
		string games = line == null ? "—" : line.Games.ToString(CultureInfo.InvariantCulture);
		int interest = market.InterestFor(player.Id).Count;
		int offers = market.PendingForPlayer(player.Id).Count;
		PlayerAgreement? agreement = market.AgreementFor(player.Id);
		string status;
		Color statusColor;
		if (agreement != null)
		{
			OrganizationState? school = _session.RecruitingSchool(agreement.OrganizationId);
			status = school?.ShortName.ToUpperInvariant() ?? "SIGNED";
			statusColor = agreement.OrganizationId == _session.CatalogOrganizationId ? Green : Urgent;
		}
		else if (market.PendingOffer(_session.CatalogOrganizationId, player.Id) != null)
		{
			status = "OFFERED";
			statusColor = Accent;
		}
		else
		{
			status = "UNSIGNED";
			statusColor = TextMuted;
		}

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		var name = MakeText(player.FullName, _semibold, 14, TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(name);
		columns.AddChild(MakeFixed(player.DisplayPosition, 36, TextMuted));
		columns.AddChild(MakeFixed(player.Grade.ToAbbrev(), 36, TextMuted));
		columns.AddChild(MakeFixed(ovr, 56, Accent, _bold));
		columns.AddChild(MakeFixed(pot, 56, pot == "?" ? TextMuted : Accent, _bold));
		columns.AddChild(MakeFixed(games, 28, games == "—" ? TextMuted : Accent, _bold));
		columns.AddChild(MakeFixed(interest.ToString(CultureInfo.InvariantCulture), 36, interest > 0 ? Accent : TextMuted, _bold));
		columns.AddChild(MakeFixed(offers.ToString(CultureInfo.InvariantCulture), 52, offers > 0 ? Accent : TextMuted, _bold));
		columns.AddChild(MakeFixed(status, 78, statusColor, _semibold, 11));
		row.AddChild(columns);
		return row;
	}

	private void RebuildDossier(
		RecruitingMarket market,
		DistrictOpenPlay openPlay,
		DateOnly today,
		OrganizationState? school,
		bool signing,
		int available)
	{
		foreach (Node child in _dossierHost.GetChildren())
		{
			child.QueueFree();
		}

		Player? player = _session.FindProspect(_selectedId);
		if (player == null)
		{
			_dossierHost.AddChild(MakeText("Select a player.", _medium, 13, TextMuted, HorizontalAlignment.Left));
			return;
		}

		PlayerAgreement? agreement = market.AgreementFor(player.Id);
		PlayerAgreementSnapshot? snapshot = null;
		if (agreement != null)
		{
			OrganizationState? signedSchool = _session.RecruitingSchool(agreement.OrganizationId);
			snapshot = new PlayerAgreementSnapshot(
				signedSchool?.DisplayName ?? agreement.OrganizationId,
				agreement.StartSeasonYear,
				agreement.YearsRemaining,
				agreement.TermYears,
				agreement.AnnualSponsorship,
				agreement.TotalCommitment);
		}

		string teamShort = agreement == null
			? "Open Play"
			: (_session.RecruitingSchool(agreement.OrganizationId)?.ShortName ?? "Signed");
		var profile = new PlayerProfileCard
		{
			FrameVisible = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_dossierHost.AddChild(profile);
		profile.Bind(PlayerProfiles.FromOpenPlay(
			player,
			openPlay,
			today,
			teamShort,
			snapshot,
			_session.InterestCopy(player.Id)));

		_dossierHost.AddChild(MakeText("PRIORITIES", _semibold, 10, Accent, HorizontalAlignment.Left));
		_dossierHost.AddChild(MakeText($"{player.Priorities.Label}  ·  {player.Priorities.Hook}", _medium, 13, TextPrimary, HorizontalAlignment.Left));
		_dossierHost.AddChild(MakeText("SCHOOL INTEREST", _semibold, 10, Accent, HorizontalAlignment.Left));
		_dossierHost.AddChild(MakeText(_session.InterestCopy(player.Id), _medium, 13, TextPrimary, HorizontalAlignment.Left));
		if (school != null)
		{
			var preview = new RecruitingOffer
			{
				PlayerId = player.Id,
				OrganizationId = school.Id,
				AnnualSponsorship = _offerAmount,
				TermYears = Math.Max(1, player.RemainingEligibility),
			};
			_dossierHost.AddChild(MakeText(
				RecruitingEvaluator.FitSummary(player, school, market.EvaluationRoster(school.Id), preview),
				_medium,
				12,
				TextMuted,
				HorizontalAlignment.Left));
		}

		IReadOnlyList<RecruitingOffer> pending = market.PendingForPlayer(player.Id);
		if (pending.Count > 0)
		{
			_dossierHost.AddChild(MakeText("COMPETING OFFERS", _semibold, 10, Accent, HorizontalAlignment.Left));
			foreach (RecruitingOffer offer in pending)
			{
				OrganizationState? bidder = _session.RecruitingSchool(offer.OrganizationId);
				string who = bidder?.ShortName.ToUpperInvariant() ?? offer.OrganizationId;
				string mine = offer.OrganizationId == _session.CatalogOrganizationId ? "  ·  YOU" : "";
				_dossierHost.AddChild(MakeText(
					$"{who}{mine}  ·  {PlayerAgreement.FormatMoney(offer.AnnualSponsorship)}/yr  ·  {offer.TermYears} yr",
					_medium,
					12,
					offer.OrganizationId == _session.CatalogOrganizationId ? Accent : TextMuted,
					HorizontalAlignment.Left));
			}
		}

		if (agreement != null)
		{
			_dossierHost.AddChild(MakeText(
				agreement.OrganizationId == _session.CatalogOrganizationId
					? "Committed to your program."
					: "Committed elsewhere.",
				_semibold,
				13,
				agreement.OrganizationId == _session.CatalogOrganizationId ? Green : Urgent,
				HorizontalAlignment.Left));
			return;
		}

		if (!signing)
		{
			_dossierHost.AddChild(MakeText(
				"Offers open when Open Play ends and Signing Week begins.",
				_medium,
				12,
				TextMuted,
				HorizontalAlignment.Left));
			var targetRow = new HBoxContainer();
			targetRow.AddThemeConstantOverride("separation", 6);
			bool watching = _session.IsScoutTarget(player.Id);
			targetRow.AddChild(MakeAction(watching ? "REMOVE TARGET" : "ADD TARGET", watching ? Urgent : Hairline, () =>
			{
				_session.ToggleScoutTarget(player);
				RefreshBoard();
			}));
			_dossierHost.AddChild(targetRow);
			return;
		}

		_dossierHost.AddChild(MakeText(
			$"Program budget left for sponsorships: {PlayerAgreement.FormatMoney(available)}",
			_medium,
			12,
			TextMuted,
			HorizontalAlignment.Left));
		_dossierHost.AddChild(MakeText("ANNUAL SPONSORSHIP", _semibold, 10, Accent, HorizontalAlignment.Left));
		var amounts = new HBoxContainer();
		amounts.AddThemeConstantOverride("separation", 4);
		foreach (int amount in OfferAmounts)
		{
			int chosen = amount;
			var chip = new Button
			{
				Text = amount == 0 ? "$0" : PlayerAgreement.FormatMoney(amount),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			bool on = _offerAmount == amount;
			chip.AddThemeFontOverride("font", _semibold);
			chip.AddThemeFontSizeOverride("font_size", 11);
			chip.AddThemeColorOverride("font_color", on ? TextOnAccent : TextPrimary);
			chip.AddThemeStyleboxOverride("normal", MakePill(on ? Accent : CardInner, 8));
			chip.AddThemeStyleboxOverride("hover", MakePill(on ? AccentHover : HoverBg, 8));
			chip.Pressed += () =>
			{
				_offerAmount = chosen;
				RefreshBoard();
			};
			amounts.AddChild(chip);
		}

		_dossierHost.AddChild(amounts);
		_dossierHost.AddChild(MakeText(
			$"{PlayerAgreement.FormatMoney(_offerAmount)} per year for {player.RemainingEligibility} years  ·  {PlayerAgreement.FormatMoney(_offerAmount * Math.Max(1, player.RemainingEligibility))} total",
			_medium,
			12,
			TextMuted,
			HorizontalAlignment.Left));

		Guid id = player.Id;
		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 6);
		bool targeted = _session.IsScoutTarget(id);
		actions.AddChild(MakeAction(targeted ? "REMOVE TARGET" : "ADD TARGET", targeted ? Urgent : Hairline, () =>
		{
			_session.ToggleScoutTarget(player);
			RefreshBoard();
		}));
		if (signing)
		{
			actions.AddChild(MakeAction("MAKE OFFER", Accent, () =>
			{
				_session.TryPlaceRecruitingOffer(id, _offerAmount);
				if (!_session.IsScoutTarget(id))
				{
					_session.AddScoutTarget(player);
				}

				RefreshBoard();
			}));
			if (market.PendingOffer(_session.CatalogOrganizationId, id) != null)
			{
				actions.AddChild(MakeAction("WITHDRAW", Urgent, () => _session.TryWithdrawRecruitingOffer(id)));
			}
		}

		_dossierHost.AddChild(actions);
	}

	private Button MakeAction(string label, Color color, Action pressed)
	{
		var button = new Button
		{
			Text = label,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		button.AddThemeFontOverride("font", _bold);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", color == Accent ? TextOnAccent : TextPrimary);
		button.AddThemeStyleboxOverride("normal", MakePill(color == Hairline ? CardInner : color, 10));
		button.AddThemeStyleboxOverride("hover", MakePill(color == Accent ? AccentHover : color, 10));
		button.Pressed += () => pressed();
		return button;
	}

	private static string StatusCopy(DateOnly today, SeasonEvent openPlay, bool signing)
	{
		if (signing)
		{
			return today >= SeasonCalendar.SigningDeadline(openPlay.Start.Year) ? "DEADLINE DAY" : "SIGNING WEEK";
		}

		if (today < openPlay.Start)
		{
			return "OPENS JANUARY 12";
		}

		return today > openPlay.End ? "OPEN PLAY COMPLETE" : "OPEN PLAY IN PROGRESS";
	}

	private Label MakeFixed(string text, float width, Color color, FontFile? font = null, int size = 13)
	{
		Label label = MakeText(text, font ?? _medium, size, color, HorizontalAlignment.Center);
		label.CustomMinimumSize = new Vector2(width, 0);
		return label;
	}

	private static Label MakeText(string text, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = align,
		};
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static MarginContainer PadBody(Control child)
	{
		var pad = new MarginContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		pad.AddThemeConstantOverride("margin_left", 14);
		pad.AddThemeConstantOverride("margin_top", 12);
		pad.AddThemeConstantOverride("margin_right", 14);
		pad.AddThemeConstantOverride("margin_bottom", 12);
		pad.AddChild(child);
		return pad;
	}

	private static PanelContainer MakeCard(bool expand)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = expand ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
		};
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
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
		});
		return card;
	}

	private static StyleBoxFlat MakePill(Color bg, float padX)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			ContentMarginLeft = padX,
			ContentMarginTop = 6,
			ContentMarginRight = padX,
			ContentMarginBottom = 6,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
		};
	}
}
