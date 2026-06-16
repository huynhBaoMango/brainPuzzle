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
    private const float ModeWidth = 130f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty objIdProp = property.FindPropertyRelative("objectId");
        SerializedProperty stateIdProp = property.FindPropertyRelative("stateId");
        SerializedProperty invertedProp = property.FindPropertyRelative("requireNotDone");
        SerializedProperty gateProp = property.FindPropertyRelative("gate");

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

        // ---- Mode popup: Done / Not Done, with optional "(gate)" variants.
        // A gate requirement is MANDATORY (must always be met regardless of
        // the state's Required Count) and does NOT count toward the count
        // milestone. Use it for trigger flags like "lose_pending".
        r.x += fieldWidth + Spacing; r.width = ModeWidth;
        bool inv = invertedProp != null && invertedProp.boolValue;
        bool gate = gateProp != null && gateProp.boolValue;
        int modeIdx = (gate ? 2 : 0) + (inv ? 1 : 0);
        // 0: is Done, 1: is Not Done, 2: is Done (gate), 3: is Not Done (gate)
        int newMode = EditorGUI.Popup(r, modeIdx, new[]
        {
            "is Done", "is Not Done", "is Done (gate)", "is Not Done (gate)"
        });
        if (newMode != modeIdx)
        {
            if (invertedProp != null) invertedProp.boolValue = (newMode & 1) != 0;
            if (gateProp != null) gateProp.boolValue = (newMode & 2) != 0;
        }

        EditorGUI.indentLevel = prevIndent;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}
