using System;
using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// Graduation / alumni desk for this spring's seniors. Career lines,
/// honors, and a fictional college next step.
/// </summary>
public partial class GraduationPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.772549f, 0.635294f, 0.325490f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color CardBorder = new(0.137255f, 0.152941f, 0.200000f, 1f);
	private static readonly Color Hairline = new(0.17f, 0.19f, 0.24f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color RowSelected = new(0.772549f, 0.635294f, 0.325490f, 0.14f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;

	private Label _title = null!;
	private Label _subtitle = null!;
	private Label _countValue = null!;
	private VBoxContainer _rowsHost = null!;
	private VBoxContainer _storyHost = null!;
	private Guid _selectedId;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.RosterBoardChanged += Refresh;
		VisibilityChanged += OnVisibilityChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.RosterBoardChanged -= Refresh;
		}

		VisibilityChanged -= OnVisibilityChanged;
	}

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
		layout.AddThemeConstantOverride("separation", 6);
		margin.AddChild(layout);
		layout.AddChild(BuildHeader());

		var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 6);
		layout.AddChild(body);
		body.AddChild(BuildListCard());
		body.AddChild(BuildStoryCard());
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
		identity.AddThemeConstantOverride("separation", -4);
		identity.AddChild(MakeText($"ORGANIZATION  ·  {GameSession.SeasonYear} SEASON", _semibold, 12, Accent, HorizontalAlignment.Left));
		_title = MakeText("GRADUATION", _bold, 22, TextPrimary, HorizontalAlignment.Left);
		identity.AddChild(_title);
		_subtitle = MakeText("SENIORS LEAVING THE PROGRAM THIS SPRING", _medium, 11, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var meta = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		_countValue = MakeText("0 PLAYERS GRADUATING", _bold, 16, TextPrimary, HorizontalAlignment.Right);
		meta.AddChild(_countValue);
		header.AddChild(meta);
		return header;
	}

	private Control BuildListCard()
	{
		var card = MakeCard(true);
		card.SizeFlagsStretchRatio = 0.95f;
		card.ClipContents = true;
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);
		layout.AddChild(MakeSectionBar("THE CLASS"));
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
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

	private Control BuildStoryCard()
	{
		var card = MakeCard(true);
		card.SizeFlagsStretchRatio = 1.35f;
		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		card.AddChild(scroll);
		_storyHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_storyHost.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(PadBody(_storyHost));
		return card;
	}

	private Control MakeSectionBar(string label)
	{
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			ContentMarginLeft = 12,
			ContentMarginTop = 7,
			ContentMarginRight = 12,
			ContentMarginBottom = 7,
		});
		header.AddChild(MakeText(label, _semibold, 10, TextMuted, HorizontalAlignment.Left));
		return header;
	}

	private void Refresh()
	{
		if (_rowsHost == null)
		{
			return;
		}

		GraduationSnapshot snapshot = GraduationDesk.For(_session);
		_title.Text = snapshot.Headline;
		_subtitle.Text = $"{_session.Organization.Name.ToUpperInvariant()}  ·  SENIORS LEAVING THIS SPRING";
		_countValue.Text = snapshot.CountLabel;

		if (snapshot.Count > 0 && (_selectedId == Guid.Empty || !Contains(snapshot.Players, _selectedId)))
		{
			_selectedId = snapshot.Players[0].Id;
		}

		foreach (Node child in _rowsHost.GetChildren())
		{
			child.QueueFree();
		}

		if (snapshot.Count == 0)
		{
			_rowsHost.AddChild(PadBody(
				MakeText("No seniors leave this spring.", _medium, 13, TextMuted, HorizontalAlignment.Left),
				expand: false));
		}
		else
		{
			for (int i = 0; i < snapshot.Players.Count; i++)
			{
				_rowsHost.AddChild(BuildRow(snapshot.Players[i], i % 2 == 0));
			}
		}

		RebuildStory(snapshot);
	}

	private static bool Contains(IReadOnlyList<GraduateRecord> players, Guid id)
	{
		foreach (GraduateRecord player in players)
		{
			if (player.Id == id)
			{
				return true;
			}
		}

		return false;
	}

	private Control BuildRow(GraduateRecord player, bool even)
	{
		bool selected = player.Id == _selectedId;
		var row = new PanelContainer { MouseDefaultCursorShape = CursorShape.PointingHand };
		row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = selected ? RowSelected : even ? RowEven : RowOdd,
			BorderColor = selected ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 10,
		});
		Guid id = player.Id;
		row.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				_selectedId = id;
				Refresh();
			}
		};

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", -2);
		var top = new HBoxContainer();
		var name = MakeText(player.FullName, _semibold, 15, TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		top.AddChild(name);
		top.AddChild(MakeText(player.Position, _medium, 12, TextMuted, HorizontalAlignment.Right));
		box.AddChild(top);
		box.AddChild(MakeText(player.Destination.School, _medium, 12, Accent, HorizontalAlignment.Left));
		row.AddChild(box);
		return row;
	}

	private void RebuildStory(GraduationSnapshot snapshot)
	{
		foreach (Node child in _storyHost.GetChildren())
		{
			child.QueueFree();
		}

		GraduateRecord? selected = null;
		foreach (GraduateRecord player in snapshot.Players)
		{
			if (player.Id == _selectedId)
			{
				selected = player;
				break;
			}
		}

		if (selected == null)
		{
			_storyHost.AddChild(MakeText("No seniors on the roster this year.", _medium, 13, TextMuted, HorizontalAlignment.Left));
			return;
		}

		// Senior send-off banner: school art, capped headshot, name.
		string orgId = _session.CatalogOrganizationId;
		var banner = new BroadcastPanel { CustomMinimumSize = new Vector2(0, 170) };
		banner.UseTeam(orgId);
		PersonPortrait? face = PlayerFaces.For(orgId, selected.FullName, 150, 170, level: TeamLevel.Varsity)
			?? PlayerFaces.For(orgId, selected.FullName, 150, 170, level: TeamLevel.JuniorVarsity);
		if (face != null)
		{
			face.Backdrop = false;
			face.AnchorLeft = 0;
			face.AnchorRight = 0.32f;
			face.AnchorTop = 0;
			face.AnchorBottom = 1;
			face.OffsetTop = 8;
			face.OffsetBottom = -3;
			face.CustomMinimumSize = Vector2.Zero;
			banner.AddChild(face);
		}

		var copy = new VBoxContainer
		{
			AnchorLeft = face != null ? 0.34f : 0f,
			AnchorRight = 1,
			AnchorTop = 0,
			AnchorBottom = 1,
			OffsetLeft = 16,
			OffsetBottom = -16,
			Alignment = BoxContainer.AlignmentMode.End,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		copy.AddThemeConstantOverride("separation", -4);
		copy.AddChild(MakeText($"CLASS OF {GameSession.SeasonYear}  ·  {selected.Position}  ·  {selected.YearsLabel}".ToUpperInvariant(), _semibold, 13, UiKit.Gold, HorizontalAlignment.Left));
		copy.AddChild(MakeText(selected.FullName.ToUpperInvariant(), _bold, 40, Colors.White, HorizontalAlignment.Left));
		banner.AddChild(copy);
		_storyHost.AddChild(banner);

		var farewell = MakeText(selected.Farewell, _medium, 15, new Color(0.82f, 0.84f, 0.88f), HorizontalAlignment.Left);
		farewell.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_storyHost.AddChild(farewell);

		_storyHost.AddChild(MakeHairline());
		_storyHost.AddChild(MakeText("CAREER", _semibold, 10, Accent, HorizontalAlignment.Left));
		_storyHost.AddChild(MakeText(selected.CareerLine, _bold, 22, TextPrimary, HorizontalAlignment.Left));
		_storyHost.AddChild(MakeText(selected.HonorsLine, _medium, 14, Green, HorizontalAlignment.Left));

		_storyHost.AddChild(MakeHairline());
		_storyHost.AddChild(MakeText("DESTINATION", _semibold, 10, Accent, HorizontalAlignment.Left));
		_storyHost.AddChild(MakeText(selected.Destination.School.ToUpperInvariant(), _bold, 22, TextPrimary, HorizontalAlignment.Left));
		_storyHost.AddChild(MakeText(selected.Destination.Path, _medium, 13, TextMuted, HorizontalAlignment.Left));
	}

	private ColorRect MakeHairline() =>
		new() { CustomMinimumSize = new Vector2(0, 1), Color = Hairline };

	private static MarginContainer PadBody(Control child, bool expand = true)
	{
		var pad = new MarginContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = expand ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
		};
		pad.AddThemeConstantOverride("margin_left", 14);
		pad.AddThemeConstantOverride("margin_top", 12);
		pad.AddThemeConstantOverride("margin_right", 14);
		pad.AddThemeConstantOverride("margin_bottom", 12);
		pad.AddChild(child);
		return pad;
	}

	private static Label MakeText(string text, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		var label = new Label { Text = text, HorizontalAlignment = align };
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
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
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
		});
		return card;
	}
}
