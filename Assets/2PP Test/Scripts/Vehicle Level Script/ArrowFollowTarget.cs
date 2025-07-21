using UnityEngine;

public class ArrowFollowTarget : MonoBehaviour
{
/*    [Tooltip("Target to point the arrow at (e.g., AI Driver)")]
    public Transform target;

    void Update()
    {
        if (target == null) return;

        // Direction to target in X-Z plane
        Vector3 direction = target.position - transform.position;
        direction.y = 0f; // Ignore vertical difference

        // Calculate angle in X-Z plane
        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        // Only affect Y rotation (yaw), keep X and Z unchanged
        Vector3 currentEuler = transform.eulerAngles;
        transform.eulerAngles = new Vector3(currentEuler.x, angle, currentEuler.z);
    }*/


    public Transform target;

    private void Update()
    {
        var dir = target.position - transform.position;

        var angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.up);
    }

}
