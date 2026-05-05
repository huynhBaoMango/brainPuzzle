using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dedicated editor window for level metadata. Auto-finds (or creates) the
/// B_LevelConfig component in the scene so designers never have to hunt for
/// it in the hierarchy. Includes the level strings editor for the LibGDX
/// string table export.
/// Open via <b>Tools &gt; Puzzle &gt; Level Config</b>.
/// </summary>
public class LevelConfigWindow : EditorWindow
{
    private Vector2 scroll;
    private bool stringsFoldout = true;
    private bool conditionsFoldout = true;
    private bool sceneObjectsFoldout = true;
    private bool sceneInteractablesFoldout = true;
    private bool sceneGroupsFoldout = true;
    private bool sceneQueuesFoldout = true;
    private bool sceneStaticsFoldout = true;
    private bool sceneZonesFoldout = true;
    private readonly HashSet<string> expandedObjectIds = new HashSet<string>();
    private SerializedObject so;
    private B_LevelConfig cached;

    [MenuItem("Tools/Puzzle/Level Config")]
    public static void Open()
    {
        var w = GetWindow<LevelConfigWindow>("Level Config");
        w.minSize = new Vector2(480f, 400f);
        w.Show();
    }

    private void OnEnable()
    {
        EditorSceneManager.sceneOpened += OnSceneChanged;
        EditorSceneManager.sceneClosed += OnSceneClosed;
    }

    private void OnDisable()
    {
        EditorSceneManager.sceneOpened -= OnSceneChanged;
        EditorSceneManager.sceneClosed -= OnSceneClosed;
    }

    private void OnSceneChanged(Scene _, OpenSceneMode __)
    {
        cached = null;
        so = null;
        Repaint();
    }

    private void OnSceneClosed(Scene _)
    {
        cached = null;
        so = null;
        Repaint();
    }

    // ============================================================
    //  FIND / CREATE
    // ============================================================

    private B_LevelConfig FindConfig()
    {
        return Object.FindAnyObjectByType<B_LevelConfig>();
    }

    private B_LevelConfig FindOrCreateConfig()
    {
        B_LevelConfig cfg = FindConfig();
        if (cfg != null) return cfg;

        GameObject go = new GameObject("_LevelConfig");
        Undo.RegisterCreatedObjectUndo(go, "Create LevelConfig");
        cfg = go.AddComponent<B_LevelConfig>();
        Selection.activeGameObject = go;
        return cfg;
    }

    private void RefreshSerializedObject()
    {
        B_LevelConfig cfg = FindConfig();

        // If the config was destroyed (scene reload, user deleted the GO),
        // Unity's == treats both cached and cfg as null, so the old
        // "cfg != cached" comparison would fail to invalidate the cache.
        // Explicitly drop the cache when there is no live cfg.
        if (cfg == null)
        {
            cached = null;
            so = null;
            return;
        }

        if (cached != cfg || so == null || so.targetObject == null)
        {
            cached = cfg;
            so = new SerializedObject(cfg);
        }
        else
        {
            so.Update();
        }
    }

    // ============================================================
    //  GUI
    // ============================================================

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        // Title
        EditorGUILayout.LabelField("Level Config", EditorStyles.boldLabel);

        RefreshSerializedObject();

        if (so == null)
        {
            EditorGUILayout.HelpBox(
                "No B_LevelConfig found in the current scene.",
                MessageType.Info);

            if (GUILayout.Button("Create Level Config", GUILayout.Height(28f)))
            {
                FindOrCreateConfig();
                RefreshSerializedObject();
            }

            EditorGUILayout.EndScrollView();
            return;
        }

