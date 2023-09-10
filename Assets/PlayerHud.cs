using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerHud : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private ScriptablePlayer playerSettings;

    [Header("Health Battery")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Slider batteryLevel;

    public void UpdateHealthDisplay(float health)
    {
        healthText.SetText($"{health.ToString("#")}%");
        batteryLevel.value = health / playerSettings.maxHealth;
    }
}
