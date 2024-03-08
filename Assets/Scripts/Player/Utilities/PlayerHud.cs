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
    [SerializeField] private ScriptablePlayer playerSettings;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerShooting playerShooting;

    [Header("Colors")]
    [SerializeField] private Color dashAvailableColor;
    [SerializeField] private Color highlitedColor;
    [SerializeField] private Color fadedColor;

    [Header("Health Battery")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Slider batteryLevel;

    [Header("Abilities Panel")]
    [SerializeField] private Image[] dashIcons;
    [SerializeField] public Slider[] dashSliders;
    [SerializeField] private Image groundSlamIcon;
    [SerializeField] public Slider groundSlamSlider;

    [Header("Weapons Panel")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI[] weaponsSlotsText;
    [SerializeField] private TextMeshProUGUI[] weaponsNamesText;
    [SerializeField] private Image[] weaponsImages;

    [Header("Crosshairs")]
    [SerializeField] private GameObject[] crosshairs;
    [SerializeField] private Image[] hitmarkers;
    [SerializeField] private Slider reloadSlider;

    [Header("UI Texts")]
    [SerializeField] private TextMeshProUGUI mediumBottomText;
    [SerializeField] private TextMeshProUGUI pingTxt;
    [SerializeField] private TextMeshProUGUI speedometerText;

    [Header("Menus")]
    [SerializeField] private GameObject gameHud;

    private int currentCrosshairIndex;
    private Vector3 crosshairScale;
    private Vector3 crosshairShotScale;
    private float crosshairShrinkTime;
    private float crosshairShrinkTimer;

    private void Start()
    {
        SettingsManager.updatedPlayerPrefs += GetPreferences;
        ResetWeaponsOnSlots();
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
        pingTxt.SetText($"Ping: {NetworkManager.Singleton.Client.RTT}");
    }

    #region GameUi
    private void GetPreferences()
    {
        if (SettingsManager.playerPreferences.crosshairType == 0 && playerShooting.activeGun) UpdateCrosshair((int)playerShooting.activeGun.crosshairType, playerShooting.activeGun.crosshairScale, playerShooting.activeGun.crosshairShotScale, playerShooting.activeGun.crosshairShrinkTime);
        else UpdateCrosshair((int)CrosshairType.dot, 1, 1, 0);
    }

    public void UpdateHealthDisplay(float health)
    {
        healthText.SetText($"{health.ToString("#")}%");
        batteryLevel.value = health / playerSettings.maxHealth;
    }

    public void UpdateAmmoDisplay(int currentAmmo, int maxAmmo)
    {
        ammoText.SetText($"{currentAmmo}/{maxAmmo}");
    }

    public void UpdateMediumBottomText(string text)
    {
        mediumBottomText.SetText(text);
    }

    public void UpdateSpeedometerText(string text)
    {
        speedometerText.SetText(text);
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
        groundSlamIcon.color = state ? highlitedColor : fadedColor;
    }

    public void UpdateDashIcons(int availableDashes)
    {
        for (int i = 0; i < dashIcons.Length; i++)
        {
            dashIcons[i].color = i < availableDashes ? dashAvailableColor : fadedColor;
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
