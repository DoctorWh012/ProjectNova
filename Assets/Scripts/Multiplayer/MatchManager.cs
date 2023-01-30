using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Riptide;
using UnityEngine.SceneManagement;
using TMPro;

public class MatchManager : MonoBehaviour
{

    private static MatchManager _singleton;
    public static MatchManager Singleton
    {
        get => _singleton;
        private set
        {
            if (_singleton == null)
            {
                _singleton = value;
            }

            else if (_singleton != value)
            {
                Debug.Log($"{nameof(MatchManager)} instance already exists, destroying duplicate");
                Destroy(value.gameObject);
            }

        }
    }

    public enum TimerAction
    {
        StartMatch,
        PrepareForMatch,
        EndMatch,
        RestartMatch,
    }

    [Header("Settings")]
    [SerializeField] private float lobbyWaitTime;
    [SerializeField] private float matchStartWaitTime;
    [SerializeField] private float matchDurationTime;
    [SerializeField] private float matchRestartTime;

    private Coroutine matchCoroutine;

    private void Awake()
    {
        Singleton = this;
        DontDestroyOnLoad(gameObject);
    }

    private void StartMatch()
    {
        matchCoroutine = StartCoroutine(MatchCountDownTimer(lobbyWaitTime, GameCanvas.Instance.timerText, TimerAction.StartMatch));
    }

    private void CancelMatch()
    {
        StopCoroutine(matchCoroutine);
        GameCanvas.Instance.timerText.SetText($"");
    }

    public void ExitMatch()
    {
        NetworkManager.Singleton.Client.Disconnect();
        if (NetworkManager.Singleton.Server.IsRunning) NetworkManager.Singleton.Server.Stop();

        GameObject[] toBeDestroyed = GameObject.FindGameObjectsWithTag("Destroy");
        foreach (GameObject go in toBeDestroyed) Destroy(go);
        foreach (KeyValuePair<ushort, Player> player in Player.list) Destroy(player.Value.gameObject);

    }

    private void FreezeAllPlayerMovement(bool state)
    {
        foreach (KeyValuePair<ushort, Player> player in Player.list)
        {
            player.Value.Movement.FreezePlayerMovement(state);
        }
    }

    private void FreezeAllPlayerShooting(bool state)
    {
        foreach (KeyValuePair<ushort, Player> player in Player.list)
        {
            player.Value.GunShoot.FreezePlayerShooting(state);
        }
    }

    private void RespawnEveryone()
    {
        foreach (KeyValuePair<ushort, Player> player in Player.list)
        {
            player.Value.transform.position = SpawnHandler.Instance.GetSpawnLocation();
        }
    }

    private void DisableEnableALLPlayers(bool state)
    {
        foreach (KeyValuePair<ushort, Player> player in Player.list)
        {
            player.Value.gameObject.SetActive(state);
        }
    }

    public void SendMatchStartMessage(bool shouldStart)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.matchStart);
        message.AddBool(shouldStart);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    public void SendMatchOverMessage()
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.matchOver);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    [MessageHandler((ushort)ServerToClientId.matchStart)]
    private static void StartStopMatch(Message message)
    {
        if (message.GetBool()) MatchManager.Singleton.StartMatch();
        else MatchManager.Singleton.CancelMatch();
    }

    [MessageHandler((ushort)ServerToClientId.matchOver)]
    private static void EndMatch(Message message)
    {
        MatchManager.Singleton.StartCoroutine(MatchManager.Singleton.SwitchMatchScene("RiptideWinScreen", TimerAction.EndMatch));
    }

    private IEnumerator SwitchMatchScene(string sceneName, TimerAction action)
    {
        if (NetworkManager.Singleton.Server.IsRunning) { FreezeAllPlayerMovement(true); FreezeAllPlayerShooting(true); }

        SceneManager.LoadScene(sceneName);
        while (SceneManager.GetActiveScene().name != sceneName) yield return null;

        switch (action)
        {
            case TimerAction.StartMatch:
                StartCoroutine(MatchCountDownTimer(matchStartWaitTime, GameCanvas.Instance.bigPopUpText, TimerAction.PrepareForMatch));
                if (!NetworkManager.Singleton.Server.IsRunning) yield break;
                RespawnEveryone();
                break;

            case TimerAction.EndMatch:
                DisableEnableALLPlayers(false);
                GameCanvas.Instance.gameObject.SetActive(false);
                StartCoroutine(MatchCountDownTimer(matchRestartTime, null, TimerAction.RestartMatch));
                break;

            case TimerAction.RestartMatch:
                DisableEnableALLPlayers(true);
                RespawnEveryone();
                FreezeAllPlayerMovement(false);
                FreezeAllPlayerShooting(false);
                break;
        }
    }

    public IEnumerator MatchCountDownTimer(float countDownLenght, TextMeshProUGUI timerDisplay, TimerAction action)
    {
        float finishTime = Time.time + countDownLenght;
        string timerText;

        while (Time.time < finishTime)
        {
            if (action == TimerAction.StartMatch) timerText = $"Starting in {Mathf.Abs(Time.time - finishTime).ToString("0")}";
            else timerText = $"{Mathf.Abs(Time.time - finishTime).ToString("0")}";

            if (timerDisplay != null) { timerDisplay.SetText(timerText); }

            yield return null;
        }

        switch (action)
        {
            case TimerAction.StartMatch:
                GameCanvas.Instance.timerText.SetText($"");
                StartCoroutine(SwitchMatchScene("RiptideMultiplayer", TimerAction.StartMatch));
                break;

            case TimerAction.PrepareForMatch:
                GameCanvas.Instance.bigPopUpText.SetText("GO!");
                yield return new WaitForSeconds(1);
                GameCanvas.Instance.bigPopUpText.SetText("");
                StartCoroutine(MatchCountDownTimer(matchDurationTime, GameCanvas.Instance.timerText, TimerAction.EndMatch));

                if (!NetworkManager.Singleton.Server.IsRunning) break;
                FreezeAllPlayerMovement(false);
                FreezeAllPlayerShooting(false);
                break;

            case TimerAction.EndMatch:
                if (!NetworkManager.Singleton.Server.IsRunning) break;
                FreezeAllPlayerMovement(true);
                FreezeAllPlayerShooting(true);
                SendMatchOverMessage();
                break;

            case TimerAction.RestartMatch:
                Destroy(UIManager.Instance.gameObject);
                StartCoroutine(SwitchMatchScene("RiptideLobby", TimerAction.RestartMatch));
                break;
        }
    }
}
