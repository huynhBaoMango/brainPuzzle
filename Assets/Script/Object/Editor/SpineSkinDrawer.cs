using UnityEditor;
using UnityEngine;

/// <summary>
/// Renders a string field tagged <see cref="SpineSkinAttribute"/> as a
/// dropdown of skin names pulled from the owner object's SkeletonAnimation
/// (B_SpineSkinSet, B_InteractableObject, B_StaticObject, group/queue
/// members). Falls back to a placeholder if no skeleton is reachable.
/// </summary>
[CustomPropertyDrawer(typeof(SpineSkinAttribute))]
public class SpineSkinDrawer : PropertyDrawer
{
    public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(rect, property, label);
            return;
        }

        Rect labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
        Rect popupRect = new Rect(
            rect.x + EditorGUIUtility.labelWidth, rect.y,
            rect.width - EditorGUIUtility.labelWidth, rect.height);
        EditorGUI.LabelField(labelRect, label);

        Object owner = property.serializedObject.targetObject;
        string[] skins = PuzzleEditorHelper.GetSpineSkinNamesForOwner(owner);

        if (skins == null || skins.Length == 0)
        {
            string ownerName = owner != null ? owner.GetType().Name : "(unknown)";
            EditorGUI.Popup(popupRect, 0,
                new[] { $"(no skeleton / no skins — owner: {ownerName})" });
            return;
        }

        PuzzleEditorHelper.StringPopupField(popupRect, property, skins, "(none)");
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}
