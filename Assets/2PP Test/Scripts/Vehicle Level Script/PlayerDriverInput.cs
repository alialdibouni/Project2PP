using UnityEngine;

[RequireComponent(typeof(CarController))]
public class PlayerDriverInput : MonoBehaviour
{
    private CarController carController;
    private CarInputActions carControls;
    private Rigidbody rb;

    [Header("Debug")]
    [Tooltip("Current speed of the car in MPH (read-only)")]
    [SerializeField] private float currentSpeedMph;

    public float CurrentSpeedMph => currentSpeedMph;

    void Awake()
    {
        carController = GetComponent<CarController>();
        carControls = new CarInputActions();
        rb = GetComponent<Rigidbody>();
    }

    void OnEnable() => carControls.Enable();
    void OnDisable() => carControls.Disable();

    void Update()
    {
        Vector2 input = carControls.Car.Movement.ReadValue<Vector2>();
        carController.inputVector = input;

        float speed = rb.linearVelocity.magnitude;
        currentSpeedMph = speed * 2.23694f;
    }
}
