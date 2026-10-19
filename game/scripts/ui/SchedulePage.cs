using System.Collections.Generic;
using System.Globalization;
using Godot;
using HSBM.Domain;

/// <summary>
/// Full-season results table for the active squad. Calendar stays the
/// month desk; this page is the baseball log: date, opponent, result, record.
/// </summary>
public partial class SchedulePage : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.772549f, 0.635294f, 0.325490f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color CardBorder = new(0.137255f, 0.152941f, 0.200000f, 1f);
	private static readonly Color Hairline = new(0.17f, 0.19f, 0.24f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color RowNext = new(0.772549f, 0.635294f, 0.325490f, 0.14f);
	private const float DateWidth = 88f;
	private const float ResultWidth = 96f;
	private const float RecordWidth = 72f;

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;

	private Label _subtitle = null!;
	private Label _countValue = null!;
	private Label _countLabel = null!;
	private VBoxContainer _rowsHost = null!;
	private ScrollContainer _scroll = null!;
	private Control? _focusRow;
	private BoxScoreOverlay _boxScoreOverlay = null!;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_boxScoreOverlay = new BoxScoreOverlay();
		AddChild(_boxScoreOverlay);
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		_session.CareerChanged += OnCareerChanged;
		VisibilityChanged += OnVisibilityChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
			_session.CareerChanged -= OnCareerChanged;
		}

		VisibilityChanged -= OnVisibilityChanged;
	}

	public void FocusCurrentGame()
	{
		Refresh();
		Callable.From(ScrollToFocus).CallDeferred();
	}

	private void ScrollToFocus()
	{
		if (_focusRow != null && GodotObject.IsInstanceValid(_focusRow))
		{
			_scroll.EnsureControlVisible(_focusRow);
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => Refresh();

	private void OnCareerChanged() => Refresh();

	private void OnVisibilityChanged()
	{
		if (IsVisibleInTree() && _rowsHost != null)
		{
			Refresh();
		}
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
		layout.AddChild(SeasonDeskTabs.Build(_semibold, SeasonDeskTabs.Schedule, OnDeskTabPicked));
		layout.AddChild(BuildListCard());
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
		identity.AddChild(MakeText($"HOME  ·  {GameSession.SeasonYear} SEASON", _semibold, 12, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("SEASON SCHEDULE", _bold, 36, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("VARSITY  ·  GREAT LAKES DISTRICT", _medium, 12, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);

		var countBlock = new VBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			Alignment = BoxContainer.AlignmentMode.End,
		};
		countBlock.AddThemeConstantOverride("separation", -4);
		_countValue = MakeText("0\u20130", _bold, 28, TextPrimary, HorizontalAlignment.Right);
		countBlock.AddChild(_countValue);
		_countLabel = MakeText("RECORD", _semibold, 11, TextMuted, HorizontalAlignment.Right);
		countBlock.AddChild(_countLabel);
		header.AddChild(countBlock);
		return header;
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

		_scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
			FollowFocus = true,
		};
		layout.AddChild(_scroll);

		_rowsHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkBegin,
		};
		_rowsHost.AddThemeConstantOverride("separation", 0);
		_scroll.AddChild(_rowsHost);
		return card;
	}

	private Control BuildColumnHeader()
	{
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			BorderColor = CardBorder,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 7,
			ContentMarginRight = 12,
			ContentMarginBottom = 7,
		});

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 12);
		columns.AddChild(MakeHeaderCell("DATE", DateWidth));
		var opponent = MakeText("OPPONENT", _semibold, 10, TextMuted, HorizontalAlignment.Left);
		opponent.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.AddChild(opponent);
		columns.AddChild(MakeHeaderCell("RESULT", ResultWidth, HorizontalAlignment.Right));
		columns.AddChild(MakeHeaderCell("RECORD", RecordWidth, HorizontalAlignment.Right));
		header.AddChild(columns);
		return header;
	}

	private Label MakeHeaderCell(string text, float width, HorizontalAlignment align = HorizontalAlignment.Left)
	{
		Label label = MakeText(text, _semibold, 10, TextMuted, align);
		label.CustomMinimumSize = new Vector2(width, 0);
		return label;
	}

	private void Refresh()
	{
		ApplyHeaderCopy();
		RebuildRows();
	}

	private void ApplyHeaderCopy()
	{
		bool varsity = _session.ActiveTeam.Level == TeamLevel.Varsity;
		string level = varsity ? "VARSITY" : "JV";
		_subtitle.Text = $"{level}  ·  {_session.Organization.Name.ToUpperInvariant()}";
	}

	private void RebuildRows()
	{
		foreach (Node child in _rowsHost.GetChildren())
		{
			_rowsHost.RemoveChild(child);
			child.QueueFree();
		}

		_focusRow = null;
		List<ScheduleRow> rows = BuildRows(_session.ActiveTeam.Level);
		_countValue.Text = rows.Count == 0 ? "0\u20130" : FinalRecord(rows);
		_countLabel.Text = "RECORD";

		if (rows.Count == 0)
		{
			var empty = MakeText("No games on the board for this squad.", _medium, 13, TextMuted, HorizontalAlignment.Center);
			empty.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			_rowsHost.AddChild(empty);
			return;
		}

		int played = 0;
		int remaining = 0;
		foreach (ScheduleRow row in rows)
		{
			if (row.Game.Completed && !row.Game.Exhibition)
			{
				played++;
			}
			else if (!row.Game.Completed)
			{
				remaining++;
			}
		}

		_subtitle.Text =
			$"{(_session.ActiveTeam.Level == TeamLevel.Varsity ? "VARSITY" : "JV")}  ·  {_session.Organization.Name.ToUpperInvariant()}  ·  {played} PLAYED  ·  {remaining} REMAINING";

		for (int i = 0; i < rows.Count; i++)
		{
			Control visual = BuildRow(rows[i], i);
			_rowsHost.AddChild(visual);
			if (rows[i].IsFocus)
			{
				_focusRow = visual;
			}
		}
	}

	private List<ScheduleRow> BuildRows(TeamLevel level)
	{
		var rows = new List<ScheduleRow>();
		int wins = 0;
		int losses = 0;
		int ties = 0;
		bool focused = false;
		foreach (CalendarContest game in _session.SeasonContests(level))
		{
			if (game.Level != level || game.IsTryout)
			{
				continue;
			}

			string record = string.Empty;
			if (game.Completed && !game.Exhibition)
			{
				if (game.Won)
				{
					wins++;
				}
				else if (game.TeamScore == game.OpponentScore)
				{
					ties++;
				}
				else
				{
					losses++;
				}

				record = FormatRecord(wins, losses, ties);
			}

			bool isNext = !focused && !game.Completed && game.Date >= _session.CurrentDate;
			if (isNext)
			{
				focused = true;
			}

			rows.Add(new ScheduleRow(game, record, isNext));
		}

		if (!focused)
		{
			for (int i = rows.Count - 1; i >= 0; i--)
			{
				if (rows[i].Game.Date <= _session.CurrentDate)
				{
					rows[i] = rows[i] with { IsFocus = true };
					break;
				}
			}
		}

		return rows;
	}

	private static string FinalRecord(List<ScheduleRow> rows)
	{
		for (int i = rows.Count - 1; i >= 0; i--)
		{
			if (rows[i].Record.Length > 0)
			{
				return rows[i].Record;
			}
		}

		return "0\u20130";
	}

	private static string FormatRecord(int wins, int losses, int ties) =>
		ties > 0 ? $"{wins}\u2013{losses}\u2013{ties}" : $"{wins}\u2013{losses}";

	private Control BuildRow(ScheduleRow entry, int index)
	{
		CalendarContest game = entry.Game;
		HubTeam opponent = DistrictHubData.GetTeam(game.OpponentId);
		bool past = game.Date < _session.CurrentDate && !game.Completed;
		Color rowBg = entry.IsFocus ? RowNext : index % 2 == 0 ? RowEven : RowOdd;

		var row = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = game.Completed ? "View the box score" : "Open game preview",
		};
		row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = rowBg,
			BorderColor = entry.IsFocus ? Accent : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = entry.IsFocus ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 10,
		});
		if (past)
		{
			row.Modulate = new Color(1f, 1f, 1f, 0.62f);
		}

		CalendarContest captured = game;
		row.GuiInput += ev => OnRowInput(ev, captured);

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 12);
		row.AddChild(columns);

		var dateCol = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(DateWidth, 0),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		dateCol.AddThemeConstantOverride("separation", -2);
		dateCol.AddChild(MakeText(
			game.Date.ToString("MMM d", CultureInfo.InvariantCulture).ToUpperInvariant(),
			_bold,
			14,
			entry.IsFocus ? Accent : TextPrimary,
			HorizontalAlignment.Left));
		dateCol.AddChild(MakeText(game.Time, _medium, 11, TextMuted, HorizontalAlignment.Left));
		columns.AddChild(dateCol);

		columns.AddChild(BuildOpponent(game, opponent, entry.IsFocus));

		string result = FormatResult(game);
		var resultLabel = MakeText(result, _bold, 14, ResultColor(game), HorizontalAlignment.Right);
		resultLabel.CustomMinimumSize = new Vector2(ResultWidth, 0);
		resultLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		columns.AddChild(resultLabel);

		var recordLabel = MakeText(
			entry.Record.Length > 0 ? entry.Record : "\u2014",
			_semibold,
			14,
			entry.Record.Length > 0 ? TextPrimary : TextMuted,
			HorizontalAlignment.Right);
		recordLabel.CustomMinimumSize = new Vector2(RecordWidth, 0);
		recordLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		columns.AddChild(recordLabel);
		return row;
	}

	private Control BuildOpponent(CalendarContest game, HubTeam opponent, bool focus)
	{
		var identity = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = $"Open {opponent.ShortName} profile",
		};
		identity.AddThemeConstantOverride("separation", 8);
		TextureRect? logo = TeamLogos.TryMakeIcon(game.OpponentId, 28);
		if (logo != null)
		{
			identity.AddChild(logo);
		}

		var names = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		names.AddThemeConstantOverride("separation", -2);
		string site = game.Home ? "VS" : "@";
		names.AddChild(MakeText(
			$"{site}  {opponent.ShortName.ToUpperInvariant()}",
			_semibold,
			14,
			focus ? Accent : TextPrimary,
			HorizontalAlignment.Left));
		string kind = game.Exhibition ? "EXHIBITION" : game.VenueLabel;
		names.AddChild(MakeText(kind, _medium, 10, TextMuted, HorizontalAlignment.Left));
		identity.AddChild(names);
		SchoolLinks.MakeClickable(identity, game.OpponentId);
		return identity;
	}

	private void OnRowInput(InputEvent ev, CalendarContest game)
	{
		if (ev is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
		{
			return;
		}

		if (game.Completed)
		{
			_boxScoreOverlay.Open(ClubhouseBoxScore.For(game));
		}
		else if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowGamePreview(game);
		}

		AcceptEvent();
	}

	private void OnDeskTabPicked(string key)
	{
		if (key != SeasonDeskTabs.Calendar)
		{
			return;
		}

		if (GetTree().CurrentScene is AppNavigator navigator)
		{
			navigator.ShowCalendar();
		}
	}

	private static string FormatResult(CalendarContest game)
	{
		if (!game.Completed)
		{
			return game.Exhibition ? "EXH" : "\u2014";
		}

		string mark = game.Won ? "W" : game.TeamScore == game.OpponentScore ? "T" : "L";
		string score = $"{game.TeamScore}\u2013{game.OpponentScore}";
		return game.Exhibition ? $"EXH {mark} {score}" : $"{mark} {score}";
	}

	private static Color ResultColor(CalendarContest game)
	{
		if (!game.Completed)
		{
			return TextMuted;
		}

		if (game.Won)
		{
			return Green;
		}

		return game.TeamScore == game.OpponentScore ? Accent : Urgent;
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

	private static StyleBoxFlat MakeCardStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.070588f, 0.082353f, 0.109804f, 1f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 0,
			ContentMarginTop = 0,
			ContentMarginRight = 0,
			ContentMarginBottom = 0,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerDetail = 3,
		};
	}

	private sealed record ScheduleRow(CalendarContest Game, string Record, bool IsFocus);
}
