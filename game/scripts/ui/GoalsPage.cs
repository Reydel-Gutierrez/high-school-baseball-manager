using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// Organization manager contract board: deal year, board satisfaction,
/// derived job security, required goals, bonus subgoals, and prior
/// seasons at this program once the first year is complete.
/// </summary>
public partial class GoalsPage : Control
{
	private const int CurrentSeasonYear = 2026;

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color FairOrange = new(0.95f, 0.55f, 0.22f, 1f);
	private static readonly Color GoodLime = new(0.55f, 0.82f, 0.32f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);

	private static readonly BoardSatisfaction[] SatisfactionScale =
	[
		BoardSatisfaction.Poor,
		BoardSatisfaction.Fair,
		BoardSatisfaction.Average,
		BoardSatisfaction.Good,
		BoardSatisfaction.Excellent,
	];

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _cascadeLogo = null!;

	private Label _subtitle = null!;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_cascadeLogo = TeamLogos.Load(TeamLogos.CascadeId)!;
		_session = GetNode<GameSession>("/root/GameSession");

		BuildPage();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		ApplyActiveTeam();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => ApplyActiveTeam();

	private void ApplyActiveTeam()
	{
		_subtitle.Text = $"{_session.Organization.Name.ToUpperInvariant()}  ·  PROGRAM MANAGER";
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
		layout.AddChild(BuildStandingRow());

		var body = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 6);
		layout.AddChild(body);

		Control mainCard = BuildGoalCard(
			"MAIN GOALS",
			"Required under the current contract.",
			BuildMainGoals(),
			true);
		Control subCard = BuildGoalCard(
			"SUBGOALS",
			"Bonus objectives that move board satisfaction up or down.",
			BuildSubGoals(),
			false);

