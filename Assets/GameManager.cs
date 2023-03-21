using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public bool networking { get; set; }
    public float minTimeBetweenTicks { get; private set; }

    private static GameManager _singleton;
    public static GameManager Singleton
    {
        get => _singleton;
        private set
        {
            if (_singleton == null)
            {
                _singleton = value;
            }

            else if (_singleton != value)
            {
                Debug.Log($"{nameof(GameManager)} instance already exists, destroying duplicate");
                Destroy(value.gameObject);
            }

        }
    }

    public GameObject LocalPlayerPrefab => localPlayerPrefab;
    public GameObject PlayerPrefab => playerPrefab;
    public ParticleSystem HitPrefab => hitPrefab;
    public ParticleSystem PlayerHitPrefab => playerHitPrefab;
    public TrailRenderer ShotTrail => shotTrail;

    [Header("Prefabs")]
    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private ParticleSystem hitPrefab;
    [SerializeField] private ParticleSystem playerHitPrefab;
    [SerializeField] private TrailRenderer shotTrail;

    private void Awake()
    {
        Singleton = this;
        DontDestroyOnLoad(gameObject);
        minTimeBetweenTicks = 1f / NetworkManager.ServerTickRate;
    }
}
