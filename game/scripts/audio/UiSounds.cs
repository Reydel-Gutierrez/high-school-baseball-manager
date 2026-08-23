using System;
using Godot;

/// <summary>
/// Reusable UI audio helper. Existing and future BaseButton / PopupMenu
/// controls are wired automatically. Override a click with SetClick or the
/// `ui_click` metadata key (select, navigate, back, confirm, advance_day, silent).
/// </summary>
public static class UiSounds
{
	public const string ClickMeta = "ui_click";
	private const string BoundMeta = "ui_sounds_bound";

	public static int BoundButtonCount { get; private set; }
	public static int BoundPopupCount { get; private set; }
	public static int BoundClickableCount { get; private set; }

	public static void Play(UiSound sound) => AudioManager.Current?.PlayUi(sound);

	public static void PlaySfx(SfxId sfx) => AudioManager.Current?.PlaySfx(sfx);

	public static void SetClick(Node node, UiClickSound sound)
	{
		node.SetMeta(ClickMeta, RoleName(sound));
	}

	public static void Bind(Control control, UiClickSound click = UiClickSound.Auto)
	{
		if (click != UiClickSound.Auto)
		{
			SetClick(control, click);
		}

		TryBind(control);
	}

	public static void Attach(AudioManager manager)
	{
		SceneTree tree = manager.GetTree();
		tree.NodeAdded += OnNodeAdded;
		BindTree(tree.Root);
	}

	public static void Detach(AudioManager manager)
	{
		SceneTree? tree = manager.GetTree();
		if (tree != null)
		{
			tree.NodeAdded -= OnNodeAdded;
		}
	}

	private static void BindTree(Node node)
	{
		TryBind(node);
		foreach (Node child in node.GetChildren())
		{
			BindTree(child);
		}
	}

	private static void OnNodeAdded(Node node) => TryBind(node);

	private static void TryBind(Node node)
	{
		if (node.HasMeta(BoundMeta))
		{
			return;
		}

		switch (node)
		{
			case BaseButton button:
				BindButton(button);
				break;
			case PopupMenu popup:
				BindPopup(popup);
				break;
			case Control control when IsClickableControl(control):
				BindClickable(control);
				break;
		}
	}

	private static bool IsClickableControl(Control control) =>
		control.MouseDefaultCursorShape == Control.CursorShape.PointingHand
		&& control.MouseFilter != Control.MouseFilterEnum.Ignore;

	private static void BindButton(BaseButton button)
	{
		button.SetMeta(BoundMeta, true);
		BoundButtonCount++;
		button.MouseEntered += () =>
		{
			if (button.IsVisibleInTree() && !button.Disabled)
			{
				Play(UiSound.Hover);
			}
		};
		button.Pressed += () =>
		{
			if (button.Disabled)
			{
				return;
			}

			string text = button is Button labeled ? labeled.Text : button.Name;
			UiSound? sound = ResolveClick(button, text);
			if (sound.HasValue)
			{
				Play(sound.Value);
			}
		};
	}

	private static void BindPopup(PopupMenu popup)
	{
		popup.SetMeta(BoundMeta, true);
		BoundPopupCount++;
		popup.IdFocused += _ =>
		{
			if (popup.Visible)
			{
				Play(UiSound.Hover);
			}
		};
		popup.IdPressed += id =>
		{
			UiSound? sound = ResolveClick(popup, ReadPopupItemText(popup, (int)id));
			if (sound.HasValue)
			{
				Play(sound.Value);
			}
		};
	}

	private static string ReadPopupItemText(PopupMenu popup, int id)
	{
		for (int i = 0; i < popup.ItemCount; i++)
		{
			if (popup.GetItemId(i) == id)
			{
				return popup.GetItemText(i);
			}
		}

		return popup.Name;
	}

	private static void BindClickable(Control control)
	{
		control.SetMeta(BoundMeta, true);
		BoundClickableCount++;
		control.MouseEntered += () =>
		{
			if (control.IsVisibleInTree())
			{
				Play(UiSound.Hover);
			}
		};
		control.GuiInput += ev =>
		{
			if (ev is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
			{
				return;
			}

			UiSound? sound = ResolveClick(control, control.Name);
			if (sound.HasValue)
			{
				Play(sound.Value);
			}
		};
	}

	private static UiSound? ResolveClick(Node node, string? text)
	{
		UiClickSound role = ReadRole(node);
		if (role == UiClickSound.Auto && node.GetParent() is Node parent)
		{
			UiClickSound inherited = ReadRole(parent);
			if (inherited != UiClickSound.Auto)
			{
				role = inherited;
			}
		}

		if (role == UiClickSound.Auto)
		{
			role = InferClick(text, node.Name);
		}

		return role switch
		{
			UiClickSound.Silent => null,
			UiClickSound.Select => UiSound.Select,
			UiClickSound.Navigate => UiSound.Navigate,
			UiClickSound.Back => UiSound.Back,
			UiClickSound.Confirm => UiSound.Confirm,
			UiClickSound.AdvanceDay => UiSound.AdvanceDay,
			_ => UiSound.Select,
		};
	}

	private static UiClickSound ReadRole(Node node)
	{
		if (!node.HasMeta(ClickMeta))
		{
			return UiClickSound.Auto;
		}

		string value = node.GetMeta(ClickMeta).AsString();
		return value.Trim().ToLowerInvariant() switch
		{
			"silent" or "none" => UiClickSound.Silent,
			"select" => UiClickSound.Select,
			"navigate" => UiClickSound.Navigate,
			"back" => UiClickSound.Back,
			"confirm" => UiClickSound.Confirm,
			"advance" or "advance_day" or "advanceday" => UiClickSound.AdvanceDay,
			"auto" => UiClickSound.Auto,
			_ => UiClickSound.Auto,
		};
	}

	private static string RoleName(UiClickSound sound) => sound switch
	{
		UiClickSound.Silent => "silent",
		UiClickSound.Select => "select",
		UiClickSound.Navigate => "navigate",
		UiClickSound.Back => "back",
		UiClickSound.Confirm => "confirm",
		UiClickSound.AdvanceDay => "advance_day",
		_ => "auto",
	};

	private static UiClickSound InferClick(string? text, string nodeName)
	{
		string label = (text ?? string.Empty).Trim();
		string haystack = $"{label} {nodeName}".ToUpperInvariant();

		if (label.Equals("PLAY", StringComparison.OrdinalIgnoreCase)
			|| haystack.Contains("ADVANCE DAY", StringComparison.Ordinal)
			|| haystack.Contains("SIMULATE", StringComparison.Ordinal))
		{
			return UiClickSound.AdvanceDay;
		}

		if (haystack.Contains("SUBMIT", StringComparison.Ordinal)
			|| haystack.Contains("CONFIRM", StringComparison.Ordinal)
			|| haystack.Contains("APPLY", StringComparison.Ordinal))
		{
			return UiClickSound.Confirm;
		}

		if (haystack.Contains("CLOSE", StringComparison.Ordinal)
			|| haystack.Contains("CANCEL", StringComparison.Ordinal)
			|| haystack.Contains("BACK", StringComparison.Ordinal)
			|| haystack.Contains("ALL TEAMS", StringComparison.Ordinal))
		{
			return UiClickSound.Back;
		}

		return UiClickSound.Select;
	}
}
