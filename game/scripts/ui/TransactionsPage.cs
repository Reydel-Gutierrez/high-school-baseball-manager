using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// Organization transaction log: completed trades, District Open Pool
/// signings, releases into the pool, and freshman tryout selections.
/// </summary>
public partial class TransactionsPage : Control
{
	private const string FilterAll = "all";
	private const string FilterTrades = "trade";
	private const string FilterOpenPool = "open_pool";
	private const string FilterTryouts = "tryout";
	private const string FilterReleases = "release";
	private const int CurrentSeasonYear = 2026;

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color OpenPoolBlue = new(0.45f, 0.72f, 0.88f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _cascadeLogo = null!;

	private readonly Dictionary<string, Button> _filterButtons = new();

	private Label _subtitle = null!;
	private Label _countValue = null!;
	private VBoxContainer _rowsHost = null!;
	private string _activeFilter = FilterAll;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_cascadeLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		_session.TransactionsChanged += OnTransactionsChanged;
		ApplyActiveTeam();
		ApplyFilter(FilterAll);
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
			_session.TransactionsChanged -= OnTransactionsChanged;
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => ApplyActiveTeam();

	private void OnTransactionsChanged()
	{
		if (IsVisibleInTree())
		{
			RebuildRows();
		}
	}

	public override void _Notification(int what)
	{
		if (what == NotificationVisibilityChanged && Visible && _session != null && _rowsHost != null)
		{
			RebuildRows();
		}
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
		layout.AddChild(BuildFilters());
		layout.AddChild(BuildListCard());
	}

	private Control BuildHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);

