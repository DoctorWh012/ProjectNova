using UnityEngine;
using TMPro;

public class KeybindButton : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] public KeyCode key;
    [SerializeField] private TextMeshProUGUI keyTxt;

    public void SetKey(KeyCode newKey)
    {
        key = newKey;
        keyTxt.SetText(newKey.ToString());
    }
}
