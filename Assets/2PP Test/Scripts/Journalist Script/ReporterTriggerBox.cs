using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using Synty.AnimationBaseLocomotion.Samples.InputSystem; // InputReader + generated Controls
using System.Reflection;

[RequireComponent(typeof(Collider))]
public class ReporterTriggerBox : MonoBehaviour
{
    [Header("B-Roll Targets")]
    [SerializeField] private Transform _bRollLookAt;         // "BRollLookAt"
    [SerializeField] private Transform _bRollCameraPosition; // "BRollCameraPosition"

    private Controls _controls;
    private InputReader _playerInputReader;
    private bool _playerInside;
    private bool _pendingUnlockOnReenable;

    // B-Roll runtime state
    private bool _bRollActive;
    private FollowPlayerCamera _followPlayerCamera;
    private Transform _originalLookAtTarget;
    private Transform _cameraManTransform;
    private Vector3 _cameraManOriginalPos;
    private NavMeshAgent _cameraManAgent;

    // If CameraMan has a follower script, disable it during B-roll to avoid destination being overwritten
    private FollowPlayer _cameraManFollower;
    private bool _cameraManFollowerWasEnabled;

    // Cache original stopping distance to restore after B-Roll
    private float _cameraManOriginalStoppingDistance = -1f;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        _controls = new Controls();
    }

    private void OnEnable()
    {
        _controls.Player.LockOn.performed += OnLockOnPerformed;
    }

    private void OnDisable()
    {
        _controls.Player.LockOn.performed -= OnLockOnPerformed;
        _controls.Player.Disable();
    }

    private void OnTriggerEnter(Collider other)
    {
        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null) return;

        _playerInputReader = reader;
        _playerInside = true;

        _controls.Player.Enable();
    }

    private void OnTriggerExit(Collider other)
    {
        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null || reader != _playerInputReader) return;

        if (_bRollActive) RevertBRoll();

        _playerInputReader.SuppressLockOnToggle = false;
        ResetInputReaderInversionState(_playerInputReader);

        _playerInside = false;
        _playerInputReader = null;
        _pendingUnlockOnReenable = false;

        _controls.Player.Disable();
    }

    private void Update()
    {
        if (!_playerInside || _playerInputReader == null) return;

        if (_bRollActive && !IsReaderLockedOn(_playerInputReader))
        {
            RevertBRoll();
        }

        if (IsReaderLockedOn(_playerInputReader)
            && Keyboard.current != null
            && Keyboard.current.bKey.wasPressedThisFrame)
        {
            ToggleBRoll();
        }
    }

    private void OnLockOnPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (!_playerInside || _playerInputReader == null) return;

        _playerInputReader.SuppressLockOnToggle = true;

        if (_playerInputReader.enabled)
        {
            // ENTER REPORTER MODE
            if (!IsReaderLockedOn(_playerInputReader))
            {
                _playerInputReader.onLockOnToggled?.Invoke();
                _playerInputReader.onSprintDeactivated?.Invoke();
            }
            _playerInputReader.enabled = false;
            _pendingUnlockOnReenable = true;
        }
        else
        {
            // EXIT REPORTER MODE
            _playerInputReader.enabled = true;

            if (IsReaderLockedOn(_playerInputReader))
            {
                _playerInputReader.onLockOnToggled?.Invoke();
                _playerInputReader.onSprintDeactivated?.Invoke();
            }

            if (_bRollActive) RevertBRoll();
            ResetInputReaderInversionState(_playerInputReader);

            _pendingUnlockOnReenable = false;
        }

        _playerInputReader.SuppressLockOnToggle = false;
    }

    private void ToggleBRoll()
    {
        if (_bRollActive)
        {
            RevertBRoll();
            return;
        }

        TryActivateBRoll();
    }

    private void TryActivateBRoll()
    {
        if (_bRollActive) return;
        if (_bRollLookAt == null || _bRollCameraPosition == null)
        {
            Debug.LogWarning("[ReporterTriggerBox] B-Roll targets not assigned.");
            return;
        }

        if (_cameraManTransform == null)
        {
            var camManGO = GameObject.FindGameObjectWithTag("CameraMan");
            if (camManGO == null)
            {
                Debug.LogWarning("[ReporterTriggerBox] No GameObject with tag 'CameraMan' found.");
                return;
            }
            _cameraManTransform = camManGO.transform;
            _cameraManAgent = _cameraManTransform.GetComponent<NavMeshAgent>();
            _cameraManFollower = _cameraManTransform.GetComponent<FollowPlayer>();
        }
        else
        {
            if (_cameraManAgent == null) _cameraManAgent = _cameraManTransform.GetComponent<NavMeshAgent>();
            if (_cameraManFollower == null) _cameraManFollower = _cameraManTransform.GetComponent<FollowPlayer>();
        }

        if (_cameraManAgent != null && _cameraManOriginalStoppingDistance < 0f)
        {
            _cameraManOriginalStoppingDistance = _cameraManAgent.stoppingDistance;
        }

        if (_followPlayerCamera == null)
        {
            _followPlayerCamera = FindObjectOfType<FollowPlayerCamera>();
            if (_followPlayerCamera == null)
            {
                Debug.LogWarning("[ReporterTriggerBox] FollowPlayerCamera not found in scene.");
                return;
            }
        }

        _originalLookAtTarget = GetFollowCameraTarget(_followPlayerCamera);
        _cameraManOriginalPos = _cameraManTransform.position;

        // Disable follower that might overwrite destination each frame
        if (_cameraManFollower != null)
        {
            _cameraManFollowerWasEnabled = _cameraManFollower.enabled;
            _cameraManFollower.enabled = false;
        }

        SetFollowCameraTarget(_followPlayerCamera, _bRollLookAt);

        // Move CameraMan via NavMesh with stoppingDistance = 0 to reach exact BRoll position
        MoveCameraManTo(_bRollCameraPosition.position, 0f);

        _bRollActive = true;
    }

    private void RevertBRoll()
    {
        if (_followPlayerCamera != null && _originalLookAtTarget != null)
        {
            SetFollowCameraTarget(_followPlayerCamera, _originalLookAtTarget);
        }

        // If there was a follower, restore it (and stopping distance) after clearing any agent path.
        if (_cameraManFollower != null)
        {
            if (_cameraManAgent != null && _cameraManAgent.isOnNavMesh)
            {
                _cameraManAgent.ResetPath();
                if (_cameraManOriginalStoppingDistance >= 0f)
                    _cameraManAgent.stoppingDistance = _cameraManOriginalStoppingDistance;
            }
            _cameraManFollower.enabled = _cameraManFollowerWasEnabled;
        }
        else
        {
            // Drive back to original position; restore original stopping distance for the move if known
            float stop = _cameraManOriginalStoppingDistance >= 0f ? _cameraManOriginalStoppingDistance : 0f;
            MoveCameraManTo(_cameraManOriginalPos, stop);
        }

        _bRollActive = false;
    }

    private void MoveCameraManTo(Vector3 destination, float stoppingDistance)
    {
        if (_cameraManTransform == null) return;

        if (_cameraManAgent != null && _cameraManAgent.isOnNavMesh)
        {
            // Snap destination to a nearby navmesh point to ensure it’s reachable
            if (NavMesh.SamplePosition(destination, out var hit, 2.0f, NavMesh.AllAreas))
            {
                destination = hit.position;
            }

            _cameraManAgent.isStopped = false;
            _cameraManAgent.stoppingDistance = stoppingDistance; // set requested stopping distance (0 for B-Roll)
            _cameraManAgent.SetDestination(destination);
        }
        else
        {
            // Fallback: direct set if no agent
            _cameraManTransform.position = destination;
        }
    }

    // Utilities to get/set FollowPlayerCamera target via reflection (field is private)
    private static Transform GetFollowCameraTarget(FollowPlayerCamera cam)
    {
        var f = typeof(FollowPlayerCamera).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic);
        return f != null ? (Transform)f.GetValue(cam) : null;
    }
    private static void SetFollowCameraTarget(FollowPlayerCamera cam, Transform t)
    {
        var f = typeof(FollowPlayerCamera).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic);
        if (f != null) f.SetValue(cam, t);
    }

    private static bool IsReaderLockedOn(InputReader reader)
    {
        var field = typeof(InputReader).GetField("_isLockedOnLocal", BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null && field.FieldType == typeof(bool) && (bool)field.GetValue(reader);
    }

    private static void ResetInputReaderInversionState(InputReader reader)
    {
        if (reader == null) return;
        var field = typeof(InputReader).GetField("_isLockedOnLocal", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(bool))
        {
            field.SetValue(reader, false);
        }
    }
}