		header.AddChild(TeamLogos.MakeHeaderMark(_cascadeLogo));
		header.AddChild(TeamLogos.MakeHeaderRule(new Color(0.28f, 0.30f, 0.35f, 1f)));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -2);
		identity.AddChild(MakeText($"ORGANIZATION  ·  {CurrentSeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("TRANSACTIONS", _bold, 26, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("CASCADE REGIONAL HIGH SCHOOL", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var countBlock = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		countBlock.AddThemeConstantOverride("separation", -4);
		_countValue = MakeText("0", _bold, 28, TextPrimary, HorizontalAlignment.Right);
		countBlock.AddChild(_countValue);
		countBlock.AddChild(MakeText("MOVES", _semibold, 11, TextMuted, HorizontalAlignment.Right));
		header.AddChild(countBlock);
		return header;
	}

	private Control BuildFilters()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		AddFilter(row, FilterAll, "ALL");
		AddFilter(row, FilterTrades, "TRADES");
		AddFilter(row, FilterOpenPool, "OPEN POOL");
		AddFilter(row, FilterTryouts, "TRYOUTS");
		AddFilter(row, FilterReleases, "RELEASES");
		return row;
	}

	private void AddFilter(HBoxContainer row, string key, string label)
	{
		var button = new Button { Text = label };
		button.Pressed += () => ApplyFilter(key);
		_filterButtons[key] = button;
		row.AddChild(button);
	}

	private Control BuildListCard()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle());

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
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
			FollowFocus = true,
		};
		layout.AddChild(scroll);

		_rowsHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
		};
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
		columns.AddThemeConstantOverride("separation", 12);
		columns.AddChild(MakeHeaderCell("DATE", 72));
		columns.AddChild(MakeHeaderCell("TYPE", 100));
		var transaction = MakeText("TRANSACTION", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		transaction.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(transaction);
		header.AddChild(columns);
		return header;
	}

	private Label MakeHeaderCell(string text, float width)
	{
		Label label = MakeText(text, _semibold, 10, TextMuted, HorizontalAlignment.Left);
		label.CustomMinimumSize = new Vector2(width, 0);
		return label;
	}

	private void ApplyFilter(string filterKey)
	{
		_activeFilter = filterKey;
		UpdateFilterStyles();
		RebuildRows();
	}

	private void UpdateFilterStyles()
	{
		foreach (KeyValuePair<string, Button> pair in _filterButtons)
		{
			bool isActive = pair.Key == _activeFilter;
			Button button = pair.Value;
			button.Flat = false;
			button.AddThemeFontOverride("font", _semibold);
			button.AddThemeFontSizeOverride("font_size", 12);
			button.AddThemeColorOverride("font_color", isActive ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("font_hover_color", isActive ? TextOnAccent : Colors.White);
			button.AddThemeColorOverride("font_pressed_color", isActive ? TextOnAccent : TextPrimary);
			button.AddThemeColorOverride("font_focus_color", isActive ? TextOnAccent : Colors.White);
			button.AddThemeStyleboxOverride("normal", MakePill(isActive ? Accent : Colors.Transparent, 12));
			button.AddThemeStyleboxOverride("hover", MakePill(isActive ? AccentHover : HoverBg, 12));
			button.AddThemeStyleboxOverride("pressed", MakePill(isActive ? AccentHover : HoverBg, 12));
			button.AddThemeStyleboxOverride("focus", MakePill(isActive ? AccentHover : HoverBg, 12));
		}
	}

	private void RebuildRows()
	{
		foreach (Node child in _rowsHost.GetChildren())
		{
			child.QueueFree();
		}

		List<PlayerTransaction> visible = GetVisibleEntries();
		_countValue.Text = $"{visible.Count}";

		if (visible.Count == 0)
		{
			var empty = MakeText("No transactions in this group.", _medium, 13, TextMuted, HorizontalAlignment.Center);
			empty.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_rowsHost.AddChild(empty);
			return;
		}

		for (int i = 0; i < visible.Count; i++)
		{
			_rowsHost.AddChild(BuildRow(visible[i], i % 2 == 0));
		}
	}

	private List<PlayerTransaction> GetVisibleEntries()
	{
		IReadOnlyList<PlayerTransaction> entries = _session.Transactions;
		if (_activeFilter == FilterAll)
		{
			return new List<PlayerTransaction>(entries);
		}

		PlayerTransactionKind kind = FilterKind(_activeFilter);
		var matches = new List<PlayerTransaction>();
		foreach (PlayerTransaction entry in entries)
		{
			if (entry.Kind == kind)
			{
				matches.Add(entry);
			}
		}

		return matches;
	}

	private Control BuildRow(PlayerTransaction entry, bool even)
	{
		var row = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
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
		columns.AddThemeConstantOverride("separation", 12);

		Label date = MakeText(entry.DateLabel, _semibold, 12, TextMuted, HorizontalAlignment.Left);
		date.CustomMinimumSize = new Vector2(72, 0);
		date.VerticalAlignment = VerticalAlignment.Top;
		columns.AddChild(date);

		var typeWrap = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(100, 0),
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
		};
		typeWrap.AddChild(MakeTypePill(entry));
		columns.AddChild(typeWrap);

		var copy = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		copy.AddThemeConstantOverride("separation", 2);

		var headline = MakeText(entry.Headline, _medium, 14, TextPrimary, HorizontalAlignment.Left);
		headline.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		headline.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		copy.AddChild(headline);
		copy.AddChild(MakeText(entry.Detail, _semibold, 11, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(copy);

		row.AddChild(columns);
		return row;
	}

	private PanelContainer MakeTypePill(PlayerTransaction entry)
	{
		Color color = TypeColor(entry.Kind);
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
			ContentMarginTop = 3,
			ContentMarginRight = 8,
			ContentMarginBottom = 3,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		});
		pill.AddChild(MakeText(TypeLabel(entry.Kind), _semibold, 10, color, HorizontalAlignment.Center));
		return pill;
	}

	private static PlayerTransactionKind FilterKind(string filter) => filter switch
	{
		FilterTrades => PlayerTransactionKind.Trade,
		FilterOpenPool => PlayerTransactionKind.OpenPool,
		FilterTryouts => PlayerTransactionKind.Tryout,
		_ => PlayerTransactionKind.Release,
	};

	private static string TypeLabel(PlayerTransactionKind kind) => kind switch
	{
		PlayerTransactionKind.Trade => "TRADE",
		PlayerTransactionKind.OpenPool => "OPEN POOL",
		PlayerTransactionKind.Release => "RELEASE",
		_ => "TRYOUT",
	};

	private static Color TypeColor(PlayerTransactionKind kind) => kind switch
	{
		PlayerTransactionKind.Trade => Accent,
		PlayerTransactionKind.OpenPool => OpenPoolBlue,
		PlayerTransactionKind.Release => Urgent,
		_ => Green,
	};

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
			ContentMarginLeft = 0,
			ContentMarginTop = 0,
			ContentMarginRight = 0,
			ContentMarginBottom = 0,
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
