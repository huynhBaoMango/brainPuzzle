using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders a string field tagged <see cref="SpineAnimAttribute"/> as a
/// dropdown of animation names pulled from:
///  1. The StateAction's actionTarget (if set)
///  2. The owner object's skeleton (B_InteractableObject, B_StaticObject, etc.)
/// Falls back to a plain text field if no skeleton is available at edit
/// time (e.g. the skeleton reference isn't assigned yet).
/// </summary>
[CustomPropertyDrawer(typeof(SpineAnimAttribute))]
public class SpineAnimDrawer : PropertyDrawer
{
    public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(rect, property, label);
            return;
        }

        // Always draw label + popup
        Rect labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
        Rect popupRect = new Rect(
            rect.x + EditorGUIUtility.labelWidth, rect.y,
            rect.width - EditorGUIUtility.labelWidth, rect.height);
        EditorGUI.LabelField(labelRect, label);

        // Get the actual skeleton owner: prefer actionTarget if set on parent StateAction
        Object skeletonOwner = GetSkeletonOwner(property);
        string[] anims = PuzzleEditorHelper.GetSpineAnimNamesForOwner(skeletonOwner);

        if (anims == null || anims.Length == 0)
        {
            string owner = skeletonOwner != null
                ? skeletonOwner.GetType().Name
                : property.serializedObject.targetObject.GetType().Name;
            EditorGUI.Popup(popupRect, 0, new[] { $"(no skeleton / no anims — owner: {owner})" });
            return;
        }

        PuzzleEditorHelper.StringPopupField(popupRect, property, anims, "(none)");
    }

    private static Object GetSkeletonOwner(SerializedProperty spineAnimProperty)
    {
        // Use the same approach as ActivateState: direct FindPropertyRelative
        Object actionTarget = GetActionTargetFromStateAction(spineAnimProperty);
        if (actionTarget != null)
        {
            return actionTarget;
        }

        return spineAnimProperty.serializedObject.targetObject;
    }

    private static Object GetActionTargetFromStateAction(SerializedProperty spineAnimProperty)
    {
        // Get the parent StateAction - then use FindPropertyRelative like ActivateState does
        SerializedProperty parentAction = spineAnimProperty.GetSerializedPropertyParent();
        if (parentAction == null) return null;
        
        // Now use FindPropertyRelative like StateActionDrawer does for activateTarget
        SerializedProperty actionTargetProp = parentAction.FindPropertyRelative("actionTarget");
        if (actionTargetProp != null)
        {
            return actionTargetProp.objectReferenceValue;
        }
        
        return null;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}

public static class SpineAnimDrawerExtensions
{
    public static SerializedProperty GetSerializedPropertyParent(this SerializedProperty property)
    {
        if (property == null) return null;

        string propertyPath = property.propertyPath;
        string[] parts = propertyPath.Split('.');
        if (parts.Length < 2) return null;

        string parentPath = string.Join(".", parts, 0, parts.Length - 1);
        return property.serializedObject.FindProperty(parentPath);
    }
}