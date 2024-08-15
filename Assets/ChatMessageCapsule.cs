using UnityEngine;
using TMPro;

public class ChatMessageCapsule : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private TextMeshProUGUI chatMessageTxt;

    public void SetupChatMessage(string chatMessage)
    {
        chatMessageTxt.SetText(chatMessage);
        Invoke(nameof(DeactivateMessage), 3);
    }

    public void ActivateMessage()
    {
        gameObject.SetActive(true);
        CancelInvoke(nameof(DeactivateMessage));
    }

    public void DeactivateMessage()
    {
        gameObject.SetActive(false);
    }
}
