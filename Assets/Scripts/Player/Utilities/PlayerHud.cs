using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/* WORK REMINDER

    Implement Crosshair Color Customization

*/

public class PlayerHud : MonoBehaviour
{
    [Header("Components")]
    [Space(5)]
    [SerializeField] private ScriptablePlayer playerSettings;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerShooting playerShooting;

    [Header("Overlays")]
    [Space(5)]
    [SerializeField] private Image hurtOverlay;
    [SerializeField] private Image focusedOverlay;

    [Header("Colors")]
    [Space(5)]
    [SerializeField] private Color groundSlamAvailableColor;
    [SerializeField] private Color highlitedColor;
    [SerializeField] private Color fadedColor;

    [Header("Health Battery")]
    [Space(5)]
    [SerializeField] private Slider healthBarFill;

    [Header("Abilities Panel")]
    [Space(5)]
    [SerializeField] private Image[] dashIcons;
    [SerializeField] public Slider[] dashSliders;
    [SerializeField] private Image groundSlamIcon;
    [SerializeField] public Slider groundSlamSlider;

    [Header("Weapons Panel")]
    [Space(5)]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI[] weaponsSlotsText;
    [SerializeField] private TextMeshProUGUI[] weaponsNamesText;
    [SerializeField] private Image[] weaponsImages;

    [Header("Crosshairs")]
    [Space(5)]
    [SerializeField] private GameObject[] crosshairs;
    [SerializeField] private Image[] hitmarkers;
    [SerializeField] private Slider reloadSlider;

    [Header("UI Texts")]
    [Space(5)]
    [SerializeField] private TextMeshProUGUI scoreboardIndicatorKeyTxt;
    [SerializeField] private TextMeshProUGUI mediumBottomText;

    [Header("Menus")]
    [Space(5)]
    [SerializeField] private GameObject gameHud;

    private int currentCrosshairIndex;
    private Vector3 crosshairScale;
    private Vector3 crosshairShotScale;
    private float crosshairShrinkTime;
    private float crosshairShrinkTimer;

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

    private void Update()
    {
        ScaleDownCrosshair();
    }

    #region GameUi
    private void GetPreferences()
    {
        scoreboardIndicatorKeyTxt.SetText(SettingsManager.playerPreferences.scoreboardKey.ToString());
        if (SettingsManager.playerPreferences.crosshairType == 0 && playerShooting.currentWeapon)
        {
            UpdateCrosshair((int)playerShooting.currentWeapon.crosshairType, playerShooting.currentWeapon.crosshairScale, playerShooting.currentWeapon.crosshairShotScale, playerShooting.currentWeapon.crosshairShrinkTime);
        }
        else UpdateCrosshair((int)CrosshairType.dot, 1, 1, 0);
    }

    public void UpdateHealthDisplay(float health)
    {
        DOTween.To(() => healthBarFill.value, x => healthBarFill.value = x, health / playerSettings.maxHealth, 0.3f);
    }

    public void FadeHurtOverlay()
    {
        Tweener tweener = hurtOverlay.DOFade(170f / 250, 0.1f).SetEase(Ease.OutSine);
        tweener.OnComplete(() => hurtOverlay.DOFade(0, 0.3f).SetEase(Ease.OutSine));
    }

    public void UpdateAmmoDisplay(int currentAmmo, int maxAmmo)
    {
        ammoText.SetText($"{currentAmmo}/{maxAmmo}");
    }

    public void UpdateMediumBottomText(string text)
    {
        mediumBottomText.SetText(text);
    }

    private void ResetAllUITexts()
    {
        mediumBottomText.SetText("");
    }

    public void HighlightWeaponOnSlot(int slot)
    {
        FadeWeaponsSlots();

        weaponsSlotsText[slot].color = highlitedColor;
        weaponsNamesText[slot].color = highlitedColor;
        weaponsImages[slot].color = highlitedColor;
    }

    private void FadeWeaponsSlots()
    {
        for (int i = 0; i < weaponsSlotsText.Length; i++)
        {
            weaponsSlotsText[i].color = fadedColor;
            weaponsNamesText[i].color = fadedColor;
            weaponsImages[i].color = fadedColor;
        }
    }

    public void UpdateWeaponOnSlot(int slot, string weaponName, Sprite weaponImage, bool switching)
    {
        weaponsNamesText[slot].SetText(weaponName);
        weaponsImages[slot].enabled = true;
        weaponsImages[slot].sprite = weaponImage;
        if (switching) HighlightWeaponOnSlot(slot);
    }

    public void ResetWeaponsOnSlots()
    {
        for (int i = 0; i < weaponsSlotsText.Length; i++)
        {
            weaponsNamesText[i].SetText("");
            weaponsImages[i].enabled = false;
        }
    }

    public void UpdateGroundSlamIcon(bool state)
    {
        groundSlamIcon.color = state ? groundSlamAvailableColor : fadedColor;
    }

    public void UpdateDashIcons(int availableDashes)
    {
        for (int i = 0; i < dashIcons.Length; i++)
        {
            dashIcons[i].color = i < availableDashes ? highlitedColor : fadedColor;
            dashSliders[i].value = i < availableDashes ? 1 : 0;
        }
    }

    public void UpdateCrosshair(int crosshairIndex, float weaponCrosshairScale, float weaponcrosshairShotScale, float weaponcrosshairShrinkTime)
    {
        DisableAllCrosshairs();

        currentCrosshairIndex = crosshairIndex;
        crosshairs[currentCrosshairIndex].SetActive(true);

        crosshairShrinkTimer = 0;
        crosshairScale = new Vector3(weaponCrosshairScale, weaponCrosshairScale, weaponCrosshairScale);
        crosshairShotScale = new Vector3(weaponcrosshairShotScale, weaponcrosshairShotScale, weaponcrosshairShotScale);
        crosshairShrinkTime = weaponcrosshairShrinkTime;

        crosshairs[currentCrosshairIndex].transform.localScale = crosshairScale;
    }

    public void UpdateReloadSlider(float val)
    {
        if (val >= 1)
        {
            reloadSlider.value = 0;
            return;
        }

        reloadSlider.value = val;
    }

    public void ScaleCrosshairShot()
    {
        crosshairs[currentCrosshairIndex].transform.localScale = crosshairShotScale;
        crosshairShrinkTimer = 0;
    }

    private void ScaleDownCrosshair()
    {
        if (crosshairShrinkTimer >= crosshairShrinkTime) return;

        crosshairShrinkTimer += Time.deltaTime;
        crosshairs[currentCrosshairIndex].transform.localScale = Vector3.Lerp(crosshairShotScale, crosshairScale, crosshairShrinkTimer / crosshairShrinkTime);
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
