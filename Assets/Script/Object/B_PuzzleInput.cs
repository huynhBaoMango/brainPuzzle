using UnityEngine;

/// <summary>
/// Scene-wide pointer dispatcher. Routes press/drag/release to whichever
/// interactable or group wins the PickAt sort-order check.
/// Auto-created once per runtime — designers don't place anything by hand.
/// </summary>
[DisallowMultipleComponent]
public class B_PuzzleInput : MonoBehaviour
{
    private static B_PuzzleInput instance;

    private B_InteractableObject pressedObj;
    private B_InteractableGroup pressedGroup;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExists()
    {
        if (instance != null) return;
        GameObject go = new GameObject("[B_PuzzleInput]");
        instance = go.AddComponent<B_PuzzleInput>();
        DontDestroyOnLoad(go);
    }

    private bool hasRunStartCheck;

    private void LateUpdate()
    {
        // On the first frame after all Awake/Start calls have run,
        // fire reactive states so REQUIREMENT_MET with empty requirements
        // triggers level intro animations.
        if (!hasRunStartCheck)
        {
            hasRunStartCheck = true;
            B_InteractableObject.CheckReactiveStatesOnce();
        }
    }

    private void Update()
    {
        if (B_InteractableObject.ActionsRunning)
        {
            pressedObj = null;
            pressedGroup = null;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Vector2 world = MouseToWorld();
            B_InteractableObject.LayerPick pick =
                B_InteractableObject.PickAt(world, (B_InteractableObject)null);

            if (pick.group != null)
            {
                pressedGroup = pick.group;
                pressedGroup.HandlePress(world);
            }
            else if (pick.interactable != null)
            {
                pressedObj = pick.interactable;
                pressedObj.HandlePress(world);
            }
        }
        else if (Input.GetMouseButton(0))
        {
            Vector2 world = MouseToWorld();
            if (pressedObj != null) pressedObj.HandleDrag(world);
            else if (pressedGroup != null) pressedGroup.HandleDrag(world);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            Vector2 world = MouseToWorld();
            if (pressedObj != null)
            {
                pressedObj.HandleRelease(world);
                pressedObj = null;
            }
            else if (pressedGroup != null)
            {
                pressedGroup.HandleRelease(world);
                pressedGroup = null;
            }
        }
    }

    private static Vector2 MouseToWorld()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector2.zero;
        Vector3 s = Input.mousePosition;
        s.z = Mathf.Abs(cam.transform.position.z);
        Vector3 w = cam.ScreenToWorldPoint(s);
        return new Vector2(w.x, w.y);
    }
}
