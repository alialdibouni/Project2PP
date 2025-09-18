using UnityEngine;

public class FollowPlayerCamera : MonoBehaviour
{
    [SerializeField] private Transform target; // Assign the player GameObject in the Inspector
    [SerializeField] private float rotationLerpSpeed = 8f;   // Higher = faster snap

    void Update()
    {
        if (target == null) return;

        Vector3 toTarget = target.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f) return;

        // Compute desired rotation and smoothly slerp toward it
        Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desired, Time.deltaTime * rotationLerpSpeed);
    }
}
