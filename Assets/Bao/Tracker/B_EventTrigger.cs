using UnityEngine;
using UnityEngine.Events;

public class B_EventTrigger : MonoBehaviour
{
    public UnityEvent eventToTrigger;

    public void Start()
    {
        eventToTrigger.Invoke();
    }
}
