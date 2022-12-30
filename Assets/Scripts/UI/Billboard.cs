using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Billboard : MonoBehaviour
{
    Transform mainCam;

    // Start is called before the first frame update
    private void Start()
    {
        StartCoroutine(GetCamera());
        transform.Rotate(Vector3.right * 180);
    }

    // Update is called once per frame
    private void Update()
    {
        transform.LookAt(mainCam);
        transform.Rotate(Vector3.up * 180);
    }

    private IEnumerator GetCamera()
    {
        while (!GameObject.FindGameObjectWithTag("MainCamera")) { yield return null; }
        mainCam = GameObject.FindGameObjectWithTag("MainCamera").transform;
    }
}
