using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Title-screen career picker. In-game save, rename, and exit live on
/// Settings → File Management.
/// </summary>
public partial class CareersOverlay : Control
{
	public enum Mode
	{
		Dashboard,
		LoadOnly,
	}

	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color Amber = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AmberHot = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color Blue = new(0.117647f, 0.454902f, 0.960784f, 1f);
	private static readonly Color BlueHot = new(0.22f, 0.56f, 1f, 1f);
	private static readonly Color CardFill = new(0.055f, 0.062f, 0.078f, 0.96f);
	private static readonly Color CardBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color PanelFill = new(0.047f, 0.055f, 0.070f, 0.98f);
	private static readonly Color HoverFill = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color Danger = new(0.82f, 0.28f, 0.24f, 1f);

	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private Mode _mode = Mode.Dashboard;
	private Color _accent = Amber;
	private Color _accentHot = AmberHot;
	private string? _selectedId;
	private string? _pendingDeleteId;
	private Label _status = null!;
	private Label _kicker = null!;
	private VBoxContainer _list = null!;
	private Control _confirmHost = null!;
	private Label _confirmBody = null!;
	private Button _saveButton = null!;
	private Button _loadButton = null!;
	private Button _deleteButton = null!;
	private Button _exitButton = null!;
	private readonly Dictionary<string, PanelContainer> _cards = new();

	public event Action<CareerSaveSlot>? CareerChosen;

	public event Action? SavedAndExited;

	public override void _Ready()
	{
		_bold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Bold.ttf");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		_medium = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-Medium.ttf");
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		Build();
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (!Visible || ev is not InputEventKey key || !key.Pressed || key.Echo || key.Keycode != Key.Escape)
		{
			return;
		}

		if (_confirmHost.Visible)
		{
			HideConfirm();
		}
		else
		{
			Close();
		}

