using UnityEngine;

public class DamageOnCollision : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float damage;

    private void OnCollisionEnter(Collision other)
    {
        if (!other.gameObject.CompareTag("Player") || !NetworkManager.Singleton.Server.IsRunning) return;

        other.gameObject.GetComponent<Player>().playerHealth.ReceiveDamage(damage, null);
    }
}
