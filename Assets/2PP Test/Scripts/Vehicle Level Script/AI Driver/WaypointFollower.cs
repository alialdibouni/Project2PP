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
    public float lookaheadDistance = 5f; // NEW: How far ahead to look on the path

    [Header("Throttle Control")]
    public float baseThrottle = 1f;
    public float minThrottle = 0.2f;
    public float turnSlowdownAngle = 10f;
    public float fullBrakeAngle = 60f;

    [Header("Speed Prediction")]
    public float maxSafeSpeed = 15f;
    public float minSafeSpeed = 5f;
    public float speedWeight = 0.7f;

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

        // Find the segment we're on
        Transform wpA = waypoints[currentIndex];
        Transform wpB = waypoints[(currentIndex + 1) % waypoints.Count];

        // Project car position onto the segment
        Vector3 pos = transform.position;
        Vector3 a = wpA.position;
        Vector3 b = wpB.position;
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(pos - a, ab.normalized) / ab.magnitude);

        // Find lookahead point
        float lookaheadT = Mathf.Clamp01(t + lookaheadDistance / ab.magnitude);
        Vector3 lookaheadPoint = Vector3.Lerp(a, b, lookaheadT);

        // Advance waypoint if passed
        if ((pos - b).magnitude < waypointPassThreshold)
        {
            currentIndex = (currentIndex + 1) % waypoints.Count;
        }

        // --- Steering ---
        Vector3 localTarget = transform.InverseTransformPoint(lookaheadPoint);
        float angleToTarget = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        float normalizedSteering = Mathf.Clamp(angleToTarget / maxSteeringAngle, -1f, 1f);
        aiDriver.steering = Mathf.Lerp(aiDriver.steering, normalizedSteering, Time.deltaTime * steeringSmoothing);

        // --- Predict Upcoming Turn ---
        // Get next segment (for bend prediction)
        int nextIndex = (currentIndex + 1) % waypoints.Count;
        int nextNextIndex = (currentIndex + 2) % waypoints.Count;
        Vector3 nextA = waypoints[nextIndex].position;
        Vector3 nextB = waypoints[nextNextIndex].position;
        Vector3 dirCurrent = (b - a).normalized;
        Vector3 dirNext = (nextB - nextA).normalized;
        float bendAngle = Vector3.Angle(dirCurrent, dirNext); // 0 = straight, 180 = sharp turn

        // --- Speed & Braking Control ---
        float speed = rb.linearVelocity.magnitude;
        float speedFactor = Mathf.InverseLerp(minSafeSpeed, maxSafeSpeed, speed);

        // Use the sharper of the current steering or upcoming bend
        float turnAngleFactor = Mathf.InverseLerp(turnSlowdownAngle, fullBrakeAngle, Mathf.Max(Mathf.Abs(angleToTarget), bendAngle));

        float totalBrakeFactor = Mathf.Clamp01(
            Mathf.Lerp(turnAngleFactor, turnAngleFactor * speedFactor, speedWeight)
        );

        float targetThrottle = Mathf.Lerp(baseThrottle, minThrottle, totalBrakeFactor);
        float targetBrake = totalBrakeFactor; // More brake as the turn is sharper

        aiDriver.throttle = targetThrottle;
        aiDriver.brake = targetBrake; // You must use this in your CarController
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        Gizmos.color = Color.green;
        for (int i = 0; i < waypoints.Count; i++)
        {
            Gizmos.DrawWireSphere(waypoints[i].position, 1f);
            Gizmos.DrawLine(
                waypoints[i].position,
                waypoints[(i + 1) % waypoints.Count].position
            );
        }

        if (Application.isPlaying && waypoints.Count > currentIndex)
        {
            // Draw lookahead point
            Transform wpA = waypoints[currentIndex];
            Transform wpB = waypoints[(currentIndex + 1) % waypoints.Count];
            Vector3 pos = transform.position;
            Vector3 a = wpA.position;
            Vector3 b = wpB.position;
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(pos - a, ab.normalized) / ab.magnitude);
            float lookaheadT = Mathf.Clamp01(t + lookaheadDistance / ab.magnitude);
            Vector3 lookaheadPoint = Vector3.Lerp(a, b, lookaheadT);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, lookaheadPoint);
            Gizmos.DrawWireSphere(lookaheadPoint, 0.5f);
        }
    }
#endif
}