		if (_session.ShowContractHistory)
		{
			var goals = new VBoxContainer
			{
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SizeFlagsVertical = SizeFlags.ExpandFill,
				SizeFlagsStretchRatio = 1.2f,
			};
			goals.AddThemeConstantOverride("separation", 6);
			mainCard.SizeFlagsVertical = SizeFlags.ShrinkBegin;
			goals.AddChild(mainCard);
			goals.AddChild(subCard);
			body.AddChild(goals);
			body.AddChild(BuildHistoryCard());
		}
		else
		{
			mainCard.SizeFlagsStretchRatio = 1f;
			subCard.SizeFlagsStretchRatio = 1.15f;
			body.AddChild(mainCard);
			body.AddChild(subCard);
		}
	}

	private Control BuildHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);

		header.AddChild(TeamLogos.MakeHeaderMark(_cascadeLogo));
		header.AddChild(TeamLogos.MakeHeaderRule(Hairline));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -4);
		identity.AddChild(MakeText($"ORGANIZATION  ·  {CurrentSeasonYear} SEASON", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("GOALS & SATISFACTION", _bold, 22, TextPrimary, HorizontalAlignment.Left));
		_subtitle = MakeText("CASCADE REGIONAL HIGH SCHOOL  ·  PROGRAM MANAGER", _medium, 11, new Color(0.72f, 0.76f, 0.82f), HorizontalAlignment.Left);
		identity.AddChild(_subtitle);
		header.AddChild(identity);
		return header;
	}

	private Control BuildStandingRow()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 6);
		row.AddChild(BuildContractYearCard());
		row.AddChild(BuildSatisfactionCard());
		row.AddChild(BuildJobSecurityCard());
		return row;
	}

	private Control BuildContractYearCard()
	{
		var card = MakeCard(false);
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		card.SizeFlagsStretchRatio = 0.7f;

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 3);
		card.AddChild(layout);

		layout.AddChild(MakeText("CONTRACT YEAR", _semibold, 10, Accent, HorizontalAlignment.Left));
		layout.AddChild(MakeAccentLine());

		var valueRow = new HBoxContainer();
		valueRow.AddThemeConstantOverride("separation", 4);
		valueRow.AddChild(MakeText($"{_session.ContractYear}", _bold, 30, TextPrimary, HorizontalAlignment.Left));
		valueRow.AddChild(MakeText("/", _bold, 22, TextMuted, HorizontalAlignment.Left));
		valueRow.AddChild(MakeText($"{_session.ContractLength}", _bold, 30, Accent, HorizontalAlignment.Left));
		layout.AddChild(valueRow);
		layout.AddChild(MakeText(ContractYearCopy(), _medium, 11, TextMuted, HorizontalAlignment.Left));
		return card;
	}

	private Control BuildSatisfactionCard()
	{
		BoardSatisfaction current = _session.BoardSatisfaction;
		var card = MakeCard(false);
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		card.SizeFlagsStretchRatio = 1.55f;

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 4);
		card.AddChild(layout);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		header.AddChild(MakeText("BOARD SATISFACTION", _semibold, 10, Accent, HorizontalAlignment.Left));
		var rating = MakeText(current.ToLabel().ToUpperInvariant(), _bold, 16, SatisfactionColor(current), HorizontalAlignment.Right);
		rating.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(rating);
		layout.AddChild(header);
		layout.AddChild(MakeAccentLine());

		var scale = new HBoxContainer();
		scale.AddThemeConstantOverride("separation", 4);
		foreach (BoardSatisfaction step in SatisfactionScale)
		{
			scale.AddChild(BuildSatisfactionPill(step, current));
		}

		layout.AddChild(scale);
		layout.AddChild(MakeText(SatisfactionCopy(current), _medium, 11, TextMuted, HorizontalAlignment.Left));
		return card;
	}

	private Control BuildSatisfactionPill(BoardSatisfaction rating, BoardSatisfaction current)
	{
		bool active = rating == current;
		Color color = SatisfactionColor(rating);
		var pill = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		pill.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = active ? new Color(color.R, color.G, color.B, 0.18f) : CardInner,
			BorderColor = active ? color : Hairline,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 4,
			ContentMarginTop = 4,
			ContentMarginRight = 4,
			ContentMarginBottom = 4,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		});
		pill.AddChild(MakeText(
			rating.ToLabel().ToUpperInvariant(),
			_semibold,
			10,
			active ? color : TextMuted,
			HorizontalAlignment.Center));
		return pill;
	}

	private Control BuildJobSecurityCard()
	{
		JobSecurity security = _session.JobSecurity;
		var card = MakeCard(false);
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		card.SizeFlagsStretchRatio = 0.85f;

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 3);
		card.AddChild(layout);

		layout.AddChild(MakeText("JOB SECURITY", _semibold, 10, Accent, HorizontalAlignment.Left));
		layout.AddChild(MakeAccentLine());
		layout.AddChild(MakeText(security.ToLabel().ToUpperInvariant(), _bold, 26, SecurityColor(security), HorizontalAlignment.Left));
		layout.AddChild(MakeText(SecurityCopy(security), _medium, 11, TextMuted, HorizontalAlignment.Left));
		return card;
	}

	private Control BuildGoalCard(string title, string hint, IReadOnlyList<ContractGoal> goals, bool main)
	{
		var card = MakeCard(true);
		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 4);
		card.AddChild(layout);

		layout.AddChild(MakeText(title, _semibold, 11, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(MakeText(hint, _medium, 10, TextMuted, HorizontalAlignment.Left));
		layout.AddChild(MakeAccentLine());

		var list = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		list.AddThemeConstantOverride("separation", main ? 5 : 4);
		layout.AddChild(list);

		for (int i = 0; i < goals.Count; i++)
		{
			Control row = BuildGoalRow(goals[i], i, main);
			row.SizeFlagsVertical = SizeFlags.ExpandFill;
			list.AddChild(row);
		}

		return card;
	}

	private Control BuildGoalRow(ContractGoal goal, int index, bool main)
	{
		var row = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		row.AddThemeStyleboxOverride("panel", MakeInnerCard(main ? 10 : 8, main ? 6 : 4, index % 2 == 0 ? RowEven : RowOdd, main));

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 8);
		row.AddChild(columns);

		columns.AddChild(BuildIndexBadge(index + 1, main));

		var copy = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		copy.AddThemeConstantOverride("separation", -2);
		copy.AddChild(MakeText(goal.Title, _semibold, main ? 15 : 13, TextPrimary, HorizontalAlignment.Left));
		var detail = MakeText(goal.Detail, _medium, 10, TextMuted, HorizontalAlignment.Left);
		detail.AutowrapMode = main ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
		detail.ClipText = !main;
		detail.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		copy.AddChild(detail);
		columns.AddChild(copy);

		columns.AddChild(MakeStatusPill(goal.Status));
		return row;
	}

	private Control BuildIndexBadge(int number, bool main)
	{
		int size = main ? 24 : 20;
		var badge = new PanelContainer
		{
			CustomMinimumSize = new Vector2(size, size),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		badge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = main ? Accent : CardInner,
			BorderColor = Accent,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		});
		var label = MakeText($"{number}", _bold, main ? 13 : 11, main ? TextOnAccent : Accent, HorizontalAlignment.Center);
		label.VerticalAlignment = VerticalAlignment.Center;
		badge.AddChild(label);
		return badge;
	}

	private Control MakeStatusPill(GoalStatus status)
	{
		Color color = status switch
		{
			GoalStatus.Met => Green,
			GoalStatus.Missed => Urgent,
			_ => Accent,
		};
		var pill = new PanelContainer
		{
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		pill.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(color.R, color.G, color.B, 0.16f),
			BorderColor = color,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 6,
			ContentMarginTop = 2,
			ContentMarginRight = 6,
			ContentMarginBottom = 2,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		});
		pill.AddChild(MakeText(StatusLabel(status), _semibold, 9, color, HorizontalAlignment.Center));
		return pill;
	}

	private Control BuildHistoryCard()
	{
		var card = MakeCard(true);
		card.SizeFlagsStretchRatio = 1f;

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 4);
		card.AddChild(layout);

		layout.AddChild(MakeText("CONTRACT HISTORY", _semibold, 11, TextPrimary, HorizontalAlignment.Left));
		layout.AddChild(MakeText("Previous seasons as manager at this organization.", _medium, 10, TextMuted, HorizontalAlignment.Left));
		layout.AddChild(MakeAccentLine());
		layout.AddChild(BuildHistoryHeader());

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);

		var rows = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		rows.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(rows);

		IReadOnlyList<SeasonHistory> seasons = BuildSeasonHistory();
		for (int i = 0; i < seasons.Count; i++)
		{
			rows.AddChild(BuildHistoryRow(seasons[i], i % 2 == 0));
		}

		return card;
	}

	private Control BuildHistoryHeader()
	{
		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047059f, 0.054902f, 0.070588f, 0.96f),
			BorderColor = CardBorder,
			BorderWidthBottom = 1,
			ContentMarginLeft = 8,
			ContentMarginTop = 5,
			ContentMarginRight = 8,
			ContentMarginBottom = 5,
		});

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 6);
		columns.AddChild(MakeHeaderCell("YEAR", 44, HorizontalAlignment.Left));
		columns.AddChild(MakeHeaderCell("RECORD", 56, HorizontalAlignment.Left));
		columns.AddChild(MakeHeaderCell("DISTRICT", 0, HorizontalAlignment.Center, true));
		columns.AddChild(MakeHeaderCell("REGIONAL", 0, HorizontalAlignment.Center, true));
		columns.AddChild(MakeHeaderCell("STATE", 0, HorizontalAlignment.Center, true));
		header.AddChild(columns);
		return header;
	}

	private Label MakeHeaderCell(string text, float width, HorizontalAlignment align, bool expand = false)
	{
		Label label = MakeText(text, _semibold, 10, TextMuted, align);
		if (expand)
		{
			label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		}
		else
		{
			label.CustomMinimumSize = new Vector2(width, 0);
		}

		return label;
	}

	private Control BuildHistoryRow(SeasonHistory season, bool even)
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
			ContentMarginLeft = 8,
			ContentMarginTop = 7,
			ContentMarginRight = 8,
			ContentMarginBottom = 7,
		});

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 6);

		Label year = MakeText($"{season.Year}", _semibold, 13, TextPrimary, HorizontalAlignment.Left);
		year.CustomMinimumSize = new Vector2(44, 0);
		columns.AddChild(year);

		Label record = MakeText(season.Record, _bold, 13, TextPrimary, HorizontalAlignment.Left);
		record.CustomMinimumSize = new Vector2(56, 0);
		columns.AddChild(record);

		columns.AddChild(MakeYesNoCell(season.DistrictChampion));
		columns.AddChild(MakeYesNoCell(season.RegionalChampion));
		columns.AddChild(MakeYesNoCell(season.StateChampion));
		row.AddChild(columns);
		return row;
	}

	private Control MakeYesNoCell(bool yes)
	{
		var wrap = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		wrap.AddChild(MakeText(yes ? "Yes" : "No", _semibold, 12, yes ? Green : TextMuted, HorizontalAlignment.Center));
		return wrap;
	}

	private string ContractYearCopy()
	{
		int year = _session.ContractYear;
		int length = _session.ContractLength;
		if (year <= 1)
		{
			return $"First year of a {length}-year agreement.";
		}

		if (year >= length)
		{
			return "Final year of this deal.";
		}

		return $"{length - year} seasons remaining after this one.";
	}

	private static PanelContainer MakeCard(bool expand)
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = expand ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCardStyle());
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
			ContentMarginLeft = 10,
			ContentMarginTop = 8,
			ContentMarginRight = 10,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		};
	}

	private static StyleBoxFlat MakeInnerCard(float padX, float padY, Color bg, bool accentEdge)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			BorderColor = accentEdge ? Accent : Hairline,
			BorderWidthLeft = accentEdge ? 2 : 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = padX,
			ContentMarginTop = padY,
			ContentMarginRight = padX,
			ContentMarginBottom = padY,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 3,
		};
	}

	private static Color SatisfactionColor(BoardSatisfaction satisfaction) => satisfaction switch
	{
		BoardSatisfaction.Poor => Urgent,
		BoardSatisfaction.Fair => FairOrange,
		BoardSatisfaction.Good => GoodLime,
		BoardSatisfaction.Excellent => Green,
		_ => Accent,
	};

	private static Color SecurityColor(JobSecurity security) => security switch
	{
		JobSecurity.Secured => Green,
		JobSecurity.AtRisk => Urgent,
		_ => Accent,
	};

	private static string SatisfactionCopy(BoardSatisfaction satisfaction) => satisfaction switch
	{
		BoardSatisfaction.Poor => "The board is unhappy. Missing main goals will put the seat in play.",
		BoardSatisfaction.Fair => "The board wants a turnaround. Meeting main goals would stabilize standing.",
		BoardSatisfaction.Good => "The board likes the direction. Keep hitting goals to lock this in.",
		BoardSatisfaction.Excellent => "The board is fully behind this staff after the goals that have been met.",
		_ => "The board is in the middle. Goals met will raise this; misses will drop it.",
	};

	private static string SecurityCopy(JobSecurity security) => security switch
	{
		JobSecurity.Secured => "Tracks board satisfaction. The seat is safe for now.",
		JobSecurity.AtRisk => "Tracks board satisfaction. A weak finish could end this deal.",
		_ => "Tracks board satisfaction. Stay at Average or better to keep the seat.",
	};

	private static string StatusLabel(GoalStatus status) => status switch
	{
		GoalStatus.Met => "MET",
		GoalStatus.Missed => "MISSED",
		_ => "IN PROGRESS",
	};

	private static List<ContractGoal> BuildMainGoals() =>
	[
		new("Finish with a .500 record", "Post a varsity record of .500 or better by the end of the regular season.", GoalStatus.InProgress),
		new("Qualify for the Regional Playoff", "Classify out of district play and earn a regional tournament berth.", GoalStatus.InProgress),
	];

	private static List<ContractGoal> BuildSubGoals() =>
	[
		new("Win the District Championship", "Finish first in district play and take the title.", GoalStatus.InProgress),
		new("Produce 3 college-committed seniors", "Help at least three seniors sign with college programs.", GoalStatus.InProgress),
		new("Keep varsity ERA under 3.50", "Hold the staff earned-run average below 3.50 for the season.", GoalStatus.InProgress),
		new("Finish top 3 in district standings", "End district play no lower than third place.", GoalStatus.InProgress),
		new("Post a winning JV season", "Finish the junior varsity schedule above .500.", GoalStatus.InProgress),
	];

	private static List<SeasonHistory> BuildSeasonHistory() =>
	[
		new(2025, "19-11", true, false, false),
		new(2024, "15-15", false, false, false),
		new(2023, "22-8", true, true, false),
		new(2022, "13-17", false, false, false),
	];

	private enum GoalStatus
	{
		InProgress,
		Met,
		Missed,
	}

	private sealed record ContractGoal(string Title, string Detail, GoalStatus Status);

	private sealed record SeasonHistory(
		int Year,
		string Record,
		bool DistrictChampion,
		bool RegionalChampion,
		bool StateChampion);
}