		GetViewport().SetInputAsHandled();
	}

	public void Open(Mode mode = Mode.Dashboard)
	{
		_mode = mode;
		bool title = mode == Mode.LoadOnly;
		_accent = title ? Blue : Amber;
		_accentHot = title ? BlueHot : AmberHot;
		_kicker.AddThemeColorOverride("font_color", _accent);
		CareerLibrary.EnsureReady();
		IReadOnlyList<CareerSaveSlot> slots = CareerLibrary.ListOrdered();
		_selectedId = CareerLibrary.ActiveId
			?? GameSession.Current?.ActiveSaveId
			?? (slots.Count > 0 ? slots[0].Id : null);
		_pendingDeleteId = null;
		_saveButton.Visible = !title;
		_exitButton.Visible = !title;
		StyleButton(_saveButton, true);
		StyleButton(_loadButton, true);
		StyleButton(_deleteButton, false);
		StyleButton(_exitButton, false);
		RebuildList();
		RefreshActions();
		SetStatus(title ? "Select a career to load." : "Save, load, or delete coaching careers.");
		HideConfirm();
		Visible = true;
		MoveToFront();
	}

	public void Close()
	{
		Visible = false;
		HideConfirm();
	}

	private void Build()
	{
		var dim = new ColorRect
		{
			Color = new Color(0.01f, 0.02f, 0.04f, 0.78f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		dim.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				Close();
				UiSounds.Play(UiSound.Back);
			}
		};
		AddChild(dim);

		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(520, 560),
			MouseFilter = MouseFilterEnum.Stop,
		};
		panel.AddThemeStyleboxOverride("panel", MakePanel(18));
		center.AddChild(panel);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 12);
		panel.AddChild(layout);
		layout.AddChild(BuildHeader());
		layout.AddChild(BuildActions());
		layout.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = CardBorder });

		var scroll = new ScrollContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
		};
		layout.AddChild(scroll);

		_list = new VBoxContainer();
		_list.AddThemeConstantOverride("separation", 10);
		_list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.AddChild(_list);

		_status = MakeLabel(string.Empty, _medium, 12, TextMuted, HorizontalAlignment.Left);
		_status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(_status);

		_confirmHost = BuildConfirm();
		AddChild(_confirmHost);
	}

	private Control BuildHeader()
	{
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 8);
		var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		titles.AddThemeConstantOverride("separation", -2);
		_kicker = MakeLabel("FILE", _medium, 10, Amber, HorizontalAlignment.Left);
		titles.AddChild(_kicker);
		titles.AddChild(MakeLabel("CAREERS", _bold, 26, TextPrimary, HorizontalAlignment.Left));
		header.AddChild(titles);
		header.AddChild(MakeButton("CLOSE", false, Close, UiClickSound.Back));
		return header;
	}

	private Control BuildActions()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		_saveButton = MakeButton("SAVE", true, SaveCurrent, UiClickSound.Confirm);
		_loadButton = MakeButton("LOAD", true, LoadSelected, UiClickSound.Confirm);
		_deleteButton = MakeButton("DELETE", false, AskDelete, UiClickSound.Select);
		_exitButton = MakeButton("SAVE & EXIT", false, SaveAndExit, UiClickSound.Confirm);
		_saveButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_loadButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_deleteButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_exitButton.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(_saveButton);
		row.AddChild(_loadButton);
		row.AddChild(_deleteButton);
		row.AddChild(_exitButton);
		return row;
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
			Color = new Color(0.01f, 0.02f, 0.04f, 0.55f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		host.AddChild(dim);

		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		host.AddChild(center);

		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(380, 0),
			MouseFilter = MouseFilterEnum.Stop,
		};
		panel.AddThemeStyleboxOverride("panel", MakePanel(16));
		center.AddChild(panel);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 12);
		panel.AddChild(layout);
		layout.AddChild(MakeLabel("DELETE CAREER", _bold, 20, TextPrimary, HorizontalAlignment.Left));
		_confirmBody = MakeLabel(string.Empty, _medium, 14, TextMuted, HorizontalAlignment.Left);
		_confirmBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		layout.AddChild(_confirmBody);

		var actions = new HBoxContainer();
		actions.AddThemeConstantOverride("separation", 8);
		actions.AddChild(MakeButton("CANCEL", false, HideConfirm, UiClickSound.Back));
		Button destroy = MakeButton("DELETE", true, ConfirmDelete, UiClickSound.Confirm);
		destroy.AddThemeColorOverride("font_color", TextPrimary);
		destroy.AddThemeStyleboxOverride("normal", MakeFilled(Danger));
		destroy.AddThemeStyleboxOverride("hover", MakeFilled(new Color(0.92f, 0.36f, 0.30f)));
		destroy.AddThemeStyleboxOverride("pressed", MakeFilled(new Color(0.92f, 0.36f, 0.30f)));
		destroy.AddThemeStyleboxOverride("focus", MakeFilled(new Color(0.92f, 0.36f, 0.30f)));
		actions.AddChild(destroy);
		layout.AddChild(actions);
		return host;
	}

	private void RebuildList()
	{
		foreach (Node child in _list.GetChildren())
		{
			child.QueueFree();
		}

		_cards.Clear();
		IReadOnlyList<CareerSaveSlot> slots = CareerLibrary.ListOrdered();
		for (int i = 0; i < slots.Count; i++)
		{
			if (i > 0)
			{
				_list.AddChild(new ColorRect
				{
					CustomMinimumSize = new Vector2(0, 1),
					Color = CardBorder,
				});
			}

			_list.AddChild(BuildCard(slots[i]));
		}

		if (slots.Count == 0)
		{
			_list.AddChild(MakeLabel("No saved careers yet.", _medium, 14, TextMuted, HorizontalAlignment.Left));
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
		card.AddThemeStyleboxOverride("panel", MakeCard(selected));
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
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_top", 12);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_bottom", 12);
		card.AddChild(margin);

		var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		col.AddThemeConstantOverride("separation", 1);
		margin.AddChild(col);

		var nameRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		nameRow.AddThemeConstantOverride("separation", 10);
		Label name = MakeLabel(slot.DisplayName, _bold, 20, TextPrimary, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameRow.AddChild(name);
		if (current)
		{
			nameRow.AddChild(MakeChip("CURRENT"));
		}

		col.AddChild(nameRow);
		col.AddChild(MakeLabel(slot.SchoolName, _medium, 14, TextMuted, HorizontalAlignment.Left));
		col.AddChild(MakeLabel(slot.SeasonLine, _semibold, 13, TextPrimary, HorizontalAlignment.Left));
		col.AddChild(MakeLabel(slot.RecordLabel, _bold, 22, TextPrimary, HorizontalAlignment.Left));
		col.AddChild(MakeLabel(slot.Status, _semibold, 13, _accent, HorizontalAlignment.Left));
		return card;
	}

	private Control MakeChip(string text)
	{
		var chip = new PanelContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		chip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = _accent,
			ContentMarginLeft = 8,
			ContentMarginTop = 2,
			ContentMarginRight = 8,
			ContentMarginBottom = 2,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		});
		chip.AddChild(MakeLabel(text, _semibold, 10, TextOnAccent, HorizontalAlignment.Center));
		return chip;
	}

	private void Select(string id)
	{
		_selectedId = id;
		foreach (KeyValuePair<string, PanelContainer> pair in _cards)
		{
			pair.Value.AddThemeStyleboxOverride("panel", MakeCard(pair.Key == id));
		}

		RefreshActions();
	}

	private void RefreshActions()
	{
		CareerSaveSlot? selected = CareerLibrary.Find(_selectedId);
		bool hasSelection = selected != null;
		bool isActive = hasSelection && selected!.Id == CareerLibrary.ActiveId;
		_loadButton.Disabled = !hasSelection;
		_deleteButton.Disabled = !hasSelection;
		_saveButton.Disabled = GameSession.Current == null;
		_loadButton.Text = isActive && _mode == Mode.Dashboard ? "LOADED" : "LOAD";
	}

	private void SaveCurrent()
	{
		GameSession? session = GameSession.Current;
		if (session == null)
		{
			return;
		}

		CareerSaveSlot saved = CareerLibrary.Upsert(session.CaptureCareerSlot());
		session.ApplyCareerSlot(saved, announce: false);
		_selectedId = saved.Id;
		RebuildList();
		RefreshActions();
		SetStatus($"Saved {saved.CoachName} at {saved.SchoolName}.");
	}

	private void LoadSelected()
	{
		CareerSaveSlot? slot = CareerLibrary.Find(_selectedId);
		if (slot == null)
		{
			return;
		}

		CareerLibrary.SetActive(slot.Id);
		GameSession.Current?.ApplyCareerSlot(slot);
		_selectedId = slot.Id;
		RebuildList();
		RefreshActions();
		SetStatus($"Loaded {slot.CoachName}.");
		CareerChosen?.Invoke(slot);
		if (_mode == Mode.Dashboard)
		{
			Close();
		}
	}

	private void AskDelete()
	{
		CareerSaveSlot? slot = CareerLibrary.Find(_selectedId);
		if (slot == null)
		{
			return;
		}

		_pendingDeleteId = slot.Id;
		_confirmBody.Text = $"Delete {slot.CoachName}'s career at {slot.SchoolName}? This cannot be undone.";
		_confirmHost.Visible = true;
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
		RebuildList();
		RefreshActions();
		SetStatus("Career deleted.");
	}

	private void HideConfirm()
	{
		_pendingDeleteId = null;
		_confirmHost.Visible = false;
	}

	private void SaveAndExit()
	{
		SaveCurrent();
		SavedAndExited?.Invoke();
	}

	private void SetStatus(string text) => _status.Text = text;

	private Button MakeButton(string text, bool primary, Action onPressed, UiClickSound click)
	{
		var button = new Button
		{
			Text = text,
			Flat = false,
			CustomMinimumSize = new Vector2(0, 34),
			MouseDefaultCursorShape = CursorShape.PointingHand,
			FocusMode = FocusModeEnum.All,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 13);
		StyleButton(button, primary);
		UiSounds.SetClick(button, click);
		button.Pressed += onPressed;
		return button;
	}

	private void StyleButton(Button button, bool primary)
	{
		if (primary)
		{
			button.AddThemeColorOverride("font_color", TextOnAccent);
			button.AddThemeColorOverride("font_hover_color", TextOnAccent);
			button.AddThemeColorOverride("font_pressed_color", TextOnAccent);
			button.AddThemeColorOverride("font_focus_color", TextOnAccent);
			button.AddThemeStyleboxOverride("normal", MakeFilled(_accent));
			button.AddThemeStyleboxOverride("hover", MakeFilled(_accentHot));
			button.AddThemeStyleboxOverride("pressed", MakeFilled(_accentHot));
			button.AddThemeStyleboxOverride("focus", MakeFilled(_accentHot));
			return;
		}

		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeStyleboxOverride("normal", MakeOutlined(CardFill, CardBorder));
		button.AddThemeStyleboxOverride("hover", MakeOutlined(HoverFill, _accent));
		button.AddThemeStyleboxOverride("pressed", MakeOutlined(HoverFill, _accent));
		button.AddThemeStyleboxOverride("focus", MakeOutlined(HoverFill, _accent));
	}

	private static Label MakeLabel(string text, FontFile font, int size, Color color, HorizontalAlignment align)
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

	private StyleBoxFlat MakeCard(bool selected)
	{
		return new StyleBoxFlat
		{
			BgColor = selected ? new Color(0.10f, 0.12f, 0.18f, 0.96f) : CardFill,
			BorderColor = selected ? _accent : CardBorder,
			BorderWidthLeft = selected ? 2 : 1,
			BorderWidthTop = selected ? 2 : 1,
			BorderWidthRight = selected ? 2 : 1,
			BorderWidthBottom = selected ? 2 : 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerDetail = 4,
		};
	}

	private static StyleBoxFlat MakePanel(float pad)
	{
		return new StyleBoxFlat
		{
			BgColor = PanelFill,
			BorderColor = CardBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = pad,
			ContentMarginTop = pad,
			ContentMarginRight = pad,
			ContentMarginBottom = pad,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerDetail = 5,
		};
	}

	private static StyleBoxFlat MakeFilled(Color fill)
	{
		return new StyleBoxFlat
		{
			BgColor = fill,
			ContentMarginLeft = 12,
			ContentMarginTop = 7,
			ContentMarginRight = 12,
			ContentMarginBottom = 7,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
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
			ContentMarginLeft = 12,
			ContentMarginTop = 7,
			ContentMarginRight = 12,
			ContentMarginBottom = 7,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
		};
	}
}
