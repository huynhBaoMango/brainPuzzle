using UnityEditor;
using UnityEngine;

/// <summary>
/// Foldout drawer for ObjectState. Each state shows a smart header
/// (e.g. "▼ on_princess  [DRAG → shower]  ✓") and groups its fields into
/// labeled sections inside. Conditionally hides Required Zone Id when the
/// trigger is not DRAG.
/// </summary>
[CustomPropertyDrawer(typeof(ObjectState))]
public class ObjectStateDrawer : PropertyDrawer
{
    private const float SectionPadding = 6f;
    private const float SectionLabelExtra = 2f;

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

    // Single source of truth for layout — same code measures and renders.
    private float DrawOrMeasure(Rect position, SerializedProperty property, bool draw)
    {
        float startY = position.y;
        float y = startY;
        float lineH = EditorGUIUtility.singleLineHeight;

        // ---- Foldout header ----
        if (draw)
        {
            Rect headerRect = new Rect(position.x, y, position.width, lineH);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.foldout);
            headerStyle.fontStyle = FontStyle.Bold;
            property.isExpanded = EditorGUI.Foldout(
                headerRect, property.isExpanded, MakeHeader(property), true, headerStyle);
        }
        y += lineH;

        if (!property.isExpanded) return y - startY;

        y += SectionPadding;

        // ---- Identity ----
        DrawSection(ref y, position, "Identity", draw);
        DrawField(ref y, position, property, "stateId", "State Id", draw);
        y += SectionPadding;

        // ---- Visual & Audio ----
        DrawSection(ref y, position, "Visual & Audio", draw);
        VisualMode visMode = PuzzleEditorHelper.GetOwnerVisualMode(property.serializedObject.targetObject);
        if (visMode == VisualMode.Sprite)
        {
            DrawField(ref y, position, property, "stateSprite", "Sprite", draw);
        }
        else
        {
            DrawField(ref y, position, property, "stateSpineAnim", "Spine Anim", draw);
            DrawField(ref y, position, property, "stateSpineLoop", "Spine Loop", draw);
        }
        DrawField(ref y, position, property, "stateSFX", "SFX", draw);
        y += SectionPadding;

        // ---- Activation ----
        DrawSection(ref y, position, "Activation", draw);
        DrawField(ref y, position, property, "trigger", "Trigger", draw);

        // Required Zone Id only matters for DRAG triggers — hide otherwise.
        InteractType currentTrigger =
            (InteractType)property.FindPropertyRelative("trigger").enumValueIndex;
        if (currentTrigger == InteractType.DRAG)
        {
            DrawZoneIdPopup(ref y, position, property, draw);
            // Drag sprite only matters under Sprite mode — hide in Spine mode.
            if (visMode == VisualMode.Sprite)
                DrawField(ref y, position, property, "dragSprite", "Drag Sprite", draw);
        }
            

        DrawField(ref y, position, property, "requirements", "Requirements", draw);
        DrawField(ref y, position, property, "requiredCount", "Required Count (0 = all)", draw);
        y += SectionPadding;

        // ---- Actions ----
        DrawSection(ref y, position, "Actions", draw);
        DrawField(ref y, position, property, "actions", "Actions", draw);
        y += SectionPadding;

        // ---- Messages ----
        DrawSection(ref y, position, "Messages", draw);
        DrawField(ref y, position, property, "successMessageKey", "Success Message Key", draw);
        DrawField(ref y, position, property, "failMessageKey", "Fail Message Key", draw);
        DrawField(ref y, position, property, "hintMessageKey", "Hint Message Key", draw);
        y += SectionPadding;

        // ---- Events ----
        DrawSection(ref y, position, "Events", draw);
        DrawField(ref y, position, property, "onStartHook", "On Start Hook", draw);
        DrawField(ref y, position, property, "OnStartState", "On Start State (Unity only)", draw);
        y += SectionPadding;

        // ---- Runtime ----
        DrawSection(ref y, position, "Runtime", draw);
        DrawField(ref y, position, property, "repeatable", "Repeatable", draw);
        DrawField(ref y, position, property, "isDone", "Is Done", draw);

        return y - startY;
    }

    private void DrawSection(ref float y, Rect position, string text, bool draw)
    {
        float h = EditorGUIUtility.singleLineHeight;
        if (draw)
        {
            Rect rect = new Rect(position.x, y, position.width, h);
            GUIStyle style = new GUIStyle(EditorStyles.miniBoldLabel);
            style.normal.textColor = new Color(0.6f, 0.85f, 1f);
            EditorGUI.LabelField(rect, text, style);

            // Thin underline for the section.
            Rect lineRect = new Rect(position.x, y + h - 1f, position.width, 1f);
            EditorGUI.DrawRect(lineRect, new Color(0.6f, 0.85f, 1f, 0.25f));
        }
        y += h + SectionLabelExtra;
    }

    private void DrawField(ref float y, Rect position, SerializedProperty parent,
                           string name, string label, bool draw)
    {
        SerializedProperty prop = parent.FindPropertyRelative(name);
        if (prop == null) return;

        float h = EditorGUI.GetPropertyHeight(prop, true);
        if (draw)
        {
            Rect rect = new Rect(position.x, y, position.width, h);
            GUIContent content = new GUIContent(label, prop.tooltip);
            EditorGUI.PropertyField(rect, prop, content, true);
        }
        y += h + EditorGUIUtility.standardVerticalSpacing;
    }

    // Draws a Required Zone Id popup listing all B_DropZones in the scene.
    private void DrawZoneIdPopup(ref float y, Rect position,
                                  SerializedProperty parent, bool draw)
    {
        SerializedProperty prop = parent.FindPropertyRelative("requiredZoneId");
        if (prop == null) return;

        float h = EditorGUIUtility.singleLineHeight;
        if (draw)
        {
            Rect rect = new Rect(position.x, y, position.width, h);
            EditorGUI.LabelField(
                new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height),
                new GUIContent("Required Zone Id"));
            Rect popupRect = new Rect(
                rect.x + EditorGUIUtility.labelWidth, rect.y,
                rect.width - EditorGUIUtility.labelWidth, rect.height);
            string[] zoneIds = PuzzleEditorHelper.GetAllZoneIds();
            PuzzleEditorHelper.StringPopupField(popupRect, prop, zoneIds, "(any zone)");
        }
        y += h + EditorGUIUtility.standardVerticalSpacing;
    }

    // Builds the foldout label that summarizes the state at a glance.
    private string MakeHeader(SerializedProperty property)
    {
        string stateId = property.FindPropertyRelative("stateId").stringValue;
        InteractType trigger = (InteractType)property.FindPropertyRelative("trigger").enumValueIndex;
        string zone = property.FindPropertyRelative("requiredZoneId").stringValue;
        bool isDone = property.FindPropertyRelative("isDone").boolValue;

        string id = string.IsNullOrEmpty(stateId) ? "<unnamed>" : stateId;
        string trig = trigger.ToString();
        if (trigger == InteractType.DRAG && !string.IsNullOrEmpty(zone))
            trig += $" → {zone}";

        string done = isDone ? "  ✓" : string.Empty;
        return $"{id}    [{trig}]{done}";
    }
}
