using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAnimationAdapter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;

    [Header("Grounding")]
    [SerializeField] private Transform groundCheck; // optional; if null uses transform.position
    [SerializeField] private LayerMask groundMask = ~0; // default: everything
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private float groundRayLength = 0.6f;
    [SerializeField] private float groundOffsetUp = 0.1f;

    [Header("Gait thresholds (m/s)")]
    [SerializeField] private float walkSpeed = 1.4f;
    [SerializeField] private float runSpeed = 2.5f;
    [SerializeField] private float sprintSpeed = 7f;

    [Header("Smoothing")]
    [SerializeField] private float speedDamping = 10f;

    [Header("Input pulse durations")]
    [SerializeField] private float startPulseDuration = 0.20f;
    [SerializeField] private float pressedPulseDuration = 0.12f;
    [SerializeField] private float moveThreshold = 0.08f;

    // Animator parameter hashes from Synty controller
    private readonly int _movementInputTappedHash = Animator.StringToHash("MovementInputTapped");
    private readonly int _movementInputPressedHash = Animator.StringToHash("MovementInputPressed");
    private readonly int _movementInputHeldHash = Animator.StringToHash("MovementInputHeld");
    private readonly int _shuffleDirectionXHash = Animator.StringToHash("ShuffleDirectionX");
    private readonly int _shuffleDirectionZHash = Animator.StringToHash("ShuffleDirectionZ");

    private readonly int _moveSpeedHash = Animator.StringToHash("MoveSpeed");
    private readonly int _currentGaitHash = Animator.StringToHash("CurrentGait");

    private readonly int _isJumpingAnimHash = Animator.StringToHash("IsJumping");
    private readonly int _fallingDurationHash = Animator.StringToHash("FallingDuration");

    private readonly int _inclineAngleHash = Animator.StringToHash("InclineAngle");

    private readonly int _strafeDirectionXHash = Animator.StringToHash("StrafeDirectionX");
    private readonly int _strafeDirectionZHash = Animator.StringToHash("StrafeDirectionZ");

    private readonly int _forwardStrafeHash = Animator.StringToHash("ForwardStrafe");
    private readonly int _cameraRotationOffsetHash = Animator.StringToHash("CameraRotationOffset");
    private readonly int _isStrafingHash = Animator.StringToHash("IsStrafing");
    private readonly int _isTurningInPlaceHash = Animator.StringToHash("IsTurningInPlace");

    private readonly int _isCrouchingHash = Animator.StringToHash("IsCrouching");

    private readonly int _isWalkingHash = Animator.StringToHash("IsWalking");
    private readonly int _isStoppedHash = Animator.StringToHash("IsStopped");
    private readonly int _isStartingHash = Animator.StringToHash("IsStarting");

    private readonly int _isGroundedHash = Animator.StringToHash("IsGrounded");

    private float smoothedSpeed;
    private float fallingDuration;
    private bool isGrounded;

    // Input/transition emulation
    private bool wasMovingLastFrame;
    private float startPulseTimer;
    private float pressedPulseTimer;

    private void Reset()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Ensure root motion doesn't fight the NavMeshAgent
        if (animator != null) animator.applyRootMotion = false;
    }

    private void Update()
    {
        UpdateGrounded();

        // Speed from NavMeshAgent
        var vel = agent != null ? agent.velocity : Vector3.zero;
        float speed2D = new Vector3(vel.x, 0f, vel.z).magnitude;
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed2D, speedDamping * Time.deltaTime);

        bool isMoving = speed2D > moveThreshold;

        // Emulate player input flags used by the sample controller
        if (isMoving && !wasMovingLastFrame)
        {
            startPulseTimer = startPulseDuration;
            pressedPulseTimer = pressedPulseDuration;
        }

        startPulseTimer = Mathf.Max(0f, startPulseTimer - Time.deltaTime);
        pressedPulseTimer = Mathf.Max(0f, pressedPulseTimer - Time.deltaTime);

        bool inputHeld = isMoving;
        bool inputPressed = pressedPulseTimer > 0f;
        bool inputTapped = false;

        // Drive animator params (match names/types the sample writes)
        animator.SetFloat(_moveSpeedHash, smoothedSpeed);
        animator.SetInteger(_currentGaitHash, CalculateGait(speed2D));

        animator.SetBool(_movementInputHeldHash, inputHeld);
        animator.SetBool(_movementInputPressedHash, inputPressed);
        animator.SetBool(_movementInputTappedHash, inputTapped);

        // Default strafe/blend values for forward locomotion
        animator.SetFloat(_strafeDirectionZHash, 1f);
        animator.SetFloat(_strafeDirectionXHash, 0f);
        animator.SetFloat(_forwardStrafeHash, 1f);
        animator.SetFloat(_cameraRotationOffsetHash, 0f);
        animator.SetFloat(_isStrafingHash, 0f); // 0 = not strafing

        animator.SetBool(_isTurningInPlaceHash, false);
        animator.SetBool(_isCrouchingHash, false);

        animator.SetBool(_isWalkingHash, speed2D >= moveThreshold && speed2D < ((walkSpeed + runSpeed) * 0.5f));
        animator.SetBool(_isStoppedHash, !isMoving);
        animator.SetBool(_isStartingHash, startPulseTimer > 0f);

        animator.SetBool(_isGroundedHash, isGrounded);
        animator.SetFloat(_inclineAngleHash, 0f);

        if (isGrounded) fallingDuration = 0f; else fallingDuration += Time.deltaTime;
        animator.SetFloat(_fallingDurationHash, fallingDuration);

        animator.SetBool(_isJumpingAnimHash, false);

        // Optional: expose shuffle for start animations
        animator.SetFloat(_shuffleDirectionZHash, 1f);
        animator.SetFloat(_shuffleDirectionXHash, 0f);

        wasMovingLastFrame = isMoving;
    }

    private int CalculateGait(float speed)
    {
        float runThreshold = (walkSpeed + runSpeed) * 0.5f;
        float sprintThreshold = (runSpeed + sprintSpeed) * 0.5f;

        if (speed < 0.01f) return 0;          // Idle
        if (speed < runThreshold) return 1;   // Walk
        if (speed < sprintThreshold) return 2;// Run
        return 3;                             // Sprint
    }

    private void UpdateGrounded()
    {
        Vector3 origin = groundCheck ? groundCheck.position : transform.position + Vector3.up * groundOffsetUp;
        float rayLen = groundRayLength;

        // SphereCast for a forgiving ground check
        isGrounded = Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out _, rayLen, groundMask, QueryTriggerInteraction.Ignore);
    }
}