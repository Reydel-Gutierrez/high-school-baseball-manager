using System.Collections.Generic;
using Godot;

/// <summary>
/// School marks used across clubhouse, district hub, and matchup UI.
/// One file per organization; varsity and JV share it. Missing teams stay
/// on letter fallbacks until their logo is added.
/// </summary>
public static class TeamLogos
{
	public const string CascadeId = "cascade";
	public const string GreatLakesId = "great-lakes";
	public const string DelawareValleyId = "delaware-valley";

	private static readonly Dictionary<string, string> Paths = new(StringComparer.Ordinal)
	{
		[CascadeId] = "res://assets/teams/01_Cascade_Regional_High_School/CRlogo.png",
		[GreatLakesId] = "res://assets/teams/02_Great_Lakes_High_School/GLlogo.png",
		["lakes"] = "res://assets/teams/02_Great_Lakes_High_School/GLlogo.png",
		[DelawareValleyId] = "res://assets/teams/04_Delaware_Valley_High_School/DVlogo.png",
	};

	public static Texture2D? Load(string? organizationId)
	{
		if (string.IsNullOrEmpty(organizationId) || !Paths.TryGetValue(organizationId, out string? path))
		{
			return null;
		}

		return GD.Load<Texture2D>(path);
	}

	public static TextureRect MakeIcon(string? organizationId, float size)
	{
		return new TextureRect
		{
			Texture = Load(organizationId),
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
		};
	}

	public static TextureRect? TryMakeIcon(string? organizationId, float size)
	{
		return Load(organizationId) == null ? null : MakeIcon(organizationId, size);
	}

	public static TextureRect MakeHeaderMark(Texture2D? texture, float size = 84)
	{
		return new TextureRect
		{
			Texture = texture,
			CustomMinimumSize = new Vector2(size, size),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
		};
	}

	public static ColorRect MakeHeaderRule(Color color)
	{
		return new ColorRect
		{
			CustomMinimumSize = new Vector2(1, 0),
			Color = color,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
	}
}
