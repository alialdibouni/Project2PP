using UnityEngine;
using UnityEngine.AI;

public class BasicHeadBob : MonoBehaviour
{
    [Header("Bobbing")]
    [SerializeField] float amplitude = 0.02f;   // Height of the bob (meters)
    [SerializeField] float frequency = 6f;      // Cycles per second

    [Header("Activation")]
    [SerializeField] bool onlyWhenMoving = false;
    [SerializeField] float movementThreshold = 0.1f; // Min speed to consider "moving"
    [SerializeField] Transform movementReference;    // Optional: whose movement to check (defaults to this)

    [Header("Space")]
    [SerializeField] bool useLocalSpace = true; // Bob in local vs world space
    [SerializeField] float returnSmoothness = 10f; // How quickly it eases back to rest

    Vector3 _startLocalPos;
    Vector3 _startWorldPos;
    Vector3 _currentOffset;
    Vector3 _lastRefPos;

    Rigidbody _rb;
    CharacterController _cc;
    NavMeshAgent _agent;

    void Start()
    {
        if (movementReference == null) movementReference = transform;

        _startLocalPos = transform.localPosition;
        _startWorldPos = transform.position;
        _lastRefPos = movementReference.position;

        _rb = movementReference.GetComponent<Rigidbody>();
        _cc = movementReference.GetComponent<CharacterController>();
        _agent = movementReference.GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        float speed = GetSpeed();
        bool bobActive = !onlyWhenMoving || speed > movementThreshold;

        Vector3 targetOffset = bobActive
            ? new Vector3(0f, Mathf.Sin(Time.time * frequency) * amplitude, 0f)
            : Vector3.zero;

        // Smooth towards target offset to avoid snapping when stopping/starting
        _currentOffset = Vector3.Lerp(_currentOffset, targetOffset, Time.deltaTime * returnSmoothness);

        if (useLocalSpace)
            transform.localPosition = _startLocalPos + _currentOffset;
        else
            transform.position = _startWorldPos + _currentOffset;
    }

    void OnDisable()
    {
        // Reset position when disabled to avoid leaving residual offset
        transform.localPosition = _startLocalPos;
        transform.position = _startWorldPos;
        _currentOffset = Vector3.zero;
    }

    float GetSpeed()
    {
        // Prefer component velocities if available
        if (_rb != null) return _rb.linearVelocity.magnitude;
        if (_cc != null) return _cc.velocity.magnitude;
        if (_agent != null) return _agent.velocity.magnitude;

        // Fallback: estimate from position delta
        Vector3 pos = movementReference.position;
        float speed = (pos - _lastRefPos).magnitude / Mathf.Max(Time.deltaTime, 1e-5f);
        _lastRefPos = pos;
        return speed;
    }
}
