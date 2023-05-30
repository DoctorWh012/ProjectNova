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

    private void VerifyUIText(Player player)
    {
        if (player.IsLocal)
        {
            switch (desiredAction)
            {
                case Action.PickUpGun:
                    GameCanvas.Instance.SetUiPopUpText($"Press [E] to PickUp the gun!");
                    break;

                case Action.StartMatch:
                    GameCanvas.Instance.SetUiPopUpText($"Press [E] to Start the match!");
                    break;
            }

        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Interactor")) return;
        if (waitCoroutine != null) return;

        Player player = other.GetComponentInParent<Player>();
        VerifyUIText(player);

        waitCoroutine = StartCoroutine(WaitForInteract(player));
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Interactor") || waitCoroutine != null) return;

        Player player = other.GetComponentInParent<Player>();
        VerifyUIText(player);

        waitCoroutine = StartCoroutine(WaitForInteract(player));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Interactor")) return;
        if (waitCoroutine == null) return;

        // if (other.GetComponentInParent<Player>().Movement.rb.isKinematic) return;

        GameCanvas.Instance.SetUiPopUpText("");
        if (waitCoroutine != null) StopCoroutine(waitCoroutine);
        waitCoroutine = null;
    }

    private IEnumerator WaitForInteract(Player player)
    {
        while (!player.playerMovement.interacting)
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
