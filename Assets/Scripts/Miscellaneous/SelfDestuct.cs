using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelfDestuct : MonoBehaviour
{
    [SerializeField] float destroyTime;
    // Start is called before the first frame update
    void Start()
    {
        Invoke("SelfDestroy", destroyTime);
    }

    private void SelfDestroy()
    {
        Destroy(gameObject);
    }
}
