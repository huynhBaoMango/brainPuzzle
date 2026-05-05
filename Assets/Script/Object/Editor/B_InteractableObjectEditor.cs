using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for B_InteractableObject. Surfaces validation problems
/// (empty ids, duplicate state ids, NONE triggers) as colored HelpBoxes
/// at the top so non-coding designers can spot mistakes immediately.
/// </summary>
[CustomEditor(typeof(B_InteractableObject))]
public class B_InteractableObjectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty objectIdProp = serializedObject.FindProperty("objectId");
        SerializedProperty dataProp = serializedObject.FindProperty("data");

        DrawHeaderBanner(objectIdProp);
        DrawValidationMessages(objectIdProp, dataProp);

        EditorGUILayout.Space(4f);
        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }

    // Big id banner at the very top — the most important field for designers.
    private void DrawHeaderBanner(SerializedProperty objectIdProp)
    {
        string id = objectIdProp.stringValue;
        string display = string.IsNullOrEmpty(id) ? "<no id>" : id;

        Rect rect = GUILayoutUtility.GetRect(0f, 28f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.22f, 0.28f));

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 13;
        style.normal.textColor = string.IsNullOrEmpty(id)
            ? new Color(1f, 0.6f, 0.4f)
            : new Color(0.7f, 0.95f, 1f);

        EditorGUI.LabelField(rect, $"OBJECT  ◈  {display}", style);
    }

    private void DrawValidationMessages(SerializedProperty objectIdProp, SerializedProperty dataProp)
    {
        if (string.IsNullOrEmpty(objectIdProp.stringValue))
        {
            EditorGUILayout.HelpBox(
                "Object Id is empty. Other objects' state requirements cannot reference this object until you give it an id.",
                MessageType.Warning);
        }

        if (dataProp == null) return;
        SerializedProperty statesProp = dataProp.FindPropertyRelative("states");
        if (statesProp == null || !statesProp.isArray) return;

        var seenIds = new HashSet<string>();
        var duplicates = new HashSet<string>();
        int emptyIds = 0;
        int noneTriggers = 0;
        int dragMissingZone = 0;

        for (int i = 0; i < statesProp.arraySize; i++)
        {
            SerializedProperty state = statesProp.GetArrayElementAtIndex(i);
            string stateId = state.FindPropertyRelative("stateId").stringValue;
            int triggerIdx = state.FindPropertyRelative("trigger").enumValueIndex;
            string zone = state.FindPropertyRelative("requiredZoneId").stringValue;

            if (string.IsNullOrEmpty(stateId)) emptyIds++;
            else if (!seenIds.Add(stateId)) duplicates.Add(stateId);

            if ((InteractType)triggerIdx == InteractType.NONE) noneTriggers++;
            if ((InteractType)triggerIdx == InteractType.DRAG && string.IsNullOrEmpty(zone))
                dragMissingZone++;
        }

        if (emptyIds > 0)
            EditorGUILayout.HelpBox(
                $"{emptyIds} state(s) have an empty State Id and cannot be referenced by Requirements.",
                MessageType.Warning);

        if (duplicates.Count > 0)
            EditorGUILayout.HelpBox(
                "Duplicate State Ids: " + string.Join(", ", duplicates),
                MessageType.Error);

        if (noneTriggers > 0)
            EditorGUILayout.HelpBox(
                $"{noneTriggers} state(s) have Trigger = NONE. They will never activate from player input.",
                MessageType.Info);

        if (dragMissingZone > 0)
            EditorGUILayout.HelpBox(
                $"{dragMissingZone} DRAG state(s) have no Required Zone Id. They will activate on any drop position.",
                MessageType.Info);

        // ==============================================================
        //  CROSS-OBJECT VALIDATION
        // ==============================================================

        DrawCrossObjectValidation(statesProp);
    }

    // Validates references that point to other objects / zones in the scene.
    private void DrawCrossObjectValidation(SerializedProperty statesProp)
    {
        // Cache scene data once per inspector draw.
        string[] sceneObjectIds = PuzzleEditorHelper.GetAllObjectIds();
        string[] sceneZoneIds = PuzzleEditorHelper.GetAllZoneIds();

        var brokenReqs = new List<string>();
        var brokenZones = new List<string>();
        var brokenActivate = new List<string>();

        for (int i = 0; i < statesProp.arraySize; i++)
        {
            SerializedProperty state = statesProp.GetArrayElementAtIndex(i);
            string sid = state.FindPropertyRelative("stateId").stringValue;
            string stateLabel = string.IsNullOrEmpty(sid) ? $"State [{i}]" : sid;

            // ---- Check requirements ----
            SerializedProperty reqs = state.FindPropertyRelative("requirements");
            if (reqs != null && reqs.isArray)
            {
                for (int r = 0; r < reqs.arraySize; r++)
                {
                    SerializedProperty req = reqs.GetArrayElementAtIndex(r);
                    string reqObjId = req.FindPropertyRelative("objectId").stringValue;
                    string reqStateId = req.FindPropertyRelative("stateId").stringValue;

                    if (string.IsNullOrEmpty(reqObjId)) continue;

                    // Check objectId exists in scene.
                    if (System.Array.IndexOf(sceneObjectIds, reqObjId) < 0)
                    {
                        brokenReqs.Add($"{stateLabel}: requirement references object \"{reqObjId}\" which doesn't exist in the scene.");
                        continue;
                    }

                    // Check stateId exists on that object.
                    if (!string.IsNullOrEmpty(reqStateId))
                    {
                        string[] targetStates = PuzzleEditorHelper.GetStateIds(reqObjId);
                        if (System.Array.IndexOf(targetStates, reqStateId) < 0)
                            brokenReqs.Add($"{stateLabel}: requirement references state \"{reqStateId}\" on \"{reqObjId}\" which doesn't exist.");
                    }
                }
            }

            // ---- Check requiredZoneId ----
            int triggerIdx = state.FindPropertyRelative("trigger").enumValueIndex;
            if ((InteractType)triggerIdx == InteractType.DRAG)
            {
                string zoneId = state.FindPropertyRelative("requiredZoneId").stringValue;
                if (!string.IsNullOrEmpty(zoneId) && System.Array.IndexOf(sceneZoneIds, zoneId) < 0)
                    brokenZones.Add($"{stateLabel}: required zone \"{zoneId}\" doesn't exist in the scene.");
            }

            // ---- Check ActivateState actions ----
            SerializedProperty actions = state.FindPropertyRelative("actions");
            if (actions != null && actions.isArray)
            {
                for (int a = 0; a < actions.arraySize; a++)
                {
                    SerializedProperty action = actions.GetArrayElementAtIndex(a);
                    int actionType = action.FindPropertyRelative("type").enumValueIndex;
                    if ((StateActionType)actionType != StateActionType.ActivateState) continue;

                    Object targetRef = action.FindPropertyRelative("activateTarget").objectReferenceValue;
                    string actStateId = action.FindPropertyRelative("activateStateId").stringValue;

                    if (targetRef == null && !string.IsNullOrEmpty(actStateId))
                    {
                        brokenActivate.Add($"{stateLabel}: ActivateState has state id \"{actStateId}\" but no target interactable assigned.");
                        continue;
                    }

                    if (targetRef != null && !string.IsNullOrEmpty(actStateId))
                    {
                        B_InteractableObject targetObj = targetRef as B_InteractableObject;
                        if (targetObj != null && !string.IsNullOrEmpty(targetObj.ObjectId))
                        {
                            string[] targetStates = PuzzleEditorHelper.GetStateIds(targetObj.ObjectId);
                            if (System.Array.IndexOf(targetStates, actStateId) < 0)
                                brokenActivate.Add($"{stateLabel}: ActivateState targets \"{targetObj.ObjectId}.{actStateId}\" but that state doesn't exist.");
                        }
                    }
                }
            }
        }

        // Display collected warnings.
        foreach (string msg in brokenReqs)
            EditorGUILayout.HelpBox(msg, MessageType.Warning);
        foreach (string msg in brokenZones)
            EditorGUILayout.HelpBox(msg, MessageType.Warning);
        foreach (string msg in brokenActivate)
            EditorGUILayout.HelpBox(msg, MessageType.Error);
    }
}
