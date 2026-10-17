using System;
using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// Organization incoming class: players signed this offseason. The list
/// fills during Signing Week and locks after the deadline.
/// </summary>
public partial class IncomingClassPage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color RowSelected = new(0.956863f, 0.643137f, 0.109804f, 0.14f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;

	private Label _title = null!;
	private Label _subtitle = null!;
	private Label _statusChip = null!;
	private Label _countValue = null!;
	private VBoxContainer _rowsHost = null!;
	private VBoxContainer _dossierHost = null!;
	private Guid _selectedId;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.TryoutsChanged += Refresh;
		_session.RosterBoardChanged += Refresh;
		VisibilityChanged += OnVisibilityChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.TryoutsChanged -= Refresh;
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
		body.AddChild(BuildDossierCard());
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
		identity.AddChild(MakeText($"ORGANIZATION  ·  {GameSession.SeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		_title = MakeText("INCOMING CLASS", _bold, 22, TextPrimary, HorizontalAlignment.Left);
		identity.AddChild(_title);
		_subtitle = MakeText("SIGNED PLAYERS FOR THIS BASEBALL YEAR", _medium, 11, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var meta = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		meta.AddThemeConstantOverride("separation", 2);
		_statusChip = MakeText("SIGNING IN PROGRESS", _bold, 12, Accent, HorizontalAlignment.Right);
		meta.AddChild(_statusChip);
		_countValue = MakeText("0 NEW PLAYERS", _bold, 16, TextPrimary, HorizontalAlignment.Right);
		meta.AddChild(_countValue);
		header.AddChild(meta);
		return header;
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
		columns.AddChild(MakeFixed("POS", 40, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("YEAR", 40, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("OVR", 56, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("POT", 56, TextMuted, _semibold, 10));
		columns.AddChild(MakeFixed("SQUAD", 70, TextMuted, _semibold, 10));
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
		_dossierHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_dossierHost.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(PadBody(_dossierHost));
		return card;
	}

	private void Refresh()
	{
		if (_rowsHost == null)
		{
			return;
		}

		IncomingClassSnapshot snapshot = IncomingClassDesk.For(_session);
		_title.Text = snapshot.Headline;
		_subtitle.Text = snapshot.Finalized
			? $"{_session.Organization.Name.ToUpperInvariant()}  ·  CLASS IS LOCKED"
			: $"{_session.Organization.Name.ToUpperInvariant()}  ·  FILLS DURING SIGNING WEEK";
		_statusChip.Text = snapshot.StatusLabel;
		_statusChip.AddThemeColorOverride("font_color", snapshot.Finalized ? Green : Accent);
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
			_rowsHost.AddChild(MakeEmptyState(snapshot.Finalized));
		}
		else
		{
			for (int i = 0; i < snapshot.Players.Count; i++)
			{
				_rowsHost.AddChild(BuildRow(snapshot.Players[i], i % 2 == 0));
			}
		}

		RebuildDossier(snapshot);
	}

	private static bool Contains(IReadOnlyList<IncomingClassPlayer> players, Guid id)
	{
		foreach (IncomingClassPlayer player in players)
		{
			if (player.Id == id)
			{
				return true;
			}
		}

		return false;
	}

	private Control MakeEmptyState(bool finalized)
	{
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		box.AddChild(MakeText(
			finalized ? "No incoming class this year." : "No one has signed yet.",
			_semibold,
			16,
			TextPrimary,
			HorizontalAlignment.Left));
		box.AddChild(MakeText(
			finalized
				? "This program did not add players in Signing Week. The class stays empty until next offseason."
				: "Players you sign in District Open Play / Signing Week appear here. The class finalizes after the February 1 deadline.",
			_medium,
			13,
			TextMuted,
			HorizontalAlignment.Left));
		return PadBody(box, expand: false);
	}

	private Control BuildRow(IncomingClassPlayer player, bool even)
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
				Refresh();
			}
		};

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 10);
		var name = MakeText(player.FullName, _semibold, 14, TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(name);
		columns.AddChild(MakeFixed(player.Position, 40, TextMuted));
		columns.AddChild(MakeFixed(player.Year, 40, TextMuted));
		columns.AddChild(MakeFixed(player.OverallText, 56, Accent, _bold));
		columns.AddChild(MakeFixed(player.PotentialText, 56, Accent, _bold));
		columns.AddChild(MakeFixed(player.Squad.ToUpperInvariant(), 70, TextMuted, _semibold, 11));
		row.AddChild(columns);
		return row;
	}

	private void RebuildDossier(IncomingClassSnapshot snapshot)
	{
		foreach (Node child in _dossierHost.GetChildren())
		{
			child.QueueFree();
		}

		IncomingClassPlayer? selected = null;
		foreach (IncomingClassPlayer player in snapshot.Players)
		{
			if (player.Id == _selectedId)
			{
				selected = player;
				break;
			}
		}

		if (selected == null)
		{
			_dossierHost.AddChild(MakeText(
				snapshot.Finalized ? "The class is empty this year." : "Sign a player in Open Play to start the class.",
				_medium,
				13,
				TextMuted,
				HorizontalAlignment.Left));
			return;
		}

		Player? prospect = _session.FindProspect(selected.Id);
		if (prospect != null)
		{
			PlayerAgreement? agreement = _session.RecruitingMarket.AgreementFor(selected.Id);
			PlayerAgreementSnapshot? deal = null;
			if (agreement != null)
			{
				deal = new PlayerAgreementSnapshot(
					_session.Organization.Name,
					agreement.StartSeasonYear,
					agreement.YearsRemaining,
					agreement.TermYears,
					agreement.AnnualSponsorship,
					agreement.TotalCommitment);
			}

			var profile = new PlayerProfileCard
			{
				FrameVisible = false,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			_dossierHost.AddChild(profile);
			profile.Bind(PlayerProfiles.FromOpenPlay(prospect, _session.OpenPlay, _session.CurrentDate, _session.Organization.ShortName, deal));
			return;
		}

		_dossierHost.AddChild(MakeText(selected.FullName.ToUpperInvariant(), _bold, 22, TextPrimary, HorizontalAlignment.Left));
		_dossierHost.AddChild(MakeText(
			$"{selected.Position}  ·  {selected.Year}  ·  {selected.Squad}",
			_medium,
			13,
			TextMuted,
			HorizontalAlignment.Left));
		_dossierHost.AddChild(MakeText(
			$"OVR {selected.OverallText}  ·  POT {selected.PotentialText}",
			_bold,
			18,
			Accent,
			HorizontalAlignment.Left));
	}

	private Label MakeFixed(string text, float width, Color color, FontFile? font = null, int size = 13)
	{
		Label label = MakeText(text, font ?? _medium, size, color, HorizontalAlignment.Center);
		label.CustomMinimumSize = new Vector2(width, 0);
		return label;
	}

	private static Label MakeText(string text, FontFile font, int size, Color color, HorizontalAlignment align)
	{
		var label = new Label { Text = text, HorizontalAlignment = align };
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

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
}
