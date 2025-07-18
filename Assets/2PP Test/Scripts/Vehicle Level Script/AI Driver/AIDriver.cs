using UnityEngine;

[RequireComponent(typeof(CarController))]
public class AIDriver : MonoBehaviour
{
    private CarController carController;
    private Rigidbody rb;
    private VehicleHealth vehicleHealth;

    [Header("Input")]
    [Tooltip("Steering input (-1 = left, 1 = right)")]
    [Range(-1f, 1f)] public float steering = 0f;

    [Tooltip("Default throttle (-1 = reverse | 0 = standstill | 1 = full forward)")]
    [Range(-1f, 1f)] public float throttle = 1f;

    [Header("Stuck Detection")]
    public float stuckVelocityThreshold = 0.5f;
    public float stuckDuration = 2f;
    public float minReverseDuration = 1f;
    public float maxReverseDuration = 1f;

    private float stuckTimer = 0f;
    private float reverseTimer = 0f;
    private bool reversing = false;

    [Header("Debug")]
    [Tooltip("Current speed of the car in MPH (read-only)")]
    [SerializeField] private float currentSpeedMph;

    public float CurrentSpeedMph => currentSpeedMph;

    void Awake()
    {
        carController = GetComponent<CarController>();
        rb = GetComponent<Rigidbody>();
        vehicleHealth = GetComponent<VehicleHealth>();
    }

    void Update()
    {
        float speed = rb.linearVelocity.magnitude;
        currentSpeedMph = speed * 2.23694f;

        // Stop the car if health is zero
        if (vehicleHealth != null && vehicleHealth.currentHealth <= 0f)
        {
            throttle = 0f;
            currentSpeedMph = 0f;
            carController.inputVector = Vector2.zero;
            return;
        }

        if (!reversing)
        {
            // Check if the car is stuck while trying to move forward
            if (throttle > 0.1f && speed < stuckVelocityThreshold)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= stuckDuration)
                {
                    reversing = true;
                    reverseTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }
        else
        {
            reverseTimer += Time.deltaTime;

            // Continue reversing until either we reach min duration AND start moving, OR max time expires
            bool movingNow = speed > stuckVelocityThreshold;
            if ((reverseTimer >= minReverseDuration && movingNow) || reverseTimer >= maxReverseDuration)
            {
                reversing = false;
                stuckTimer = 0f;
            }
        }

        // Use full throttle during reverse
        float activeThrottle = reversing ? -1f : throttle;
        carController.inputVector = new Vector2(Mathf.Clamp(steering, -1f, 1f), Mathf.Clamp(activeThrottle, -1f, 1f));
    }
}
