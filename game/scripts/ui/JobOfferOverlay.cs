using System;
using Godot;
using HSBM.Domain;

/// <summary>
/// Phone-style inbound offer: another school put term and salary on paper.
/// Closing without ACCEPT / DECLINE leaves the call on the home banner.
/// </summary>
public partial class JobOfferOverlay : Control
{
	private static readonly Color TextPrimary = new(0.956863f, 0.964706f, 0.972549f, 1f);
	private static readonly Color TextMuted = new(0.55f, 0.58f, 0.64f, 1f);
	private static readonly Color Accent = new(0.772549f, 0.635294f, 0.325490f, 1f);
	private static readonly Color AccentHover = new(0.850980f, 0.729412f, 0.443137f, 1f);
	private static readonly Color TextOnAccent = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color Green = new(0.239216f, 0.862745f, 0.450980f, 1f);
	private static readonly Color Urgent = new(0.906f, 0.298f, 0.235f, 1f);
	private static readonly Color Hairline = new(0.17f, 0.19f, 0.24f, 1f);
	private static readonly Color Gold = new(0.772549f, 0.635294f, 0.325490f, 1f);

	private GameSession _session = null!;
	private FontFile _bold = null!;
	private FontFile _semibold = null!;
	private FontFile _medium = null!;
	private VBoxContainer _body = null!;
	private bool _built;
	private CoachingJobOffer? _offer;

	public event Action? Dismissed;

	public event Action? Resolved;

	public override void _Ready()
	{
		EnsureBuilt();
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (!Visible || !ev.IsActionPressed("ui_cancel"))
		{
			return;
		}

		UiSounds.Play(UiSound.Back);
		Dismiss();
		GetViewport().SetInputAsHandled();
	}

	public void Present()
	{
		EnsureBuilt();
		if (!BindNext())
		{
			Visible = false;
			return;
		}

		Visible = true;
		MoveToFront();
		UiSounds.Play(UiSound.Notification);
	}

	public void Dismiss()
	{
		if (!Visible)
		{
			return;
		}

		Visible = false;
		Dismissed?.Invoke();
	}

