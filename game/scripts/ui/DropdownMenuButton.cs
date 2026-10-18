using Godot;

public partial class DropdownMenuButton : MenuButton
{
	[Export]
	public float MinimumPopupWidth { get; set; } = 180.0f;

	[Export]
	public float MaximumPopupWidth { get; set; } = 280.0f;

	// Hover opens the menu; the click that usually follows would toggle it
	// shut again. The first close after a hover-open, with the pointer still
	// on the button, is that click — reopen instead. A second click closes.
	private const ulong HoverClickGraceMs = 15000;
	private ulong _hoverOpenedAt;

	public override void _Ready()
	{
		Flat = false;
		MouseEntered += OpenDropdown;
		FocusEntered += OpenDropdown;

		PopupMenu popup = GetPopup();
		popup.HideOnItemSelection = true;
		popup.HideOnCheckableItemSelection = true;

		// Keep open-menu text light even if a parent theme changes pressed colors.
		AddThemeColorOverride("font_pressed_color", new Color(0.909804f, 0.929412f, 0.949020f));
		AddThemeColorOverride("font_hover_pressed_color", Colors.White);

		popup.AboutToPopup += ConfigurePopup;
		popup.PopupHide += OnPopupHidden;
		ConfigurePopup();
	}

	private void ConfigurePopup()
	{
		PopupMenu popup = GetPopup();

		int minWidth = Mathf.RoundToInt(MinimumPopupWidth);
		int maxWidth = Mathf.Max(minWidth, Mathf.RoundToInt(MaximumPopupWidth));

		popup.MinSize = new Vector2I(minWidth, 0);
		popup.MaxSize = new Vector2I(maxWidth, 0);

		// Prefer natural content width when larger than the minimum.
		popup.ResetSize();
		Vector2 contentSize = popup.GetContentsMinimumSize();
		int targetWidth = Mathf.Clamp(Mathf.CeilToInt(contentSize.X) + 8, minWidth, maxWidth);
		popup.Size = new Vector2I(targetWidth, popup.Size.Y);
	}

	private void OpenDropdown()
	{
		PopupMenu popup = GetPopup();

		if (!Disabled && !popup.Visible)
		{
			ConfigurePopup();
			ShowPopup();
			_hoverOpenedAt = Time.GetTicksMsec();
		}
	}

	private void OnPopupHidden()
	{
		if (_hoverOpenedAt == 0)
		{
			return;
		}

		ulong elapsed = Time.GetTicksMsec() - _hoverOpenedAt;
		_hoverOpenedAt = 0;
		bool pointerOnButton = GetGlobalRect().HasPoint(GetGlobalMousePosition());
		if (elapsed <= HoverClickGraceMs && pointerOnButton && !Disabled)
		{
			CallDeferred(MethodName.ReopenAfterClick);
		}
	}

	private void ReopenAfterClick()
	{
		if (!GetPopup().Visible)
		{
			ConfigurePopup();
			ShowPopup();
		}
	}
}
