using Godot;

/// <summary>
/// Machine-local preferences (volumes, later display options). Stored in
/// user:// so they survive career changes and keep working offline.
/// </summary>
public static class GameSettings
{
	public const string Path = "user://settings.cfg";

	private const string AudioSection = "audio";

	public static void ApplyAudio(AudioManager audio)
	{
		var cfg = new ConfigFile();
		if (cfg.Load(Path) != Error.Ok)
		{
			return;
		}

		audio.MasterVolume = ReadFloat(cfg, "master", audio.MasterVolume);
		audio.MusicVolume = ReadFloat(cfg, "music", audio.MusicVolume);
		audio.AmbienceVolume = ReadFloat(cfg, "ambience", audio.AmbienceVolume);
		audio.UiVolume = ReadFloat(cfg, "ui", audio.UiVolume);
		audio.SfxVolume = ReadFloat(cfg, "sfx", audio.SfxVolume);
		audio.Muted = ReadBool(cfg, "muted", audio.Muted);

		bool shuffle = ReadBool(cfg, "shuffle", audio.ShuffleEnabled);
		if (shuffle != audio.ShuffleEnabled)
		{
			audio.ToggleShuffle();
		}
	}

	public static void SaveAudio(AudioManager audio)
	{
		var cfg = new ConfigFile();
		cfg.Load(Path);
		cfg.SetValue(AudioSection, "master", audio.MasterVolume);
		cfg.SetValue(AudioSection, "music", audio.MusicVolume);
		cfg.SetValue(AudioSection, "ambience", audio.AmbienceVolume);
		cfg.SetValue(AudioSection, "ui", audio.UiVolume);
		cfg.SetValue(AudioSection, "sfx", audio.SfxVolume);
		cfg.SetValue(AudioSection, "muted", audio.Muted);
		cfg.SetValue(AudioSection, "shuffle", audio.ShuffleEnabled);
		cfg.Save(Path);
	}

	private static float ReadFloat(ConfigFile cfg, string key, float fallback)
	{
		return (float)cfg.GetValue(AudioSection, key, fallback).AsDouble();
	}

	private static bool ReadBool(ConfigFile cfg, string key, bool fallback)
	{
		return cfg.GetValue(AudioSection, key, fallback).AsBool();
	}
}
