using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerEffects : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private Animator playerAnimator;

    public void PlayerAnimator(int[] inputs, bool isSliding)
    {
        if (player.IsLocal) return;
        if (isSliding) { playerAnimator.Play("Slide"); return; }
        switch (inputs[0])
        {
            case 1:
                playerAnimator.Play("Run");
                return;
            case -1:
                playerAnimator.Play("RunBackwards");
                return;
        }
        switch (inputs[1])
        {
            case 1:
                playerAnimator.Play("RunRight");
                return;
            case -1:
                playerAnimator.Play("RunLeft");
                return;
        }
        playerAnimator.Play("Idle");
    }

    private void PlayAnimation(string animation)
    {
        if (!playerAnimator.GetCurrentAnimatorStateInfo(0).IsName(animation))
        {
            playerAnimator.Play(animation);
        }
    }
}
