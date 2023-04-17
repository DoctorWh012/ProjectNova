using System.Collections;
using UnityEngine;
using Riptide;
using EZCameraShake;

public class PlayerShooting : MonoBehaviour
{
    public int activeGun { get; private set; }
    public bool isWeaponTilted { get; private set; } = false;
    public WeaponType activeWeaponType { get; private set; }
    public Animator animator { get; private set; }


    [Header("Components")]
    [SerializeField] private Player player;
    [SerializeField] private AudioSource playerAudioSource;
    [SerializeField] public GunComponents[] gunsSettings;
    [SerializeField] public MeleeComponents[] meleeSettings;
    [SerializeField] private HeadBobController headBobController;
    [SerializeField] private Camera scopeCam;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip hitMarkerSfx;
    [SerializeField] private AudioClip spinSFX;
    [SerializeField] private AudioClip reloadSFX;

    private Transform barrelTip;
    private ParticleSystem weaponEffectParticle;
    public int range;

    IEnumerator tiltWeaponCoroutine;

    // Start is called before the first frame update
    private void Start()
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
        if (player.IsLocal) GameCanvas.Instance.ChangeGunSlotIcon(((int)gunsSettings[index].gunSettings.slot), gunsSettings[index].gunSettings.gunIcon);
        EnableActiveGunMesh(index);
    }

    public void SwitchMelee(int index)
    {
        activeGun = index;
        activeWeaponType = meleeSettings[index].meleeSettings.weaponType;
        animator = meleeSettings[index].animator;
        weaponEffectParticle = meleeSettings[index].meleeParticles;
        if (player.IsLocal) GameCanvas.Instance.ChangeGunSlotIcon(((int)meleeSettings[index].meleeSettings.slot), meleeSettings[index].meleeSettings.gunIcon);
        EnableActiveGunMesh(index);
    }

    public void EnableActiveGunMesh(int index)
    {
        DisableAllMeleeMesh();
        DisableAllGunMeshes();
        if (activeWeaponType != WeaponType.melee)
        {
            foreach (MeshRenderer mesh in gunsSettings[index].gunMesh) mesh.enabled = true;
            if (player.IsLocal) foreach (SkinnedMeshRenderer armMesh in gunsSettings[index].armMesh) armMesh.enabled = true;

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
        if (player.IsLocal) foreach (SkinnedMeshRenderer armMesh in meleeSettings[index].armMesh) armMesh.enabled = true;

    }

    public void DisableAllGunMeshes()
    {
        for (int i = 0; i < gunsSettings.Length; i++)
        {
            foreach (MeshRenderer mesh in gunsSettings[i].gunMesh) mesh.enabled = false;
            foreach (SkinnedMeshRenderer armMesh in gunsSettings[i].armMesh) armMesh.enabled = false;

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
            foreach (MeshRenderer mesh in meleeSettings[i].meleeMesh) mesh.enabled = false;
            foreach (SkinnedMeshRenderer armMesh in meleeSettings[i].armMesh) armMesh.enabled = false;
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

    public void HitParticle(Vector3 hitPos)
    {
        Instantiate(GameManager.Singleton.HitPrefab, hitPos, Quaternion.identity);
    }

    public void BulletTrailEffect(bool didHit, Vector3 hitPos, Vector2 spread)
    {
        // If the raycast hit something places the GameObject at rayHit.point
        if (didHit)
        {
            HitParticle(hitPos);
            TrailRenderer tracer = Instantiate(GameManager.Singleton.ShotTrail, barrelTip.position, Quaternion.identity);
            tracer.AddPosition(barrelTip.position);
            tracer.transform.position = hitPos;
        }

        // If it didn't hit something just moves the GameObject foward
        else
        {
            TrailRenderer tracer = Instantiate(GameManager.Singleton.ShotTrail, barrelTip.position, Quaternion.LookRotation(barrelTip.forward));
            tracer.AddPosition(barrelTip.position);
            tracer.transform.position += (barrelTip.forward + new Vector3(spread.x, spread.y, 0)) * range;
        }
    }

    private void HitEffect(Vector3 position)
    {
        ParticleSystem hitEffect = Instantiate(GameManager.Singleton.PlayerHitPrefab, position, Quaternion.identity);
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

            if (NetworkManager.Singleton.Server.IsRunning) return;
            if (player.IsLocal) { player.GunShoot.ammunition = message.GetUShort(); return; }
            player.playerShooting.BulletTrailEffect(message.GetBool(), message.GetVector3(), message.GetVector2());
            player.playerShooting.ShootingAnimator(message.GetBool(), player.IsLocal);
        }
    }

    [MessageHandler((ushort)ServerToClientId.meleeAtack)]
    private static void PlayerAtackedMelee(Message message)
    {

        if (Player.list.TryGetValue(message.GetUShort(), out Player player))
        {
            if (NetworkManager.Singleton.Server.IsRunning || player.IsLocal) return;
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
            if (message.GetBool()) player.playerShooting.SwitchMelee((int)message.GetByte());
            else player.playerShooting.SwitchGun((int)message.GetByte());
        }
    }
    #endregion

    public void DoTheSpin(int times, float duration)
    {
        StartCoroutine(RotateGun(times, duration));
    }

    public void TiltGun(float angle, float duration)
    {
        if (tiltWeaponCoroutine != null) StopCoroutine(tiltWeaponCoroutine);
        tiltWeaponCoroutine = TiltWeapon(angle, duration);
        StartCoroutine(tiltWeaponCoroutine);
    }

    private IEnumerator RotateGun(int times, float duration)
    {
        yield return new WaitForSeconds(0.1f);
        while (animator.GetCurrentAnimatorStateInfo(0).IsName("Recoil")) yield return null;

        SoundManager.Instance.PlaySound(audioSource, spinSFX);
        gunsSettings[activeGun].animator.enabled = false;

        Vector3 toAngle = new Vector3(-89.98f + -(times * 360), 0, 0);
        for (float t = 0f; t < 1f; t += Time.deltaTime / (duration - 0.2f))
        {
            gunsSettings[activeGun].gunModelPos.localRotation = Quaternion.Euler(toAngle * t);
            yield return null;
        }
        gunsSettings[activeGun].gunModelPos.localRotation = Quaternion.Euler(new Vector3(-89.98f, 0, 0));
        gunsSettings[activeGun].animator.enabled = true;

        SoundManager.Instance.PlaySound(audioSource, reloadSFX);
        yield return new WaitForSeconds(0.4f);
        audioSource.Stop();
    }

    private IEnumerator TiltWeapon(float tiltAngle, float duration)
    {
        Transform weaponTransform = gunsSettings[activeGun].transform;
        Quaternion startingAngle = weaponTransform.localRotation;
        Quaternion toAngle = Quaternion.Euler(new Vector3(0, 0, tiltAngle));
        float rotationDuration = 0;

        isWeaponTilted = !isWeaponTilted;
        while (weaponTransform.localRotation != toAngle)
        {
            weaponTransform.localRotation = Quaternion.Lerp(startingAngle, toAngle, rotationDuration / duration);
            rotationDuration += Time.deltaTime;
            yield return null;
        }
    }
}
