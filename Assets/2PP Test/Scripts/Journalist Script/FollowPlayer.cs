using UnityEngine;
using UnityEngine.AI;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class FollowPlayer : MonoBehaviour
{
    [Header("Target")]
    public Transform targetTransform;

    [Header("Speed Settings")]
    [SerializeField] private float defaultSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;

    private NavMeshAgent _nav;
    private InputReader _inputReader;

    private void Awake()
    {
        _nav = GetComponent<NavMeshAgent>();
        if (_nav != null)
        {
            _nav.speed = defaultSpeed;
        }
    }

    private void OnEnable()
    {
        TryBindInputReader();
        SetAgentSpeed(defaultSpeed);
    }

    private void OnDisable()
    {
        UnbindInputReader();
        SetAgentSpeed(defaultSpeed);
    }

    private void Update()
    {
        if (_nav == null || targetTransform == null) return;

        // Follow player
        _nav.SetDestination(targetTransform.position);

        // In case player reference changes at runtime, try rebind
        if (_inputReader == null)
        {
            TryBindInputReader();
        }
    }

    private void TryBindInputReader()
    {
        if (targetTransform == null || _inputReader != null) return;

        _inputReader = targetTransform.GetComponentInParent<InputReader>();
        if (_inputReader != null)
        {
            _inputReader.onSprintActivated += OnSprintActivated;
            _inputReader.onSprintDeactivated += OnSprintDeactivated;
        }
    }

    private void UnbindInputReader()
    {
        if (_inputReader != null)
        {
            _inputReader.onSprintActivated -= OnSprintActivated;
            _inputReader.onSprintDeactivated -= OnSprintDeactivated;
            _inputReader = null;
        }
    }

    private void OnSprintActivated()
    {
        SetAgentSpeed(sprintSpeed);
    }

    private void OnSprintDeactivated()
    {
        SetAgentSpeed(defaultSpeed);
    }

    private void SetAgentSpeed(float speed)
    {
        if (_nav != null) _nav.speed = speed;
    }
}
