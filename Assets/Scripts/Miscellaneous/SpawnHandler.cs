using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnHandler : MonoBehaviour
{
    public static SpawnHandler Instance;
    [SerializeField] private Transform[] spawns;

    private void Awake() => Instance = this;

    public Vector3 GetSpawnLocation()
    {
        int spawnLoc = Random.Range(0, spawns.Length);
        Vector3 finalSpawn = new Vector3(spawns[spawnLoc].position.x, spawns[spawnLoc].position.y + 2, spawns[spawnLoc].position.z);
        return finalSpawn;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        foreach (Transform spawner in spawns)
        {
            Gizmos.DrawWireSphere(new Vector3(spawner.position.x, spawner.position.y + 2, spawner.position.z), 2f);
        }
    }
}
