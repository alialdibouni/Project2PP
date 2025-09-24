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
    [SerializeField] private Transform _initialCameraPosition;

    [Header("Recording Requirements (seconds)")]
    [SerializeField] private float _requiredReporterSeconds = 10f;
    [SerializeField] private float _requiredBRollSeconds = 5f;

    [Header("B-Roll Zoom (FOV)")]
    [SerializeField] private float _bRollMinFov = 20f;
    [SerializeField] private float _bRollMaxFov = 60f;
    [SerializeField] private float _bRollZoomStep = 2.0f; // FOV change per scroll notch (~120 units)

    [Header("UI Prompt")]
    [TextArea][SerializeField] private string _enterPromptMessage = "Right Click: Enter Reporter Mode\n";
    [TextArea][SerializeField] private string _reporterPromptMessage = "Right Click: Exit Reporter Mode\nB - B Roll\n";

    [Header("On Report (Disable when both recordings complete)")]
    [SerializeField] private GameObject _reportArtifact;     // Assign the GameObject to hide/show
    [SerializeField] private bool _destroyArtifactInstead = false; // Destroys when complete (can't be shown again)
    [SerializeField] private string _reportSaveKey = "";     // Base key for persistence

    [Header("Reporter Audio")]
    [SerializeField] private AudioSource _reportAudioSource; // Optional; will be auto-created if null
    [SerializeField] private AudioClip _reportClip;          // Your MP3 clip
    [SerializeField] private float _reportVolume = 1f;
    [SerializeField] private float _fadeInDuration = 0.6f;
    [SerializeField] private float _fadeOutDuration = 0.6f;

    private Controls _controls;
    private InputReader _playerInputReader;
    private bool _playerInside;
    private bool _pendingUnlockOnReenable;

    // Player UI
    private PlayerUI _playerUI;

    // Player Animator
    private Animator _playerAnimator;
    private int _isReportingHash;

    // B-Roll runtime state
    private bool _bRollActive;
    private FollowPlayerCamera _followPlayerCamera;
    private Transform _originalLookAtTarget;
    private Transform _cameraManTransform;
    private NavMeshAgent _cameraManAgent;

    // If CameraMan has a follower script, disable it during Reporter Mode/B-Roll moves
    private FollowPlayer _cameraManFollower;
    private bool _cameraManFollowerWasEnabled;

    // Agent tuning persistence
    private float _cameraManOriginalStoppingDistance = -1f;

    // Camera zoom cache
    private Camera _cameraManCamera;
    private float _cameraManOriginalFov;
    private bool _cameraManFovCached;

    // Progress
    private float _accumReporterSeconds;
    private float _accumBRollSeconds;

    // Completion state
    private bool _bothComplete;

    // Move coroutine
    private Coroutine _returnRoutine;

    // Audio fade coroutine
    private Coroutine _audioFadeRoutine;

    private string RepSecondsKey => string.IsNullOrEmpty(_reportSaveKey) ? null : _reportSaveKey + "_RepSec";
    private string BRollSecondsKey => string.IsNullOrEmpty(_reportSaveKey) ? null : _reportSaveKey + "_BRollSec";

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        _controls = new Controls();
        _isReportingHash = Animator.StringToHash("isReporting");

        // Load persisted progress
        LoadProgress();

        // Apply artifact visibility based on completion
        EvaluateReportArtifactVisibility();

        EnsureAudioSource();
    }

    private void OnEnable()
    {
        _controls.Player.LockOn.performed += OnLockOnPerformed;
    }

    private void OnDisable()
    {
        _controls.Player.LockOn.performed -= OnLockOnPerformed;
        _controls.Player.Disable();

        SaveProgress();
    }

    private void OnTriggerEnter(Collider other)
    {
        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null) return;

        _playerInputReader = reader;
        _playerUI = other.GetComponentInParent<PlayerUI>();
        _playerAnimator = other.GetComponentInParent<Animator>(); // cache player's Animator
        _playerInside = true;

        _playerUI?.UpdateText(_enterPromptMessage);
        _controls.Player.Enable();

        // Ensure artifact respects current progress state right away on enter
        EvaluateReportArtifactVisibility();
    }

    private void OnTriggerExit(Collider other)
    {
        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null || reader != _playerInputReader) return;

        if (_bRollActive) RevertBRoll();

        // Safety: restore FOV on exit
        RestoreBRollFovToDefault();

        // Always stop reporter audio when leaving the trigger
        StopReporterAudio();

        // Ensure animator flag is reset on exit
        SetAnimatorReporting(false);

        _playerInputReader.SuppressLockOnToggle = false;
        ResetInputReaderInversionState(_playerInputReader);

        // Ensure follower enabled if disabled
        if (EnsureCameraManRefs() && _cameraManFollower != null && !_cameraManFollower.enabled)
        {
            if (_cameraManAgent != null && _cameraManAgent.isOnNavMesh) _cameraManAgent.ResetPath();
            if (_cameraManOriginalStoppingDistance >= 0f) _cameraManAgent.stoppingDistance = _cameraManOriginalStoppingDistance;
            _cameraManFollower.enabled = true;
        }

        // Update artifact based on current progress (show if incomplete, hide/destroy if complete)
        EvaluateReportArtifactVisibility();
        SaveProgress();

        // Clear prompt
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

        bool inReporterMode = IsReaderLockedOn(_playerInputReader);

        // Accumulate recording time while in Reporter Mode
        if (inReporterMode && !_bothComplete)
        {
            if (_bRollActive)
            {
                _accumBRollSeconds = Mathf.Min(_requiredBRollSeconds, _accumBRollSeconds + Time.deltaTime);
            }
            else
            {
                _accumReporterSeconds = Mathf.Min(_requiredReporterSeconds, _accumReporterSeconds + Time.deltaTime);
            }

            // If newly completed, update artifact and persist
            bool nowComplete = IsReporterComplete() && IsBRollComplete();
            if (nowComplete != _bothComplete)
            {
                _bothComplete = nowComplete;
                EvaluateReportArtifactVisibility();
                SaveProgress();
            }
        }

        // Prompt + counters
        if (_playerUI != null)
        {
            if (inReporterMode)
            {
                float repLeft = Mathf.Max(0f, _requiredReporterSeconds - _accumReporterSeconds);
                float brLeft = Mathf.Max(0f, _requiredBRollSeconds - _accumBRollSeconds);
                _playerUI.UpdateText($"{_reporterPromptMessage}Footage left: {repLeft:0}s\nB-Roll left: {brLeft:0}s");
            }
            else
            {
                _playerUI.UpdateText(_enterPromptMessage);
            }
        }

        // If reporter mode turned off while B-Roll active, revert immediately
        if (_bRollActive && !inReporterMode)
        {
            RevertBRoll();
        }

        // Toggle B-Roll with B while locked-on inside the trigger
        if (inReporterMode && Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            ToggleBRoll();
        }

        // Handle zoom only while B-Roll is active
        if (_bRollActive && Mouse.current != null && _cameraManCamera != null)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y; // +/-120 per notch typically
            if (Mathf.Abs(scrollY) > 0.01f)
            {
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
            if (EnsureCameraManRefs())
            {
                if (_cameraManOriginalStoppingDistance < 0f && _cameraManAgent != null)
                    _cameraManOriginalStoppingDistance = _cameraManAgent.stoppingDistance;

                // Cache original FOV on entering Reporter Mode
                if (_cameraManCamera != null)
                {
                    _cameraManOriginalFov = _cameraManCamera.fieldOfView;
                    _cameraManFovCached = true;
                }

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
            else
            {
                Debug.LogWarning("[ReporterTriggerBox] CameraMan not found; cannot move to InitialCameraPosition.");
            }

            // Animator: flag reporting ON
            SetAnimatorReporting(true);

            // Hide artifact while actively recording (regardless of completion state)
            UpdateArtifactVisibility(inRecording: true);

            // Start reporter audio (fade in)
            StartReporterAudio();

            SaveProgress();
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

            // Re-enable follower on exit
            if (EnsureCameraManRefs() && _cameraManFollower != null)
            {
                if (_cameraManAgent != null && _cameraManAgent.isOnNavMesh) _cameraManAgent.ResetPath();
                if (_cameraManOriginalStoppingDistance >= 0f) _cameraManAgent.stoppingDistance = _cameraManOriginalStoppingDistance;
                _cameraManFollower.enabled = true;
            }

            // Stop reporter audio (fade out)
            StopReporterAudio();

            // Animator: flag reporting OFF
            SetAnimatorReporting(false);

            ResetInputReaderInversionState(_playerInputReader);
            _pendingUnlockOnReenable = false;

            // After a recording session ends, update artifact based on progress
            EvaluateReportArtifactVisibility();
            SaveProgress();
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

        if (_cameraManFollower != null)
        {
            _cameraManFollowerWasEnabled = _cameraManFollower.enabled;
            _cameraManFollower.enabled = false;
        }

        SetFollowCameraTarget(_followPlayerCamera, _bRollLookAt);

        // Move CameraMan via NavMesh with stoppingDistance = 0 to reach exact B-Roll position
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
            if (_returnRoutine != null)
            {
                StopCoroutine(_returnRoutine);
                _returnRoutine = null;
            }

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
            if (_cameraManTransform != null && _initialCameraPosition != null)
            {
                _cameraManTransform.position = _initialCameraPosition.position;
            }

            if (_cameraManFollower != null)
            {
                _cameraManFollower.enabled = true;
            }
        }

        _bRollActive = false;
        // On returning from B-Roll, artifact is governed by overall progress (not shown if complete)
        EvaluateReportArtifactVisibility();
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
            _cameraManFollower.enabled = true;
        }

        _returnRoutine = null;

        // After returning to initial, artifact visibility still depends only on progress
        EvaluateReportArtifactVisibility();
    }

    private void MoveCameraManTo(Vector3 destination, float stoppingDistance)
    {
        if (_cameraManTransform == null) return;

        if (_cameraManAgent != null && _cameraManAgent.isOnNavMesh)
        {
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

    // Audio helpers
    private void EnsureAudioSource()
    {
        if (_reportAudioSource == null)
        {
            _reportAudioSource = gameObject.GetComponent<AudioSource>();
            if (_reportAudioSource == null) _reportAudioSource = gameObject.AddComponent<AudioSource>();
        }

        _reportAudioSource.playOnAwake = false;
        _reportAudioSource.loop = true;
        _reportAudioSource.spatialBlend = 0f; // 2D by default; set to 1 for 3D if preferred
        _reportAudioSource.volume = 0f;

        if (_reportClip != null) _reportAudioSource.clip = _reportClip;
    }

    private void StartReporterAudio()
    {
        if (_reportClip == null) return;
        EnsureAudioSource();

        if (_audioFadeRoutine != null)
        {
            StopCoroutine(_audioFadeRoutine);
            _audioFadeRoutine = null;
        }

        if (_reportAudioSource.clip != _reportClip) _reportAudioSource.clip = _reportClip;
        if (!_reportAudioSource.isPlaying)
        {
            _reportAudioSource.volume = 0f;
            _reportAudioSource.Play();
        }

        _audioFadeRoutine = StartCoroutine(FadeAudio(_reportAudioSource, _reportAudioSource.volume, Mathf.Clamp01(_reportVolume), _fadeInDuration));
    }

    private void StopReporterAudio()
    {
        if (_reportAudioSource == null || !_reportAudioSource.isPlaying)
        {
            return;
        }

        if (_audioFadeRoutine != null)
        {
            StopCoroutine(_audioFadeRoutine);
            _audioFadeRoutine = null;
        }

        _audioFadeRoutine = StartCoroutine(FadeOutAndStop(_reportAudioSource, _fadeOutDuration));
    }

    private IEnumerator FadeAudio(AudioSource src, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration > 0f ? t / duration : 1f;
            src.volume = Mathf.Lerp(from, to, k);
            yield return null;
        }
        src.volume = to;
        _audioFadeRoutine = null;
    }

    private IEnumerator FadeOutAndStop(AudioSource src, float duration)
    {
        float start = src.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration > 0f ? t / duration : 1f;
            src.volume = Mathf.Lerp(start, 0f, k);
            yield return null;
        }
        src.volume = 0f;
        src.Stop();
        _audioFadeRoutine = null;
    }

    // Progress helpers/persistence/artifact visibility
    private bool IsReporterComplete() => _accumReporterSeconds >= Mathf.Max(0f, _requiredReporterSeconds - 0.0001f);
    private bool IsBRollComplete() => _accumBRollSeconds >= Mathf.Max(0f, _requiredBRollSeconds - 0.0001f);

    private void EvaluateReportArtifactVisibility()
    {
        _bothComplete = IsReporterComplete() && IsBRollComplete();
        // Not in active recording when evaluating here
        UpdateArtifactVisibility(inRecording: false);

        // Persist completion bit if key provided
        if (!string.IsNullOrEmpty(_reportSaveKey))
        {
            PlayerPrefs.SetInt(_reportSaveKey, _bothComplete ? 1 : 0);
        }
    }

    private void UpdateArtifactVisibility(bool inRecording)
    {
        if (_reportArtifact == null) return;

        // If fully complete, hide permanently (or destroy if chosen)
        if (_bothComplete)
        {
            if (_destroyArtifactInstead)
            {
                if (_reportArtifact != null) Destroy(_reportArtifact);
            }
            else
            {
                _reportArtifact.SetActive(false);
            }
            return;
        }

        // Otherwise, hide while actively recording; show when not recording
        bool shouldShow = !inRecording && !_destroyArtifactInstead;
        _reportArtifact.SetActive(shouldShow);
    }

    private void LoadProgress()
    {
        if (!string.IsNullOrEmpty(_reportSaveKey))
        {
            _accumReporterSeconds = PlayerPrefs.GetFloat(RepSecondsKey ?? string.Empty, 0f);
            _accumBRollSeconds = PlayerPrefs.GetFloat(BRollSecondsKey ?? string.Empty, 0f);
            _bothComplete = PlayerPrefs.GetInt(_reportSaveKey, 0) == 1;

            // Clamp to current requirements in case they changed
            _accumReporterSeconds = Mathf.Clamp(_accumReporterSeconds, 0f, _requiredReporterSeconds);
            _accumBRollSeconds = Mathf.Clamp(_accumBRollSeconds, 0f, _requiredBRollSeconds);
        }
    }

    private void SaveProgress()
    {
        if (string.IsNullOrEmpty(_reportSaveKey)) return;

        PlayerPrefs.SetFloat(RepSecondsKey, _accumReporterSeconds);
        PlayerPrefs.SetFloat(BRollSecondsKey, _accumBRollSeconds);
        PlayerPrefs.SetInt(_reportSaveKey, (_bothComplete ? 1 : 0));
        PlayerPrefs.Save();
    }

    // Reflection helpers
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

    private bool IsInReporterMode() => _playerInputReader != null && !_playerInputReader.enabled;

    private void SetAnimatorReporting(bool value)
    {
        if (_playerAnimator == null) return;
        _playerAnimator.SetBool(_isReportingHash, value);
    }
}