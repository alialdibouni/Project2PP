using UnityEngine;

public class TargetPlayer : MonoBehaviour
{
    [Header("Ray Cone Settings")]
    public Camera targetCamera;
    public int rayCount = 20;
    public float coneAngle = 30f;
    public float rayLength = 10f;

    [Header("Raycast Ignore Settings")]
    public string[] ignoredTags;

    private bool playerInView = false;
    public bool IsPlayerInView => playerInView;

    // Store ray results for gizmo drawing
    private bool[] hitPlayer;
    private Vector3[] rayDirections;
    private float[] rayHitDistances;
    private bool[] rayHitSomething;
    private int raysDrawn = 0;

    [Header("Debug Log Settings")]
    public float logCooldown = 1f; // seconds between logs
    private float logTimer = 0f;
    private bool lastLoggedPlayerInView = false;

    void Start()
    {
        hitPlayer = new bool[rayCount];
        rayDirections = new Vector3[rayCount];
        rayHitDistances = new float[rayCount];
        rayHitSomething = new bool[rayCount];
    }

    void Update()
    {
        UpdatePlayerInView();

        // Handle log cooldown
        logTimer += Time.deltaTime;
        if (logTimer >= logCooldown)
        {
            if (playerInView != lastLoggedPlayerInView)
            {
                Debug.Log(playerInView ? "Player is in view" : "Player is not in view");
                lastLoggedPlayerInView = playerInView;
                logTimer = 0f;
            }
        }
    }

    // This method updates playerInView and stores ray info for gizmos
    private void UpdatePlayerInView()
    {
        if (targetCamera == null)
            return;

        Vector3 origin = targetCamera.transform.position;
        Vector3 forward = targetCamera.transform.forward;

        playerInView = false;

        int grid = Mathf.CeilToInt(Mathf.Sqrt(rayCount));
        raysDrawn = 0;

        for (int y = 0; y < grid; y++)
        {
            float v = (grid == 1) ? 0.5f : (float)y / (grid - 1);
            float pitch = Mathf.Lerp(-coneAngle / 2f, coneAngle / 2f, v);

            for (int x = 0; x < grid; x++)
            {
                if (raysDrawn >= rayCount)
                    break;

                float u = (grid == 1) ? 0.5f : (float)x / (grid - 1);
                float yaw = Mathf.Lerp(-coneAngle / 2f, coneAngle / 2f, u);

                Quaternion rot = Quaternion.Euler(pitch, yaw, 0);
                Vector3 dir = rot * forward;

                Vector3 currentOrigin = origin;
                float remainingLength = rayLength;
                bool hitSomething = false;
                bool hitPlayerThisRay = false;
                float hitDistance = rayLength;

                while (remainingLength > 0f)
                {
                    Ray ray = new Ray(currentOrigin, dir);
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit, remainingLength))
                    {
                        hitSomething = true;
                        bool ignore = false;
                        foreach (var tag in ignoredTags)
                        {
                            if (hit.collider.CompareTag(tag))
                            {
                                currentOrigin = hit.point + dir * 0.01f;
                                remainingLength -= hit.distance + 0.01f;
                                ignore = true;
                                break;
                            }
                        }
                        if (ignore)
                        {
                            continue;
                        }

                        if (hit.collider.CompareTag("Player"))
                        {
                            hitPlayerThisRay = true;
                            hitDistance = rayLength - remainingLength + hit.distance;
                            playerInView = true;
                        }
                        else
                        {
                            hitDistance = rayLength - remainingLength + hit.distance;
                        }
                        break;
                    }
                    else
                    {
                        break;
                    }
                }

                if (hitPlayer != null && raysDrawn < hitPlayer.Length)
                {
                    hitPlayer[raysDrawn] = hitPlayerThisRay;
                    rayDirections[raysDrawn] = dir;
                    rayHitDistances[raysDrawn] = hitDistance;
                    rayHitSomething[raysDrawn] = hitSomething;
                }

                raysDrawn++;
            }
        }
    }

    void OnDrawGizmos()
    {
        // Only draw if we have ray data (from Update)
        if (targetCamera == null || rayDirections == null)
            return;

        Vector3 origin = targetCamera.transform.position;

        for (int i = 0; i < raysDrawn; i++)
        {
            if (hitPlayer[i])
            {
                Gizmos.color = Color.green;
            }
            else if (playerInView)
            {
                Gizmos.color = Color.yellow;
            }
            else
            {
                Gizmos.color = Color.red;
            }

            if (rayHitSomething[i])
                Gizmos.DrawRay(origin, rayDirections[i] * rayHitDistances[i]);
            else
                Gizmos.DrawRay(origin, rayDirections[i] * rayLength);
        }
    }
}
