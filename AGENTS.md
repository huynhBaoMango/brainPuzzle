# AGENTS.md - Brain Puzzle Development Guide

This file provides guidance for AI agents operating in this Unity project.

## Project Overview

- **Engine**: Unity 6 (6000.3.10f1)
- **Platform**: Android/iOS mobile game using URP
- **Custom Code**: Lives under `Assets/Bao/` (framework) and `Assets/Script/Object/` (gameplay)
- **Framework**: In-house "Bao" framework with B_-prefixed classes

## Build / Run / Test Commands

### Opening the Project
```bash
# Open via Unity Hub with Unity 6000.3.10f1
# Primary scene: Assets/Scenes/SampleScene.unity
```

### Running Tests
```bash
# Run all play-mode tests via CLI
Unity -runTests -batchmode -projectPath . -testResults results.xml -testPlatform PlayMode

# Run all edit-mode tests
Unity -runTests -batchmode -projectPath . -testResults results.xml -testPlatform EditMode

# Run a single test (filter by full method name)
Unity -runTests -batchmode -projectPath . -testResults results.xml -testPlatform PlayMode -testFilter "Namespace.ClassName.MethodName"
```

### Building
```bash
# Android build via Unity CLI
Unity -batchmode -projectPath . -buildTarget Android -quit
```

### Editor Windows (Tools Menu)
- **Tools > Puzzle > Level Config** - Edit level metadata, strings, win/lose conditions
- **Tools > Puzzle > Level Exporter** - Export scene to JSON for LibGDX runtime
- **Tools > Puzzle > Level Importer** - Import JSON levels to Unity

---

## Code Style Guidelines

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes | PascalCase, B_ prefix for framework | `B_InteractableObject`, `B_AudioManager` |
| Methods | PascalCase | `HandlePress()`, `TryActivateMatching()` |
| Fields/Properties | PascalCase (public), _camelCase (private) | `ObjectId`, `_actionLockCount` |
| Constants | PascalCase | `MaxTapDistance` |
| Enums | PascalCase | `InteractType`, `StateActionType` |
| Interfaces | I prefix | `IB_Initializable` |

### File Organization

```csharp
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

// Namespace matches folder path
namespace MyNamespace
{
    /// <summary>
    /// Class-level doc comment for public classes.
    /// </summary>
    public class MyClass : MonoBehaviour
    {
        // ============================================================
        //  INSPECTOR (serialized fields grouped by purpose)
        // ============================================================
        
        [Header("Identity")]
        [Tooltip("Description for tooltip.")]
        [SerializeField] private string objectId;

        [Header("Data")]
        [SerializeField] private ObjectData data;

        // ============================================================
        //  PUBLIC PROPERTIES (read-only access)
        // ============================================================
        
        public string ObjectId => objectId;

        // ============================================================
        //  UNITY LIFECYCLE
        // ============================================================
        
        private void Awake() { }
        private void Start() { }
        private void Update() { }

        // ============================================================
        //  PRIVATE METHODS
        // ============================================================
        
        private void DoSomething() { }
    }
}
```

### Type Guidelines

- **Prefer** `List<T>` over arrays for dynamic collections
- **Use** `System.Action` / `Func<T>` for callbacks (C# built-in, not Unity Events unless needed)
- **Use** `bool` for flags, not int/byte
- **Use** `string.Empty` instead of `""`
- **Never** suppress warnings with `#pragma` unless absolutely necessary

### Property Drawers (Editor Code)

- Custom drawers: `[CustomPropertyDrawer(typeof(MyClass))]`
- Attribute definitions: Place in separate file from drawer when possible
- Use `SerializedProperty` for all inspector GUI - never `EditorGUIUtility` for direct field access
- Property drawers must handle both draw and measure modes in `GetPropertyHeight`

### Error Handling

- **Never** use empty catch blocks: `catch(Exception e) { }`
- **Always** log meaningful errors: `Debug.LogError($"Failed to load: {path}")`
- **Use** `try/catch` for async operations and deserialization
- **Never** suppress type errors with `as any` or `@ts-ignore` equivalents

### Import Ordering

1. System (System.*)
2. System.Collections / System.Collections.Generic
3. Third-party (Newtonsoft.*, DG.*, Spine.*)
4. Unity (UnityEngine, UnityEditor)
5. Project (Bao.*, Script.*)

### Unity-Specific Patterns

- **Singletons**: Use `Instance` property + `DontDestroyOnLoad`
  ```csharp
  public static MyClass Instance { get; private set; }
  private void Awake() { Instance = this; }
  ```
- **ScriptableObjects**: For configuration data, place in `Assets/Bao/Config/`
- **Coroutines**: Use for async operations; prefer `TaskCompletionSource` for async init (see `B_VariableDatabase`)
- **Events**: Use `B_DataEvent` (Action-based) for variable change notifications

### JSON Serialization

- Uses **Newtonsoft.Json** (not Unity's JsonUtility)
- Property naming: use `[JsonProperty("camelCase")]` or `CamelCasePropertyNamesContractResolver`
- Handle nulls explicitly with `NullValueHandling`

### Spine Integration

- SkeletonAnimation references stored as `Spine.Unity.SkeletonAnimation`
- Animation playback via `B_InteractableObject.PlaySpineAnim()`
- Editor animation picker via `[SpineAnim]` attribute + `SpineAnimDrawer`

---

## Key Files Reference

| File | Purpose |
|------|---------|
| `Assets/Bao/Data/B_VariableDatabase.cs` | JSON-backed key/value save system |
| `Assets/Bao/Data/B_PlayerDataHelper.cs` | Typed accessors for player state |
| `Assets/Bao/Audio/B_AudioManager.cs` | Pooled AudioSource manager |
| `Assets/Bao/SceneTransition/B_SceneController.cs` | Scene loading with transitions |
| `Assets/Bao/Dialog/B_BaseDialog.cs` | Base class for popups/modals |
| `Assets/Script/Object/B_InteractableObject.cs` | Core puzzle interaction |
| `Assets/Script/Object/Editor/LevelExporterWindow.cs` | Level JSON export |
| `Assets/CLAUDE.md` | Project-specific AI guidance |

---

## Common Tasks

### Adding New State Action Types

1. Add enum value to `StateActionType` (StateAction.cs)
2. Add runtime branch in `B_InteractableObject.RunAction()`
3. Add UI fields in `StateActionDrawer` (Editor)
4. Add export field in `LevelExporterWindow.BuildAction()`

### Adding Player Persistent Data

1. Register key in `B_VariableDatabase`
2. Expose via `B_PlayerDataHelper` if player-facing

---

Generated for Brain Puzzle Unity project