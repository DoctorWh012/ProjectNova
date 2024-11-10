using UnityEngine;
using UnityEngine.Events;

public class LR01AnimationEvents : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private UnityEvent fadeHeat;
    [SerializeField] private UnityEvent spinWeapon;

    public void FadeHeat()
    {
        fadeHeat.Invoke();
    }

    public void SpinWeapon()
    {
        spinWeapon.Invoke();
    }
}
