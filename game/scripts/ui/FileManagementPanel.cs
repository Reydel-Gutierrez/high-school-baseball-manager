using System;
using System.Collections.Generic;
using Godot;
using HSBM.Domain;

/// <summary>
/// Clubhouse career binder hosted under Settings. Save, rename, load,
/// delete, and return to the title screen from the coaching seat.
/// </summary>
public partial class FileManagementPanel : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color CardInner = new(0.055f, 0.062f, 0.078f, 0.92f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color Hairline = new(0.28f, 0.30f, 0.35f, 1f);
	private static readonly Color Locker = new(0.078f, 0.055f, 0.039f, 0.94f);
	private static readonly Color Brass = new(0.92f, 0.74f, 0.28f, 1f);
	private static readonly Color Parchment = new(0.82f, 0.78f, 0.70f, 1f);
	private static readonly Color Danger = new(0.82f, 0.28f, 0.24f, 1f);
	private static readonly Color DangerHot = new(0.92f, 0.36f, 0.30f, 1f);

	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Texture2D _folder = null!;

	private string? _selectedId;
	private string? _pendingDeleteId;
	private Label _status = null!;
	private Label _sessionName = null!;
	private Label _sessionMeta = null!;
	private Label _sessionRecord = null!;
	private Label _sessionSeason = null!;
	private Label _sessionStatus = null!;
	private VBoxContainer _list = null!;
	private LineEdit _nameField = null!;
	private Button _saveButton = null!;
	private Button _loadButton = null!;
	private Button _renameButton = null!;
	private Button _deleteButton = null!;
	private Label _autosaveHint = null!;
	private Control _confirmHost = null!;
	private Label _confirmTitle = null!;
	private Label _confirmBody = null!;
	private LineEdit _confirmName = null!;
	private Button _confirmAction = null!;
	private Action? _onConfirm;
	private readonly Dictionary<string, PanelContainer> _cards = new();

	public event Action? SavedAndExited;

	public event Action? ExitGameRequested;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		_folder = UiSvg.Load("res://assets/ui/icon_folder.svg", 512);
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Build();
		Refresh();
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (!IsVisibleInTree() || !_confirmHost.Visible || ev is not InputEventKey key || !key.Pressed || key.Echo || key.Keycode != Key.Escape)
		{
			return;
		}

		HideConfirm();
		GetViewport().SetInputAsHandled();
	}

	public void Refresh()
	{
		CareerLibrary.EnsureReady();
		IReadOnlyList<CareerSaveSlot> slots = CareerLibrary.ListOrdered();
		_selectedId = CareerLibrary.ActiveId
			?? GameSession.Current?.ActiveSaveId
			?? _selectedId
			?? (slots.Count > 0 ? slots[0].Id : null);
		RefreshSession();
		RebuildList();
		RefreshActions();
		HideConfirm();
		SetStatus("Lock in this seat, stamp a copy, load another file, or leave the clubhouse.");
	}

	private void Build()
	{
		var root = new VBoxContainer();
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		root.AddThemeConstantOverride("separation", 10);
		AddChild(root);
		root.AddChild(BuildHeader());

		var body = new HBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		body.AddThemeConstantOverride("separation", 10);
		root.AddChild(body);
		body.AddChild(BuildSeatPlaque());
		body.AddChild(BuildBinder());

		_confirmHost = BuildConfirm();
		AddChild(_confirmHost);
	}

	private Control BuildHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 14);

		TextureRect mark = TeamLogos.MakeHeaderMark(_folder, 52);
		mark.Modulate = Accent;
		header.AddChild(mark);
		header.AddChild(TeamLogos.MakeHeaderRule(Hairline));

		var identity = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		identity.AddThemeConstantOverride("separation", -2);
		identity.AddChild(MakeText("CLUBHOUSE  ·  FILES", _medium, 10, Accent, HorizontalAlignment.Left));
		identity.AddChild(MakeText("CAREER FILES", _bold, 26, TextPrimary, HorizontalAlignment.Left));
		identity.AddChild(MakeText(
			"Save, save as, load, return to the title screen, or quit.",
			_medium,
			12,
			Parchment,
			HorizontalAlignment.Left));
		header.AddChild(identity);
		return header;
	}

	private Control BuildSeatPlaque()
	{
		var plaque = new PanelContainer
		{
			CustomMinimumSize = new Vector2(280, 0),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 0.92f,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		plaque.AddThemeStyleboxOverride("panel", MakePlaque());

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 10);
		plaque.AddChild(layout);

		var kickerRow = new HBoxContainer();
		kickerRow.AddThemeConstantOverride("separation", 8);
		kickerRow.AddChild(MakeChip("CURRENT SEAT", Brass, TextOnAccent));
		kickerRow.AddChild(MakeFlex());
		layout.AddChild(kickerRow);

		_sessionName = MakeText("—", _bold, 28, TextPrimary, HorizontalAlignment.Left);
		layout.AddChild(_sessionName);
		_sessionMeta = MakeText("—", _semibold, 14, Parchment, HorizontalAlignment.Left);
		_sessionMeta.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(_sessionMeta);

		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });

		var metrics = new HBoxContainer();
		metrics.AddThemeConstantOverride("separation", 18);
		_sessionRecord = AddMetric(metrics, "—", "RECORD");
		_sessionSeason = AddMetric(metrics, "—", "SEASON");
		_sessionStatus = AddMetric(metrics, "—", "STATUS");
		layout.AddChild(metrics);

		layout.AddChild(MakeFlex());

		var actions = new VBoxContainer();
		actions.AddThemeConstantOverride("separation", 8);
		_saveButton = MakeGameButton("SAVE CAREER", true, SaveCurrent, UiClickSound.Confirm);
		actions.AddChild(_saveButton);
		actions.AddChild(MakeGameButton("SAVE AS", true, AskSaveAs, UiClickSound.Confirm));
		actions.AddChild(MakeGameButton("RETURN TO MAIN MENU", false, AskExitToTitle, UiClickSound.Select));
		actions.AddChild(MakeGameButton("EXIT GAME", false, AskExitGame, UiClickSound.Select));
		layout.AddChild(actions);

		_autosaveHint = MakeText(string.Empty, _medium, 11, TextMuted, HorizontalAlignment.Left);
		_autosaveHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(_autosaveHint);
		return plaque;
	}

	private Control BuildBinder()
	{
		var binder = new PanelContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsStretchRatio = 1.18f,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			ClipContents = true,
		};
		binder.AddThemeStyleboxOverride("panel", MakeBinder());

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 10);
		binder.AddChild(layout);

		var heading = new HBoxContainer();
		heading.AddThemeConstantOverride("separation", 8);
		var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		titles.AddThemeConstantOverride("separation", -2);
		titles.AddChild(MakeText("THE BINDER", _medium, 10, Accent, HorizontalAlignment.Left));
		titles.AddChild(MakeText("SAVED CAREERS", _bold, 20, TextPrimary, HorizontalAlignment.Left));
		heading.AddChild(titles);
		layout.AddChild(heading);

		layout.AddChild(BuildNameplate());

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
		};
		layout.AddChild(scroll);

		_list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_list.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_list);

		var footer = new HBoxContainer();
		footer.AddThemeConstantOverride("separation", 8);
		_loadButton = MakeGameButton("LOAD", true, LoadSelected, UiClickSound.Confirm);
		_loadButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		footer.AddChild(_loadButton);
		_deleteButton = MakeGameButton("REMOVE FROM BINDER", false, AskDelete, UiClickSound.Select);
		_deleteButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		footer.AddChild(_deleteButton);
		layout.AddChild(footer);

		_status = MakeText(string.Empty, _medium, 12, Parchment, HorizontalAlignment.Left);
		_status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(_status);
		return binder;
	}

	private Control BuildNameplate()
	{
		var plate = new PanelContainer();
		plate.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.10f, 0.07f, 0.04f, 0.96f),
			BorderColor = Brass,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 12,
			ContentMarginTop = 10,
			ContentMarginRight = 12,
			ContentMarginBottom = 10,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2,
		});

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 6);
		plate.AddChild(layout);
		layout.AddChild(MakeText("NAMEPLATE", _medium, 10, Brass, HorizontalAlignment.Left));

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		_nameField = new LineEdit
		{
			PlaceholderText = "Career name",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0, 38),
			MaxLength = 40,
		};
		_nameField.AddThemeFontOverride("font", _bold);
		_nameField.AddThemeFontSizeOverride("font_size", 18);
		_nameField.AddThemeColorOverride("font_color", TextPrimary);
		_nameField.AddThemeColorOverride("font_placeholder_color", TextMuted);
		_nameField.AddThemeColorOverride("caret_color", Accent);
		_nameField.AddThemeStyleboxOverride("normal", MakeNameplateField(false));
		_nameField.AddThemeStyleboxOverride("focus", MakeNameplateField(true));
		_nameField.TextSubmitted += _ => RenameSelected();
		row.AddChild(_nameField);
		_renameButton = MakeGameButton("PUT ON FILE", true, RenameSelected, UiClickSound.Confirm);
		_renameButton.CustomMinimumSize = new Vector2(118, 38);
		row.AddChild(_renameButton);
		layout.AddChild(row);
		return plate;
	}

	private Control BuildConfirm()
	{
		var host = new Control
		{
			Visible = false,
			MouseFilter = MouseFilterEnum.Stop,
		};
		host.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var dim = new ColorRect
		{
			Color = new Color(0.02f, 0.01f, 0.01f, 0.72f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		host.AddChild(dim);

		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		host.AddChild(center);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(420, 0),
			MouseFilter = MouseFilterEnum.Stop,
		};
		panel.AddThemeStyleboxOverride("panel", MakePlaque(18));
		center.AddChild(panel);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 12);
		panel.AddChild(layout);
		layout.AddChild(MakeText("HOLD UP", _medium, 11, Accent, HorizontalAlignment.Left));
		_confirmTitle = MakeText("CONFIRM", _bold, 26, TextPrimary, HorizontalAlignment.Left);
		layout.AddChild(_confirmTitle);
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Accent });
		_confirmBody = MakeText(string.Empty, _medium, 15, Parchment, HorizontalAlignment.Left);
		_confirmBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(_confirmBody);
		_confirmName = new LineEdit
		{
			Visible = false,
			PlaceholderText = "Copy name",
			CustomMinimumSize = new Vector2(0, 38),
			MaxLength = 40,
		};
		_confirmName.AddThemeFontOverride("font", _bold);
		_confirmName.AddThemeFontSizeOverride("font_size", 18);
		_confirmName.AddThemeColorOverride("font_color", TextPrimary);
		_confirmName.AddThemeColorOverride("font_placeholder_color", TextMuted);
		_confirmName.AddThemeColorOverride("caret_color", Accent);
		_confirmName.AddThemeStyleboxOverride("normal", MakeNameplateField(false));
		_confirmName.AddThemeStyleboxOverride("focus", MakeNameplateField(true));
		_confirmName.TextSubmitted += _ => RunConfirm();
		layout.AddChild(_confirmName);

		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 8);
		Button cancel = MakeGameButton("KEEP IT", false, HideConfirm, UiClickSound.Back);
		cancel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		actions.AddChild(cancel);
		_confirmAction = MakeGameButton("CONFIRM", true, RunConfirm, UiClickSound.Confirm);
		_confirmAction.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		actions.AddChild(_confirmAction);
		layout.AddChild(actions);
		return host;
	}

	private void RefreshSession()
	{
		GameSession? session = GameSession.Current;
		if (session == null)
		{
			_sessionName.Text = "NO ACTIVE SEAT";
			_sessionMeta.Text = "Open a career from the title screen.";
			_sessionRecord.Text = "—";
			_sessionSeason.Text = "—";
			_sessionStatus.Text = "—";
			_saveButton.Disabled = true;
			_autosaveHint.Text = AutosaveHint();
			return;
		}

		CareerSaveSlot snapshot = session.CaptureCareerSlot();
		_saveButton.Disabled = false;
		_sessionName.Text = session.CoachName.ToUpperInvariant();
		_sessionMeta.Text = $"{snapshot.SchoolName.ToUpperInvariant()}  ·  HEAD COACH";
		_sessionRecord.Text = session.RecordLabel;
		_sessionSeason.Text = snapshot.SeasonNumber.ToString();
		_sessionStatus.Text = snapshot.Status.ToUpperInvariant();
		_autosaveHint.Text = AutosaveHint();
	}

	private void RebuildList()
	{
		foreach (Node child in _list.GetChildren())
		{
			child.QueueFree();
		}

		_cards.Clear();
		IReadOnlyList<CareerSaveSlot> slots = CareerLibrary.ListOrdered();
		foreach (CareerSaveSlot slot in slots)
		{
			_list.AddChild(BuildCard(slot));
		}

		if (slots.Count == 0)
		{
			_list.AddChild(MakeText(
				"The binder is empty. Save this seat to start the file.",
				_medium,
				14,
				Parchment,
				HorizontalAlignment.Left));
		}
	}

	private Control BuildCard(CareerSaveSlot slot)
	{
		bool selected = slot.Id == _selectedId;
		bool current = slot.Id == CareerLibrary.ActiveId || slot.Id == GameSession.Current?.ActiveSaveId;
		var card = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Stop,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		card.AddThemeStyleboxOverride("panel", MakeLockerCard(selected));
		_cards[slot.Id] = card;

		var hit = new Button
		{
			Flat = true,
			Text = string.Empty,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			FocusMode = FocusModeEnum.All,
		};
		hit.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		hit.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
		hit.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
		hit.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		UiSounds.SetClick(hit, UiClickSound.Select);
		string id = slot.Id;
		hit.Pressed += () => Select(id);
		card.AddChild(hit);

		var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_bottom", 10);
		card.AddChild(margin);

		var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 12);
		margin.AddChild(row);

		var copy = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		copy.AddThemeConstantOverride("separation", 0);
		row.AddChild(copy);

		var nameRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		nameRow.AddThemeConstantOverride("separation", 8);
		Label name = MakeText(slot.DisplayName.ToUpperInvariant(), _bold, 20, TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameRow.AddChild(name);
		if (current)
		{
			nameRow.AddChild(MakeChip("IN THE CHAIR", Accent, TextOnAccent));
		}

		copy.AddChild(nameRow);
		copy.AddChild(MakeText(
			$"{slot.SchoolName.ToUpperInvariant()}  ·  {slot.SeasonLine.ToUpperInvariant()}",
			_medium,
			12,
			Parchment,
			HorizontalAlignment.Left));
		copy.AddChild(MakeText(
			slot.Status.ToUpperInvariant(),
			_semibold,
			12,
			Accent,
			HorizontalAlignment.Left));

		var score = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		score.AddThemeConstantOverride("separation", -4);
		score.AddChild(MakeText(slot.RecordLabel, _bold, 28, TextPrimary, HorizontalAlignment.Right));
		score.AddChild(MakeText(slot.LastPlayedLabel.ToUpperInvariant(), _medium, 10, TextMuted, HorizontalAlignment.Right));
		row.AddChild(score);
		return card;
	}

	private Control MakeChip(string text, Color fill, Color ink)
	{
		var chip = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = fill,
			ContentMarginLeft = 8,
			ContentMarginTop = 3,
			ContentMarginRight = 8,
			ContentMarginBottom = 3,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2,
		});
		chip.AddChild(MakeText(text, _semibold, 10, ink, HorizontalAlignment.Center));
		return chip;
	}

	private Label AddMetric(HBoxContainer row, string value, string label)
	{
		var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		col.AddThemeConstantOverride("separation", -4);
		var amount = MakeText(value, _bold, 26, TextPrimary, HorizontalAlignment.Left);
		col.AddChild(amount);
		col.AddChild(MakeText(label, _semibold, 11, TextMuted, HorizontalAlignment.Left));
		row.AddChild(col);
		return amount;
	}

	private void Select(string id)
	{
		_selectedId = id;
		foreach (KeyValuePair<string, PanelContainer> pair in _cards)
		{
			pair.Value.AddThemeStyleboxOverride("panel", MakeLockerCard(pair.Key == id));
		}

		RefreshActions();
	}

	private void RefreshActions()
	{
		CareerSaveSlot? selected = CareerLibrary.Find(_selectedId);
		bool hasSelection = selected != null;
		bool isActive = hasSelection && selected!.Id == CareerLibrary.ActiveId;
		_loadButton.Disabled = !hasSelection || isActive;
		_deleteButton.Disabled = !hasSelection;
		_renameButton.Disabled = !hasSelection;
		_nameField.Editable = hasSelection;
		if (!_nameField.HasFocus())
		{
			_nameField.Text = selected?.CoachName ?? string.Empty;
		}

		_loadButton.Text = isActive ? "ALREADY IN THE CHAIR" : "LOAD";
	}

	private void SaveCurrent()
	{
		GameSession? session = GameSession.Current;
		if (session == null)
		{
			return;
		}

		CareerSaveSlot saved = session.SaveCareer();
		_selectedId = saved.Id;
		RefreshSession();
		RebuildList();
		RefreshActions();
		SetStatus($"{saved.DisplayName}'s seat at {saved.SchoolName} is locked in.");
	}

	private void AskSaveAs()
	{
		GameSession? session = GameSession.Current;
		if (session == null)
		{
			return;
		}

		string suggested = string.IsNullOrWhiteSpace(session.CoachName)
			? "Career copy"
			: $"{session.CoachName} (Copy)";
		ShowConfirm(
			"SAVE A COPY?",
			"Stamp a name on this copy. The seat you're in stays on file, and you keep coaching from the new one.",
			"STAMP IT",
			ConfirmSaveAs,
			danger: false,
			nameDefault: suggested);
	}

	private void ConfirmSaveAs()
	{
		GameSession? session = GameSession.Current;
		string label = _confirmName.Text.Trim();
		HideConfirm();
		if (session == null)
		{
			return;
		}

		if (string.IsNullOrEmpty(label))
		{
			SetStatus("Stamp a name on the copy before filing it.");
			return;
		}

		CareerSaveSlot saved = session.SaveCareerAs(label);
		_selectedId = saved.Id;
		RefreshSession();
		RebuildList();
		RefreshActions();
		SetStatus($"{saved.DisplayName} is on file as a new career.");
	}

	private void LoadSelected()
	{
		CareerSaveSlot? slot = CareerLibrary.Find(_selectedId);
		if (slot == null)
		{
			return;
		}

		if (slot.Id == CareerLibrary.ActiveId || slot.Id == GameSession.Current?.ActiveSaveId)
		{
			SetStatus($"{slot.DisplayName} is already in the chair.");
			return;
		}

		ShowConfirm(
			"SWITCH SEATS?",
			$"Load {slot.DisplayName} at {slot.SchoolName}? Anything not locked in on this seat will be gone.",
			"TAKE THE CHAIR",
			() =>
			{
				HideConfirm();
				CareerLibrary.SetActive(slot.Id);
				GameSession.Current?.ApplyCareerSlot(slot);
				_selectedId = slot.Id;
				RefreshSession();
				RebuildList();
				RefreshActions();
				SetStatus($"{slot.DisplayName} is back in the chair.");
			},
			danger: false);
	}

	private void RenameSelected()
	{
		CareerSaveSlot? slot = CareerLibrary.Find(_selectedId);
		if (slot == null)
		{
			return;
		}

		string next = _nameField.Text.Trim();
		if (string.IsNullOrEmpty(next))
		{
			SetStatus("Stamp a name on the plate before filing it.");
			return;
		}

		if (string.Equals(next, slot.CoachName, StringComparison.Ordinal))
		{
			SetStatus($"{slot.CoachName} already wears that nameplate.");
			return;
		}

		CareerSaveSlot? renamed = CareerLibrary.Rename(slot.Id, next);
		if (renamed == null)
		{
			SetStatus("Could not restamp that career.");
			return;
		}

		if (GameSession.Current?.ActiveSaveId == renamed.Id)
		{
			GameSession.Current.SetCoachName(renamed.CoachName);
		}

		_selectedId = renamed.Id;
		RefreshSession();
		RebuildList();
		RefreshActions();
		SetStatus($"Nameplate restamped: {renamed.CoachName}.");
	}

	private void AskDelete()
	{
		CareerSaveSlot? slot = CareerLibrary.Find(_selectedId);
		if (slot == null)
		{
			return;
		}

		_pendingDeleteId = slot.Id;
		ShowConfirm(
			"PULL THE FILE?",
			$"Remove {slot.DisplayName}'s career at {slot.SchoolName} from the binder? That seat is gone for good.",
			"PULL IT",
			ConfirmDelete,
			danger: true);
	}

	private void ConfirmDelete()
	{
		string? id = _pendingDeleteId;
		HideConfirm();
		if (string.IsNullOrEmpty(id) || !CareerLibrary.Delete(id))
		{
			return;
		}

		if (GameSession.Current?.ActiveSaveId == id)
		{
			GameSession.Current.ClearActiveSave();
		}

		IReadOnlyList<CareerSaveSlot> remaining = CareerLibrary.ListOrdered();
		_selectedId = remaining.Count > 0 ? remaining[0].Id : null;
		RefreshSession();
		RebuildList();
		RefreshActions();
		SetStatus("That career is out of the binder.");
	}

	private void AskExitToTitle()
	{
		ShowConfirm(
			"LEAVE THE CLUBHOUSE?",
			"Return to the main menu without saving? Anything not locked in on this seat will be gone.",
			"WALK OUT",
			() =>
			{
				HideConfirm();
				SavedAndExited?.Invoke();
			},
			danger: false);
	}

	private void AskExitGame()
	{
		ShowConfirm(
			"QUIT TO DESKTOP?",
			"Exit the game without saving? Anything not locked in on this seat will be gone.",
			"QUIT",
			() =>
			{
				HideConfirm();
				ExitGameRequested?.Invoke();
			},
			danger: true);
	}

	private void ShowConfirm(string title, string body, string confirmLabel, Action action, bool danger, string? nameDefault = null)
	{
		_onConfirm = action;
		_confirmTitle.Text = title;
		_confirmBody.Text = body;
		_confirmAction.Text = confirmLabel;
		_confirmAction.AddThemeColorOverride("font_color", danger ? TextPrimary : TextOnAccent);
		_confirmAction.AddThemeColorOverride("font_hover_color", danger ? TextPrimary : TextOnAccent);
		_confirmAction.AddThemeStyleboxOverride("normal", MakeFilled(danger ? Danger : Accent));
		_confirmAction.AddThemeStyleboxOverride("hover", MakeFilled(danger ? DangerHot : AccentHover));
		_confirmAction.AddThemeStyleboxOverride("pressed", MakeFilled(danger ? DangerHot : AccentHover));
		_confirmAction.AddThemeStyleboxOverride("focus", MakeFilled(danger ? DangerHot : AccentHover));
		bool named = nameDefault != null;
		_confirmName.Visible = named;
		if (named)
		{
			_confirmName.Text = nameDefault;
		}

		_confirmHost.Visible = true;
		_confirmHost.MoveToFront();
		if (named)
		{
			_confirmName.GrabFocus();
			_confirmName.SelectAll();
		}
	}

	private void RunConfirm() => _onConfirm?.Invoke();

	private void HideConfirm()
	{
		_pendingDeleteId = null;
		_onConfirm = null;
		_confirmName.Visible = false;
		_confirmHost.Visible = false;
	}

	private static string AutosaveHint()
	{
		AutosavePreferences prefs = GameSettings.Autosave;
		if (!prefs.Enabled)
		{
			return "Autosave is off. Turn it back on under Gameplay.";
		}

		var parts = new List<string>();
		if (prefs.AfterGame)
		{
			parts.Add("games");
		}

		if (prefs.AfterMajorTransaction)
		{
			parts.Add("roster moves");
		}

		if (prefs.AfterWeekAdvance)
		{
			parts.Add("weeks");
		}

		if (prefs.AfterPostseasonRound)
		{
			parts.Add("postseason");
		}

		if (prefs.AfterOffseasonTransition)
		{
			parts.Add("offseason");
		}

		if (parts.Count == 0)
		{
			return "Autosave is on, but every trigger is off. Change that under Gameplay.";
		}

		return $"Autosave after {string.Join(", ", parts)}. Configure under Gameplay.";
	}

	private void SetStatus(string text) => _status.Text = text;

	private Button MakeGameButton(string text, bool primary, Action onPressed, UiClickSound click)
	{
		var button = new Button
		{
			Text = text,
			Flat = false,
			CustomMinimumSize = new Vector2(0, 40),
			MouseDefaultCursorShape = CursorShape.PointingHand,
			FocusMode = FocusModeEnum.All,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		button.AddThemeFontOverride("font", _bold);
		button.AddThemeFontSizeOverride("font_size", 14);
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
			button.AddThemeColorOverride("font_hover_color", Colors.White);
			button.AddThemeStyleboxOverride("normal", MakeOutlined(Locker, Brass));
			button.AddThemeStyleboxOverride("hover", MakeOutlined(HoverBg, Accent));
			button.AddThemeStyleboxOverride("pressed", MakeOutlined(HoverBg, Accent));
			button.AddThemeStyleboxOverride("focus", MakeOutlined(HoverBg, Accent));
		}

		UiSounds.SetClick(button, click);
		button.Pressed += onPressed;
		return button;
	}

	private static Control MakeFlex()
	{
		return new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
	}

	private static StyleBoxFlat MakePlaque(float pad = 16)
	{
		return new StyleBoxFlat
		{
			BgColor = Locker,
			BorderColor = Brass,
			BorderWidthLeft = 4,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = pad,
			ContentMarginTop = pad,
			ContentMarginRight = pad,
			ContentMarginBottom = pad,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2,
			CornerDetail = 4,
		};
	}

	private static StyleBoxFlat MakeBinder()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.047f, 0.052f, 0.068f, 0.96f),
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 16,
			ContentMarginTop = 14,
			ContentMarginRight = 16,
			ContentMarginBottom = 14,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2,
			CornerDetail = 4,
		};
	}

	private static StyleBoxFlat MakeLockerCard(bool selected)
	{
		return new StyleBoxFlat
		{
			BgColor = selected ? new Color(0.12f, 0.09f, 0.05f, 0.96f) : CardInner,
			BorderColor = selected ? Accent : Hairline,
			BorderWidthLeft = selected ? 4 : 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2,
			CornerDetail = 4,
		};
	}

	private static StyleBoxFlat MakeNameplateField(bool focused)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.03f, 0.02f, 0.96f),
			BorderColor = focused ? Accent : Brass,
			BorderWidthLeft = 0,
			BorderWidthTop = 0,
			BorderWidthRight = 0,
			BorderWidthBottom = 2,
			ContentMarginLeft = 4,
			ContentMarginTop = 6,
			ContentMarginRight = 4,
			ContentMarginBottom = 4,
		};
	}

	private static StyleBoxFlat MakeFilled(Color fill)
	{
		return new StyleBoxFlat
		{
			BgColor = fill,
			ContentMarginLeft = 14,
			ContentMarginTop = 8,
			ContentMarginRight = 14,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2,
		};
	}

	private static StyleBoxFlat MakeOutlined(Color fill, Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = fill,
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 14,
			ContentMarginTop = 8,
			ContentMarginRight = 14,
			ContentMarginBottom = 8,
			CornerRadiusTopLeft = 2,
			CornerRadiusTopRight = 2,
			CornerRadiusBottomRight = 2,
			CornerRadiusBottomLeft = 2,
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
}
