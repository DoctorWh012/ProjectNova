using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class KillFeedDisplay : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] public TextMeshProUGUI killerTxt;
    [SerializeField] public Image killMethodImg;
    [SerializeField] public TextMeshProUGUI victimTxt;
}
