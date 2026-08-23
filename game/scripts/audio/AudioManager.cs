using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Persistent autoload for music, ambience, UI, and gameplay SFX.
/// Missing or unassigned streams are ignored so the game can run without audio files.
/// </summary>
public partial class AudioManager : Node
{
	public const string MusicBus = "Music";
	public const string AmbienceBus = "Ambience";
	public const string UiBus = "UI";
	public const string SfxBus = "SFX";

	public const float DefaultMusicFade = 0.8f;
	public const float DefaultAmbienceFade = 0.6f;

	private const float SilentDb = -80f;
	private const int UiPlayerCount = 4;
	private const int SfxPlayerCount = 8;

	public static AudioManager Current { get; private set; } = null!;

	/// <summary>
	/// Debug-only request logs. Ignored in release exports even when left true.
	/// Set false in the editor if the Output panel is too noisy.
	/// </summary>
	public static bool DiagnosticsEnabled { get; set; } = true;

	/// <summary>
	/// Hover fires often; leave false unless you are tracing pointer-enter binding.
	/// </summary>
	public static bool LogUiHover { get; set; }

	private readonly HashSet<string> _loggedMissing = new(StringComparer.OrdinalIgnoreCase);

	private AudioStreamPlayer _musicA = null!;
	private AudioStreamPlayer _musicB = null!;
	private AudioStreamPlayer _ambienceA = null!;
	private AudioStreamPlayer _ambienceB = null!;
	private AudioStreamPlayer[] _uiPlayers = Array.Empty<AudioStreamPlayer>();
	private AudioStreamPlayer[] _sfxPlayers = Array.Empty<AudioStreamPlayer>();
	private int _uiIndex;
	private int _sfxIndex;
	private bool _musicUsingA = true;
	private bool _ambienceUsingA = true;
	private Tween? _musicTween;
	private Tween? _ambienceTween;
	private string _currentMusicPath = string.Empty;
	private string _currentAmbiencePath = string.Empty;

	private readonly List<string> _soundtrack = new();
	private readonly List<int> _shuffleHistory = new();
	private int _soundtrackIndex;
	private int _missingTrackStreak;
	private bool _shuffleEnabled;
	private bool _musicPaused;
	private bool _soundtrackStarted;
	private bool _advancingTrack;

	private readonly Dictionary<AmbienceTrack, string> _ambiencePaths = new();
	private readonly Dictionary<UiSound, string> _uiPaths = new();
	private readonly Dictionary<SfxId, string> _sfxPaths = new();

	private float _masterVolume = 1f;
	private float _musicVolume = 0.65f;
	private float _ambienceVolume = 0.45f;
	private float _uiVolume = 0.55f;
	private float _sfxVolume = 0.8f;
	private bool _muted;

	public override void _EnterTree()
	{
		Current = this;
	}

	public override void _Ready()
	{
		EnsureBuses();
		ResetDefaultPaths();
		BuildPlayers();
		GameSettings.ApplyAudio(this);
		ApplyAllVolumes();
		UiSounds.Attach(this);
		LogStartupStatus();
		if (ShouldRunUiSoundTest())
		{
			CallDeferred(nameof(TestUiSound));
		}
	}

