using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unified level importer. Two modes:
/// <list type="bullet">
///   <item><b>PSD Import</b> — reads a Photoshop JSON layer export + images/ folder
///     and spawns one SpriteRenderer per layer.</item>
///   <item><b>Level JSON Import</b> — reads an exported level JSON (from
///     LevelExporterWindow) and reconstructs interactables, groups, statics,
///     drop zones, and full ObjectData (states, actions, requirements) in
///     the scene. A two-pass approach resolves cross-object references
///     (action targets, move targets, activate targets) after all objects
///     are spawned.</item>
/// </list>
/// Open via <b>Tools &gt; Puzzle &gt; Level Importer</b>.
/// </summary>
public class LevelImporterWindow : EditorWindow
{
    private enum ImportMode { PSD, LevelJSON }

    private const string PrefKeyMode = "LevelImporter.mode";
    private const string PrefKeyJson = "LevelImporter.jsonPath";
    private const string PrefKeyPPU = "LevelImporter.pixelsPerUnit";
    private const string PrefKeyCanvasW = "LevelImporter.canvasW";
    private const string PrefKeyCanvasH = "LevelImporter.canvasH";
    private const string PrefKeyParent = "LevelImporter.parentName";
    private const string PrefKeyAssetRoot = "LevelImporter.assetRoot";
    private const string PrefKeyStripPrefix = "LevelImporter.stripPrefix";

    private ImportMode mode = ImportMode.PSD;
    private string jsonPath = "Assets/LevelAssets/lv1/lv1.json";
    private float pixelsPerUnit = 100f;

    /// <summary>
    /// Pixels-per-unit resolved at the start of the current import run.
    /// Set by ImportLevelJSON, consumed by ImportAction (specifically the
    /// MoveTo case, which has to spawn anchors at world positions but
    /// doesn't have ppu in its signature).
    /// </summary>
    private float currentPpu = 100f;
    private int canvasWidth = 1080;
    private int canvasHeight = 1920;
    private string parentName = "_Level";
    private string assetRoot = "Assets/LevelAssets/";
    private string stripPrefix = "assets/levels/";

    private Vector2 scroll;
    private string lastMessage;
    private MessageType lastMessageType;

    // Two-pass reference resolution
    private struct PendingRef
    {
        public SerializedObject so;
        public string actionPath;   // SerializedProperty path to the action element
        public string fieldName;    // "actionTarget", "moveTarget", or "activateTarget"
        public string targetId;     // objectId to resolve
        public string zoneId;       // optional zone for moveTarget
    }

    private Dictionary<string, GameObject> idMap;
    private Dictionary<string, B_InteractableQueue> queueMap;
    private List<PendingRef> pendingRefs;

    [MenuItem("Tools/Puzzle/Level Importer")]
    public static void Open()
    {
        var w = GetWindow<LevelImporterWindow>("Level Importer");
        w.minSize = new Vector2(460f, 340f);
        w.Show();
    }

    private void OnEnable()
    {
        mode = (ImportMode)EditorPrefs.GetInt(PrefKeyMode, 0);
        jsonPath = EditorPrefs.GetString(PrefKeyJson, jsonPath);
        pixelsPerUnit = EditorPrefs.GetFloat(PrefKeyPPU, pixelsPerUnit);
        canvasWidth = EditorPrefs.GetInt(PrefKeyCanvasW, canvasWidth);
        canvasHeight = EditorPrefs.GetInt(PrefKeyCanvasH, canvasHeight);
        parentName = EditorPrefs.GetString(PrefKeyParent, parentName);
        assetRoot = EditorPrefs.GetString(PrefKeyAssetRoot, assetRoot);
        stripPrefix = EditorPrefs.GetString(PrefKeyStripPrefix, stripPrefix);
    }

    private void OnDisable()
    {
        EditorPrefs.SetInt(PrefKeyMode, (int)mode);
        EditorPrefs.SetString(PrefKeyJson, jsonPath);
        EditorPrefs.SetFloat(PrefKeyPPU, pixelsPerUnit);
        EditorPrefs.SetInt(PrefKeyCanvasW, canvasWidth);
        EditorPrefs.SetInt(PrefKeyCanvasH, canvasHeight);
        EditorPrefs.SetString(PrefKeyParent, parentName);
        EditorPrefs.SetString(PrefKeyAssetRoot, assetRoot);
        EditorPrefs.SetString(PrefKeyStripPrefix, stripPrefix);
    }

    // ============================================================
    //  GUI
    // ============================================================

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Level Importer", EditorStyles.boldLabel);

        mode = (ImportMode)EditorGUILayout.EnumPopup("Import Mode", mode);

        EditorGUILayout.Space();

        // JSON path (shared by both modes)
        using (new EditorGUILayout.HorizontalScope())
        {
            jsonPath = EditorGUILayout.TextField("JSON File", jsonPath);
            if (GUILayout.Button("…", GUILayout.Width(24f)))
            {
                string dir = string.IsNullOrEmpty(jsonPath)
                    ? "Assets/" : Path.GetDirectoryName(jsonPath);
                string picked = EditorUtility.OpenFilePanel("Pick JSON", dir, "json");
                if (!string.IsNullOrEmpty(picked))
                    jsonPath = ToProjectRelativePath(picked);
            }
        }

        parentName = EditorGUILayout.TextField(
            new GUIContent("Parent Object", "All spawned objects go under this root."),
            parentName);

