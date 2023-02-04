using UnityEngine;
using System.Collections;

public class Interactable : MonoBehaviour
{
    private enum Action { PickUpGun, StartMatch }
    [SerializeField] private Action desiredAction;

    private Coroutine waitCoroutine;
    private Animator animator;
    private bool matchStart = false;

    private void Start()
    {
        if (desiredAction == Action.StartMatch) animator = GetComponent<Animator>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Player player = other.GetComponentInParent<Player>();
        if (player.IsLocal)
        {
            switch (desiredAction)
            {
                case Action.PickUpGun:
                    GameCanvas.Instance.SetUiPopUpText($"Press [{player.multiplayerController.interact}] to PickUp the gun!");
                    break;

                case Action.StartMatch:
                    GameCanvas.Instance.SetUiPopUpText($"Press [{player.multiplayerController.interact}] to Start the match!");
                    break;
            }

        }

        if (!NetworkManager.Singleton.Server.IsRunning) return;
        waitCoroutine = StartCoroutine(WaitForInteract(player));
    }

    private void OnTriggerStay(Collider other)
    {
        if (waitCoroutine != null) return;
        if (!other.CompareTag("Player")) return;

        Player player = other.GetComponentInParent<Player>();
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

            case Action.StartMatch:
                animator.Play("Press");
                if (!NetworkManager.Singleton.Server.IsRunning) break;
                if (!player.IsLocal) break;
                
                matchStart = !matchStart;
                MatchManager.Singleton.SendMatchStartMessage(matchStart);
                yield return new WaitForSeconds(1);
                break;
        }
        waitCoroutine = null;
    }
}
