using UnityEngine;

public class StartMatchBtn : Interactable
{
    [Header("Components")]
    [SerializeField] private Animator animator;

    private bool canPress = true;

    private void Update()
    {
        if (!NetworkManager.Singleton.Server.IsRunning) return;
        if (players.Count == 0) return;

        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Id != NetworkManager.Singleton.Client.Id) return;
            if (players[i].playerInteractions.interactTimeCounter > 0 && canPress)
            {
                players[i].playerHud.OpenCloseMatchSettingsMenu();
                canPress = false;
                Invoke("RestorePress", 0.5f);
            }
        }
    }

    private void RestorePress()
    {
        canPress = true;
    }
}
