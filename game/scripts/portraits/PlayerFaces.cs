using HSBM.Domain;
using Godot;

/// <summary>
/// One place for every screen to get a player's engine-rendered face, so
/// the roster row, the lineup chip and the profile card all show the same
/// person in the same school jersey.
/// </summary>
public static class PlayerFaces
{
	/// <summary>Headshot for a world player, or null when no appearance is known.</summary>
	public static PersonPortrait? For(Player? player, float width, float height, bool circle = false)
	{
		if (player?.Appearance == null)
		{
			return null;
		}

		(Color jersey, Color trim) = TeamColors.For(player.OrganizationId);
		var portrait = new PersonPortrait
		{
			CustomMinimumSize = new Vector2(width, height),
			Circle = circle,
			Backdrop = true,
			BackdropColor = jersey.Darkened(0.55f),
		};
		portrait.SetPlayer(player, jersey, trim);
		return portrait;
	}

	/// <summary>Look a player up by school and name (preview cards, box scores).</summary>
	public static PersonPortrait? For(string? teamId, string name, float width, float height, bool circle = false, TeamLevel level = TeamLevel.Varsity)
	{
		GameSession? session = GameSession.Current;
		if (session?.World == null || string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(name))
		{
			return null;
		}

		return For(session.FindWorldPlayer(teamId, name, level), width, height, circle);
	}

	/// <summary>Look a player up by id.</summary>
	public static PersonPortrait? For(System.Guid? playerId, float width, float height, bool circle = false)
	{
		if (playerId is not { } id || GameSession.Current?.World == null)
		{
			return null;
		}

		return For(GameSession.Current.FindWorldPlayer(id), width, height, circle);
	}
}
