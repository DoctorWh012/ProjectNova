using UnityEngine;
using TMPro;

public class PlayerScoreCapsule : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color netColor;
    [SerializeField] private Color localColor;

    [Header("Components")]
    [SerializeField] private TextMeshProUGUI playerNameTxt;
    [SerializeField] public TextMeshProUGUI playerKDTxt;
    [SerializeField] public TextMeshProUGUI playerPingTxt;

    private ushort playerId;

    public void SetUpCapsule(ushort id, string playerName)
    {
        playerId = id;
        playerNameTxt.SetText(playerName);

    }
}
