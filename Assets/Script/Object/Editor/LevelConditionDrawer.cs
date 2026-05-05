using UnityEditor;
using UnityEngine;

/// <summary>
/// Compact drawer for LevelCondition. Mirrors StateRequirementDrawer:
/// "[▾ Type]   Object [▾ princess] → State [▾ dressed]"
///
/// Uses the shared <see cref="PuzzleEditorHelper.GetAllObjectIds"/> list so
/// both B_InteractableObject IDs and B_InteractableQueue IDs appear in the
/// dropdown — win/lose conditions can watch queue states directly.
///
/// Auto-migrates the legacy <c>target</c> (B_InteractableObject reference)
/// field into the new <c>targetId</c> string whenever an existing condition
/// is drawn with an empty targetId, so older scenes keep working.
/// </summary>
[CustomPropertyDrawer(typeof(LevelCondition))]
public class LevelConditionDrawer : PropertyDrawer
{
    private const float TypeWidth = 110f;
    private const float ObjectLabelWidth = 48f;
    private const float StateLabelWidth = 40f;
    private const float ArrowWidth = 14f;
    private const float Spacing = 4f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty typeProp = property.FindPropertyRelative("type");
        SerializedProperty targetIdProp = property.FindPropertyRelative("targetId");
        SerializedProperty legacyTargetProp = property.FindPropertyRelative("target");
        SerializedProperty stateIdProp = property.FindPropertyRelative("stateId");

        // Lazy migration: if targetId is empty but the legacy target ref is
        // set, copy the ObjectId over so existing scenes just work.
        if (targetIdProp != null && string.IsNullOrEmpty(targetIdProp.stringValue)
            && legacyTargetProp != null && legacyTargetProp.objectReferenceValue is B_InteractableObject legacy
            && !string.IsNullOrEmpty(legacy.ObjectId))
        {
            targetIdProp.stringValue = legacy.ObjectId;
        }

        int prevIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        float fieldWidth =
            (position.width - TypeWidth - ObjectLabelWidth - StateLabelWidth
             - ArrowWidth - Spacing * 5f) / 2f;

        // ---- Type enum ----
        Rect r = new Rect(position.x, position.y, TypeWidth, position.height);
        EditorGUI.PropertyField(r, typeProp, GUIContent.none);

        // ---- Object/Queue Id dropdown ----
        r.x += TypeWidth + Spacing; r.width = ObjectLabelWidth;
        EditorGUI.LabelField(r, "Object");

        r.x += ObjectLabelWidth; r.width = fieldWidth;
        string[] ids = PuzzleEditorHelper.GetAllObjectIds();
        PuzzleEditorHelper.StringPopupField(r, targetIdProp, ids, "(none)");

        // ---- Arrow ----
        r.x += fieldWidth + Spacing; r.width = ArrowWidth;
        EditorGUI.LabelField(r, "→");

        // ---- State Id dropdown (filtered by selected target) ----
        r.x += ArrowWidth + Spacing; r.width = StateLabelWidth;
        EditorGUI.LabelField(r, "State");

        r.x += StateLabelWidth; r.width = fieldWidth;
        string[] stateIds = PuzzleEditorHelper.GetStateIds(targetIdProp.stringValue);
        PuzzleEditorHelper.StringPopupField(r, stateIdProp, stateIds, "(none)");

        EditorGUI.indentLevel = prevIndent;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}
