using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for B_DropZone. Warns when Zone Id is empty
/// (otherwise no state's requiredZoneId can ever match it).
/// </summary>
[CustomEditor(typeof(B_DropZone))]
public class B_DropZoneEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty zoneIdProp = serializedObject.FindProperty("zoneId");
        string id = zoneIdProp.stringValue;
        string display = string.IsNullOrEmpty(id) ? "<no id>" : id;

        // Header banner
        Rect rect = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.28f, 0.28f));

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 12;
        style.normal.textColor = string.IsNullOrEmpty(id)
            ? new Color(1f, 0.6f, 0.4f)
            : new Color(0.5f, 1f, 0.95f);
        EditorGUI.LabelField(rect, $"DROP ZONE  ⚓  {display}", style);

        if (string.IsNullOrEmpty(id))
        {
            EditorGUILayout.HelpBox(
                "Zone Id is empty. No interactable state's Required Zone Id will ever match this drop zone.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(4f);
        DrawDefaultInspector();

        // ---- Sort Order sync helper ----
        B_DropZone zone = (B_DropZone)target;
        SpriteRenderer sr = zone.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            SerializedProperty sortOrderProp = serializedObject.FindProperty("sortOrder");
            if (sortOrderProp.intValue != sr.sortingOrder)
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.HelpBox(
                    $"Sort Order ({sortOrderProp.intValue}) differs from SpriteRenderer sorting order ({sr.sortingOrder}).",
                    MessageType.Info);
                if (GUILayout.Button("Sync Sort Order from SpriteRenderer"))
                {
                    sortOrderProp.intValue = sr.sortingOrder;
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
