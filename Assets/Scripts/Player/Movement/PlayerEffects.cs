using UnityEngine;

public class PlayerEffects : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] public ParticleSystem jumpSmokeParticle;
    [SerializeField] private AudioClip jumpSFX;
    [SerializeField] public ParticleSystem slideGrindParticle;
    [SerializeField] public AudioSource slideGrindAudioSRC;
    [SerializeField] private AudioSource mainAudioSRC;
    [SerializeField] private AudioClip[] walkingSFX;
    [SerializeField] private ParticleSystem speedLinesEffect;

    [Header("Settings")]
    [SerializeField] private float speedLineStartAtSpeed;
    [SerializeField] private float speedLineMultiplier;
    [SerializeField] private float speedLineSpoolTime;

    private bool isGrinding = false;

    private bool isLerpingUp = false;
    private ParticleSystem.EmissionModule emission;
    private float lerpDuration = 0;

    private void Start()
    {
        emission = speedLinesEffect.emission;
    }

    private void FixedUpdate()
    {
        if (player.IsLocal) UpdateSpeedLinesEmission();
    }

    private void UpdateSpeedLinesEmission()
    {
        float speed = playerMovement.rb.velocity.magnitude;

        if (speed < speedLineStartAtSpeed && emission.rateOverTimeMultiplier > 0)
        {
            if (isLerpingUp) { lerpDuration = 0; isLerpingUp = false; }
            emission.rateOverTime = Mathf.Lerp(emission.rateOverTimeMultiplier, 0, lerpDuration / speedLineSpoolTime);
            lerpDuration += Time.deltaTime;
        }

        else if (speed > speedLineStartAtSpeed)
        {
            if (!isLerpingUp) { lerpDuration = 0; isLerpingUp = true; }
            emission.rateOverTime = Mathf.Lerp(emission.rateOverTimeMultiplier, Mathf.Abs(speed * speedLineMultiplier), Time.deltaTime / (speedLineSpoolTime / 2));
            lerpDuration += Time.deltaTime;
        }
    }


    public void PlayerAnimator(float horizontal, float vertical, bool isSliding)
    {
        // if (player.IsLocal) return;
        if (isSliding) { playerAnimator.Play("Slide"); return; }
        switch (vertical)
        {
            case 1:
                playerAnimator.Play("Run");
                return;
            case -1:
                playerAnimator.Play("RunBackwards");
                return;
        }
        switch (horizontal)
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
