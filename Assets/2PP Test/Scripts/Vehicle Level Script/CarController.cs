using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Car Properties")]
    public float motorTorque = 2000f;
    public float brakeTorque = 2000f;
    public float maxSpeed = 20f;
    public float steeringRange = 30f;
    public float steeringRangeAtMaxSpeed = 10f;
    public float centreOfGravityOffset = -1f;

    [Header("Engine Audio")]
    public AudioSource engineAudioSource; // Assign in Inspector
    public float minEnginePitch = 1f;
    public float maxEnginePitch = 2f;

    [Header("External Input")]
    [Tooltip("X = Steering, Y = Throttle/Brake")]
    public Vector2 inputVector = Vector2.zero; // Set externally by player or AI

    public float CurrentSteeringInput { get; private set; }
    public float CurrentAccelerationInput { get; private set; }

    private WheelControl[] wheels;
    private Rigidbody rigidBody;

    void Start()
    {
        rigidBody = GetComponent<Rigidbody>();

        // Adjust center of mass to improve stability
        Vector3 centerOfMass = rigidBody.centerOfMass;
        centerOfMass.y += centreOfGravityOffset;
        rigidBody.centerOfMass = centerOfMass;

        wheels = GetComponentsInChildren<WheelControl>();

        if (engineAudioSource != null)
        {
            engineAudioSource.loop = true;
            engineAudioSource.playOnAwake = false;
            if (!engineAudioSource.isPlaying)
                engineAudioSource.Play();
        }
    }

    void FixedUpdate()
    {
        float vInput = inputVector.y;
        float hInput = inputVector.x;

        CurrentSteeringInput = hInput;
        CurrentAccelerationInput = vInput;

        float forwardSpeed = Vector3.Dot(transform.forward, rigidBody.linearVelocity);
        float speedFactor = Mathf.InverseLerp(0, maxSpeed, Mathf.Abs(forwardSpeed));

        float currentMotorTorque = Mathf.Lerp(motorTorque, 0, speedFactor);
        float currentSteerRange = Mathf.Lerp(steeringRange, steeringRangeAtMaxSpeed, speedFactor);
        bool isAccelerating = Mathf.Sign(vInput) == Mathf.Sign(forwardSpeed);

        // Engine audio pitch update
        if (engineAudioSource != null)
        {
            float targetPitch = minEnginePitch;
            if (forwardSpeed > 0.1f)
            {
                targetPitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, speedFactor);
            }
            engineAudioSource.pitch = Mathf.MoveTowards(engineAudioSource.pitch, targetPitch, Time.fixedDeltaTime * 2f);
        }

        // Apply input to wheels
        foreach (var wheel in wheels)
        {
            if (wheel.steerable)
            {
                wheel.WheelCollider.steerAngle = hInput * currentSteerRange;
            }

            if (isAccelerating)
            {
                if (wheel.motorized)
                {
                    wheel.WheelCollider.motorTorque = vInput * currentMotorTorque;
                }
                wheel.WheelCollider.brakeTorque = 0f;
            }
            else
            {
                wheel.WheelCollider.motorTorque = 0f;
                wheel.WheelCollider.brakeTorque = Mathf.Abs(vInput) * brakeTorque;
            }
        }
    }
}
