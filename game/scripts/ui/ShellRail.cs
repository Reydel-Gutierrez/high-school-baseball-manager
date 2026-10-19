using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Left navigation rail from the redesign: game mark on top, six sections
/// (Home, Team, Season, League, Scout, Office) as icon + label tiles, and the
/// head coach's headshot at the bottom (opens My Career).
/// </summary>
public partial class ShellRail : PanelContainer
{
	public const string Home = "home";
	public const string Team = "team";
	public const string Season = "season";
	public const string League = "league";
	public const string Scout = "scout";
	public const string Office = "office";

	private static readonly (string Key, string Label, string Path)[] Sections =
	[
		(Home, "HOME", "<path d=\"M3 11l9-7 9 7v9a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z\"/>"),
		(Team, "TEAM", "<circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 20c.5-3.5 3-5.5 6-5.5s5.5 2 6 5.5\"/><circle cx=\"17\" cy=\"9\" r=\"2.5\"/><path d=\"M16 14.5c2.6.2 4.4 2 5 5\"/>"),
		(Season, "SEASON", "<rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\"/>"),
		(League, "LEAGUE", "<path d=\"M4 20V10M10 20V4M16 20v-7M22 20H2\"/>"),
		(Scout, "SCOUT", "<circle cx=\"11\" cy=\"11\" r=\"6\"/><path d=\"M20 20l-4.5-4.5\"/>"),
		(Office, "OFFICE", "<rect x=\"4\" y=\"7\" width=\"16\" height=\"13\" rx=\"2\"/><path d=\"M9 7V5h6v2M4 12h16\"/>"),
	];

	private readonly Dictionary<string, (Button Button, TextureRect Icon, Label Label)> _items = new(StringComparer.Ordinal);
	private readonly Dictionary<string, (Texture2D On, Texture2D Off)> _icons = new(StringComparer.Ordinal);
	private string _active = Home;
	private bool _locked;

	/// <summary>Raised when a section tile is clicked.</summary>
	public event Action<string>? SectionPressed;

	/// <summary>Raised when the coach headshot is clicked.</summary>
	public event Action? CoachPressed;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(88, 0);
		AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = UiKit.RailBg,
			BorderColor = UiKit.Line,
			BorderWidthRight = 1,
			ContentMarginTop = 18,
			ContentMarginBottom = 18,
		});

		var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
		col.AddThemeConstantOverride("separation", 6);
		AddChild(col);

		var brand = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		brand.AddThemeConstantOverride("separation", -6);
		brand.AddChild(UiKit.Text_("HSB", UiKit.Bold, 15, UiKit.Text, HorizontalAlignment.Center));
		brand.AddChild(UiKit.Text_("26", UiKit.Bold, 28, UiKit.Gold, HorizontalAlignment.Center));
		col.AddChild(brand);
		col.AddChild(UiKit.Spacer(0, 18));

		foreach ((string key, string label, string path) in Sections)
		{
			_icons[key] = (Icon(path, UiKit.Gold), Icon(path, UiKit.Muted));
			col.AddChild(BuildItem(key, label));
		}

		col.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
		var coach = new CoachBadge(() => CoachPressed?.Invoke()) { Compact = true, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
		col.AddChild(coach);
		Apply();
	}

	public void SetActive(string section)
	{
		_active = section;
		Apply();
	}

	/// <summary>Before a contract is signed only the Office is open.</summary>
	public void SetLocked(bool locked)
	{
		_locked = locked;
		Apply();
	}

	private Control BuildItem(string key, string label)
	{
		var wrap = new CenterContainer();
		var button = new Button
		{
			CustomMinimumSize = new Vector2(66, 62),
			FocusMode = FocusModeEnum.None,
			MouseDefaultCursorShape = CursorShape.PointingHand,
			TooltipText = label[..1] + label[1..].ToLowerInvariant(),
		};
		var inner = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
		inner.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		inner.AddThemeConstantOverride("separation", 3);
		var icon = new TextureRect
		{
			CustomMinimumSize = new Vector2(24, 24),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		var text = UiKit.Text_(label, UiKit.Semibold, 11, UiKit.Muted, HorizontalAlignment.Center);
		inner.AddChild(icon);
		inner.AddChild(text);
		button.AddChild(inner);
		button.Pressed += () =>
		{
			if (!_locked || key == Office)
			{
				SectionPressed?.Invoke(key);
			}
		};
		_items[key] = (button, icon, text);
		wrap.AddChild(button);
		return wrap;
	}

	private void Apply()
	{
		foreach ((string key, (Button button, TextureRect icon, Label label)) in _items)
		{
			bool on = key == _active;
			bool disabled = _locked && key != Office;
			Color bg = on ? UiKit.Selected : Colors.Transparent;
			button.AddThemeStyleboxOverride("normal", UiKit.Box(bg, 14));
			button.AddThemeStyleboxOverride("hover", UiKit.Box(disabled ? bg : new Color("#151922"), 14));
			button.AddThemeStyleboxOverride("pressed", UiKit.Box(UiKit.Selected, 14));
			button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			icon.Texture = on ? _icons[key].On : _icons[key].Off;
			label.AddThemeColorOverride("font_color", on ? UiKit.Text : UiKit.Muted);
			button.Modulate = disabled ? new Color(1, 1, 1, 0.3f) : Colors.White;
			button.MouseDefaultCursorShape = disabled ? CursorShape.Arrow : CursorShape.PointingHand;
		}
	}

	private static Texture2D Icon(string paths, Color color)
	{
		string svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" width=\"24\" height=\"24\" fill=\"none\" stroke=\"#{color.ToHtml(false)}\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{paths}</svg>";
		var image = new Image();
		image.LoadSvgFromString(svg, 3f);
		image.GenerateMipmaps();
		return ImageTexture.CreateFromImage(image);
	}
}
