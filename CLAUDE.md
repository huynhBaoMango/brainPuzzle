# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity 6 (`6000.3.10f1`) C# mobile game (Android/iOS) using URP. All custom gameplay/infrastructure code lives under `Assets/Bao/` and follows an in-house framework called **Bao**.

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
