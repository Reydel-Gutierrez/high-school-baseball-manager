using Godot;

public partial class Boot : Node
{
    public override void _Ready()
    {
        GameSettings.ApplyDisplay();
        GD.Print("High School Baseball Manager booted successfully.");
        GetTree().ChangeSceneToFile("res://scenes/main_menu/main_menu.tscn");
    }
}