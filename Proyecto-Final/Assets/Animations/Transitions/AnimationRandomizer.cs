using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationRandomizer : MonoBehaviour
{

    [Header("Animation Speed")]
    [SerializeField] private float minSpeed = 0.8f;
    [SerializeField] private float maxSpeed = 1.2f;

    [Header("Animation Start")]
    [SerializeField] private bool randomizeStartTime = true;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (animator == null)
            return;

        // Randomize playback speed
        animator.speed = Random.Range(minSpeed, maxSpeed);

        // Randomize the starting point of the current animation
        if (randomizeStartTime)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

            animator.Play(
                state.fullPathHash,
                0,
                Random.Range(0f, 1f)
            );
        }
    }
}
