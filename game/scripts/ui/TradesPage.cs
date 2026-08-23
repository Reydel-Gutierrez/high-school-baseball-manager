using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// Organization trade desk: move program players and a partner roster into
/// opposing boxes, then submit a transfer. Pending and closed deals open
/// in a modal from the header.
/// </summary>
public partial class TradesPage : Control
{
	private const int MaxPlayersPerSide = 4;
	private const int CurrentSeasonYear = 2026;

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
	private ShaderMaterial _circleMaterial = null!;
	private Texture2D _cascadeLogo = null!;

	private readonly List<TradePlayer> _programPlayers = BuildProgramPlayers();
	private readonly List<PartnerProgram> _partners = BuildPartners();
	private readonly List<TradeDeal> _deals = new();
	private readonly List<TradePlayer> _outgoing = new();
	private readonly List<TradePlayer> _incoming = new();

	private int _partnerIndex;

	private Label _subtitle = null!;
	private Button _pendingButton = null!;
	private Button _closedButton = null!;
	private MenuButton _partnerButton = null!;
	private VBoxContainer _yourRows = null!;
	private VBoxContainer _partnerRows = null!;
	private VBoxContainer _outgoingHost = null!;
	private VBoxContainer _incomingHost = null!;
	private Label _incomingTitle = null!;
	private Button _submitButton = null!;
	private Control _overlay = null!;
	private PanelContainer _overlayDialog = null!;
	private VBoxContainer _overlayBody = null!;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_circleMaterial = new ShaderMaterial
		{
			Shader = GD.Load<Shader>("res://assets/ui/circle_crop.gdshader"),
		};
		_cascadeLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
		_session = GetNode<GameSession>("/root/GameSession");

