using System.Collections.Generic;
using Godot;

/// <summary>
/// Manages compact top-bar navigation active styling for top-level items.
/// </summary>
public partial class TopBarNavigation : PanelContainer
{
	public const string HomeKey = "home";
	public const string OrganizationKey = "organization";
	public const string ClubhouseKey = "clubhouse";
	public const string DistrictHubKey = "district_hub";
	public const string FinancesKey = "finances";
	public const string ScoutingKey = "scouting";
	public const string CareerKey = "career";
	public const string SettingsKey = "settings";

	[Export]
	public string InitialNavigationKey { get; set; } = HomeKey;

	private readonly Dictionary<string, BaseButton> _items = new();
	private string _activeKey = string.Empty;

	private StyleBoxFlat _inactiveNormal = null!;
	private StyleBoxFlat _inactiveHover = null!;
	private StyleBoxFlat _inactiveFocus = null!;
	private StyleBoxFlat _activeNormal = null!;
	private StyleBoxFlat _activeHover = null!;
	private StyleBoxFlat _activeFocus = null!;
	private StyleBoxFlat _iconInactiveNormal = null!;
	private StyleBoxFlat _iconInactiveHover = null!;
	private StyleBoxFlat _iconInactiveFocus = null!;
	private StyleBoxFlat _iconActiveNormal = null!;
	private StyleBoxFlat _iconActiveHover = null!;
	private StyleBoxFlat _iconActiveFocus = null!;

	// Prototype palette: dark navy bar, amber active pill, cool off-white labels.
	private static readonly Color TextPrimary = new(0.800000f, 0.819608f, 0.850980f, 1f); // #CCD1D9
	private static readonly Color TextActive = new(0.070588f, 0.074510f, 0.086275f, 1f);
	private static readonly Color Accent = new(0.956863f, 0.643137f, 0.109804f, 1f); // #F4A41C
	private static readonly Color AccentHover = new(0.980392f, 0.721569f, 0.200000f, 1f);
	private static readonly Color HoverBg = new(0.184314f, 0.203922f, 0.239216f, 1f);
	private static readonly Color FocusBorder = new(0.988235f, 0.843137f, 0.450980f, 1f);

	public override void _Ready()
	{
		BuildStyles();
		ResolveNavItems();
		SetActiveNavigation(InitialNavigationKey);
	}

	public void SetActiveNavigation(string navigationKey)
	{
		if (string.IsNullOrWhiteSpace(navigationKey))
		{
			return;
		}

		string key = navigationKey.Trim().ToLowerInvariant();
		if (!_items.ContainsKey(key))
		{
			GD.PushWarning($"TopBarNavigation: unknown navigation key '{navigationKey}'.");
			return;
		}

		_activeKey = key;

		foreach (KeyValuePair<string, BaseButton> pair in _items)
		{
			ApplyItemState(pair.Value, pair.Key == _activeKey);
		}
	}

	public string GetActiveNavigation() => _activeKey;

	private void ResolveNavItems()
	{
		_items.Clear();

		TryRegister(HomeKey, "TopBarMargin/HBoxContainer/NavigationRow/HomeButton");
		TryRegister(OrganizationKey, "TopBarMargin/HBoxContainer/NavigationRow/OrganizationMenu");
		TryRegister(ClubhouseKey, "TopBarMargin/HBoxContainer/NavigationRow/ClubhouseMenu");
		TryRegister(DistrictHubKey, "TopBarMargin/HBoxContainer/NavigationRow/DistrictHubMenu");
		TryRegister(FinancesKey, "TopBarMargin/HBoxContainer/NavigationRow/FinancesMenu");
		TryRegister(ScoutingKey, "TopBarMargin/HBoxContainer/NavigationRow/ScoutingMenu");
		TryRegister(CareerKey, "TopBarMargin/HBoxContainer/NavigationRow/CareerButton");
		TryRegister(SettingsKey, "TopBarMargin/HBoxContainer/RightCluster/SettingsButton");
	}

	private void TryRegister(string key, string relativePath)
	{
		BaseButton? button = GetNodeOrNull<BaseButton>(relativePath);
		if (button == null)
		{
			GD.PushWarning($"TopBarNavigation: missing nav control at '{relativePath}'.");
			return;
		}

		// Flat mode can suppress normal stylebox fill in some Button/MenuButton draws.
		if (button is Button plainButton)
		{
			plainButton.Flat = false;
		}

		button.FocusMode = Control.FocusModeEnum.All;
		_items[key] = button;
	}

