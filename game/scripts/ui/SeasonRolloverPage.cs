using System;
using Godot;
using HSBM.Domain;

/// <summary>
/// Year-turn recap: graduates, awards, final record, and career beats
/// before the next baseball year posts.
/// </summary>
public partial class SeasonRolloverPage : Control
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
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ExpandFill;

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

		SeasonRolloverSheet sheet = SeasonRolloverDesk.For(_session);
		var poster = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		poster.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardFill,
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 28,
			ContentMarginTop = 20,
			ContentMarginRight = 28,
			ContentMarginBottom = 20,
			CornerRadiusTopLeft = 10,
			CornerRadiusTopRight = 10,
			CornerRadiusBottomRight = 10,
			CornerRadiusBottomLeft = 10,
		});
		_body.AddChild(poster);

		var layout = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		layout.AddThemeConstantOverride("separation", 10);
		poster.AddChild(layout);

		layout.AddChild(MakeText("NEW BASEBALL YEAR", _medium, 12, Accent, HorizontalAlignment.Center));
		layout.AddChild(MakeText($"{sheet.SeasonYear} YEAR IN REVIEW", _bold, 32, TextPrimary, HorizontalAlignment.Center));
		layout.AddChild(MakeText(sheet.SchoolName, _semibold, 16, new Color(0.82f, 0.85f, 0.90f), HorizontalAlignment.Center));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });

		var metrics = new HBoxContainer();
		metrics.AddThemeConstantOverride("separation", 10);
		metrics.AddChild(BuildMetric(sheet.RecordLabel, "FINAL RECORD"));
		metrics.AddChild(BuildMetric(sheet.DistrictPlace.ToUpperInvariant(), "DISTRICT"));
		metrics.AddChild(BuildMetric(sheet.Postseason.ToUpperInvariant(), "POSTSEASON"));
		layout.AddChild(metrics);

		var columns = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		columns.AddThemeConstantOverride("separation", 10);
		columns.AddChild(BuildListCard("GRADUATING CLASS", Empty(sheet.Graduates, "No seniors leaving this spring."), sheet.Graduates, g =>
			$"{g.Name.ToUpperInvariant()}  ·  {g.Position}  ·  {g.Destination}"));
		columns.AddChild(BuildListCard("HONORS", Empty(sheet.Honors, "No program awards this year."), sheet.Honors, h =>
			$"{h.Scope} {h.Award.ToUpperInvariant()}  ·  {h.PlayerName}  ·  {h.Stat}"));
		layout.AddChild(columns);

		var career = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		career.AddThemeStyleboxOverride("panel", MakeInner());
		var careerBox = new VBoxContainer();
		careerBox.AddThemeConstantOverride("separation", 4);
		career.AddChild(careerBox);
		careerBox.AddChild(MakeText("CAREER DEVELOPMENTS", _semibold, 11, Accent, HorizontalAlignment.Left));
		foreach (string line in sheet.Developments)
		{
			var body = MakeText(line, _medium, 13, TextPrimary, HorizontalAlignment.Left);
			body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			careerBox.AddChild(body);
		}

		layout.AddChild(career);

		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 12);
		actions.AddChild(MakeAction("GO BACK", false, 0.45f, GoBack));
		actions.AddChild(MakeAction("BEGIN NEW YEAR", true, 1.55f, Continue));
		layout.AddChild(actions);
	}

	private static string Empty<T>(System.Collections.Generic.IReadOnlyList<T> items, string fallback) =>
		items.Count == 0 ? fallback : "";

	private Control BuildListCard<T>(string title, string empty, System.Collections.Generic.IReadOnlyList<T> items, Func<T, string> line)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", MakeInner());
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		card.AddChild(box);
		box.AddChild(MakeText(title, _semibold, 11, Accent, HorizontalAlignment.Left));
		if (items.Count == 0)
		{
			var none = MakeText(empty, _medium, 13, TextMuted, HorizontalAlignment.Left);
			none.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			box.AddChild(none);
			return card;
		}

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		box.AddChild(scroll);
		var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		list.AddThemeConstantOverride("separation", 4);
		scroll.AddChild(list);
		foreach (T item in items)
		{
			var row = MakeText(line(item), _medium, 13, TextPrimary, HorizontalAlignment.Left);
			row.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			list.AddChild(row);
		}

		return card;
	}

	private Control BuildMetric(string value, string label)
	{
		var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		card.AddThemeStyleboxOverride("panel", MakeInner());
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 0);
		card.AddChild(col);
		col.AddChild(MakeText(value, _bold, 24, Green, HorizontalAlignment.Center));
		col.AddChild(MakeText(label, _semibold, 11, TextMuted, HorizontalAlignment.Center));
		return card;
	}

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
			button.AddThemeStyleboxOverride("normal", MakeFilled(Accent));
			button.AddThemeStyleboxOverride("hover", MakeFilled(AccentHover));
			button.AddThemeStyleboxOverride("pressed", MakeFilled(AccentHover));
			button.AddThemeStyleboxOverride("focus", MakeFilled(AccentHover));
		}
		else
		{
			button.AddThemeColorOverride("font_color", TextPrimary);
			button.AddThemeStyleboxOverride("normal", MakeFilled(HoverBg));
			button.AddThemeStyleboxOverride("hover", MakeFilled(HoverBg));
			button.AddThemeStyleboxOverride("pressed", MakeFilled(HoverBg));
			button.AddThemeStyleboxOverride("focus", MakeFilled(HoverBg));
		}

		button.Pressed += onPressed;
		return button;
	}

	private static StyleBoxFlat MakeInner() =>
		new()
		{
			BgColor = CardInner,
			BorderColor = CardBorder,
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
		};

	private static StyleBoxFlat MakeFilled(Color bg) =>
		new()
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
}
