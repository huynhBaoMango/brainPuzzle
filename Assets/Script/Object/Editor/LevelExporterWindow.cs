using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window that walks the currently-open scene and writes a
/// JSON level file matching the LibGDX runtime schema. Open via
/// Tools &gt; Puzzle &gt; Level Exporter.
/// </summary>
public class LevelExporterWindow : EditorWindow
{
    private const string PrefKeyAssetRoot = "LevelExporter.assetRoot";
    private const string PrefKeyAssetPrefix = "LevelExporter.assetPrefix";
    private const string PrefKeyOutputDir = "LevelExporter.outputDir";
    private const string PrefKeyPretty = "LevelExporter.prettyPrint";
    private const string PrefKeyCapturePreview  = "LevelExporter.capturePreview";
    private const string PrefKeyPreviewWidth    = "LevelExporter.previewWidth";
    private const string PrefKeyPreviewHeight   = "LevelExporter.previewHeight";
    private const string PrefKeyPreviewFilename = "LevelExporter.previewFilename";
    private const string PrefKeyPreviewTransparent = "LevelExporter.previewTransparent";
    private const string PrefKeyPreviewMatchAspect = "LevelExporter.previewMatchAspect";
    private const string PrefKeyPreviewFitObject   = "LevelExporter.previewFitObject";

    private string assetRoot = "Assets/LevelAssets/";
    private string outputAssetPrefix = "assets/levels/";
    private string outputDir = "Assets/LevelAssets";
    private bool prettyPrint = true;

    // Preview capture (one-step "Export + Preview" workflow)
    private bool capturePreview = true;
    private int previewWidth = 512;
    private int previewHeight = 768;
    private string previewFilename = "preview.png";
    private bool previewTransparent = false;
    private bool previewMatchAspect = true;
    private string previewFitObjectId = "bg";

    /// <summary>Full path to the last exported level.json (for Reveal button).</summary>
    private string lastExportedPath;

    private Vector2 scroll;
    private string lastMessage;
    private MessageType lastMessageType = MessageType.None;

    // Coordinate conversion params, recomputed at the start of every export.
    private float pxPerUnit;
    private Vector2 originWorld;

    [MenuItem("Tools/Puzzle/Level Exporter")]
    public static void Open()
    {
        var w = GetWindow<LevelExporterWindow>("Level Exporter");
        w.minSize = new Vector2(420f, 260f);
        w.Show();
    }

    private void OnEnable()
    {
        assetRoot = EditorPrefs.GetString(PrefKeyAssetRoot, assetRoot);
        outputAssetPrefix = EditorPrefs.GetString(PrefKeyAssetPrefix, outputAssetPrefix);
        outputDir = EditorPrefs.GetString(PrefKeyOutputDir, outputDir);
        prettyPrint = EditorPrefs.GetBool(PrefKeyPretty, prettyPrint);
        capturePreview     = EditorPrefs.GetBool(PrefKeyCapturePreview, capturePreview);
        previewWidth       = EditorPrefs.GetInt(PrefKeyPreviewWidth, previewWidth);
        previewHeight      = EditorPrefs.GetInt(PrefKeyPreviewHeight, previewHeight);
        previewFilename    = EditorPrefs.GetString(PrefKeyPreviewFilename, previewFilename);
        previewTransparent = EditorPrefs.GetBool(PrefKeyPreviewTransparent, previewTransparent);
        previewMatchAspect = EditorPrefs.GetBool(PrefKeyPreviewMatchAspect, previewMatchAspect);
        previewFitObjectId = EditorPrefs.GetString(PrefKeyPreviewFitObject, previewFitObjectId);
    }

    private void OnDisable()
    {
        EditorPrefs.SetString(PrefKeyAssetRoot, assetRoot);
        EditorPrefs.SetString(PrefKeyAssetPrefix, outputAssetPrefix);
        EditorPrefs.SetString(PrefKeyOutputDir, outputDir);
        EditorPrefs.SetBool(PrefKeyPretty, prettyPrint);
        EditorPrefs.SetBool(PrefKeyCapturePreview, capturePreview);
        EditorPrefs.SetInt(PrefKeyPreviewWidth, previewWidth);
        EditorPrefs.SetInt(PrefKeyPreviewHeight, previewHeight);
        EditorPrefs.SetString(PrefKeyPreviewFilename, previewFilename);
        EditorPrefs.SetBool(PrefKeyPreviewTransparent, previewTransparent);
        EditorPrefs.SetBool(PrefKeyPreviewMatchAspect, previewMatchAspect);
        EditorPrefs.SetString(PrefKeyPreviewFitObject, previewFitObjectId);
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Level Exporter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Walks the current scene for B_LevelConfig, B_InteractableObject, and B_DropZone " +
            "components and writes them as JSON matching the LibGDX runtime schema.",
            MessageType.Info);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Paths", EditorStyles.boldLabel);
        assetRoot = EditorGUILayout.TextField(
            new GUIContent("Asset Root Prefix (strip)",
                "Prefix stripped from every sprite/sfx Unity path before export. e.g. \"Assets/LevelAssets/\" turns \"Assets/LevelAssets/lv1/images/foo.png\" into \"lv1/images/foo.png\"."),
            assetRoot);
        outputAssetPrefix = EditorGUILayout.TextField(
            new GUIContent("Output Asset Prefix",
                "Prepended to every exported sprite/sfx path after stripping. e.g. \"assets/levels/\" turns \"lv1/images/foo.png\" into \"assets/levels/lv1/images/foo.png\" to match the LibGDX runtime convention."),
            outputAssetPrefix);
        using (new EditorGUILayout.HorizontalScope())
        {
            outputDir = EditorGUILayout.TextField(
                new GUIContent("Output Directory",
                    "Base directory. Export creates lv{N}/ inside this with level.json, strings.json, images/, anim/."),
                outputDir);
            if (GUILayout.Button("…", GUILayout.Width(24f)))
            {
                string picked = EditorUtility.OpenFolderPanel(
                    "Choose export directory", outputDir, "");
                if (!string.IsNullOrEmpty(picked))
                {
                    // Make relative to project if possible.
                    string projectRoot = Path.GetFullPath(Application.dataPath + "/..");
                    if (picked.StartsWith(projectRoot))
                        picked = picked.Substring(projectRoot.Length + 1);
                    outputDir = picked.Replace("\\", "/");
                }
            }
        }

        // Show preview of where files will go.
        B_LevelConfig previewConfig = Object.FindAnyObjectByType<B_LevelConfig>();
        if (previewConfig != null && !string.IsNullOrEmpty(previewConfig.levelId))
        {
            string preview = $"{outputDir}/{previewConfig.levelId}/";
            EditorGUILayout.HelpBox(
                $"Will export to:\n  {preview}level.json\n  {preview}strings.json\n  {preview}images/\n  {preview}anim/",
                MessageType.None);
        }

        prettyPrint = EditorGUILayout.Toggle(
            new GUIContent("Pretty Print", "Human-readable JSON formatting."),
            prettyPrint);

        EditorGUILayout.Space(4f);

        // ---- Preview capture ----
        capturePreview = EditorGUILayout.ToggleLeft(
            new GUIContent("Capture preview PNG after export",
                "Render the level camera to a PNG next to level.json so " +
                "the menu's thumbnail comes out of the same Export click."),
            capturePreview);

        if (capturePreview)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                previewFitObjectId = EditorGUILayout.TextField(
                    new GUIContent("Fit To Object Id",
                        "objectId of a static (or interactable) whose visual bounds the capture should frame. " +
                        "Camera is temporarily moved + sized so the PNG fills exactly that object — no blank " +
                        "space top/bottom or left/right. Common choice: the background sprite (e.g. \"bg\"). " +
                        "Leave empty to fall back to the level camera's natural view."),
                    previewFitObjectId);

