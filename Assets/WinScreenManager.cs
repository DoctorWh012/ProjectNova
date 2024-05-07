using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WinScreenManager : MonoBehaviour
{
    private struct PlayerDisplayData
    {
        public Image playerAvatarImg;
        public TextMeshProUGUI playerNameTxt;
    }

    public static WinScreenManager Instance;

    [Header("Placing Board")]
    [SerializeField] private PlayerDisplayData firstPlaceDisplayData;
    [SerializeField] private PlayerDisplayData secondPlaceDisplayData;
    [SerializeField] private PlayerDisplayData thirdPlaceDisplayData;

    private void Awake()
    {
        Instance = this;
    }

    
}
