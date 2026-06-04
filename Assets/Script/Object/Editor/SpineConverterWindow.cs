using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bulk-prepares raw Spine exports for Unity import. Spine-Unity expects the
/// binary skeleton as a <c>.skel.bytes</c> TextAsset and the atlas as a
/// <c>.atlas.txt</c> TextAsset; raw <c>.skel</c>/<c>.atlas</c> files are ignored
/// by Unity. This tool scans a folder, appends <c>.bytes</c> to every skeleton
/// and <c>.txt</c> to every atlas, then re-imports.
/// Open via <b>Tools &gt; Puzzle &gt; Spine Converter</b>.
/// </summary>
public class SpineConverterWindow : EditorWindow
{
    private const string SkelExt = ".skel";
    private const string AtlasExt = ".atlas";

    private DefaultAsset targetFolder;
    private bool recursive = true;
    private bool deleteOriginal = true;
    private Vector2 scroll;
    private string log = "";

    [MenuItem("Tools/Puzzle/Spine Converter")]
    public static void Open()
    {
        var w = GetWindow<SpineConverterWindow>("Spine Converter");
        w.minSize = new Vector2(420f, 300f);
        w.Show();
    }

    private void OnEnable()
    {
        // Default to current selection if it is a folder.
        if (Selection.activeObject is DefaultAsset da &&
            AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(da)))
        {
            targetFolder = da;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Appends .bytes to .skel files and .txt to .atlas files so Spine-Unity " +
            "can import them as TextAssets.",
            MessageType.Info);

        EditorGUILayout.Space();
        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Target Folder", targetFolder, typeof(DefaultAsset), false);

        recursive = EditorGUILayout.Toggle("Include Subfolders", recursive);
        deleteOriginal = EditorGUILayout.Toggle("Delete Original", deleteOriginal);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(targetFolder == null))
        {
            if (GUILayout.Button("Convert", GUILayout.Height(32f)))
                Convert();
        }

        if (!string.IsNullOrEmpty(log))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }

    private void Convert()
    {
        string folderPath = AssetDatabase.GetAssetPath(targetFolder);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            log = "Selected object is not a folder.";
            return;
        }

        // folderPath is project-relative (e.g. "Assets/Spine"); turn it absolute.
        string absFolder = Path.GetFullPath(folderPath);
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        var sb = new StringBuilder();
        int converted = 0, skipped = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string file in Directory.GetFiles(absFolder, "*", option))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string suffix;
                if (ext == SkelExt) suffix = ".bytes";
                else if (ext == AtlasExt) suffix = ".txt";
                else continue;

                string dest = file + suffix;
                if (File.Exists(dest))
                {
                    sb.AppendLine($"SKIP (exists): {Rel(dest)}");
                    skipped++;
                    continue;
                }

                File.Copy(file, dest);
                if (deleteOriginal)
                {
                    File.Delete(file);
                    DeleteMetaIfExists(file);
                }

                sb.AppendLine($"OK: {Rel(file)} -> {Rel(dest)}");
                converted++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        sb.Insert(0, $"Converted {converted}, skipped {skipped}.\n\n");
        log = sb.ToString();
        Debug.Log($"[SpineConverter] Converted {converted}, skipped {skipped} in {folderPath}.");
    }

    private static void DeleteMetaIfExists(string file)
    {
        string meta = file + ".meta";
        if (File.Exists(meta)) File.Delete(meta);
    }

    private static string Rel(string absPath)
    {
        string dataPath = Application.dataPath.Replace('\\', '/');
        string norm = absPath.Replace('\\', '/');
        int idx = norm.IndexOf("/Assets/", System.StringComparison.Ordinal);
        if (norm.StartsWith(dataPath)) return "Assets" + norm.Substring(dataPath.Length);
        if (idx >= 0) return norm.Substring(idx + 1);
        return norm;
    }
}