		SeedPrototypeDeals();
		BuildPage();
		BuildOverlay();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		ApplyActiveTeam();
		RefreshAll();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsVisibleInTree() || @event is not InputEventKey key || !key.Pressed || key.Echo)
		{
			return;
		}

		if (_overlay.Visible && key.Keycode == Key.Escape)
		{
			CloseOverlay();
			GetViewport().SetInputAsHandled();
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _)
	{
		_outgoing.Clear();
		_incoming.Clear();
		ApplyActiveTeam();
		RefreshAll();
	}

	private void ApplyActiveTeam()
	{
		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		_subtitle.Text = varsity
			? $"{_session.Organization.Name.ToUpperInvariant()}  ·  VARSITY"
			: $"{_session.Organization.Name.ToUpperInvariant()}  ·  JV";
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
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 8);
		layout.AddChild(body);

		body.AddChild(BuildRosterCard(
			"YOUR PLAYERS",
			"Click to add to the send box.",
			out _yourRows,
			1f));
		body.AddChild(BuildBoardCard());
		body.AddChild(BuildPartnerCard());
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
		identity.AddChild(MakeText($"ORGANIZATION  ·  {CurrentSeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("TRADE DESK", _bold, 26, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("CASCADE REGIONAL HIGH SCHOOL", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var actions = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		actions.AddThemeConstantOverride("separation", 6);
		_pendingButton = MakeAction("PENDING TRADES  ·  0", false, 1f);
		_pendingButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		_pendingButton.Pressed += () => OpenHistoryOverlay(true);
		actions.AddChild(_pendingButton);
		_closedButton = MakeAction("CLOSED TRADES  ·  0", false, 1f);
		_closedButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
		_closedButton.Pressed += () => OpenHistoryOverlay(false);
		actions.AddChild(_closedButton);
		header.AddChild(actions);
		return header;
	}

	private Control BuildRosterCard(string title, string hint, out VBoxContainer rows, float stretch)
	{
		var card = MakeCard();
		card.SizeFlagsStretchRatio = stretch;
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		card.AddChild(layout);

		layout.AddChild(MakeText(title, _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(MakeText(hint, _medium, 11, TextMuted, HorizontalAlignment.Left));
		layout.AddChild(MakeAccentLine());

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);

		rows = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		rows.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(rows);
		return card;
	}

	private Control BuildPartnerCard()
	{
		var card = MakeCard();
		card.SizeFlagsStretchRatio = 1f;
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		card.AddChild(layout);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		var titles = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		titles.AddThemeConstantOverride("separation", -2);
		titles.AddChild(MakeText("TRADE PARTNER", _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		titles.AddChild(MakeText("Click a player to add to the receive box.", _medium, 11, TextMuted, HorizontalAlignment.Left));
		header.AddChild(titles);
		layout.AddChild(header);

		_partnerButton = new MenuButton
		{
			Text = PartnerLabel(_partners[_partnerIndex]),
			Flat = false,
			SwitchOnHover = true,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		StyleMenuButton(_partnerButton);
		PopupMenu popup = _partnerButton.GetPopup();
		for (int i = 0; i < _partners.Count; i++)
		{
			popup.AddItem(_partners[i].Name.ToUpperInvariant(), i);
		}

		popup.IdPressed += OnPartnerPicked;
		layout.AddChild(_partnerButton);
		layout.AddChild(MakeAccentLine());

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);

		_partnerRows = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_partnerRows.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(_partnerRows);
		return card;
	}

	private Control BuildBoardCard()
	{
		var card = MakeCard();
		card.SizeFlagsStretchRatio = 1.15f;
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		card.AddChild(layout);

		layout.AddChild(MakeText("TRANSFER BOARD", _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(MakeText("Build both sides, then submit for district review.", _medium, 11, TextMuted, HorizontalAlignment.Left));
		layout.AddChild(MakeAccentLine());

		var boxes = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		boxes.AddThemeConstantOverride("separation", 8);
		layout.AddChild(boxes);

		boxes.AddChild(BuildTradeBox("YOU SEND", _session.Organization.ShortName.ToUpperInvariant(), out _outgoingHost, true));
		boxes.AddChild(BuildTradeBox("YOU RECEIVE", _partners[_partnerIndex].ShortName, out _incomingHost, false));

		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 6);
		_submitButton = MakeAction("SUBMIT TRANSFER", true, 1.4f);
		_submitButton.Pressed += SubmitTransfer;
		actions.AddChild(_submitButton);
		Button clear = MakeAction("CLEAR BOARD", false, 1f);
		clear.Pressed += ClearBoard;
		actions.AddChild(clear);
		layout.AddChild(actions);
		return card;
	}

	private Control BuildTradeBox(string title, string subtitle, out VBoxContainer host, bool outgoing)
	{
		var box = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		box.AddThemeStyleboxOverride("panel", MakeInnerCard(8, 8));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 4);
		box.AddChild(layout);

		layout.AddChild(MakeText(title, _semibold, 11, Accent, HorizontalAlignment.Left));
		Label sideTitle = MakeText(subtitle, _medium, 11, TextMuted, HorizontalAlignment.Left);
		layout.AddChild(sideTitle);
		if (!outgoing)
		{
			_incomingTitle = sideTitle;
		}

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);

		host = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		host.AddThemeConstantOverride("separation", 4);
		scroll.AddChild(host);
		return box;
	}

	public void OpenForPlayer(string teamId, string firstName, string lastName)
	{
		int index = FindPartnerIndex(teamId);
		if (index >= 0 && index != _partnerIndex)
		{
			_partnerIndex = index;
			_incoming.Clear();
			_partnerButton.Text = PartnerLabel(_partners[_partnerIndex]);
			_incomingTitle.Text = _partners[_partnerIndex].ShortName;
		}

		if (index >= 0)
		{
			foreach (TradePlayer player in _partners[_partnerIndex].Players)
			{
				if (player.FirstName == firstName && player.LastName == lastName && !ContainsPlayer(_incoming, player.Id))
				{
					if (_incoming.Count < MaxPlayersPerSide && !IsLocked(player.Id))
					{
						_incoming.Add(player);
					}

					break;
				}
			}
		}

		RefreshAll();
	}

	private void OnPartnerPicked(long id)
	{
		int index = (int)id;
		if (index == _partnerIndex || index < 0 || index >= _partners.Count)
		{
			return;
		}

		_partnerIndex = index;
		_incoming.Clear();
		_partnerButton.Text = PartnerLabel(_partners[_partnerIndex]);
		_incomingTitle.Text = _partners[_partnerIndex].ShortName;
		RefreshAll();
	}

	private void RefreshAll()
	{
		RebuildRoster(_yourRows, GetVisibleProgramPlayers(), _outgoing, ToggleOutgoing);
		RebuildRoster(_partnerRows, GetVisiblePartnerPlayers(), _incoming, ToggleIncoming);
		RebuildBox(_outgoingHost, _outgoing, "Add players from your roster.", ToggleOutgoing);
		RebuildBox(_incomingHost, _incoming, "Add players from the partner roster.", ToggleIncoming);
		RefreshSubmit();
		RefreshHistoryButtons();
	}

	private void RefreshHistoryButtons()
	{
		int pending = CountDeals(TradeStatus.Pending);
		int closed = _deals.Count - pending;
		_pendingButton.Text = $"PENDING TRADES  ·  {FormatInt(pending)}";
		_closedButton.Text = $"CLOSED TRADES  ·  {FormatInt(closed)}";
		StyleHistoryButton(_pendingButton, pending > 0);
		StyleHistoryButton(_closedButton, false);
	}

	private void StyleHistoryButton(Button button, bool highlight)
	{
		button.AddThemeColorOverride("font_color", highlight ? Accent : TextPrimary);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, highlight ? Accent : CardBorder));
		button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("focus", MakeOutlinedButton(HoverBg, Accent));
	}

	private void RefreshSubmit()
	{
		bool ready = _outgoing.Count > 0 && _incoming.Count > 0;
		_submitButton.Disabled = !ready;
		_submitButton.AddThemeColorOverride("font_disabled_color", new Color(0.40f, 0.42f, 0.46f));
		if (ready)
		{
			_submitButton.AddThemeColorOverride("font_color", TextOnAccent);
			_submitButton.AddThemeColorOverride("font_hover_color", TextOnAccent);
			_submitButton.AddThemeColorOverride("font_pressed_color", TextOnAccent);
			_submitButton.AddThemeColorOverride("font_focus_color", TextOnAccent);
			_submitButton.AddThemeStyleboxOverride("normal", MakeFilledButton(Accent));
			_submitButton.AddThemeStyleboxOverride("hover", MakeFilledButton(AccentHover));
			_submitButton.AddThemeStyleboxOverride("pressed", MakeFilledButton(AccentHover));
			_submitButton.AddThemeStyleboxOverride("focus", MakeFilledButton(AccentHover));
			_submitButton.AddThemeStyleboxOverride("disabled", MakeFilledButton(new Color(0.18f, 0.20f, 0.24f)));
		}
		else
		{
			_submitButton.AddThemeStyleboxOverride("disabled", MakeFilledButton(new Color(0.18f, 0.20f, 0.24f)));
		}
	}

	private void RebuildRoster(
		VBoxContainer host,
		List<TradePlayer> players,
		List<TradePlayer> selected,
		Action<TradePlayer> onToggle)
	{
		ClearHost(host);
		if (players.Count == 0)
		{
			host.AddChild(MakeText("No players available.", _medium, 12, TextMuted, HorizontalAlignment.Center));
			return;
		}

		for (int i = 0; i < players.Count; i++)
		{
			TradePlayer player = players[i];
			bool locked = IsLocked(player.Id);
			bool inBox = ContainsPlayer(selected, player.Id);
			host.AddChild(BuildRosterRow(player, i, locked, inBox, onToggle));
		}
	}

	private Control BuildRosterRow(
		TradePlayer player,
		int index,
		bool locked,
		bool inBox,
		Action<TradePlayer> onToggle)
	{
		Color rowColor = locked
			? new Color(0.05f, 0.055f, 0.07f, 0.9f)
			: inBox ? RowSelected : (index % 2 == 0 ? RowEven : RowOdd);

		var row = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseDefaultCursorShape = locked ? CursorShape.Arrow : CursorShape.PointingHand,
		};
		row.AddThemeStyleboxOverride("panel", MakeRowStyle(rowColor, inBox && !locked));
		if (!locked)
		{
			row.GuiInput += @event =>
			{
				if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
				{
					TradePlayer picked = player;
					Callable.From(() => onToggle(picked)).CallDeferred();
				}
			};
			row.MouseEntered += () =>
			{
				if (!inBox)
				{
					row.AddThemeStyleboxOverride("panel", MakeRowStyle(HoverBg, false));
				}
			};
			row.MouseExited += () =>
			{
				if (!inBox)
				{
					Color restored = index % 2 == 0 ? RowEven : RowOdd;
					row.AddThemeStyleboxOverride("panel", MakeRowStyle(restored, false));
				}
			};
		}

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		row.AddChild(columns);

		columns.AddChild(MakeCell($"#{FormatInt(player.Jersey)}", 36, _bold, 12, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(BuildPortrait(player, 26));
		columns.AddChild(BuildNameCell(player, locked));
		columns.AddChild(MakeCell(player.Position, 52, _semibold, 11, TextPrimary, HorizontalAlignment.Center));
		columns.AddChild(MakeCell(player.Year, 24, _semibold, 11, TextMuted, HorizontalAlignment.Center));
		columns.AddChild(MakeCell(FormatInt(player.Overall), 28, _bold, 13, OverallColor(player.Overall), HorizontalAlignment.Right));
		return row;
	}

	private Control BuildNameCell(TradePlayer player, bool locked)
	{
		var box = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		box.AddThemeConstantOverride("separation", -2);
		box.AddChild(MakeText(player.LastName.ToUpperInvariant(), _semibold, 13, TextPrimary, HorizontalAlignment.Left));
		string sub = locked ? $"{player.FirstName}  ·  PENDING" : player.FirstName;
		box.AddChild(MakeText(sub, _medium, 10, locked ? Accent : TextMuted, HorizontalAlignment.Left));
		return box;
	}

	private void RebuildBox(VBoxContainer host, List<TradePlayer> players, string emptyHint, Action<TradePlayer> onRemove)
	{
		ClearHost(host);
		if (players.Count == 0)
		{
			var hint = MakeText(emptyHint, _medium, 12, TextMuted, HorizontalAlignment.Center);
			hint.AutowrapMode = TextServer.AutowrapMode.Word;
			host.AddChild(hint);
			return;
		}

		foreach (TradePlayer player in players)
		{
			host.AddChild(BuildChip(player, onRemove));
		}
	}

	private Control BuildChip(TradePlayer player, Action<TradePlayer> onRemove)
	{
		var chip = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		chip.AddThemeStyleboxOverride("panel", MakeChipStyle());
		chip.GuiInput += @event =>
		{
			if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				TradePlayer picked = player;
				Callable.From(() => onRemove(picked)).CallDeferred();
			}
		};

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		chip.AddChild(row);
		row.AddChild(BuildPortrait(player, 28));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		identity.AddThemeConstantOverride("separation", -2);
		identity.AddChild(MakeText(player.LastName.ToUpperInvariant(), _semibold, 13, TextPrimary, HorizontalAlignment.Left));
		identity.AddChild(MakeText(
			$"{player.FirstName}  ·  {player.Position}  ·  {player.Year}",
			_medium,
			10,
			TextMuted,
			HorizontalAlignment.Left));
		row.AddChild(identity);
		row.AddChild(MakeText(FormatInt(player.Overall), _bold, 16, OverallColor(player.Overall), HorizontalAlignment.Right));
		return chip;
	}

	private void FillHistory(VBoxContainer host, List<TradeDeal> deals, bool pending)
	{
		ClearHost(host);
		if (deals.Count == 0)
		{
			host.AddChild(MakeText(
				pending ? "No pending transfers." : "No closed transfers yet.",
				_medium,
				13,
				TextMuted,
				HorizontalAlignment.Center));
			return;
		}

		foreach (TradeDeal deal in deals)
		{
			host.AddChild(BuildDealSummary(deal));
		}
	}

	private Control BuildDealSummary(TradeDeal deal)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", MakeInnerCard(12, 8));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 2);
		card.AddChild(layout);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 10);
		header.AddChild(MakeText(deal.DateLabel.ToUpperInvariant(), _medium, 11, TextMuted, HorizontalAlignment.Left));
		header.AddChild(MakeText("·", _medium, 11, TextMuted, HorizontalAlignment.Left));
		var partner = MakeText(deal.PartnerName.ToUpperInvariant(), _semibold, 13, TextPrimary, HorizontalAlignment.Left);
		partner.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(partner);
		header.AddChild(MakeText(StatusLabel(deal.Status), _semibold, 11, StatusColor(deal.Status), HorizontalAlignment.Right));
		layout.AddChild(header);

		string sendLabel = deal.Status == TradeStatus.Pending ? "Send" : "Sent";
		string receiveLabel = deal.Status == TradeStatus.Pending ? "Receive" : "Received";
		var swap = MakeText(
			$"{sendLabel} {FormatPlayerList(deal.Outgoing)}  ·  {receiveLabel} {FormatPlayerList(deal.Incoming)}",
			_medium,
			12,
			TextMuted,
			HorizontalAlignment.Left);
		swap.AutowrapMode = TextServer.AutowrapMode.Word;
		layout.AddChild(swap);
		return card;
	}

	private void ToggleOutgoing(TradePlayer player)
	{
		ToggleSide(_outgoing, player);
	}

	private void ToggleIncoming(TradePlayer player)
	{
		ToggleSide(_incoming, player);
	}

	private void ToggleSide(List<TradePlayer> side, TradePlayer player)
	{
		if (IsLocked(player.Id))
		{
			return;
		}

		int index = side.FindIndex(existing => existing.Id == player.Id);
		if (index >= 0)
		{
			side.RemoveAt(index);
		}
		else if (side.Count < MaxPlayersPerSide)
		{
			side.Add(player);
		}

		RefreshAll();
	}

	private void ClearBoard()
	{
		_outgoing.Clear();
		_incoming.Clear();
		RefreshAll();
	}

	private void SubmitTransfer()
	{
		if (_outgoing.Count == 0 || _incoming.Count == 0)
		{
			return;
		}

		PartnerProgram partner = _partners[_partnerIndex];
		var deal = new TradeDeal(
			$"deal-{_deals.Count + 1}",
			partner.Id,
			partner.Name,
			[.. _outgoing],
			[.. _incoming],
			TradeStatus.Pending,
			"APR 24");
		_deals.Insert(0, deal);
		_outgoing.Clear();
		_incoming.Clear();
		RefreshAll();
		OpenSubmitOverlay(deal);
	}

	private void BuildOverlay()
	{
		var layer = new CanvasLayer { Layer = 40 };
		AddChild(layer);

		_overlay = new Control
		{
			Name = "TransferOverlay",
			Visible = false,
			MouseFilter = MouseFilterEnum.Stop,
		};
		_overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		layer.AddChild(_overlay);

		var dimmer = new ColorRect
		{
			Color = new Color(0.02f, 0.03f, 0.04f, 0.72f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dimmer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		dimmer.GuiInput += @event =>
		{
			if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				CloseOverlay();
			}
		};
		_overlay.AddChild(dimmer);

		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_overlay.AddChild(center);

		_overlayDialog = new PanelContainer
		{
			CustomMinimumSize = new Vector2(420, 0),
			MouseFilter = MouseFilterEnum.Stop,
		};
		_overlayDialog.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.078431f, 0.094118f, 0.98f),
			BorderColor = new Color(0.27451f, 0.294118f, 0.333333f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 18,
			ContentMarginTop = 16,
			ContentMarginRight = 18,
			ContentMarginBottom = 16,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerDetail = 4,
		});
		center.AddChild(_overlayDialog);

		_overlayBody = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_overlayBody.AddThemeConstantOverride("separation", 8);
		_overlayDialog.AddChild(_overlayBody);
	}

	private void OpenHistoryOverlay(bool pending)
	{
		List<TradeDeal> deals = pending ? GetDeals(TradeStatus.Pending) : GetClosedDeals();
		_overlayDialog.CustomMinimumSize = new Vector2(560, 0);
		ClearHost(_overlayBody);

		_overlayBody.AddChild(MakeText("TRADE DESK", _semibold, 11, Accent, HorizontalAlignment.Left));
		_overlayBody.AddChild(MakeText(
			pending ? "PENDING TRADES" : "CLOSED TRADES",
			_bold,
			22,
			TextPrimary,
			HorizontalAlignment.Left));
		_overlayBody.AddChild(MakeText(
			pending
				? $"{FormatInt(deals.Count)} awaiting district review."
				: $"{FormatInt(deals.Count)} completed or declined.",
			_medium,
			12,
			TextMuted,
			HorizontalAlignment.Left));
		_overlayBody.AddChild(MakeAccentLine());

		var scroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, 240),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		_overlayBody.AddChild(scroll);

		var host = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		host.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(host);
		FillHistory(host, deals, pending);

		Button close = MakeAction("CLOSE", false, 1f);
		close.Pressed += CloseOverlay;
		_overlayBody.AddChild(close);
		_overlay.Visible = true;
	}

	private void OpenSubmitOverlay(TradeDeal deal)
	{
		_overlayDialog.CustomMinimumSize = new Vector2(420, 0);
		ClearHost(_overlayBody);
		_overlayBody.AddChild(MakeText("TRANSFER SUBMITTED", _semibold, 11, Accent, HorizontalAlignment.Left));
		_overlayBody.AddChild(MakeText("PENDING DISTRICT REVIEW", _bold, 22, TextPrimary, HorizontalAlignment.Left));
		_overlayBody.AddChild(MakeText(
			$"Proposal with {deal.PartnerName} is waiting on approval.",
			_medium,
			12,
			TextMuted,
			HorizontalAlignment.Left));
		_overlayBody.AddChild(MakeAccentLine());
		_overlayBody.AddChild(MakeText($"YOU SEND  ·  {FormatPlayerList(deal.Outgoing)}", _semibold, 13, TextPrimary, HorizontalAlignment.Left));
		_overlayBody.AddChild(MakeText($"YOU RECEIVE  ·  {FormatPlayerList(deal.Incoming)}", _semibold, 13, TextPrimary, HorizontalAlignment.Left));

		Button viewPending = MakeAction("VIEW PENDING TRADES", true, 1f);
		viewPending.Pressed += () => OpenHistoryOverlay(true);
		_overlayBody.AddChild(viewPending);

		Button close = MakeAction("CLOSE", false, 1f);
		close.Pressed += CloseOverlay;
		_overlayBody.AddChild(close);
		_overlay.Visible = true;
	}

	private void CloseOverlay()
	{
		_overlay.Visible = false;
	}

	private List<TradePlayer> GetVisibleProgramPlayers()
	{
		var visible = new List<TradePlayer>();
		foreach (TradePlayer player in _programPlayers)
		{
			if (_session.ActiveTeam.Level.MatchesSquad(player.Squad))
			{
				visible.Add(player);
			}
		}

		return visible;
	}

	private List<TradePlayer> GetVisiblePartnerPlayers()
	{
		var visible = new List<TradePlayer>();
		foreach (TradePlayer player in _partners[_partnerIndex].Players)
		{
			if (_session.ActiveTeam.Level.MatchesSquad(player.Squad))
			{
				visible.Add(player);
			}
		}

		return visible;
	}

	private bool IsLocked(string playerId)
	{
		foreach (TradeDeal deal in _deals)
		{
			if (deal.Status != TradeStatus.Pending)
			{
				continue;
			}

			if (ContainsPlayer(deal.Outgoing, playerId) || ContainsPlayer(deal.Incoming, playerId))
			{
				return true;
			}
		}

		return false;
	}

	private int CountDeals(TradeStatus status)
	{
		int count = 0;
		foreach (TradeDeal deal in _deals)
		{
			if (deal.Status == status)
			{
				count++;
			}
		}

		return count;
	}

	private List<TradeDeal> GetDeals(TradeStatus status)
	{
		var matches = new List<TradeDeal>();
		foreach (TradeDeal deal in _deals)
		{
			if (deal.Status == status)
			{
				matches.Add(deal);
			}
		}

		return matches;
	}

	private List<TradeDeal> GetClosedDeals()
	{
		var matches = new List<TradeDeal>();
		foreach (TradeDeal deal in _deals)
		{
			if (deal.Status != TradeStatus.Pending)
			{
				matches.Add(deal);
			}
		}

		return matches;
	}

	private Control BuildPortrait(TradePlayer player, float size)
	{
		var wrap = new Control
		{
			CustomMinimumSize = new Vector2(size, size),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
		};

		if (!string.IsNullOrEmpty(player.PortraitPath))
		{
			var portrait = new TextureRect
			{
				Texture = GD.Load<Texture2D>(player.PortraitPath),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				Material = _circleMaterial,
				CustomMinimumSize = new Vector2(size, size),
			};
			portrait.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			wrap.AddChild(portrait);
			return wrap;
		}

		int radius = Mathf.RoundToInt(size / 2f);
		var fallback = new PanelContainer();
		fallback.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		fallback.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = HoverBg,
			BorderColor = Accent,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerDetail = 8,
		});
		var initial = MakeText(
			player.LastName[..1].ToUpperInvariant(),
			_bold,
			Mathf.RoundToInt(size * 0.38f),
			Accent,
			HorizontalAlignment.Center);
		initial.VerticalAlignment = VerticalAlignment.Center;
		fallback.AddChild(initial);
		wrap.AddChild(fallback);
		return wrap;
	}

	private Button MakeAction(string text, bool primary, float stretch)
	{
		var button = new Button
		{
			Text = text,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = stretch,
			CustomMinimumSize = new Vector2(0, 32),
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		if (primary)
		{
			button.AddThemeColorOverride("font_color", TextOnAccent);
			button.AddThemeColorOverride("font_hover_color", TextOnAccent);
			button.AddThemeColorOverride("font_pressed_color", TextOnAccent);
			button.AddThemeColorOverride("font_focus_color", TextOnAccent);
			button.AddThemeStyleboxOverride("normal", MakeFilledButton(Accent));
			button.AddThemeStyleboxOverride("hover", MakeFilledButton(AccentHover));
			button.AddThemeStyleboxOverride("pressed", MakeFilledButton(AccentHover));
			button.AddThemeStyleboxOverride("focus", MakeFilledButton(AccentHover));
		}
		else
		{
			button.AddThemeColorOverride("font_color", TextPrimary);
			button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, CardBorder));
			button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
			button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
			button.AddThemeStyleboxOverride("focus", MakeOutlinedButton(HoverBg, Accent));
		}

		return button;
	}

	private void StyleMenuButton(MenuButton button)
	{
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, CardBorder));
		button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
		button.AddThemeStyleboxOverride("focus", MakeOutlinedButton(HoverBg, Accent));
	}

	private static void ClearHost(Node host)
	{
		while (host.GetChildCount() > 0)
		{
			Node child = host.GetChild(0);
			host.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static bool ContainsPlayer(IReadOnlyList<TradePlayer> players, string id)
	{
		foreach (TradePlayer player in players)
		{
			if (player.Id == id)
			{
				return true;
			}
		}

		return false;
	}

	private static string FormatPlayerList(IReadOnlyList<TradePlayer> players)
	{
		if (players.Count == 0)
		{
			return "—";
		}

		var names = new List<string>(players.Count);
		foreach (TradePlayer player in players)
		{
			names.Add(player.LastName.ToUpperInvariant());
		}

		return string.Join(", ", names);
	}

	private static string PartnerLabel(PartnerProgram partner) => $"{partner.Name.ToUpperInvariant()} ▼";

	private int FindPartnerIndex(string teamId)
	{
		string partnerId = teamId switch
		{
			"great-lakes" => "lakes",
			"harbor-ridge" => "harbor",
			"delaware-valley" => "delaware-valley",
			_ => teamId,
		};

		for (int i = 0; i < _partners.Count; i++)
		{
			if (_partners[i].Id == partnerId)
			{
				return i;
			}
		}

		HubTeam team = DistrictHubData.GetTeam(teamId);
		for (int i = 0; i < _partners.Count; i++)
		{
			if (_partners[i].Name.Contains(team.Name, StringComparison.OrdinalIgnoreCase)
				|| team.Name.Contains(_partners[i].ShortName, StringComparison.OrdinalIgnoreCase))
			{
				return i;
			}
		}

		return -1;
	}

	private static string FormatInt(int value) => value.ToString(CultureInfo.InvariantCulture);

	private static string StatusLabel(TradeStatus status) => status switch
	{
		TradeStatus.Pending => "PENDING",
		TradeStatus.Completed => "COMPLETED",
		_ => "DECLINED",
	};

	private static Color StatusColor(TradeStatus status) => status switch
	{
		TradeStatus.Pending => Accent,
		TradeStatus.Completed => Green,
		_ => Urgent,
	};

	private static Color OverallColor(int value)
	{
		if (value >= 85)
		{
			return Green;
		}

		if (value >= 78)
		{
			return Accent;
		}

		if (value >= 70)
		{
			return TextPrimary;
		}

		return TextMuted;
	}

	private static Label MakeCell(string text, float width, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		Label label = MakeText(text, font, size, color, align);
		label.CustomMinimumSize = new Vector2(width, 0);
		label.VerticalAlignment = VerticalAlignment.Center;
		label.ClipText = true;
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

	private PanelContainer MakeCard()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle());
		return card;
	}

	private static ColorRect MakeAccentLine()
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 2),
			Color = Accent,
		};
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
			ContentMarginLeft = 10,
			ContentMarginTop = 8,
			ContentMarginRight = 10,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		};
	}

	private static StyleBoxFlat MakeInnerCard(float padX, float padY)
	{
		return new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = Hairline,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = padX,
			ContentMarginTop = padY,
			ContentMarginRight = padX,
			ContentMarginBottom = padY,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		};
	}

	private static StyleBoxFlat MakeRowStyle(Color bg, bool selected)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = selected ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = selected ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 8,
			ContentMarginTop = 5,
			ContentMarginRight = 8,
			ContentMarginBottom = 5,
		};
	}

	private static StyleBoxFlat MakeChipStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.08f, 0.1f, 0.96f),
			BorderColor = Accent,
			BorderWidthLeft = 2,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 8,
			ContentMarginTop = 6,
			ContentMarginRight = 8,
			ContentMarginBottom = 6,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		};
	}

	private static StyleBoxFlat MakeFilledButton(Color bg)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};
	}

	private static StyleBoxFlat MakeOutlinedButton(Color bg, Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 8,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};
	}

	private void SeedPrototypeDeals()
	{
		TradePlayer peck = FindProgram("cascade-peck");
		TradePlayer voss = FindPartnerPlayer("lakes", "lakes-voss");
		TradePlayer keene = FindProgram("cascade-keene");
		TradePlayer moss = FindPartnerPlayer("lakes", "lakes-moss");
		TradePlayer cole = FindProgram("cascade-cole");
		TradePlayer nash = FindPartnerPlayer("delaware-valley", "delaware-nash");

		_deals.Add(new TradeDeal(
			"deal-pending-1",
			"lakes",
			"Great Lakes High School",
			[peck],
			[voss],
			TradeStatus.Pending,
			"APR 22"));
		_deals.Add(new TradeDeal(
			"deal-closed-1",
			"lakes",
			"Great Lakes High School",
			[keene],
			[moss],
			TradeStatus.Completed,
			"APR 10"));
		_deals.Add(new TradeDeal(
			"deal-closed-2",
			"delaware-valley",
			"Delaware Valley High School",
			[cole],
			[nash],
			TradeStatus.Declined,
			"APR 4"));
	}

	private TradePlayer FindProgram(string id)
	{
		foreach (TradePlayer player in _programPlayers)
		{
			if (player.Id == id)
			{
				return player;
			}
		}

		throw new InvalidOperationException($"Missing program player '{id}'.");
	}

	private TradePlayer FindPartnerPlayer(string teamId, string playerId)
	{
		foreach (PartnerProgram partner in _partners)
		{
			if (partner.Id != teamId)
			{
				continue;
			}

			foreach (TradePlayer player in partner.Players)
			{
				if (player.Id == playerId)
				{
					return player;
				}
			}
		}

		throw new InvalidOperationException($"Missing partner player '{playerId}'.");
	}

	private static List<TradePlayer> BuildProgramPlayers()
	{
		const string marini = "res://assets/teams/01_Cascade_Regional_High_School/Players/Cascade_player_01_Marini.png";
		return
		[
			P("cascade-marini", "Luca", "Marini", 11, "RHP", "SR", 88, "Varsity", marini),
			P("cascade-brooks", "Jalen", "Brooks", 7, "CF", "SR", 84, "Varsity"),
			P("cascade-cruz", "Mateo", "Cruz", 21, "C", "JR", 81, "Varsity"),
			P("cascade-hale", "Drew", "Hale", 4, "SS", "JR", 80, "Varsity"),
			P("cascade-cole", "Isaiah", "Cole", 9, "1B", "SR", 79, "Varsity"),
			P("cascade-lawson", "Trey", "Lawson", 32, "LHP", "SR", 76, "Varsity"),
			P("cascade-barrett", "Noah", "Barrett", 15, "3B", "JR", 77, "Varsity"),
			P("cascade-whitaker", "Cam", "Whitaker", 2, "2B", "SO", 74, "Varsity"),
			P("cascade-vargas", "Eli", "Vargas", 24, "LF", "SR", 73, "Varsity"),
			P("cascade-peck", "Ryan", "Peck", 18, "RF", "JR", 72, "Varsity"),
			P("cascade-ortiz", "Ben", "Ortiz", 8, "RHP", "JR", 71, "Varsity"),
			P("cascade-walsh", "Finn", "Walsh", 16, "C", "SO", 70, "Varsity"),
			P("cascade-grant", "Miles", "Grant", 5, "RHP", "SO", 68, "JV"),
			P("cascade-drake", "Owen", "Drake", 14, "C", "SO", 67, "JV"),
			P("cascade-keene", "Sam", "Keene", 6, "CF", "FR", 64, "JV"),
			P("cascade-nolan", "Chris", "Nolan", 27, "2B", "FR", 62, "JV"),
			P("cascade-hayes", "Leo", "Hayes", 17, "RHP", "FR", 64, "JV"),
			P("cascade-briggs", "Evan", "Briggs", 1, "SS", "SO", 66, "JV"),
			P("cascade-ruiz", "Alex", "Ruiz", 34, "CF", "SO", 67, "JV"),
			P("cascade-dean", "Parker", "Dean", 38, "RF", "SO", 64, "JV"),
		];
	}

	private static List<PartnerProgram> BuildPartners()
	{
		const string tony = "res://assets/teams/02_Great_Lakes_High_School/Players/Great_lakes_player_tony.png";
		return
		[
			new("lakes", "Great Lakes High School", "GREAT LAKES",
			[
				P("lakes-tony", "Tony", "Romano", 19, "RHP", "SR", 82, "Varsity", tony),
				P("lakes-diaz", "Mateo", "Diaz", 8, "SS", "JR", 79, "Varsity"),
				P("lakes-rowe", "Calvin", "Rowe", 25, "C", "SR", 77, "Varsity"),
				P("lakes-kim", "Jonah", "Kim", 3, "CF", "JR", 80, "Varsity"),
				P("lakes-slater", "Reid", "Slater", 14, "1B", "SR", 76, "Varsity"),
				P("lakes-boone", "Marcus", "Boone", 22, "3B", "JR", 74, "Varsity"),
				P("lakes-voss", "Eli", "Voss", 6, "2B", "SO", 73, "Varsity"),
				P("lakes-trent", "Owen", "Trent", 31, "RF", "JR", 71, "Varsity"),
				P("lakes-hale", "Gavin", "Hale", 12, "LHP", "SO", 70, "JV"),
				P("lakes-moss", "Cole", "Moss", 4, "C", "FR", 64, "JV"),
				P("lakes-quinn", "Seth", "Quinn", 9, "SS", "SO", 66, "JV"),
				P("lakes-bellamy", "Nate", "Bellamy", 2, "CF", "FR", 63, "JV"),
			]),
			new("delaware-valley", "Delaware Valley High School", "DELAWARE VALLEY",
			[
				P("delaware-nash", "Grant", "Nash", 10, "RHP", "SR", 81, "Varsity"),
				P("delaware-porter", "Luis", "Porter", 5, "C", "JR", 75, "Varsity"),
				P("delaware-glenn", "Aiden", "Glenn", 1, "SS", "SR", 78, "Varsity"),
				P("delaware-west", "Theo", "West", 23, "CF", "JR", 76, "Varsity"),
				P("delaware-cobb", "Bryce", "Cobb", 17, "1B", "SO", 73, "Varsity"),
				P("delaware-pike", "Jonah", "Pike", 8, "LHP", "SO", 65, "JV"),
				P("delaware-drummond", "Cal", "Drummond", 15, "2B", "FR", 61, "JV"),
			]),
			new("harbor", "Harbor Ridge High School", "HARBOR RIDGE",
			[
				P("harbor-steele", "Max", "Steele", 27, "RHP", "SR", 80, "Varsity"),
				P("harbor-marin", "Diego", "Marin", 11, "C", "JR", 74, "Varsity"),
				P("harbor-frost", "Ian", "Frost", 2, "SS", "SO", 77, "Varsity"),
				P("harbor-dale", "Wes", "Dale", 9, "LF", "SR", 72, "Varsity"),
				P("harbor-cho", "Henry", "Cho", 44, "1B", "JR", 70, "Varsity"),
				P("harbor-reed", "Tyler", "Reed", 6, "RHP", "FR", 63, "JV"),
				P("harbor-parish", "Ben", "Parish", 18, "3B", "SO", 60, "JV"),
			]),
		];
	}

	private static TradePlayer P(
		string id,
		string first,
		string last,
		int jersey,
		string position,
		string year,
		int overall,
		string squad,
		string? portrait = null) =>
		new(id, first, last, jersey, position, year, overall, squad, portrait);

	private enum TradeStatus
	{
		Pending,
		Completed,
		Declined,
	}

	private sealed record PartnerProgram(
		string Id,
		string Name,
		string ShortName,
		IReadOnlyList<TradePlayer> Players);

	private sealed record TradePlayer(
		string Id,
		string FirstName,
		string LastName,
		int Jersey,
		string Position,
		string Year,
		int Overall,
		string Squad,
		string? PortraitPath);

	private sealed record TradeDeal(
		string Id,
		string PartnerTeamId,
		string PartnerName,
		IReadOnlyList<TradePlayer> Outgoing,
		IReadOnlyList<TradePlayer> Incoming,
		TradeStatus Status,
		string DateLabel);
}
