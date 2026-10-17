using System;
using System.Collections.Generic;
using Godot;
using HSBM.Domain;

public enum DisplayWindowMode
{
	Windowed,
	Borderless,
	Exclusive,
}

public enum DisplayScaleMode
{
	KeepAspect,
	Expand,
	Stretch,
}

/// <summary>
/// Machine-local preferences (volumes, display). Stored in user:// so they
/// survive career changes and keep working offline.
/// </summary>
public static class GameSettings
{
	public const string Path = "user://settings.cfg";
	public const int DesignWidth = 1152;
	public const int DesignHeight = 648;
	public const int DefaultWindowWidth = 1920;
	public const int DefaultWindowHeight = 1080;

	private const string AudioSection = "audio";
	private const string AutosaveSection = "autosave";
	private const string GameplaySection = "gameplay";
	private const string DisplaySection = "display";

	private static readonly AutosavePreferences AutosaveStore = new();
	private static readonly Vector2I[] ResolutionPresets =
	[
		new(1280, 720),
		new(1366, 768),
		new(1600, 900),
		new(1680, 1050),
		new(1920, 1080),
		new(1920, 1200),
		new(2048, 1080),
		new(2560, 1080),
		new(2560, 1440),
		new(2560, 1600),
		new(3440, 1440),
		new(3840, 1080),
		new(3840, 1600),
		new(3840, 2160),
		new(5120, 1440),
		new(5120, 2160),
		new(7680, 2160),
	];

	private static bool _autosaveLoaded;
	private static bool _gameplayLoaded;
	private static bool _displayLoaded;
	private static bool _showSimulationRecap = true;
	private static int _soundtrackIndex;
	private static DisplayWindowMode _windowMode = DisplayWindowMode.Borderless;
	private static DisplayScaleMode _scaleMode = DisplayScaleMode.Expand;
	private static int _displayWidth = DefaultWindowWidth;
	private static int _displayHeight = DefaultWindowHeight;
	private static float _uiScale = 1f;

	public static AutosavePreferences Autosave
	{
		get
		{
			EnsureAutosave();
			return AutosaveStore;
		}
	}

	public static bool ShowSimulationRecap
	{
		get
		{
			EnsureGameplay();
			return _showSimulationRecap;
		}
		set
		{
			EnsureGameplay();
			_showSimulationRecap = value;
			SaveGameplay();
		}
	}

	public static int SoundtrackIndex
	{
		get
		{
			EnsureGameplay();
			return _soundtrackIndex;
		}
		set => _soundtrackIndex = Math.Max(0, value);
	}

	public static DisplayWindowMode WindowMode
	{
		get
		{
			EnsureDisplay();
			return _windowMode;
		}
		set
		{
			EnsureDisplay();
			_windowMode = value;
			SaveDisplay();
			ApplyDisplay();
		}
	}

	public static DisplayScaleMode ScaleMode
	{
		get
		{
			EnsureDisplay();
			return _scaleMode;
		}
		set
		{
			EnsureDisplay();
			_scaleMode = value;
			SaveDisplay();
			ApplyDisplay();
		}
	}

	public static Vector2I Resolution
	{
		get
		{
			EnsureDisplay();
			return new Vector2I(_displayWidth, _displayHeight);
		}
		set
		{
			EnsureDisplay();
			Vector2I size = ClampResolution(value);
			_displayWidth = size.X;
			_displayHeight = size.Y;
			SaveDisplay();
			ApplyDisplay();
		}
	}

	public static float UiScale
	{
		get
		{
			EnsureDisplay();
			return _uiScale;
		}
		set
		{
			EnsureDisplay();
			_uiScale = Mathf.Clamp(value, 0.85f, 1.25f);
			SaveDisplay();
			ApplyDisplay();
		}
	}

	public static bool ResolutionLocked => WindowMode == DisplayWindowMode.Borderless;

	public static void ApplyAudio(AudioManager audio)
	{
		var cfg = new ConfigFile();
		if (cfg.Load(Path) != Error.Ok)
		{
			_gameplayLoaded = true;
			_displayLoaded = true;
			return;
		}

		audio.MasterVolume = ReadFloat(cfg, AudioSection, "master", audio.MasterVolume);
		audio.MusicVolume = ReadFloat(cfg, AudioSection, "music", audio.MusicVolume);
		audio.AmbienceVolume = ReadFloat(cfg, AudioSection, "ambience", audio.AmbienceVolume);
		audio.UiVolume = ReadFloat(cfg, AudioSection, "ui", audio.UiVolume);
		audio.SfxVolume = ReadFloat(cfg, AudioSection, "sfx", audio.SfxVolume);
		audio.Muted = ReadBool(cfg, AudioSection, "muted", audio.Muted);

		bool shuffle = ReadBool(cfg, AudioSection, "shuffle", audio.ShuffleEnabled);
		if (shuffle != audio.ShuffleEnabled)
		{
			audio.ToggleShuffle();
		}

		_soundtrackIndex = Math.Max(0, (int)cfg.GetValue(AudioSection, "track", _soundtrackIndex).AsInt32());
		ReadAutosave(cfg);
		ReadGameplay(cfg);
		ReadDisplay(cfg);
	}

