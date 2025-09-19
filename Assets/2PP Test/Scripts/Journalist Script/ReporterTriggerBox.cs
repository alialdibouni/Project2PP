using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using Synty.AnimationBaseLocomotion.Samples.InputSystem; // InputReader + generated Controls
using System.Reflection;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class ReporterTriggerBox : MonoBehaviour
{
    [Header("B-Roll Targets")]
    [SerializeField] private Transform _bRollLookAt;         // "BRollLookAt"
    [SerializeField] private Transform _bRollCameraPosition; // "BRollCameraPosition"
    [SerializeField] private Transform _initialCameraPosition; // NEW: where CameraMan goes on Reporter Mode enter and after B-Roll

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
    private NavMeshAgent _cameraManAgent;

    // If CameraMan has a follower script, disable it during Reporter Mode/B-Roll moves
    private FollowPlayer _cameraManFollower;
    private bool _cameraManFollowerWasEnabled;

    // Cache original stopping distance to restore after moves
    private float _cameraManOriginalStoppingDistance = -1f;

    // Camera zoom cache
    private Camera _cameraManCamera;
    private float _cameraManOriginalFov;
    private bool _cameraManFovCached;

    // Report state
    private bool _hasReported;

    // Move coroutine
    private Coroutine _returnRoutine;

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
                if (_destroyArtifactInstead) Destroy(_reportArtifact);
                else _reportArtifact.SetActive(false);
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
        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null) return;

        _playerInputReader = reader;
        _playerUI = other.GetComponentInParent<PlayerUI>();
        _playerInside = true;

        // Show prompt immediately on enter (will switch to reporter prompt when locked-on)
        _playerUI?.UpdateText(_enterPromptMessage);

        _controls.Player.Enable();
    }

    private void OnTriggerExit(Collider other)
    {
        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null || reader != _playerInputReader) return;

        if (_bRollActive) RevertBRoll();

        // Safety: restore FOV on exit
        RestoreBRollFovToDefault();

        _playerInputReader.SuppressLockOnToggle = false;
        ResetInputReaderInversionState(_playerInputReader);

        // Ensure the follower is re-enabled if we leave while it was disabled
        if (EnsureCameraManRefs() && _cameraManFollower != null && !_cameraManFollower.enabled)
        {
            if (_cameraManAgent != null && _cameraManAgent.isOnNavMesh) _cameraManAgent.ResetPath();
            if (_cameraManOriginalStoppingDistance >= 0f) _cameraManAgent.stoppingDistance = _cameraManOriginalStoppingDistance;
            _cameraManFollower.enabled = true; // force re-enable
        }

        // Clear prompt on exit
        _playerUI?.UpdateText(string.Empty);

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

            // Move CameraMan to InitialCameraPosition
            if (!EnsureCameraManRefs())
            {
                Debug.LogWarning("[ReporterTriggerBox] CameraMan not found; cannot move to InitialCameraPosition.");
            }
            else
            {
                if (_cameraManOriginalStoppingDistance < 0f && _cameraManAgent != null)
                    _cameraManOriginalStoppingDistance = _cameraManAgent.stoppingDistance;

                // Cache original FOV on entering Reporter Mode
                if (_cameraManCamera != null)
                {
                    _cameraManOriginalFov = _cameraManCamera.fieldOfView;
                    _cameraManFovCached = true;
                }

                // Disable follower while we position the cameraman
                if (_cameraManFollower != null)
                {
                    _cameraManFollowerWasEnabled = _cameraManFollower.enabled;
                    _cameraManFollower.enabled = false;
                }

                if (_initialCameraPosition != null)
                {
                    MoveCameraManTo(_initialCameraPosition.position, 0f);
                }
                else
                {
                    Debug.LogWarning("[ReporterTriggerBox] _initialCameraPosition is not assigned.");
                }
            }

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
            else RestoreBRollFovToDefault();

            // Re-enable follower on exit (force enable)
            if (EnsureCameraManRefs() && _cameraManFollower != null)
            {
                if (_cameraManAgent != null && _cameraManAgent.isOnNavMesh) _cameraManAgent.ResetPath();
                if (_cameraManOriginalStoppingDistance >= 0f) _cameraManAgent.stoppingDistance = _cameraManOriginalStoppingDistance;
                _cameraManFollower.enabled = true;
            }

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
            if (_destroyArtifactInstead) Destroy(_reportArtifact);
            else _reportArtifact.SetActive(false);
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

        if (!EnsureCameraManRefs()) return;

        if (_cameraManOriginalStoppingDistance < 0f && _cameraManAgent != null)
            _cameraManOriginalStoppingDistance = _cameraManAgent.stoppingDistance;

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
        RestoreBRollFovToDefault();

        if (_cameraManAgent != null && _cameraManAgent.isOnNavMesh && _initialCameraPosition != null)
        {
            // Stop any previous routine
            if (_returnRoutine != null)
            {
                StopCoroutine(_returnRoutine);
                _returnRoutine = null;
            }

            // Keep follower disabled until we are back to initial position
            if (_cameraManFollower != null)
            {
                _returnRoutine = StartCoroutine(ReturnCameraManToInitialThenRestoreFollower());
            }
            else
            {
                float stop = _cameraManOriginalStoppingDistance >= 0f ? _cameraManOriginalStoppingDistance : 0f;
                MoveCameraManTo(_initialCameraPosition.position, stop);
            }
        }
        else
        {
            // Fallback if no agent or no initial position
            if (_cameraManTransform != null && _initialCameraPosition != null)
            {
                _cameraManTransform.position = _initialCameraPosition.position;
            }

            if (_cameraManFollower != null)
            {
                _cameraManFollower.enabled = true; // force re-enable
            }
        }

        _bRollActive = false;
    }

    private void RestoreBRollFovToDefault()
    {
        if (_cameraManCamera == null && _cameraManTransform != null)
        {
            _cameraManCamera = _cameraManTransform.GetComponentInChildren<Camera>(true);
        }

        if (_cameraManCamera != null)
        {
            if (_cameraManFovCached)
            {
                _cameraManCamera.fieldOfView = _cameraManOriginalFov;
            }
            else
            {
                _cameraManCamera.fieldOfView = Mathf.Clamp(_cameraManCamera.fieldOfView, _bRollMinFov, _bRollMaxFov);
            }
        }

        _cameraManFovCached = false;
    }

    private IEnumerator ReturnCameraManToInitialThenRestoreFollower()
    {
        MoveCameraManTo(_initialCameraPosition.position, 0.05f);

        while (_cameraManAgent != null && _cameraManAgent.isOnNavMesh)
        {
            if (!_cameraManAgent.pathPending)
            {
                if (_cameraManAgent.remainingDistance <= Mathf.Max(_cameraManAgent.stoppingDistance, 0.05f))
                {
                    break;
                }
            }
            yield return null;
        }

        if (_cameraManOriginalStoppingDistance >= 0f)
        {
            _cameraManAgent.stoppingDistance = _cameraManOriginalStoppingDistance;
        }

        _cameraManAgent.ResetPath();

        if (_cameraManFollower != null)
        {
            _cameraManFollower.enabled = true; // force re-enable
        }

        _returnRoutine = null;
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
            _cameraManAgent.stoppingDistance = stoppingDistance;
            _cameraManAgent.SetDestination(destination);
        }
        else
        {
            // Fallback: direct set if no agent
            _cameraManTransform.position = destination;
        }
    }

    private bool EnsureCameraManRefs()
    {
        if (_cameraManTransform == null)
        {
            var camManGO = GameObject.FindGameObjectWithTag("CameraMan");
            if (camManGO == null) return false;
            _cameraManTransform = camManGO.transform;
        }
        if (_cameraManAgent == null) _cameraManAgent = _cameraManTransform.GetComponent<NavMeshAgent>();
        if (_cameraManFollower == null) _cameraManFollower = _cameraManTransform.GetComponent<FollowPlayer>();
        if (_cameraManCamera == null) _cameraManCamera = _cameraManTransform.GetComponentInChildren<Camera>(true);
        return true;
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