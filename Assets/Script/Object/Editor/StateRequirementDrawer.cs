using UnityEditor;
using UnityEngine;

/// <summary>
/// Compact one-line drawer for StateRequirement with dropdown pickers:
/// "Object [▾ princess] → State [▾ dressed]".
/// Uses scene-scanning popups so designers can't mis-type an id.
/// </summary>
[CustomPropertyDrawer(typeof(StateRequirement))]
public class StateRequirementDrawer : PropertyDrawer
{
    private const float ObjectLabelWidth = 48f;
    private const float StateLabelWidth = 40f;
    private const float ArrowWidth = 14f;
    private const float Spacing = 4f;
    private const float ModeWidth = 80f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty objIdProp = property.FindPropertyRelative("objectId");
        SerializedProperty stateIdProp = property.FindPropertyRelative("stateId");
        SerializedProperty invertedProp = property.FindPropertyRelative("requireNotDone");

        // Reset indent so the row stays on a single line in nested lists.
        int prevIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float fieldWidth =
            (position.width - ObjectLabelWidth - StateLabelWidth - ArrowWidth
             - ModeWidth - Spacing * 5f) / 2f;

        // ---- Object Id dropdown ----
        Rect r = new Rect(position.x, position.y, ObjectLabelWidth, position.height);
        EditorGUI.LabelField(r, "Object");

        r.x += ObjectLabelWidth; r.width = fieldWidth;
        string[] objectIds = PuzzleEditorHelper.GetAllObjectIds();
        PuzzleEditorHelper.StringPopupField(r, objIdProp, objectIds, "(none)");

        // ---- Arrow ----
        r.x += fieldWidth + Spacing; r.width = ArrowWidth;
        EditorGUI.LabelField(r, "→");

        // ---- State Id dropdown (filtered by selected object) ----
        r.x += ArrowWidth + Spacing; r.width = StateLabelWidth;
        EditorGUI.LabelField(r, "State");

        r.x += StateLabelWidth; r.width = fieldWidth;
        string[] stateIds = PuzzleEditorHelper.GetStateIds(objIdProp.stringValue);
        PuzzleEditorHelper.StringPopupField(r, stateIdProp, stateIds, "(none)");

        // ---- Mode popup: Done / Not Done ----
        r.x += fieldWidth + Spacing; r.width = ModeWidth;
        int modeIdx = invertedProp != null && invertedProp.boolValue ? 1 : 0;
        int newMode = EditorGUI.Popup(r, modeIdx, new[] { "is Done", "is Not Done" });
        if (invertedProp != null && newMode != modeIdx)
            invertedProp.boolValue = (newMode == 1);

        EditorGUI.indentLevel = prevIndent;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}
