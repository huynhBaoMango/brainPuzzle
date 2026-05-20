# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity 6 (`6000.3.10f1`) C# mobile game (Android/iOS) using URP. Custom code is split between two systems:
- `Assets/Bao/` — in-house **Bao** framework (singletons, save data, audio, dialogs, ads, analytics).
- `Assets/Script/Object/` — **Level Authoring System** for a Brain Out-style puzzle game. Levels are designed in Unity, exported to JSON, and consumed by a separate LibGDX runtime. Spine-Unity is used heavily.

## Build / Run / Test

- Open the project in Unity Hub with Unity `6000.3.10f1`. Primary scene: `Assets/Scenes/SampleScene.unity`.
- No npm/make scripts. Builds run via the Unity Editor or CLI, e.g.:
  ```
  Unity -batchmode -projectPath . -buildTarget Android -quit
  ```
- Tests use the Unity Test Framework (`com.unity.test-framework` 1.6.0). Run from **Window > General > Test Runner** in the editor, or via CLI:
  ```
  Unity -runTests -batchmode -projectPath . -testResults results.xml -testPlatform PlayMode
  ```
  Use `-testPlatform EditMode` for edit-mode tests. Filter to a single test with `-testFilter "Namespace.ClassName.MethodName"`.
- No project-specific tests currently exist under `Assets/Bao/`; only the third-party `Assets/NaughtyAttributes/Scripts/Test/` suite is present.

## Architecture: the Bao Framework

All custom code is `B_`-prefixed and organized by module under `Assets/Bao/<Module>/`. Modules: `Ads`, `Audio`, `CrossPromo`, `Data`, `Dialog`, `IAP`, `LoadingScene`, `Localization`, `LuckySpin`, `SceneTransition`, `Tracker`, `ZenSDK`.

Each module typically exposes a `MonoBehaviour` singleton (`Instance` property + `DontDestroyOnLoad`) that is initialized from the loading scene and persists across scene loads.

Key entry points to read first:

- `Assets/Bao/Data/B_VariableDatabase.cs` — JSON-backed key/value save system at `Application.persistentDataPath`. Async init via `TaskCompletionSource`. **All persistent state should go through this**, not `PlayerPrefs` or ad-hoc file IO.
- `Assets/Bao/Data/B_PlayerDataHelper.cs` — central typed accessors for player state (stars, level, items).
- `Assets/Bao/Audio/B_AudioManager.cs` — pooled `AudioSource` BGM/SFX manager (default pool size 15). Respects `B_BoolSO` toggles for music/SFX/vibration.
- `Assets/Bao/SceneTransition/B_SceneController.cs` — coroutine-based scene loading with transition animations. Builds the global UI root canvas hierarchy that survives scene loads.
- `Assets/Bao/Dialog/B_BaseDialog.cs` — base class for popups/modals. Animations are pluggable via `DialogAnimationSO` (e.g. `FadeScaleAnimationSO`, `SlideAnimationSO`).
- `Assets/Bao/Tracker/B_Tracker.cs` — analytics wrapper (level start/complete/duration), routes through Firebase.
- `Assets/Bao/ZenSDK/` — wraps Firebase + Google Play Services (in-app review, in-app update, etc.).
- `Assets/Bao/ConstValue.cs` — global constants.

## Data Conventions

