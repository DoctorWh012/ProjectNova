using UnityEngine;
using UnityEngine.Events;

public class WeaponEventMiddleMan : MonoBehaviour
{
    [SerializeField] private UnityEvent finishShooting;
    [SerializeField] private UnityEvent finishSwitching;

    public void CallFinishShooting()
    {
        finishShooting.Invoke();
    }

    public void CallFinishSwitching()
    {
        finishSwitching.Invoke();
    }
}
