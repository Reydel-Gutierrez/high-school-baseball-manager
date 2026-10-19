# High School Baseball Manager — notes for Claude

A Football-Manager-style high school baseball sim. You are the primary developer;
the owner reviews in game and gives direction. This file is the handoff: read it
first, then `docs/ui-concepts/README.md`.

## Stack
- Godot 4.7.1 .NET (C#, net8.0, GL Compatibility). Editor: `C:\tools\godot\Godot_v4.7.1-stable_mono_win64\`.
- `src/HSBM.Domain` (pure model), `src/HSBM.Simulation` (world gen, game engine, day processor),
  `src/HSBM.Application` (CareerService), `src/HSBM.Persistence` (SQLite), `game/` (Godot UI), `tests/` (xUnit).
- Build: Alt+B in the Godot editor, or `dotnet build "game/High School Baseball Manager.csproj"`.
  Tests: `dotnet test HSBM.sln` (or VS Test Explorer → Run All). All 271 pass as of Oct 19 2026.
- Viewport is 1152×648 with `canvas_items` + `expand`: on the owner's 3428×1368 ultrawide the logical
  canvas is about **1620×648**. Vertical space is the scarce resource; check every page fits without scrolling.

## Rules
- **Never play or modify the owner's own career saves** (Marcus Johnson / Delaware Valley).
  Test with the **Test Coach** career (Northfield Knights).
- Commits are authored as `Claude <noreply@anthropic.com>`; push to GitHub
  (`Reydel-Gutierrez/high-school-baseball-manager`, branch `cursor/home-page-briefing-layout`) after each piece of work.
- New scripts/art make Godot write `.uid` / `.import` files: commit them with the change.
- After changing a `.tscn` from outside the editor, the editor shows "Files modified outside Godot":
  choose **Reload from disk** (otherwise the editor can save the stale scene over yours).
- Don't add trading (removed on purpose: unrealistic for high school). `PlayerTransaction.Trade` stays only for old saves.

## UI redesign (October 2026) — in progress
Target look: `docs/ui-concepts/01..23_*.png` (rendered mockups; numbers in them are illustrative).
One left rail, one gold CONTINUE button, team-branded banners, two text sizes per card.

Building blocks (use these, don't hand-roll styles):
- `game/scripts/ui/UiKit.cs` — tokens (`Bg #0b0d12`, `Card #12151c`, `Line #232733`, `Text`, `Muted #8a909c`,
  `Gold #C5A253`, `Green`, `Red`), Barlow Condensed fonts, `MakeCard`, `CardHeader`, `Action`, `Pill`, `Kpi`,
  `KpiLive` (updatable KPI tile), `PageHeader`, `Meter`, `Box`.
- `ShellRail.cs` (left rail), `SectionBar.cs` (section tabs), shell wiring in `AppNavigator.BuildShell`
  (`ViewSections` maps each view to rail section + tab; `TabsFor` lists a section's tabs).
- `BroadcastPanel` — team banner art + team-color gradient; `UseTeam(orgId)`. Use for any featured player/team.
- `PlayerFaces.For(teamId, name, w, h, circle, level)` / `PlayerFaces.For(player, w, h)` — capped headshots.
- `TeamBrands` / `SchoolBrandCatalog` — names, mascots, locations, colors, logo/banner art.

Status:
- [x] Phase 1 shell: rail, slim top bar (date, phase pill, CONTINUE + ▾ simulate menu), section tabs.
- [x] Phase 2 Home (`HomePage.Dashboard.cs`): team banner + "Needs your attention", next game card
      (strength bars, starter banners), week strip, inbox.
- [x] Global skin pass: gold accent everywhere (old orange `0.956863f, 0.643137f, 0.109804f` replaced),
      rounded cards, flat dark canvas (pages' `BackgroundImage` hidden in `BuildShell`), 36px page titles.
- [x] Phase 3 Team: ballfield depth chart (`DepthChartField.cs`), pitching staff + medical KPI rows.
- [x] Phase 4 Season: calendar cells/chips, live game broadcast layout (pitcher | field | batter),
      game preview faces, graduation send-off banner. Duplicate CALENDAR/SCHEDULE pills hidden (`SeasonDeskTabs`).
- [ ] Phase 5 League (concepts 12–16): districts, standings as a 2×2 grid of the four districts, leaders,
      rankings & awards, news. Remove the Districts page's own STANDINGS pill (section tabs cover it).
- [ ] Phase 6 Scout & Office (17–21): player search, career & mail, board & goals, budget & sponsors, job market.
- [ ] Phase 7 Main menu & coach creator (22–23). Creator: RANDOMIZE should use the coherent appearance
      generator; at 3428×1368 its CONTINUE button falls off-screen.
- [ ] Verify each phase in game, push, update the project doc.

Ideas not built yet (need sim support first): pitch-count history and rest days on Pitching Staff,
arm-care workload on Medical, tryout "first look" tags.

## After the redesign
Brand the other 30 schools like Northfield and Marblehead (`SchoolBrandCatalog` entry + `assets/teams/{id}/logo.png`
1024² transparent + `banner.png` 1600×807). Several catalog schools are real (Sandusky, Westlake, Hibbing):
rename them to fictional ones. Canva art: the owner downloads PNGs into `branding_inbox/` (git-ignored);
`tools/brand_cutout.py` makes the transparent logo.

## Face engine
`HeadshotStudio` renders 560×700 headshots (cached in `user://headshots`, keyed by `FaceRecipe.RenderVersion` —
bump it when shaders change). Shaders in `game/assets/facekit/`; bakers and a WebGL harness in `tools/facekit/`.
