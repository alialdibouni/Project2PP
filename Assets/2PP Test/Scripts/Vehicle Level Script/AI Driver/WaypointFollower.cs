using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(AIDriver))]
public class WaypointFollower : MonoBehaviour
{
    [Header("Waypoints")]
    public List<Transform> waypoints = new List<Transform>();

    [Header("Movement Settings")]
    public float waypointPassThreshold = 2f;
    public float maxSteeringAngle = 45f;
    public float steeringSmoothing = 5f;

    [Header("Throttle Control")]
    public float baseThrottle = 1f;
    public float minThrottle = 0.2f;
    public float turnSlowdownAngle = 10f;
    public float fullBrakeAngle = 60f;

    [Header("Speed Prediction")]
    public float maxSafeSpeed = 15f; // Speed (m/s) where full throttle is still safe
    public float minSafeSpeed = 5f;  // Below this, braking shouldn't happen
    public float speedWeight = 0.7f; // How much weight to give speed vs angle (0 = angle only, 1 = speed only)

    private int currentIndex = 0;
    private AIDriver aiDriver;
    private Rigidbody rb;

    void Awake()
    {
        aiDriver = GetComponent<AIDriver>();
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (waypoints == null || waypoints.Count == 0)
            return;

        Transform target = waypoints[currentIndex];
        Vector3 localTarget = transform.InverseTransformPoint(target.position);

        // Advance waypoint if passed
        if (localTarget.z < -waypointPassThreshold)
        {
            currentIndex = (currentIndex + 1) % waypoints.Count;
            target = waypoints[currentIndex];
            localTarget = transform.InverseTransformPoint(target.position);
        }

        // --- Steering ---
        float angleToTarget = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        float normalizedSteering = Mathf.Clamp(angleToTarget / maxSteeringAngle, -1f, 1f);
        aiDriver.steering = Mathf.Lerp(aiDriver.steering, normalizedSteering, Time.deltaTime * steeringSmoothing);

        // --- Speed Prediction ---
        float speed = rb.linearVelocity.magnitude; // In m/s
        float speedFactor = Mathf.InverseLerp(minSafeSpeed, maxSafeSpeed, speed); // 0 = slow, 1 = fast
        float turnAngleFactor = Mathf.InverseLerp(turnSlowdownAngle, fullBrakeAngle, Mathf.Abs(angleToTarget)); // 0 = easy, 1 = sharp

        float totalBrakeFactor = Mathf.Clamp01(
            Mathf.Lerp(turnAngleFactor, turnAngleFactor * speedFactor, speedWeight)
        );

        float targetThrottle = Mathf.Lerp(baseThrottle, minThrottle, totalBrakeFactor);
        aiDriver.throttle = targetThrottle;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        Gizmos.color = Color.green;
        for (int i = 0; i < waypoints.Count; i++)
        {
            Gizmos.DrawWireSphere(waypoints[i].position, 1f);
            if (i < waypoints.Count - 1)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }

        if (Application.isPlaying && waypoints.Count > currentIndex)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, waypoints[currentIndex].position);
        }
    }
#endif
}
