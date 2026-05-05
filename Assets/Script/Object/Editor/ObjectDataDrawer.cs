using UnityEditor;
using UnityEngine;

/// <summary>
/// Drawer for ObjectData. Hides the init-sprite field when the owner is in
/// Spine mode, and the init-spine fields when the owner is in Sprite mode,
/// so designers only see the fields that apply to their active visual mode.
///
/// Owner visualMode is resolved via <see cref="PuzzleEditorHelper.GetOwnerVisualMode"/>
/// — works for B_InteractableObject, B_InteractableGroup, B_InteractableQueue,
/// and B_StaticObject alike.
/// </summary>
[CustomPropertyDrawer(typeof(ObjectData))]
public class ObjectDataDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        DrawOrMeasure(position, property, draw: true);
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return DrawOrMeasure(new Rect(0f, 0f, 0f, 0f), property, draw: false);
    }

    private float DrawOrMeasure(Rect position, SerializedProperty property, bool draw)
    {
        float startY = position.y;
        float y = startY;
        float lineH = EditorGUIUtility.singleLineHeight;

        // Foldout header
        if (draw)
        {
            Rect headerRect = new Rect(position.x, y, position.width, lineH);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, property.displayName, true);
        }
        y += lineH;

        if (!property.isExpanded) return y - startY;

        VisualMode mode = PuzzleEditorHelper.GetOwnerVisualMode(property.serializedObject.targetObject);

        if (draw) EditorGUI.indentLevel++;

        DrawField(ref y, position, property, "initStateId", draw);

        // Mode-dependent fields.
        if (mode == VisualMode.Sprite)
        {
            DrawField(ref y, position, property, "initSprite", draw);
        }
        else // Spine
        {
            DrawField(ref y, position, property, "initSpineAnim", draw);
            DrawField(ref y, position, property, "initSpineLoop", draw);
        }

        DrawField(ref y, position, property, "initSFX", draw);
        DrawField(ref y, position, property, "states", draw);

        if (draw) EditorGUI.indentLevel--;

        return y - startY;
    }

    private static void DrawField(ref float y, Rect position, SerializedProperty parent,
                                  string name, bool draw)
    {
        SerializedProperty prop = parent.FindPropertyRelative(name);
        if (prop == null) return;

        float h = EditorGUI.GetPropertyHeight(prop, true);
        if (draw)
        {
            Rect rect = new Rect(position.x, y, position.width, h);
            EditorGUI.PropertyField(rect, prop, true);
        }
        y += h + EditorGUIUtility.standardVerticalSpacing;
    }
}
