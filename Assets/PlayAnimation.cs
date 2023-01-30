using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string animationName;
    // Start is called before the first frame update
    void Start()
    {
        animator.Play(animationName);
    }
}