	private bool BindNext()
	{
		_offer = JobOfferDesk.Next(_session);
		if (_offer == null)
		{
			return false;
		}

		foreach (Node child in _body.GetChildren())
		{
			_body.RemoveChild(child);
			child.QueueFree();
		}

		CoachingJobOffer offer = _offer;
		_body.AddChild(MakeText("NEW MESSAGE", _semibold, 11, Accent, HorizontalAlignment.Center));
		_body.AddChild(MakeText("JOB OFFER", _bold, 28, Gold, HorizontalAlignment.Center));
		_body.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 2), Color = Gold });
		_body.AddChild(MakeText(
			offer.SchoolFullName.ToUpperInvariant(),
			_bold,
			20,
			TextPrimary,
			HorizontalAlignment.Center));
		_body.AddChild(MakeText("Head Coach", _medium, 14, TextMuted, HorizontalAlignment.Center));

		_body.AddChild(MakeStat("PROGRAM REPUTATION", offer.Reputation.ToString(), RepColor(offer.Reputation)));
		_body.AddChild(MakeStat("FACILITIES", offer.Facilities.ToLabel(), GradeColor(offer.Facilities)));
		_body.AddChild(MakeStat("EXPECTED RESULT", offer.ExpectationLabel, ExpectationColor(offer.TargetExpectation)));
		_body.AddChild(MakeStat("SALARY", $"{offer.SalaryLabel} / year", Green));

		_body.AddChild(new ColorRect { CustomMinimumSize = new Vector2(0, 1), Color = Hairline });
		_body.AddChild(MakeText("CONTRACT", _semibold, 11, Gold, HorizontalAlignment.Left));
		_body.AddChild(MakeText(offer.TermLabel, _bold, 26, TextPrimary, HorizontalAlignment.Left));
		var security = MakeText(
			"Paid from the baseball budget. Term is job security.",
			_medium,
			12,
			TextMuted,
			HorizontalAlignment.Left);
		security.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_body.AddChild(security);

		var actions = new VBoxContainer();
		actions.AddThemeConstantOverride("separation", 8);
		Button accept = MakeFilled("ACCEPT", Gold, TextOnAccent);
		accept.Pressed += OnAccept;
		actions.AddChild(accept);
		Button decline = MakeOutlined("DECLINE");
		decline.Pressed += OnDecline;
		actions.AddChild(decline);
		_body.AddChild(actions);
		return true;
	}

	private void OnAccept()
	{
		if (_offer == null)
		{
			return;
		}

		if (!_session.TryAcceptJobOffer(_offer.Id))
		{
			UiSounds.Play(UiSound.Error);
			return;
		}

		UiSounds.Play(UiSound.Confirm);
		Visible = false;
		Resolved?.Invoke();
	}

	private void OnDecline()
	{
		if (_offer == null)
		{
			return;
		}

		if (!_session.TryDeclineJobOffer(_offer.Id))
		{
			UiSounds.Play(UiSound.Error);
			return;
		}

		UiSounds.Play(UiSound.Back);
		if (BindNext())
		{
			return;
		}

		Visible = false;
		Resolved?.Invoke();
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
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var dim = new ColorRect
		{
			Color = new Color(0.02f, 0.03f, 0.04f, 0.82f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		dim.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				UiSounds.Play(UiSound.Back);
				Dismiss();
			}
		};
		AddChild(dim);

		var center = new CenterContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);

		var bezel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(336, 0),
			MouseFilter = MouseFilterEnum.Stop,
		};
		bezel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.07f, 0.09f, 1f),
			BorderColor = new Color(0.18f, 0.20f, 0.24f, 1f),
			BorderWidthLeft = 8,
			BorderWidthTop = 10,
			BorderWidthRight = 8,
			BorderWidthBottom = 12,
			CornerRadiusTopLeft = 28,
			CornerRadiusTopRight = 28,
			CornerRadiusBottomRight = 28,
			CornerRadiusBottomLeft = 28,
			ContentMarginLeft = 4,
			ContentMarginTop = 4,
			ContentMarginRight = 4,
			ContentMarginBottom = 4,
			CornerDetail = 8,
		});
		center.AddChild(bezel);

		var screen = new PanelContainer();
		screen.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.055f, 0.062f, 0.078f, 1f),
			CornerRadiusTopLeft = 20,
			CornerRadiusTopRight = 20,
			CornerRadiusBottomRight = 20,
			CornerRadiusBottomLeft = 20,
			ContentMarginLeft = 18,
			ContentMarginTop = 14,
			ContentMarginRight = 18,
			ContentMarginBottom = 18,
			CornerDetail = 8,
		});
		bezel.AddChild(screen);

		var layout = new VBoxContainer();
		layout.AddThemeConstantOverride("separation", 10);
		screen.AddChild(layout);
		layout.AddChild(BuildStatusBar());

		_body = new VBoxContainer();
		_body.AddThemeConstantOverride("separation", 8);
		layout.AddChild(_body);
	}

	private Control BuildStatusBar()
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var carrier = MakeText("ATHLETIC OFFICE", _semibold, 11, TextMuted, HorizontalAlignment.Left);
		carrier.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(carrier);
		Button close = new()
		{
			Text = "CLOSE",
			Flat = true,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		close.AddThemeFontOverride("font", _semibold);
		close.AddThemeFontSizeOverride("font_size", 11);
		close.AddThemeColorOverride("font_color", TextMuted);
		close.AddThemeColorOverride("font_hover_color", TextPrimary);
		close.Pressed += () =>
		{
			UiSounds.Play(UiSound.Back);
			Dismiss();
		};
		UiSounds.SetClick(close, UiClickSound.Silent);
		row.AddChild(close);
		return row;
	}

	private Control MakeStat(string label, string value, Color valueColor)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var name = MakeText(label, _medium, 12, TextMuted, HorizontalAlignment.Left);
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddChild(name);
		row.AddChild(MakeText(value, _bold, 16, valueColor, HorizontalAlignment.Right));
		return row;
	}

	private Button MakeFilled(string text, Color bg, Color ink)
	{
		var button = new Button
		{
			Text = text,
			Flat = false,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			CustomMinimumSize = new Vector2(0, 40),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		button.AddThemeFontOverride("font", _bold);
		button.AddThemeFontSizeOverride("font_size", 15);
		button.AddThemeColorOverride("font_color", ink);
		button.AddThemeColorOverride("font_hover_color", ink);
		button.AddThemeColorOverride("font_pressed_color", ink);
		button.AddThemeColorOverride("font_focus_color", ink);
		button.AddThemeStyleboxOverride("normal", MakeFill(bg));
		button.AddThemeStyleboxOverride("hover", MakeFill(AccentHover));
		button.AddThemeStyleboxOverride("pressed", MakeFill(AccentHover));
		button.AddThemeStyleboxOverride("focus", MakeFill(AccentHover));
		UiSounds.SetClick(button, UiClickSound.Silent);
		return button;
	}

	private Button MakeOutlined(string text)
	{
		var button = new Button
		{
			Text = text,
			Flat = false,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			CustomMinimumSize = new Vector2(0, 40),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		button.AddThemeFontOverride("font", _bold);
		button.AddThemeFontSizeOverride("font_size", 15);
		button.AddThemeColorOverride("font_color", TextPrimary);
		button.AddThemeColorOverride("font_hover_color", Colors.White);
		button.AddThemeColorOverride("font_pressed_color", TextPrimary);
		button.AddThemeStyleboxOverride("normal", MakeOutline(new Color(0.055f, 0.062f, 0.078f, 1f), Hairline));
		button.AddThemeStyleboxOverride("hover", MakeOutline(new Color(0.184314f, 0.203922f, 0.239216f, 1f), Urgent));
		button.AddThemeStyleboxOverride("pressed", MakeOutline(new Color(0.184314f, 0.203922f, 0.239216f, 1f), Urgent));
		UiSounds.SetClick(button, UiClickSound.Silent);
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

	private static StyleBoxFlat MakeFill(Color bg) => new()
	{
		BgColor = bg,
		ContentMarginLeft = 14,
		ContentMarginTop = 10,
		ContentMarginRight = 14,
		ContentMarginBottom = 10,
		CornerRadiusTopLeft = 8,
		CornerRadiusTopRight = 8,
		CornerRadiusBottomRight = 8,
		CornerRadiusBottomLeft = 8,
		CornerDetail = 4,
	};

	private static StyleBoxFlat MakeOutline(Color bg, Color border) => new()
	{
		BgColor = bg,
		BorderColor = border,
		BorderWidthLeft = 1,
		BorderWidthTop = 1,
		BorderWidthRight = 1,
		BorderWidthBottom = 1,
		ContentMarginLeft = 14,
		ContentMarginTop = 10,
		ContentMarginRight = 14,
		ContentMarginBottom = 10,
		CornerRadiusTopLeft = 8,
		CornerRadiusTopRight = 8,
		CornerRadiusBottomRight = 8,
		CornerRadiusBottomLeft = 8,
		CornerDetail = 4,
	};

	private static Color RepColor(int reputation)
	{
		if (reputation >= 75)
		{
			return Green;
		}

		if (reputation <= 40)
		{
			return Urgent;
		}

		return Accent;
	}

	private static Color ExpectationColor(BoardExpectation expectation) => expectation switch
	{
		BoardExpectation.Rebuild => Urgent,
		BoardExpectation.WinDistrict or BoardExpectation.StateTitle => Green,
		_ => Accent,
	};

	private static Color GradeColor(LetterGrade grade) => grade switch
	{
		>= LetterGrade.BMinus => Green,
		<= LetterGrade.DPlus => Urgent,
		_ => Accent,
	};
}
