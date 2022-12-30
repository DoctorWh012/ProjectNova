using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Riptide;
public class ScoreBoard : MonoBehaviour
{
    public static ScoreBoard Instance;
    Dictionary<Player, ScoreBoardItem> scoreboardItems = new Dictionary<Player, ScoreBoardItem>();
    [SerializeField] private Transform container;
    [SerializeField] private GameObject scoreboardItemPrefab;


    private void Awake() => Instance = this;

    public void AddScoreBoarditem(Player player)
    {
        ScoreBoardItem item = Instantiate(scoreboardItemPrefab, container).GetComponent<ScoreBoardItem>();
        item.Initialize(player);
        scoreboardItems[player] = item;
    }

    public void RemoveScoreBoardItem(Player player)
    {
        if (!player.IsLocal)
        {
            Destroy(scoreboardItems[player].gameObject);
            scoreboardItems.Remove(player);
        }
    }

    #region Messages
    [MessageHandler((ushort)ServerToClientId.playerScore)]
    private static void UpdateScore(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (player == null) Debug.LogError("GODDAMIT");
            ScoreBoard.Instance.scoreboardItems[player].UpdateKD(message.GetInt(), message.GetInt());
        }
    }

    #endregion
}
