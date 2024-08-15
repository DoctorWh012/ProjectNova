using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerScoreCapsule : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color netColor;
    [SerializeField] private Color localColor;

    [Header("Components")]
    [SerializeField] private Image capsuleBackground;
    [SerializeField] public Image playerImg;
    [SerializeField] private TextMeshProUGUI playerNameTxt;
    [SerializeField] public TextMeshProUGUI playerKDTxt;
    [SerializeField] public TextMeshProUGUI playerPingTxt;

    [SerializeField] public ushort playerId;

    public void SetUpCapsule(ushort id, string playerName, Sprite image, int kills, int deaths)
    {
        playerId = id;
        capsuleBackground.color = id == NetworkManager.Singleton.Client.Id ? localColor : netColor;
        playerNameTxt.SetText(playerName);
        playerImg.sprite = image;
        playerKDTxt.SetText($"{kills} / {deaths}");
    }
}
