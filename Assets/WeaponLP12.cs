using UnityEngine;
using DG.Tweening;

public class WeaponLP12 : BaseWeaponShotgun
{
    [Header("LP-12")]
    [Header("Sounds")]
    [Space(5)]
    [SerializeField, Range(0, 1)] protected float pumpInSoundVolume;
    [SerializeField] protected AudioClip pumpInSound;
    [SerializeField, Range(0, 1)] protected float pumpOutSoundVolume;
    [SerializeField] protected AudioClip pumpOutSound;
    [SerializeField, Range(0, 1)] protected float magOutSoundVolume;
    [SerializeField] protected AudioClip magOutSound;
    [SerializeField, Range(0, 1)] protected float magFitSoundVolume;
    [SerializeField] protected AudioClip magFitSound;
    [SerializeField, Range(0, 1)] protected float magPunchSoundVolume;
    [SerializeField] protected AudioClip magPunchSound;

    [Header("Effects")]
    [Space(5)]
    [SerializeField] protected Transform magPos;
    [SerializeField] protected Rigidbody magOutPrefab;

    public void PlayPumpInSFX()
    {
        weaponAudioSource.GetRandomPitch();
        weaponAudioSource.PlayOneShot(pumpInSound, pumpInSoundVolume);
    }

    public void PlayPumpOutSFX()
    {
        weaponAudioSource.GetRandomPitch();
        weaponAudioSource.PlayOneShot(pumpOutSound, pumpOutSoundVolume);
    }

    public void PlayMagOutEffects()
    {
        weaponAudioSource.GetRandomPitch(0.05f, 0.1f);
        weaponAudioSource.PlayOneShot(magOutSound, magOutSoundVolume);

        Rigidbody magRb = Instantiate(magOutPrefab, magPos);
        magRb.transform.localPosition = new Vector3(0, 0, 0);
        magRb.transform.localEulerAngles = new Vector3(89.98f, 0, 180);
        magRb.transform.SetParent(null);
        magRb.AddForce(magRb.transform.up * 3, ForceMode.Impulse);

        // Time.timeScale = 0;
        magRb.transform.DOScale(Vector3.zero, 1).SetDelay(2).SetEase(Ease.InOutQuad).OnComplete(() => Destroy(magRb.gameObject));
    }

    public void PlayMagInSFX()
    {
        weaponAudioSource.GetRandomPitch();
        weaponAudioSource.PlayOneShot(magFitSound, magFitSoundVolume);
    }

    public void PlayMagPunchSFX()
    {
        weaponAudioSource.GetRandomPitch(0.05f, 0.1f);
        weaponAudioSource.PlayOneShot(magPunchSound, magPunchSoundVolume);
    }
}
