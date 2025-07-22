using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(AIDriver))]
public class WaypointFollower : MonoBehaviour
{
    [Header("Waypoints")]
    public List<Transform> waypoints = new List<Transform>();

    [Header("Brake Points")]
    public List<Transform> brakePoints = new List<Transform>();
    [Tooltip("Distance to start braking before a brake point")]
    public float brakePointRadius = 8f;
    [Tooltip("Target speed (in MPH) to slow down to at brake points")]
    public float brakePointTargetSpeedMph = 10f;

    [Header("Movement Settings")]
    public float waypointPassThreshold = 2f;
    public float maxSteeringAngle = 45f;
    public float steeringSmoothing = 5f;
    public float lookaheadDistance = 5f;

    [Header("Throttle Control")]
    public float baseThrottle = 1f;
    public float minThrottle = 0.2f;
    public float turnSlowdownAngle = 10f;
    public float fullBrakeAngle = 60f;

    [Header("Speed Prediction")]
    public float maxSafeSpeed = 15f;
    public float minSafeSpeed = 5f;
    public float speedWeight = 0.7f;

    [Header("Recovery Settings")]
    [Tooltip("How long to stop before reversing (seconds)")]
    public float recoveryStopDuration = 1f;
    [Tooltip("How long to reverse when spun out (seconds)")]
    public float recoveryReverseDuration = 2f;
    [Tooltip("How long to pause after reversing (seconds)")]
    public float recoveryPauseDuration = 0.5f;

    private int currentIndex = 0;
    private AIDriver aiDriver;
    private Rigidbody rb;
    //private bool reversing = false;

    // Thresholds for reversing logic
    private const float reverseEnterAlignment = 0.0f;   // Enter reverse if facing >90° away
    private const float reverseExitAlignment = 0.5f;    // Exit reverse if facing within ~60°
    private const float minReverseSpeed = 1.0f;         // Only reverse if nearly stopped

