using Godot;
using HSBM.Domain;

/// <summary>
/// Right-side school/player inspector used when the active page does not
/// already keep a profile card in its own column.
/// </summary>
public partial class ProfileDock : Control
{
	private static readonly Color Dim = new(0.02f, 0.02f, 0.03f, 0.45f);

	private SchoolProfileCard _school = null!;
	private PlayerProfileCard _player = null!;
	private GameSession _session = null!;

	public override void _Ready()
	{
		_session = GetNode<GameSession>("/root/GameSession");
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ExpandFill;
		MouseFilter = MouseFilterEnum.Ignore;
		Visible = false;

		var dim = new ColorRect
		{
			Color = Dim,
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.Arrow,
		};
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		dim.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
			{
				Dismiss();
			}
		};
		AddChild(dim);

		var panel = new MarginContainer
		{
			CustomMinimumSize = new Vector2(400, 0),
		};
		panel.SetAnchor(Side.Left, 1f);
		panel.SetAnchor(Side.Top, 0f);
		panel.SetAnchor(Side.Right, 1f);
		panel.SetAnchor(Side.Bottom, 1f);
		panel.OffsetLeft = -428;
		panel.OffsetTop = 8;
		panel.OffsetRight = -8;
		panel.OffsetBottom = -8;
		panel.MouseFilter = MouseFilterEnum.Stop;
		AddChild(panel);

		_school = new SchoolProfileCard
		{
			Visible = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_school.SetCloseHandler(Dismiss);
		panel.AddChild(_school);

		_player = new PlayerProfileCard
		{
			Visible = false,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_player.SetCloseHandler(Dismiss);
		panel.AddChild(_player);
	}

	public void ShowSchool(string teamId)
	{
		_player.Visible = false;
		_school.Visible = true;
		_school.Bind(SchoolProfiles.NormalizeId(teamId), _session.ActiveTeam.Level);
		Present();
	}

	public void ShowPlayer(string teamId, string playerName)
	{
		string id = string.IsNullOrWhiteSpace(teamId) ? string.Empty : SchoolProfiles.NormalizeId(teamId);
		PlayerProfileSnapshot? snapshot = PlayerProfiles.Find(id, playerName, _session.ActiveTeam.Level);
		if (snapshot == null)
		{
			return;
		}

		_school.Visible = false;
		_player.Visible = true;
		_player.Bind(snapshot);
		Present();
	}

	public void Dismiss()
	{
		Visible = false;
		MouseFilter = MouseFilterEnum.Ignore;
		_school.Visible = false;
		_player.Visible = false;
	}

	private void Present()
	{
		Visible = true;
		MouseFilter = MouseFilterEnum.Stop;
		MoveToFront();
	}
}
