using UnityEngine;

public class KillOnCollision : MonoBehaviour
{
    private void OnCollisionEnter(Collision other)
    {
        if (!other.gameObject.CompareTag("Player") || !NetworkManager.Singleton.Server.IsRunning) return;

        other.gameObject.GetComponent<Player>().playerHealth.InstaKill();
    }
}
