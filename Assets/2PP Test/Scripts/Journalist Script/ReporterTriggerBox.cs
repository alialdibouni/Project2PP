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

    [Header("B-Roll Zoom (FOV)")]
    [SerializeField] private float _bRollMinFov = 20f;
    [SerializeField] private float _bRollMaxFov = 60f;
    [SerializeField] private float _bRollZoomStep = 2.0f; // FOV change per scroll notch (~120 units)

    [Header("UI Prompt")]
    [TextArea]
    [SerializeField] private string _enterPromptMessage = "Right Click: Enter Reporter Mode\n";
    [SerializeField] private string _reporterPromptMessage = "Right Click: Exit Reporter Mode\nB - B Roll\n";

    [Header("On Report (Disable once when Reporter Mode is activated)")]
    [SerializeField] private GameObject _reportArtifact;     // Assign the GameObject to disable/hide
    [SerializeField] private bool _destroyArtifactInstead = false; // If true, Destroy instead of SetActive(false)
    [SerializeField] private string _reportSaveKey = "";     // Optional: set a unique key to persist across sessions (PlayerPrefs)

    private Controls _controls;
    private InputReader _playerInputReader;
    private bool _playerInside;
    private bool _pendingUnlockOnReenable;

    // Reference to player's UI to show prompt
    private PlayerUI _playerUI;

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

    // Camera zoom cache
    private Camera _cameraManCamera;
    private float _cameraManOriginalFov;
    private bool _cameraManFovCached;

    // Report state
    private bool _hasReported;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        _controls = new Controls();

        // If persistence enabled, hide artifact at startup if already reported
        if (!string.IsNullOrEmpty(_reportSaveKey) && PlayerPrefs.GetInt(_reportSaveKey, 0) == 1)
        {
            _hasReported = true;
            if (_reportArtifact != null)
            {
                if (_destroyArtifactInstead)
                {
                    Destroy(_reportArtifact);
                }
                else
                {
                    _reportArtifact.SetActive(false);
                }
            }
        }
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
        // Cache player components
        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null) return;

        _playerInputReader = reader;
        _playerUI = other.GetComponentInParent<PlayerUI>();
        _playerInside = true;

        // Show prompt immediately on enter (will switch to reporter prompt when locked-on)
        if (_playerUI != null)
        {
            _playerUI.UpdateText(_enterPromptMessage);
        }

        _controls.Player.Enable();
    }

    private void OnTriggerExit(Collider other)
    {
        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null || reader != _playerInputReader) return;

        if (_bRollActive) RevertBRoll();

        _playerInputReader.SuppressLockOnToggle = false;
        ResetInputReaderInversionState(_playerInputReader);

        // Clear prompt on exit
        if (_playerUI != null)
        {
            _playerUI.UpdateText(string.Empty);
        }

        _playerInside = false;
        _playerInputReader = null;
        _playerUI = null;
        _pendingUnlockOnReenable = false;

        _controls.Player.Disable();
    }

    private void Update()
    {
        if (!_playerInside || _playerInputReader == null) return;

        // Keep the prompt visible while inside; switch text based on Reporter Mode state
        if (_playerUI != null)
        {
            bool inReporterMode = IsReaderLockedOn(_playerInputReader);
            _playerUI.UpdateText(inReporterMode ? _reporterPromptMessage : _enterPromptMessage);
        }

        // If reporter mode turned off while B-Roll active, revert immediately
        if (_bRollActive && !IsReaderLockedOn(_playerInputReader))
        {
            RevertBRoll();
        }

        // Toggle B-Roll with B while locked-on inside the trigger
        if (IsReaderLockedOn(_playerInputReader)
            && Keyboard.current != null
            && Keyboard.current.bKey.wasPressedThisFrame)
        {
            ToggleBRoll();
        }

        // Handle zoom only while B-Roll is active
        if (_bRollActive && Mouse.current != null && _cameraManCamera != null)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y; // +/-120 per notch typically
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                // Negative scrollY should zoom in (smaller FOV), positive zoom out
                float delta = -scrollY * (_bRollZoomStep / 120f);
                float fov = Mathf.Clamp(_cameraManCamera.fieldOfView + delta, _bRollMinFov, _bRollMaxFov);
                _cameraManCamera.fieldOfView = fov;
            }
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

            // Mark reported once and hide/destroy the artifact
            MarkReported();
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

    private void MarkReported()
    {
        if (_hasReported) return;

        _hasReported = true;

        if (_reportArtifact != null)
        {
            if (_destroyArtifactInstead)
            {
                Destroy(_reportArtifact);
            }
            else
            {
                _reportArtifact.SetActive(false);
            }
        }

        if (!string.IsNullOrEmpty(_reportSaveKey))
        {
            PlayerPrefs.SetInt(_reportSaveKey, 1);
            PlayerPrefs.Save();
        }
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
            _cameraManCamera = _cameraManTransform.GetComponentInChildren<Camera>(true);
        }
        else
        {
            if (_cameraManAgent == null) _cameraManAgent = _cameraManTransform.GetComponent<NavMeshAgent>();
            if (_cameraManFollower == null) _cameraManFollower = _cameraManTransform.GetComponent<FollowPlayer>();
            if (_cameraManCamera == null) _cameraManCamera = _cameraManTransform.GetComponentInChildren<Camera>(true);
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

        // Cache original FOV once per session
        if (_cameraManCamera != null && !_cameraManFovCached)
        {
            _cameraManOriginalFov = _cameraManCamera.fieldOfView;
            _cameraManFovCached = true;
        }

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

        // Restore original FOV
        if (_cameraManCamera != null && _cameraManFovCached)
        {
            _cameraManCamera.fieldOfView = _cameraManOriginalFov;
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