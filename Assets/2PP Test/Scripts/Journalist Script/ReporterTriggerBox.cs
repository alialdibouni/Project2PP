using UnityEngine;
using UnityEngine.InputSystem;
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

        // Revert any active B-Roll
        if (_bRollActive) RevertBRoll();

        // Clear suppression + inversion
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
    }

    private void OnLockOnPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (!_playerInside || _playerInputReader == null) return;

        // Take control of lock-on this frame to avoid double toggles
        _playerInputReader.SuppressLockOnToggle = true;

        if (_playerInputReader.enabled)
        {
            // ENTER REPORTER MODE:
            // Ensure lock-on is ON, then disable input to freeze movement
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
            // EXIT REPORTER MODE:
            _playerInputReader.enabled = true;

            // Ensure lock-on is OFF
            if (IsReaderLockedOn(_playerInputReader))
            {
                _playerInputReader.onLockOnToggled?.Invoke();
                _playerInputReader.onSprintDeactivated?.Invoke();
            }

            // Revert B-Roll and stop inversion
            if (_bRollActive) RevertBRoll();
            ResetInputReaderInversionState(_playerInputReader);

            _pendingUnlockOnReenable = false;
        }

        // Release suppression after we handled this click
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

        SetFollowCameraTarget(_followPlayerCamera, _bRollLookAt);
        _cameraManTransform.position = _bRollCameraPosition.position;

        _bRollActive = true;
    }

    private void RevertBRoll()
    {
        if (_followPlayerCamera != null && _originalLookAtTarget != null)
        {
            SetFollowCameraTarget(_followPlayerCamera, _originalLookAtTarget);
        }
        if (_cameraManTransform != null)
        {
            _cameraManTransform.position = _cameraManOriginalPos;
        }
        _bRollActive = false;
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
