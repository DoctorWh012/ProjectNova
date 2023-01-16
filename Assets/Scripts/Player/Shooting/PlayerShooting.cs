using System.Collections;
using UnityEngine;
using Riptide;
using EZCameraShake;

public class PlayerShooting : MonoBehaviour
{
    public bool isReloading { get; private set; } = false;
    public int activeGun { get; private set; }
    public WeaponType activeWeaponType { get; private set; }


    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] public GunComponents[] gunsSettings;
    [SerializeField] public MeleeComponents[] meleeSettings;
    [SerializeField] private HeadBobController headBobController;
    [SerializeField] private Camera scopeCam;

    [Space]
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitMarkerSfx;
    [SerializeField] private AudioClip spinSFX;
    [SerializeField] private AudioClip reloadSFX;

    private Transform barrelTip;
    private Animator animator;
    private ParticleSystem weaponEffectParticle;
    private int range;

    // Start is called before the first frame update
    void Awake()
    {
        SendGunSettings(0);
    }

    public void SwitchGun(int index)
    {
        activeGun = index;
        activeWeaponType = gunsSettings[index].gunSettings.weaponType;
        barrelTip = gunsSettings[index].barrelTip;
        animator = gunsSettings[index].animator;
        weaponEffectParticle = gunsSettings[index].muzzleFlash;
        EnableActiveGunMesh(index);
    }

    public void SwitchMelee(int index)
    {
        activeGun = index;
        activeWeaponType = meleeSettings[index].meleeSettings.weaponType;
        animator = meleeSettings[index].animator;
        weaponEffectParticle = meleeSettings[index].meleeParticles;
        EnableActiveGunMesh(index);
    }

    public void EnableActiveGunMesh(int index)
    {
        DisableAllMeleeMesh();
        DisableAllGunMeshes();
        if (activeWeaponType != WeaponType.melee)
        {
            foreach (MeshRenderer mesh in gunsSettings[index].gunMesh)
            {
                mesh.enabled = true;
            }
            gunsSettings[index].gunTrail.enabled = true;

            if (!gunsSettings[index].gunSettings.canAim || !player.IsLocal) return;
            gunsSettings[index].scopeMesh.enabled = true;
            scopeCam.enabled = true;
            scopeCam.fieldOfView = gunsSettings[index].gunSettings.scopeFov;
            return;
        }

        foreach (MeshRenderer mesh in meleeSettings[index].meleeMesh)
        {
            mesh.enabled = true;
        }

    }

    public void DisableAllGunMeshes()
    {
        for (int i = 0; i < gunsSettings.Length; i++)
        {
            foreach (MeshRenderer mesh in gunsSettings[i].gunMesh) { mesh.enabled = false; }

            gunsSettings[i].gunTrail.enabled = false;

            if (!gunsSettings[i].gunSettings.canAim || !player.IsLocal) continue;
            gunsSettings[i].scopeMesh.enabled = false;
            scopeCam.enabled = false;
        }
    }

    public void DisableAllMeleeMesh()
    {
        for (int i = 0; i < meleeSettings.Length; i++)
        {
            foreach (MeshRenderer mesh in meleeSettings[i].meleeMesh) { mesh.enabled = false; }
        }
    }

    public void AimDownSight(bool aim)
    {
        if (!gunsSettings[activeGun].gunSettings.canAim) return;
        gunsSettings[activeGun].gunSway.ResetGunPosition();
        gunsSettings[activeGun].gunSway.enabled = !aim;
        headBobController.InstantlyResetGunPos();
        headBobController.gunCambob = !aim;
        animator.SetBool("Aiming", aim);
    }

    public void ShootingAnimator(bool shouldPlay, bool playerIsLocal)
    {
        if (shouldPlay) SoundManager.Instance.PlaySound(playerAudioSource, gunsSettings[activeGun].gunShootSounds[0]);
        if (playerIsLocal && shouldPlay) ShootShaker();
        weaponEffectParticle.Play();
        animator.Play("Recoil");
    }

    public void MeleeAtackAnimator()
    {
        animator.Play("Attack");
        SoundManager.Instance.PlaySound(playerAudioSource, meleeSettings[activeGun].meleeSounds[0]);
        weaponEffectParticle.Play();
    }

    private void HitParticle(Vector3 hitPos)
    {
        Instantiate(GameLogic.Singleton.HitPrefab, hitPos, Quaternion.identity);
    }

    public void BulletTrailEffect(bool didHit, Vector3 hitPos, Vector2 spread)
    {
        // If the raycast hit something places the GameObject at rayHit.point
        if (didHit)
        {
            HitParticle(hitPos);
            TrailRenderer tracer = Instantiate(GameLogic.Singleton.ShotTrail, barrelTip.position, Quaternion.identity);
            tracer.AddPosition(barrelTip.position);
            tracer.transform.position = hitPos;
        }

        // If it didn't hit something just moves the GameObject foward
        else
        {
            TrailRenderer tracer = Instantiate(GameLogic.Singleton.ShotTrail, barrelTip.position, Quaternion.LookRotation(barrelTip.forward));
            tracer.AddPosition(barrelTip.position);
            tracer.transform.position += (barrelTip.forward + new Vector3(spread.x, spread.y, 0)) * range;
        }
    }

    private void HitEffect(Vector3 position)
    {
        ParticleSystem hitEffect = Instantiate(GameLogic.Singleton.PlayerHitPrefab, position, Quaternion.identity);
    }

    private void PlayHitmarker(bool shouldPlay)
    {
        if (shouldPlay) SoundManager.Instance.PlaySound(audioSource, hitMarkerSfx);
    }

    private void ShootShaker()
    {
        GunComponents gun = gunsSettings[activeGun];
        for (int i = 0; i < gun.shakeAmmount; i++)
        {
            CameraShaker.Instance.ShakeOnce(gun.shakeIntensity, gun.shakeRoughness, gun.fadeinTime, gun.fadeOutTime);
        }
    }

    // Messages
    #region Messages
    public void SendGunSettings(int index)
    {
        Message message = Message.Create(MessageSendMode.Reliable, ClientToServerId.gunChange);
        message.AddInt(index);
        NetworkManager.Singleton.Client.Send(message);
    }

    [MessageHandler((ushort)ServerToClientId.playerShot)]
    private static void PlayerShot(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerShooting.BulletTrailEffect(message.GetBool(), message.GetVector3(), message.GetVector2());
            player.playerShooting.ShootingAnimator(message.GetBool(), player.IsLocal);
        }
    }

    [MessageHandler((ushort)ServerToClientId.meleeAtack)]
    private static void PlayerAtackedMelee(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerShooting.MeleeAtackAnimator();
            if (message.GetBool()) player.playerShooting.HitParticle(message.GetVector3());
        }
    }

    [MessageHandler((ushort)ServerToClientId.playerHit)]
    private static void PlayerHit(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            player.playerShooting.HitEffect(message.GetVector3());
            if (!player.IsLocal) player.playerShooting.PlayHitmarker(message.GetBool());
        }
    }

    [MessageHandler((ushort)ServerToClientId.gunChanged)]
    private static void ChangeGun(Message message)
    {
        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (message.GetBool()) player.playerShooting.SwitchMelee(message.GetInt());
            else player.playerShooting.SwitchGun(message.GetInt());
            player.playerShooting.range = message.GetInt();
        }
    }
    #endregion

    public void DoTheSpin(int times, float duration)
    {
        StartCoroutine(RotateGun(times, duration));
    }

    private IEnumerator RotateGun(int times, float duration)
    {
        yield return new WaitForSeconds(0.2f);
        SoundManager.Instance.PlaySound(audioSource, spinSFX);
        isReloading = true;
        gunsSettings[activeGun].animator.enabled = false;

        Vector3 toAngle = new Vector3(-89.98f + -(times * 360), 0, 0);
        for (float t = 0f; t < 1f; t += Time.deltaTime / (duration * 0.7f))
        {
            gunsSettings[activeGun].gunModelPos.localRotation = Quaternion.Euler(toAngle * t);
            yield return null;
        }
        gunsSettings[activeGun].gunModelPos.localRotation = Quaternion.Euler(new Vector3(-89.98f, 0, 0));
        gunsSettings[activeGun].animator.enabled = true;
        isReloading = false;

        SoundManager.Instance.PlaySound(audioSource, reloadSFX);
        yield return new WaitForSeconds(0.4f);
        audioSource.Stop();
    }
}
