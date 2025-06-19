using UnityEngine;

public class SteeringWheelController : MonoBehaviour
{
    [Header("Steering Wheel Properties")]
    public float rotationSpeed = 100f; // Speed of wheel rotation
    public float maxRotationAngle = 90f; // Maximum rotation angle of the wheel
    public float minRotationAngle = -90f; // Minimum rotation angle of the wheel

    private CarInputActions carControls; // Reference to the new input system

    void Awake()
    {
        carControls = new CarInputActions(); // Initialize Input Actions
    }
    void OnEnable()
    {
        carControls.Enable();
    }
    void OnDisable()
    {
        carControls.Disable();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // Read the Vector2 input from the new Input System
        Vector2 inputVector = carControls.Car.Movement.ReadValue<Vector2>();

        // Get the horizontal input for steering and invert it
        float hInput = -inputVector.x; // Invert steering input

        // Calculate the desired rotation angle based on input
        float targetAngle = Mathf.Clamp(hInput * maxRotationAngle, minRotationAngle, maxRotationAngle);

        // Smoothly rotate the wheel towards the target angle
        float currentAngle = transform.localEulerAngles.z;
        currentAngle = (currentAngle > 180) ? currentAngle - 360 : currentAngle; // Convert to -180 to 180 range
        float newAngle = Mathf.MoveTowards(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
        transform.localEulerAngles = new Vector3(0, 0, newAngle);
    }
}