	private static bool ShouldRunUiSoundTest()
	{
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg == "--test-ui-sound")
			{
				return true;
			}
		}

		return false;
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (!OS.IsDebugBuild())
		{
			return;
		}

		if (ev is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F9)
		{
			TestUiSound();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _ExitTree()
	{
		UiSounds.Detach(this);
		if (Current == this)
		{
			Current = null!;
		}
	}

	public float MasterVolume
	{
		get => _masterVolume;
		set => SetVolume(AudioChannel.Master, value);
	}

	public float MusicVolume
	{
		get => _musicVolume;
		set => SetVolume(AudioChannel.Music, value);
	}

	public float AmbienceVolume
	{
		get => _ambienceVolume;
		set => SetVolume(AudioChannel.Ambience, value);
	}

	public float UiVolume
	{
		get => _uiVolume;
		set => SetVolume(AudioChannel.Ui, value);
	}

	public float SfxVolume
	{
		get => _sfxVolume;
		set => SetVolume(AudioChannel.Sfx, value);
	}

	public bool Muted
	{
		get => _muted;
		set => SetMuted(value);
	}

	public string CurrentMusicPath => _currentMusicPath;

	public string CurrentAmbiencePath => _currentAmbiencePath;

	public string CurrentTrackTitle
	{
		get
		{
			if (string.IsNullOrEmpty(_currentMusicPath))
			{
				return string.Empty;
			}

			return _currentMusicPath.GetFile().GetBaseName().Replace('_', ' ');
		}
	}

	public int CurrentTrackIndex => _soundtrackIndex;

	public int TrackCount => _soundtrack.Count;

	public bool IsMusicPlaying
	{
		get
		{
			if (_musicA == null)
			{
				return false;
			}

			AudioStreamPlayer player = ActiveMusic();
			return GodotObject.IsInstanceValid(player) && player.Playing && !_musicPaused;
		}
	}

	public bool IsMusicPaused => _musicPaused;

	public bool ShuffleEnabled => _shuffleEnabled;

	public event Action? SoundtrackChanged;

	public bool MusicMuted
	{
		get
		{
			int index = AudioServer.GetBusIndex(MusicBus);
			return index >= 0 && AudioServer.IsBusMute(index);
		}
		set => SetChannelMuted(AudioChannel.Music, value);
	}

	public void SetVolume(AudioChannel channel, float linear01)
	{
		float volume = Mathf.Clamp(linear01, 0f, 1f);
		switch (channel)
		{
			case AudioChannel.Master:
				_masterVolume = volume;
				break;
			case AudioChannel.Music:
				_musicVolume = volume;
				break;
			case AudioChannel.Ambience:
				_ambienceVolume = volume;
				break;
			case AudioChannel.Ui:
				_uiVolume = volume;
				break;
			case AudioChannel.Sfx:
				_sfxVolume = volume;
				break;
		}

		ApplyVolume(channel);
	}

	public float GetVolume(AudioChannel channel) => channel switch
	{
		AudioChannel.Master => _masterVolume,
		AudioChannel.Music => _musicVolume,
		AudioChannel.Ambience => _ambienceVolume,
		AudioChannel.Ui => _uiVolume,
		AudioChannel.Sfx => _sfxVolume,
		_ => 1f,
	};

	public void SetMuted(bool muted)
	{
		_muted = muted;
		int master = AudioServer.GetBusIndex("Master");
		if (master >= 0)
		{
			AudioServer.SetBusMute(master, muted);
		}
	}

	public void SetChannelMuted(AudioChannel channel, bool muted)
	{
		int index = AudioServer.GetBusIndex(BusName(channel));
		if (index >= 0)
		{
			AudioServer.SetBusMute(index, muted);
		}
	}

	public void SetAmbiencePath(AmbienceTrack track, string path)
	{
		if (track != AmbienceTrack.None)
		{
			_ambiencePaths[track] = path;
		}
	}

	public void SetUiPath(UiSound sound, string path) => _uiPaths[sound] = path;

	public void SetSfxPath(SfxId sfx, string path) => _sfxPaths[sfx] = path;

	public void StartSoundtrack()
	{
		if (_soundtrackStarted && (IsMusicPlaying || _musicPaused))
		{
			Log($"[Audio] Soundtrack already running ({CurrentTrackTitle}), skip restart.");
			return;
		}

		RebuildSoundtrack();
		if (_soundtrack.Count == 0)
		{
			Log("[Audio] Soundtrack empty — no track_*.mp3/ogg files found.");
			return;
		}

		_soundtrackStarted = true;
		_musicPaused = false;
		PlayPlaylistIndex(_soundtrackIndex, fadeSeconds: 0.6f, force: true);
	}

	public void NextTrack(float fadeSeconds = DefaultMusicFade)
	{
		if (_soundtrack.Count == 0)
		{
			RebuildSoundtrack();
		}

		if (_soundtrack.Count == 0)
		{
			return;
		}

		int next = NextPlaylistIndex(_soundtrackIndex);
		if (_shuffleEnabled)
		{
			_shuffleHistory.Add(_soundtrackIndex);
			if (_shuffleHistory.Count > 32)
			{
				_shuffleHistory.RemoveAt(0);
			}
		}

		PlayPlaylistIndex(next, fadeSeconds, force: true);
	}

	public void PreviousTrack(float fadeSeconds = DefaultMusicFade)
	{
		if (_soundtrack.Count == 0)
		{
			RebuildSoundtrack();
		}

		if (_soundtrack.Count == 0)
		{
			return;
		}

		int previous;
		if (_shuffleEnabled && _shuffleHistory.Count > 0)
		{
			previous = _shuffleHistory[^1];
			_shuffleHistory.RemoveAt(_shuffleHistory.Count - 1);
		}
		else
		{
			previous = (_soundtrackIndex - 1 + _soundtrack.Count) % _soundtrack.Count;
		}

		PlayPlaylistIndex(previous, fadeSeconds, force: true);
	}

	public void PauseMusic()
	{
		AudioStreamPlayer player = ActiveMusic();
		if (!GodotObject.IsInstanceValid(player) || player.Stream == null)
		{
			return;
		}

		player.StreamPaused = true;
		_musicPaused = true;
		Log("[Audio] Soundtrack paused.");
		SoundtrackChanged?.Invoke();
	}

	public void ResumeMusic()
	{
		if (!_soundtrackStarted)
		{
			StartSoundtrack();
			return;
		}

		AudioStreamPlayer player = ActiveMusic();
		if (!GodotObject.IsInstanceValid(player) || player.Stream == null)
		{
			StartSoundtrack();
			return;
		}

		player.StreamPaused = false;
		_musicPaused = false;
		if (!player.Playing)
		{
			player.Play();
		}

		Log("[Audio] Soundtrack resumed.");
		SoundtrackChanged?.Invoke();
	}

	public void TogglePauseMusic()
	{
		if (_musicPaused || !IsMusicPlaying)
		{
			ResumeMusic();
		}
		else
		{
			PauseMusic();
		}
	}

	public void ToggleShuffle()
	{
		_shuffleEnabled = !_shuffleEnabled;
		if (!_shuffleEnabled)
		{
			_shuffleHistory.Clear();
		}

		Log($"[Audio] Shuffle {(_shuffleEnabled ? "on" : "off")}.");
		SoundtrackChanged?.Invoke();
	}

	public void StopMusic(float fadeSeconds = DefaultMusicFade)
	{
		_currentMusicPath = string.Empty;
		_soundtrackStarted = false;
		_musicPaused = false;
		FadeOutPair(ref _musicTween, _musicA, _musicB, fadeSeconds);
		SoundtrackChanged?.Invoke();
	}

	private void RebuildSoundtrack()
	{
		_soundtrack.Clear();
		_soundtrack.AddRange(AudioLibrary.LoadSoundtrackPlaylist());
		if (_soundtrackIndex >= _soundtrack.Count)
		{
			_soundtrackIndex = 0;
		}
	}

	private int NextPlaylistIndex(int current)
	{
		if (_soundtrack.Count <= 1)
		{
			return 0;
		}

		if (_shuffleEnabled)
		{
			int next = current;
			int guard = 0;
			while (next == current && guard++ < 16)
			{
				next = (int)(GD.Randi() % (uint)_soundtrack.Count);
			}

			return next;
		}

		return (current + 1) % _soundtrack.Count;
	}

	private void PlayPlaylistIndex(int index, float fadeSeconds, bool force)
	{
		if (_soundtrack.Count == 0)
		{
			return;
		}

		index = ((_soundtrack.Count + index % _soundtrack.Count) % _soundtrack.Count);
		string path = _soundtrack[index];
		if (!force && IsSameTrack(_currentMusicPath, path) && ActiveMusic().Playing && !_musicPaused)
		{
			return;
		}

		Log($"[Audio] Soundtrack track {index + 1}/{_soundtrack.Count}: {path}");
		AudioStream? stream = LoadStream(path);
		if (stream == null)
		{
			_missingTrackStreak++;
			if (_missingTrackStreak >= _soundtrack.Count)
			{
				Log("[Audio] Soundtrack has no playable tracks.");
				return;
			}

			_soundtrackIndex = index;
			NextTrack(0f);
			return;
		}

		_missingTrackStreak = 0;
		_soundtrackIndex = index;
		_musicPaused = false;
		_advancingTrack = true;
		DisableLoop(stream);
		Crossfade(ref _musicUsingA, _musicA, _musicB, ref _musicTween, ref _currentMusicPath, stream, path, fadeSeconds);
		ActiveMusic().StreamPaused = false;
		_advancingTrack = false;
		_soundtrackStarted = true;
		Log($"[Audio] Playing soundtrack on bus: {MusicBus} ({CurrentTrackTitle})");
		SoundtrackChanged?.Invoke();
	}

	private void OnMusicFinished(AudioStreamPlayer player)
	{
		if (_advancingTrack || _musicPaused || !_soundtrackStarted)
		{
			return;
		}

		if (!GodotObject.IsInstanceValid(player) || player != ActiveMusic())
		{
			return;
		}

		NextTrack(0.45f);
	}

	private static void DisableLoop(AudioStream stream)
	{
		switch (stream)
		{
			case AudioStreamOggVorbis ogg:
				ogg.Loop = false;
				break;
			case AudioStreamMP3 mp3:
				mp3.Loop = false;
				break;
			case AudioStreamWav wav:
				wav.LoopMode = AudioStreamWav.LoopModeEnum.Disabled;
				break;
		}
	}

	public void PlayAmbience(AmbienceTrack track, float fadeSeconds = DefaultAmbienceFade)
	{
		if (track == AmbienceTrack.None)
		{
			StopAmbience(fadeSeconds);
			return;
		}

		Log($"[Audio] Ambience requested: {track}");
		PlayAmbience(_ambiencePaths.GetValueOrDefault(track, AudioLibrary.AmbiencePath(track)), fadeSeconds);
	}

	public void PlayAmbience(string path, float fadeSeconds = DefaultAmbienceFade)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			StopAmbience(fadeSeconds);
			return;
		}

		if (IsSameTrack(_currentAmbiencePath, path) && ActiveAmbience().Playing)
		{
			Log($"[Audio] Ambience already playing, skip restart: {path}");
			return;
		}

		Log($"[Audio] Path: {path}");
		AudioStream? stream = LoadStream(path);
		if (stream == null)
		{
			return;
		}

		EnableLoop(stream);
		Crossfade(ref _ambienceUsingA, _ambienceA, _ambienceB, ref _ambienceTween, ref _currentAmbiencePath, stream, path, fadeSeconds);
		Log($"[Audio] Playing ambience on bus: {AmbienceBus}");
	}

	public void StopAmbience(float fadeSeconds = DefaultAmbienceFade)
	{
		_currentAmbiencePath = string.Empty;
		FadeOutPair(ref _ambienceTween, _ambienceA, _ambienceB, fadeSeconds);
	}

	public void PlayUi(UiSound sound)
	{
		string path = _uiPaths.GetValueOrDefault(sound, AudioLibrary.UiPath(sound));
		if (sound == UiSound.Navigate && !ResourceExists(path))
		{
			path = _uiPaths.GetValueOrDefault(UiSound.Select, AudioLibrary.UiSelect);
		}

		if (sound == UiSound.Hover)
		{
			Log($"[Audio] UI requested: {sound}", includeHover: true);
			Log($"[Audio] Path: {path}", includeHover: true);
		}
		else
		{
			Log($"[Audio] UI requested: {sound}");
			Log($"[Audio] Path: {path}");
			Log($"[Audio] UiSounds bound — buttons: {UiSounds.BoundButtonCount}, popups: {UiSounds.BoundPopupCount}, cards: {UiSounds.BoundClickableCount}");
		}

		PlayOneShot(_uiPlayers, ref _uiIndex, path, UiBus, logHover: sound == UiSound.Hover);
	}

	public void PlayUi(string path)
	{
		Log($"[Audio] UI requested: custom");
		Log($"[Audio] Path: {path}");
		PlayOneShot(_uiPlayers, ref _uiIndex, path, UiBus);
	}

	public void PlaySfx(SfxId sfx)
	{
		string path = _sfxPaths.GetValueOrDefault(sfx, AudioLibrary.SfxPath(sfx));
		Log($"[Audio] SFX requested: {sfx}");
		Log($"[Audio] Path: {path}");
		PlayOneShot(_sfxPlayers, ref _sfxIndex, path, SfxBus);
	}

	public void PlaySfx(string path)
	{
		Log($"[Audio] SFX requested: custom");
		Log($"[Audio] Path: {path}");
		PlayOneShot(_sfxPlayers, ref _sfxIndex, path, SfxBus);
	}

	/// <summary>
	/// Development helper: tries to play ui_select.ogg directly, bypassing UI binding.
	/// Use this to tell an AudioManager/playback problem from a UiSounds binding problem.
	/// </summary>
	public void TestUiSound()
	{
		string path = AudioLibrary.UiSelect;
		GD.Print("[Audio] TestUiSound() — bypassing UI binding");
		GD.Print($"[Audio] UI requested: Select");
		GD.Print($"[Audio] Path: {path}");
		bool found = ResourceExists(path);
		GD.Print($"[Audio] Resource found: {found}");
		if (!found)
		{
			GD.Print($"[Audio] Missing audio resource: {path}");
			return;
		}

		PlayOneShot(_uiPlayers, ref _uiIndex, path, UiBus, forceLog: true);
	}

	private void ResetDefaultPaths()
	{
		_ambiencePaths.Clear();
		_uiPaths.Clear();
		_sfxPaths.Clear();

		foreach (AmbienceTrack track in Enum.GetValues<AmbienceTrack>())
		{
			if (track != AmbienceTrack.None)
			{
				_ambiencePaths[track] = AudioLibrary.AmbiencePath(track);
			}
		}

		foreach (UiSound sound in Enum.GetValues<UiSound>())
		{
			_uiPaths[sound] = AudioLibrary.UiPath(sound);
		}

		foreach (SfxId sfx in Enum.GetValues<SfxId>())
		{
			_sfxPaths[sfx] = AudioLibrary.SfxPath(sfx);
		}
	}

	private void BuildPlayers()
	{
		_musicA = MakePlayer("MusicA", MusicBus);
		_musicB = MakePlayer("MusicB", MusicBus);
		_ambienceA = MakePlayer("AmbienceA", AmbienceBus);
		_ambienceB = MakePlayer("AmbienceB", AmbienceBus);
		_musicA.Finished += () => OnMusicFinished(_musicA);
		_musicB.Finished += () => OnMusicFinished(_musicB);
		_ambienceA.Finished += () => RestartIfActive(_ambienceA, _currentAmbiencePath);
		_ambienceB.Finished += () => RestartIfActive(_ambienceB, _currentAmbiencePath);

		_uiPlayers = new AudioStreamPlayer[UiPlayerCount];
		for (int i = 0; i < UiPlayerCount; i++)
		{
			_uiPlayers[i] = MakePlayer($"Ui{i}", UiBus);
		}

		_sfxPlayers = new AudioStreamPlayer[SfxPlayerCount];
		for (int i = 0; i < SfxPlayerCount; i++)
		{
			_sfxPlayers[i] = MakePlayer($"Sfx{i}", SfxBus);
		}
	}

	private AudioStreamPlayer MakePlayer(string name, string bus)
	{
		var player = new AudioStreamPlayer
		{
			Name = name,
			Bus = bus,
		};
		AddChild(player);
		return player;
	}

	private AudioStreamPlayer ActiveMusic() => _musicUsingA ? _musicA : _musicB;

	private AudioStreamPlayer ActiveAmbience() => _ambienceUsingA ? _ambienceA : _ambienceB;

	private void Crossfade(
		ref bool usingA,
		AudioStreamPlayer playerA,
		AudioStreamPlayer playerB,
		ref Tween? tween,
		ref string currentPath,
		AudioStream stream,
		string path,
		float fadeSeconds)
	{
		AudioStreamPlayer incoming = usingA ? playerB : playerA;
		AudioStreamPlayer outgoing = usingA ? playerA : playerB;
		usingA = !usingA;
		currentPath = path;

		tween?.Kill();
		incoming.Stream = stream;
		incoming.VolumeDb = fadeSeconds <= 0f || !outgoing.Playing ? 0f : SilentDb;
		incoming.Play();

		if (fadeSeconds <= 0f || !outgoing.Playing)
		{
			outgoing.Stop();
			incoming.VolumeDb = 0f;
			return;
		}

		tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(incoming, "volume_db", 0f, fadeSeconds)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(outgoing, "volume_db", SilentDb, fadeSeconds)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.In);
		AudioStreamPlayer fadeOut = outgoing;
		tween.Chain().TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(fadeOut))
			{
				fadeOut.Stop();
			}
		}));
	}

	private void FadeOutPair(ref Tween? tween, AudioStreamPlayer playerA, AudioStreamPlayer playerB, float fadeSeconds)
	{
		tween?.Kill();
		if (!playerA.Playing && !playerB.Playing)
		{
			return;
		}

		if (fadeSeconds <= 0f)
		{
			playerA.Stop();
			playerB.Stop();
			return;
		}

		tween = CreateTween();
		tween.SetParallel(true);
		if (playerA.Playing)
		{
			tween.TweenProperty(playerA, "volume_db", SilentDb, fadeSeconds);
		}

		if (playerB.Playing)
		{
			tween.TweenProperty(playerB, "volume_db", SilentDb, fadeSeconds);
		}

		tween.Chain().TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(playerA))
			{
				playerA.Stop();
			}

			if (GodotObject.IsInstanceValid(playerB))
			{
				playerB.Stop();
			}
		}));
	}

	private void RestartIfActive(AudioStreamPlayer player, string currentPath)
	{
		if (string.IsNullOrEmpty(currentPath) || player.Stream == null)
		{
			return;
		}

		AudioStreamPlayer active = ActiveAmbience();
		if (player != active)
		{
			return;
		}

		player.Play();
	}

	private void PlayOneShot(
		AudioStreamPlayer[] pool,
		ref int index,
		string path,
		string bus,
		bool logHover = false,
		bool forceLog = false)
	{
		AudioStream? stream = LoadStream(path, logHover, forceLog);
		if (stream == null || pool.Length == 0)
		{
			return;
		}

		try
		{
			AudioStreamPlayer player = pool[index];
			index = (index + 1) % pool.Length;
			player.Stream = stream;
			player.VolumeDb = 0f;
			player.Play();
			Log($"[Audio] Playing on bus: {bus}", includeHover: logHover, force: forceLog);
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[Audio] Playback failed for '{path}': {ex.Message}");
		}
	}

	private static bool IsSameTrack(string current, string next) =>
		!string.IsNullOrEmpty(current) && string.Equals(current, next, StringComparison.OrdinalIgnoreCase);

	private AudioStream? LoadStream(
		string path,
		bool logHover = false,
		bool forceLog = false)
	{
		bool found = ResourceExists(path);
		Log($"[Audio] Resource found: {found}", includeHover: logHover, force: forceLog);
		if (!found)
		{
			LogMissing(path, forceLog);
			return null;
		}

		try
		{
			AudioStream? stream = ResourceLoader.Load<AudioStream>(path);
			if (stream == null)
			{
				LogMissing(path, forceLog);
				return null;
			}

			return stream;
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[Audio] Failed to load '{path}': {ex.Message}");
			return null;
		}
	}

	private static bool ResourceExists(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}

		try
		{
			return ResourceLoader.Exists(path);
		}
		catch (Exception ex)
		{
			GD.PushWarning($"[Audio] Exists check failed for '{path}': {ex.Message}");
			return false;
		}
	}

	private static void EnableLoop(AudioStream stream)
	{
		switch (stream)
		{
			case AudioStreamOggVorbis ogg:
				ogg.Loop = true;
				break;
			case AudioStreamMP3 mp3:
				mp3.Loop = true;
				break;
			case AudioStreamWav wav:
				wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
				break;
		}
	}

	private static void EnsureBuses()
	{
		EnsureBus(MusicBus);
		EnsureBus(AmbienceBus);
		EnsureBus(UiBus);
		EnsureBus(SfxBus);
	}

	private static void EnsureBus(string name)
	{
		if (AudioServer.GetBusIndex(name) >= 0)
		{
			return;
		}

		int index = AudioServer.BusCount;
		AudioServer.AddBus(index);
		AudioServer.SetBusName(index, name);
		AudioServer.SetBusSend(index, "Master");
	}

	private void ApplyAllVolumes()
	{
		ApplyVolume(AudioChannel.Master);
		ApplyVolume(AudioChannel.Music);
		ApplyVolume(AudioChannel.Ambience);
		ApplyVolume(AudioChannel.Ui);
		ApplyVolume(AudioChannel.Sfx);
		SetMuted(_muted);
	}

	private void ApplyVolume(AudioChannel channel)
	{
		int index = AudioServer.GetBusIndex(BusName(channel));
		if (index < 0)
		{
			return;
		}

		AudioServer.SetBusVolumeDb(index, ToDb(GetVolume(channel)));
	}

	private static string BusName(AudioChannel channel) => channel switch
	{
		AudioChannel.Master => "Master",
		AudioChannel.Music => MusicBus,
		AudioChannel.Ambience => AmbienceBus,
		AudioChannel.Ui => UiBus,
		AudioChannel.Sfx => SfxBus,
		_ => "Master",
	};

	private static float ToDb(float linear)
	{
		if (linear <= 0.0001f)
		{
			return SilentDb;
		}

		return Mathf.LinearToDb(linear);
	}

	private void Log(string message, bool includeHover = false, bool force = false)
	{
		if (!force && !(DiagnosticsEnabled && OS.IsDebugBuild()))
		{
			return;
		}

		if (includeHover && !LogUiHover && !force)
		{
			return;
		}

		GD.Print(message);
	}

	private void LogMissing(string path, bool force = false)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}

		if (!force && !_loggedMissing.Add(path))
		{
			return;
		}

		if (force || (DiagnosticsEnabled && OS.IsDebugBuild()))
		{
			GD.Print($"[Audio] Missing audio resource: {path}");
		}
	}

	private void LogStartupStatus()
	{
		if (!(DiagnosticsEnabled && OS.IsDebugBuild()))
		{
			return;
		}

		GD.Print("[Audio] AudioManager ready");
		GD.Print($"[Audio] Current valid: {Current != null}");
		GD.Print($"[Audio] Players — Music: 2, Ambience: 2, UI: {_uiPlayers.Length}, SFX: {_sfxPlayers.Length}");
		LogBus("Master");
		LogBus(MusicBus);
		LogBus(AmbienceBus);
		LogBus(UiBus);
		LogBus(SfxBus);
		GD.Print($"[Audio] Master muted (manager): {_muted}");

		RebuildSoundtrack();
		GD.Print($"[Audio] Soundtrack tracks: {_soundtrack.Count}");
		foreach (string path in _soundtrack)
		{
			GD.Print($"[Audio] Catalog present: {path}");
		}

		int present = _soundtrack.Count;
		int expected = _soundtrack.Count;
		foreach (string path in ExpectedCatalogPaths())
		{
			expected++;
			if (ResourceExists(path))
			{
				present++;
				GD.Print($"[Audio] Catalog present: {path}");
			}
		}

		GD.Print($"[Audio] Catalog files Godot can load: {present}/{expected}");
		if (present == 0)
		{
			GD.Print("[Audio] No playable audio files found. Drop files into assets/audio — silence is expected until then.");
		}

		CallDeferred(nameof(LogUiBindSummary));
		SceneTreeTimer timer = GetTree().CreateTimer(0.35);
		timer.Timeout += LogUiBindSummary;
	}

	private void LogUiBindSummary()
	{
		if (!(DiagnosticsEnabled && OS.IsDebugBuild()))
		{
			return;
		}

		GD.Print($"[Audio] UiSounds bound so far — buttons: {UiSounds.BoundButtonCount}, popups: {UiSounds.BoundPopupCount}, cards: {UiSounds.BoundClickableCount}");
	}

	private void LogBus(string name)
	{
		int index = AudioServer.GetBusIndex(name);
		if (index < 0)
		{
			GD.Print($"[Audio] Bus '{name}' missing");
			return;
		}

		GD.Print($"[Audio] Bus '{name}' mute={AudioServer.IsBusMute(index)} volume_db={AudioServer.GetBusVolumeDb(index):0.##}");
	}

	private static IEnumerable<string> ExpectedCatalogPaths()
	{
		yield return AudioLibrary.AmbStadiumDay;
		yield return AudioLibrary.AmbStadiumNight;
		yield return AudioLibrary.AmbLockerRoom;
		yield return AudioLibrary.UiHover;
		yield return AudioLibrary.UiSelect;
		yield return AudioLibrary.UiNavigate;
		yield return AudioLibrary.UiBack;
		yield return AudioLibrary.UiConfirm;
		yield return AudioLibrary.UiError;
		yield return AudioLibrary.UiNotification;
		yield return AudioLibrary.UiAdvanceDay;
		yield return AudioLibrary.SfxBatHit;
		yield return AudioLibrary.SfxGloveCatch;
		yield return AudioLibrary.SfxCrowdCheer;
		yield return AudioLibrary.SfxUmpireCall;
	}
}