        if (mode == ImportMode.PSD)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("PSD Settings", EditorStyles.boldLabel);
            pixelsPerUnit = EditorGUILayout.FloatField("Pixels Per Unit", pixelsPerUnit);
            canvasWidth = EditorGUILayout.IntField("Canvas Width (px)", canvasWidth);
            canvasHeight = EditorGUILayout.IntField("Canvas Height (px)", canvasHeight);
        }
        else
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Level JSON Settings", EditorStyles.boldLabel);
            stripPrefix = EditorGUILayout.TextField(
                new GUIContent("Strip Prefix (fallback)",
                    "Stripped from sprite paths in the JSON (inverse of the exporter's \"Output Asset Prefix\"). Auto-overridden when the JSON contains an \"assetPathPrefix\" field. e.g. \"assets/levels/\" turns \"assets/levels/lv1/images/foo.png\" into \"lv1/images/foo.png\"."),
                stripPrefix);
            assetRoot = EditorGUILayout.TextField(
                new GUIContent("Asset Root Prefix",
                    "Prepended to sprite paths after stripping to form Unity asset paths. e.g. \"Assets/LevelAssets/\" turns \"lv1/images/foo.png\" into \"Assets/LevelAssets/lv1/images/foo.png\"."),
                assetRoot);
            EditorGUILayout.HelpBox(
                "Imports the full level: interactables (with states, actions, requirements), " +
                "groups, static objects, drop zones, and B_LevelConfig.\n\n" +
                "Tip: recent exports embed an \"assetPathPrefix\" in the JSON, so the Strip Prefix is auto-detected on import. The field above is only used for legacy JSONs without that metadata.",
                MessageType.Info);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Import into Scene", GUILayout.Height(32f)))
        {
            if (mode == ImportMode.PSD) ImportPSD();
            else ImportLevelJSON();
        }

        if (!string.IsNullOrEmpty(lastMessage))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(lastMessage, lastMessageType);
        }

        EditorGUILayout.EndScrollView();
    }

    // ============================================================
    //  PSD IMPORT (same as before)
    // ============================================================

    private void ImportPSD()
    {
        try
        {
            // Normalize first — AssetDatabase paths must be relative to project root.
            jsonPath = ToProjectRelativePath(jsonPath);

            if (!File.Exists(jsonPath))
            { ShowMessage($"File not found: {jsonPath}", MessageType.Error); return; }

            string jsonFolder = Path.GetDirectoryName(jsonPath)?.Replace("\\", "/") ?? "";
            string imagesFolder = jsonFolder + "/images";
            if (!Directory.Exists(imagesFolder))
            { ShowMessage($"Images folder not found: {imagesFolder}", MessageType.Error); return; }

            string jsonText = File.ReadAllText(jsonPath);
            JObject root = JObject.Parse(jsonText);
            JObject layers = root["default"] as JObject;
            if (layers == null)
            { ShowMessage("JSON has no \"default\" key.", MessageType.Error); return; }

            Transform parent = FindOrCreateParent(parentName);
            float ppu = Mathf.Max(0.001f, pixelsPerUnit);
            int sortOrder = 0;
            int spawned = 0;
            List<string> warnings = new List<string>();

            foreach (var entry in layers)
            {
                string name = entry.Key;
                JObject inner = entry.Value as JObject;
                JObject data = inner?[name] as JObject;
                if (data == null) { warnings.Add($"Skipped '{name}': bad structure."); continue; }

                float psdX = data["x"]?.Value<float>() ?? 0f;
                float psdY = data["y"]?.Value<float>() ?? 0f;

                string assetPath = $"{imagesFolder}/{name}.png";
                Sprite sprite = LoadOrFixSprite(assetPath, ppu);
                if (sprite == null) { warnings.Add($"Skipped '{name}': no sprite."); continue; }

                float halfW = canvasWidth * 0.5f;
                float halfH = canvasHeight * 0.5f;
                float worldX = (psdX - halfW) / ppu;
                float worldY = (psdY + halfH) / ppu;

                GameObject go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Import PSD Layer");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(worldX, worldY, 0f);

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = sortOrder++;
                spawned++;
            }

            string msg = $"PSD: Imported {spawned} layer(s) under '{parentName}'.";
            if (warnings.Count > 0) msg += "\n  • " + string.Join("\n  • ", warnings);
            ShowMessage(msg, warnings.Count > 0 ? MessageType.Warning : MessageType.Info);
            Selection.activeGameObject = parent.gameObject;
        }
        catch (System.Exception e) { ShowMessage("PSD import failed: " + e.Message, MessageType.Error); Debug.LogException(e); }
    }

    // ============================================================
    //  LEVEL JSON IMPORT
    // ============================================================

    private void ImportLevelJSON()
    {
        try
        {
            // Normalize first — AssetDatabase paths must be relative to project root.
            jsonPath = ToProjectRelativePath(jsonPath);

            if (!File.Exists(jsonPath))
            { ShowMessage($"File not found: {jsonPath}", MessageType.Error); return; }

            string jsonText = File.ReadAllText(jsonPath);
            JObject root = JObject.Parse(jsonText);

            // If the JSON carries the prefix it was exported with, use it.
            // Falls back to the UI field value for legacy JSONs.
            string jsonPrefix = root["assetPathPrefix"]?.Value<string>();
            if (!string.IsNullOrEmpty(jsonPrefix))
                stripPrefix = jsonPrefix;

            // Clear any existing level before importing so fields reflect the
            // new JSON exactly (no leftovers from a previous level).
            ClearExistingLevel();

            Transform parent = FindOrCreateParent(parentName);

            int vw = root["viewport"]?["virtualWidth"]?.Value<int>() ?? 1080;
            int vh = root["viewport"]?["virtualHeight"]?.Value<int>() ?? 1920;
            float ppu = vh > 0 ? vh / 10.8f : 100f; // derive from viewport

            // Use the camera in the scene to derive ppu if available
            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
                ppu = vh / (cam.orthographicSize * 2f);

            currentPpu = ppu;

            int totalSpawned = 0;
            List<string> warnings = new List<string>();

            // Prepare two-pass structures.
            idMap = new Dictionary<string, GameObject>();
            queueMap = new Dictionary<string, B_InteractableQueue>();
            pendingRefs = new List<PendingRef>();

            // ---- Pass 1: Spawn all objects ----

            // LevelConfig
            SpawnLevelConfig(root, parent, vw, vh, cam);

            // Interactables
            JArray interactables = root["interactables"] as JArray;
            if (interactables != null)
            {
                foreach (JToken it in interactables)
                {
                    SpawnInteractable(it, parent, ppu, warnings);
                    totalSpawned++;
                }
            }

            // Groups
            JArray groups = root["groups"] as JArray;
            if (groups != null)
            {
                foreach (JToken grp in groups)
                {
                    SpawnGroup(grp, parent, ppu, warnings);
                    totalSpawned++;
                }
            }

            // Queues
            JArray queues = root["queues"] as JArray;
            if (queues != null)
            {
                foreach (JToken q in queues)
                {
                    SpawnQueue(q, parent, ppu, warnings);
                    totalSpawned++;
                }
            }

            // Static objects
            JArray statics = root["staticObjects"] as JArray;
            if (statics != null)
            {
                foreach (JToken s in statics)
                {
                    SpawnStaticObject(s, parent, ppu, warnings);
                    totalSpawned++;
                }
            }

            // Standalone drop zones
            JArray zones = root["dropZones"] as JArray;
            if (zones != null)
            {
                foreach (JToken z in zones)
                {
                    SpawnDropZone(z, parent, ppu, warnings);
                    totalSpawned++;
                }
            }

            // ---- Pass 2: Resolve cross-object references ----
            ResolvePendingRefs(warnings);

            // Cleanup
            idMap = null;
            queueMap = null;
            pendingRefs = null;

            string msg = $"Level JSON: Spawned {totalSpawned} object(s) under '{parentName}'.";
            if (warnings.Count > 0) msg += "\n  • " + string.Join("\n  • ", warnings);
            ShowMessage(msg, warnings.Count > 0 ? MessageType.Warning : MessageType.Info);
            Selection.activeGameObject = parent.gameObject;
        }
        catch (System.Exception e) { ShowMessage("Level JSON import failed: " + e.Message, MessageType.Error); Debug.LogException(e); }
    }

    // ---- Level config ----

    private void SpawnLevelConfig(JObject root, Transform parent, int vw, int vh, Camera cam)
    {
        GameObject go = new GameObject("_LevelConfig");
        Undo.RegisterCreatedObjectUndo(go, "Import LevelConfig");
        go.transform.SetParent(parent, false);

        B_LevelConfig cfg = go.AddComponent<B_LevelConfig>();
        cfg.levelId = root["levelId"]?.Value<string>() ?? "";
        cfg.title = root["title"]?.Value<string>() ?? "";
        cfg.description = root["description"]?.Value<string>() ?? "";
        cfg.defaultHintMessageKey = root["defaultHintMessageKey"]?.Value<string>() ?? "";
        cfg.virtualWidth = vw;
        cfg.virtualHeight = vh;
        cfg.levelCamera = cam;

        // Fallback: if levelId is empty in the JSON, use the folder name.
        if (string.IsNullOrEmpty(cfg.levelId))
        {
            string folderName = Path.GetFileName(Path.GetDirectoryName(jsonPath) ?? "");
            if (!string.IsNullOrEmpty(folderName)) cfg.levelId = folderName;
        }

        // Win / lose conditions.
        cfg.winConditions = ImportConditions(root["win"] as JArray);
        cfg.loseConditions = ImportConditions(root["lose"] as JArray);

        // Import strings.json if it exists alongside the level JSON.
        ImportStringsJson(cfg);
    }

    private List<LevelCondition> ImportConditions(JArray arr)
    {
        List<LevelCondition> list = new List<LevelCondition>();
        if (arr == null) return list;

        string[] enumNames = System.Enum.GetNames(typeof(LevelConditionType));
        foreach (JToken c in arr)
        {
            if (c == null || c.Type == JTokenType.Null) continue;

            LevelCondition cond = new LevelCondition
            {
                targetId = c["objectId"]?.Value<string>() ?? "",
                stateId = c["stateId"]?.Value<string>() ?? "",
            };

            // Exporter writes the type in camelCase ("stateActivated"); the
            // enum values are PascalCase ("StateActivated"). Match loosely so
            // we're resilient to casing drift from hand-edits too.
            string typeStr = c["type"]?.Value<string>();
            if (!string.IsNullOrEmpty(typeStr))
            {
                for (int i = 0; i < enumNames.Length; i++)
                {
                    if (string.Equals(enumNames[i], typeStr, System.StringComparison.OrdinalIgnoreCase))
                    {
                        cond.type = (LevelConditionType)i;
                        break;
                    }
                }
            }

            list.Add(cond);
        }
        return list;
    }

    private void ImportStringsJson(B_LevelConfig cfg)
    {
        string dir = Path.GetDirectoryName(jsonPath);
        if (string.IsNullOrEmpty(dir)) return;

        string stringsPath = Path.Combine(dir, "strings.json");
        if (!File.Exists(stringsPath)) return;

        try
        {
            string text = File.ReadAllText(stringsPath);
            JArray arr = JArray.Parse(text);
            cfg.strings = new List<LevelString>();

            foreach (JToken entry in arr)
            {
                cfg.strings.Add(new LevelString
                {
                    key = entry["key"]?.Value<string>() ?? "",
                    en = entry["en"]?.Value<string>() ?? "",
                    vn = entry["vn"]?.Value<string>() ?? "",
                });
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to import strings.json: {e.Message}");
        }
    }

    // ---- Interactable ----

    private void SpawnInteractable(JToken it, Transform parent, float ppu, List<string> warnings)
    {
        string id = it["objectId"]?.Value<string>() ?? "unnamed";
        GameObject go = new GameObject(id);
        Undo.RegisterCreatedObjectUndo(go, "Import Interactable");
        go.transform.SetParent(parent, false);

        SetTransform(go, it, ppu);

        // Spine mode: if spineBasePath is set, attach a SkeletonAnimation
        // driven by the matching SkeletonDataAsset. Otherwise sprite mode
        // with a plain SpriteRenderer.
        string spineBase = it["spineBasePath"]?.Value<string>();
        Spine.Unity.SkeletonAnimation skeleton = null;
        SpriteRenderer sr = null;

        if (!string.IsNullOrEmpty(spineBase))
        {
            var dataAsset = ResolveSkeletonDataAsset(spineBase, warnings);
            if (dataAsset != null)
            {
                // Attach SkeletonAnimation (+ MeshRenderer + MeshFilter + material)
                // onto the SAME GameObject as the interactable. Matches the
                // designer's authoring layout — one GO with everything on it.
                skeleton = Spine.Unity.SkeletonRenderer
                    .AddSpineComponent<Spine.Unity.SkeletonAnimation>(go, dataAsset);

                if (go.GetComponent<MeshRenderer>() is MeshRenderer mr)
                    mr.sortingOrder = it["sortOrder"]?.Value<int>() ?? 0;

                // Preview the init animation at edit time.
                string initAnim = it["data"]?["initSpineAnim"]?.Value<string>();
                bool initLoop = it["data"]?["initSpineLoop"]?.Value<bool>() ?? true;
                B_InteractableObject.PlaySpineAnim(skeleton, initAnim, initLoop);
            }
        }
        else
        {
            string spritePath = it["data"]?["initSprite"]?.Value<string>();
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = it["sortOrder"]?.Value<int>() ?? 0;
            if (!string.IsNullOrEmpty(spritePath))
            {
                string resolved = ResolveImportPath(spritePath);
                Sprite sprite = LoadOrFixSprite(resolved, 100f);
                if (sprite != null) sr.sprite = sprite;
                else warnings.Add($"Sprite not found: \"{spritePath}\" → looked up at \"{resolved}\"");
            }
        }

        // Collider
        AddCollider(go, it["collider"], ppu);

        // B_InteractableObject
        B_InteractableObject interactable = go.AddComponent<B_InteractableObject>();
        SerializedObject so = new SerializedObject(interactable);
        so.FindProperty("objectId").stringValue = id;
        so.FindProperty("startHidden").boolValue = it["startHidden"]?.Value<bool>() ?? false;
        if (skeleton != null)
            so.FindProperty("skeleton").objectReferenceValue = skeleton;
        SetVisualMode(so, it["visualMode"]);

        // ObjectData (states, actions, requirements)
        JToken dataJson = it["data"];
        if (dataJson != null)
        {
            SerializedProperty dataProp = so.FindProperty("data");
            ImportObjectData(dataProp, dataJson, so, warnings);
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        // Register for cross-object reference resolution.
        if (!string.IsNullOrEmpty(id))
            idMap[id] = go;

        // Nested drop zones
        JArray nestedZones = it["dropZones"] as JArray;
        if (nestedZones != null)
        {
            foreach (JToken nz in nestedZones)
                SpawnNestedDropZone(nz, go, ppu, warnings);
        }

        ApplyInitialSkins(go, it);
    }

    // ---- Group ----

    private void SpawnGroup(JToken grp, Transform parent, float ppu, List<string> warnings)
    {
        GameObject go = new GameObject("group");
        Undo.RegisterCreatedObjectUndo(go, "Import Group");
        go.transform.SetParent(parent, false);

        SetPosition(go, grp, ppu);

        // Collider
        AddCollider(go, grp["collider"], ppu);

        // B_InteractableGroup
        B_InteractableGroup group = go.AddComponent<B_InteractableGroup>();
        SerializedObject so = new SerializedObject(group);
        so.FindProperty("sortOrder").intValue = grp["sortOrder"]?.Value<int>() ?? 0;

        string pickMode = grp["pickMode"]?.Value<string>() ?? "First";
        SerializedProperty modeProp = so.FindProperty("pickMode");
        modeProp.enumValueIndex = System.Array.IndexOf(
            System.Enum.GetNames(typeof(B_InteractableGroup.PickMode)), pickMode);
        if (modeProp.enumValueIndex < 0) modeProp.enumValueIndex = 0;

        SetVisualMode(so, grp["visualMode"]);

        // ObjectData (states, actions, requirements)
        JToken dataJson = grp["data"];
        if (dataJson != null)
        {
            SerializedProperty dataProp = so.FindProperty("data");
            ImportObjectData(dataProp, dataJson, so, warnings);
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        // Members
        JArray members = grp["members"] as JArray;
        if (members != null)
        {
            so.Update();
            SerializedProperty membersProp = so.FindProperty("members");
            membersProp.arraySize = members.Count;

            for (int i = 0; i < members.Count; i++)
            {
                JToken m = members[i];
                string mSpritePath = m["sprite"]?.Value<string>();
                string mSpineBase = m["spineBasePath"]?.Value<string>();

                GameObject memberGo = new GameObject($"member_{i}");
                Undo.RegisterCreatedObjectUndo(memberGo, "Import Group Member");
                memberGo.transform.SetParent(go.transform, false);

                // Exporter wrote member positions in camera/world space (same
                // frame as the group itself), so we must set the WORLD
                // position here. SetPosition would have set localPosition
                // and caused the member to be offset by the group's position.
                float mx = m["position"]?["x"]?.Value<float>() ?? 0f;
                float my = m["position"]?["y"]?.Value<float>() ?? 0f;
                memberGo.transform.position = new Vector3(mx / ppu, my / ppu, 0f);

                if (!string.IsNullOrEmpty(mSpineBase))
                {
                    // Spine member: attach directly to memberGo, no child.
                    var dataAsset = ResolveSkeletonDataAsset(mSpineBase, warnings);
                    if (dataAsset != null)
                    {
                        Spine.Unity.SkeletonRenderer
                            .AddSpineComponent<Spine.Unity.SkeletonAnimation>(memberGo, dataAsset);

                        if (memberGo.GetComponent<MeshRenderer>() is MeshRenderer mr)
                            mr.sortingOrder = m["sortOrder"]?.Value<int>() ?? 0;
                    }
                }
                else
                {
                    SpriteRenderer msr = memberGo.AddComponent<SpriteRenderer>();
                    msr.sortingOrder = m["sortOrder"]?.Value<int>() ?? 0;
                    if (!string.IsNullOrEmpty(mSpritePath))
                    {
                        string mResolved = ResolveImportPath(mSpritePath);
                        Sprite sprite = LoadOrFixSprite(mResolved, 100f);
                        if (sprite != null) msr.sprite = sprite;
                        else warnings.Add($"Member sprite not found: \"{mSpritePath}\" → looked up at \"{mResolved}\"");
                    }
                }

                membersProp.GetArrayElementAtIndex(i).objectReferenceValue = memberGo;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        ApplyInitialSkins(go, grp);
    }

    // ---- Queue ----

    private void SpawnQueue(JToken q, Transform parent, float ppu, List<string> warnings)
    {
        string qid = q["queueId"]?.Value<string>();
        string goName = string.IsNullOrEmpty(qid) ? "queue" : $"queue_{qid}";
        GameObject go = new GameObject(goName);
        Undo.RegisterCreatedObjectUndo(go, "Import Queue");
        go.transform.SetParent(parent, false);

        SetPosition(go, q, ppu);

        // Optional collider (queue itself is tappable if authored with one).
        JToken colliderJson = q["collider"];
        if (colliderJson != null && colliderJson.Type != JTokenType.Null)
            AddCollider(go, colliderJson, ppu);

        // B_InteractableQueue
        B_InteractableQueue queue = go.AddComponent<B_InteractableQueue>();
        SerializedObject so = new SerializedObject(queue);
        so.FindProperty("queueId").stringValue = qid ?? "";
        so.FindProperty("sortOrder").intValue = q["sortOrder"]?.Value<int>() ?? 0;
        so.FindProperty("shiftDuration").floatValue = q["shiftDuration"]?.Value<float>() ?? 0.35f;

        // Shift ease (string → enum).
        string easeStr = q["shiftEase"]?.Value<string>();
        if (!string.IsNullOrEmpty(easeStr))
        {
            SerializedProperty easeProp = so.FindProperty("shiftEase");
            int idx = System.Array.IndexOf(System.Enum.GetNames(typeof(DG.Tweening.Ease)), easeStr);
            if (idx >= 0) easeProp.enumValueIndex = idx;
        }

        SetVisualMode(so, q["visualMode"]);

        // Empty-queue chain state id (target object resolved via second pass).
        JToken emptyStateIdTok = q["queueEmptyStateId"];
        if (emptyStateIdTok != null && emptyStateIdTok.Type != JTokenType.Null)
            so.FindProperty("queueEmptyStateId").stringValue = emptyStateIdTok.Value<string>() ?? "";

        // ObjectData (states, actions)
        JToken dataJson = q["data"];
        if (dataJson != null)
        {
            SerializedProperty dataProp = so.FindProperty("data");
            ImportObjectData(dataProp, dataJson, so, warnings);
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        // Members (same shape as groups)
        JArray members = q["members"] as JArray;
        if (members != null)
        {
            so.Update();
            SerializedProperty membersProp = so.FindProperty("members");
            membersProp.arraySize = members.Count;

            for (int i = 0; i < members.Count; i++)
            {
                JToken m = members[i];
                string mSpritePath = m["sprite"]?.Value<string>();
                string mSpineBase = m["spineBasePath"]?.Value<string>();

                GameObject memberGo = new GameObject($"member_{i}");
                Undo.RegisterCreatedObjectUndo(memberGo, "Import Queue Member");
                memberGo.transform.SetParent(go.transform, false);

                float mx = m["position"]?["x"]?.Value<float>() ?? 0f;
                float my = m["position"]?["y"]?.Value<float>() ?? 0f;
                memberGo.transform.position = new Vector3(mx / ppu, my / ppu, 0f);

                if (!string.IsNullOrEmpty(mSpineBase))
                {
                    var dataAsset = ResolveSkeletonDataAsset(mSpineBase, warnings);
                    if (dataAsset != null)
                    {
                        Spine.Unity.SkeletonRenderer
                            .AddSpineComponent<Spine.Unity.SkeletonAnimation>(memberGo, dataAsset);
                        if (memberGo.GetComponent<MeshRenderer>() is MeshRenderer mr)
                            mr.sortingOrder = m["sortOrder"]?.Value<int>() ?? 0;
                    }
                }
                else
                {
                    SpriteRenderer msr = memberGo.AddComponent<SpriteRenderer>();
                    msr.sortingOrder = m["sortOrder"]?.Value<int>() ?? 0;
                    if (!string.IsNullOrEmpty(mSpritePath))
                    {
                        string mResolved = ResolveImportPath(mSpritePath);
                        Sprite sprite = LoadOrFixSprite(mResolved, 100f);
                        if (sprite != null) msr.sprite = sprite;
                        else warnings.Add($"Member sprite not found: \"{mSpritePath}\" → looked up at \"{mResolved}\"");
                    }
                }

                membersProp.GetArrayElementAtIndex(i).objectReferenceValue = memberGo;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Slots: spawn empty anchor transforms at the saved world positions.
        JArray slots = q["slots"] as JArray;
        if (slots != null)
        {
            so.Update();
            SerializedProperty slotsProp = so.FindProperty("slots");
            slotsProp.arraySize = slots.Count;

            for (int i = 0; i < slots.Count; i++)
            {
                float sx = slots[i]?["x"]?.Value<float>() ?? 0f;
                float sy = slots[i]?["y"]?.Value<float>() ?? 0f;
                GameObject slotGo = new GameObject($"slot_{i}");
                Undo.RegisterCreatedObjectUndo(slotGo, "Import Queue Slot");
                slotGo.transform.SetParent(go.transform, false);
                slotGo.transform.position = new Vector3(sx / ppu, sy / ppu, 0f);
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotGo.transform;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Register for AdvanceQueue / empty-chain resolution.
        if (!string.IsNullOrEmpty(qid))
            queueMap[qid] = queue;

        // Empty-chain target — deferred to second pass.
        string emptyTargetId = q["queueEmptyTargetId"]?.Value<string>();
        if (!string.IsNullOrEmpty(emptyTargetId))
        {
            pendingRefs.Add(new PendingRef
            {
                so = so,
                actionPath = "queueEmptyTarget", // top-level SerializedProperty path
                fieldName = "queueEmptyTarget",
                targetId = emptyTargetId,
            });
        }

        ApplyInitialSkins(go, q);
    }

    // ---- Static object ----

    private void SpawnStaticObject(JToken s, Transform parent, float ppu, List<string> warnings)
    {
        string id = s["objectId"]?.Value<string>() ?? "static";
        GameObject go = new GameObject(id);
        Undo.RegisterCreatedObjectUndo(go, "Import Static");
        go.transform.SetParent(parent, false);

        SetTransform(go, s, ppu);

        // Spine mode: attach a SkeletonAnimation child.
        // Sprite mode: add SpriteRenderer with the sprite.
        string spineBase = s["spineBasePath"]?.Value<string>();
        Spine.Unity.SkeletonAnimation skeleton = null;

        if (!string.IsNullOrEmpty(spineBase))
        {
            var dataAsset = ResolveSkeletonDataAsset(spineBase, warnings);
            if (dataAsset != null)
            {
                // Attach spine to the SAME GameObject as the static — no child GO.
                skeleton = Spine.Unity.SkeletonRenderer
                    .AddSpineComponent<Spine.Unity.SkeletonAnimation>(go, dataAsset);

                if (go.GetComponent<MeshRenderer>() is MeshRenderer mr)
                    mr.sortingOrder = s["sortOrder"]?.Value<int>() ?? 0;

                // Preview the authored init animation at edit time. Falls
                // back to the first available animation if none authored.
                string initAnim = s["initSpineAnim"]?.Value<string>();
                bool initLoop = s["initSpineLoop"]?.Value<bool>() ?? true;
                if (!string.IsNullOrEmpty(initAnim))
                {
                    B_InteractableObject.PlaySpineAnim(skeleton, initAnim, initLoop);
                }
                else if (skeleton.Skeleton != null && skeleton.Skeleton.Data.Animations.Count > 0)
                {
                    skeleton.AnimationState.SetAnimation(0,
                        skeleton.Skeleton.Data.Animations.Items[0], true);
                }
            }
        }
        else
        {
            string spritePath = s["sprite"]?.Value<string>();
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = s["sortOrder"]?.Value<int>() ?? 0;
            if (!string.IsNullOrEmpty(spritePath))
            {
                string staticResolved = ResolveImportPath(spritePath);
                Sprite sprite = LoadOrFixSprite(staticResolved, 100f);
                if (sprite != null) sr.sprite = sprite;
                else warnings.Add($"Static sprite not found: \"{spritePath}\" → looked up at \"{staticResolved}\"");
            }
        }

        B_StaticObject staticObj = go.AddComponent<B_StaticObject>();
        SerializedObject so = new SerializedObject(staticObj);
        so.FindProperty("objectId").stringValue = id;
        so.FindProperty("startHidden").boolValue = s["startHidden"]?.Value<bool>() ?? false;
        if (skeleton != null)
            so.FindProperty("skeleton").objectReferenceValue = skeleton;
        SetVisualMode(so, s["visualMode"]);

        SerializedProperty initAnimProp = so.FindProperty("initSpineAnim");
        if (initAnimProp != null)
            initAnimProp.stringValue = s["initSpineAnim"]?.Value<string>() ?? "";
        SerializedProperty initLoopProp = so.FindProperty("initSpineLoop");
        if (initLoopProp != null)
            initLoopProp.boolValue = s["initSpineLoop"]?.Value<bool>() ?? true;

        so.ApplyModifiedPropertiesWithoutUndo();

        // Add collider only if "blocks" is true
        bool blocks = s["blocks"]?.Value<bool>() ?? false;
        if (blocks) AddCollider(go, s["collider"], ppu);

        // Register for cross-object reference resolution.
        if (!string.IsNullOrEmpty(id))
            idMap[id] = go;

        ApplyInitialSkins(go, s);
    }

    // ---- Drop zone (standalone) ----

    private void SpawnDropZone(JToken z, Transform parent, float ppu, List<string> warnings)
    {
        string zoneId = z["zoneId"]?.Value<string>() ?? "zone";
        GameObject go = new GameObject($"zone_{zoneId}");
        Undo.RegisterCreatedObjectUndo(go, "Import DropZone");
        go.transform.SetParent(parent, false);

        SetPosition(go, z, ppu);

        // Add + size the collider BEFORE B_DropZone so [RequireComponent]
        // doesn't auto-create a second default-sized (1,1) BoxCollider2D.
        float w = z["size"]?["x"]?.Value<float>() ?? 100f;
        float h = z["size"]?["y"]?.Value<float>() ?? 100f;
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(w / ppu, h / ppu);
        col.isTrigger = true;

        B_DropZone dz = go.AddComponent<B_DropZone>();
        SerializedObject so = new SerializedObject(dz);
        so.FindProperty("zoneId").stringValue = zoneId;
        so.FindProperty("sortOrder").intValue = z["sortOrder"]?.Value<int>() ?? 0;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- Nested drop zone ----

    private void SpawnNestedDropZone(JToken nz, GameObject interactableGo, float ppu, List<string> warnings)
    {
        string zoneId = nz["zoneId"]?.Value<string>() ?? "zone";

        // Nested drop zones ALWAYS attach to the interactable's own GameObject
        // and inherit the interactable's Collider2D — one transform, one
        // inspector, one hit box. Designers who want a distinct drop area
        // should author a standalone drop zone instead (separate GameObject,
        // listed at scene root) rather than nesting.
        if (interactableGo.GetComponent<B_DropZone>() != null)
        {
            warnings.Add($"'{interactableGo.name}' already has a B_DropZone — nested zone '{zoneId}' skipped.");
            return;
        }

        // Warn (but don't fail) if the exported JSON carried a non-zero offset
        // from the old child-GameObject authoring pattern. The imported zone
        // will sit on the interactable's own collider; move the interactable
        // itself if the position needs tweaking.
        float offX = nz["localOffset"]?["x"]?.Value<float>() ?? 0f;
        float offY = nz["localOffset"]?["y"]?.Value<float>() ?? 0f;
        if (!Mathf.Approximately(offX, 0f) || !Mathf.Approximately(offY, 0f))
        {
            warnings.Add($"Nested zone '{zoneId}' on '{interactableGo.name}' had a non-zero offset in JSON ({offX}, {offY}) — merged onto the interactable's collider. Author as a standalone drop zone if you need a distinct drop area.");
        }

        B_DropZone dz = interactableGo.AddComponent<B_DropZone>();
        SerializedObject so = new SerializedObject(dz);
        so.FindProperty("zoneId").stringValue = zoneId;
        so.FindProperty("sortOrder").intValue = nz["sortOrder"]?.Value<int>() ?? 0;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ============================================================
    //  OBJECT DATA IMPORT (states, actions, requirements)
    // ============================================================

    private void ImportObjectData(SerializedProperty dataProp, JToken json,
                                  SerializedObject parentSo, List<string> warnings)
    {
        // initStateId
        SerializedProperty initStateIdProp = dataProp.FindPropertyRelative("initStateId");
        if (initStateIdProp != null)
            initStateIdProp.stringValue = json["initStateId"]?.Value<string>() ?? "init";

        // initSprite
        string initSpritePath = json["initSprite"]?.Value<string>();
        if (!string.IsNullOrEmpty(initSpritePath))
        {
            string initResolved = ResolveImportPath(initSpritePath);
            Sprite s = LoadOrFixSprite(initResolved, 100f);
            SerializedProperty sp = dataProp.FindPropertyRelative("initSprite");
            if (sp != null) sp.objectReferenceValue = s;
            if (s == null) warnings.Add($"Init sprite not found: \"{initSpritePath}\" → looked up at \"{initResolved}\"");
        }

        // initSpineAnim + initSpineLoop
        SetString(dataProp, "initSpineAnim", json["initSpineAnim"]);
        SerializedProperty initLoopProp = dataProp.FindPropertyRelative("initSpineLoop");
        if (initLoopProp != null)
            initLoopProp.boolValue = json["initSpineLoop"]?.Value<bool>() ?? true;

        // initSFX
        AssignAudioClip(dataProp, "initSFX", json["initSfx"], warnings);

        // States
        JArray statesJson = json["states"] as JArray;
        if (statesJson == null || statesJson.Count == 0) return;

        SerializedProperty statesProp = dataProp.FindPropertyRelative("states");
        statesProp.arraySize = statesJson.Count;

        for (int i = 0; i < statesJson.Count; i++)
        {
            SerializedProperty stateProp = statesProp.GetArrayElementAtIndex(i);
            ImportState(stateProp, statesJson[i], parentSo, i, warnings);
        }
    }

    private void ImportState(SerializedProperty stateProp, JToken json,
                             SerializedObject parentSo, int stateIndex,
                             List<string> warnings)
    {
        // Simple string fields
        SetString(stateProp, "stateId", json["stateId"]);
        SetString(stateProp, "requiredZoneId", json["requiredZoneId"]);
        SetString(stateProp, "successMessageKey", json["successMessageKey"]);
        SetString(stateProp, "failMessageKey", json["failMessageKey"]);
        SetString(stateProp, "hintMessageKey", json["hintMessageKey"]);
        SetString(stateProp, "stateSpineAnim", json["stateSpineAnim"]);

        // stateSpineLoop
        SerializedProperty stateLoopProp = stateProp.FindPropertyRelative("stateSpineLoop");
        if (stateLoopProp != null)
            stateLoopProp.boolValue = json["stateSpineLoop"]?.Value<bool>() ?? false;

        // Trigger enum
        string triggerStr = json["trigger"]?.Value<string>();
        if (!string.IsNullOrEmpty(triggerStr))
        {
            SerializedProperty triggerProp = stateProp.FindPropertyRelative("trigger");
            if (triggerProp != null)
            {
                int idx = System.Array.IndexOf(
                    System.Enum.GetNames(typeof(InteractType)), triggerStr);
                if (idx >= 0) triggerProp.enumValueIndex = idx;
                else warnings.Add($"Unknown trigger type: {triggerStr}");
            }
        }

        // Repeatable
        SerializedProperty repeatProp = stateProp.FindPropertyRelative("repeatable");
        if (repeatProp != null)
            repeatProp.boolValue = json["repeatable"]?.Value<bool>() ?? false;

        // Sprites
        string stateSprPath = json["stateSprite"]?.Value<string>();
        if (!string.IsNullOrEmpty(stateSprPath))
        {
            string stateResolved = ResolveImportPath(stateSprPath);
            Sprite s = LoadOrFixSprite(stateResolved, 100f);
            SerializedProperty sp = stateProp.FindPropertyRelative("stateSprite");
            if (sp != null) sp.objectReferenceValue = s;
            if (s == null) warnings.Add($"State sprite not found: \"{stateSprPath}\" → looked up at \"{stateResolved}\"");
        }

        string dragSprPath = json["dragSprite"]?.Value<string>();
        if (!string.IsNullOrEmpty(dragSprPath))
        {
            string dragResolved = ResolveImportPath(dragSprPath);
            Sprite s = LoadOrFixSprite(dragResolved, 100f);
            SerializedProperty sp = stateProp.FindPropertyRelative("dragSprite");
            if (sp != null) sp.objectReferenceValue = s;
            if (s == null) warnings.Add($"Drag sprite not found: \"{dragSprPath}\" → looked up at \"{dragResolved}\"");
        }

        // stateSFX
        AssignAudioClip(stateProp, "stateSFX", json["stateSfx"], warnings);

        // Requirements
        JArray reqsJson = json["requirements"] as JArray;
        if (reqsJson != null && reqsJson.Count > 0)
        {
            SerializedProperty reqsProp = stateProp.FindPropertyRelative("requirements");
            reqsProp.arraySize = reqsJson.Count;
            for (int r = 0; r < reqsJson.Count; r++)
            {
                SerializedProperty reqProp = reqsProp.GetArrayElementAtIndex(r);
                SetString(reqProp, "objectId", reqsJson[r]["objectId"]);
                SetString(reqProp, "stateId", reqsJson[r]["stateId"]);

                SerializedProperty invProp = reqProp.FindPropertyRelative("requireNotDone");
                if (invProp != null)
                    invProp.boolValue = reqsJson[r]["requireNotDone"]?.Value<bool>() ?? false;
            }
        }

        // Actions
        JArray actionsJson = json["actions"] as JArray;
        if (actionsJson != null && actionsJson.Count > 0)
        {
            SerializedProperty actionsProp = stateProp.FindPropertyRelative("actions");
            actionsProp.arraySize = actionsJson.Count;
            for (int a = 0; a < actionsJson.Count; a++)
            {
                SerializedProperty actionProp = actionsProp.GetArrayElementAtIndex(a);
                string actionPath = actionProp.propertyPath;
                ImportAction(actionProp, actionsJson[a], parentSo, actionPath, warnings);
            }
        }
    }

    private void ImportAction(SerializedProperty actionProp, JToken json,
                              SerializedObject parentSo, string actionPath,
                              List<string> warnings)
    {
        // Type enum
        string typeStr = json["type"]?.Value<string>();
        StateActionType actionType = StateActionType.Wait;
        if (!string.IsNullOrEmpty(typeStr))
        {
            SerializedProperty typeProp = actionProp.FindPropertyRelative("type");
            int idx = System.Array.IndexOf(
                System.Enum.GetNames(typeof(StateActionType)), typeStr);
            if (idx >= 0)
            {
                typeProp.enumValueIndex = idx;
                actionType = (StateActionType)idx;
            }
            else warnings.Add($"Unknown action type: {typeStr}");
        }

        // Common fields
        SerializedProperty parallelProp = actionProp.FindPropertyRelative("runInParallel");
        if (parallelProp != null)
            parallelProp.boolValue = json["runInParallel"]?.Value<bool>() ?? false;

        SerializedProperty durationProp = actionProp.FindPropertyRelative("duration");
        if (durationProp != null)
            durationProp.floatValue = json["duration"]?.Value<float>() ?? 0.4f;

        // actionTarget — deferred to second pass
        string actionTargetId = json["actionTargetId"]?.Value<string>();
        if (!string.IsNullOrEmpty(actionTargetId))
        {
            pendingRefs.Add(new PendingRef
            {
                so = parentSo,
                actionPath = actionPath,
                fieldName = "actionTarget",
                targetId = actionTargetId,
            });
        }

        // Type-specific fields
        switch (actionType)
        {
            case StateActionType.Wait:
                // duration only — already set
                break;

            case StateActionType.MoveTo:
            {
                // Ease
                string easeStr = json["ease"]?.Value<string>();
                if (!string.IsNullOrEmpty(easeStr))
                {
                    SerializedProperty easeProp = actionProp.FindPropertyRelative("ease");
                    if (easeProp != null)
                    {
                        if (System.Enum.TryParse<DG.Tweening.Ease>(easeStr, out var easeVal))
                            easeProp.enumValueIndex = (int)easeVal;
                    }
                }

                // moveTarget — deferred to second pass
                string moveObjId = json["moveTargetObjectId"]?.Value<string>();
                string moveZoneId = json["moveTargetZoneId"]?.Value<string>();
                if (!string.IsNullOrEmpty(moveObjId) || !string.IsNullOrEmpty(moveZoneId))
                {
                    pendingRefs.Add(new PendingRef
                    {
                        so = parentSo,
                        actionPath = actionPath,
                        fieldName = "moveTarget",
                        targetId = moveObjId ?? "",
                        zoneId = moveZoneId,
                    });
                }
                else
                {
                    // No reference id → MoveTo carried an absolute world
                    // position. Spawn a tiny anchor Transform at that
                    // position and wire it as moveTarget so the action
                    // round-trips correctly.
                    JToken posTok = json["moveTargetPosition"];
                    if (posTok != null && posTok.Type != JTokenType.Null)
                    {
                        float px = posTok["x"]?.Value<float>() ?? 0f;
                        float py = posTok["y"]?.Value<float>() ?? 0f;
                        float ppu = currentPpu > 0f ? currentPpu : 100f;
                        Vector3 worldPos = new Vector3(px / ppu, py / ppu, 0f);

                        GameObject anchor = new GameObject("_moveAnchor");
                        Undo.RegisterCreatedObjectUndo(anchor, "Import MoveTo Anchor");

                        // Parent under the owning B_InteractableObject /
                        // Group / Queue's transform.parent (i.e. level root)
                        // so the anchor lives next to other imported
                        // objects, not as a global stray. Set world position
                        // explicitly so the anchor lands at the saved spot
                        // regardless of parent transform.
                        Component owner = parentSo.targetObject as Component;
                        Transform ownerParent = owner != null && owner.transform.parent != null
                            ? owner.transform.parent
                            : null;
                        anchor.transform.SetParent(ownerParent, false);
                        anchor.transform.position = worldPos;

                        SerializedProperty p = actionProp.FindPropertyRelative("moveTarget");
                        if (p != null)
                        {
                            p.objectReferenceValue = anchor.transform;
                            // Apply now so subsequent code in the same
                            // pass sees the wired reference.
                            parentSo.ApplyModifiedPropertiesWithoutUndo();
                            parentSo.Update();
                        }
                    }
                }
                break;
            }

            case StateActionType.Disappear:
            {
                SerializedProperty fo = actionProp.FindPropertyRelative("fadeOut");
                if (fo != null) fo.boolValue = json["fadeOut"]?.Value<bool>() ?? true;
                SerializedProperty dd = actionProp.FindPropertyRelative("destroyOnDisappear");
                if (dd != null) dd.boolValue = json["destroyOnDisappear"]?.Value<bool>() ?? true;
                break;
            }

            case StateActionType.Appear:
            {
                SerializedProperty fi = actionProp.FindPropertyRelative("fadeIn");
                if (fi != null) fi.boolValue = json["fadeIn"]?.Value<bool>() ?? true;
                break;
            }

            case StateActionType.DoAnimation:
            {
                SetString(actionProp, "spineAnim", json["spineAnim"]);
                SerializedProperty slProp = actionProp.FindPropertyRelative("spineLoop");
                if (slProp != null)
                    slProp.boolValue = json["spineLoop"]?.Value<bool>() ?? false;
                break;
            }

            case StateActionType.ActivateState:
            {
                SetString(actionProp, "activateStateId", json["activateStateId"]);

                // activateTarget — deferred to second pass
                string actTargetId = json["activateTargetObjectId"]?.Value<string>();
                if (!string.IsNullOrEmpty(actTargetId))
                {
                    pendingRefs.Add(new PendingRef
                    {
                        so = parentSo,
                        actionPath = actionPath,
                        fieldName = "activateTarget",
                        targetId = actTargetId,
                    });
                }

                // chainGuards — same shape as state requirements.
                JArray guardsJson = json["chainGuards"] as JArray;
                if (guardsJson != null && guardsJson.Count > 0)
                {
                    SerializedProperty guardsProp = actionProp.FindPropertyRelative("chainGuards");
                    guardsProp.arraySize = guardsJson.Count;
                    for (int g = 0; g < guardsJson.Count; g++)
                    {
                        SerializedProperty gProp = guardsProp.GetArrayElementAtIndex(g);
                        SetString(gProp, "objectId", guardsJson[g]["objectId"]);
                        SetString(gProp, "stateId", guardsJson[g]["stateId"]);
                        SerializedProperty inv = gProp.FindPropertyRelative("requireNotDone");
                        if (inv != null)
                            inv.boolValue = guardsJson[g]["requireNotDone"]?.Value<bool>() ?? false;
                    }
                }
                break;
            }

            case StateActionType.AdvanceQueue:
            {
                SetString(actionProp, "queueServeStateId", json["queueServeStateId"]);

                // queueTarget — deferred to second pass (queueMap lookup)
                string qTargetId = json["queueTargetId"]?.Value<string>();
                if (!string.IsNullOrEmpty(qTargetId))
                {
                    pendingRefs.Add(new PendingRef
                    {
                        so = parentSo,
                        actionPath = actionPath,
                        fieldName = "queueTarget",
                        targetId = qTargetId,
                    });
                }
                break;
            }

            case StateActionType.SkinChange:
            {
                SetString(actionProp, "skinName", json["skinName"]);

                // skinOp (string → enum)
                string opStr = json["skinOp"]?.Value<string>();
                if (!string.IsNullOrEmpty(opStr))
                {
                    SerializedProperty opProp = actionProp.FindPropertyRelative("skinOp");
                    int idx = System.Array.IndexOf(System.Enum.GetNames(typeof(SkinOp)), opStr);
                    if (idx >= 0) opProp.enumValueIndex = idx;
                }

                // skinTarget — deferred (idMap lookup, then GetComponent on Pass 2)
                string skinTargetId = json["skinTargetObjectId"]?.Value<string>();
                if (!string.IsNullOrEmpty(skinTargetId))
                {
                    pendingRefs.Add(new PendingRef
                    {
                        so = parentSo,
                        actionPath = actionPath,
                        fieldName = "skinTarget",
                        targetId = skinTargetId,
                    });
                }
                break;
            }

            case StateActionType.PlaySFX:
            {
                AssignAudioClip(actionProp, "sfxClip", json["sfxClip"], warnings);
                break;
            }
        }
    }

    // ============================================================
    //  PASS 2: RESOLVE CROSS-OBJECT REFERENCES
    // ============================================================

    private void ResolvePendingRefs(List<string> warnings)
    {
        if (pendingRefs == null || pendingRefs.Count == 0) return;

        foreach (PendingRef pref in pendingRefs)
        {
            pref.so.Update();

            // Top-level queueEmptyTarget lives directly on the SerializedObject,
            // not inside an action element. Handle it before the actionProp lookup.
            if (pref.fieldName == "queueEmptyTarget")
            {
                GameObject target = FindInIdMap(pref.targetId, warnings);
                if (target != null)
                {
                    B_InteractableObject interactable = target.GetComponent<B_InteractableObject>();
                    SerializedProperty p = pref.so.FindProperty("queueEmptyTarget");
                    if (p != null && interactable != null)
                        p.objectReferenceValue = interactable;
                    else if (interactable == null)
                        warnings.Add($"Queue empty-chain target '{pref.targetId}' has no B_InteractableObject component.");
                }
                pref.so.ApplyModifiedPropertiesWithoutUndo();
                continue;
            }

            SerializedProperty actionProp = pref.so.FindProperty(pref.actionPath);
            if (actionProp == null)
            {
                warnings.Add($"Could not find action at path '{pref.actionPath}' for reference resolution.");
                continue;
            }

            switch (pref.fieldName)
            {
                case "actionTarget":
                {
                    GameObject target = FindInIdMap(pref.targetId, warnings);
                    if (target != null)
                    {
                        SerializedProperty p = actionProp.FindPropertyRelative("actionTarget");
                        if (p != null) p.objectReferenceValue = target;
                    }
                    break;
                }

                case "moveTarget":
                {
                    Transform resolved = null;

                    // Try to find by zone first (zone is a child of the object).
                    if (!string.IsNullOrEmpty(pref.zoneId))
                    {
                        // Search all drop zones for matching zoneId.
                        B_DropZone[] allZones = Object.FindObjectsByType<B_DropZone>(
                            FindObjectsInactive.Include, FindObjectsSortMode.None);
                        foreach (B_DropZone z in allZones)
                        {
                            if (z.ZoneId == pref.zoneId)
                            {
                                resolved = z.transform;
                                break;
                            }
                        }
                    }

                    // Fall back to the object itself.
                    if (resolved == null && !string.IsNullOrEmpty(pref.targetId))
                    {
                        GameObject target = FindInIdMap(pref.targetId, warnings);
                        if (target != null) resolved = target.transform;
                    }

                    if (resolved != null)
                    {
                        SerializedProperty p = actionProp.FindPropertyRelative("moveTarget");
                        if (p != null) p.objectReferenceValue = resolved;
                    }
                    break;
                }

                case "activateTarget":
                {
                    GameObject target = FindInIdMap(pref.targetId, warnings);
                    if (target != null)
                    {
                        B_InteractableObject interactable = target.GetComponent<B_InteractableObject>();
                        SerializedProperty p = actionProp.FindPropertyRelative("activateTarget");
                        if (p != null && interactable != null)
                            p.objectReferenceValue = interactable;
                        else if (interactable == null)
                            warnings.Add($"ActivateState target '{pref.targetId}' has no B_InteractableObject component.");
                    }
                    break;
                }

                case "queueTarget":
                {
                    if (queueMap != null && queueMap.TryGetValue(pref.targetId, out var q))
                    {
                        SerializedProperty p = actionProp.FindPropertyRelative("queueTarget");
                        if (p != null) p.objectReferenceValue = q;
                    }
                    else
                    {
                        warnings.Add($"AdvanceQueue target '{pref.targetId}' — no queue with that queueId was imported.");
                    }
                    break;
                }

                case "skinTarget":
                {
                    GameObject target = FindInIdMap(pref.targetId, warnings);
                    if (target != null)
                    {
                        B_InteractableObject interactable = target.GetComponent<B_InteractableObject>();
                        SerializedProperty p = actionProp.FindPropertyRelative("skinTarget");
                        if (p != null && interactable != null)
                            p.objectReferenceValue = interactable;
                        else if (interactable == null)
                            warnings.Add($"SkinChange target '{pref.targetId}' has no B_InteractableObject component.");
                    }
                    break;
                }
            }

            pref.so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private GameObject FindInIdMap(string id, List<string> warnings)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (idMap.TryGetValue(id, out GameObject go)) return go;
        warnings.Add($"Could not resolve object reference: '{id}' not found in imported objects.");
        return null;
    }

    // ============================================================
    //  HELPERS
    // ============================================================

    private void SetTransform(GameObject go, JToken json, float ppu)
    {
        SetPosition(go, json, ppu);
        go.transform.localScale = Vector3.one * (json["scale"]?.Value<float>() ?? 1f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, json["rotation"]?.Value<float>() ?? 0f);
    }

    private void SetPosition(GameObject go, JToken json, float ppu)
    {
        float x = json["position"]?["x"]?.Value<float>() ?? 0f;
        float y = json["position"]?["y"]?.Value<float>() ?? 0f;
        go.transform.localPosition = new Vector3(x / ppu, y / ppu, 0f);
    }

    private void AddCollider(GameObject go, JToken colliderJson, float ppu)
    {
        if (colliderJson == null) return;

        string type = colliderJson["type"]?.Value<string>() ?? "box";
        // Collider offset (local, i.e. how far the collider center sits
        // from the GameObject's transform position). Exporter writes this
        // in pixel units relative to the exported visual position, so
        // dividing by ppu gives local Unity units.
        float offsetX = (colliderJson["offsetX"]?.Value<float>() ?? 0f) / ppu;
        float offsetY = (colliderJson["offsetY"]?.Value<float>() ?? 0f) / ppu;

        if (type == "circle")
        {
            CircleCollider2D cc = go.AddComponent<CircleCollider2D>();
            cc.radius = (colliderJson["radius"]?.Value<float>() ?? 50f) / ppu;
            cc.offset = new Vector2(offsetX, offsetY);
        }
        else
        {
            BoxCollider2D bc = go.AddComponent<BoxCollider2D>();
            float w = colliderJson["width"]?.Value<float>() ?? 100f;
            float h = colliderJson["height"]?.Value<float>() ?? 100f;
            bc.size = new Vector2(w / ppu, h / ppu);
            bc.offset = new Vector2(offsetX, offsetY);
        }
    }

    private static void SetString(SerializedProperty parent, string fieldName, JToken value)
    {
        if (value == null || value.Type == JTokenType.Null) return;
        SerializedProperty prop = parent.FindPropertyRelative(fieldName);
        if (prop != null) prop.stringValue = value.Value<string>() ?? "";
    }

    /// <summary>
    /// Reads a "visualMode" JSON value ("Sprite" or "Spine") and writes
    /// it to the SerializedObject's visualMode enum field. Falls back to
    /// Sprite for legacy JSONs without the field.
    /// </summary>
    /// <summary>
    /// If the imported JSON has a non-empty <c>initialSkins</c> array on a
    /// Spine-mode container, attach a <see cref="B_SpineSkinSet"/> to the
    /// spawned GameObject and populate its list. Awake on the component
    /// will combine and apply at runtime.
    /// </summary>
    private static void ApplyInitialSkins(GameObject go, JToken json)
    {
        if (go == null) return;
        JArray arr = json?["initialSkins"] as JArray;
        if (arr == null || arr.Count == 0) return;

        // Spine setup must already exist for this to matter; if there's no
        // SkeletonAnimation we silently skip (the [RequireComponent] on
        // B_SpineSkinSet would otherwise auto-add a default and break).
        if (go.GetComponentInChildren<Spine.Unity.SkeletonAnimation>() == null) return;

        // Attach to the first GameObject in the hierarchy that owns a
        // SkeletonAnimation, so the [RequireComponent] is satisfied.
        var skel = go.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
        GameObject host = skel.gameObject;

        B_SpineSkinSet set = host.GetComponent<B_SpineSkinSet>();
        if (set == null) set = host.AddComponent<B_SpineSkinSet>();

        SerializedObject sso = new SerializedObject(set);
        SerializedProperty listProp = sso.FindProperty("initialSkins");
        listProp.arraySize = arr.Count;
        for (int i = 0; i < arr.Count; i++)
        {
            string name = arr[i]?.Value<string>() ?? "";
            listProp.GetArrayElementAtIndex(i).stringValue = name;
        }
        sso.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetVisualMode(SerializedObject so, JToken value)
    {
        SerializedProperty prop = so.FindProperty("visualMode");
        if (prop == null) return;
        string s = value?.Value<string>();
        int idx = string.IsNullOrEmpty(s)
            ? (int)VisualMode.Sprite
            : System.Array.IndexOf(System.Enum.GetNames(typeof(VisualMode)), s);
        if (idx < 0) idx = (int)VisualMode.Sprite;
        prop.enumValueIndex = idx;
    }

    private string ResolveImportPath(string exportedPath)
    {
        if (string.IsNullOrEmpty(exportedPath)) return null;
        string path = exportedPath.Replace("\\", "/");

        // Strip the runtime-side prefix (e.g. "assets/levels/") written
        // by the exporter's "Output Asset Prefix".
        if (!string.IsNullOrEmpty(stripPrefix) && path.StartsWith(stripPrefix))
            path = path.Substring(stripPrefix.Length);

        // Prepend the Unity-side prefix (e.g. "Assets/LevelAssets/")
        // to produce a valid AssetDatabase path.
        if (!string.IsNullOrEmpty(assetRoot) && !path.StartsWith(assetRoot))
            path = assetRoot + path;
        return path;
    }

    /// <summary>
    /// Reverses the exporter's ResolveSpineBasePath. Given an exported
    /// base path like "assets/levels/lv1/anim/Bg_Ngheo", finds the Unity
    /// SkeletonDataAsset in the same folder (conventionally
    /// "Bg_Ngheo_SkeletonData.asset"). Returns null if not found.
    /// </summary>
    private Spine.Unity.SkeletonDataAsset ResolveSkeletonDataAsset(string exportedBasePath, List<string> warnings)
    {
        if (string.IsNullOrEmpty(exportedBasePath)) return null;

        string unityBase = ResolveImportPath(exportedBasePath);
        if (string.IsNullOrEmpty(unityBase)) return null;

        // Convention: <base>_SkeletonData.asset in same folder.
        string dataAssetPath = unityBase + "_SkeletonData.asset";
        var dataAsset = AssetDatabase.LoadAssetAtPath<Spine.Unity.SkeletonDataAsset>(dataAssetPath);
        if (dataAsset != null) return dataAsset;

        // Fallback: scan the folder for any SkeletonDataAsset whose
        // skeletonJSON's base name matches.
        string folder = Path.GetDirectoryName(unityBase)?.Replace("\\", "/");
        string baseName = Path.GetFileName(unityBase);
        if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(baseName))
        {
            string[] guids = AssetDatabase.FindAssets("t:SkeletonDataAsset", new[] { folder });
            foreach (string g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                var candidate = AssetDatabase.LoadAssetAtPath<Spine.Unity.SkeletonDataAsset>(p);
                if (candidate == null || candidate.skeletonJSON == null) continue;

                string skPath = AssetDatabase.GetAssetPath(candidate.skeletonJSON);
                if (string.IsNullOrEmpty(skPath)) continue;

                string skBase = skPath;
                if (skBase.EndsWith(".skel.bytes")) skBase = skBase.Substring(0, skBase.Length - ".skel.bytes".Length);
                else if (skBase.EndsWith(".json")) skBase = skBase.Substring(0, skBase.Length - ".json".Length);
                else
                {
                    int dot = skBase.LastIndexOf('.');
                    if (dot >= 0) skBase = skBase.Substring(0, dot);
                }

                if (skBase == unityBase) return candidate;
            }
        }

        warnings.Add($"SkeletonDataAsset not found for base path: \"{exportedBasePath}\" → looked near \"{dataAssetPath}\"");
        return null;
    }

    /// <summary>
    /// Normalizes a filesystem path to be relative to the Unity project root.
    /// Handles mixed slashes (Windows backslash vs forward slash) and strips
    /// the absolute project prefix if present. AssetDatabase.LoadAssetAtPath
    /// requires paths in this format (e.g. "Assets/Foo/bar.png").
    /// </summary>
    private static string ToProjectRelativePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        path = path.Replace("\\", "/");

        string projectRoot = Path.GetFullPath(Application.dataPath + "/..")
            .Replace("\\", "/").TrimEnd('/');

        if (path.StartsWith(projectRoot + "/"))
            path = path.Substring(projectRoot.Length + 1);

        return path;
    }

    /// <summary>
    /// Resolves an exported audio path → loads the AudioClip asset →
    /// assigns to the named field on the parent SerializedProperty. Used
    /// for ObjectData.initSFX, ObjectState.stateSFX, and StateAction.sfxClip.
    /// </summary>
    private void AssignAudioClip(SerializedProperty parent, string fieldName,
                                  JToken pathToken, List<string> warnings)
    {
        if (parent == null || pathToken == null || pathToken.Type == JTokenType.Null)
            return;

        string path = pathToken.Value<string>();
        if (string.IsNullOrEmpty(path)) return;

        string resolved = ResolveImportPath(path);
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(resolved);

        SerializedProperty p = parent.FindPropertyRelative(fieldName);
        if (p != null) p.objectReferenceValue = clip;

        if (clip == null)
            warnings.Add($"AudioClip not found: \"{path}\" → looked up at \"{resolved}\"");
    }

    private static Sprite LoadOrFixSprite(string assetPath, float ppu)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null) return sprite;

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = ppu;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private Transform FindOrCreateParent(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null) return existing.transform;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Level Root");
        return go.transform;
    }

    /// <summary>
    /// Destroys the existing level parent GameObject and any stray level
    /// components (B_LevelConfig, interactables, groups, statics, drop zones)
    /// so a fresh import doesn't inherit fields from the previous level.
    /// </summary>
    private void ClearExistingLevel()
    {
        // Remove the parent GameObject (_Level) and everything under it.
        GameObject parent = GameObject.Find(parentName);
        if (parent != null)
            Undo.DestroyObjectImmediate(parent);

        // Also remove any stray level objects that weren't under the parent
        // (e.g. a _LevelConfig the designer dragged out of the parent).
        foreach (var cfg in Object.FindObjectsByType<B_LevelConfig>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(cfg.gameObject);

        foreach (var obj in Object.FindObjectsByType<B_InteractableObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(obj.gameObject);

        foreach (var grp in Object.FindObjectsByType<B_InteractableGroup>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(grp.gameObject);

        // Queues — added to cleanup so re-importing doesn't leave stale
        // queues alongside freshly-spawned ones.
        foreach (var q in Object.FindObjectsByType<B_InteractableQueue>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(q.gameObject);

        foreach (var st in Object.FindObjectsByType<B_StaticObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(st.gameObject);

        foreach (var zone in Object.FindObjectsByType<B_DropZone>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(zone.gameObject);
    }

    private void ShowMessage(string msg, MessageType type)
    {
        lastMessage = msg;
        lastMessageType = type;
        Repaint();
    }
}