- Configuration is **ScriptableObject-based**, not JSON/YAML.
- Typed variable SOs: `B_IntSO`, `B_BoolSO`, `B_StringSO` — all extend `B_VariableSO_Base`.
- Variable changes flow through `B_DataEvent` (C# `Action`-based). Subscribe to these events rather than polling.
- To add persistent state, register a key in `B_VariableDatabase` and expose it through `B_PlayerDataHelper` if it's player-facing.

## Third-Party Libraries Already Present

Prefer these over adding new dependencies:

- **DoTween** — animation/tweening
- **NaughtyAttributes** — inspector polish (`[Button]`, `[ShowIf]`, etc.)
- **Newtonsoft.Json** — JSON serialization
- **Firebase**, **GoogleMobileAds** (with mediation), **GooglePlayPlugins**
- **Unity IAP**, **Unity Localization**, **URP**, **Input System**

## Conventions to Follow

- Prefix new framework classes with `B_` and place them in the matching `Assets/Bao/<Module>/` folder.
- New singletons follow the existing `Instance` + `DontDestroyOnLoad` pattern.
- Long-running initialization uses coroutines or `TaskCompletionSource` (see `B_VariableDatabase` for the canonical example).
- UI that must survive scene loads should attach to the global UI root created by `B_SceneController`.

## Architecture: Level Authoring System (`Assets/Script/Object/`)

A second, mostly-independent code area. Levels are authored as Unity scene hierarchies, exported to JSON, and re-importable from that JSON. The JSON is also consumed by a separate LibGDX runtime — **every change here must round-trip cleanly AND keep the LibGDX schema usable**.

### Core scene components

- `B_LevelConfig` — scene-level config (time limit, win/lose conditions, time-up target). Exposes static `OnLevelEnded(bool isWin)` and `EvaluateOutcome()`. Win = ALL conditions met, Lose = ANY met.
- `B_InteractableObject` — tappable/draggable object with an `ObjectData` (states + state-driven actions). Has its own `ObjectId`. Sort order falls back to `MeshRenderer` for Spine objects (see `GetSortOrder`).
- `B_InteractableGroup` — multiple member GOs sharing one set of states.
- `B_InteractableQueue` — ordered line of members with `slots[]`. Members shift up on serve; `tailFollowers` trail the line but never serve. Followers can be authored as **independent top-level interactables** referenced by id (see `objectIdRef` in `GroupMemberJson`) — don't spawn duplicates on import.
- `B_StaticObject` — non-interactable scenery; may host nested drop zones on the same GameObject.
- `B_DropZone` — drop target. Can live standalone OR nested under a static/interactable (then it's serialized inside that parent's `dropZones[]` to preserve the "one GameObject hosts both" pattern).
- `B_SpineSkinSet` — runtime multi-skin combine for Spine. Backs the `SkinChange` action (Add/Remove/Toggle).
- `B_LevelTimerRunner` — Play-mode helper that reads `B_LevelConfig.timeLimit` and force-activates `timeUpStateId` on expiry. Editor-only convention — not written to JSON; LibGDX runs its own countdown.

### StateAction system

`StateAction.cs` defines 10 action types: `Wait`, `MoveTo`, `Disappear`, `Appear`, `DoAnimation`, `ActivateState` (with `chainGuards`), `AdvanceQueue`, `PlaySFX`, `SkinChange`, `ScaleTo`. Every action must be wired in **four** places — drift between them is the #1 source of round-trip bugs:

1. `LevelExporterWindow.BuildAction` (write) — `Editor/LevelExporterWindow.cs`
2. `LevelImporterWindow.ImportAction` (read) — `Editor/LevelImporterWindow.cs`
3. The runtime `RunActions` coroutines in `B_InteractableObject`, `B_InteractableGroup`, `B_InteractableQueue`
4. The LibGDX runtime (external repo)

### Exporter / Importer (`Editor/LevelExporterWindow.cs`, `Editor/LevelImporterWindow.cs`)

Accessed via `Tools/Puzzle/Level Exporter` and `Tools/Puzzle/Level Importer` menus.

**JSON optimization** is aggressive — `NullValueHandling.Ignore` + `DefaultValueHandling.Ignore` are set globally. To control what's written:

- `[System.ComponentModel.DefaultValue(x)]` — field omitted when equal to `x`. Used for `duration=0.4f`, `scale=1f`.
- `[JsonProperty(DefaultValueHandling=Include)]` — **always** write, even at default. Used for `initSpineLoop` (LibGDX needs explicit `true`/`false`).
- **Nullable types** (`float?`) — only emit when explicitly assigned. Used for `scaleTarget` so non-ScaleTo actions don't drag along `"scaleTarget": 0.0`.
- `ActionUsesActionTarget` gates `actionTargetId` to action types that actually use it.

**Round-trip invariants** (anything that breaks these has caused real bugs):

- The exporter scans `FindObjectsByType<B_*>` — but skips `B_DropZone`s nested under a static or interactable, since those go into the parent's `dropZones[]`.
- Importer's `ClearExistingLevel` must destroy GOs referenced by queue `Members` / `TailFollowers` / group `Members` **before** destroying the queue/group, because some setups author them as top-level **siblings** (no `B_*` component) and they'd otherwise survive cleanup and duplicate on re-import.
- Queue/group `data.initSpineAnim` lives in the JSON `data` block. The queue/group GO has no skeleton — on import the importer sets the **inspector** `_animationName` + `loop` fields on each member's `SkeletonAnimation` via `SerializedObject`. (Calling `PlaySpineAnim` alone doesn't persist; the field stays blank on next Play.)
- Cross-object references (action targets, `timeUpTarget`, `queueEmptyTarget`) are resolved in a **second pass** via `pendingRefs`, because referenced GOs may not exist yet when their referrers are spawned.
- `MoveTo` with no `objectId`/`zoneId` falls back to spawning a `_moveAnchor` GameObject at `moveTargetPosition`.
- Coordinates are in **pixels relative to camera world position** at export time (`WorldToPx` uses `camera.position` + the configured PPU as origin). LibGDX must mirror this convention.

### Audio in level JSON

`initSFX`, `stateSFX`, and `sfxClip` (on PlaySFX actions) export as asset-relative paths. The importer's `AssignAudioClip` helper restores the `AudioClip` references — don't expect Unity to do this automatically.

### Designer-facing docs

`Assets/Script/Object/HUONG_DAN.md` — Vietnamese authoring guide. Keep it brief.
