using UnityEngine;
using UnityEngine.Events;

public class EventMiddleMan : MonoBehaviour
{
    [Header("Components")]
    public UnityEvent unityEvent;

    public void DoEvent()
    {
        unityEvent.Invoke();
    }
}