	private void BuildStyles()
	{
		_inactiveNormal = MakeFlat(Colors.Transparent, 0, Colors.Transparent, 4, 14, 6);
		_inactiveHover = MakeFlat(HoverBg, 0, Colors.Transparent, 4, 14, 6);
		_inactiveFocus = MakeFlat(HoverBg, 1, FocusBorder, 4, 14, 6);

		_activeNormal = MakeFlat(Accent, 0, Colors.Transparent, 4, 14, 6);
		_activeHover = MakeFlat(AccentHover, 0, Colors.Transparent, 4, 14, 6);
		_activeFocus = MakeFlat(AccentHover, 1, FocusBorder, 4, 14, 6);

		_iconInactiveNormal = MakeFlat(Colors.Transparent, 0, Colors.Transparent, 4, 6, 6);
		_iconInactiveHover = MakeFlat(HoverBg, 0, Colors.Transparent, 4, 6, 6);
		_iconInactiveFocus = MakeFlat(HoverBg, 1, FocusBorder, 4, 6, 6);

		_iconActiveNormal = MakeFlat(Accent, 0, Colors.Transparent, 4, 6, 6);
		_iconActiveHover = MakeFlat(AccentHover, 0, Colors.Transparent, 4, 6, 6);
		_iconActiveFocus = MakeFlat(AccentHover, 1, FocusBorder, 4, 6, 6);
	}

	private static StyleBoxFlat MakeFlat(
		Color bg,
		int borderWidth,
		Color borderColor,
		int radius,
		float marginX,
		float marginY)
	{
		return new StyleBoxFlat
		{
			BgColor = bg,
			DrawCenter = true,
			ContentMarginLeft = marginX,
			ContentMarginTop = marginY,
			ContentMarginRight = marginX,
			ContentMarginBottom = marginY,
			BorderWidthLeft = borderWidth,
			BorderWidthTop = borderWidth,
			BorderWidthRight = borderWidth,
			BorderWidthBottom = borderWidth,
			BorderColor = borderColor,
			CornerRadiusTopLeft = radius,
			CornerRadiusTopRight = radius,
			CornerRadiusBottomRight = radius,
			CornerRadiusBottomLeft = radius,
			CornerDetail = 5,
		};
	}

	private void ApplyItemState(BaseButton item, bool isActive)
	{
		bool isIconButton = item.Name == "SettingsButton";

		if (isActive)
		{
			item.AddThemeColorOverride("font_color", TextActive);
			item.AddThemeColorOverride("font_hover_color", TextActive);
			item.AddThemeColorOverride("font_pressed_color", TextActive);
			item.AddThemeColorOverride("font_focus_color", TextActive);
			item.AddThemeColorOverride("font_hover_pressed_color", TextActive);
			item.AddThemeColorOverride("icon_normal_color", TextActive);
			item.AddThemeColorOverride("icon_hover_color", TextActive);
			item.AddThemeColorOverride("icon_pressed_color", TextActive);
			item.AddThemeColorOverride("icon_focus_color", TextActive);

			item.AddThemeStyleboxOverride("normal", isIconButton ? _iconActiveNormal : _activeNormal);
			item.AddThemeStyleboxOverride("hover", isIconButton ? _iconActiveHover : _activeHover);
			item.AddThemeStyleboxOverride("pressed", isIconButton ? _iconActiveHover : _activeHover);
			item.AddThemeStyleboxOverride("focus", isIconButton ? _iconActiveFocus : _activeFocus);
			item.AddThemeStyleboxOverride("hover_pressed", isIconButton ? _iconActiveHover : _activeHover);
			return;
		}

		item.AddThemeColorOverride("font_color", TextPrimary);
		item.AddThemeColorOverride("font_hover_color", Colors.White);
		item.AddThemeColorOverride("font_pressed_color", TextPrimary);
		item.AddThemeColorOverride("font_focus_color", Colors.White);
		item.AddThemeColorOverride("font_hover_pressed_color", Colors.White);
		item.AddThemeColorOverride("icon_normal_color", TextPrimary);
		item.AddThemeColorOverride("icon_hover_color", Colors.White);
		item.AddThemeColorOverride("icon_pressed_color", TextPrimary);
		item.AddThemeColorOverride("icon_focus_color", Colors.White);

		item.AddThemeStyleboxOverride("normal", isIconButton ? _iconInactiveNormal : _inactiveNormal);
		item.AddThemeStyleboxOverride("hover", isIconButton ? _iconInactiveHover : _inactiveHover);
		item.AddThemeStyleboxOverride("pressed", isIconButton ? _iconInactiveHover : _inactiveHover);
		item.AddThemeStyleboxOverride("focus", isIconButton ? _iconInactiveFocus : _inactiveFocus);
		item.AddThemeStyleboxOverride("hover_pressed", isIconButton ? _iconInactiveHover : _inactiveHover);
	}
}
