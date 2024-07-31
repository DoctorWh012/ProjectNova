using UnityEngine;
using TMPro;
using DG.Tweening;


public class DamageIndicator : MonoBehaviour
{
    [Header("Components")]
    [Space(5)]
    [SerializeField] private TextMeshPro indicator;
    [SerializeField] private Color criticalHitColor;

    [Header("Settings")]
    [Space(5)]
    [SerializeField] private Vector3 startingScale;
    [SerializeField] private Vector3 finalScale;
    [SerializeField] private float scaleTime;

    public void DisplayDamage(int dmg, bool critical)
    {
        if(critical) indicator.color = criticalHitColor;
        indicator.SetText(dmg.ToString());
        indicator.transform.localScale = startingScale;
        indicator.transform.DOPunchScale(finalScale, scaleTime, 0, 0).SetEase(Ease.OutCirc).OnComplete(() => Destroy(gameObject));
    }
}
