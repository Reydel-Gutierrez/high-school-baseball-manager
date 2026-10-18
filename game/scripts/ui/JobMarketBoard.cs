using System;
using System.Collections.Generic;
using Godot;
using HSBM.Application;
using HSBM.Domain;

/// <summary>
/// The coach's office job market: posted seats at first license and
/// during the offseason carousel. APPLY takes the chair when reputation
/// reaches the program.
/// </summary>
public partial class JobMarketBoard : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color RowEven = new(0.055f, 0.062f, 0.078f, 0.55f);
	private static readonly Color RowOdd = new(0.070588f, 0.078431f, 0.094118f, 0.35f);
	private static readonly Color Gold = new(0.92f, 0.74f, 0.28f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private LineEdit _search = null!;
	private VBoxContainer _rowsHost = null!;
	private VBoxContainer _detailHost = null!;
	private string? _selectedId;
	private string _query = "";

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_session = GetNode<GameSession>("/root/GameSession");
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ExpandFill;
		Build();
		_session.CareerChanged += Refresh;
		VisibilityChanged += OnVisibilityChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.CareerChanged -= Refresh;
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

	public void Refresh()
	{
		if (_rowsHost == null)
		{
			return;
		}

		IReadOnlyList<JobListing> jobs = VisibleJobs();
		if (jobs.Count > 0 && (string.IsNullOrEmpty(_selectedId) || FindJob(_selectedId) == null))
		{
			_selectedId = jobs[0].OrganizationId;
		}
		else if (jobs.Count == 0)
		{
			_selectedId = null;
		}

		RebuildRows();
		BindDetail(FindJob(_selectedId ?? ""));
	}

	private void Build()
	{
		var body = new HBoxContainer();
		body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		body.SizeFlagsVertical = SizeFlags.ExpandFill;
		body.AddThemeConstantOverride("separation", 8);
		AddChild(body);
		body.AddChild(BuildTable());
		body.AddChild(BuildDetailCard());
	}

	private Control BuildTable()
	{
		var card = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.45f,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCard(0));

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 0);
		card.AddChild(layout);

		var heading = new PanelContainer();
		heading.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047f, 0.04f, 0.035f, 0.96f),
			ContentMarginLeft = 14,
			ContentMarginTop = 10,
			ContentMarginRight = 14,
			ContentMarginBottom = 10,
		});
		var titles = new VBoxContainer();
		titles.AddThemeConstantOverride("separation", -2);
		titles.AddChild(MakeText("COACHING JOBS", _bold, 18, TextPrimary, HorizontalAlignment.Left));
		titles.AddChild(MakeText(
			"Posted seats this offseason. Athletic directors only call if your reputation reaches the program.",
			_medium,
			12,
			TextMuted,
			HorizontalAlignment.Left));
		heading.AddChild(titles);
		layout.AddChild(heading);
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Gold });
		layout.AddChild(BuildSearch());
		layout.AddChild(BuildHeaderRow());

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);
		_rowsHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_rowsHost.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(_rowsHost);
		RebuildRows();
		return card;
	}

	private Control BuildSearch()
	{
		var pad = new MarginContainer();
		pad.AddThemeConstantOverride("margin_left", 12);
		pad.AddThemeConstantOverride("margin_top", 8);
		pad.AddThemeConstantOverride("margin_right", 12);
		pad.AddThemeConstantOverride("margin_bottom", 8);
		_search = new LineEdit
		{
			PlaceholderText = "Search school or district",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CaretBlink = true,
		};
		_search.AddThemeFontOverride("font", _medium);
		_search.AddThemeFontSizeOverride("font_size", 14);
		_search.TextChanged += OnSearchChanged;
		pad.AddChild(_search);
		return pad;
	}

	private void OnSearchChanged(string value)
	{
		_query = value ?? "";
		IReadOnlyList<JobListing> jobs = VisibleJobs();
		if (jobs.Count > 0 && (string.IsNullOrEmpty(_selectedId) || FindJob(_selectedId) == null || !FindJob(_selectedId)!.Matches(_query)))
		{
			_selectedId = jobs[0].OrganizationId;
		}

		RebuildRows();
		BindDetail(FindJob(_selectedId ?? ""));
	}

	private IReadOnlyList<JobListing> VisibleJobs()
	{
		var list = new List<JobListing>();
		foreach (JobListing job in _session.AvailableJobs)
		{
			if (job.Matches(_query))
			{
				list.Add(job);
			}
		}

		return list;
	}

	private Control BuildHeaderRow()
	{
		var row = new PanelContainer();
		row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.045f, 0.06f, 0.95f),
			ContentMarginLeft = 14,
			ContentMarginTop = 6,
			ContentMarginRight = 14,
			ContentMarginBottom = 6,
		});
		row.AddChild(Columns(
			MakeText("SCHOOL", _semibold, 11, TextMuted, HorizontalAlignment.Left),
			MakeText("REP", _semibold, 11, TextMuted, HorizontalAlignment.Right),
			MakeText("STATUS", _semibold, 11, TextMuted, HorizontalAlignment.Right)));
		return row;
	}

	private void RebuildRows()
	{
		foreach (Node child in _rowsHost.GetChildren())
		{
			_rowsHost.RemoveChild(child);
			child.QueueFree();
		}

		IReadOnlyList<JobListing> jobs = VisibleJobs();
		for (int i = 0; i < jobs.Count; i++)
		{
			_rowsHost.AddChild(BuildRow(jobs[i], i, jobs[i].OrganizationId == _selectedId));
		}

		if (jobs.Count == 0)
		{
			_rowsHost.AddChild(MakeText(
				string.IsNullOrWhiteSpace(_query) ? "No seats are posted." : "No posted seats match that search.",
				_medium,
				13,
				TextMuted,
				HorizontalAlignment.Left));
		}
	}

	private Control BuildRow(JobListing job, int index, bool selected)
	{
		var row = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = selected ? new Color(Gold.R, Gold.G, Gold.B, 0.14f) : index % 2 == 0 ? RowEven : RowOdd,
			BorderColor = selected ? Gold : new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = selected ? 2 : 0,
			BorderWidthBottom = 1,
			ContentMarginLeft = 14,
			ContentMarginTop = 8,
			ContentMarginRight = 14,
			ContentMarginBottom = 8,
		});
		row.GuiInput += (InputEvent evt) =>
		{
			if (evt is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				Select(job.OrganizationId);
			}
		};

		Label school = MakeText(job.SchoolName.ToUpperInvariant(), _semibold, 14, selected ? Gold : TextPrimary, HorizontalAlignment.Left);
		school.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		row.AddChild(Columns(
			school,
			MakeText(job.Reputation.ToString(), _bold, 14, RepColor(job.Reputation), HorizontalAlignment.Right),
			_session.WasPassedOver(job.OrganizationId)
				? MakeText("PASSED", _semibold, 11, TextMuted, HorizontalAlignment.Right)
				: MakeText(JobListing.OpenStatus, _semibold, 11, Green, HorizontalAlignment.Right)));
		return row;
	}

	private Control BuildDetailCard()
	{
		var card = new PanelContainer
		{
			CustomMinimumSize = new Vector2(360, 0),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 0.9f,
			ClipContents = true,
		};
		card.AddThemeStyleboxOverride("panel", MakeCard(0));
		_detailHost = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_detailHost.AddThemeConstantOverride("separation", 0);
		card.AddChild(_detailHost);
		return card;
	}

	private void Select(string organizationId)
	{
		_selectedId = organizationId;
		RebuildRows();
		BindDetail(FindJob(organizationId));
	}

	private JobListing? FindJob(string organizationId)
	{
		foreach (JobListing job in _session.AvailableJobs)
		{
			if (job.OrganizationId == organizationId)
			{
				return job;
			}
		}

		return null;
	}

	private void BindDetail(JobListing? job)
	{
		foreach (Node child in _detailHost.GetChildren())
		{
			_detailHost.RemoveChild(child);
			child.QueueFree();
		}

		if (job == null)
		{
			var empty = new VBoxContainer
			{
				SizeFlagsVertical = SizeFlags.ExpandFill,
				Alignment = BoxContainer.AlignmentMode.Center,
			};
			empty.AddChild(MakeText("SELECT A VACANCY", _bold, 16, TextMuted, HorizontalAlignment.Center));
			_detailHost.AddChild(empty);
			return;
		}

		var header = new PanelContainer();
		header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.047f, 0.04f, 0.035f, 0.96f),
			ContentMarginLeft = 16,
			ContentMarginTop = 12,
			ContentMarginRight = 16,
			ContentMarginBottom = 12,
		});
		var headerLayout = new VBoxContainer();
		headerLayout.AddThemeConstantOverride("separation", 2);
		header.AddChild(headerLayout);
		headerLayout.AddChild(MakeText(job.DistrictName.ToUpperInvariant(), _semibold, 10, Gold, HorizontalAlignment.Left));
		headerLayout.AddChild(MakeText(job.SchoolFullName.ToUpperInvariant(), _bold, 22, TextPrimary, HorizontalAlignment.Left));
		_detailHost.AddChild(header);
		_detailHost.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Gold });

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		_detailHost.AddChild(scroll);
		var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		pad.AddThemeConstantOverride("margin_left", 16);
		pad.AddThemeConstantOverride("margin_top", 12);
		pad.AddThemeConstantOverride("margin_right", 16);
		pad.AddThemeConstantOverride("margin_bottom", 8);
		scroll.AddChild(pad);

		var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 10);
		pad.AddChild(body);

		AddStat(body, "PROGRAM REPUTATION", job.Reputation.ToString(), RepColor(job.Reputation));
		AddStat(body, "LAST SEASON", job.LastYearLabel, TextPrimary);
		AddStat(body, "ROSTER OUTLOOK", job.RosterOutlook.ToLabel(), GradeColor(job.RosterOutlook));
		AddStat(body, "FACILITIES", job.Facilities.ToLabel(), GradeColor(job.Facilities));
		AddStat(body, "BOARD EXPECTATION", job.ExpectationLabel, ExpectationColor(job.TargetExpectation));
		AddStat(body, "CONTRACT", job.ContractTermLabel, TextPrimary);
		AddStat(body, "SALARY", $"{job.CoachSalaryLabel} / year", Green);

		AddSection(body, "WHY POSITION IS OPEN");
		var why = MakeText(job.VacancyLabel, _medium, 14, TextPrimary, HorizontalAlignment.Left);
		why.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		body.AddChild(why);

		if (!job.InReach)
		{
			var hint = MakeText(
				"Raise your name on the circuit. Wins, titles, and holding the board's target open better chairs.",
				_medium,
				12,
				TextMuted,
				HorizontalAlignment.Left);
			hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			body.AddChild(hint);
		}

		_detailHost.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = Hairline });
		var footer = new MarginContainer();
		footer.AddThemeConstantOverride("margin_left", 12);
		footer.AddThemeConstantOverride("margin_top", 10);
		footer.AddThemeConstantOverride("margin_right", 12);
		footer.AddThemeConstantOverride("margin_bottom", 10);
		Button action = MakeAccept(job);
		string orgId = job.OrganizationId;
		action.Pressed += () => OnAction(orgId);
		footer.AddChild(action);
		_detailHost.AddChild(footer);
	}

	private void OnAction(string organizationId)
	{
		JobListing? job = FindJob(organizationId);
		if (job == null || !job.InReach || _session.WasPassedOver(organizationId))
		{
			UiSounds.Play(UiSound.Error);
			return;
		}

		// Applying means sitting for an interview; the school decides.
		UiSounds.Play(UiSound.Confirm);
		var interview = new InterviewOverlay();
		GetTree().Root.AddChild(interview);
		interview.Begin(_session, job);
		interview.AcceptedOffer += OnOfferAccepted;
		interview.TreeExited += () => CallDeferred(MethodName.RefreshAfterInterview);
	}

	private void OnOfferAccepted(string organizationId)
	{
		if (!_session.TryTakeJob(organizationId))
		{
			UiSounds.Play(UiSound.Error);
			return;
		}

		UiSounds.Play(UiSound.Confirm);
	}

	private void RefreshAfterInterview()
	{
		if (IsInsideTree() && _selectedId != null)
		{
			BindDetail(FindJob(_selectedId));
		}
	}

	private Button MakeAccept(JobListing job)
	{
		bool passed = _session.WasPassedOver(job.OrganizationId);
		bool blocked = !job.InReach || passed;
		string text = passed ? "PASSED OVER THIS YEAR" : blocked ? "REPUTATION TOO LOW" : "APPLY & INTERVIEW";
		var button = new Button
		{
			Text = text,
			Flat = false,
			Disabled = blocked,
			MouseDefaultCursorShape = blocked ? CursorShape.Arrow : CursorShape.PointingHand,
			CustomMinimumSize = new Vector2(0, 40),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		button.AddThemeFontOverride("font", _bold);
		button.AddThemeFontSizeOverride("font_size", 15);
		button.AddThemeColorOverride("font_color", blocked ? TextMuted : TextOnAccent);
		button.AddThemeColorOverride("font_hover_color", blocked ? TextMuted : TextOnAccent);
		button.AddThemeColorOverride("font_pressed_color", blocked ? TextMuted : TextOnAccent);
		button.AddThemeColorOverride("font_focus_color", blocked ? TextMuted : TextOnAccent);
		button.AddThemeStyleboxOverride("normal", MakeFilled(blocked ? Hairline : Gold));
		button.AddThemeStyleboxOverride("hover", MakeFilled(blocked ? Hairline : AccentHover));
		button.AddThemeStyleboxOverride("pressed", MakeFilled(blocked ? Hairline : AccentHover));
		button.AddThemeStyleboxOverride("focus", MakeFilled(blocked ? Hairline : AccentHover));
		button.AddThemeStyleboxOverride("disabled", MakeFilled(Hairline));
		UiSounds.SetClick(button, UiClickSound.Silent);
		return button;
	}

	private void AddSection(VBoxContainer body, string title)
	{
		body.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = Hairline });
		body.AddChild(MakeText(title, _semibold, 11, Gold, HorizontalAlignment.Left));
	}

	private void AddStat(VBoxContainer body, string label, string value, Color valueColor)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var name = MakeText(label, _medium, 12, TextMuted, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(name);
		row.AddChild(MakeText(value, _bold, 14, valueColor, HorizontalAlignment.Right));
		body.AddChild(row);
	}

	private static HBoxContainer Columns(Control school, Control rep, Control status)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		school.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		rep.CustomMinimumSize = new Vector2(44, 0);
		status.CustomMinimumSize = new Vector2(72, 0);
		row.AddChild(school);
		row.AddChild(rep);
		row.AddChild(status);
		return row;
	}

	private static Color RepColor(int reputation)
	{
		if (reputation >= 75)
		{
			return Green;
		}

		if (reputation <= 40)
		{
			return new Color(0.906f, 0.298f, 0.235f);
		}

		return Accent;
	}

	private static Color ExpectationColor(BoardExpectation expectation) => expectation switch
	{
		BoardExpectation.Rebuild => new Color(0.906f, 0.298f, 0.235f),
		BoardExpectation.WinDistrict or BoardExpectation.StateTitle => Green,
		_ => Accent,
	};

	private static Color GradeColor(LetterGrade grade) => grade switch
	{
		>= LetterGrade.BMinus => Green,
		<= LetterGrade.DPlus => new Color(0.906f, 0.298f, 0.235f),
		_ => Accent,
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

	private static StyleBoxFlat MakeCard(float pad) => new()
	{
		BgColor = new Color(0.070588f, 0.078431f, 0.094118f, 0.94f),
		BorderColor = CardBorder,
		BorderWidthLeft = 1,
		BorderWidthTop = 1,
		BorderWidthRight = 1,
		BorderWidthBottom = 1,
		ContentMarginLeft = pad,
		ContentMarginTop = pad,
		ContentMarginRight = pad,
		ContentMarginBottom = pad,
		CornerRadiusTopLeft = 3,
		CornerRadiusTopRight = 3,
		CornerRadiusBottomRight = 3,
		CornerRadiusBottomLeft = 3,
		CornerDetail = 3,
	};

	private static StyleBoxFlat MakeFilled(Color bg) => new()
	{
		BgColor = bg,
		ContentMarginLeft = 14,
		ContentMarginTop = 10,
		ContentMarginRight = 14,
		ContentMarginBottom = 10,
		CornerRadiusTopLeft = 3,
		CornerRadiusTopRight = 3,
		CornerRadiusBottomRight = 3,
		CornerRadiusBottomLeft = 3,
		CornerDetail = 4,
	};
}
