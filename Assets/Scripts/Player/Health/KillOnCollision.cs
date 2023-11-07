using UnityEngine;

public class KillOnCollision : MonoBehaviour
{
    private void OnCollisionEnter(Collision other)
    {
        print($"{gameObject.tag}");
        if (!other.gameObject.CompareTag("Player") || !NetworkManager.Singleton.Server.IsRunning) return;

        other.gameObject.GetComponent<Player>().playerHealth.InstaKill();
    }
}
