using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoreBoardItem : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI playerText;
    [SerializeField] TextMeshProUGUI kdText;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void Initialize(Player player)
    {
        playerText.SetText(player.username);
    }


    public void UpdateKD(int deaths, int kills)
    {
        kdText.SetText($"{kills}/{deaths}");
    }
}
