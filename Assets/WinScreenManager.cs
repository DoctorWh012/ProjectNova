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

        playerWonTxt.SetText($"[{GameManager.playersOnLobby[GameManager.playersPlacingFFA[0]].playerName}]\n Wins!");
        bestPlayerAvatar.sprite = GameManager.playersOnLobby[GameManager.playersPlacingFFA[0]].playerAvatar;
        bestPlayerTxt.SetText($"{GameManager.playersOnLobby[GameManager.playersPlacingFFA[0]].playerName}");
        firstPlaceDisplayData.playerNameTxt.SetText(GameManager.playersOnLobby[GameManager.playersPlacingFFA[0]].playerName);
        firstPlaceDisplayData.playerAvatarImg.sprite = GameManager.playersOnLobby[GameManager.playersPlacingFFA[0]].playerAvatar;

        if (GameManager.playersPlacingFFA.Length < 2) return;
        secondPlaceDisplayData.displayGameObject.SetActive(true);
        secondPlaceDisplayData.playerNameTxt.SetText(GameManager.playersOnLobby[GameManager.playersPlacingFFA[1]].playerName);
        secondPlaceDisplayData.playerAvatarImg.sprite = GameManager.playersOnLobby[GameManager.playersPlacingFFA[1]].playerAvatar;

        if (GameManager.playersPlacingFFA.Length < 3) return;
        thirdPlaceDisplayData.displayGameObject.SetActive(true);
        thirdPlaceDisplayData.playerNameTxt.SetText(GameManager.playersOnLobby[GameManager.playersPlacingFFA[2]].playerName);
        thirdPlaceDisplayData.playerAvatarImg.sprite = GameManager.playersOnLobby[GameManager.playersPlacingFFA[2]].playerAvatar;
    }
}
