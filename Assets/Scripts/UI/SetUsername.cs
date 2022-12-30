using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SetUsername : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] Player player;
    [SerializeField] TextMeshProUGUI text;
    // Start is called before the first frame update
    void Start()
    {
        text.SetText(player.username);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
