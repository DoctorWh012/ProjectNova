using UnityEngine;
using UnityEngine.Events;

public class EventMiddleMan : MonoBehaviour
{
    [SerializeField] private UnityEvent call;

    public void CallEvent()
    {
        call.Invoke();
    }

}
