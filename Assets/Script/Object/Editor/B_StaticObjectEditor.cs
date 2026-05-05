using UnityEditor;
using UnityEngine;

/// <summary>
/// Clean inspector for B_StaticObject. Shows a header banner with the
/// sprite's sorting order (the key design number) and surfaces the
/// blocksDrop toggle prominently.
/// </summary>
[CustomEditor(typeof(B_StaticObject))]
public class B_StaticObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        B_StaticObject so = (B_StaticObject)target;
        SpriteRenderer sr = so.GetComponent<SpriteRenderer>();
        Collider2D col = so.GetComponent<Collider2D>();
        int order = sr != null ? sr.sortingOrder : 0;
        bool blocks = col != null && col.enabled;

        // Header banner.
        Rect rect = GUILayoutUtility.GetRect(0f, 26f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.22f, 0.22f, 0.18f));

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 12;
        style.normal.textColor = blocks
            ? new Color(1f, 0.85f, 0.5f)
            : new Color(0.7f, 0.7f, 0.7f);
        string label = blocks
            ? $"STATIC OBJECT  ▪  Order {order}  ▪  blocks (has collider)"
            : $"STATIC OBJECT  ▪  Order {order}  ▪  visual only (no collider)";
        EditorGUI.LabelField(rect, label, style);

        EditorGUILayout.Space(4f);
        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }
}
