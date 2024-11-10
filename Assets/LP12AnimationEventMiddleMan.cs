using UnityEngine;
using UnityEngine.Events;

public class LP12AnimationEventMiddleMan : MonoBehaviour
{
    [Header("Components")]
    [Space(5)]
    [SerializeField] private UnityEvent pumpIn;
    [SerializeField] private UnityEvent pumpOut;
    [SerializeField] private UnityEvent magOut;
    [SerializeField] private UnityEvent magFit;
    [SerializeField] private UnityEvent magPunch;

    public void PlayPumpInSFX()
    {
        pumpIn.Invoke();
    }

    public void PlayPumpOutSFX()
    {
        pumpOut.Invoke();
    }

    public void PlayMagOutEffects()
    {
        magOut.Invoke();
    }

    public void PlayMagFitSFX()
    {
        magFit.Invoke();
    }

    public void PlayMagPunch()
    {
        magPunch.Invoke();
    }
}