	public static void ApplyDisplay()
	{
		EnsureDisplay();
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}

		Window window = tree.Root;
		window.ContentScaleMode = Window.ContentScaleModeEnum.CanvasItems;
		window.ContentScaleSize = new Vector2I(DesignWidth, DesignHeight);
		window.ContentScaleFactor = _uiScale;
		window.ContentScaleAspect = _scaleMode == DisplayScaleMode.Stretch
			? Window.ContentScaleAspectEnum.Ignore
			: Window.ContentScaleAspectEnum.Expand;

		Vector2I size = ClampResolution(new Vector2I(_displayWidth, _displayHeight));
		switch (_windowMode)
		{
			case DisplayWindowMode.Borderless:
				window.Mode = Window.ModeEnum.Fullscreen;
				break;
			case DisplayWindowMode.Exclusive:
				window.Mode = Window.ModeEnum.Windowed;
				window.Size = size;
				window.Mode = Window.ModeEnum.ExclusiveFullscreen;
				break;
			default:
				window.Mode = Window.ModeEnum.Windowed;
				window.Size = size;
				window.MoveToCenter();
				break;
		}
	}

	public static IReadOnlyList<Vector2I> AvailableResolutions()
	{
		EnsureDisplay();
		Vector2I native = NativeScreenSize();
		var sizes = new List<Vector2I>();
		foreach (Vector2I preset in ResolutionPresets)
		{
			if (preset.X <= native.X && preset.Y <= native.Y && !ContainsSize(sizes, preset))
			{
				sizes.Add(preset);
			}
		}

		if (!ContainsSize(sizes, native))
		{
			sizes.Add(native);
			sizes.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
		}

		if (sizes.Count == 0)
		{
			sizes.Add(new Vector2I(DesignWidth, DesignHeight));
		}

		return sizes;
	}

	public static string ResolutionLabel(Vector2I size)
	{
		Vector2I native = NativeScreenSize();
		string label = $"{size.X} × {size.Y}";
		return size == native ? $"{label}  (Native)" : label;
	}

	public static void SaveAudio(AudioManager audio)
	{
		var cfg = Open();
		cfg.SetValue(AudioSection, "master", audio.MasterVolume);
		cfg.SetValue(AudioSection, "music", audio.MusicVolume);
		cfg.SetValue(AudioSection, "ambience", audio.AmbienceVolume);
		cfg.SetValue(AudioSection, "ui", audio.UiVolume);
		cfg.SetValue(AudioSection, "sfx", audio.SfxVolume);
		cfg.SetValue(AudioSection, "muted", audio.Muted);
		cfg.SetValue(AudioSection, "shuffle", audio.ShuffleEnabled);
		cfg.SetValue(AudioSection, "track", audio.CurrentTrackIndex);
		cfg.Save(Path);
	}

	public static void SaveGameplay()
	{
		EnsureGameplay();
		var cfg = Open();
		cfg.SetValue(GameplaySection, "sim_recap", _showSimulationRecap);
		cfg.Save(Path);
	}

	public static void SaveAutosave()
	{
		EnsureAutosave();
		var cfg = Open();
		cfg.SetValue(AutosaveSection, "enabled", AutosaveStore.Enabled);
		cfg.SetValue(AutosaveSection, "game", AutosaveStore.AfterGame);
		cfg.SetValue(AutosaveSection, "transaction", AutosaveStore.AfterMajorTransaction);
		cfg.SetValue(AutosaveSection, "week", AutosaveStore.AfterWeekAdvance);
		cfg.SetValue(AutosaveSection, "postseason", AutosaveStore.AfterPostseasonRound);
		cfg.SetValue(AutosaveSection, "offseason", AutosaveStore.AfterOffseasonTransition);
		cfg.Save(Path);
	}

	public static void SaveDisplay()
	{
		EnsureDisplay();
		var cfg = Open();
		cfg.SetValue(DisplaySection, "window_mode", WindowModeKey(_windowMode));
		cfg.SetValue(DisplaySection, "scale_mode", ScaleModeKey(_scaleMode));
		cfg.SetValue(DisplaySection, "ui_scale", _uiScale);
		cfg.SetValue(DisplaySection, "width", _displayWidth);
		cfg.SetValue(DisplaySection, "height", _displayHeight);
		cfg.Save(Path);
	}

	private static void EnsureAutosave()
	{
		if (_autosaveLoaded)
		{
			return;
		}

		var cfg = new ConfigFile();
		if (cfg.Load(Path) == Error.Ok)
		{
			ReadAutosave(cfg);
		}
		else
		{
			_autosaveLoaded = true;
		}
	}

	private static void EnsureGameplay()
	{
		if (_gameplayLoaded)
		{
			return;
		}

		var cfg = new ConfigFile();
		if (cfg.Load(Path) == Error.Ok)
		{
			ReadGameplay(cfg);
		}
		else
		{
			_gameplayLoaded = true;
		}
	}

	private static void EnsureDisplay()
	{
		if (_displayLoaded)
		{
			return;
		}

		var cfg = new ConfigFile();
		if (cfg.Load(Path) == Error.Ok)
		{
			ReadDisplay(cfg);
		}
		else
		{
			Vector2I size = DefaultWindowSize();
			_displayWidth = size.X;
			_displayHeight = size.Y;
			_displayLoaded = true;
		}
	}

	private static void ReadGameplay(ConfigFile cfg)
	{
		_showSimulationRecap = ReadBool(cfg, GameplaySection, "sim_recap", _showSimulationRecap);
		_soundtrackIndex = Math.Max(0, (int)cfg.GetValue(AudioSection, "track", _soundtrackIndex).AsInt32());
		_gameplayLoaded = true;
	}

	private static void ReadAutosave(ConfigFile cfg)
	{
		AutosaveStore.Enabled = ReadBool(cfg, AutosaveSection, "enabled", AutosaveStore.Enabled);
		AutosaveStore.AfterGame = ReadBool(cfg, AutosaveSection, "game", AutosaveStore.AfterGame);
		AutosaveStore.AfterMajorTransaction = ReadBool(cfg, AutosaveSection, "transaction", AutosaveStore.AfterMajorTransaction);
		AutosaveStore.AfterWeekAdvance = ReadBool(cfg, AutosaveSection, "week", AutosaveStore.AfterWeekAdvance);
		AutosaveStore.AfterPostseasonRound = ReadBool(cfg, AutosaveSection, "postseason", AutosaveStore.AfterPostseasonRound);
		AutosaveStore.AfterOffseasonTransition = ReadBool(cfg, AutosaveSection, "offseason", AutosaveStore.AfterOffseasonTransition);
		_autosaveLoaded = true;
	}

	private static void ReadDisplay(ConfigFile cfg)
	{
		_windowMode = ParseWindowMode(cfg.GetValue(DisplaySection, "window_mode", WindowModeKey(_windowMode)).AsString());
		_scaleMode = ParseScaleMode(cfg.GetValue(DisplaySection, "scale_mode", ScaleModeKey(_scaleMode)).AsString());
		_uiScale = Mathf.Clamp(ReadFloat(cfg, DisplaySection, "ui_scale", _uiScale), 0.85f, 1.25f);
		int width = Math.Max(640, (int)cfg.GetValue(DisplaySection, "width", DefaultWindowWidth).AsInt32());
		int height = Math.Max(360, (int)cfg.GetValue(DisplaySection, "height", DefaultWindowHeight).AsInt32());
		Vector2I size = ClampResolution(new Vector2I(width, height));
		_displayWidth = size.X;
		_displayHeight = size.Y;
		_displayLoaded = true;
	}

	private static Vector2I ClampResolution(Vector2I requested)
	{
		Vector2I native = NativeScreenSize();
		int width = Math.Clamp(requested.X, 1280, Math.Max(1280, native.X));
		int height = Math.Clamp(requested.Y, 720, Math.Max(720, native.Y));
		return new Vector2I(width, height);
	}

	private static Vector2I NativeScreenSize()
	{
		int screen = DisplayServer.WindowGetCurrentScreen();
		Vector2I size = DisplayServer.ScreenGetSize(screen);
		if (size.X < 1280 || size.Y < 720)
		{
			return new Vector2I(DefaultWindowWidth, DefaultWindowHeight);
		}

		return size;
	}

	private static Vector2I DefaultWindowSize() => NativeScreenSize();

	private static bool ContainsSize(List<Vector2I> sizes, Vector2I size)
	{
		foreach (Vector2I item in sizes)
		{
			if (item == size)
			{
				return true;
			}
		}

		return false;
	}

	private static string WindowModeKey(DisplayWindowMode mode) => mode switch
	{
		DisplayWindowMode.Borderless => "borderless",
		DisplayWindowMode.Exclusive => "exclusive",
		_ => "windowed",
	};

	private static DisplayWindowMode ParseWindowMode(string value) => value switch
	{
		"borderless" => DisplayWindowMode.Borderless,
		"exclusive" => DisplayWindowMode.Exclusive,
		_ => DisplayWindowMode.Windowed,
	};

	private static string ScaleModeKey(DisplayScaleMode mode) => mode switch
	{
		DisplayScaleMode.KeepAspect => "keep",
		DisplayScaleMode.Stretch => "stretch",
		_ => "expand",
	};

	private static DisplayScaleMode ParseScaleMode(string value) => value switch
	{
		"stretch" or "ignore" => DisplayScaleMode.Stretch,
		_ => DisplayScaleMode.Expand,
	};

	private static ConfigFile Open()
	{
		var cfg = new ConfigFile();
		cfg.Load(Path);
		return cfg;
	}

	private static float ReadFloat(ConfigFile cfg, string section, string key, float fallback)
	{
		return (float)cfg.GetValue(section, key, fallback).AsDouble();
	}

	private static bool ReadBool(ConfigFile cfg, string section, string key, bool fallback)
	{
		return cfg.GetValue(section, key, fallback).AsBool();
	}
}
