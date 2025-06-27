using UnityEngine;

[RequireComponent(typeof(CarController))]
public class AIDriver : MonoBehaviour
{
    private CarController carController;

    [Tooltip("Steering input (-1 = full left, 1 = full right)")]
    [Range(-1f, 1f)]
    public float steering = 0f;

    [Tooltip("Throttle input (-1 = full reverse, 0 = stop, 1 = full forward)")]
    [Range(-1f, 1f)]
    public float throttle = 1f;

    void Awake()
    {
        carController = GetComponent<CarController>();
    }

    void Update()
    {
        float clampedSteering = Mathf.Clamp(steering, -1f, 1f);
        float clampedThrottle = Mathf.Clamp(throttle, -1f, 1f);

        carController.inputVector = new Vector2(clampedSteering, clampedThrottle);
    }
}
