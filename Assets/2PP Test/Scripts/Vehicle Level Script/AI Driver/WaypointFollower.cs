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

        Vector3 pos = transform.position;

        // --- Find the closest segment ---
        float minDist = float.MaxValue;
        int closestIndex = currentIndex;
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 a = waypoints[i].position;
            Vector3 b = waypoints[(i + 1) % waypoints.Count].position;
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(pos - a, ab.normalized) / ab.magnitude);
            Vector3 closestPoint = Vector3.Lerp(a, b, t);
            float dist = (pos - closestPoint).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                closestIndex = i;
            }
        }
        currentIndex = closestIndex;

        // --- Continue as before ---
        Transform wpA = waypoints[currentIndex];
        Transform wpB = waypoints[(currentIndex + 1) % waypoints.Count];

        Vector3 a2 = wpA.position;
        Vector3 b2 = wpB.position;
        Vector3 ab2 = b2 - a2;
        float t2 = Mathf.Clamp01(Vector3.Dot(pos - a2, ab2.normalized) / ab2.magnitude);

        // Find lookahead point
        float lookaheadT = Mathf.Clamp01(t2 + lookaheadDistance / ab2.magnitude);
        Vector3 lookaheadPoint = Vector3.Lerp(a2, b2, lookaheadT);

        // --- Steering ---
        Vector3 localTarget = transform.InverseTransformPoint(lookaheadPoint);
        float angleToTarget = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        float normalizedSteering = Mathf.Clamp(angleToTarget / maxSteeringAngle, -1f, 1f);
        aiDriver.steering = Mathf.Lerp(aiDriver.steering, normalizedSteering, Time.deltaTime * steeringSmoothing);

        // --- Predict Upcoming Turn ---
        int nextIndex = (currentIndex + 1) % waypoints.Count;
        int nextNextIndex = (currentIndex + 2) % waypoints.Count;
        Vector3 nextA = waypoints[nextIndex].position;
        Vector3 nextB = waypoints[nextNextIndex].position;
        Vector3 dirCurrent = (b2 - a2).normalized;
        Vector3 dirNext = (nextB - nextA).normalized;
        float bendAngle = Vector3.Angle(dirCurrent, dirNext);

        // --- Speed & Braking Control ---
        float speed = rb.linearVelocity.magnitude;
        float speedFactor = Mathf.InverseLerp(minSafeSpeed, maxSafeSpeed, speed);
        float turnAngleFactor = Mathf.InverseLerp(turnSlowdownAngle, fullBrakeAngle, Mathf.Max(Mathf.Abs(angleToTarget), bendAngle));
        float totalBrakeFactor = Mathf.Clamp01(
            Mathf.Lerp(turnAngleFactor, turnAngleFactor * speedFactor, speedWeight)
        );
        float targetThrottle = Mathf.Lerp(baseThrottle, minThrottle, totalBrakeFactor);
        float targetBrake = totalBrakeFactor;

        aiDriver.throttle = targetThrottle;
        aiDriver.brake = targetBrake;
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
