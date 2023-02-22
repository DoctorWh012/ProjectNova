using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerEffects : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] public ParticleSystem jumpSmokeParticle;
    [SerializeField] private AudioClip jumpSFX;
    [SerializeField] public ParticleSystem slideGrindParticle;
    [SerializeField] public AudioSource slideGrindAudioSRC;
    [SerializeField] private AudioSource mainAudioSRC;
    [SerializeField] private AudioClip[] walkingSFX;
    private bool isGrinding = false;

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

    public void PlayWalkingSound()
    {
        AudioClip audio = walkingSFX[Random.Range(0, walkingSFX.Length)];
        mainAudioSRC.PlayOneShot(audio, Random.Range(-0.1f, 0.1f));
    }

    public void PlayJumpEffects()
    {
        jumpSmokeParticle.Play();
    }

    public void PlaySlideEffects(bool state)
    {

        if (state && !isGrinding)
        {
            slideGrindParticle.Play();
            slideGrindAudioSRC.Play();
            isGrinding = true;
        }
        else if (!state && isGrinding)
        {
            slideGrindParticle.Stop();
            slideGrindAudioSRC.Stop();
            isGrinding = false;
        }
    }
}
