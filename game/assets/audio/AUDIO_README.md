# Game audio

Drop real audio files into these folders. Do not add placeholder/silent files.
Missing files are skipped at runtime; the game keeps running.

## Folders

| Category | Folder | Bus |
| --- | --- | --- |
| Background music | `assets/audio/music/` | Music |
| Stadium / environmental ambience | `assets/audio/ambience/` | Ambience |
| UI sounds | `assets/audio/ui/` | UI |
| Gameplay sound effects | `assets/audio/sfx/` | SFX |

Master volume sits above all four buses.

## Recommended formats

- **Soundtrack:** MP3 (`.mp3`) or Ogg Vorbis (`.ogg`), 44.1 kHz or 48 kHz, stereo. Tracks play in sequence (they should **not** be imported as looping).
- **Ambience:** Ogg Vorbis (`.ogg`), looping, 44.1 kHz or 48 kHz, stereo.
- **UI sounds:** Ogg Vorbis (`.ogg`), short one-shots, 44.1 kHz or 48 kHz, mono or stereo.
- **Gameplay SFX:** WAV (`.wav`) or Ogg Vorbis (`.ogg`), short one-shots, 44.1 kHz, mono or stereo.

Godot imports files automatically. Keep the filenames below if you want them to load with no code changes.

## Expected filenames

### UI (`assets/audio/ui/`)

- `ui_hover.ogg` — pointer enters a control
- `ui_select.ogg` — click / select a button, card, or menu item
- `ui_back.ogg` — back, close, or cancel
- `ui_confirm.ogg` — confirm / submit
- `ui_error.ogg` — invalid action (play from code)
- `ui_notification.ogg` — message received (play from code)
- `ui_advance_day.ogg` — Advance Day / Simulate / Play

Optional: `ui_navigate.ogg` — opening another screen. If this file is absent, navigation uses `ui_select.ogg`.

### Music (`assets/audio/music/`)

One global soundtrack playlist. Songs are **not** tied to Dashboard, Playoffs, Championship, or other game states.

Name files in order:

- `track_01.mp3`
- `track_02.mp3`
- `track_03.mp3`
- `track_04.mp3`
- `track_05.mp3`
- `track_06.mp3` — add more with the next number

`.ogg` is also accepted (`track_06.ogg`). Drop the file in this folder and restart the game. `AudioManager` discovers `track_*.mp3` / `track_*.ogg` automatically (and also checks `track_01` … `track_20` if folder listing is empty in an export).

Missing numbers are skipped. The playlist starts when the main game shell loads and keeps playing while you change pages or scenes.

### Ambience (`assets/audio/ambience/`)

- `amb_stadium_day.ogg`
- `amb_stadium_night.ogg`
- `amb_locker_room.ogg`

### Gameplay SFX (`assets/audio/sfx/`)

- `sfx_bat_hit.wav`
- `sfx_glove_catch.wav`
- `sfx_crowd_cheer.wav`
- `sfx_umpire_call.wav`

## How tracks are assigned

`AudioManager` loads the paths above by convention.

- **Add a soundtrack song:** drop `track_06.mp3` (next free number) into `assets/audio/music/` and restart.
- **Replace a song:** overwrite the same filename.
- **Use a different filename for ambience/UI/SFX:** `SetAmbiencePath(...)`, `SetUiPath(...)`, or `SetSfxPath(...)`.
- **Inspector metadata on a control:** set `ui_click` to `select`, `navigate`, `back`, `confirm`, `advance_day`, or `silent`.

## Soundtrack controls

The soundtrack is a single playlist. Shuffle is **off** by default (play in filename order).

```csharp
AudioManager.Current.StartSoundtrack();   // already called from AppNavigator
AudioManager.Current.NextTrack();
AudioManager.Current.PreviousTrack();
AudioManager.Current.PauseMusic();
AudioManager.Current.ResumeMusic();
AudioManager.Current.TogglePauseMusic();  // play / pause for a future top-bar control
AudioManager.Current.ToggleShuffle();
AudioManager.Current.MusicVolume = 0.65f;
AudioManager.Current.MusicMuted = true;
```

Now playing (for a later mini player):

```csharp
string title = AudioManager.Current.CurrentTrackTitle;     // e.g. "track 01"
int index = AudioManager.Current.CurrentTrackIndex;        // 0-based
int count = AudioManager.Current.TrackCount;
bool paused = AudioManager.Current.IsMusicPaused;
bool playing = AudioManager.Current.IsMusicPlaying;
bool shuffle = AudioManager.Current.ShuffleEnabled;
AudioManager.Current.SoundtrackChanged += RefreshMusicWidget;
```

## Ambience

Ambience is still scene-specific (stadium, locker room). Music is not.

```csharp
AudioManager.Current.PlayAmbience(AmbienceTrack.StadiumDay);
AudioManager.Current.PlayAmbience(AmbienceTrack.StadiumNight);
AudioManager.Current.PlayAmbience(AmbienceTrack.LockerRoom);
AudioManager.Current.StopAmbience();
```
