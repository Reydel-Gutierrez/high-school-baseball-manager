using Godot;
using HSBM.Domain;

/// <summary>
/// Compact JV / Varsity switch for the top bar. Writes through GameSession
/// so every screen can react to the same active team.
/// </summary>
public partial class TeamLevelToggle : HBoxContainer
{
	private static readonly Color TextPrimary = new(0.800000f, 0.819608f, 0.850980f, 1f);
	private static readonly Color TextActive = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color FrameBg = new(0.121569f, 0.137255f, 0.160784f, 1f);
	private static readonly Color FrameBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);

	private GameSession _session = null!;
	private Button _jvButton = null!;
	private Button _varsityButton = null!;
	private FontFile _semibold = null!;

	public override void _Ready()
	{
		_session = GetNode<GameSession>("/root/GameSession");
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");

		Build();
		_session.ActiveTeamChanged += OnActiveTeamChanged;
		Refresh();
	}

	public override void _ExitTree()
	{
		if (_session != null)
		{
			_session.ActiveTeamChanged -= OnActiveTeamChanged;
		}
	}

	private void OnActiveTeamChanged(TeamIdentity _) => Refresh();

	private void Build()
	{
		AddThemeConstantOverride("separation", 0);
		Alignment = AlignmentMode.Center;
		SizeFlagsVertical = SizeFlags.ShrinkCenter;

		var frame = new PanelContainer();
		frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = FrameBg,
			DrawCenter = true,
			BorderColor = FrameBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 2,
			ContentMarginTop = 2,
			ContentMarginRight = 2,
			ContentMarginBottom = 2,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerDetail = 4,
		});
		AddChild(frame);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 0);
		frame.AddChild(row);

		_jvButton = MakeSegment("JV", TeamLevel.JuniorVarsity);
		_varsityButton = MakeSegment("VARSITY", TeamLevel.Varsity);
		row.AddChild(_jvButton);
		row.AddChild(_varsityButton);
	}

	private Button MakeSegment(string text, TeamLevel level)
	{
		var button = new Button
		{
			Text = text,
			Flat = false,
			FocusMode = FocusModeEnum.All,
			CustomMinimumSize = new Vector2(text == "JV" ? 36 : 70, 24),
			TooltipText = $"View the {level.ToLabel()} team",
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		button.Pressed += () => _session.SetActiveLevel(level);
		return button;
	}

	private void Refresh()
	{
		ApplySegment(_jvButton, _session.ActiveTeam.Level == TeamLevel.JuniorVarsity);
		ApplySegment(_varsityButton, _session.ActiveTeam.Level == TeamLevel.Varsity);
	}

	private static void ApplySegment(Button button, bool isActive)
	{
		Color font = isActive ? TextActive : TextPrimary;
		button.AddThemeColorOverride("font_color", font);
		button.AddThemeColorOverride("font_hover_color", isActive ? TextActive : Colors.White);
		button.AddThemeColorOverride("font_pressed_color", font);
		button.AddThemeColorOverride("font_focus_color", isActive ? TextActive : Colors.White);
		button.AddThemeColorOverride("font_hover_pressed_color", isActive ? TextActive : Colors.White);

		button.AddThemeStyleboxOverride("normal", MakePill(isActive ? Accent : Colors.Transparent));
		button.AddThemeStyleboxOverride("hover", MakePill(isActive ? AccentHover : HoverBg));
		button.AddThemeStyleboxOverride("pressed", MakePill(isActive ? AccentHover : HoverBg));
		button.AddThemeStyleboxOverride("focus", MakePill(isActive ? AccentHover : HoverBg));
		button.AddThemeStyleboxOverride("hover_pressed", MakePill(isActive ? AccentHover : HoverBg));
	}

	private static StyleBoxFlat MakePill(Color bg)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			DrawCenter = true,
			ContentMarginLeft = 10,
			ContentMarginTop = 4,
			ContentMarginRight = 10,
			ContentMarginBottom = 4,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerDetail = 4,
		};
	}
}
