using UnityEngine;
using System.Collections;

public class Interactable : MonoBehaviour
{
    private enum Action { PickUpGun, }
    [SerializeField] private Action desiredAction;

    private Coroutine waitCoroutine;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Player player = other.GetComponentInParent<Player>();
        if (player.IsLocal) GameCanvas.Instance.SetUiPopUpText($"Press [{player.multiplayerController.interact}] to PickUp the gun!");

        if (!NetworkManager.Singleton.Server.IsRunning) return;
        waitCoroutine = StartCoroutine(WaitForInteract(player));
    }

    private void OnTriggerExit(Collider other)
    {
        GameCanvas.Instance.SetUiPopUpText("");
        if (waitCoroutine != null) StopCoroutine(waitCoroutine);
    }

    private IEnumerator WaitForInteract(Player player)
    {
        while (!player.Movement.interact)
        {
            yield return null;
        }
        switch (desiredAction)
        {
            case Action.PickUpGun:
                GetComponent<GunSpawn>().PickUpTheGun(player);
                GameCanvas.Instance.SetUiPopUpText("");
                break;
        }
    }
}
