using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Riptide;
using UnityEngine.SceneManagement;

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
                Destroy(value);
            }

        }
    }

    [Header("Settings")]
    [SerializeField] private float lobbyWaitTime;
    [SerializeField] private float matchStartWaitTime;

    private Coroutine matchCoroutine;

    private void Awake()
    {
        Singleton = this;
        DontDestroyOnLoad(gameObject);
    }

    private void StartMatch()
    {
        matchCoroutine = StartCoroutine(PrepareMatch());
    }

    private void CancelMatch()
    {
        StopCoroutine(matchCoroutine);
        GameCanvas.Instance.timerText.SetText($"");
    }

    private IEnumerator SwitchMatchScene()
    {
        if (NetworkManager.Singleton.Server.IsRunning) FreezeAllPlayerMovement(true);
        
        GameCanvas.Instance.timerText.SetText($"");
        SceneManager.LoadScene("RiptideMultiplayer");
        while (SceneManager.GetActiveScene().name != "RiptideMultiplayer") yield return null;

        StartCoroutine(MatchStartCountDownTimer(matchStartWaitTime));

        if (!NetworkManager.Singleton.Server.IsRunning) yield break;
        RespawnEveryone();
    }

    private void FreezeAllPlayerMovement(bool state)
    {
        foreach (KeyValuePair<ushort, Player> player in Player.list)
        {
            player.Value.Movement.FreezePlayerMovement(state);
        }
    }

    private void RespawnEveryone()
    {
        foreach (KeyValuePair<ushort, Player> player in Player.list)
        {
            player.Value.transform.position = SpawnHandler.Instance.GetSpawnLocation();
        }
    }

    public void SendMatchStartMessage(bool shouldStart)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ServerToClientId.matchStart);
        message.AddBool(shouldStart);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    [MessageHandler((ushort)ServerToClientId.matchStart)]
    private static void StartStopMatch(Message message)
    {
        if (message.GetBool()) MatchManager.Singleton.StartMatch();
        else MatchManager.Singleton.CancelMatch();
    }

    public IEnumerator PrepareMatch()
    {
        float startTime = Time.time + lobbyWaitTime;
        while (Time.time < startTime)
        {
            GameCanvas.Instance.timerText.SetText($"Starting in {Mathf.Abs(Time.time - startTime).ToString("0")}");
            yield return null;
        }
        StartCoroutine(SwitchMatchScene());
    }

    public IEnumerator MatchStartCountDownTimer(float countDownLenght)
    {
        float finishTime = Time.time + countDownLenght;

        while (Time.time < finishTime)
        {
            GameCanvas.Instance.bigPopUpText.SetText($"{Mathf.Abs(Time.time - finishTime).ToString("0")}");
            yield return null;
        }
        GameCanvas.Instance.bigPopUpText.SetText("GO!");
        yield return new WaitForSeconds(1);
        GameCanvas.Instance.bigPopUpText.SetText("");

        if (NetworkManager.Singleton.Server.IsRunning) FreezeAllPlayerMovement(false);
    }


}
