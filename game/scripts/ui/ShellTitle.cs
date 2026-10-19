using Godot;

/// <summary>
/// A page that puts its crest, kicker and title in the shell's top bar
/// instead of its own header row, giving the page body that height back.
/// The navigator moves <see cref="ShellTitle"/> into the top bar while the
/// page is showing.
/// </summary>
public interface IShellTitled
{
	Control ShellTitle { get; }
}

public static class ShellTitle
{
	/// <summary>Compact top-bar title: program crest, gold kicker, 34px title.</summary>
	public static HBoxContainer Make(string kicker, string title, out Label titleLabel)
	{
		var row = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		row.AddThemeConstantOverride("separation", 14);
		var mark = new ProgramMark(52) { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		row.AddChild(mark);
		var col = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		col.AddThemeConstantOverride("separation", -6);
		col.AddChild(UiKit.Kicker(kicker));
		titleLabel = UiKit.Text_(title, UiKit.Bold, 34, UiKit.Text);
		col.AddChild(titleLabel);
		row.AddChild(col);
		return row;
	}
}
