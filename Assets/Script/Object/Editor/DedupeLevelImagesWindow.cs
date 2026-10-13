using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor window that finds "dead" image assets — files inside
/// <c>Assets/LevelAssets/lv{N}/images/</c> whose GUID does NOT appear in the
/// corresponding <c>Assets/Scenes/lv{N}.unity</c> scene.
///
/// Instead of deleting, dead files are moved (via <see cref="AssetDatabase.MoveAsset"/>)
/// into <c>Assets/DEAD_ASSET/lv{N}/images/…</c>, preserving the relative sub-
/// path so a designer can restore individually or all at once.
///
/// Access: <b>Tools ▸ Puzzle ▸ Dedupe Level Images</b>.
/// </summary>
public class DedupeLevelImagesWindow : EditorWindow
{
    private const string LevelAssetsRoot = "Assets/LevelAssets";
    private const string ScenesRoot      = "Assets/Scenes";
    private const string DeadAssetRoot   = "Assets/DEAD_ASSET";
    private const string QuarantineLog   = "Assets/DEAD_ASSET/quarantine_log.txt";

    private static readonly HashSet<string> ImageExtensions = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".psd", ".tga", ".gif", ".bmp"
    };

    // -------- state --------
    private List<LevelReport> reports = new List<LevelReport>();
    private Vector2 scroll;
    private bool showKept = false;
    private string statusMsg = "Click 'Scan' to check every lv{N}/images vs its scene.";
    private MessageType statusType = MessageType.Info;

    private class LevelReport
    {
        public string levelName;
        public string scenePath;
        public bool   skipped;
        public string skipReason;
        public bool   expanded = true;
        public List<ImageRecord> dead = new List<ImageRecord>();
        public List<ImageRecord> kept = new List<ImageRecord>();
    }

    private class ImageRecord
    {
        public string assetPath;   // full path (Assets/…)
        public string relPath;     // relative to the lv folder (e.g. images/foo.png)
        public string guid;
        public long   size;
        public int    refCount;
        public bool   selected = true;   // dead only: whether to move on next Move click
    }

    [MenuItem("Tools/Puzzle/Dedupe Level Images")]
    public static void Open()
    {
        var w = GetWindow<DedupeLevelImagesWindow>("Dedupe Level Images");
        w.minSize = new Vector2(640, 500);
    }

    // ============================================================
    //  UI
    // ============================================================

    private void OnGUI()
    {
        DrawHeader();

        EditorGUILayout.Space();
        DrawToolbar();

        if (!string.IsNullOrEmpty(statusMsg))
            EditorGUILayout.HelpBox(statusMsg, statusType);

        DrawSummary();

        EditorGUILayout.Space(4);
        scroll = EditorGUILayout.BeginScrollView(scroll,
            "box", GUILayout.ExpandHeight(true));
        foreach (var r in reports) DrawLevel(r);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        DrawActionButtons();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Dedupe Level Images", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "For every " + LevelAssetsRoot + "/lv{N}/images/*.{png,jpg,psd,…} this window " +
            "checks whether its GUID is referenced in " + ScenesRoot + "/lv{N}.unity.\n\n" +
            "Files with 0 refs are marked DEAD. Moving them puts them under " + DeadAssetRoot +
            "/lv{N}/images/… (no permanent deletion — you can Restore later).",
            MessageType.None);
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Scan", GUILayout.Width(100))) Scan();

        GUI.enabled = reports.Count > 0;
        if (GUILayout.Button("Select all DEAD", GUILayout.Width(120)))
            SetAllSelected(true);
        if (GUILayout.Button("Deselect all DEAD", GUILayout.Width(140)))
            SetAllSelected(false);
        if (GUILayout.Button("Expand all", GUILayout.Width(90)))
            SetAllExpanded(true);
        if (GUILayout.Button("Collapse all", GUILayout.Width(90)))
            SetAllExpanded(false);
        GUI.enabled = true;

        GUILayout.FlexibleSpace();
        showKept = GUILayout.Toggle(showKept, "Show KEPT rows", GUILayout.Width(140));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSummary()
    {
        int deadTotal = 0, deadSelected = 0, keptTotal = 0;
        long deadBytes = 0, selBytes = 0;
        foreach (var r in reports)
        {
            keptTotal += r.kept.Count;
            foreach (var d in r.dead)
            {
                deadTotal++;
                deadBytes += d.size;
                if (d.selected) { deadSelected++; selBytes += d.size; }
            }
        }
        EditorGUILayout.LabelField(
            $"Levels: {reports.Count}   KEPT: {keptTotal}   " +
            $"DEAD: {deadTotal} ({FormatSize(deadBytes)})   " +
            $"Selected: {deadSelected} ({FormatSize(selBytes)})",
            EditorStyles.boldLabel);
    }

    private void DrawLevel(LevelReport r)
    {
        // Header line with colored dot.
        Color prev = GUI.contentColor;
        Color tag = r.skipped
            ? new Color(0.6f, 0.6f, 0.6f)
            : (r.dead.Count > 0 ? new Color(1f, 0.75f, 0.2f) : new Color(0.5f, 0.9f, 0.5f));
        GUI.contentColor = tag;
        string header = r.skipped
            ? $"{r.levelName}   SKIPPED"
            : $"{r.levelName}   DEAD: {r.dead.Count}   KEPT: {r.kept.Count}";
        r.expanded = EditorGUILayout.Foldout(r.expanded, header, true);
        GUI.contentColor = prev;

        if (!r.expanded) return;
        EditorGUI.indentLevel++;

        if (r.skipped)
        {
            EditorGUILayout.LabelField("Reason: " + r.skipReason);
        }
        else
        {
            // Per-level select-all row
            if (r.dead.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select all in " + r.levelName, GUILayout.Width(180)))
                    r.dead.ForEach(d => d.selected = true);
                if (GUILayout.Button("Deselect all in " + r.levelName, GUILayout.Width(180)))
                    r.dead.ForEach(d => d.selected = false);
                EditorGUILayout.EndHorizontal();
            }

            foreach (var d in r.dead) DrawDeadRow(d);
            if (showKept) foreach (var k in r.kept) DrawKeptRow(k);
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(2);
    }

    private void DrawDeadRow(ImageRecord d)
    {
        EditorGUILayout.BeginHorizontal();
        d.selected = EditorGUILayout.ToggleLeft(
            d.relPath + "   " + FormatSize(d.size),
            d.selected, GUILayout.MinWidth(300));
        if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(50)))
        {
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(d.assetPath);
            if (obj != null) EditorGUIUtility.PingObject(obj);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawKeptRow(ImageRecord k)
    {
        EditorGUILayout.LabelField($"     {k.relPath}   refs={k.refCount}",
            EditorStyles.miniLabel);
    }

    private void DrawActionButtons()
    {
        int selected = reports.Sum(r => r.dead.Count(d => d.selected));
        long selectedBytes = reports.Sum(r => r.dead.Where(d => d.selected).Sum(d => d.size));

        EditorGUILayout.BeginHorizontal();

        GUI.enabled = selected > 0;
        GUI.backgroundColor = new Color(1f, 0.75f, 0.3f);
        if (GUILayout.Button(
            $"Move {selected} selected → {DeadAssetRoot}/  ({FormatSize(selectedBytes)})",
            GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog(
                "Move to DEAD_ASSET",
                $"Move {selected} file(s) into {DeadAssetRoot}/ for later recovery?\n\n" +
                "This is reversible — use 'Restore all' or move them back from the Project window.",
                "Move", "Cancel"))
                MoveSelectedToDeadAsset();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        if (GUILayout.Button("Restore all from " + DeadAssetRoot, GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog(
                "Restore",
                "Move every file under " + DeadAssetRoot + " back to LevelAssets/? " +
                "Their original relative paths are preserved.",
                "Restore", "Cancel"))
                RestoreAllFromDeadAsset();
        }

        GUI.backgroundColor = new Color(0.5f, 0.8f, 1f);
        if (GUILayout.Button("Repair DEAD_ASSET (merge dup folders)",
            GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog(
                "Repair",
                "Merge every '<name> N' duplicate folder inside " + DeadAssetRoot +
                " back into its base '<name>' folder. Use this to clean up the mess " +
                "left by the old batch-mode bug.",
                "Repair", "Cancel"))
                RepairDeadAssetDupFolders();
        }
        GUI.backgroundColor = Color.white;

        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("Empty " + DeadAssetRoot + " (permanent!)",
            GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog(
                "Empty DEAD_ASSET",
                "PERMANENTLY delete " + DeadAssetRoot + " and everything inside?\n\n" +
                "This cannot be undone from within Unity.",
                "Delete", "Cancel"))
                EmptyDeadAssetFolder();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    // ============================================================
    //  SCAN
    // ============================================================

    private void Scan()
    {
        reports.Clear();
        statusType = MessageType.Info;
        statusMsg = "Scanning…";
        Repaint();

        if (!AssetDatabase.IsValidFolder(LevelAssetsRoot))
        {
            statusType = MessageType.Error;
            statusMsg = "Not found: " + LevelAssetsRoot;
            return;
        }

        var lvDirs = Directory.GetDirectories(LevelAssetsRoot, "lv*",
            SearchOption.TopDirectoryOnly);
        Array.Sort(lvDirs, NaturalCompare);

        try
        {
            for (int i = 0; i < lvDirs.Length; i++)
            {
                string lvDir = lvDirs[i].Replace("\\", "/");
                string lvName = Path.GetFileName(lvDir);

                EditorUtility.DisplayProgressBar(
                    "Dedupe Level Images",
                    $"Scanning {lvName} ({i + 1}/{lvDirs.Length})",
                    (float)i / Mathf.Max(1, lvDirs.Length));

                string imagesDir = lvDir + "/images";
                if (!Directory.Exists(imagesDir)) continue;

                var report = new LevelReport { levelName = lvName };

                string scenePath = ScenesRoot + "/" + lvName + ".unity";
                if (!File.Exists(scenePath))
                {
                    report.skipped = true;
                    report.skipReason = "No scene at " + scenePath;
                    reports.Add(report);
                    continue;
                }
                report.scenePath = scenePath;

                string sceneText = File.ReadAllText(scenePath);

                var imgFiles = Directory.GetFiles(imagesDir, "*.*",
                    SearchOption.AllDirectories)
                    .Where(f => ImageExtensions.Contains(Path.GetExtension(f)))
                    .Select(f => f.Replace("\\", "/"));

                foreach (var img in imgFiles)
                {
                    string guid = AssetDatabase.AssetPathToGUID(img);
                    if (string.IsNullOrEmpty(guid)) continue;

                    long size = new FileInfo(img).Length;
                    int refCount = CountReferences(sceneText, guid);

                    var rec = new ImageRecord
                    {
                        assetPath = img,
                        relPath   = img.Substring(lvDir.Length + 1),
                        guid      = guid,
                        size      = size,
                        refCount  = refCount,
                    };
                    if (refCount == 0) report.dead.Add(rec);
                    else                report.kept.Add(rec);
                }

                report.dead.Sort((a, b) => string.Compare(a.relPath, b.relPath,
                    StringComparison.OrdinalIgnoreCase));
                report.kept.Sort((a, b) => string.Compare(a.relPath, b.relPath,
                    StringComparison.OrdinalIgnoreCase));

                reports.Add(report);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        int totalDead = reports.Sum(r => r.dead.Count);
        int totalKept = reports.Sum(r => r.kept.Count);
        long deadBytes = reports.Sum(r => r.dead.Sum(d => d.size));
        statusType = totalDead > 0 ? MessageType.Warning : MessageType.Info;
        statusMsg = $"Scan done. {totalDead} DEAD ({FormatSize(deadBytes)}) across " +
                    $"{reports.Count} level(s). {totalKept} kept.";
        Repaint();
    }

    /// <summary>Counts occurrences of <c>"guid: &lt;hex&gt;"</c> in a scene YAML.</summary>
    private static int CountReferences(string sceneText, string guid)
    {
        string needle = "guid: " + guid;
        int count = 0;
        int idx = 0;
        while ((idx = sceneText.IndexOf(needle, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    // ============================================================
    //  MOVE (quarantine)
    // ============================================================

    private void MoveSelectedToDeadAsset()
    {
        var log = new List<string>();
        int moved = 0, failed = 0;

        // ---- Gather jobs first ----
        var jobs = new List<(string src, string dest)>();
        foreach (var r in reports)
        {
            if (r.skipped) continue;
            foreach (var d in r.dead)
            {
                if (!d.selected) continue;
                string src = d.assetPath;
                // Strip "Assets/LevelAssets/" so we keep the lv{N}/images/… shape.
                string subPath = src.StartsWith(LevelAssetsRoot + "/")
                    ? src.Substring(LevelAssetsRoot.Length + 1)
                    : Path.GetFileName(src);
                jobs.Add((src, DeadAssetRoot + "/" + subPath));
            }
        }

        // ---- Phase 1: pre-create every destination folder OUTSIDE any batch ----
        // Doing this inside AssetDatabase.StartAssetEditing() causes
        // IsValidFolder() to return stale false for just-created folders, and
        // Unity then auto-suffixes duplicates ("images 1", "images 2", …) and
        // subsequent MoveAsset calls end up in the wrong folder or fail.
        EnsureFolderExists(DeadAssetRoot);
        var uniqueFolders = jobs
            .Select(j => Path.GetDirectoryName(j.dest).Replace("\\", "/"))
            .Distinct()
            .ToList();
        foreach (var f in uniqueFolders)
            EnsureFolderExists(f);

        // ---- Phase 2: move files one by one (no batch editing) ----
        for (int i = 0; i < jobs.Count; i++)
        {
            var (src, destPath) = jobs[i];

            EditorUtility.DisplayProgressBar(
                "Moving to DEAD_ASSET",
                $"{Path.GetFileName(src)}  ({i + 1}/{jobs.Count})",
                (float)i / Mathf.Max(1, jobs.Count));

            // Collision suffix (rare).
            string finalDest = destPath;
            int n = 1;
            while (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(finalDest) != null)
            {
                string ext = Path.GetExtension(destPath);
                string bare = destPath.Substring(0, destPath.Length - ext.Length);
                finalDest = $"{bare}_dup{n}{ext}";
                n++;
            }

            string err = AssetDatabase.MoveAsset(src, finalDest);
            if (string.IsNullOrEmpty(err))
            {
                moved++;
                log.Add(src + "  ->  " + finalDest);
            }
            else
            {
                failed++;
                log.Add("FAILED " + src + "  (" + err + ")");
            }
        }
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Append log so restores across sessions can trace what happened.
        var header = new[]
        {
            "",
            "=== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                "   moved=" + moved + " failed=" + failed + " ==="
        };
        try
        {
            EnsureFolderExists(DeadAssetRoot);
            File.AppendAllLines(QuarantineLog, header.Concat(log));
            AssetDatabase.ImportAsset(QuarantineLog);
        }
        catch (Exception e) { Debug.LogWarning("Could not write log: " + e.Message); }

        statusType = failed == 0 ? MessageType.Info : MessageType.Warning;
        statusMsg = $"Moved {moved} file(s) to {DeadAssetRoot}. " +
                    (failed > 0 ? failed + " failed. See Console." : "");

        Scan(); // refresh view
    }

    // ============================================================
    //  RESTORE
    // ============================================================

    private void RestoreAllFromDeadAsset()
    {
        if (!AssetDatabase.IsValidFolder(DeadAssetRoot))
        {
            statusMsg = DeadAssetRoot + " is empty (nothing to restore).";
            statusType = MessageType.Info;
            return;
        }

        int restored = 0, failed = 0;

        // Same two-phase pattern as MoveSelectedToDeadAsset: create all target
        // folders outside any batch, then move one by one. Batch editing here
        // would produce the "images 1"-suffix bug on freshly-created folders.
        var files = Directory.GetFiles(DeadAssetRoot, "*.*",
                SearchOption.AllDirectories)
            .Select(f => f.Replace("\\", "/"))
            .Where(f => !f.EndsWith(".meta") && f != QuarantineLog)
            .ToList();

        var jobs = files.Select(src =>
        {
            string rel = src.Substring(DeadAssetRoot.Length + 1);      // lv40/images/foo.png
            return (src, dest: LevelAssetsRoot + "/" + rel);
        }).ToList();

        foreach (var f in jobs
            .Select(j => Path.GetDirectoryName(j.dest).Replace("\\", "/"))
            .Distinct())
        {
            EnsureFolderExists(f);
        }

        for (int i = 0; i < jobs.Count; i++)
        {
            var (src, dest) = jobs[i];
            EditorUtility.DisplayProgressBar(
                "Restoring from DEAD_ASSET",
                $"{Path.GetFileName(src)}  ({i + 1}/{jobs.Count})",
                (float)i / Mathf.Max(1, jobs.Count));

            string err = AssetDatabase.MoveAsset(src, dest);
            if (string.IsNullOrEmpty(err)) restored++;
            else { failed++; Debug.LogWarning("Restore failed: " + src + " (" + err + ")"); }
        }
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        statusType = failed == 0 ? MessageType.Info : MessageType.Warning;
        statusMsg = $"Restored {restored} file(s). " +
                    (failed > 0 ? failed + " failed. See Console." : "");
        Scan();
    }

    /// <summary>
    /// Merges Unity's auto-numbered "<name> N" duplicate folders back into
    /// the canonical "<name>" folder. Left behind by the pre-fix batch-mode
    /// bug where <see cref="AssetDatabase.IsValidFolder"/> reported stale
    /// false during batching and each CreateFolder produced a new copy.
    ///
    /// Walks the tree bottom-up so nested dups (e.g. "lv10 5/images 3/…")
    /// are merged one layer at a time until every "<name> N" has been
    /// consumed. Files inside dup folders are moved into the base folder
    /// via <see cref="AssetDatabase.MoveAsset"/>, preserving GUIDs.
    /// </summary>
    private void RepairDeadAssetDupFolders()
    {
        if (!AssetDatabase.IsValidFolder(DeadAssetRoot))
        {
            statusMsg = DeadAssetRoot + " does not exist.";
            statusType = MessageType.Info;
            return;
        }

        int filesMoved = 0;
        int foldersRemoved = 0;
        int passes = 0;
        var log = new List<string>();

        try
        {
            // Repeat until a pass finds nothing to merge — nested dup shapes
            // (e.g. "lv10 5/images 3/x/…") may need multiple passes because
            // Directory.GetDirectories only reveals what currently exists.
            while (true)
            {
                passes++;
                var allDirs = Directory.GetDirectories(DeadAssetRoot, "*",
                        SearchOption.AllDirectories)
                    .Select(d => d.Replace("\\", "/"))
                    .OrderByDescending(d => d.Length) // deepest first
                    .ToList();

                bool anyMerged = false;

                foreach (var dupDir in allDirs)
                {
                    if (!Directory.Exists(dupDir)) continue; // may have been removed this pass

                    string name = Path.GetFileName(dupDir);
                    if (!TryStripUnitySuffix(name, out string baseName)) continue;

                    string parent = Path.GetDirectoryName(dupDir).Replace("\\", "/");
                    string baseDir = parent + "/" + baseName;

                    EditorUtility.DisplayProgressBar(
                        "Repair DEAD_ASSET",
                        $"Pass {passes}: {dupDir} → {baseDir}",
                        0f);

                    if (!AssetDatabase.IsValidFolder(baseDir))
                    {
                        // No canonical version exists — just rename the dup.
                        string err = AssetDatabase.MoveAsset(dupDir, baseDir);
                        if (string.IsNullOrEmpty(err))
                        {
                            log.Add("RENAME " + dupDir + " -> " + baseDir);
                            anyMerged = true;
                        }
                        else
                        {
                            log.Add("FAIL RENAME " + dupDir + " (" + err + ")");
                        }
                        continue;
                    }

                    // Merge: move every file directly inside dupDir into baseDir.
                    // Subdirectories are handled by the outer loop (deepest first).
                    var files = Directory.GetFiles(dupDir, "*.*",
                            SearchOption.TopDirectoryOnly)
                        .Select(f => f.Replace("\\", "/"))
                        .Where(f => !f.EndsWith(".meta"))
                        .ToList();

                    foreach (var src in files)
                    {
                        string fn = Path.GetFileName(src);
                        string dest = baseDir + "/" + fn;

                        // Collision → suffix.
                        int n = 1;
                        while (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dest) != null)
                        {
                            string ext = Path.GetExtension(fn);
                            string bare = fn.Substring(0, fn.Length - ext.Length);
                            dest = $"{baseDir}/{bare}_dup{n}{ext}";
                            n++;
                        }

                        string err = AssetDatabase.MoveAsset(src, dest);
                        if (string.IsNullOrEmpty(err))
                        {
                            filesMoved++;
                            anyMerged = true;
                            log.Add("MOVE " + src + " -> " + dest);
                        }
                        else
                        {
                            log.Add("FAIL " + src + " (" + err + ")");
                        }
                    }

                    // Empty subfolders (if any) — deepest-first loop already
                    // drained them, so this should be empty now.
                    var subdirs = Directory.GetDirectories(dupDir);
                    if (subdirs.Length == 0 && Directory.GetFiles(dupDir, "*.*",
                            SearchOption.TopDirectoryOnly)
                            .All(f => f.EndsWith(".meta")))
                    {
                        if (AssetDatabase.DeleteAsset(dupDir))
                        {
                            foldersRemoved++;
                            anyMerged = true;
                            log.Add("REMOVED " + dupDir);
                        }
                    }
                }

                if (!anyMerged || passes > 40) break; // safety cap
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Append to log for traceability.
        try
        {
            EnsureFolderExists(DeadAssetRoot);
            var header = new[]
            {
                "",
                "=== REPAIR " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                    "   passes=" + passes +
                    "   filesMoved=" + filesMoved +
                    "   foldersRemoved=" + foldersRemoved + " ==="
            };
            File.AppendAllLines(QuarantineLog, header.Concat(log));
            AssetDatabase.ImportAsset(QuarantineLog);
        }
        catch (Exception e) { Debug.LogWarning("Could not write log: " + e.Message); }

        statusType = MessageType.Info;
        statusMsg = $"Repair done: {filesMoved} files merged, " +
                    $"{foldersRemoved} dup folders removed in {passes} pass(es).";
    }

    /// <summary>
    /// Detects Unity's auto-numbered suffix pattern <c>"&lt;base&gt; N"</c>
    /// where N is a positive integer, and returns the base. Returns false
    /// when the name isn't a duplicate.
    /// </summary>
    private static bool TryStripUnitySuffix(string name, out string baseName)
    {
        baseName = null;
        if (string.IsNullOrEmpty(name)) return false;
        int lastSpace = name.LastIndexOf(' ');
        if (lastSpace <= 0 || lastSpace == name.Length - 1) return false;
        string suffix = name.Substring(lastSpace + 1);
        if (!int.TryParse(suffix, out int n) || n < 1) return false;
        baseName = name.Substring(0, lastSpace);
        return baseName.Length > 0;
    }

    private void EmptyDeadAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder(DeadAssetRoot))
        {
            statusMsg = DeadAssetRoot + " does not exist.";
            return;
        }
        if (!AssetDatabase.DeleteAsset(DeadAssetRoot))
        {
            statusMsg = "Failed to delete " + DeadAssetRoot + ".";
            statusType = MessageType.Error;
            return;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        statusMsg = DeadAssetRoot + " has been emptied.";
        statusType = MessageType.Info;
    }

    // ============================================================
    //  HELPERS
    // ============================================================

    private void SetAllSelected(bool v)
    {
        foreach (var r in reports)
            r.dead.ForEach(d => d.selected = v);
    }

    private void SetAllExpanded(bool v)
    {
        foreach (var r in reports) r.expanded = v;
    }

    /// <summary>
    /// Creates a folder recursively via AssetDatabase (so .meta is generated).
    /// Idempotent — no-op if it already exists.
    /// </summary>
    private static void EnsureFolderExists(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath)) return;
        folderPath = folderPath.Replace("\\", "/").TrimEnd('/');
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        var parts = folderPath.Split('/');
        if (parts.Length < 2 || parts[0] != "Assets") return;

        string cur = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    /// <summary>Sort so lv2 &lt; lv10 (numeric aware).</summary>
    private static int NaturalCompare(string a, string b)
    {
        string na = Path.GetFileName(a);
        string nb = Path.GetFileName(b);
        // Strip leading "lv"
        if (na.StartsWith("lv") && nb.StartsWith("lv")
            && int.TryParse(na.Substring(2), out int ia)
            && int.TryParse(nb.Substring(2), out int ib))
            return ia.CompareTo(ib);
        return string.Compare(na, nb, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatSize(long b)
    {
        if (b >= 1073741824L) return (b / 1073741824.0).ToString("0.0") + " GB";
        if (b >= 1048576L)    return (b / 1048576.0).ToString("0.0") + " MB";
        if (b >= 1024L)       return (b / 1024.0).ToString("0.0") + " KB";
        return b + " B";
    }
}
