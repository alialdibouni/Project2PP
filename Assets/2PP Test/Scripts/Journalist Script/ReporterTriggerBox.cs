using UnityEngine;
using Synty.AnimationBaseLocomotion.Samples.InputSystem; // InputReader + generated Controls
using System.Reflection;

[RequireComponent(typeof(Collider))]
public class ReporterTriggerBox : MonoBehaviour
{
    private Controls _controls;                // Our own input map (only LockOn is used)
    private InputReader _playerInputReader;    // Target to toggle
    private bool _playerInside;
    private bool _pendingUnlockOnReenable;     // After we disable InputReader, unlock on next enable

    private void Awake()
    {
        // Ensure trigger collider
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        _controls = new Controls();
    }

    private void OnEnable()
    {
        // Hook LockOn once; we'll Enable/Disable the map as the player enters/exits
        _controls.Player.LockOn.performed += OnLockOnPerformed;
    }

    private void OnDisable()
    {
        _controls.Player.LockOn.performed -= OnLockOnPerformed;
        _controls.Player.Disable();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Find the player's InputReader on this object or its parents
        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null) return;

        _playerInputReader = reader;
        _playerInside = true;

        // Start listening for right-click while inside
        _controls.Player.Enable();
    }

    private void OnTriggerExit(Collider other)
    {
        // Only react if the exiting object is the same player we tracked
        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null || reader != _playerInputReader) return;

        // Clear inversion state on exit
        ResetInputReaderInversionState(_playerInputReader);

        _playerInside = false;
        _playerInputReader = null;
        _pendingUnlockOnReenable = false;

        // Stop listening outside the trigger
        _controls.Player.Disable();
    }

    private void OnLockOnPerformed(UnityEngine.InputSystem.InputAction.CallbackContext ctx)
    {
        if (!_playerInside || _playerInputReader == null) return;

        if (_playerInputReader.enabled)
        {
            // First click inside trigger: disable InputReader, remember to unlock next time
            _playerInputReader.enabled = false;
            _pendingUnlockOnReenable = true;
        }
        else
        {
            // Second click: re-enable InputReader and disable lock-on
            _playerInputReader.enabled = true;

            if (_pendingUnlockOnReenable)
            {
                // Use the same toggle event the controller listens to (turns off lock-on gameplay)
                _playerInputReader.onLockOnToggled?.Invoke();
                _playerInputReader.onSprintDeactivated?.Invoke();

                // Also clear input inversion state in InputReader so A/D are no longer flipped
                ResetInputReaderInversionState(_playerInputReader);

                _pendingUnlockOnReenable = false;
            }
        }
    }

    // Reset InputReader's private _isLockedOnLocal flag (so horizontal inversion stops)
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
