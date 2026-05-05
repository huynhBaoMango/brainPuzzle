using UnityEngine;

public class DontDestroyThis : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }
}
