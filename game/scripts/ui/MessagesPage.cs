using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// Coach inbox: things that need attention before the next day.
/// Not the league wire — click-through takes you to the relevant desk.
/// </summary>
public partial class MessagesPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.772549f, 0.635294f, 0.325490f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardInner = new(0.090196f, 0.105882f, 0.141176f, 1f);
	private static readonly Color CardBorder = new(0.137255f, 0.152941f, 0.200000f, 1f);
	private static readonly Color Hairline = new(0.17f, 0.19f, 0.24f, 1f);
	private static readonly Color LakesBlue = new(0.45f, 0.72f, 0.88f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;

	private Label _subtitle = null!;
	private Label _countValue = null!;
	private HBoxContainer _filterHost = null!;
	private VBoxContainer _listHost = null!;
	private VBoxContainer _featureHost = null!;
	private int _selectedIndex;
	private string? _selectedId;
	private InboxChannel? _filter;
	private List<InboxMessage> _items = new();

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.CareerChanged += OnCareerChanged;
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		_session.ScoutTargetsChanged += OnCareerChanged;
		VisibilityChanged += OnVisibilityChanged;
		ApplyChrome();
		BindInbox();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.CareerChanged -= OnCareerChanged;
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
			_session.ScoutTargetsChanged -= OnCareerChanged;
		}

		VisibilityChanged -= OnVisibilityChanged;
	}

	private void OnVisibilityChanged()
	{
		if (IsVisibleInTree())
		{
			BindInbox();
		}
	}

	private void OnCareerChanged()
	{
		if (IsVisibleInTree())
		{
			BindInbox();
		}
		else
		{
			ApplyChrome();
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => ApplyChrome();

	private void ApplyChrome()
	{
		int unread = MessagesDesk.UnreadCount(_session);
		int total = MessagesDesk.For(_session).Count;
		_subtitle.Text = unread > 0
			? $"{unread} NEED{(unread == 1 ? "S" : "")} ATTENTION  ·  {_session.CurrentDate.ToString("dddd, MMMM d", CultureInfo.InvariantCulture).ToUpperInvariant()}"
			: $"CLEAR  ·  {_session.CurrentDate.ToString("dddd, MMMM d", CultureInfo.InvariantCulture).ToUpperInvariant()}";
		_countValue.Text = total.ToString(CultureInfo.InvariantCulture);
		_countValue.AddThemeColorOverride("font_color", unread > 0 ? Accent : Green);
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

		var body = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 8);
		layout.AddChild(body);

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

		var listHeading = new PanelContainer();
		listHeading.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 8,
		});
		listHeading.AddChild(MakeText("INBOX", _bold, 14, TextPrimary, HorizontalAlignment.Left));
		listLayout.AddChild(listHeading);
		listLayout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		listLayout.AddChild(scroll);
		_listHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_listHost.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(_listHost);
		body.AddChild(listCard);

		var featureCard = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.15f,
			ClipContents = true,
		};
		featureCard.AddThemeStyleboxOverride("panel", MakeCardStyle(12));
		_featureHost = new VBoxContainer();
		_featureHost.AddThemeConstantOverride("separation", 8);
		featureCard.AddChild(_featureHost);
		body.AddChild(featureCard);
	}

	private Control BuildHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);
		header.AddChild(UserProgram.MakeHeaderMark());
		header.AddChild(TeamLogos.MakeHeaderRule(Hairline));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -2);
		identity.AddChild(MakeText($"CLUBHOUSE  ·  {GameSession.SeasonYear} SEASON", _semibold, 12, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("MESSAGES", _bold, 36, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("CLEAR", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var metric = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		metric.AddThemeConstantOverride("separation", -2);
		_countValue = MakeText("0", _bold, 26, Accent, HorizontalAlignment.Right);
		metric.AddChild(_countValue);
		metric.AddChild(MakeText("WAITING", _medium, 10, TextMuted, HorizontalAlignment.Right));
		header.AddChild(metric);
		return header;
	}

	private Control BuildFilters()
	{
		_filterHost = new HBoxContainer();
		_filterHost.AddThemeConstantOverride("separation", 6);
		return _filterHost;
	}

	private void RebuildFilters()
	{
		foreach (Node child in _filterHost.GetChildren())
		{
			_filterHost.RemoveChild(child);
			child.QueueFree();
		}

		IReadOnlyList<InboxMessage> all = MessagesDesk.For(_session);
		_filterHost.AddChild(MakeFilterChip("ALL", null, all.Count));
		foreach (InboxChannel channel in Enum.GetValues<InboxChannel>())
		{
			int count = 0;
			foreach (InboxMessage item in all)
			{
				if (item.Channel == channel)
				{
					count++;
				}
			}

			if (count == 0)
			{
				continue;
			}

			_filterHost.AddChild(MakeFilterChip(channel.ToLabel(), channel, count));
		}
	}

	private Control MakeFilterChip(string label, InboxChannel? channel, int count)
	{
		bool selected = _filter == channel;
		var button = new Button
		{
			Text = $"{label}  {count}",
			MouseDefaultCursorShape = CursorShape.PointingHand,
			CustomMinimumSize = new Vector2(0, 28),
		};
		button.AddThemeFontOverride("font", selected ? _bold : _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		Color color = channel == null ? Accent : ChannelColor(channel.Value);
		button.AddThemeColorOverride("font_color", selected ? TextOnAccent : color);
		button.AddThemeColorOverride("font_hover_color", selected ? TextOnAccent : TextPrimary);
		button.AddThemeStyleboxOverride("normal", selected ? MakeFilledButton(color) : MakeOutlinedButton(color));
		button.AddThemeStyleboxOverride("hover", selected ? MakeFilledButton(color) : MakeOutlinedButton(Accent));
		button.AddThemeStyleboxOverride("pressed", MakeFilledButton(color));
		button.AddThemeStyleboxOverride("focus", selected ? MakeFilledButton(color) : MakeOutlinedButton(Accent));
		button.Pressed += () =>
		{
			_filter = channel;
			_selectedIndex = 0;
			_selectedId = null;
			BindInbox();
		};
		return button;
	}

	private void BindInbox()
	{
		ApplyChrome();
		RebuildFilters();
		_items = new List<InboxMessage>();
		foreach (InboxMessage item in MessagesDesk.For(_session))
		{
			if (_filter == null || item.Channel == _filter)
			{
				_items.Add(item);
			}
		}

		if (!string.IsNullOrEmpty(_selectedId))
		{
			for (int i = 0; i < _items.Count; i++)
			{
				if (_items[i].Id == _selectedId)
				{
					_selectedIndex = i;
					break;
				}
			}
		}

		if (_selectedIndex >= _items.Count)
		{
			_selectedIndex = 0;
		}

		RebuildList();
		if (_items.Count > 0)
		{
			SelectItem(_selectedIndex, markRead: false);
		}
		else
		{
			BindEmpty();
		}
	}

	private void RebuildList()
	{
		foreach (Node child in _listHost.GetChildren())
		{
			_listHost.RemoveChild(child);
			child.QueueFree();
		}

		for (int i = 0; i < _items.Count; i++)
		{
			_listHost.AddChild(BuildListRow(_items[i], i, i == _selectedIndex));
		}
	}

	private Control BuildListRow(InboxMessage item, int index, bool selected)
	{
		var row = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		row.AddThemeStyleboxOverride("panel", MakeRowStyle(selected, index % 2 == 0, item.Unread && !selected));
		row.GuiInput += (InputEvent evt) =>
		{
			if (evt is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				if (selected)
				{
					OpenItem(item);
				}
				else
				{
					SelectItem(index, markRead: true);
				}
			}
		};

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);

		var pip = new ColorRect
		{
			CustomMinimumSize = new Vector2(6, 6),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Color = item.Unread ? ChannelColor(item.Channel) : Hairline,
		};
		columns.AddChild(pip);

		var copy = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		copy.AddThemeConstantOverride("separation", 2);
		copy.AddChild(MakeTag(item.Channel));
		var headline = MakeText(
			item.Headline,
			selected || item.Unread ? _bold : _semibold,
			13,
			selected ? Accent : TextPrimary,
			HorizontalAlignment.Left);
		headline.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		copy.AddChild(headline);
		var preview = MakeText(item.Preview, _medium, 12, TextMuted, HorizontalAlignment.Left);
		preview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		preview.MaxLinesVisible = 1;
		copy.AddChild(preview);
		columns.AddChild(copy);

		row.AddChild(columns);
		return row;
	}

	private void SelectItem(int index, bool markRead)
	{
		if (_items.Count == 0)
		{
			BindEmpty();
			return;
		}

		_selectedIndex = Math.Clamp(index, 0, _items.Count - 1);
		InboxMessage item = _items[_selectedIndex];
		_selectedId = item.Id;
		if (markRead && item.Unread)
		{
			_session.AcknowledgeMessage(item.Id);
			return;
		}

		RebuildList();
		BindFeature(item);
	}

	private void BindEmpty()
	{
		foreach (Node child in _featureHost.GetChildren())
		{
			_featureHost.RemoveChild(child);
			child.QueueFree();
		}

		_featureHost.AddChild(MakeTag(InboxChannel.Urgent));
		_featureHost.AddChild(MakeText("INBOX", _semibold, 11, TextMuted, HorizontalAlignment.Left));
		_featureHost.AddChild(MakeText("NOTHING WAITING", _bold, 26, TextPrimary, HorizontalAlignment.Left));
		var body = MakeText(
			"When a roster decision, scouting report, board card, injury, or job call needs you before the next day, it lands here. Advance when the desk is clear.",
			_medium,
			15,
			new Color(0.78f, 0.81f, 0.86f),
			HorizontalAlignment.Left);
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_featureHost.AddChild(body);
	}

	private void BindFeature(InboxMessage item)
	{
		foreach (Node child in _featureHost.GetChildren())
		{
			_featureHost.RemoveChild(child);
			child.QueueFree();
		}

		_featureHost.AddChild(MakeTag(item.Channel));
		_featureHost.AddChild(MakeText(item.DateLabel + "  ·  " + item.Channel.ToLabel(), _semibold, 11, TextMuted, HorizontalAlignment.Left));

		var headline = MakeText(item.Headline.ToUpperInvariant(), _bold, 26, TextPrimary, HorizontalAlignment.Left);
		headline.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_featureHost.AddChild(headline);
		_featureHost.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = ChannelColor(item.Channel) });

		var body = MakeText(item.Body, _medium, 15, new Color(0.78f, 0.81f, 0.86f), HorizontalAlignment.Left);
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_featureHost.AddChild(body);

		var pull = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		pull.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = CardInner,
			BorderColor = Hairline,
			BorderWidthLeft = 2,
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
		var pullCopy = new VBoxContainer();
		pullCopy.AddThemeConstantOverride("separation", 4);
		pullCopy.AddChild(MakeText("WHY IT MATTERS", _semibold, 10, Accent, HorizontalAlignment.Left));
		var pullBody = MakeText(item.Preview, _medium, 13, TextMuted, HorizontalAlignment.Left);
		pullBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		pullCopy.AddChild(pullBody);
		pull.AddChild(pullCopy);
		_featureHost.AddChild(pull);

		var open = new Button
		{
			Text = item.ActionLabel,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			CustomMinimumSize = new Vector2(0, 40),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
		};
		open.AddThemeFontOverride("font", _bold);
		open.AddThemeFontSizeOverride("font_size", 14);
		open.AddThemeColorOverride("font_color", TextOnAccent);
		open.AddThemeColorOverride("font_hover_color", TextOnAccent);
		open.AddThemeStyleboxOverride("normal", MakeFilledButton(ChannelColor(item.Channel)));
		open.AddThemeStyleboxOverride("hover", MakeFilledButton(Accent));
		open.AddThemeStyleboxOverride("pressed", MakeFilledButton(Accent));
		open.AddThemeStyleboxOverride("focus", MakeFilledButton(Accent));
		open.Pressed += () => OpenItem(item);
		_featureHost.AddChild(open);
	}

	private void OpenItem(InboxMessage item)
	{
		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.OpenInboxItem(item);
		}
	}

	private PanelContainer MakeTag(InboxChannel channel)
	{
		Color color = ChannelColor(channel);
		var pill = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
		};
		pill.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(color.R, color.G, color.B, 0.16f),
			BorderColor = color,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 7,
			ContentMarginTop = 2,
			ContentMarginRight = 7,
			ContentMarginBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
		});
		pill.AddChild(MakeText(channel.ToLabel(), _bold, 10, color, HorizontalAlignment.Center));
		return pill;
	}

	private static Color ChannelColor(InboxChannel channel) => channel switch
	{
		InboxChannel.Urgent => Urgent,
		InboxChannel.Scouting => LakesBlue,
		InboxChannel.Board => Accent,
		InboxChannel.Player => Green,
		InboxChannel.Career => new Color(0.72f, 0.55f, 0.90f, 1f),
		_ => Hairline,
	};

	private static StyleBoxFlat MakeRowStyle(bool selected, bool even, bool unread)
	{
		return new StyleBoxFlat
		{
			BgColor = selected
				? new Color(0.772549f, 0.635294f, 0.325490f, 0.14f)
				: unread
					? new Color(0.772549f, 0.635294f, 0.325490f, 0.06f)
					: even ? RowEven : RowOdd,
			BorderColor = selected ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = selected || unread ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 10,
		};
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

	private static StyleBoxFlat MakeCardStyle(float pad)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = pad,
			ContentMarginTop = pad,
			ContentMarginRight = pad,
			ContentMarginBottom = pad,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerDetail = 3,
		};
	}

	private static StyleBoxFlat MakeFilledButton(Color color)
	{
		return new StyleBoxFlat
		{
			BgColor = color,
			ContentMarginLeft = 14,
			ContentMarginTop = 6,
			ContentMarginRight = 14,
			ContentMarginBottom = 6,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
		};
	}

	private static StyleBoxFlat MakeOutlinedButton(Color color)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.055f, 0.062f, 0.078f, 0.92f),
			BorderColor = color,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 5,
			ContentMarginRight = 12,
			ContentMarginBottom = 5,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
		};
	}
}
