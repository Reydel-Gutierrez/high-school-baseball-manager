using Godot;

public partial class Boot : Node
{
    public override void _Ready()
    {
        GameSettings.ApplyDisplay();
        GD.Print("High School Baseball Manager booted successfully.");
        // Deferred: the tree is still adding Boot when _Ready runs.
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://scenes/main_menu/main_menu.tscn");
    }
}