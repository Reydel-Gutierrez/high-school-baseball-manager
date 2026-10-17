using System;
using Godot;

/// <summary>
/// Compact prev / play / next transport that lives on the top bar so
/// soundtrack state is not owned by the current page.
/// </summary>
public partial class SoundtrackControls : HBoxContainer
{
	private static readonly Color TextPrimary = new(0.800000f, 0.819608f, 0.850980f, 1f);
	private static readonly Color TextActive = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f);
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color FrameBg = new(0.121569f, 0.137255f, 0.160784f, 1f);
	private static readonly Color FrameBorder = new(0.243137f, 0.262745f, 0.301961f, 1f);

	private FontFile _semibold = null!;
	private Button _play = null!;
	private Label _title = null!;

	public override void _Ready()
	{
		_semibold = GD.Load<FontFile>("res://assets/fonts/BarlowCondensed-SemiBold.ttf");
		AddThemeConstantOverride("separation", 0);
		Alignment = AlignmentMode.Center;
		SizeFlagsVertical = SizeFlags.ShrinkCenter;

		var frame = new PanelContainer();
		frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = FrameBg,
			BorderColor = FrameBorder,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			ContentMarginLeft = 4,
			ContentMarginTop = 2,
			ContentMarginRight = 8,
			ContentMarginBottom = 2,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomRight = 4,
			CornerRadiusBottomLeft = 4,
		});
		AddChild(frame);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 4);
		frame.AddChild(row);

		row.AddChild(MakeButton("‹", "Previous track", () => AudioManager.Current.PreviousTrack()));
		_play = MakeButton("PLAY", "Play or pause music", () =>
		{
			AudioManager.Current.TogglePauseMusic();
			Refresh();
		});
		_play.CustomMinimumSize = new Vector2(52, 24);
		row.AddChild(_play);
		row.AddChild(MakeButton("›", "Next track", () => AudioManager.Current.NextTrack()));

		_title = new Label
		{
			Text = "MUSIC",
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			CustomMinimumSize = new Vector2(88, 0),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_title.AddThemeFontOverride("font", _semibold);
		_title.AddThemeFontSizeOverride("font_size", 11);
		_title.AddThemeColorOverride("font_color", TextPrimary);
		row.AddChild(_title);

		if (AudioManager.Current != null)
		{
			AudioManager.Current.SoundtrackChanged += Refresh;
		}

		Refresh();
	}

	public override void _ExitTree()
	{
		if (AudioManager.Current != null)
		{
			AudioManager.Current.SoundtrackChanged -= Refresh;
		}
	}

	private void Refresh()
	{
		if (_play == null || AudioManager.Current == null)
		{
			return;
		}

		AudioManager audio = AudioManager.Current;
		bool playing = audio.IsMusicPlaying && !audio.IsMusicPaused;
		_play.Text = playing ? "PAUSE" : "PLAY";
		ApplyButton(_play, playing);
		string title = audio.CurrentTrackTitle;
		_title.Text = string.IsNullOrEmpty(title) ? "MUSIC" : title.ToUpperInvariant();
		TooltipText = string.IsNullOrEmpty(title)
			? "Soundtrack"
			: $"{title}  ·  Track {audio.CurrentTrackIndex + 1} of {audio.TrackCount}";
	}

	private Button MakeButton(string text, string tip, Action pressed)
	{
		var button = new Button
		{
			Text = text,
			Flat = false,
			FocusMode = FocusModeEnum.All,
			CustomMinimumSize = new Vector2(28, 24),
			TooltipText = tip,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		button.AddThemeFontOverride("font", _semibold);
		button.AddThemeFontSizeOverride("font_size", 12);
		ApplyButton(button, false);
		button.Pressed += pressed;
		return button;
	}

	private static void ApplyButton(Button button, bool active)
	{
		button.AddThemeColorOverride("font_color", active ? TextActive : TextPrimary);
		button.AddThemeColorOverride("font_hover_color", active ? TextActive : Colors.White);
		button.AddThemeStyleboxOverride("normal", MakePill(active ? Accent : Colors.Transparent));
		button.AddThemeStyleboxOverride("hover", MakePill(active ? AccentHover : HoverBg));
		button.AddThemeStyleboxOverride("pressed", MakePill(active ? AccentHover : HoverBg));
		button.AddThemeStyleboxOverride("focus", MakePill(active ? AccentHover : HoverBg));
	}

	private static StyleBoxFlat MakePill(Color bg) =>
		new()
		{
			BgColor = bg,
			DrawCenter = true,
			ContentMarginLeft = 8,
			ContentMarginTop = 4,
			ContentMarginRight = 8,
			ContentMarginBottom = 4,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomRight = 3,
			CornerRadiusBottomLeft = 3,
		};
}
