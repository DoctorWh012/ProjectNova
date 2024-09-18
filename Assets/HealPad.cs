using UnityEngine;

public class HealPad : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private GameObject model;

    [Header("Settings")]
    [SerializeField] private float healAmount;
    [SerializeField] private float rechargeTime;

    private bool active = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!active || !other.CompareTag("Player")) return;
        DisableHealPad();
        Invoke(nameof(EnableHealPad), rechargeTime);

        if (!NetworkManager.Singleton.Server.IsRunning) return;
        Player player = other.GetComponentInParent<Player>();
        player.playerHealth.RecoverHealth(healAmount);
    }

    private void EnableHealPad()
    {
        active = true;
        model.SetActive(true);
    }

    private void DisableHealPad()
    {
        active = false;
        model.SetActive(false);
    }
}
