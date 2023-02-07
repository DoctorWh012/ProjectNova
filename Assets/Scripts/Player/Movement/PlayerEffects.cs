using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerEffects : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] public ParticleSystem jumpSmokeParticle;
    [SerializeField] public ParticleSystem slideGrindParticle;
    [SerializeField] public AudioSource slideGrindAudioSRC;

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

    public void PlayJumpEffects()
    {
        jumpSmokeParticle.Play();
        PlayerCam.Instance.TiltCamera(false, 0, 0, 0.2f);
    }

    public void PlaySlideEffects(bool state)
    {
        if (state && !slideGrindParticle.isPlaying) { slideGrindParticle.Play(); slideGrindAudioSRC.Play(); }
        else if (!state && slideGrindParticle.isPlaying) { slideGrindParticle.Stop(); slideGrindAudioSRC.Stop(); }
    }
}
