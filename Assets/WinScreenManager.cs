using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WinScreenManager : MonoBehaviour
{
    [Serializable]
    private struct PlayerDisplayData
    {
        public GameObject displayGameObject;
        public Image playerAvatarImg;
        public TextMeshProUGUI playerNameTxt;
    }

    public static WinScreenManager Instance;

    [Header("Components")]
    [SerializeField] private GameObject freeForAllPlacing;

    [Header("Free For All Placing")]
    [SerializeField] private TextMeshProUGUI playerWonTxt;
    [SerializeField] private Image bestPlayerAvatar;
    [SerializeField] private TextMeshProUGUI bestPlayerTxt;

    [SerializeField] private PlayerDisplayData firstPlaceDisplayData;
    [SerializeField] private PlayerDisplayData secondPlaceDisplayData;
    [SerializeField] private PlayerDisplayData thirdPlaceDisplayData;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetupFreeForAllPlacement();
    }

    public void SetupFreeForAllPlacement()
    {
        secondPlaceDisplayData.displayGameObject.SetActive(false);
        thirdPlaceDisplayData.displayGameObject.SetActive(false);

        playerWonTxt.SetText($"[{MatchManager.playersOnLobby[MatchManager.playersPlacing[0]].playerName}]\n Wins!");
        bestPlayerAvatar.sprite = MatchManager.playersOnLobby[MatchManager.playersPlacing[0]].playerAvatar;
        bestPlayerTxt.SetText($"{MatchManager.playersOnLobby[MatchManager.playersPlacing[0]].playerName}");
        firstPlaceDisplayData.playerNameTxt.SetText(MatchManager.playersOnLobby[MatchManager.playersPlacing[0]].playerName);
        firstPlaceDisplayData.playerAvatarImg.sprite = MatchManager.playersOnLobby[MatchManager.playersPlacing[0]].playerAvatar;

        if (MatchManager.playersPlacing.Length < 2) return;
        secondPlaceDisplayData.displayGameObject.SetActive(true);
        secondPlaceDisplayData.playerNameTxt.SetText(MatchManager.playersOnLobby[MatchManager.playersPlacing[1]].playerName);
        secondPlaceDisplayData.playerAvatarImg.sprite = MatchManager.playersOnLobby[MatchManager.playersPlacing[1]].playerAvatar;

        if (MatchManager.playersPlacing.Length < 3) return;
        thirdPlaceDisplayData.displayGameObject.SetActive(true);
        thirdPlaceDisplayData.playerNameTxt.SetText(MatchManager.playersOnLobby[MatchManager.playersPlacing[2]].playerName);
        thirdPlaceDisplayData.playerAvatarImg.sprite = MatchManager.playersOnLobby[MatchManager.playersPlacing[2]].playerAvatar;
    }
}
