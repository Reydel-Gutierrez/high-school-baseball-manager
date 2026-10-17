using Godot;
using HSBM.Domain;

/// <summary>
/// Local businesses backing the baseball program. Cash and in-kind only —
/// high-school players do not have contracts.
/// </summary>
public partial class SponsorsPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _fallbackLogo = null!;
	private TextureRect _mark = null!;
	private Label _subtitle = null!;
	private VBoxContainer _sponsorHost = null!;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_fallbackLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.CareerChanged += OnSessionChanged;
		_session.ContractStateChanged += OnSessionChanged;
		VisibilityChanged += OnVisibilityChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.CareerChanged -= OnSessionChanged;
			_session.ContractStateChanged -= OnSessionChanged;
		}

		VisibilityChanged -= OnVisibilityChanged;
	}

	private void OnSessionChanged() => Refresh();

	private void OnVisibilityChanged()
	{
		if (IsVisibleInTree())
		{
			Refresh();
		}
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
		layout.AddThemeConstantOverride("separation", 8);
		margin.AddChild(layout);

		layout.AddChild(BuildHeader());
		layout.AddChild(BuildListCard());
	}

	private Control BuildHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);

		_mark = TeamLogos.MakeHeaderMark(_fallbackLogo);
		header.AddChild(_mark);
		header.AddChild(TeamLogos.MakeHeaderRule(Hairline));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -4);
		identity.AddChild(MakeText($"FINANCES  ·  {GameSession.SeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("SPONSORS", _bold, 22, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("", _medium, 11, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);
		return header;
	}

	private Control BuildListCard()
	{
		var card = MakeCard();
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 8);
		card.AddChild(layout);

		layout.AddChild(MakeText("CURRENT SPONSORS", _semibold, 12, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(MakeAccentLine());

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		layout.AddChild(scroll);

		_sponsorHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_sponsorHost.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_sponsorHost);
		return card;
	}

	private void Refresh()
	{
		if (_subtitle == null)
		{
			return;
		}

		_mark.Texture = TeamLogos.Load(_session.Organization.Id) ?? _fallbackLogo;
		_subtitle.Text = _session.Organization.Name.ToUpperInvariant();

		ProgramFinanceSnapshot sheet = FinanceDesk.Current(_session);
		foreach (Node child in _sponsorHost.GetChildren())
		{
			child.QueueFree();
		}

		if (sheet.Sponsors.Count == 0)
		{
			_sponsorHost.AddChild(MakeText(
				"No sponsors on the books this season.",
				_medium,
				14,
				TextMuted,
				HorizontalAlignment.Left));
			return;
		}

		foreach (ProgramSponsor sponsor in sheet.Sponsors)
		{
			_sponsorHost.AddChild(BuildSponsorCard(sponsor));
		}
	}

	private Control BuildSponsorCard(ProgramSponsor sponsor)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = Hairline,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 14,
			ContentMarginTop = 12,
			ContentMarginRight = 14,
			ContentMarginBottom = 12,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		});

		var copy = new VBoxContainer();
		copy.AddThemeConstantOverride("separation", 0);
		copy.AddChild(MakeText(sponsor.Name.ToUpperInvariant(), _bold, 22, TextPrimary, HorizontalAlignment.Left));
		copy.AddChild(MakeText(sponsor.AmountLabel, _semibold, 14, Accent, HorizontalAlignment.Left));
		card.AddChild(copy);
		return card;
	}

	private static PanelContainer MakeCard()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.078431f, 0.094118f, 0.94f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 14,
			ContentMarginTop = 12,
			ContentMarginRight = 14,
			ContentMarginBottom = 12,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		});
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
}
