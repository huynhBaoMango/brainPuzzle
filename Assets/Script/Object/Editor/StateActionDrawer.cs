using UnityEditor;
using UnityEngine;

/// <summary>
/// Foldout drawer for StateAction. The expanded view only shows the
/// fields relevant to the chosen action type — designers never see
/// irrelevant params. Collapsed view summarizes the action in one line.
/// </summary>
[CustomPropertyDrawer(typeof(StateAction))]
public class StateActionDrawer : PropertyDrawer
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

        // Foldout header with smart summary
        if (draw)
        {
            Rect headerRect = new Rect(position.x, y, position.width, lineH);
            property.isExpanded = EditorGUI.Foldout(
                headerRect, property.isExpanded, MakeHeader(property), true);
        }
        y += lineH;

        if (!property.isExpanded) return y - startY;

        if (draw) EditorGUI.indentLevel++;
        y += 2f;

        DrawField(ref y, position, property, "type", "Type", draw);

        StateActionType type =
            (StateActionType)property.FindPropertyRelative("type").enumValueIndex;

        // Show actionTarget for types that can operate on another object.
        if (type == StateActionType.MoveTo
            || type == StateActionType.Disappear
            || type == StateActionType.Appear
            || type == StateActionType.DoAnimation)
        {
            DrawField(ref y, position, property, "actionTarget", "Target Object (optional)", draw);
        }

        switch (type)
        {
            case StateActionType.Wait:
                DrawField(ref y, position, property, "duration", "Duration (s)", draw);
                break;

            case StateActionType.MoveTo:
                DrawField(ref y, position, property, "moveTarget", "Move To", draw);
                DrawField(ref y, position, property, "duration", "Duration (s)", draw);
                DrawField(ref y, position, property, "ease", "Ease", draw);
                break;

            case StateActionType.Disappear:
                DrawField(ref y, position, property, "fadeOut", "Fade Out", draw);
                DrawField(ref y, position, property, "duration", "Duration (s)", draw);
                DrawField(ref y, position, property, "destroyOnDisappear", "Destroy After", draw);
                break;

            case StateActionType.Appear:
                DrawField(ref y, position, property, "fadeIn", "Fade In", draw);
                DrawField(ref y, position, property, "duration", "Duration (s)", draw);
                break;

            case StateActionType.DoAnimation:
                DrawField(ref y, position, property, "spineAnim", "Spine Anim", draw);
                DrawField(ref y, position, property, "spineLoop", "Spine Loop", draw);
                DrawField(ref y, position, property, "duration", "Wait After (s)", draw);
                break;

            case StateActionType.ActivateState:
                DrawField(ref y, position, property, "activateTarget", "Target Interactable", draw);
                DrawActivateStateIdPopup(ref y, position, property, draw);
                DrawField(ref y, position, property, "chainGuards", "Chain Guards (skip if ALL met)", draw);
                break;

            case StateActionType.AdvanceQueue:
                DrawField(ref y, position, property, "queueTarget", "Target Queue", draw);
                DrawQueueServeStateIdPopup(ref y, position, property, draw);
                break;

            case StateActionType.PlaySFX:
                DrawField(ref y, position, property, "sfxClip", "Audio Clip", draw);
                DrawField(ref y, position, property, "duration", "Wait After (s)", draw);
                break;

            case StateActionType.SkinChange:
                DrawField(ref y, position, property, "skinTarget", "Target Interactable", draw);
                DrawField(ref y, position, property, "skinOp", "Op", draw);
                DrawSkinNamePopup(ref y, position, property, draw);
                break;
        }

        DrawField(ref y, position, property, "runInParallel", "Run In Parallel", draw);

        if (draw) EditorGUI.indentLevel--;
        y += 2f;

        return y - startY;
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

    // Draws a State Id popup filtered by the activateTarget's states.
    private void DrawActivateStateIdPopup(ref float y, Rect position,
                                          SerializedProperty property, bool draw)
    {
        SerializedProperty stateIdProp = property.FindPropertyRelative("activateStateId");
        if (stateIdProp == null) return;

        float h = EditorGUIUtility.singleLineHeight;
        if (draw)
        {
            Rect rect = new Rect(position.x, y, position.width, h);

            Object targetObj = property.FindPropertyRelative("activateTarget").objectReferenceValue;
            B_InteractableObject target = targetObj as B_InteractableObject;

            if (target != null && !string.IsNullOrEmpty(target.ObjectId))
            {
                string[] stateIds = PuzzleEditorHelper.GetStateIds(target.ObjectId);
                EditorGUI.LabelField(
                    new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height),
                    new GUIContent("State Id"));
                Rect popupRect = new Rect(
                    rect.x + EditorGUIUtility.labelWidth, rect.y,
                    rect.width - EditorGUIUtility.labelWidth, rect.height);
                PuzzleEditorHelper.StringPopupField(popupRect, stateIdProp, stateIds, "(none)");
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.TextField(rect, "State Id", "(select target first)");
                EditorGUI.EndDisabledGroup();
            }
        }
        y += h + EditorGUIUtility.standardVerticalSpacing;
    }

    // Draws a State Id popup filtered by the queueTarget's states.
    private void DrawQueueServeStateIdPopup(ref float y, Rect position,
                                            SerializedProperty property, bool draw)
    {
        SerializedProperty stateIdProp = property.FindPropertyRelative("queueServeStateId");
        if (stateIdProp == null) return;

        float h = EditorGUIUtility.singleLineHeight;
        if (draw)
        {
            Rect rect = new Rect(position.x, y, position.width, h);

            Object targetObj = property.FindPropertyRelative("queueTarget").objectReferenceValue;
            B_InteractableQueue target = targetObj as B_InteractableQueue;

            if (target != null && target.Data != null)
            {
                string[] stateIds = PuzzleEditorHelper.GetStateIdsFromData(target.Data);
                EditorGUI.LabelField(
                    new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height),
                    new GUIContent("Serve State Id"));
                Rect popupRect = new Rect(
                    rect.x + EditorGUIUtility.labelWidth, rect.y,
                    rect.width - EditorGUIUtility.labelWidth, rect.height);
                // "(default)" = first non-init state — matches ServeHead(null) behavior.
                PuzzleEditorHelper.StringPopupField(popupRect, stateIdProp, stateIds, "(default: first state)");
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.TextField(rect, "Serve State Id", "(select target queue first)");
                EditorGUI.EndDisabledGroup();
            }
        }
        y += h + EditorGUIUtility.standardVerticalSpacing;
    }

    // Draws a Skin Name popup filtered by the skinTarget's SkeletonDataAsset.
    private void DrawSkinNamePopup(ref float y, Rect position,
                                    SerializedProperty property, bool draw)
    {
        SerializedProperty skinNameProp = property.FindPropertyRelative("skinName");
        if (skinNameProp == null) return;

        float h = EditorGUIUtility.singleLineHeight;
        if (draw)
        {
            Rect rect = new Rect(position.x, y, position.width, h);

            Object targetObj = property.FindPropertyRelative("skinTarget").objectReferenceValue;
            B_InteractableObject target = targetObj as B_InteractableObject;

            string[] skins = target != null
                ? PuzzleEditorHelper.GetSpineSkinNamesForOwner(target)
                : new string[0];

            if (skins != null && skins.Length > 0)
            {
                EditorGUI.LabelField(
                    new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height),
                    new GUIContent("Skin Name"));
                Rect popupRect = new Rect(
                    rect.x + EditorGUIUtility.labelWidth, rect.y,
                    rect.width - EditorGUIUtility.labelWidth, rect.height);
                PuzzleEditorHelper.StringPopupField(popupRect, skinNameProp, skins, "(none)");
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.TextField(rect, "Skin Name",
                    target == null
                        ? "(select target first)"
                        : "(target has no skins / no SkeletonDataAsset)");
                EditorGUI.EndDisabledGroup();
            }
        }
        y += h + EditorGUIUtility.standardVerticalSpacing;
    }

    // Compact summary shown when the action is collapsed.
    private string MakeHeader(SerializedProperty property)
    {
        StateActionType type =
            (StateActionType)property.FindPropertyRelative("type").enumValueIndex;
        bool parallel = property.FindPropertyRelative("runInParallel").boolValue;
        float dur = property.FindPropertyRelative("duration").floatValue;

        string summary;
        switch (type)
        {
            case StateActionType.Wait:
                summary = $"Wait  {dur:0.##}s";
                break;

            case StateActionType.MoveTo:
                {
                    Object t = property.FindPropertyRelative("moveTarget").objectReferenceValue;
                    string name = t != null ? t.name : "<no target>";
                    summary = $"MoveTo  →  {name}  ({dur:0.##}s)";
                    break;
                }

            case StateActionType.Disappear:
                {
                    bool fade = property.FindPropertyRelative("fadeOut").boolValue;
                    bool destroy = property.FindPropertyRelative("destroyOnDisappear").boolValue;
                    string mode = fade ? "fade" : "instant";
                    string after = destroy ? "destroy" : "deactivate";
                    summary = $"Disappear  ({mode}, {after}, {dur:0.##}s)";
                    break;
                }

            case StateActionType.Appear:
                {
                    bool fade = property.FindPropertyRelative("fadeIn").boolValue;
                    string mode = fade ? "fade" : "instant";
                    summary = $"Appear  ({mode}, {dur:0.##}s)";
                    break;
                }

            case StateActionType.DoAnimation:
                {
                    string spine = property.FindPropertyRelative("spineAnim").stringValue;
                    string label = string.IsNullOrEmpty(spine) ? "<no anim>" : spine;
                    summary = $"DoAnimation  '{label}'  ({dur:0.##}s)";
                    break;
                }

            case StateActionType.ActivateState:
                {
                    Object t = property.FindPropertyRelative("activateTarget").objectReferenceValue;
                    string sid = property.FindPropertyRelative("activateStateId").stringValue;
                    string name = t != null ? t.name : "<no target>";
                    if (string.IsNullOrEmpty(sid)) sid = "<no state>";
                    SerializedProperty guards = property.FindPropertyRelative("chainGuards");
                    int guardCount = guards != null && guards.isArray ? guards.arraySize : 0;
                    summary = guardCount > 0
                        ? $"ActivateState  {name}.{sid}   [guarded ×{guardCount}]"
                        : $"ActivateState  {name}.{sid}";
                    break;
                }

            case StateActionType.AdvanceQueue:
                {
                    Object t = property.FindPropertyRelative("queueTarget").objectReferenceValue;
                    string sid = property.FindPropertyRelative("queueServeStateId").stringValue;
                    string name = t != null ? t.name : "<no queue>";
                    summary = string.IsNullOrEmpty(sid)
                        ? $"AdvanceQueue  {name}"
                        : $"AdvanceQueue  {name}.{sid}";
                    break;
                }

            case StateActionType.PlaySFX:
                {
                    Object clip = property.FindPropertyRelative("sfxClip").objectReferenceValue;
                    string name = clip != null ? clip.name : "<no clip>";
                    summary = dur > 0f
                        ? $"PlaySFX  '{name}'  (wait {dur:0.##}s)"
                        : $"PlaySFX  '{name}'";
                    break;
                }

            case StateActionType.SkinChange:
                {
                    Object t = property.FindPropertyRelative("skinTarget").objectReferenceValue;
                    string skin = property.FindPropertyRelative("skinName").stringValue;
                    SkinOp op = (SkinOp)property.FindPropertyRelative("skinOp").enumValueIndex;
                    string targetName = t != null ? t.name : "<no target>";
                    if (string.IsNullOrEmpty(skin)) skin = "<no skin>";
                    summary = $"SkinChange  {op}  '{skin}'  on  {targetName}";
                    break;
                }

            default:
                summary = type.ToString();
                break;
        }

        if (parallel) summary += "   ‖ parallel";
        return summary;
    }
}
