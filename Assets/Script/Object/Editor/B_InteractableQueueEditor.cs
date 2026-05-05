using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for B_InteractableQueue. Keeps the default Unity layout
/// but replaces the "Queue Empty State Id" string field with a popup filtered
/// by whatever states the assigned <c>queueEmptyTarget</c> exposes — same
/// pattern as ActivateState / AdvanceQueue drawers.
/// </summary>
[CustomEditor(typeof(B_InteractableQueue))]
public class B_InteractableQueueEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iter = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iter.NextVisible(enterChildren))
        {
            enterChildren = false;

            // Skip the built-in script field — Unity already draws it at the top.
            if (iter.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(iter, true);
                continue;
            }

            // Replace queueEmptyStateId with a popup filtered by the target.
            if (iter.propertyPath == "queueEmptyStateId")
            {
                DrawQueueEmptyStateIdPopup();
                continue;
            }

            EditorGUILayout.PropertyField(iter, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawQueueEmptyStateIdPopup()
    {
        SerializedProperty stateIdProp = serializedObject.FindProperty("queueEmptyStateId");
        SerializedProperty targetProp = serializedObject.FindProperty("queueEmptyTarget");
        if (stateIdProp == null) return;

        B_InteractableObject target = targetProp != null
            ? targetProp.objectReferenceValue as B_InteractableObject : null;

        if (target != null && !string.IsNullOrEmpty(target.ObjectId))
        {
            Rect rect = EditorGUILayout.GetControlRect();
            EditorGUI.LabelField(
                new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height),
                new GUIContent("Queue Empty State Id", stateIdProp.tooltip));
            Rect popupRect = new Rect(
                rect.x + EditorGUIUtility.labelWidth, rect.y,
                rect.width - EditorGUIUtility.labelWidth, rect.height);

            string[] stateIds = PuzzleEditorHelper.GetStateIds(target.ObjectId);
            PuzzleEditorHelper.StringPopupField(popupRect, stateIdProp, stateIds, "(none)");
        }
        else
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(
                    new GUIContent("Queue Empty State Id", stateIdProp.tooltip),
                    "(assign Queue Empty Target first)");
            }
        }
    }
}