    // Recovery state machine
    private enum RecoveryState { None, Stopping, Reversing, Recovering }
    private RecoveryState recoveryState = RecoveryState.None;
    private float recoveryTimer = 0f;

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
        float closestT = 0f;
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
                closestT = t;
            }
        }
        currentIndex = closestIndex;

        Transform wpA = waypoints[currentIndex];
        Transform wpB = waypoints[(currentIndex + 1) % waypoints.Count];

        // --- Dynamic lookahead distance based on speed ---
        float speed = rb.linearVelocity.magnitude;
        float dynamicLookahead = Mathf.Lerp(lookaheadDistance, lookaheadDistance * 3f, Mathf.InverseLerp(0, maxSafeSpeed, speed));

        // --- Use improved lookahead ---
        Vector3 lookaheadPoint = GetLookaheadPoint(pos, currentIndex, closestT, dynamicLookahead);

        // Steering calculation
        Vector3 localTarget = transform.InverseTransformPoint(lookaheadPoint);
        float angleToTarget = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
        float normalizedSteering = Mathf.Clamp(angleToTarget / maxSteeringAngle, -1f, 1f);

        // --- Path direction (for spin-out detection) ---
        Vector3 pathDir = (lookaheadPoint - pos).normalized;
        Vector3 carForward = transform.forward;
        float alignment = Vector3.Dot(carForward, pathDir); // 1 = aligned, -1 = opposite

        // --- Simple recovery state machine ---
        switch (recoveryState)
        {
            case RecoveryState.None:
                if (alignment < 0f) // Facing away from path
                {
                    recoveryState = RecoveryState.Stopping;
                    recoveryTimer = recoveryStopDuration;
                    aiDriver.throttle = 0f;
                    return;
                }
                break;

            case RecoveryState.Stopping:
                recoveryTimer -= Time.deltaTime;
                aiDriver.throttle = 0f;
                if (recoveryTimer <= 0f)
                {
                    recoveryState = RecoveryState.Reversing;
                    recoveryTimer = recoveryReverseDuration;
                }
                return;

            case RecoveryState.Reversing:
                recoveryTimer -= Time.deltaTime;
                // Steer so the front of the car turns toward the path while reversing
                Vector3 localPathDir = transform.InverseTransformDirection(pathDir);
                float angleToPath = Mathf.Atan2(localPathDir.x, localPathDir.z) * Mathf.Rad2Deg;
                float reverseSteering = Mathf.Clamp(-angleToPath / maxSteeringAngle, -1f, 1f);

                aiDriver.steering = Mathf.Lerp(aiDriver.steering, reverseSteering, Time.deltaTime * steeringSmoothing);
                aiDriver.throttle = -1f;
                if (recoveryTimer <= 0f)
                {
                    recoveryState = RecoveryState.Recovering;
                    recoveryTimer = recoveryPauseDuration;
                }
                return;

            case RecoveryState.Recovering:
                recoveryTimer -= Time.deltaTime;
                aiDriver.throttle = 0f;
                if (recoveryTimer <= 0f)
                {
                    recoveryState = RecoveryState.None;
                }
                return;
        }

        // --- Brake Point Detection ---
        bool nearBrakePoint = false;
        foreach (var bp in brakePoints)
        {
            if (bp == null) continue;
            float dist = Vector3.Distance(pos, bp.position);
            if (dist < brakePointRadius)
            {
                nearBrakePoint = true;
                break;
            }
        }

        if (nearBrakePoint)
        {
            // --- Throttle logic for braking based on speed in mph ---
            float speedMph = aiDriver.CurrentSpeedMph;
            if (speedMph > brakePointTargetSpeedMph)
                aiDriver.throttle = -1f; // full brake/reverse
            else if (speedMph > brakePointTargetSpeedMph * 0.5f)
                aiDriver.throttle = -0.5f; // moderate brake
            else
                aiDriver.throttle = 0f; // coasting/stop

            aiDriver.steering = Mathf.Lerp(aiDriver.steering, normalizedSteering, Time.deltaTime * steeringSmoothing);
            return;
        }

        // --- Normal driving logic ---
        aiDriver.steering = Mathf.Lerp(aiDriver.steering, normalizedSteering, Time.deltaTime * steeringSmoothing);

        // --- Predict Upcoming Turn ---
        int nextIndex = (currentIndex + 1) % waypoints.Count;
        int nextNextIndex = (currentIndex + 2) % waypoints.Count;
        Vector3 nextA = waypoints[nextIndex].position;
        Vector3 nextB = waypoints[nextNextIndex].position;
        Vector3 dirCurrent = (wpB.position - wpA.position).normalized;
        Vector3 dirNext = (nextB - nextA).normalized;
        float bendAngle = Vector3.Angle(dirCurrent, dirNext);

        // --- Speed & Braking Control ---
        float speedFactor = Mathf.InverseLerp(minSafeSpeed, maxSafeSpeed, speed);
        float turnAngleFactor = Mathf.InverseLerp(turnSlowdownAngle, fullBrakeAngle, Mathf.Max(Mathf.Abs(angleToTarget), bendAngle));
        float totalBrakeFactor = Mathf.Clamp01(
            Mathf.Lerp(turnAngleFactor, turnAngleFactor * speedFactor, speedWeight)
        );
        float targetThrottle = Mathf.Lerp(baseThrottle, minThrottle, totalBrakeFactor);

        // Smooth transitions
        aiDriver.throttle = Mathf.Lerp(aiDriver.throttle, targetThrottle, Time.deltaTime * 3f);
    }

    Vector3 GetLookaheadPoint(Vector3 pos, int startIndex, float tOnSegment, float lookaheadDist)
    {
        int segA = startIndex;
        int segB = (segA + 1) % waypoints.Count;
        Vector3 a = waypoints[segA].position;
        Vector3 b = waypoints[segB].position;
        Vector3 ab = b - a;

        float segLength = ab.magnitude;
        float distOnSeg = (1f - tOnSegment) * segLength;

        if (lookaheadDist <= distOnSeg)
        {
            float lookaheadT = tOnSegment + (lookaheadDist / segLength);
            return Vector3.Lerp(a, b, lookaheadT);
        }
        else
        {
            float remaining = lookaheadDist - distOnSeg;
            return GetLookaheadPoint(pos, segB, 0f, remaining);
        }
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

        if (brakePoints != null)
        {
            Gizmos.color = Color.yellow;
            foreach (var bp in brakePoints)
            {
                if (bp != null)
                {
                    Gizmos.DrawWireSphere(bp.position, brakePointRadius);
                }
            }
        }

        if (Application.isPlaying && waypoints.Count > currentIndex)
        {
            // Draw lookahead point using the same logic as Update
            Vector3 pos = transform.position;

            // Find closest segment and t
            float minDist = float.MaxValue;
            int closestIndex = currentIndex;
            float closestT = 0f;
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
                    closestT = t;
                }
            }

            float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
            float dynamicLookahead = Mathf.Lerp(lookaheadDistance, lookaheadDistance * 3f, Mathf.InverseLerp(0, maxSafeSpeed, speed));
            Vector3 lookaheadPoint = GetLookaheadPoint(pos, closestIndex, closestT, dynamicLookahead);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, lookaheadPoint);
            Gizmos.DrawWireSphere(lookaheadPoint, 0.5f);
        }
    }
#endif
}
