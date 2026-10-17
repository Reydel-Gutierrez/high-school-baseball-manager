using System;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// Year-end board recap. Record, district, postseason, contract target,
/// reputation, budget, and the players who defined the year — then
/// CONTINUE TO OFFSEASON.
/// </summary>
public partial class SeasonReviewPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.90f);
	private static readonly Color CardFill = new(0.070588f, 0.078431f, 0.094118f, 0.88f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private VBoxContainer _body = null!;
	private bool _built;

	public event Action? Closed;
	public event Action? Continued;

	public override void _Ready()
	{
		EnsureBuilt();
		Rebuild();
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (!Visible || !ev.IsActionPressed("ui_cancel"))
		{
			return;
		}

		GoBack();
		GetViewport().SetInputAsHandled();
	}

	public void Refresh()
	{
		if (_built)
		{
			Rebuild();
		}
	}

	private void EnsureBuilt()
	{
		if (_built)
		{
			return;
		}

		_built = true;
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_session = GetNode<GameSession>("/root/GameSession");
		MouseFilter = MouseFilterEnum.Stop;

		var dim = new ColorRect
		{
			Color = new Color(0.02f, 0.025f, 0.04f, 0.52f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var margin = new MarginContainer();
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 36);
		margin.AddThemeConstantOverride("margin_top", 22);
		margin.AddThemeConstantOverride("margin_right", 36);
		margin.AddThemeConstantOverride("margin_bottom", 22);
		AddChild(margin);

		_body = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		margin.AddChild(_body);
	}

	private void Rebuild()
	{
		if (!_built)
		{
			return;
		}

		foreach (Node child in _body.GetChildren())
		{
			child.QueueFree();
		}

		SeasonReviewSheet sheet = SeasonReviewDesk.For(_session);
		Texture2D? logo = TeamLogos.Load(_session.CatalogOrganizationId);

		var poster = new PanelContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		poster.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardFill,
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 28,
			ContentMarginTop = 22,
			ContentMarginRight = 28,
			ContentMarginBottom = 22,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			ShadowColor = new Color(0f, 0f, 0f, 0.35f),
			ShadowSize = 12,
			ShadowOffset = new Vector2(0, 4),
		});
		_body.AddChild(poster);

		var layout = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		layout.AddThemeConstantOverride("separation", 12);
		poster.AddChild(layout);

		layout.AddChild(MakeText("SEASON REVIEW", _medium, 12, Accent, HorizontalAlignment.Center));
		layout.AddChild(MakeText($"{sheet.SeasonYear} SEASON REVIEW", _bold, 34, TextPrimary, HorizontalAlignment.Center));
		layout.AddChild(MakeText(sheet.SchoolName, _semibold, 18, new Color(0.82f, 0.85f, 0.90f), HorizontalAlignment.Center));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });

		var metrics = new HBoxContainer();
		metrics.AddThemeConstantOverride("separation", 10);
		metrics.AddChild(BuildMetric(sheet.RecordLabel, "FINAL RECORD"));
		metrics.AddChild(BuildMetric(sheet.DistrictPlaceLabel, "DISTRICT"));
		metrics.AddChild(BuildMetric(sheet.PostseasonLabel.ToUpperInvariant(), "POSTSEASON"));
		layout.AddChild(metrics);

		layout.AddChild(BuildTargetRow(sheet));

		var deltas = new HBoxContainer();
		deltas.AddThemeConstantOverride("separation", 10);
		deltas.AddChild(BuildDelta("BOARD SATISFACTION", sheet.SatisfactionBefore, sheet.SatisfactionAfter));
		deltas.AddChild(BuildDelta("PROGRAM REPUTATION", sheet.ReputationBefore, sheet.ReputationAfter));
		deltas.AddChild(BuildFinance(sheet));
		layout.AddChild(deltas);

		var players = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		players.AddThemeConstantOverride("separation", 10);
		players.AddChild(BuildPlayerCard("TOP PLAYER", sheet.TopPlayer));
		players.AddChild(BuildPlayerCard("BREAKOUT PLAYER", sheet.BreakoutPlayer));
		layout.AddChild(players);

		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 12);
		actions.AddChild(MakeAction("GO BACK", false, 0.45f, GoBack));
		actions.AddChild(MakeAction(sheet.ContinueLabel, true, 1.55f, Continue));
		layout.AddChild(actions);
	}

	private Control BuildMetric(string value, string label, bool positive = true)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
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
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 0);
		card.AddChild(col);
		col.AddChild(MakeText(value, _bold, 26, positive ? Green : TextPrimary, HorizontalAlignment.Center));
		col.AddChild(MakeText(label, _semibold, 11, TextMuted, HorizontalAlignment.Center));
		return card;
	}

	private Control BuildTargetRow(SeasonReviewSheet sheet)
	{
		Color color = sheet.TargetMet ? Green : Urgent;
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", MakeInnerPanel(color));
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		card.AddChild(row);
		var copy = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		copy.AddThemeConstantOverride("separation", -2);
		copy.AddChild(MakeText("BOARD TARGET", _semibold, 11, Accent, HorizontalAlignment.Left));
		copy.AddChild(MakeText(sheet.TargetLabel, _bold, 22, TextPrimary, HorizontalAlignment.Left));
		row.AddChild(copy);
		row.AddChild(MakeText(sheet.TargetMet ? "MET  ✓" : "MISSED", _bold, 22, color, HorizontalAlignment.Right));
		return card;
	}

	private Control BuildDelta(string label, int before, int after)
	{
		int delta = after - before;
		Color color = delta > 0 ? Green : delta < 0 ? Urgent : TextPrimary;
		string arrow = delta > 0 ? "→" : delta < 0 ? "→" : "·";
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", MakeInnerPanel(CardBorder));
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 0);
		card.AddChild(col);
		col.AddChild(MakeText(label, _semibold, 11, Accent, HorizontalAlignment.Center));
		col.AddChild(MakeText($"{before} {arrow} {after}", _bold, 24, color, HorizontalAlignment.Center));
		string signed = delta > 0 ? $"+{delta}" : delta.ToString(CultureInfo.InvariantCulture);
		col.AddChild(MakeText(signed, _medium, 12, TextMuted, HorizontalAlignment.Center));
		return card;
	}

	private Control BuildFinance(SeasonReviewSheet sheet)
	{
		Color color = sheet.FinanceDelta > 0 ? Green : sheet.FinanceDelta < 0 ? Urgent : TextPrimary;
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", MakeInnerPanel(CardBorder));
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 0);
		card.AddChild(col);
		col.AddChild(MakeText("FINANCES", _semibold, 11, Accent, HorizontalAlignment.Center));
		col.AddChild(MakeText(sheet.FinanceLabel, _bold, 24, color, HorizontalAlignment.Center));
		col.AddChild(MakeText("SEASON SURPLUS", _medium, 12, TextMuted, HorizontalAlignment.Center));
		return card;
	}

	private Control BuildPlayerCard(string label, SeasonReviewPlayer player)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", MakeInnerPanel(Hairline));
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		card.AddChild(row);

		Control portrait = BuildPortrait(player);
		portrait.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(portrait);

		var copy = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		copy.AddThemeConstantOverride("separation", 0);
		copy.AddChild(MakeText(label, _semibold, 11, Accent, HorizontalAlignment.Left));
		copy.AddChild(MakeText(player.Name, _bold, 22, TextPrimary, HorizontalAlignment.Left));
		var line = MakeText(player.Line, _medium, 13, TextMuted, HorizontalAlignment.Left);
		line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		copy.AddChild(line);
		row.AddChild(copy);
		return card;
	}

	private Control BuildPortrait(SeasonReviewPlayer player)
	{
		if (!string.IsNullOrEmpty(player.PortraitPath))
		{
			var texture = GD.Load<Texture2D>(player.PortraitPath);
			if (texture != null)
			{
				return new TextureRect
				{
					Texture = texture,
					CustomMinimumSize = new Vector2(56, 72),
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
					TextureFilter = TextureFilterEnum.LinearWithMipmaps,
					MouseFilter = MouseFilterEnum.Ignore,
				};
			}
		}

		var fallback = new PanelContainer
		{
			CustomMinimumSize = new Vector2(56, 72),
		};
		fallback.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = Accent,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		string initials = player.Name.Length == 0 ? "—" : player.Name[..1];
		fallback.AddChild(MakeText(initials, _bold, 22, Accent, HorizontalAlignment.Center));
		return fallback;
	}

	private static StyleBoxFlat MakeInnerPanel(Color border) =>
		new()
		{
			BgColor = CardInner,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 14,
			ContentMarginTop = 10,
			ContentMarginRight = 14,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		};

	private void Continue()
	{
		UiSounds.Play(UiSound.Navigate);
		Continued?.Invoke();
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
