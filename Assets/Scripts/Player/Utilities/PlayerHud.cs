using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class PlayerHud : MonoBehaviour
{
    [Header("Components")]
    [Space(5)]
    [SerializeField] private ScriptablePlayer playerSettings;
    [SerializeField] private PlayerShooting playerShooting;

    [Header("Overlays")]
    [Space(5)]
    [SerializeField] private Image hurtOverlay;
    [SerializeField] private GameObject lMinusSymbol;
    [SerializeField] private GameObject rMinusSymbol;

    [SerializeField] private Image healOverlay;
    [SerializeField] private GameObject lPlusSymbol;
    [SerializeField] private GameObject rPlusSymbol;

    [Header("Colors")]
    [Space(5)]
    [SerializeField] private Color highlitedColor;
    [SerializeField] private Color fadedColor;

    [Header("Health")]
    [Space(5)]
    [SerializeField] private Image healthBar;

    [Header("Abilities Panel")]
    [Space(5)]
    [SerializeField] private Image[] staminaBars;

    [Header("Weapons Panel")]
    [Space(5)]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI[] weaponsSlotsTxt;
    [SerializeField] private TextMeshProUGUI[] weaponsNamesTxt;
    [SerializeField] private Image[] weaponsImages;

    [Header("Crosshairs")]
    [Space(5)]
    [SerializeField] private GameObject[] crosshairs;
    [SerializeField] private Image[] hitmarkers;
    [SerializeField] private Image ReloadIndicator;

    [Header("UI Texts")]
    [Space(5)]
    [SerializeField] private TextMeshProUGUI killIndicatorTxt;
    [SerializeField] private TextMeshProUGUI mediumBottomTxt;

    [Header("Key Indicators")]
    [Space(5)]
    [SerializeField] private TextMeshProUGUI scoreboardIndicatorKeyTxt;
    [SerializeField] private TextMeshProUGUI ultimateIndicatorKeyTxt;

    [Header("Menus")]
    [Space(5)]
    [SerializeField] private GameObject gameHud;

    private int currentCrosshairIndex;
    private Vector3 crosshairScale;
    private Vector3 crosshairShotScale;
    private float crosshairShrinkTime;

    private void Start()
    {
        SettingsManager.updatedPlayerPrefs += GetPreferences;
        ResetAllUITexts();
    }

    private void OnApplicationQuit()
    {
        SettingsManager.updatedPlayerPrefs -= GetPreferences;
    }

    private void OnDestroy()
    {
        SettingsManager.updatedPlayerPrefs -= GetPreferences;
    }

    private void GetPreferences()
    {
        scoreboardIndicatorKeyTxt.SetText(SettingsManager.playerPreferences.scoreboardKey.ToString());
        ultimateIndicatorKeyTxt.SetText(SettingsManager.playerPreferences.altFireBtn.ToString());
        if (SettingsManager.playerPreferences.crosshairType == 0 && playerShooting.currentWeapon)
        {
            UpdateCrosshair((int)playerShooting.currentWeapon.crosshairType, playerShooting.currentWeapon.crosshairScale, playerShooting.currentWeapon.crosshairShotScale, playerShooting.currentWeapon.crosshairShrinkTime);
        }
        else UpdateCrosshair((int)CrosshairType.dot, 1, 1, 0);
    }

    #region TextsUI
    private void ResetAllUITexts()
    {
        mediumBottomTxt.SetText("");
    }

    public void UpdateMediumBottomText(string text)
    {
        mediumBottomTxt.SetText(text);
    }

    public void FadeKillIndicator(string playerName)
    {
        killIndicatorTxt.SetText($"Eliminated <<color=#C33C3C>{playerName}</color>>");
        killIndicatorTxt.transform.DOComplete();
        killIndicatorTxt.transform.DOScale(Vector3.one, 0.15f).OnComplete(() => killIndicatorTxt.transform.DOScale(Vector3.zero, 0.35f).SetDelay(2));
    }
    #endregion

    #region HealthUI
    public void UpdateHealthDisplay(float health)
    {
        DOTween.To(() => healthBar.fillAmount, x => healthBar.fillAmount = x, health / playerSettings.maxHealth, 0.3f);
    }

    public void FadeHurtOverlay()
    {
        hurtOverlay.DOComplete();
        Tweener hurtTweener = hurtOverlay.DOFade(170f / 250, 0.1f).SetEase(Ease.OutSine);
        hurtTweener.OnComplete(() => hurtOverlay.DOFade(0, 0.3f).SetEase(Ease.OutSine));
        AnimateHealthOverlaySymbol(lMinusSymbol, rMinusSymbol, 0.25f, 30, 2);
    }

    private void AnimateHealthOverlaySymbol(GameObject lSymbol, GameObject rSymbol, float duration, float shakeStrenght, int vibrato)
    {
        lSymbol.transform.DOComplete();
        lSymbol.transform.localScale = Vector3.zero;
        lSymbol.transform.DOPunchScale(Vector3.one, duration, 0).SetEase(Ease.InOutElastic);
        lSymbol.transform.DOShakePosition(duration, shakeStrenght, vibrato, fadeOut: false);

        rSymbol.transform.DOComplete();
        rSymbol.transform.localScale = Vector3.zero;
        rSymbol.transform.DOPunchScale(Vector3.one, duration, 0).SetEase(Ease.InOutElastic);
        rSymbol.transform.DOShakePosition(duration, shakeStrenght, vibrato, fadeOut: false);
    }

    public void FadeHealOverlay()
    {
        healOverlay.DOComplete();
        Tweener healTweener = healOverlay.DOFade(170f / 250, 0.1f).SetEase(Ease.OutSine);
        healTweener.OnComplete(() => healOverlay.DOFade(0, 0.3f).SetEase(Ease.OutSine));
        AnimateHealthOverlaySymbol(lPlusSymbol, rPlusSymbol, 0.25f, 30, 2);
    }

    #endregion

    #region WeaponUI
    public void UpdateAmmoDisplay(int currentAmmo, int maxAmmo)
    {
        ammoText.SetText($"{currentAmmo}/{maxAmmo}");
    }

    public void HighlightWeaponOnSlot(int slot)
    {
        FadeWeaponsSlots();

        weaponsSlotsTxt[slot].color = highlitedColor;
        weaponsNamesTxt[slot].color = highlitedColor;
        weaponsImages[slot].color = highlitedColor;
    }

    private void FadeWeaponsSlots()
    {
        for (int i = 0; i < weaponsSlotsTxt.Length; i++)
        {
            weaponsSlotsTxt[i].color = fadedColor;
            weaponsNamesTxt[i].color = fadedColor;
            weaponsImages[i].color = fadedColor;
        }
    }

    public void UpdateWeaponsOnSlots(BaseWeapon firstWeapon, BaseWeapon secondWeapon, BaseWeapon thirdWeapon, int active)
    {
        ResetWeaponsOnSlots();
        if (firstWeapon)
        {
            weaponsImages[0].enabled = true;
            weaponsImages[0].sprite = firstWeapon.weaponIcon;
            weaponsNamesTxt[0].SetText(firstWeapon.weaponName);
        }
        if (secondWeapon)
        {
            weaponsImages[1].enabled = true;
            weaponsImages[1].sprite = secondWeapon.weaponIcon;
            weaponsNamesTxt[1].SetText(secondWeapon.weaponName);
        }
        if (thirdWeapon)
        {
            weaponsImages[2].enabled = true;
            weaponsImages[2].sprite = thirdWeapon.weaponIcon;
            weaponsNamesTxt[2].SetText(thirdWeapon.weaponName);
        }

        HighlightWeaponOnSlot(active);
    }

    public void ResetWeaponsOnSlots()
    {
        for (int i = 0; i < weaponsSlotsTxt.Length; i++)
        {
            weaponsNamesTxt[i].SetText("");
            weaponsImages[i].enabled = false;
        }
    }
    #endregion

    #region AbilitiesUI
    public void UpdateStamina(float stamina)
    {
        print($"Stamina is {stamina}");
        for (int i = 0; i < staminaBars.Length; i++)
        {
            if ((int)stamina == i)
            {
                staminaBars[(int)stamina].fillAmount = stamina % 1;
                staminaBars[i].color = fadedColor;
            }

            else if ((int)stamina < i)
            {
                staminaBars[i].fillAmount = 0;
                staminaBars[i].color = fadedColor;
            }

            else
            {
                staminaBars[i].fillAmount = 1;
                staminaBars[i].color = highlitedColor;
            }

        }
    }
    #endregion

    #region CrosshairUI
    public void UpdateCrosshair(int crosshairIndex, float weaponCrosshairScale, float weaponcrosshairShotScale, float weaponcrosshairShrinkTime)
    {
        DisableAllCrosshairs();

        currentCrosshairIndex = crosshairIndex;
        crosshairs[currentCrosshairIndex].SetActive(true);

        crosshairScale = new Vector3(weaponCrosshairScale, weaponCrosshairScale, weaponCrosshairScale);
        crosshairShotScale = new Vector3(weaponcrosshairShotScale, weaponcrosshairShotScale, weaponcrosshairShotScale);
        crosshairShrinkTime = weaponcrosshairShrinkTime;

        crosshairs[currentCrosshairIndex].transform.localScale = crosshairScale;
    }

    public void ReloadIndicatorFill(float time)
    {
        ReloadIndicator.DOFillAmount(1, time).SetEase(Ease.Linear).OnComplete(() => ReloadIndicator.fillAmount = 0);
    }

    public void KillRealoadIndicatorFill()
    {
        ReloadIndicator.DOComplete();
    }

    public void ScaleCrosshairShot()
    {
        if (crosshairShrinkTime == 0) return;
        crosshairs[currentCrosshairIndex].transform.DOComplete();
        crosshairs[currentCrosshairIndex].transform.DOPunchScale(crosshairShotScale, crosshairShrinkTime, 0, 0);
    }

    private void DisableAllCrosshairs()
    {
        for (int i = 0; i < crosshairs.Length; i++) crosshairs[i].SetActive(false);
    }

    public void FadeHitmarker(bool special, float fadeTime)
    {
        int index;
        index = special ? (currentCrosshairIndex == 1 ? 3 : 2) : (currentCrosshairIndex == 1 ? 1 : 0);

        hitmarkers[index].DOComplete();
        hitmarkers[index].transform.DOComplete();
        hitmarkers[index].DOFade(1, fadeTime).OnComplete(() => hitmarkers[index].DOFade(0, fadeTime));
        hitmarkers[index].transform.DOPunchScale(Vector3.one, fadeTime * 2, 0);
    }
    #endregion
}
