using System;
using Godot;
using HSBM.Domain;

/// <summary>
/// Full-screen championship payoff. District, Regional, and State share
/// the same poster: title, school, record, team-photo slot, and the road
/// to the trophy. Real team photo and stadium art plug into the slot later.
/// </summary>
public partial class ChampionshipCelebrationPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.772549f, 0.635294f, 0.325490f, 1f);
	private static readonly Color AccentHover = new(0.850980f, 0.729412f, 0.443137f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color CardInner = new(0.090196f, 0.105882f, 0.141176f, 1f);
	private static readonly Color CardFill = new(0.070588f, 0.082353f, 0.109804f, 1f);
	private static readonly Color CardBorder = new(0.137255f, 0.152941f, 0.200000f, 1f);
	private static readonly Color Hairline = new(0.17f, 0.19f, 0.24f, 1f);
	private static readonly Color GoldBorder = new(0.772549f, 0.635294f, 0.325490f, 0.85f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D? _stadium;
	private VBoxContainer _body = null!;
	private ChampionshipTitle _title = ChampionshipTitle.State;
	private bool _built;

	public event Action? Closed;
	public event Action<ChampionshipTitle>? Continued;

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

	public void Bind(ChampionshipTitle title)
	{
		_title = title;
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
		_stadium = GD.Load<Texture2D>("res://assets/teams/01_Cascade_Regional_High_School/stadium.png");
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
		_body.AddThemeConstantOverride("separation", 0);
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

		ChampionshipSheet sheet = ChampionshipDesk.For(_session, _title);
		Texture2D? logo = TeamLogos.Load(_session.CatalogOrganizationId);

		var poster = MakeGlass();
		poster.SizeFlagsVertical = SizeFlags.ExpandFill;
		_body.AddChild(poster);

		var layout = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		layout.AddThemeConstantOverride("separation", 10);
		poster.AddChild(layout);

		layout.AddChild(MakeText(sheet.ScopeLabel, _medium, 12, Accent, HorizontalAlignment.Center));
		var headline = MakeText(sheet.Headline, _bold, 46, TextPrimary, HorizontalAlignment.Center);
		headline.AutowrapMode = TextServer.AutowrapMode.Word;
		layout.AddChild(headline);
		var school = MakeText(sheet.SchoolName, _semibold, 22, new Color(0.82f, 0.85f, 0.90f), HorizontalAlignment.Center);
		school.AutowrapMode = TextServer.AutowrapMode.Word;
		layout.AddChild(school);
		layout.AddChild(MakeText(sheet.RecordLabel, _bold, 32, Accent, HorizontalAlignment.Center));
		layout.AddChild(MakeAccentLine());
		layout.AddChild(BuildPhotoSlot(logo));
		layout.AddChild(BuildRoad(sheet));
		layout.AddChild(BuildContinue(sheet));
	}

	private Control BuildPhotoSlot(Texture2D? logo)
	{
		var frame = new PanelContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.045f, 0.06f, 0.96f),
			BorderColor = GoldBorder,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerDetail = 4,
		});

		var stack = new Control
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 220),
			ClipContents = true,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		frame.AddChild(stack);

		if (_stadium != null)
		{
			var backdrop = new TextureRect
			{
				Texture = _stadium,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				Modulate = new Color(0.55f, 0.55f, 0.58f, 0.72f),
				MouseFilter = MouseFilterEnum.Ignore,
			};
			backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			stack.AddChild(backdrop);
		}

		var veil = new ColorRect
		{
			Color = new Color(0.04f, 0.05f, 0.08f, 0.42f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		stack.AddChild(veil);

		var center = new CenterContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		stack.AddChild(center);

		var copy = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		copy.AddThemeConstantOverride("separation", 6);
		center.AddChild(copy);

		if (logo != null)
		{
			copy.AddChild(new TextureRect
			{
				Texture = logo,
				CustomMinimumSize = new Vector2(88, 88),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = TextureFilterEnum.LinearWithMipmaps,
				MouseFilter = MouseFilterEnum.Ignore,
				SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			});
		}

		copy.AddChild(MakeText("TEAM PHOTO", _bold, 20, TextPrimary, HorizontalAlignment.Center));
		copy.AddChild(MakeText("STADIUM BACKDROP", _semibold, 12, Accent, HorizontalAlignment.Center));
		copy.AddChild(MakeText("Placeholder — art slots in here later.", _medium, 12, TextMuted, HorizontalAlignment.Center));
		return frame;
	}

	private Control BuildRoad(ChampionshipSheet sheet)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		box.AddChild(MakeText("ROAD TO THE CHAMPIONSHIP", _semibold, 12, Accent, HorizontalAlignment.Center));

		var row = new HBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		row.AddThemeConstantOverride("separation", 10);
		box.AddChild(row);

		for (int i = 0; i < sheet.Road.Count; i++)
		{
			if (i > 0)
			{
				row.AddChild(BuildRoadRule(sheet.Road[i].Earned || sheet.Road[i - 1].Earned));
			}

			row.AddChild(BuildRoadStep(sheet.Road[i]));
		}

		return box;
	}

	private Control BuildRoadRule(bool earned)
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(28, 2),
			Color = earned ? Accent : Hairline,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
	}

	private Control BuildRoadStep(ChampionshipRoadStep step)
	{
		Color border = step.Current ? Accent : step.Earned ? Green : CardBorder;
		Color fill = step.Current
			? new Color(0.772549f, 0.635294f, 0.325490f, 0.16f)
			: step.Earned
				? new Color(0.239216f, 0.862745f, 0.450980f, 0.10f)
				: CardInner;
		Color label = step.Earned || step.Current ? TextPrimary : TextMuted;

		var chip = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = fill,
			BorderColor = border,
			BorderWidthLeft = 1,
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

		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 0);
		chip.AddChild(col);
		col.AddChild(MakeText(step.Earned ? "WON" : step.Current ? "NOW" : "NEXT", _semibold, 10, border, HorizontalAlignment.Center));
		var name = MakeText(step.Label.ToUpperInvariant(), _bold, 14, label, HorizontalAlignment.Center);
		name.AutowrapMode = TextServer.AutowrapMode.Word;
		col.AddChild(name);
		return chip;
	}

	private Control BuildContinue(ChampionshipSheet sheet)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		row.AddChild(MakeAction("GO BACK", false, 0.45f, GoBack));
		Button next = MakeAction(sheet.ContinueLabel, true, 1.55f, Continue);
		row.AddChild(next);
		return row;
	}

	private void Continue()
	{
		UiSounds.Play(UiSound.Navigate);
		Continued?.Invoke(_title);
	}

	private void GoBack()
	{
		UiSounds.Play(UiSound.Back);
		Closed?.Invoke();
	}

	private PanelContainer MakeGlass()
	{
		var card = new PanelContainer();
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardFill,
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 28,
			ContentMarginTop = 18,
			ContentMarginRight = 28,
			ContentMarginBottom = 18,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10,
			CornerDetail = 4,
			ShadowColor = new Color(0f, 0f, 0f, 0.35f),
			ShadowSize = 12,
			ShadowOffset = new Vector2(0, 4),
		});
		return card;
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

	private static ColorRect MakeAccentLine()
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(0, 2),
			Color = Accent,
		};
	}

	private static StyleBoxFlat MakeFilledButton(Color bg)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			ContentMarginLeft = 16,
			ContentMarginTop = 10,
			ContentMarginRight = 16,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
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
			ContentMarginLeft = 16,
			ContentMarginTop = 10,
			ContentMarginRight = 16,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerDetail = 4,
		};
	}
}