                previewMatchAspect = EditorGUILayout.Toggle(
                    new GUIContent("Match Level Aspect",
                        "Only used when Fit To Object Id is empty. Auto-fit the PNG's aspect ratio to " +
                        "B_LevelConfig.virtualWidth / virtualHeight so the camera fills the texture."),
                    previewMatchAspect);

                using (new EditorGUILayout.HorizontalScope())
                {
                    previewWidth  = EditorGUILayout.IntField("Width",  previewWidth);
                    // Height is auto-computed when fitting to an object or
                    // matching level aspect — leave the field disabled then.
                    bool autoH = !string.IsNullOrEmpty(previewFitObjectId) || previewMatchAspect;
                    using (new EditorGUI.DisabledScope(autoH))
                    {
                        previewHeight = EditorGUILayout.IntField("Height", previewHeight);
                    }
                }
                previewFilename = EditorGUILayout.TextField("Filename", previewFilename);
                previewTransparent = EditorGUILayout.Toggle(
                    new GUIContent("Transparent BG",
                        "Render with alpha so the level art floats on the menu's background."),
                    previewTransparent);
            }
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = true;
            if (GUILayout.Button("Export Current Scene", GUILayout.Height(32f)))
                ExportCurrentScene();
            if (GUILayout.Button("Reveal Last Export", GUILayout.Height(32f), GUILayout.Width(150f)))
                RevealOutput();
        }

        if (!string.IsNullOrEmpty(lastMessage))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(lastMessage, lastMessageType);
        }

        EditorGUILayout.EndScrollView();
    }

    private void RevealOutput()
    {
        if (!string.IsNullOrEmpty(lastExportedPath) && File.Exists(lastExportedPath))
            EditorUtility.RevealInFinder(lastExportedPath);
        else
            ShowMessage("No recent export to reveal.", MessageType.Warning);
    }

    private void ShowMessage(string msg, MessageType type)
    {
        lastMessage = msg;
        lastMessageType = type;
        Repaint();
    }

    // ============================================================
    //  LEVEL STRINGS EXPORT
    // ============================================================

    /// <summary>
    /// Writes a strings.json file into the given directory.
    /// Format matches the LibGDX B_LangManager: [{ "key":"...", "en":"...", "vn":"..." }, ...]
    /// </summary>
    private void ExportStringsJson(B_LevelConfig config, string dir)
    {
        if (config.strings == null || config.strings.Count == 0) return;
        if (string.IsNullOrEmpty(dir)) return;

        string stringsPath = Path.Combine(dir, "strings.json");

        var entries = new List<Dictionary<string, string>>();
        foreach (LevelString ls in config.strings)
        {
            if (string.IsNullOrEmpty(ls.key)) continue;
            var entry = new Dictionary<string, string>
            {
                { "key", ls.key },
                { "en", ls.en ?? "" },
                { "vn", ls.vn ?? "" },
            };
            entries.Add(entry);
        }

        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            Formatting = prettyPrint ? Formatting.Indented : Formatting.None,
        };
        string json = JsonConvert.SerializeObject(entries, settings);
        File.WriteAllText(stringsPath, json);
    }

    // ============================================================
    //  EXPORT PIPELINE
    // ============================================================

    private void ExportCurrentScene()
    {
        try
        {
            B_LevelConfig config = Object.FindAnyObjectByType<B_LevelConfig>();
            if (config == null)
            {
                ShowMessage(
                    "No B_LevelConfig found in the open scene. " +
                    "Open the Level Config window (Tools > Puzzle > Level Config) to create one.",
                    MessageType.Error);
                return;
            }

            List<string> errors = new List<string>();
            LevelJson level = BuildLevel(config, errors);

            if (errors.Count > 0)
            {
                ShowMessage(
                    "Export aborted due to validation errors:\n  • " + string.Join("\n  • ", errors),
                    MessageType.Error);
                return;
            }

            // ---- Build folder structure: outputDir/{levelId}/ ----
            if (string.IsNullOrEmpty(config.levelId))
            {
                ShowMessage("B_LevelConfig.levelId is empty. Set it in Level Config window.", MessageType.Error);
                return;
            }
            string levelDir = Path.Combine(outputDir, config.levelId).Replace("\\", "/");
            string imagesDir = Path.Combine(levelDir, "images").Replace("\\", "/");
            string animDir = Path.Combine(levelDir, "anim").Replace("\\", "/");

            if (!Directory.Exists(levelDir)) Directory.CreateDirectory(levelDir);
            if (!Directory.Exists(imagesDir)) Directory.CreateDirectory(imagesDir);
            if (!Directory.Exists(animDir)) Directory.CreateDirectory(animDir);

            // ---- Write level.json ----
            string levelJsonPath = Path.Combine(levelDir, "level.json");

            // Drop null / default-valued fields. Most of the file is
            // StateActionJson rows that only use 2-4 fields out of 20+,
            // so the rest serialize as null. Ignoring them shrinks the
            // typical level JSON by ~58%. Receivers (Newtonsoft on the
            // import side, LibGDX-Json/Jackson/Gson on the runtime side)
            // all treat missing fields as null/0/false by default.
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                Formatting = prettyPrint ? Formatting.Indented : Formatting.None,
            };
            string json = JsonConvert.SerializeObject(level, settings);
            File.WriteAllText(levelJsonPath, json);

            // ---- Write strings.json ----
            ExportStringsJson(config, levelDir);

            lastExportedPath = levelJsonPath;
            AssetDatabase.Refresh();

            int strCount = config.strings != null ? config.strings.Count : 0;
            string msg = $"Exported level \"{level.levelId}\" → {levelDir}/\n" +
                $"{level.interactables.Count} interactables, {level.groups.Count} groups, " +
                $"{level.staticObjects.Count} static objects, {level.dropZones.Count} drop zones, " +
                $"{strCount} strings.";

            // ---- Optional: capture preview PNG ----
            if (capturePreview)
            {
                string previewPath = CaptureLevelPreview(config, levelDir, out int capturedW, out int capturedH);
                if (!string.IsNullOrEmpty(previewPath))
                    msg += $"\nPreview: {Path.GetFileName(previewPath)} " +
                           $"({capturedW}×{capturedH})";
                AssetDatabase.Refresh();
            }

            ShowMessage(msg, MessageType.Info);
        }
        catch (System.Exception e)
        {
            ShowMessage("Export failed: " + e.Message + "\n" + e.StackTrace, MessageType.Error);
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// Renders the level camera into a PNG at <c>levelDir/previewFilename</c>
    /// using the configured width / height / transparency. Returns the
    /// path on success or null on failure (warnings go to the console so
    /// export still considers the level a success).
    /// </summary>
    private string CaptureLevelPreview(B_LevelConfig cfg, string levelDir,
                                        out int capturedWidth, out int capturedHeight)
    {
        capturedWidth = 0; capturedHeight = 0;
        Camera cam = cfg.levelCamera != null ? cfg.levelCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[LevelExporter] Skipped preview capture: no camera (Level Camera unassigned and Camera.main is null).");
            return null;
        }

        // Try to fit on a specific object's bounds (typically the bg sprite)
        // — gives a tight crop with zero blank space.
        Bounds? fitBounds = ResolveFitBounds(previewFitObjectId);

        int w = Mathf.Max(16, previewWidth);
        int h = Mathf.Max(16, previewHeight);

        if (fitBounds.HasValue)
        {
            // Height follows the bg's aspect so the capture is fully filled.
            float bgW = fitBounds.Value.size.x;
            float bgH = fitBounds.Value.size.y;
            if (bgW > 0f && bgH > 0f)
                h = Mathf.Max(16, Mathf.RoundToInt(w * (bgH / bgW)));
        }
        else if (previewMatchAspect && cfg.virtualWidth > 0 && cfg.virtualHeight > 0)
        {
            // Fall back to virtual viewport aspect when no fit object was
            // specified / found.
            h = Mathf.Max(16,
                Mathf.RoundToInt(w * ((float)cfg.virtualHeight / cfg.virtualWidth)));
        }
        capturedWidth = w; capturedHeight = h;
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 1;

        // Snapshot camera state we may mutate during capture.
        CameraClearFlags prevClear = cam.clearFlags;
        Color prevBg = cam.backgroundColor;
        RenderTexture prevTarget = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;
        float prevOrthoSize = cam.orthographicSize;
        Vector3 prevCamPos = cam.transform.position;

        try
        {
            if (previewTransparent)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
            }

            // Frame the camera tightly on the fit object so the PNG fills
            // edge-to-edge. orthographicSize = half-height of the bounds;
            // camera position moves to the bounds center (preserving Z so
            // depth ordering stays the same).
            if (fitBounds.HasValue)
            {
                Bounds b = fitBounds.Value;
                cam.orthographicSize = Mathf.Max(0.001f, b.size.y * 0.5f);
                Vector3 framedPos = new Vector3(b.center.x, b.center.y, prevCamPos.z);
                cam.transform.position = framedPos;
            }

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(w, h,
                previewTransparent ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string fullPath = Path.Combine(levelDir, previewFilename).Replace("\\", "/");
            File.WriteAllBytes(fullPath, png);
            return fullPath;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[LevelExporter] Preview capture failed: " + e.Message);
            return null;
        }
        finally
        {
            cam.targetTexture = prevTarget;
            cam.clearFlags = prevClear;
            cam.backgroundColor = prevBg;
            cam.orthographicSize = prevOrthoSize;
            cam.transform.position = prevCamPos;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    /// <summary>
    /// Find a renderer in the scene whose owning B_StaticObject /
    /// B_InteractableObject has the given <paramref name="objectId"/> and
    /// return its world-space bounds. Returns null if id is empty or no
    /// match is found.
    /// </summary>
    private static Bounds? ResolveFitBounds(string objectId)
    {
        if (string.IsNullOrEmpty(objectId)) return null;

        // Static first (typical case: "bg" is a static sprite).
        foreach (var so in Object.FindObjectsByType<B_StaticObject>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (so.ObjectId != objectId) continue;
            return GetRendererBounds(so.gameObject);
        }
        // Fall back to interactables.
        foreach (var io in Object.FindObjectsByType<B_InteractableObject>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (io.ObjectId != objectId) continue;
            return GetRendererBounds(io.gameObject);
        }
        Debug.LogWarning(
            $"[LevelExporter] Fit-to-object: no B_StaticObject / B_InteractableObject " +
            $"with objectId '{objectId}' found. Falling back to camera default framing.");
        return null;
    }

    private static Bounds? GetRendererBounds(GameObject go)
    {
        if (go == null) return null;
        SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null) return sr.bounds;
        MeshRenderer mr = go.GetComponentInChildren<MeshRenderer>();
        if (mr != null && mr.bounds.size != Vector3.zero) return mr.bounds;
        return null;
    }

    // ---- Level root ------------------------------------------------

    private LevelJson BuildLevel(B_LevelConfig config, List<string> errors)
    {
        ResolveCoordinateSystem(config, errors);

        LevelJson level = new LevelJson
        {
            schemaVersion = 1,
            levelId = EmptyToNull(config.levelId),
            title = EmptyToNull(config.title),
            description = EmptyToNull(config.description),
            assetPathPrefix = EmptyToNull(outputAssetPrefix),
            defaultHintMessageKey = EmptyToNull(config.defaultHintMessageKey),
            timeLimit = config.timeLimit,
            timeUpTargetId = config.timeUpTarget != null
                ? EmptyToNull(config.timeUpTarget.ObjectId) : null,
            timeUpStateId = EmptyToNull(config.timeUpStateId),
            viewport = new ViewportJson
            {
                virtualWidth = config.virtualWidth,
                virtualHeight = config.virtualHeight,
            },
            interactables = new List<InteractableJson>(),
            groups = new List<GroupJson>(),
            queues = new List<QueueJson>(),
            staticObjects = new List<StaticObjectJson>(),
            dropZones = new List<DropZoneJson>(),
            win = BuildConditions(config.winConditions, errors, "win"),
            lose = BuildConditions(config.loseConditions, errors, "lose"),
        };

        if (string.IsNullOrEmpty(level.levelId))
            errors.Add("B_LevelConfig.levelId is empty.");

        // Interactables
        B_InteractableObject[] allInteractables =
            Object.FindObjectsByType<B_InteractableObject>(FindObjectsSortMode.InstanceID);

        HashSet<string> seenIds = new HashSet<string>();
        foreach (B_InteractableObject obj in allInteractables)
        {
            if (string.IsNullOrEmpty(obj.ObjectId))
            {
                errors.Add($"Interactable '{obj.name}' has empty Object Id.");
                continue;
            }
            if (!seenIds.Add(obj.ObjectId))
            {
                errors.Add($"Duplicate Object Id '{obj.ObjectId}' on '{obj.name}'.");
                continue;
            }

            level.interactables.Add(BuildInteractable(obj, errors));
        }

        // Groups
        B_InteractableGroup[] allGroups =
            Object.FindObjectsByType<B_InteractableGroup>(FindObjectsSortMode.InstanceID);
        foreach (B_InteractableGroup grp in allGroups)
        {
            level.groups.Add(BuildGroup(grp, errors));
        }

        // Queues
        B_InteractableQueue[] allQueues =
            Object.FindObjectsByType<B_InteractableQueue>(FindObjectsSortMode.InstanceID);
        foreach (B_InteractableQueue q in allQueues)
        {
            level.queues.Add(BuildQueue(q, errors));
        }

        // Static objects
        B_StaticObject[] allStatics =
            Object.FindObjectsByType<B_StaticObject>(FindObjectsSortMode.InstanceID);
        foreach (B_StaticObject s in allStatics)
        {
            level.staticObjects.Add(BuildStaticObject(s, errors));
        }

        // Standalone drop zones — exclude zones that are nested under an
        // interactable OR a static (those are written into the parent's
        // dropZones array instead). Without the static check, a zone on
        // the same GameObject as a static would be exported twice — once
        // nested, once standalone — and the importer would split the
        // authoring back into two separate GameObjects.
        B_DropZone[] allZones =
            Object.FindObjectsByType<B_DropZone>(FindObjectsSortMode.InstanceID);
        foreach (B_DropZone zone in allZones)
        {
            if (zone.GetComponentInParent<B_InteractableObject>() != null) continue;
            if (zone.GetComponentInParent<B_StaticObject>() != null) continue;
            level.dropZones.Add(BuildStandaloneDropZone(zone, errors));
        }

        return level;
    }

    // ---- Group ---------------------------------------------------------

    private GroupJson BuildGroup(B_InteractableGroup grp, List<string> errors)
    {
        Transform t = grp.transform;
        Collider2D col = grp.GetComponent<Collider2D>();

        GroupJson j = new GroupJson
        {
            position = WorldToPx(t.position),
            sortOrder = grp.GetSortOrder(),
            pickMode = grp.Mode.ToString(),
            visualMode = grp.VisualMode.ToString(),
            collider = BuildCollider(col, t.position),
            initialSkins = grp.VisualMode == VisualMode.Spine
                ? ResolveInitialSkins(grp.gameObject) : null,
            data = BuildObjectData(grp.Data, grp.VisualMode, errors),
            members = new List<GroupMemberJson>(),
        };

        if (grp.Members != null)
        {
            bool useSprite = grp.VisualMode == VisualMode.Sprite;
            for (int i = 0; i < grp.Members.Count; i++)
            {
                GameObject m = grp.Members[i];
                if (m == null) continue;

                SpriteRenderer sr = m.GetComponent<SpriteRenderer>();
                Spine.Unity.SkeletonAnimation mSkel = m.GetComponent<Spine.Unity.SkeletonAnimation>();
                j.members.Add(new GroupMemberJson
                {
                    sprite = useSprite ? ResolveAssetPath(sr != null ? sr.sprite : null) : null,
                    spineBasePath = useSprite ? null : EmptyToNull(ResolveSpineBasePath(mSkel)),
                    position = WorldToPx(ResolveVisualPosition(m)),
                    sortOrder = ResolveSortOrder(m),
                });
            }
        }

        return j;
    }

    // ---- Queue --------------------------------------------------------

    private QueueJson BuildQueue(B_InteractableQueue q, List<string> errors)
    {
        Transform t = q.transform;
        Collider2D col = q.GetComponent<Collider2D>();

        QueueJson j = new QueueJson
        {
            queueId = EmptyToNull(q.QueueId),
            position = WorldToPx(t.position),
            sortOrder = q.GetSortOrder(),
            visualMode = q.VisualMode.ToString(),
            collider = col != null ? BuildCollider(col, t.position) : null,
            initialSkins = q.VisualMode == VisualMode.Spine
                ? ResolveInitialSkins(q.gameObject) : null,
            data = BuildObjectData(q.Data, q.VisualMode, errors),
            members = new List<GroupMemberJson>(),
            tailFollowers = null, // populated below if non-empty
            slots = new List<Vec2Json>(),
            shiftDuration = q.ShiftDuration,
            shiftEase = q.ShiftEase.ToString(),
            queueEmptyTargetId = q.QueueEmptyTarget != null
                ? EmptyToNull(q.QueueEmptyTarget.ObjectId) : null,
            queueEmptyStateId = EmptyToNull(q.QueueEmptyStateId),
        };

        if (q.Members != null)
        {
            bool useSprite = q.VisualMode == VisualMode.Sprite;
            for (int i = 0; i < q.Members.Count; i++)
            {
                GameObject m = q.Members[i];
                if (m == null) continue;

                SpriteRenderer sr = m.GetComponent<SpriteRenderer>();
                Spine.Unity.SkeletonAnimation mSkel = m.GetComponent<Spine.Unity.SkeletonAnimation>();
                j.members.Add(new GroupMemberJson
                {
                    sprite = useSprite ? ResolveAssetPath(sr != null ? sr.sprite : null) : null,
                    spineBasePath = useSprite ? null : EmptyToNull(ResolveSpineBasePath(mSkel)),
                    position = WorldToPx(ResolveVisualPosition(m)),
                    sortOrder = ResolveSortOrder(m),
                });
            }
        }

        if (q.Slots != null)
        {
            for (int i = 0; i < q.Slots.Count; i++)
            {
                Transform slot = q.Slots[i];
                if (slot == null) continue;
                j.slots.Add(WorldToPx(slot.position));
            }
        }

        // Tail followers — characters that trail the line and shift up
        // with every serve but are never themselves served.
        if (q.TailFollowers != null && q.TailFollowers.Count > 0)
        {
            bool useSprite = q.VisualMode == VisualMode.Sprite;
            j.tailFollowers = new List<GroupMemberJson>();
            // Dedupe: skip nulls, duplicates within the followers list, and
            // any GO that's already a regular member. Re-imports of an
            // already-imported scene can leave the same follower wired in
            // multiple times — exporting them all would multiply on each
            // round-trip.
            HashSet<GameObject> seen = new HashSet<GameObject>();
            if (q.Members != null)
                foreach (var m in q.Members)
                    if (m != null) seen.Add(m);

            for (int i = 0; i < q.TailFollowers.Count; i++)
            {
                GameObject f = q.TailFollowers[i];
                if (f == null) continue;
                if (!seen.Add(f)) continue; // duplicate of an earlier entry or a member

                // If the follower is its own top-level B_InteractableObject,
                // it's already exported (and will be spawned) in the
                // interactables array. Just store the id reference here so
                // the importer wires the existing GO into tailFollowers
                // instead of spawning a duplicate child.
                B_InteractableObject fObj = f.GetComponent<B_InteractableObject>();
                if (fObj != null && !string.IsNullOrEmpty(fObj.ObjectId))
                {
                    j.tailFollowers.Add(new GroupMemberJson
                    {
                        objectIdRef = fObj.ObjectId,
                    });
                    continue;
                }

                SpriteRenderer sr = f.GetComponent<SpriteRenderer>();
                Spine.Unity.SkeletonAnimation fSkel = f.GetComponent<Spine.Unity.SkeletonAnimation>();
                j.tailFollowers.Add(new GroupMemberJson
                {
                    sprite = useSprite ? ResolveAssetPath(sr != null ? sr.sprite : null) : null,
                    spineBasePath = useSprite ? null : EmptyToNull(ResolveSpineBasePath(fSkel)),
                    position = WorldToPx(ResolveVisualPosition(f)),
                    sortOrder = ResolveSortOrder(f),
                });
            }
        }

        if (j.members.Count == 0)
            errors.Add($"Queue '{q.QueueId ?? q.name}' has no members.");
        if (j.slots.Count == 0)
            errors.Add($"Queue '{q.QueueId ?? q.name}' has no slot anchors.");

        return j;
    }

    // ---- Static object ------------------------------------------------

    private StaticObjectJson BuildStaticObject(B_StaticObject s, List<string> errors)
    {
        Transform t = s.transform;
        SpriteRenderer sr = s.GetComponent<SpriteRenderer>();
        Collider2D col = s.GetComponent<Collider2D>();
        Vector3 visualPos = ResolveVisualPosition(s.gameObject);

        StaticObjectJson j = new StaticObjectJson
        {
            objectId = EmptyToNull(s.ObjectId),
            startHidden = s.StartHidden,
            visualMode = s.VisualMode.ToString(),
            sprite = s.VisualMode == VisualMode.Sprite
                ? ResolveAssetPath(sr != null ? sr.sprite : null) : null,
            spineBasePath = s.VisualMode == VisualMode.Sprite
                ? null : EmptyToNull(ResolveSpineBasePath(s.Skeleton)),
            initSpineAnim = s.VisualMode == VisualMode.Sprite
                ? null : EmptyToNull(s.InitSpineAnim),
            initSpineLoop = s.VisualMode == VisualMode.Sprite ? false : s.InitSpineLoop,
            initialSkins = s.VisualMode == VisualMode.Spine
                ? ResolveInitialSkins(s.gameObject) : null,
            position = WorldToPx(visualPos),
            scale = t.localScale.x,
            rotation = t.eulerAngles.z,
            sortOrder = s.GetSortOrder(),
            collider = BuildCollider(col, visualPos),
            blocks = col != null,
            dropZones = new List<DropZoneLocalJson>(),
        };

        // Nested drop zones — mirror BuildInteractable. Any B_DropZone whose
        // nearest puzzle ancestor IS this static gets serialized as a nested
        // entry (so the round-trip preserves the "one GameObject hosts both"
        // authoring pattern). These zones are SKIPPED from the standalone
        // scan in BuildLevel.
        foreach (B_DropZone zone in s.GetComponentsInChildren<B_DropZone>())
        {
            if (zone.GetComponentInParent<B_StaticObject>() != s) continue;
            // Also skip if the zone is under an interactable nested under
            // this static (defensive — unusual but possible).
            if (zone.GetComponentInParent<B_InteractableObject>() != null) continue;

            Collider2D zCol = zone.GetComponent<Collider2D>();
            Vector3 zoneCenterWorld = zCol != null ? zCol.bounds.center : zone.transform.position;
            Vector3 worldOffset = zoneCenterWorld - visualPos;

            j.dropZones.Add(new DropZoneLocalJson
            {
                zoneId = zone.ZoneId,
                sortOrder = zone.SortOrder,
                localOffset = WorldDeltaToPx(worldOffset),
                size = zCol != null ? WorldDeltaToPx(zCol.bounds.size) : new Vec2Json { x = 0, y = 0 },
                shape = "box",
            });

            if (string.IsNullOrEmpty(zone.ZoneId))
                errors.Add($"Drop zone on '{zone.name}' (child of static '{s.ObjectId}') has empty Zone Id.");
        }

        return j;
    }

    // ---- Interactable ----------------------------------------------

    private InteractableJson BuildInteractable(B_InteractableObject obj, List<string> errors)
    {
        Transform t = obj.transform;
        Collider2D col = obj.GetComponent<Collider2D>();
        Vector3 visualPos = ResolveVisualPosition(obj.gameObject);

        bool objUseSprite = obj.VisualMode == VisualMode.Sprite;
        InteractableJson j = new InteractableJson
        {
            objectId = obj.ObjectId,
            startHidden = obj.StartHidden,
            visualMode = obj.VisualMode.ToString(),
            position = WorldToPx(visualPos),
            scale = t.localScale.x,
            rotation = t.eulerAngles.z,
            sortOrder = ResolveSortOrder(obj.gameObject),
            collider = BuildCollider(col, visualPos),
            spineBasePath = objUseSprite ? null : EmptyToNull(ResolveSpineBasePath(obj.Skeleton)),
            initialSkins = objUseSprite ? null : ResolveInitialSkins(obj.gameObject),
            data = BuildObjectData(obj.Data, obj.VisualMode, errors),
            dropZones = new List<DropZoneLocalJson>(),
        };

        if (obj.Data == null)
            errors.Add($"Interactable '{obj.ObjectId}' has no ObjectData assigned.");

        // Nested drop zones (living under this interactable)
        foreach (B_DropZone zone in obj.GetComponentsInChildren<B_DropZone>())
        {
            // Only include zones whose nearest interactable parent IS this one.
            if (zone.GetComponentInParent<B_InteractableObject>() != obj) continue;

            // Offset relative to the SAME anchor used by position+collider
            // (visualPos). Use the zone's COLLIDER CENTER — this folds in
            // the zone's BoxCollider2D.offset too, so LibGDX (which only
            // gets localOffset + size, no separate zone-collider-offset)
            // still reproduces the correct world position.
            Collider2D zCol = zone.GetComponent<Collider2D>();
            Vector3 zoneCenterWorld = zCol != null ? zCol.bounds.center : zone.transform.position;
            Vector3 worldOffset = zoneCenterWorld - visualPos;

            j.dropZones.Add(new DropZoneLocalJson
            {
                zoneId = zone.ZoneId,
                sortOrder = zone.SortOrder,
                localOffset = WorldDeltaToPx(worldOffset),
                size = zCol != null ? WorldDeltaToPx(zCol.bounds.size) : new Vec2Json { x = 0, y = 0 },
                shape = "box",
            });

            if (string.IsNullOrEmpty(zone.ZoneId))
                errors.Add($"Drop zone on '{zone.name}' (child of '{obj.ObjectId}') has empty Zone Id.");
        }

        return j;
    }

    // ---- Object data / states / actions ----------------------------

    private ObjectDataJson BuildObjectData(ObjectData data, List<string> errors)
    {
        return BuildObjectData(data, VisualMode.Sprite, errors);
    }

    /// <summary>
    /// Mode-aware variant. Sprite mode zeroes the Spine-specific init fields
    /// (and every state's Spine-specific fields); Spine mode zeroes the
    /// Sprite-specific init fields (and every state's Sprite-specific
    /// fields). Keeps the exported JSON clean — designers can't leak a
    /// stale sprite into a Spine-mode object or vice versa, even if they
    /// had both assigned in the inspector.
    /// </summary>
    private ObjectDataJson BuildObjectData(ObjectData data, VisualMode mode, List<string> errors)
    {
        if (data == null)
        {
            return new ObjectDataJson
            {
                initSprite = null,
                initSfx = null,
                states = new List<ObjectStateJson>(),
            };
        }

        bool useSprite = mode == VisualMode.Sprite;

        ObjectDataJson j = new ObjectDataJson
        {
            initStateId = EmptyToNull(data.initStateId),
            initSprite = useSprite ? ResolveAssetPath(data.initSprite) : null,
            initSpineAnim = useSprite ? null : EmptyToNull(data.initSpineAnim),
            initSpineLoop = useSprite ? false : data.initSpineLoop,
            initSfx = ResolveAssetPath(data.initSFX),
            states = new List<ObjectStateJson>(),
        };

        if (data.states != null)
        {
            foreach (ObjectState s in data.states)
                j.states.Add(BuildState(s, mode, errors));
        }

        return j;
    }

    private ObjectStateJson BuildState(ObjectState s, List<string> errors)
    {
        return BuildState(s, VisualMode.Sprite, errors);
    }

    private ObjectStateJson BuildState(ObjectState s, VisualMode mode, List<string> errors)
    {
        bool useSprite = mode == VisualMode.Sprite;

        ObjectStateJson j = new ObjectStateJson
        {
            stateId = EmptyToNull(s.stateId),
            dragSprite = useSprite ? ResolveAssetPath(s.dragSprite) : null,
            stateSprite = useSprite ? ResolveAssetPath(s.stateSprite) : null,
            stateSpineAnim = useSprite ? null : EmptyToNull(s.stateSpineAnim),
            stateSpineLoop = useSprite ? false : s.stateSpineLoop,
            stateSfx = ResolveAssetPath(s.stateSFX),
            trigger = s.trigger.ToString(),
            requiredZoneId = EmptyToNull(s.requiredZoneId),
            requiredCount = s.requiredCount,
            requirements = new List<StateRequirementJson>(),
            actions = new List<StateActionJson>(),
            successMessageKey = EmptyToNull(s.successMessageKey),
            failMessageKey = EmptyToNull(s.failMessageKey),
            hintMessageKey = EmptyToNull(s.hintMessageKey),
            repeatable = s.repeatable,
        };

        if (s.requirements != null)
        {
            foreach (StateRequirement r in s.requirements)
            {
                j.requirements.Add(new StateRequirementJson
                {
                    objectId = EmptyToNull(r.objectId),
                    stateId = EmptyToNull(r.stateId),
                    requireNotDone = r.requireNotDone,
                    gate = r.gate,
                });
            }
        }

        if (s.actions != null)
        {
            foreach (StateAction a in s.actions)
            {
                if (a == null) continue;
                j.actions.Add(BuildAction(a, errors));
            }
        }

        return j;
    }

    private static bool ActionUsesActionTarget(StateActionType type)
    {
        switch (type)
        {
            case StateActionType.MoveTo:
            case StateActionType.Disappear:
            case StateActionType.Appear:
            case StateActionType.DoAnimation:
            case StateActionType.ScaleTo:
            // Attach/Detach use actionTarget as the optional SUBJECT override
            // (default = the object owning the state).
            case StateActionType.AttachToBone:
            case StateActionType.DetachFromBone:
            // EnqueueMember: actionTarget = the GameObject to add to the queue
            // (default = self — the object whose state is firing).
            case StateActionType.EnqueueMember:
                return true;
            default:
                return false;
        }
    }

    private StateActionJson BuildAction(StateAction a, List<string> errors)
    {
        StateActionJson j = new StateActionJson
        {
            type = a.type.ToString(),
            runInParallel = a.runInParallel,
            duration = a.duration,
        };

        // Resolve the optional actionTarget to a string id — but only for
        // action types that actually consume it at runtime. Without this
        // gate, designers who change an action's type after assigning
        // actionTarget leave the field dangling (it gets serialized but
        // the new action type ignores it), bloating JSON and confusing
        // LibGDX-side debugging.
        if (a.actionTarget != null && ActionUsesActionTarget(a.type))
            j.actionTargetId = ResolveObjectId(a.actionTarget, errors);

        switch (a.type)
        {
            case StateActionType.Wait:
                // duration only
                break;

            case StateActionType.MoveTo:
                FillMoveTarget(j, a.moveTarget);
                j.ease = a.ease.ToString();
                if (a.rotateToMatchTarget) j.rotateToMatchTarget = true;
                break;

            case StateActionType.Disappear:
                j.fadeOut = a.fadeOut;
                j.destroyOnDisappear = a.destroyOnDisappear;
                break;

            case StateActionType.Appear:
                j.fadeIn = a.fadeIn;
                break;

            case StateActionType.DoAnimation:
                j.spineAnim = EmptyToNull(a.spineAnim);
                j.spineLoop = a.spineLoop;
                break;

            case StateActionType.ActivateState:
                if (a.activateTarget != null)
                    j.activateTargetObjectId = EmptyToNull(a.activateTarget.ObjectId);
                j.activateStateId = EmptyToNull(a.activateStateId);
                if (a.chainGuards != null && a.chainGuards.Count > 0)
                {
                    j.chainGuards = new List<StateRequirementJson>();
                    for (int g = 0; g < a.chainGuards.Count; g++)
                    {
                        StateRequirement req = a.chainGuards[g];
                        if (string.IsNullOrEmpty(req.objectId)) continue;
                        j.chainGuards.Add(new StateRequirementJson
                        {
                            objectId = req.objectId,
                            stateId = EmptyToNull(req.stateId),
                            requireNotDone = req.requireNotDone,
                            gate = req.gate,
                        });
                    }
                }
                break;

            case StateActionType.AdvanceQueue:
                if (a.queueTarget != null)
                    j.queueTargetId = EmptyToNull(a.queueTarget.QueueId);
                j.queueServeStateId = EmptyToNull(a.queueServeStateId);
                break;

            case StateActionType.PlaySFX:
                j.sfxClip = ResolveAssetPath(a.sfxClip);
                break;

            case StateActionType.SkinChange:
                if (a.skinTarget != null)
                    j.skinTargetObjectId = EmptyToNull(a.skinTarget.ObjectId);
                j.skinName = EmptyToNull(a.skinName);
                j.skinOp = a.skinOp.ToString();
                break;

            case StateActionType.ScaleTo:
                j.scaleTarget = a.scaleTarget;
                j.ease = a.ease.ToString();
                break;

            case StateActionType.AttachToBone:
                if (a.boneSource != null)
                    j.boneSourceObjectId = ResolveObjectId(a.boneSource, errors);
                j.boneName = EmptyToNull(a.boneName);
                j.keepBoneOffset = a.keepBoneOffset;
                break;

            case StateActionType.DetachFromBone:
                // Subject only — carried by actionTargetId (or self).
                break;

            case StateActionType.EnqueueMember:
                if (a.queueTarget != null)
                    j.queueTargetId = EmptyToNull(a.queueTarget.QueueId);
                // Subject (the GO to add) — carried by actionTargetId (or self).
                break;
        }

        return j;
    }

    /// <summary>
    /// Resolves a GameObject reference to a portable string id by checking
    /// for B_InteractableObject.ObjectId, then B_StaticObject.ObjectId,
    /// then falling back to the GameObject's name.
    /// </summary>
    private string ResolveObjectId(GameObject go, List<string> errors)
    {
        if (go == null) return null;

        var interactable = go.GetComponent<B_InteractableObject>();
        if (interactable != null)
        {
            if (string.IsNullOrEmpty(interactable.ObjectId))
                errors.Add($"Action targets interactable '{go.name}' which has an empty Object Id.");
            return EmptyToNull(interactable.ObjectId);
        }

        var staticObj = go.GetComponent<B_StaticObject>();
        if (staticObj != null)
        {
            if (string.IsNullOrEmpty(staticObj.ObjectId))
                errors.Add($"Action targets static object '{go.name}' which has an empty Object Id. Set one in the B_StaticObject inspector.");
            return EmptyToNull(staticObj.ObjectId);
        }

        // Fallback: use the GameObject name. Warn since it's fragile.
        errors.Add($"Action targets '{go.name}' which has no B_InteractableObject or B_StaticObject. Using GameObject name as id — rename-sensitive.");
        return go.name;
    }

    /// <summary>
    /// Resolve a MoveTo's Transform reference into the JSON equivalent:
    ///   - If it's a B_DropZone, write objectId (of its parent) + zoneId.
    ///   - Else if it has a B_InteractableObject, write its objectId.
    ///   - Otherwise, write the absolute world position.
    /// </summary>
    private void FillMoveTarget(StateActionJson j, Transform target)
    {
        if (target == null) return;

        B_DropZone zone = target.GetComponent<B_DropZone>();
        if (zone != null)
        {
            j.moveTargetZoneId = zone.ZoneId;
            B_InteractableObject parent = zone.GetComponentInParent<B_InteractableObject>();
            if (parent != null) j.moveTargetObjectId = parent.ObjectId;
            return;
        }

        B_InteractableObject obj = target.GetComponent<B_InteractableObject>();
        if (obj != null)
        {
            j.moveTargetObjectId = obj.ObjectId;
            return;
        }

        // Fall back to a literal viewport position in virtual pixels. Also
        // capture the Z rotation so `rotateToMatchTarget` has something to
        // tween toward on re-import (the anchor would otherwise spawn at
        // identity rotation, making the tween a no-op).
        j.moveTargetPosition = WorldToPx(target.position);
        j.moveTargetRotation = target.eulerAngles.z;
    }

    // ---- Drop zones -------------------------------------------------

    private DropZoneJson BuildStandaloneDropZone(B_DropZone zone, List<string> errors)
    {
        Collider2D col = zone.GetComponent<Collider2D>();
        // Use the collider center (includes BoxCollider2D.offset) as the
        // exported position so LibGDX reconstructs the zone at the right
        // world location even when the designer offset the collider on
        // the zone GameObject.
        Vector3 pos = col != null ? col.bounds.center : zone.transform.position;
        Vector3 size = col != null ? col.bounds.size : Vector3.one;

        if (string.IsNullOrEmpty(zone.ZoneId))
            errors.Add($"Standalone drop zone '{zone.name}' has empty Zone Id.");

        return new DropZoneJson
        {
            zoneId = zone.ZoneId,
            sortOrder = zone.SortOrder,
            position = WorldToPx(pos),
            size = WorldDeltaToPx(size),
            shape = "box",
        };
    }

    // ---- Colliders --------------------------------------------------

    /// <summary>
    /// Builds a collider JSON entry. The exported offset is the collider's
    /// world-space center MINUS the object's exported visual position, so
    /// the importer can reconstruct the collider's offset relative to the
    /// visual and keep them aligned even when the designer used
    /// BoxCollider2D.offset or placed the collider on a parent transform
    /// different from the renderer.
    /// </summary>
    private ColliderJson BuildCollider(Collider2D col, Vector3 visualWorldPos)
    {
        if (col == null) return null;

        // bounds.center already includes transform + collider.offset.
        Vector3 centerOffset = col.bounds.center - visualWorldPos;

        if (col is CircleCollider2D circle)
        {
            float worldRadius = circle.radius * Mathf.Abs(col.transform.lossyScale.x);
            return new ColliderJson
            {
                type = "circle",
                radius = worldRadius * pxPerUnit,
                offsetX = centerOffset.x * pxPerUnit,
                offsetY = centerOffset.y * pxPerUnit,
            };
        }

        Vector3 size = col.bounds.size;
        return new ColliderJson
        {
            type = "box",
            width = size.x * pxPerUnit,
            height = size.y * pxPerUnit,
            offsetX = centerOffset.x * pxPerUnit,
            offsetY = centerOffset.y * pxPerUnit,
        };
    }

    // ---- Conditions -------------------------------------------------

    private List<ConditionJson> BuildConditions(
        List<LevelCondition> source, List<string> errors, string label)
    {
        List<ConditionJson> result = new List<ConditionJson>();
        if (source == null) return result;

        for (int i = 0; i < source.Count; i++)
        {
            LevelCondition c = source[i];
            if (c == null) continue;

            string objId = c.GetEffectiveTargetId();
            if (string.IsNullOrEmpty(objId))
            {
                errors.Add($"{label} condition #{i}: targetId is empty.");
                continue;
            }

            result.Add(new ConditionJson
            {
                type = CamelCase(c.type.ToString()),
                objectId = objId,
                stateId = EmptyToNull(c.stateId),
            });
        }
        return result;
    }

    // ---- Coordinate system ------------------------------------------

    /// <summary>
    /// Sets <see cref="pxPerUnit"/> and <see cref="originWorld"/> from the
    /// scene camera so that exported pixel coordinates line up exactly with
    /// the LibGDX viewport. The LibGDX side centres its camera on (0, 0)
    /// and uses ExtendViewport(virtualWidth, virtualHeight), so positions
    /// in the JSON must be in CAMERA-CENTRED virtual pixels: an object
    /// sitting at the Unity camera's centre exports as (0, 0), and the
    /// visible region runs from (-virtualWidth/2, -virtualHeight/2) to
    /// (+virtualWidth/2, +virtualHeight/2).
    /// Falls back to a manual scale + Unity world origin if no camera is
    /// available, but emits a warning so the designer knows.
    /// </summary>
    private void ResolveCoordinateSystem(B_LevelConfig config, List<string> errors)
    {
        Camera cam = config.levelCamera != null
            ? config.levelCamera
            : Camera.main;
        if (cam == null) cam = Object.FindAnyObjectByType<Camera>();

        if (cam == null)
        {
            errors.Add(
                "No camera found in scene. Coordinates will use manual Pixels Per Unit and Unity world origin — objects will be offset in LibGDX.");
            pxPerUnit = Mathf.Max(0.0001f, config.pixelsPerUnit);
            originWorld = Vector2.zero;
            return;
        }

        if (!cam.orthographic)
        {
            errors.Add($"Level Camera '{cam.name}' is perspective. Switch it to Orthographic for accurate coordinate export.");
        }

        // Derive the world→pixel scale from the camera height. Deterministic
        // regardless of editor game-view size.
        float halfHeightWorld = cam.orthographicSize;
        pxPerUnit = config.virtualHeight / (halfHeightWorld * 2f);

        // CAMERA-CENTRED origin: an object at cam.position exports as (0, 0).
        originWorld = cam.transform.position;

        // Sanity check: the virtual viewport's aspect MUST match the camera's
        // aspect, otherwise positions on one axis will be wrong by the ratio.
        // We compare against the game-view aspect (Camera.aspect) which uses
        // the current display, falling back to virtualWidth/virtualHeight only
        // when no display is available.
        float virtualAspect = config.virtualWidth / (float)config.virtualHeight;
        float cameraAspect = cam.aspect;
        if (Mathf.Abs(virtualAspect - cameraAspect) > 0.01f)
        {
            bool virtualIsPortrait = virtualAspect < 1f;
            bool cameraIsPortrait = cameraAspect < 1f;
            string orientationHint = (virtualIsPortrait != cameraIsPortrait)
                ? $" Virtual is {(virtualIsPortrait ? "PORTRAIT" : "LANDSCAPE")} ({config.virtualWidth}x{config.virtualHeight}) but the camera/game view is {(cameraIsPortrait ? "PORTRAIT" : "LANDSCAPE")} (aspect {cameraAspect:0.000}). Did you forget to swap virtualWidth and virtualHeight?"
                : $" Virtual aspect {virtualAspect:0.000} ≠ camera aspect {cameraAspect:0.000}.";
            errors.Add(
                "Aspect mismatch between virtual viewport and Level Camera." + orientationHint);
        }
    }

    /// <summary>Converts a Unity world position into virtual viewport pixels relative to the camera's bottom-left.</summary>
    private Vec2Json WorldToPx(Vector3 world)
    {
        return new Vec2Json
        {
            x = (world.x - originWorld.x) * pxPerUnit,
            y = (world.y - originWorld.y) * pxPerUnit,
        };
    }

    /// <summary>Converts a Unity world delta (size, extent, offset) into virtual viewport pixels. No origin shift.</summary>
    private Vec2Json WorldDeltaToPx(Vector3 worldDelta)
    {
        return new Vec2Json
        {
            x = worldDelta.x * pxPerUnit,
            y = worldDelta.y * pxPerUnit,
        };
    }

    /// <summary>
    /// Reads the sortingOrder from whichever renderer drives the object:
    /// SpriteRenderer on self or any child (sprite mode), or MeshRenderer
    /// on any child (spine mode — SkeletonAnimation's MeshRenderer). Falls
    /// back to 0 if neither exists.
    /// </summary>
    private static int ResolveSortOrder(GameObject go)
    {
        if (go == null) return 0;
        SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) return sr.sortingOrder;
        // Spine: the SkeletonAnimation child has a MeshRenderer.
        MeshRenderer mr = go.GetComponentInChildren<MeshRenderer>();
        if (mr != null) return mr.sortingOrder;
        return 0;
    }

    /// <summary>
    /// Returns the world position where the visual of this object actually
    /// renders. Prefers the spine SkeletonAnimation's transform, then any
    /// SpriteRenderer on a child, then falls back to the root transform.
    /// Using this for export makes the JSON position match what the
    /// importer re-creates (spine child parented with localPos=0).
    /// </summary>
    private static Vector3 ResolveVisualPosition(GameObject go)
    {
        if (go == null) return Vector3.zero;
        var skel = go.GetComponentInChildren<Spine.Unity.SkeletonAnimation>();
        if (skel != null) return skel.transform.position;
        SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) return sr.transform.position;
        return go.transform.position;
    }

    // ---- Utilities --------------------------------------------------

    private string ResolveAssetPath(Object asset)
    {
        if (asset == null) return null;
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path)) return null;

        // Strip the Unity-side prefix (e.g. "Assets/LevelAssets/").
        if (!string.IsNullOrEmpty(assetRoot) && path.StartsWith(assetRoot))
            path = path.Substring(assetRoot.Length);

        path = path.Replace("\\", "/");

        // Prepend the runtime-side prefix (e.g. "assets/levels/") so the
        // exported path matches what LibGDX expects.
        if (!string.IsNullOrEmpty(outputAssetPrefix))
            path = outputAssetPrefix + path;

        return path;
    }

    /// <summary>
    /// Resolves a SkeletonAnimation's underlying .skel/.json file path and
    /// strips its extension to produce a LibGDX-friendly base path like
    /// "assets/levels/lv1/anim/Bg_Ngheo". LibGDX spine loaders append
    /// ".skel" and ".atlas" from this base. Returns null if the anim or
    /// its data asset is missing.
    /// </summary>
    /// <summary>
    /// Reads the authored multi-skin list from a B_SpineSkinSet on the given
    /// GameObject. Returns null if no component is present or its list is
    /// empty, so the JSON omits the field entirely on objects that don't
    /// use multi-skin combine.
    /// </summary>
    private List<string> ResolveInitialSkins(GameObject go)
    {
        if (go == null) return null;
        B_SpineSkinSet set = go.GetComponent<B_SpineSkinSet>();
        if (set == null || set.InitialSkins == null || set.InitialSkins.Count == 0)
            return null;
        List<string> copy = new List<string>(set.InitialSkins.Count);
        for (int i = 0; i < set.InitialSkins.Count; i++)
        {
            string n = set.InitialSkins[i];
            if (!string.IsNullOrEmpty(n)) copy.Add(n);
        }
        return copy.Count > 0 ? copy : null;
    }

    private string ResolveSpineBasePath(Spine.Unity.SkeletonAnimation anim)
    {
        if (anim == null) return null;
        Spine.Unity.SkeletonDataAsset data = anim.SkeletonDataAsset;
        if (data == null || data.skeletonJSON == null) return null;

        string path = AssetDatabase.GetAssetPath(data.skeletonJSON);
        if (string.IsNullOrEmpty(path)) return null;

        // Strip common Spine data-file extensions.
        // Unity convention: "X.skel.bytes" or "X.json".
        if (path.EndsWith(".skel.bytes")) path = path.Substring(0, path.Length - ".skel.bytes".Length);
        else if (path.EndsWith(".json"))  path = path.Substring(0, path.Length - ".json".Length);
        else
        {
            // Fallback: strip last extension.
            int dot = path.LastIndexOf('.');
            if (dot >= 0) path = path.Substring(0, dot);
        }

        // Apply the same strip/prepend dance as ResolveAssetPath so the
        // output prefix convention stays consistent.
        if (!string.IsNullOrEmpty(assetRoot) && path.StartsWith(assetRoot))
            path = path.Substring(assetRoot.Length);
        path = path.Replace("\\", "/");
        if (!string.IsNullOrEmpty(outputAssetPrefix))
            path = outputAssetPrefix + path;

        return path;
    }

    private static string EmptyToNull(string s) => string.IsNullOrEmpty(s) ? null : s;

    private static string CamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }

    // ============================================================
    //  JSON DTOs (mirror the LibGDX schema)
    // ============================================================

    private class LevelJson
    {
        public int schemaVersion;
        public string levelId;
        public string title;
        public string description;
        public string assetPathPrefix;
        public string defaultHintMessageKey;
        public float timeLimit;
        public string timeUpTargetId;
        public string timeUpStateId;
        public ViewportJson viewport;
        public List<InteractableJson> interactables;
        public List<GroupJson> groups;
        public List<QueueJson> queues;
        public List<StaticObjectJson> staticObjects;
        public List<DropZoneJson> dropZones;
        public List<ConditionJson> win;
        public List<ConditionJson> lose;
    }

    private class ViewportJson
    {
        public int virtualWidth;
        public int virtualHeight;
    }

    private class InteractableJson
    {
        public string objectId;
        public bool startHidden;
        public string visualMode;
        public Vec2Json position;
        [System.ComponentModel.DefaultValue(1f)] public float scale;
        public float rotation;
        public int sortOrder;
        public ColliderJson collider;
        public string spineBasePath;
        public List<string> initialSkins;
        public ObjectDataJson data;
        public List<DropZoneLocalJson> dropZones;
    }

    private class ObjectDataJson
    {
        public string initStateId;
        public string initSprite;
        public string initSpineAnim;
        // Always write initSpineLoop — LibGDX treats a missing bool as
        // false (its type default), but the authored default for idle
        // animations is true. Force-include so the loop intent is always
        // explicit on both sides.
        [Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include)]
        public bool initSpineLoop;
        public string initSfx;
        public List<ObjectStateJson> states;
    }

    private class ObjectStateJson
    {
        public string stateId;
        public string dragSprite;
        public string stateSprite;
        public string stateSpineAnim;
        public bool stateSpineLoop;
        public string stateSfx;
        public string trigger;
        public string requiredZoneId;
        // Milestone counter. 0 = require ALL requirements (omitted from JSON).
        // >0 = fire when at least this many requirements are met.
        [System.ComponentModel.DefaultValue(0)] public int requiredCount;
        public List<StateRequirementJson> requirements;
        public List<StateActionJson> actions;
        public string successMessageKey;
        public string failMessageKey;
        public string hintMessageKey;
        public string onStartHook;
        public bool repeatable;
    }

    private class StateRequirementJson
    {
        public string objectId;
        public string stateId;
        public bool requireNotDone;
        // Mandatory gate: must be met regardless of requiredCount; doesn't
        // count toward the milestone. Omitted from JSON when false.
        public bool gate;
    }

    private class StateActionJson
    {
        public string type;
        public bool runInParallel;

        // Match StateAction.duration's C# default (0.4f) so the exporter's
        // DefaultValueHandling.Ignore drops only fields explicitly left at
        // 0.4 — values like 0 (no wait) or 1.5 still serialize, and the
        // importer's `?? 0.4f` fallback handles the dropped-default case.
        [System.ComponentModel.DefaultValue(0.4f)]
        public float duration;

        // MoveTo
        public string moveTargetObjectId;
        public string moveTargetZoneId;
        public Vec2Json moveTargetPosition;
        // Z rotation (degrees) of the literal-position anchor. Only written
        // for the anchor fallback (object/zone refs carry their own rotation
        // via their transform on import). Nullable so the field is omitted
        // for every action / MoveTo that doesn't fall back to an anchor.
        public float? moveTargetRotation;
        public string ease;
        public bool rotateToMatchTarget;

        // Disappear
        public bool? fadeOut;
        public bool? destroyOnDisappear;

        // Appear
        public bool? fadeIn;

        // DoAnimation
        public string spineAnim;
        public bool spineLoop;

        // Shared — optional target override for MoveTo/Disappear/Appear/DoAnimation
        public string actionTargetId;

        // ActivateState
        public string activateTargetObjectId;
        public string activateStateId;
        public List<StateRequirementJson> chainGuards;

        // AdvanceQueue
        public string queueTargetId;
        public string queueServeStateId;

        // PlaySFX — asset-relative path (audio is not auto-restored on import)
        public string sfxClip;

        // SkinChange
        public string skinTargetObjectId;
        public string skinName;
        public string skinOp;

        // ScaleTo — uniform target scale (1 = original). Nullable so the
        // field is omitted from every action type that ISN'T ScaleTo. (If
        // it were a plain float, every action would carry a redundant
        // "scaleTarget": 0.0 from C#'s zero-init on existing scenes.)
        public float? scaleTarget;

        // AttachToBone — id of the spine object whose bone the subject follows,
        // the bone name, and whether to keep the subject's offset at attach
        // time. Nullable keepBoneOffset so it only emits for AttachToBone.
        public string boneSourceObjectId;
        public string boneName;
        public bool? keepBoneOffset;
    }

    private class ColliderJson
    {
        public string type;
        public float width;
        public float height;
        public float radius;
        public float offsetX;
        public float offsetY;
    }

    private class GroupJson
    {
        public Vec2Json position;
        public int sortOrder;
        public string pickMode;
        public string visualMode;
        public ColliderJson collider;
        public List<string> initialSkins;
        public ObjectDataJson data;
        public List<GroupMemberJson> members;
    }

    private class GroupMemberJson
    {
        // For tail followers only: if set, this entry references an existing
        // top-level interactable by id instead of spawning a fresh child GO
        // inside the queue. Members never use this (they're always queue
        // children). Null/empty for anonymous (sprite/spine-only) followers.
        public string objectIdRef;
        public string sprite;
        public string spineBasePath;
        public Vec2Json position;
        public int sortOrder;
    }

    private class QueueJson
    {
        public string queueId;
        public Vec2Json position;
        public int sortOrder;
        public string visualMode;
        public ColliderJson collider;
        public List<string> initialSkins;
        public ObjectDataJson data;
        public List<GroupMemberJson> members;
        // Optional. Followers that trail the line and shift up with every
        // serve but never get served. Follower i sits at slots[members.Count + i]
        // at runtime, so as members shrink the followers walk up too.
        public List<GroupMemberJson> tailFollowers;
        public List<Vec2Json> slots;
        public float shiftDuration;
        public string shiftEase;
        public string queueEmptyTargetId;
        public string queueEmptyStateId;
    }

    private class StaticObjectJson
    {
        public string objectId;
        public bool startHidden;
        public string visualMode;
        public string sprite;
        public string spineBasePath;
        public string initSpineAnim;
        // Always write — see note on ObjectDataJson.initSpineLoop.
        [Newtonsoft.Json.JsonProperty(DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Include)]
        public bool initSpineLoop;
        public List<string> initialSkins;
        public Vec2Json position;
        [System.ComponentModel.DefaultValue(1f)] public float scale;
        public float rotation;
        public int sortOrder;
        public ColliderJson collider;
        public bool blocks;
        public List<DropZoneLocalJson> dropZones;
    }

    private class DropZoneJson
    {
        public string zoneId;
        public int sortOrder;
        public Vec2Json position;
        public Vec2Json size;
        public string shape;
    }

    private class DropZoneLocalJson
    {
        public string zoneId;
        public int sortOrder;
        public Vec2Json localOffset;
        public Vec2Json size;
        public string shape;
    }

    private class Vec2Json
    {
        public float x;
        public float y;
    }

    private class ConditionJson
    {
        public string type;
        public string objectId;
        public string stateId;
    }
}