        // Ping button
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Select in Hierarchy", GUILayout.Width(160f)))
            {
                if (cached != null) Selection.activeGameObject = cached.gameObject;
            }
        }

        EditorGUILayout.Space(4f);

        // ---- Identity ----
        DrawSection("Identity");
        EditorGUILayout.PropertyField(so.FindProperty("levelId"));
        SerializedProperty titleProp = so.FindProperty("title");
        EditorGUILayout.PropertyField(titleProp, new GUIContent("Title (String Key)"));
        DrawKeyPreview(titleProp != null ? titleProp.stringValue : null);
        SerializedProperty descProp = so.FindProperty("description");
        EditorGUILayout.PropertyField(descProp, new GUIContent("Description (String Key)"));
        DrawKeyPreview(descProp != null ? descProp.stringValue : null);

        EditorGUILayout.Space(6f);

        // ---- Virtual Viewport ----
        DrawSection("Virtual Viewport");
        EditorGUILayout.PropertyField(so.FindProperty("virtualWidth"));
        EditorGUILayout.PropertyField(so.FindProperty("virtualHeight"));
        EditorGUILayout.PropertyField(so.FindProperty("levelCamera"));
        EditorGUILayout.PropertyField(so.FindProperty("pixelsPerUnit"));

        EditorGUILayout.Space(6f);

        // ---- Outcome Conditions ----
        conditionsFoldout = EditorGUILayout.Foldout(conditionsFoldout, "Outcome Conditions", true, EditorStyles.foldoutHeader);
        if (conditionsFoldout)
        {
            EditorGUILayout.PropertyField(so.FindProperty("winConditions"), true);
            EditorGUILayout.PropertyField(so.FindProperty("loseConditions"), true);
        }

        EditorGUILayout.Space(6f);

        // ---- Hints ----
        DrawSection("Hints");
        EditorGUILayout.PropertyField(so.FindProperty("defaultHintMessageKey"));

        EditorGUILayout.Space(6f);

        // ---- Level Strings ----
        DrawStringsSection();

        EditorGUILayout.Space(6f);

        // ---- Scene Objects (compact overview + inline edit) ----
        DrawSceneObjectsSection();

        so.ApplyModifiedProperties();

        EditorGUILayout.EndScrollView();
    }

    private void DrawSection(string title)
    {
        Rect rect = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.22f, 0.28f));
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = new Color(0.6f, 0.85f, 1f);
        style.alignment = TextAnchor.MiddleLeft;
        rect.x += 6f;
        EditorGUI.LabelField(rect, title, style);
    }

    // ============================================================
    //  LEVEL STRINGS
    // ============================================================

    private void DrawStringsSection()
    {
        stringsFoldout = EditorGUILayout.Foldout(stringsFoldout, "Level Strings", true, EditorStyles.foldoutHeader);
        if (!stringsFoldout) return;

        if (cached.strings == null)
            cached.strings = new List<LevelString>();

        // Collect missing keys + bulk auto-fill buttons
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Collect Missing Keys from Scene"))
            {
                Undo.RecordObject(cached, "Collect String Keys");
                int added = CollectMissingKeys(cached);
                EditorUtility.SetDirty(cached);
                if (added > 0)
                    Debug.Log($"[LevelConfig] Added {added} new string key(s).");
                else
                    Debug.Log("[LevelConfig] No new keys found — all message keys already in the table.");
            }

            if (GUILayout.Button("Auto-fill EN from VN"))
                BulkAutoFillEnFromVn();
        }

        EditorGUILayout.HelpBox(
            "Auto-fill uses the public Google Translate endpoint. Editor-only — review results before shipping.",
            MessageType.None);

        if (cached.strings.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No strings yet. Set successMessageKey / failMessageKey on states, then click \"Collect Missing Keys\".",
                MessageType.Info);
            return;
        }

        // Table header
        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Key", EditorStyles.miniBoldLabel, GUILayout.Width(140f));
            GUILayout.Label("EN", EditorStyles.miniBoldLabel);
            GUILayout.Label("VN", EditorStyles.miniBoldLabel);
            GUILayout.Space(22f);
        }

        // Rows
        int deleteIdx = -1;
        for (int i = 0; i < cached.strings.Count; i++)
        {
            LevelString entry = cached.strings[i];
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                string newKey = EditorGUILayout.TextField(entry.key, GUILayout.Width(140f));
                string newEn = EditorGUILayout.TextField(entry.en);
                string newVn = EditorGUILayout.TextField(entry.vn);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(cached, "Edit Level String");
                    entry.key = newKey;
                    entry.en = newEn;
                    entry.vn = newVn;
                    EditorUtility.SetDirty(cached);
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(entry.vn)))
                {
                    if (GUILayout.Button(new GUIContent("→EN", "Translate VN to EN (Google)"),
                                         GUILayout.Width(36f)))
                        TranslateRow(entry);
                }

                if (GUILayout.Button("×", GUILayout.Width(20f)))
                    deleteIdx = i;
            }
        }

        if (deleteIdx >= 0)
        {
            Undo.RecordObject(cached, "Delete Level String");
            cached.strings.RemoveAt(deleteIdx);
            EditorUtility.SetDirty(cached);
        }

        // Add row button
        if (GUILayout.Button("+ Add String", GUILayout.Width(100f)))
        {
            Undo.RecordObject(cached, "Add Level String");
            cached.strings.Add(new LevelString());
            EditorUtility.SetDirty(cached);
        }
    }

    /// <summary>
    /// Translate a single row's VN field into EN via the public Google
    /// Translate endpoint. Captures the entry by reference so the callback
    /// writes back to the same row even if the user reorders the list.
    /// </summary>
    private void TranslateRow(LevelString entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.vn)) return;
        B_LevelConfig target = cached;
        EditorTranslator.TranslateAsync(entry.vn, "vi", "en", (result, err) =>
        {
            if (err != null)
            {
                Debug.LogWarning($"[LevelConfig] {err}");
                return;
            }
            if (target == null) return; // config was destroyed mid-flight
            Undo.RecordObject(target, "Translate Level String");
            entry.en = result;
            EditorUtility.SetDirty(target);
            Repaint();
        });
    }

    /// <summary>
    /// Fires a translate request for every row that has VN but no EN.
    /// Shows a progress bar that clears once all in-flight requests settle.
    /// </summary>
    private void BulkAutoFillEnFromVn()
    {
        if (cached == null || cached.strings == null) return;

        List<LevelString> targets = new List<LevelString>();
        foreach (LevelString s in cached.strings)
            if (!string.IsNullOrEmpty(s.vn) && string.IsNullOrEmpty(s.en))
                targets.Add(s);

        if (targets.Count == 0)
        {
            Debug.Log("[LevelConfig] Nothing to translate — every row with VN already has EN.");
            return;
        }

        int total = targets.Count;
        int[] remaining = { total };
        B_LevelConfig cfg = cached;
        EditorUtility.DisplayProgressBar("Auto-translate", $"0 / {total}", 0f);

        foreach (LevelString entry in targets)
        {
            EditorTranslator.TranslateAsync(entry.vn, "vi", "en", (result, err) =>
            {
                if (err != null)
                    Debug.LogWarning($"[LevelConfig] {err} (key={entry.key})");
                else if (cfg != null)
                {
                    Undo.RecordObject(cfg, "Auto-fill EN");
                    entry.en = result;
                    EditorUtility.SetDirty(cfg);
                }

                remaining[0]--;
                int done = total - remaining[0];
                if (remaining[0] > 0)
                    EditorUtility.DisplayProgressBar("Auto-translate",
                        $"{done} / {total}", done / (float)total);
                else
                    EditorUtility.ClearProgressBar();
                Repaint();
            });
        }
    }

    // ============================================================
    //  KEY COLLECTION
    // ============================================================

    // ============================================================
    //  SCENE OBJECTS — compact overview with inline edit
    // ============================================================

    private void DrawSceneObjectsSection()
    {
        sceneObjectsFoldout = EditorGUILayout.Foldout(sceneObjectsFoldout, "Scene Objects", true, EditorStyles.foldoutHeader);
        if (!sceneObjectsFoldout) return;

        var interactables = Object.FindObjectsByType<B_InteractableObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        var groups = Object.FindObjectsByType<B_InteractableGroup>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        var queues = Object.FindObjectsByType<B_InteractableQueue>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        var statics = Object.FindObjectsByType<B_StaticObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        var zones = Object.FindObjectsByType<B_DropZone>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);

        // Quick summary line.
        EditorGUILayout.HelpBox(
            $"{interactables.Length} interactables · {groups.Length} groups · {queues.Length} queues · {statics.Length} statics · {zones.Length} drop zones",
            MessageType.None);

        EditorGUILayout.Space(2f);
        DrawInteractablesGroup(interactables);
        EditorGUILayout.Space(2f);
        DrawGroupsGroup(groups);
        EditorGUILayout.Space(2f);
        DrawQueuesGroup(queues);
        EditorGUILayout.Space(2f);
        DrawStaticsGroup(statics);
        EditorGUILayout.Space(2f);
        DrawZonesGroup(zones);
    }

    private void DrawQueuesGroup(B_InteractableQueue[] qs)
    {
        sceneQueuesFoldout = EditorGUILayout.Foldout(sceneQueuesFoldout,
            $"Interactable Queues ({qs.Length})", true, EditorStyles.foldoutHeader);
        if (!sceneQueuesFoldout) return;

        foreach (B_InteractableQueue q in qs)
        {
            if (q == null) continue;
            DrawQueueRow(q);
        }
    }

    private void DrawQueueRow(B_InteractableQueue q)
    {
        string uniqueKey = "queue:" + q.GetInstanceID();
        bool expanded = expandedObjectIds.Contains(uniqueKey);

        using (new EditorGUILayout.HorizontalScope("box"))
        {
            bool newExpanded = EditorGUILayout.Foldout(expanded, GUIContent.none, true, EditorStyles.foldout);
            if (newExpanded != expanded)
            {
                if (newExpanded) expandedObjectIds.Add(uniqueKey);
                else expandedObjectIds.Remove(uniqueKey);
            }
            expanded = newExpanded;

            // Editable queueId.
            SerializedObject sObj = new SerializedObject(q);
            SerializedProperty idProp = sObj.FindProperty("queueId");
            EditorGUI.BeginChangeCheck();
            string newId = EditorGUILayout.TextField(idProp.stringValue, GUILayout.MinWidth(100f));
            if (EditorGUI.EndChangeCheck())
            {
                idProp.stringValue = newId;
                sObj.ApplyModifiedProperties();
            }

            int memberCount = q.Members?.Count ?? 0;
            int slotCount = q.Slots?.Count ?? 0;
            int stateCount = q.Data?.states?.Count ?? 0;
            GUILayout.Label($"{memberCount} members · {slotCount} slots · {stateCount} state(s)",
                            EditorStyles.miniLabel);

            if (GUILayout.Button("⦿", GUILayout.Width(24f)))
                Selection.activeGameObject = q.gameObject;
        }

        if (!expanded) return;

        EditorGUI.indentLevel++;
        if (q.Data?.states != null)
        {
            DrawStateListHeader();
            SerializedObject so = new SerializedObject(q);
            SerializedProperty statesProp = so.FindProperty("data.states");
            if (statesProp != null && statesProp.isArray)
            {
                for (int i = 0; i < statesProp.arraySize; i++)
                    DrawStateRow(statesProp.GetArrayElementAtIndex(i));
                so.ApplyModifiedProperties();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No ObjectData / states.", MessageType.None);
        }
        EditorGUI.indentLevel--;
    }

    private void DrawInteractablesGroup(B_InteractableObject[] objs)
    {
        sceneInteractablesFoldout = EditorGUILayout.Foldout(sceneInteractablesFoldout,
            $"Interactables ({objs.Length})", true, EditorStyles.foldoutHeader);
        if (!sceneInteractablesFoldout) return;

        foreach (B_InteractableObject obj in objs)
        {
            if (obj == null) continue;
            DrawInteractableRow(obj);
        }
    }

    private void DrawInteractableRow(B_InteractableObject obj)
    {
        string uniqueKey = "io:" + obj.GetInstanceID();
        bool expanded = expandedObjectIds.Contains(uniqueKey);

        using (new EditorGUILayout.HorizontalScope("box"))
        {
            bool newExpanded = EditorGUILayout.Foldout(expanded, GUIContent.none, true, EditorStyles.foldout);
            if (newExpanded != expanded)
            {
                if (newExpanded) expandedObjectIds.Add(uniqueKey);
                else expandedObjectIds.Remove(uniqueKey);
            }
            expanded = newExpanded;

            // Compact id editor + state count.
            SerializedObject sObj = new SerializedObject(obj);
            SerializedProperty idProp = sObj.FindProperty("objectId");
            EditorGUI.BeginChangeCheck();
            string newId = EditorGUILayout.TextField(idProp.stringValue, GUILayout.MinWidth(100f));
            if (EditorGUI.EndChangeCheck())
            {
                idProp.stringValue = newId;
                sObj.ApplyModifiedProperties();
            }

            int stateCount = obj.Data?.states?.Count ?? 0;
            GUILayout.Label($"{stateCount} state(s)", EditorStyles.miniLabel, GUILayout.Width(80f));

            if (GUILayout.Button("⦿", GUILayout.Width(24f)))
                Selection.activeGameObject = obj.gameObject;
        }

        if (!expanded) return;

        // Indented state list.
        EditorGUI.indentLevel++;
        if (obj.Data?.states != null)
        {
            DrawStateListHeader();
            SerializedObject so = new SerializedObject(obj);
            SerializedProperty statesProp = so.FindProperty("data.states");
            if (statesProp != null && statesProp.isArray)
            {
                for (int i = 0; i < statesProp.arraySize; i++)
                {
                    SerializedProperty stateProp = statesProp.GetArrayElementAtIndex(i);
                    DrawStateRow(stateProp);
                }
                so.ApplyModifiedProperties();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No ObjectData / states.", MessageType.None);
        }
        EditorGUI.indentLevel--;
    }

    private void DrawGroupsGroup(B_InteractableGroup[] grps)
    {
        sceneGroupsFoldout = EditorGUILayout.Foldout(sceneGroupsFoldout,
            $"Interactable Groups ({grps.Length})", true, EditorStyles.foldoutHeader);
        if (!sceneGroupsFoldout) return;

        foreach (B_InteractableGroup grp in grps)
        {
            if (grp == null) continue;
            DrawGroupRow(grp);
        }
    }

    private void DrawGroupRow(B_InteractableGroup grp)
    {
        string uniqueKey = "grp:" + grp.GetInstanceID();
        bool expanded = expandedObjectIds.Contains(uniqueKey);

        using (new EditorGUILayout.HorizontalScope("box"))
        {
            bool newExpanded = EditorGUILayout.Foldout(expanded, GUIContent.none, true, EditorStyles.foldout);
            if (newExpanded != expanded)
            {
                if (newExpanded) expandedObjectIds.Add(uniqueKey);
                else expandedObjectIds.Remove(uniqueKey);
            }
            expanded = newExpanded;

            GUILayout.Label(grp.gameObject.name, GUILayout.MinWidth(100f));
            int memberCount = grp.Members?.Count ?? 0;
            int stateCount = grp.Data?.states?.Count ?? 0;
            GUILayout.Label($"{memberCount} members · {stateCount} state(s)", EditorStyles.miniLabel);

            if (GUILayout.Button("⦿", GUILayout.Width(24f)))
                Selection.activeGameObject = grp.gameObject;
        }

        if (!expanded) return;

        EditorGUI.indentLevel++;
        if (grp.Data?.states != null)
        {
            DrawStateListHeader();
            SerializedObject so = new SerializedObject(grp);
            SerializedProperty statesProp = so.FindProperty("data.states");
            if (statesProp != null && statesProp.isArray)
            {
                for (int i = 0; i < statesProp.arraySize; i++)
                    DrawStateRow(statesProp.GetArrayElementAtIndex(i));
                so.ApplyModifiedProperties();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No ObjectData / states.", MessageType.None);
        }
        EditorGUI.indentLevel--;
    }

    private void DrawStaticsGroup(B_StaticObject[] statics)
    {
        sceneStaticsFoldout = EditorGUILayout.Foldout(sceneStaticsFoldout,
            $"Statics ({statics.Length})", true, EditorStyles.foldoutHeader);
        if (!sceneStaticsFoldout) return;

        foreach (B_StaticObject s in statics)
        {
            if (s == null) continue;
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                SerializedObject sObj = new SerializedObject(s);
                SerializedProperty idProp = sObj.FindProperty("objectId");
                EditorGUI.BeginChangeCheck();
                string newId = EditorGUILayout.TextField(idProp.stringValue, GUILayout.MinWidth(100f));
                if (EditorGUI.EndChangeCheck())
                {
                    idProp.stringValue = newId;
                    sObj.ApplyModifiedProperties();
                }

                GUILayout.Label($"Order {s.GetSortOrder()}", EditorStyles.miniLabel, GUILayout.Width(80f));
                GUILayout.Label(s.VisualMode.ToString(), EditorStyles.miniLabel, GUILayout.Width(50f));

                if (GUILayout.Button("⦿", GUILayout.Width(24f)))
                    Selection.activeGameObject = s.gameObject;
            }
        }
    }

    private void DrawZonesGroup(B_DropZone[] zones)
    {
        sceneZonesFoldout = EditorGUILayout.Foldout(sceneZonesFoldout,
            $"Drop Zones ({zones.Length})", true, EditorStyles.foldoutHeader);
        if (!sceneZonesFoldout) return;

        foreach (B_DropZone z in zones)
        {
            if (z == null) continue;
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                SerializedObject sObj = new SerializedObject(z);
                SerializedProperty idProp = sObj.FindProperty("zoneId");
                EditorGUI.BeginChangeCheck();
                string newId = EditorGUILayout.TextField(idProp.stringValue, GUILayout.MinWidth(100f));
                if (EditorGUI.EndChangeCheck())
                {
                    idProp.stringValue = newId;
                    sObj.ApplyModifiedProperties();
                }

                GUILayout.Label($"Order {z.SortOrder}", EditorStyles.miniLabel, GUILayout.Width(80f));

                if (GUILayout.Button("⦿", GUILayout.Width(24f)))
                    Selection.activeGameObject = z.gameObject;
            }
        }
    }

    // ---- State list row helpers ----

    private void DrawStateListHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("State Id", EditorStyles.miniBoldLabel, GUILayout.MinWidth(80f));
            GUILayout.Label("Trigger", EditorStyles.miniBoldLabel, GUILayout.Width(85f));
            GUILayout.Label("Success Key", EditorStyles.miniBoldLabel, GUILayout.MinWidth(90f));
            GUILayout.Label("Fail Key", EditorStyles.miniBoldLabel, GUILayout.MinWidth(90f));
            GUILayout.Label("Hint Key", EditorStyles.miniBoldLabel, GUILayout.MinWidth(90f));
        }
    }

    private void DrawStateRow(SerializedProperty stateProp)
    {
        SerializedProperty idProp = stateProp.FindPropertyRelative("stateId");
        SerializedProperty triggerProp = stateProp.FindPropertyRelative("trigger");
        SerializedProperty successProp = stateProp.FindPropertyRelative("successMessageKey");
        SerializedProperty failProp = stateProp.FindPropertyRelative("failMessageKey");
        SerializedProperty hintProp = stateProp.FindPropertyRelative("hintMessageKey");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (idProp != null)
                idProp.stringValue = EditorGUILayout.TextField(idProp.stringValue, GUILayout.MinWidth(80f));
            if (triggerProp != null)
                EditorGUILayout.PropertyField(triggerProp, GUIContent.none, GUILayout.Width(85f));
            if (successProp != null)
                successProp.stringValue = EditorGUILayout.TextField(successProp.stringValue, GUILayout.MinWidth(90f));
            if (failProp != null)
                failProp.stringValue = EditorGUILayout.TextField(failProp.stringValue, GUILayout.MinWidth(90f));
            if (hintProp != null)
                hintProp.stringValue = EditorGUILayout.TextField(hintProp.stringValue, GUILayout.MinWidth(90f));
        }
    }

    /// <summary>
    /// Shows a small preview under a key-style field: "EN: ... | VN: ..."
    /// resolved from the current strings table, or a hint if the key is
    /// missing. Pure UI — no data modification.
    /// </summary>
    private void DrawKeyPreview(string key)
    {
        if (cached == null) return;
        if (string.IsNullOrEmpty(key))
        {
            EditorGUILayout.LabelField(" ", "(no key — add one and it will show a preview)", EditorStyles.miniLabel);
            return;
        }
        LevelString entry = null;
        if (cached.strings != null)
            foreach (LevelString ls in cached.strings)
                if (ls != null && ls.key == key) { entry = ls; break; }

        if (entry == null)
        {
            EditorGUILayout.LabelField(" ", $"⚠ key \"{key}\" not in strings table — click \"Collect Missing Keys\"", EditorStyles.miniLabel);
            return;
        }
        string en = string.IsNullOrEmpty(entry.en) ? "(empty)" : entry.en;
        string vn = string.IsNullOrEmpty(entry.vn) ? "(empty)" : entry.vn;
        EditorGUILayout.LabelField(" ", $"EN: {en}    |    VN: {vn}", EditorStyles.miniLabel);
    }

    private int CollectMissingKeys(B_LevelConfig config)
    {
        HashSet<string> existing = new HashSet<string>();
        foreach (LevelString ls in config.strings)
        {
            if (!string.IsNullOrEmpty(ls.key))
                existing.Add(ls.key);
        }

        HashSet<string> found = new HashSet<string>();

        // Level title is also a string key.
        if (!string.IsNullOrEmpty(config.title))
            found.Add(config.title);

        // Level description is also a string key.
        if (!string.IsNullOrEmpty(config.description))
            found.Add(config.description);

        foreach (B_InteractableObject obj in Object.FindObjectsByType<B_InteractableObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (obj.Data?.states == null) continue;
            foreach (ObjectState s in obj.Data.states)
            {
                if (!string.IsNullOrEmpty(s.successMessageKey)) found.Add(s.successMessageKey);
                if (!string.IsNullOrEmpty(s.failMessageKey)) found.Add(s.failMessageKey);
                if (!string.IsNullOrEmpty(s.hintMessageKey)) found.Add(s.hintMessageKey);
            }
        }

        foreach (B_InteractableGroup grp in Object.FindObjectsByType<B_InteractableGroup>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (grp.Data?.states == null) continue;
            foreach (ObjectState s in grp.Data.states)
            {
                if (!string.IsNullOrEmpty(s.successMessageKey)) found.Add(s.successMessageKey);
                if (!string.IsNullOrEmpty(s.failMessageKey)) found.Add(s.failMessageKey);
                if (!string.IsNullOrEmpty(s.hintMessageKey)) found.Add(s.hintMessageKey);
            }
        }

        foreach (B_InteractableQueue q in Object.FindObjectsByType<B_InteractableQueue>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (q.Data?.states == null) continue;
            foreach (ObjectState s in q.Data.states)
            {
                if (!string.IsNullOrEmpty(s.successMessageKey)) found.Add(s.successMessageKey);
                if (!string.IsNullOrEmpty(s.failMessageKey)) found.Add(s.failMessageKey);
                if (!string.IsNullOrEmpty(s.hintMessageKey)) found.Add(s.hintMessageKey);
            }
        }

        // Level-wide fallback hint key.
        if (!string.IsNullOrEmpty(config.defaultHintMessageKey))
            found.Add(config.defaultHintMessageKey);

        int added = 0;
        foreach (string key in found)
        {
            if (existing.Contains(key)) continue;
            config.strings.Add(new LevelString { key = key, en = "", vn = "" });
            added++;
        }
        return added;
    }
}
