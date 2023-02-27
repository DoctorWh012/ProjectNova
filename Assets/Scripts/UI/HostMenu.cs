using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using TMPro;
using Steamworks;

public class HostMenu : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TMP_InputField lobbyName;
    [SerializeField] private Slider playerCountSlider;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private CarouselUI.CarouselUIElement lobbyTypeUI;

    public void HostClicked()
    {
        ELobbyType lobbyType;
        if (lobbyTypeUI.CurrentIndex == 0) lobbyType = ELobbyType.k_ELobbyTypePublic;
        else lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly;

        string lobbyNm;
        if (!string.IsNullOrEmpty(lobbyName.text)) lobbyNm = lobbyName.text.Trim();
        else lobbyNm = "GERSO'S LOBBY";
        LobbyManager.Singleton.CreateLobby(lobbyType, (int)playerCountSlider.value, lobbyNm);
    }

    public void UpdateSliderText(float num)
    {
        playerCountText.SetText(num.ToString());
    }
}
