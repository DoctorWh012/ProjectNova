using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Riptide;

public class LobbyManager : MonoBehaviour
{
    private static LobbyManager _singleton;
    public static LobbyManager Singleton
    {
        get => _singleton;
        private set
        {
            if (_singleton == null)
                _singleton = value;
            else if (_singleton != value)
            {
                Debug.Log($"{nameof(LobbyManager)} instance already exists, destroying object!");
                Destroy(value);
            }
        }
    }
    protected Callback<LobbyCreated_t> lobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> lobbyEnter;
    protected Callback<LobbyMatchList_t> Callback_lobbyList;
    protected Callback<LobbyDataUpdate_t> Callback_lobbyInfo;

    private const string HostAddressKey = "HostAddress";
    private CSteamID lobbyId;
    private List<CSteamID> lobbyIDS = new List<CSteamID>();

    private void Awake()
    {
        Singleton = this;
    }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam is not initialized!");
            return;
        }

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        Callback_lobbyList = Callback<LobbyMatchList_t>.Create(OnGetLobbiesList);
        Callback_lobbyInfo = Callback<LobbyDataUpdate_t>.Create(OnGetLobbiesInfo);
    }

    internal void CreateLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, NetworkManager.Singleton.maxClientCount);
        print("createdLobby");
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {

        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Failed to create steam lobby");
            SceneManager.LoadScene("Menu");
            return;
        }

        lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(lobbyId, HostAddressKey, SteamUser.GetSteamID().ToString());
        SteamMatchmaking.SetLobbyData(lobbyId, "name", "Generic Sigma Name");
        SteamMatchmaking.SetLobbyData(lobbyId, "status", "Sick Status");

        NetworkManager.Singleton.Server.Start(0, NetworkManager.Singleton.maxClientCount);
        NetworkManager.Singleton.Client.Connect("127.0.0.1");
    }

    internal void JoinLobby(ulong lobbyId)
    {
        SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEnter(LobbyEnter_t callback)
    {
        if (NetworkManager.Singleton.Server.IsRunning)
            return;

        lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        string hostAddress = SteamMatchmaking.GetLobbyData(lobbyId, HostAddressKey);

        NetworkManager.Singleton.Client.Connect(hostAddress);
    }

    internal void LeaveLobby()
    {
        NetworkManager.Singleton.StopServer();
        NetworkManager.Singleton.DisconnectClient();
        SteamMatchmaking.LeaveLobby(lobbyId);
    }

    public void GetLobbiesList()
    {
        if (lobbyIDS.Count > 0) MainMenu.Instance.DestroyOldLobbiesDiplays();

        SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
        SteamAPICall_t tyrGetList = SteamMatchmaking.RequestLobbyList();
    }

    internal void OnGetLobbiesList(LobbyMatchList_t result)
    {
        print($"Found {result.m_nLobbiesMatching} lobbies");
        for (int i = 0; result.m_nLobbiesMatching > i; i++)
        {
            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
            lobbyIDS.Add(lobbyID);
            SteamMatchmaking.RequestLobbyData(lobbyID);
        }
    }

    internal void OnGetLobbiesInfo(LobbyDataUpdate_t result)
    {
        print($"{result.m_ulSteamIDLobby}");
        MainMenu.Instance.CreateLobbyList(lobbyIDS, result);
    }

    public IEnumerator SendName()
    {
        string playerName;
        if (SteamManager.Initialized) playerName = SteamFriends.GetPersonaName();
        else playerName = "";

        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.name);
        message.AddString(playerName);

        if (SceneManager.GetActiveScene().name != "RiptideLobby") SceneManager.LoadScene("RiptideLobby");
        while (SceneManager.GetActiveScene().name != "RiptideLobby") yield return null;

        NetworkManager.Singleton.Client.Send(message);
    }
}
