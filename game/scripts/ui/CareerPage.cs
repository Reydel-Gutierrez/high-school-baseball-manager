using System;
using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// The coach's personal desk: identity, inbox, trophy cabinet, and a
/// job board for seats at other programs. Distinct from the team pages.
/// </summary>
public partial class CareerPage : Control
{
	private enum DeskMode
	{
		Desk,
		Cabinet,
		Openings,
	}

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
	private static readonly Color Gold = new(0.92f, 0.74f, 0.28f, 1f);
	private static readonly Color GoldDeep = new(0.72f, 0.52f, 0.14f, 1f);
	private static readonly Color Office = new(0.078f, 0.055f, 0.039f, 0.94f);
	private static readonly Color Wood = new(0.22f, 0.14f, 0.08f, 1f);
	private static readonly Color EmptyMetal = new(0.32f, 0.34f, 0.38f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _iconMail = null!;
	private Texture2D _iconTrophy = null!;

	private readonly List<CareerMail> _mail = new(CareerDesk.Inbox);
	private readonly HashSet<string> _applied = new();
	private readonly HashSet<string> _acceptedInterviews = new();

	private readonly Dictionary<DeskMode, Button> _modeButtons = new();
	private Label _boardValue = null!;
	private Label _securityValue = null!;
	private Control _deskView = null!;
	private Control _cabinetView = null!;
	private Control _openingsView = null!;
	private VBoxContainer _inboxHost = null!;
	private VBoxContainer _letterHost = null!;
	private GridContainer _openingsHost = null!;

	private DeskMode _mode = DeskMode.Desk;
	private int _selectedMail;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_iconMail = GD.Load<Texture2D>("res://assets/ui/icon_mail.svg");
		_iconTrophy = GD.Load<Texture2D>("res://assets/ui/icon_trophy.svg");
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		ApplyStanding();
		ShowMode(DeskMode.Desk);
		SelectMail(FirstUnreadIndex());
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => ApplyStanding();

	private void ApplyStanding()
	{
		_boardValue.Text = _session.BoardSatisfaction.ToLabel().ToUpperInvariant();
		_boardValue.AddThemeColorOverride("font_color", SatisfactionColor(_session.BoardSatisfaction));
		_securityValue.Text = _session.JobSecurity.ToLabel().ToUpperInvariant();
		_securityValue.AddThemeColorOverride("font_color", SecurityColor(_session.JobSecurity));
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

		layout.AddChild(BuildDossier());
		layout.AddChild(BuildModeBar());

		var host = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		layout.AddChild(host);

		_deskView = BuildDesk();
		_cabinetView = BuildCabinet();
		_openingsView = BuildOpenings();
		host.AddChild(_deskView);
		host.AddChild(_cabinetView);
		host.AddChild(_openingsView);
	}

	private Control BuildDossier()
	{
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = Office,
			BorderColor = Gold,
			BorderWidthLeft = 4,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 16,
			ContentMarginTop = 12,
			ContentMarginRight = 16,
			ContentMarginBottom = 12,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		});

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 16);
		card.AddChild(row);
		row.AddChild(BuildMonogram());

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -1);
		identity.AddChild(MakeText($"MY CAREER  ·  {CareerDesk.SeasonYear}", _medium, 10, Gold, HorizontalAlignment.Left));
		identity.AddChild(MakeText(CareerDesk.CoachName.ToUpperInvariant(), _bold, 28, TextPrimary, HorizontalAlignment.Left));
		identity.AddChild(MakeText(
			$"HEAD COACH  ·  {_session.Organization.Name.ToUpperInvariant()}",
			_semibold,
			13,
			new Color(0.82f, 0.78f, 0.70f),
			HorizontalAlignment.Left));
		identity.AddChild(MakeText(
			$"YEAR {_session.ContractYear} OF {_session.ContractLength}  ·  PROMOTED FROM THE JV BENCH",
			_medium,
			11,
			TextMuted,
			HorizontalAlignment.Left));
		row.AddChild(identity);

		var metrics = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		metrics.AddThemeConstantOverride("separation", 18);
		AddMetric(metrics, CareerDesk.ThisYearRecord, "THIS YEAR", TextPrimary);
		AddMetric(metrics, CareerDesk.CareerRecord, "CAREER", TextPrimary);
		AddMetric(metrics, CareerDesk.Reputation.ToUpperInvariant(), "REPUTATION", Gold);
		_boardValue = AddMetric(metrics, "AVERAGE", "BOARD", Accent);
		_securityValue = AddMetric(metrics, "AVERAGE", "SECURITY", Accent);
		row.AddChild(metrics);

		return card;
	}

	private Control BuildMonogram()
	{
		var avatar = new PanelContainer
		{
			CustomMinimumSize = new Vector2(72, 72),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		avatar.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.08f, 0.04f, 1f),
			BorderColor = Gold,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 36,
			CornerRadiusTopRight = 36,
			CornerRadiusBottomRight = 36,
			CornerRadiusBottomLeft = 36,
			CornerDetail = 8,
		});
		var initials = MakeText(CareerDesk.CoachInitials, _bold, 26, Gold, HorizontalAlignment.Center);
		initials.VerticalAlignment = VerticalAlignment.Center;
		initials.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		initials.SizeFlagsVertical = SizeFlags.ExpandFill;
		avatar.AddChild(initials);
		return avatar;
	}

	private Label AddMetric(HBoxContainer row, string value, string label, Color valueColor)
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", -4);
		col.Alignment = BoxContainer.AlignmentMode.Center;
		var amount = MakeText(value, _bold, 22, valueColor, HorizontalAlignment.Right);
		col.AddChild(amount);
		col.AddChild(MakeText(label, _semibold, 10, TextMuted, HorizontalAlignment.Right));
		row.AddChild(col);
		return amount;
	}

	private Control BuildModeBar()
	{
		var frame = new PanelContainer();
		frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047f, 0.04f, 0.035f, 0.96f),
			BorderColor = new Color(Gold.R, Gold.G, Gold.B, 0.35f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 3,
			ContentMarginTop = 3,
			ContentMarginRight = 3,
			ContentMarginBottom = 3,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerDetail = 4,
		});

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 3);
		frame.AddChild(row);

		row.AddChild(MakeModeButton(DeskMode.Desk, "DESK", _iconMail));
		row.AddChild(MakeModeButton(DeskMode.Cabinet, "CABINET", _iconTrophy));
		row.AddChild(MakeModeButton(DeskMode.Openings, "OPENINGS", null));
		return frame;
	}

	private Button MakeModeButton(DeskMode mode, string label, Texture2D? icon)
	{
		var button = new Button
		{
			Text = label,
			Flat = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 32),
			MouseDefaultCursorShape = CursorShape.PointingHand,
			Icon = icon,
			ExpandIcon = true,
		};
		if (icon != null)
		{
			button.AddThemeConstantOverride("icon_max_width", 16);
		}

		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 13);
		button.Pressed += () => ShowMode(mode);
		_modeButtons[mode] = button;
		return button;
	}

	private void ShowMode(DeskMode mode)
	{
		_mode = mode;
		_deskView.Visible = mode == DeskMode.Desk;
		_cabinetView.Visible = mode == DeskMode.Cabinet;
		_openingsView.Visible = mode == DeskMode.Openings;
		RefreshModeButtons();

		if (mode == DeskMode.Desk)
		{
			RebuildInbox();
			BindLetter();
		}
		else if (mode == DeskMode.Openings)
		{
			RebuildOpenings();
		}
	}

	private void RefreshModeButtons()
	{
		int unread = UnreadCount();
		foreach (KeyValuePair<DeskMode, Button> pair in _modeButtons)
		{
			bool active = pair.Key == _mode;
			Button button = pair.Value;
			button.Text = pair.Key == DeskMode.Desk && unread > 0 && !active
				? $"DESK  ·  {unread}"
				: pair.Key switch
				{
					DeskMode.Desk => "DESK",
					DeskMode.Cabinet => "CABINET",
					_ => "OPENINGS",
				};

			button.AddThemeColorOverride("font_color", active ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("font_hover_color", active ? TextOnAccent : Colors.White);
			button.AddThemeColorOverride("font_pressed_color", active ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("font_focus_color", active ? TextOnAccent : Colors.White);
			button.AddThemeColorOverride("icon_normal_color", active ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("icon_hover_color", active ? TextOnAccent : Colors.White);
			button.AddThemeStyleboxOverride("normal", MakePill(active ? Gold : Colors.Transparent, 12, 6));
			button.AddThemeStyleboxOverride("hover", MakePill(active ? AccentHover : HoverBg, 12, 6));
			button.AddThemeStyleboxOverride("pressed", MakePill(active ? AccentHover : HoverBg, 12, 6));
			button.AddThemeStyleboxOverride("focus", MakePill(active ? AccentHover : HoverBg, 12, 6));
		}
	}

	private Control BuildDesk()
	{
		var body = new HBoxContainer();
		body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		body.SizeFlagsVertical = SizeFlags.ExpandFill;
		body.AddThemeConstantOverride("separation", 8);

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

		var heading = new PanelContainer();
		heading.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047f, 0.04f, 0.035f, 0.96f),
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		});
		var headingRow = new HBoxContainer();
		headingRow.AddThemeConstantOverride("separation", 8);
		heading.AddChild(headingRow);
		headingRow.AddChild(new TextureRect
		{
			Texture = _iconMail,
			CustomMinimumSize = new Vector2(16, 16),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Modulate = Gold,
		});
		headingRow.AddChild(MakeText("INBOX", _bold, 14, TextPrimary, HorizontalAlignment.Left));
		listLayout.AddChild(heading);
		listLayout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Gold });

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		listLayout.AddChild(scroll);
		_inboxHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_inboxHost.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(_inboxHost);
		body.AddChild(listCard);

		var letterCard = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.25f,
			ClipContents = true,
		};
		letterCard.AddThemeStyleboxOverride("panel", MakeCardStyle(0));
		_letterHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_letterHost.AddThemeConstantOverride("separation", 0);
		letterCard.AddChild(_letterHost);
		body.AddChild(letterCard);

		return body;
	}

	private void RebuildInbox()
	{
		foreach (Node child in _inboxHost.GetChildren())
		{
			_inboxHost.RemoveChild(child);
			child.QueueFree();
		}

		for (int i = 0; i < _mail.Count; i++)
		{
			_inboxHost.AddChild(BuildInboxRow(_mail[i], i, i == _selectedMail));
		}
	}

	private Control BuildInboxRow(CareerMail mail, int index, bool selected)
	{
		var row = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		row.AddThemeStyleboxOverride("panel", MakeRowStyle(selected, index % 2 == 0, mail.Unread && !selected));
		row.GuiInput += (InputEvent evt) =>
		{
			if (evt is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				SelectMail(index);
			}
		};

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 1);
		row.AddChild(layout);

		var top = new HBoxContainer();
		top.AddThemeConstantOverride("separation", 8);
		layout.AddChild(top);

		if (mail.Unread)
		{
			top.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(8, 8),
				Color = Gold,
				SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
			});
		}
		else
		{
			top.AddChild(new Control { CustomMinimumSize = new Vector2(8, 8) });
		}

		var from = MakeText(mail.From.ToUpperInvariant(), _semibold, 13, selected ? Gold : TextPrimary, HorizontalAlignment.Left);
		from.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		from.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		top.AddChild(from);
		top.AddChild(MakeText(mail.DateLabel, _medium, 11, TextMuted, HorizontalAlignment.Right));

		var subject = MakeText(mail.Subject, _medium, 12, TextPrimary, HorizontalAlignment.Left);
		subject.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		layout.AddChild(subject);

		var preview = MakeText(mail.Preview, _medium, 11, TextMuted, HorizontalAlignment.Left);
		preview.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		layout.AddChild(preview);
		return row;
	}

	private void SelectMail(int index)
	{
		if (index < 0 || index >= _mail.Count)
		{
			return;
		}

		_selectedMail = index;
		CareerMail mail = _mail[index];
		if (mail.Unread)
		{
			_mail[index] = mail with { Unread = false };
		}

		RebuildInbox();
		BindLetter();
		RefreshModeButtons();
	}

	private void BindLetter()
	{
		foreach (Node child in _letterHost.GetChildren())
		{
			_letterHost.RemoveChild(child);
			child.QueueFree();
		}

		if (_mail.Count == 0)
		{
			return;
		}

		CareerMail mail = _mail[_selectedMail];
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047f, 0.04f, 0.035f, 0.96f),
			ContentMarginLeft = 16,
			ContentMarginTop = 12,
			ContentMarginRight = 16,
			ContentMarginBottom = 12,
		});
		var headerLayout = new VBoxContainer();
		headerLayout.AddThemeConstantOverride("separation", 2);
		header.AddChild(headerLayout);
		headerLayout.AddChild(MakeText(KindLabel(mail.Kind), _semibold, 10, Gold, HorizontalAlignment.Left));
		headerLayout.AddChild(MakeText(mail.Subject.ToUpperInvariant(), _bold, 20, TextPrimary, HorizontalAlignment.Left));
		headerLayout.AddChild(MakeText($"{mail.From.ToUpperInvariant()}  ·  {mail.Role.ToUpperInvariant()}", _medium, 12, new Color(0.82f, 0.78f, 0.70f), HorizontalAlignment.Left));
		_letterHost.AddChild(header);
		_letterHost.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Gold });

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		_letterHost.AddChild(scroll);

		var bodyPad = new MarginContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		bodyPad.AddThemeConstantOverride("margin_left", 16);
		bodyPad.AddThemeConstantOverride("margin_top", 14);
		bodyPad.AddThemeConstantOverride("margin_right", 16);
		bodyPad.AddThemeConstantOverride("margin_bottom", 12);
		scroll.AddChild(bodyPad);

		var body = MakeText(mail.Body, _medium, 14, new Color(0.86f, 0.84f, 0.80f), HorizontalAlignment.Left);
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		bodyPad.AddChild(body);

		Control? actions = BuildLetterActions(mail);
		if (actions != null)
		{
			_letterHost.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = Hairline });
			_letterHost.AddChild(actions);
		}
	}

	private Control? BuildLetterActions(CareerMail mail)
	{
		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.End,
		};
		row.AddThemeConstantOverride("separation", 8);

		var pad = new MarginContainer();
		pad.AddThemeConstantOverride("margin_left", 12);
		pad.AddThemeConstantOverride("margin_top", 10);
		pad.AddThemeConstantOverride("margin_right", 12);
		pad.AddThemeConstantOverride("margin_bottom", 10);
		pad.AddChild(row);

		switch (mail.Kind)
		{
			case CareerMailKind.Contract:
				row.AddChild(MakeAction("VIEW CONTRACT GOALS", true, () =>
				{
					if (GetTree().CurrentScene is AppNavigator navigator)
					{
						navigator.ShowGoals();
					}
				}));
				return pad;
			case CareerMailKind.JobOffer when mail.JobId != null:
				row.AddChild(MakeAction("VIEW OPENINGS", false, () => ShowMode(DeskMode.Openings)));
				row.AddChild(MakeAction(
					_applied.Contains(mail.JobId) ? "APPLICATION SENT" : "APPLY FOR THE SEAT",
					true,
					() => ApplyToJob(mail.JobId),
					_applied.Contains(mail.JobId)));
				return pad;
			case CareerMailKind.Award:
				row.AddChild(MakeAction("OPEN THE CABINET", true, () => ShowMode(DeskMode.Cabinet)));
				return pad;
			case CareerMailKind.Media:
				bool accepted = _acceptedInterviews.Contains(mail.Id);
				row.AddChild(MakeAction(
					accepted ? "INTERVIEW ACCEPTED" : "ACCEPT THE SIT-DOWN",
					true,
					() => AcceptInterview(mail.Id),
					accepted));
				return pad;
			default:
				return null;
		}
	}

	private Control BuildCabinet()
	{
		var card = new PanelContainer();
		card.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(14));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 10);
		card.AddChild(layout);

		var heading = new HBoxContainer();
		heading.AddThemeConstantOverride("separation", 8);
		heading.AddChild(new TextureRect
		{
			Texture = _iconTrophy,
			CustomMinimumSize = new Vector2(18, 18),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Modulate = Gold,
		});
		var titles = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		titles.AddThemeConstantOverride("separation", -2);
		titles.AddChild(MakeText("TROPHY CABINET", _bold, 18, TextPrimary, HorizontalAlignment.Left));
		titles.AddChild(MakeText("Cups already on the shelf, races still open, and the ones still empty.", _medium, 12, TextMuted, HorizontalAlignment.Left));
		heading.AddChild(titles);
		layout.AddChild(heading);
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Gold });

		var shelf = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		shelf.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.04f, 0.03f, 0.88f),
			BorderColor = Wood,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 12,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		layout.AddChild(shelf);

		var grid = new GridContainer
		{
			Columns = 4,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		grid.AddThemeConstantOverride("h_separation", 10);
		grid.AddThemeConstantOverride("v_separation", 10);
		shelf.AddChild(grid);

		foreach (CareerTrophy trophy in CareerDesk.Trophies)
		{
			grid.AddChild(BuildTrophyCell(trophy));
		}

		layout.AddChild(BuildTimeline());
		return card;
	}

	private Control BuildTrophyCell(CareerTrophy trophy)
	{
		Color metal = trophy.State switch
		{
			TrophyState.Won => Gold,
			TrophyState.InProgress => Accent,
			_ => EmptyMetal,
		};
		float dim = trophy.State == TrophyState.Empty ? 0.55f : 1f;

		var cell = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Modulate = new Color(1f, 1f, 1f, dim),
		};
		cell.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = trophy.State == TrophyState.Empty ? Hairline : metal,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 8,
			ContentMarginTop = 10,
			ContentMarginRight = 8,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});

		var col = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Begin,
		};
		col.AddThemeConstantOverride("separation", 6);
		cell.AddChild(col);
		col.AddChild(BuildCupMark(metal, trophy.State));

		var name = MakeText(trophy.Name.ToUpperInvariant(), _bold, 13, metal, HorizontalAlignment.Center);
		name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		col.AddChild(name);
		col.AddChild(MakeText(
			trophy.State == TrophyState.InProgress ? "IN CONTENTION" : trophy.Year,
			_semibold,
			11,
			trophy.State == TrophyState.Empty ? TextMuted : TextPrimary,
			HorizontalAlignment.Center));

		var detail = MakeText(trophy.Detail, _medium, 11, TextMuted, HorizontalAlignment.Center);
		detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		detail.SizeFlagsVertical = SizeFlags.ExpandFill;
		col.AddChild(detail);
		return cell;
	}

	private Control BuildCupMark(Color metal, TrophyState state)
	{
		var wrap = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		wrap.AddThemeConstantOverride("separation", 2);

		var bowl = new PanelContainer
		{
			CustomMinimumSize = new Vector2(42, 28),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
		};
		bowl.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(metal.R, metal.G, metal.B, state == TrophyState.Empty ? 0.12f : 0.28f),
			BorderColor = metal,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 14,
			CornerRadiusTopRight = 14,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerDetail = 6,
		});
		wrap.AddChild(bowl);

		var stem = new ColorRect
		{
			CustomMinimumSize = new Vector2(6, 14),
			Color = metal,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
		};
		wrap.AddChild(stem);

		var cupBase = new ColorRect
		{
			CustomMinimumSize = new Vector2(28, 5),
			Color = metal,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
		};
		wrap.AddChild(cupBase);
		wrap.AddChild(new ColorRect
		{
			CustomMinimumSize = new Vector2(48, 6),
			Color = Wood,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		});
		return wrap;
	}

	private Control BuildTimeline()
	{
		var strip = new PanelContainer();
		strip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = Hairline,
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
		});

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		strip.AddChild(layout);
		layout.AddChild(MakeText("THE PATH HERE", _semibold, 11, Gold, HorizontalAlignment.Left));

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 0);
		layout.AddChild(row);

		for (int i = 0; i < CareerDesk.Timeline.Count; i++)
		{
			CareerStop stop = CareerDesk.Timeline[i];
			bool current = i == CareerDesk.Timeline.Count - 1;

			if (i > 0)
			{
				row.AddChild(new ColorRect
				{
					CustomMinimumSize = new Vector2(12, 2),
					Color = GoldDeep,
					SizeFlagsVertical = SizeFlags.ShrinkCenter,
					SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
				});
			}

			var cell = new VBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			cell.AddThemeConstantOverride("separation", -1);
			cell.AddChild(MakeText(stop.Year.ToString(), _bold, 14, current ? Gold : TextPrimary, HorizontalAlignment.Left));
			cell.AddChild(MakeText(stop.Title.ToUpperInvariant(), _semibold, 12, TextPrimary, HorizontalAlignment.Left));
			cell.AddChild(MakeText(stop.Note, _medium, 11, TextMuted, HorizontalAlignment.Left));
			row.AddChild(cell);
		}

		return strip;
	}

	private Control BuildOpenings()
	{
		var card = new PanelContainer();
		card.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		card.SizeFlagsVertical = SizeFlags.ExpandFill;
		card.AddThemeStyleboxOverride("panel", MakeCardStyle(0));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);

		var heading = new PanelContainer();
		heading.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047f, 0.04f, 0.035f, 0.96f),
			ContentMarginLeft = 14,
			ContentMarginTop = 10,
			ContentMarginRight = 14,
			ContentMarginBottom = 10,
		});
		var titles = new VBoxContainer();
		titles.AddThemeConstantOverride("separation", -2);
		titles.AddChild(MakeText("JOB BOARD", _bold, 18, TextPrimary, HorizontalAlignment.Left));
		titles.AddChild(MakeText("Programs looking for a manager. Applying stays confidential until you take the job.", _medium, 12, TextMuted, HorizontalAlignment.Left));
		heading.AddChild(titles);
		layout.AddChild(heading);
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Gold });

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);

		var pad = new MarginContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		pad.AddThemeConstantOverride("margin_left", 12);
		pad.AddThemeConstantOverride("margin_top", 12);
		pad.AddThemeConstantOverride("margin_right", 12);
		pad.AddThemeConstantOverride("margin_bottom", 12);
		scroll.AddChild(pad);

		_openingsHost = new GridContainer
		{
			Columns = 2,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_openingsHost.AddThemeConstantOverride("h_separation", 10);
		_openingsHost.AddThemeConstantOverride("v_separation", 10);
		pad.AddChild(_openingsHost);
		return card;
	}

	private void RebuildOpenings()
	{
		foreach (Node child in _openingsHost.GetChildren())
		{
			_openingsHost.RemoveChild(child);
			child.QueueFree();
		}

		foreach (CareerJob job in CareerDesk.Openings)
		{
			_openingsHost.AddChild(BuildJobCard(job));
		}
	}

	private Control BuildJobCard(CareerJob job)
	{
		bool applied = _applied.Contains(job.Id);
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = applied ? Green : CardBorder,
			BorderWidthLeft = applied ? 2 : 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 12,
			ContentMarginRight = 12,
			ContentMarginBottom = 12,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		card.AddChild(layout);

		var top = new HBoxContainer();
		top.AddThemeConstantOverride("separation", 8);
		layout.AddChild(top);
		var school = MakeText(job.School.ToUpperInvariant(), _bold, 20, TextPrimary, HorizontalAlignment.Left);
		school.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		top.AddChild(school);
		top.AddChild(BuildFitPill(job.Fit));

		layout.AddChild(MakeText(
			$"{job.District.ToUpperInvariant()}  ·  {job.Record}  ·  {job.Contract.ToUpperInvariant()}",
			_medium,
			12,
			new Color(0.82f, 0.78f, 0.70f),
			HorizontalAlignment.Left));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = Hairline });
		layout.AddChild(MakeText(job.Opening.ToUpperInvariant(), _semibold, 12, Gold, HorizontalAlignment.Left));

		var pitch = MakeText(job.Pitch, _medium, 13, TextMuted, HorizontalAlignment.Left);
		pitch.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		pitch.SizeFlagsVertical = SizeFlags.ExpandFill;
		layout.AddChild(pitch);

		layout.AddChild(MakeAction(
			applied ? "APPLICATION SENT" : "APPLY",
			true,
			() => ApplyToJob(job.Id),
			applied));
		return card;
	}

	private Control BuildFitPill(string fit)
	{
		Color color = fit == "FIT" ? Green : Accent;
		var pill = new PanelContainer();
		pill.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(color.R, color.G, color.B, 0.16f),
			BorderColor = color,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 8,
			ContentMarginTop = 2,
			ContentMarginRight = 8,
			ContentMarginBottom = 2,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		pill.AddChild(MakeText(fit, _bold, 10, color, HorizontalAlignment.Center));
		return pill;
	}

	private void ApplyToJob(string jobId)
	{
		if (_applied.Contains(jobId))
		{
			return;
		}

		CareerJob? job = CareerDesk.FindJob(jobId);
		if (job == null)
		{
			return;
		}

		_applied.Add(jobId);
		_mail.Insert(0, CareerDesk.ConfirmationFor(job));
		ShowMode(DeskMode.Desk);
		SelectMail(0);
	}

	private void AcceptInterview(string mailId)
	{
		if (!_acceptedInterviews.Add(mailId))
		{
			return;
		}

		_mail.Insert(0, new CareerMail(
			"mail-interview-ok",
			"Priya Shah",
			"Capital City Times",
			"Friday after the Great Lakes set — confirmed",
			"She will find you by the home dugout. Ten minutes, notebook only.",
			"Coach,\n\nConfirmed. I will find you by the home dugout after Friday's last out. Ten minutes, notebook only — no live hit, and I will not put the Pinecrest rumor on the record unless you bring it up.\n\nSee you then.\n\nPriya Shah\nCapital City Times",
			"APR 24",
			true,
			CareerMailKind.Confirmation,
			null));
		SelectMail(0);
	}

	private Button MakeAction(string text, bool primary, Action onPressed, bool disabled = false)
	{
		var button = new Button
		{
			Text = text,
			Flat = false,
			Disabled = disabled,
			MouseDefaultCursorShape = disabled ? CursorShape.Arrow : CursorShape.PointingHand,
			CustomMinimumSize = new Vector2(0, 32),
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		if (primary && !disabled)
		{
			button.AddThemeColorOverride("font_color", TextOnAccent);
			button.AddThemeColorOverride("font_hover_color", TextOnAccent);
			button.AddThemeColorOverride("font_pressed_color", TextOnAccent);
			button.AddThemeColorOverride("font_focus_color", TextOnAccent);
			button.AddThemeStyleboxOverride("normal", MakeFilledButton(Gold));
			button.AddThemeStyleboxOverride("hover", MakeFilledButton(AccentHover));
			button.AddThemeStyleboxOverride("pressed", MakeFilledButton(AccentHover));
			button.AddThemeStyleboxOverride("focus", MakeFilledButton(AccentHover));
		}
		else
		{
			button.AddThemeColorOverride("font_color", disabled ? TextMuted : TextPrimary);
			button.AddThemeStyleboxOverride("normal", MakeOutlinedButton(CardInner, disabled ? Hairline : CardBorder));
			button.AddThemeStyleboxOverride("hover", MakeOutlinedButton(HoverBg, Accent));
			button.AddThemeStyleboxOverride("pressed", MakeOutlinedButton(HoverBg, Accent));
			button.AddThemeStyleboxOverride("focus", MakeOutlinedButton(HoverBg, Accent));
		}

		if (!disabled)
		{
			button.Pressed += onPressed;
		}

		return button;
	}

	private int UnreadCount()
	{
		int count = 0;
		foreach (CareerMail mail in _mail)
		{
			if (mail.Unread)
			{
				count++;
			}
		}

		return count;
	}

	private int FirstUnreadIndex()
	{
		for (int i = 0; i < _mail.Count; i++)
		{
			if (_mail[i].Unread)
			{
				return i;
			}
		}

		return 0;
	}

	private static string KindLabel(CareerMailKind kind) => kind switch
	{
		CareerMailKind.Contract => "ATHLETIC OFFICE",
		CareerMailKind.JobOffer => "JOB INQUIRY",
		CareerMailKind.Award => "AWARDS DESK",
		CareerMailKind.Media => "MEDIA",
		CareerMailKind.Rival => "RIVAL BENCH",
		CareerMailKind.Booster => "BOOSTER CLUB",
		_ => "CONFIRMATION",
	};

	private static Color SatisfactionColor(BoardSatisfaction satisfaction) => satisfaction switch
	{
		BoardSatisfaction.Poor => Urgent,
		BoardSatisfaction.Fair => Accent,
		BoardSatisfaction.Good => Green,
		BoardSatisfaction.Excellent => Green,
		_ => Accent,
	};

	private static Color SecurityColor(JobSecurity security) => security switch
	{
		JobSecurity.Secured => Green,
		JobSecurity.AtRisk => Urgent,
		_ => Accent,
	};

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

	private static StyleBoxFlat MakeRowStyle(bool selected, bool even, bool unread)
	{
		return new StyleBoxFlat
		{
			BgColor = selected
				? new Color(Gold.R, Gold.G, Gold.B, 0.14f)
				: unread
					? new Color(0.09f, 0.07f, 0.04f, 0.7f)
					: even ? RowEven : RowOdd,
			BorderColor = selected ? Gold : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = selected ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 10,
		};
	}

	private static StyleBoxFlat MakePill(Color bg, float padX, float padY) =>
		new()
		{
			BgColor = bg,
			ContentMarginLeft = padX,
			ContentMarginTop = padY,
			ContentMarginRight = padX,
			ContentMarginBottom = padY,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};

	private static StyleBoxFlat MakeFilledButton(Color bg) =>
		new()
		{
			BgColor = bg,
			ContentMarginLeft = 14,
			ContentMarginTop = 8,
			ContentMarginRight = 14,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};

	private static StyleBoxFlat MakeOutlinedButton(Color bg, Color border) =>
		new()
		{
			BgColor = bg,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 14,
			ContentMarginTop = 8,
			ContentMarginRight = 14,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};
}
