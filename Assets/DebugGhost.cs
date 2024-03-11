using UnityEngine;
using TMPro;

public class DebugGhost : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Material currentPosMat;
    [SerializeField] private Material rewoundPosMat;
    [SerializeField] private TextMeshProUGUI tickText;

    public void SetupGhost(bool rewound, uint tick)
    {
        Material mat = rewound ? rewoundPosMat : currentPosMat;
        foreach (SkinnedMeshRenderer meshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>()) meshRenderer.material = mat;
        tickText.SetText(tick.ToString());
    }
}
