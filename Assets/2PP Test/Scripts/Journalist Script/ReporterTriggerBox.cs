using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

[RequireComponent(typeof(Collider))
]
public class ReporterTriggerBox : MonoBehaviour
{
    [Header("B-Roll Targets")]
    [SerializeField] private Transform _bRollLookAt;
    [SerializeField] private Transform _bRollCameraPosition;
    [SerializeField] private Transform _initialCameraPosition;

    [Header("Recording Requirements (seconds)")]
    [SerializeField] private float _requiredReporterSeconds = 10f;
    [SerializeField] private float _requiredBRollSeconds = 5f;

    [Header("B-Roll Zoom (FOV)")]
    [SerializeField] private float _bRollMinFov = 20f;
    [SerializeField] private float _bRollMaxFov = 60f;
    [SerializeField] private float _bRollZoomStep = 2.0f;

    [Header("B-Roll Recording Gates")]
    [Range(0f, 1f)] [SerializeField] private float _bRollZoomRequirement = 0.7f;

    [Header("UI Prompt")]
    [TextArea] [SerializeField] private string _enterPromptMessage = "Right Click: Enter Reporter Mode\n";
    [TextArea] [SerializeField] private string _reporterPromptMessage = "Right Click: Exit Reporter Mode\nB - B Roll\n";

    [Header("On Report (Disable when both recordings complete)")]
    [SerializeField] private GameObject _reportArtifact;
    [SerializeField] private bool _destroyArtifactInstead = false;
    [SerializeField] private string _reportSaveKey = "";

    [Header("Reporter Audio")]
    [SerializeField] private AudioSource _reportAudioSource;
    [SerializeField] private AudioClip _reportClip;
    [SerializeField] private float _reportVolume = 1f;
    [SerializeField] private float _fadeInDuration = 0.6f;
    [SerializeField] private float _fadeOutDuration = 0.6f;

    [Header("Reporter Entry VO")]
    [Tooltip("Random one-shot played once when entering Reporter Mode.")]
    [SerializeField] private List<AudioClip> _reporterEntryClips = new List<AudioClip>();
    [Tooltip("Optional AudioSource used for entry one-shots. Falls back to _reportAudioSource if null.")]
    [SerializeField] private AudioSource _entryAudioSource;
    [Range(0f, 1f)] [SerializeField] private float _entryVolume = 1f;
    [Tooltip("If true, skip entry VO if the exact same clip is already playing on the same source.")]
    [SerializeField] private bool _entryAvoidOverlap = true;

    [Header("Reporter Entry VO (Global Fallback)")]
    [SerializeField] private bool _useGlobalEntryClips = false;
    [SerializeField] private List<AudioClip> _globalEntryClips = new List<AudioClip>();

    [Header("Reporter Start Delay")]
    [Tooltip("Extra delay AFTER A_A_Report animation begins, before audio/timers start.")]
    [SerializeField] private float _reporterTimeDelay = 0.0f;

    // ADD: chase gating
    [Header("Chase Gating")]
    [SerializeField] private bool _blockWhileChased = true;
    [TextArea][SerializeField] private string _chasedPromptMessage = "You're currently being chased! Lose them before reporting.";
    private bool _currentlyChased;  

    private Controls _controls;
    private InputReader _playerInputReader;
    private bool _playerInside;
    private bool _pendingUnlockOnReenable;

    private PlayerUI _playerUI;
    private Animator _playerAnimator;
    private int _isReportingHash = Animator.StringToHash("isReporting");

    [Header("Animator Sync")]
    [SerializeField] private string _reportStateName = "A_A_Report";
    [SerializeField] private int _reportLayerIndex = 0;
    private int _reportStateHash;

    // B-Roll runtime state
    private bool _bRollActive;
    private FollowPlayerCamera _followPlayerCamera;
    private Transform _originalLookAtTarget;
    private Transform _cameraManTransform;
    private NavMeshAgent _cameraManAgent;

    private FollowPlayer _cameraManFollower;
    private bool _cameraManFollowerWasEnabled;
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

    [SerializeField] private bool _completedAndDisabled;

    // Coroutines
    private Coroutine _returnRoutine;
    private Coroutine _audioFadeRoutine;

    // Animator-driven session flags
    private bool _reportAnimDetected;
    private float _reporterDelayRemaining;
    private bool _reporterAudioStarted;

    // Entry VO guard (per Reporter Mode session)
    private bool _entryClipPlayedThisSession;

    // NEW: shuffle-bag state for entry VO (per ReporterTriggerBox instance)
    private List<int> _entryShuffleBag;
    private int _entryShuffleIndex;

    // Global anti-repeat across all ReporterTriggerBoxes
    private static int _lastEntryIndex = -1;

    private string RepSecondsKey => string.IsNullOrEmpty(_reportSaveKey) ? null : _reportSaveKey + "_RepSec";
    private string BRollSecondsKey => string.IsNullOrEmpty(_reportSaveKey) ? null : _reportSaveKey + "_BRollSec";

    // -------- Static Progress Registry --------
    private static readonly List<ReporterTriggerBox> _all = new List<ReporterTriggerBox>();
    public static event System.Action ReportProgressChanged;
    public static int TotalReports => _all.Count;
    public static int CompletedReports => _all.Count(t => t._completedAndDisabled);
    private static void RaiseProgressChanged() => ReportProgressChanged?.Invoke();
    // ------------------------------------------

    private void Awake()
    {
        // Register first so even if we disable this frame it's counted.
        if (!_all.Contains(this)) _all.Add(this);

        var col = GetComponent<Collider>();
        col.isTrigger = true;
        _controls = new Controls();

        _reportStateHash = Animator.StringToHash(_reportStateName);

        LoadProgress();
        EvaluateReportArtifactVisibility();
        EnsureAudioSource();

        // If already fully complete from persistence, disable immediately (but still remain in list).
        if (_bothComplete && !_completedAndDisabled)
        {
            _completedAndDisabled = true;
            gameObject.SetActive(false);
        }

        // Initialize entry VO shuffle bag
        RebuildEntryShuffleBag();

        RaiseProgressChanged();
    }

    private void OnDestroy()
    {
        _all.Remove(this);
        RaiseProgressChanged();
    }

    private void OnEnable()
    {
        _controls.Player.LockOn.performed += OnLockOnPerformed;

        Enemy.GlobalChaseChanged += OnGlobalChaseChanged;
        _currentlyChased = Enemy.AnyChaseActive;

        // Rebuild the bag when re-enabled (in case lists changed)
        RebuildEntryShuffleBag();
    }

    private void OnDisable()
    {
        _controls.Player.LockOn.performed -= OnLockOnPerformed;
        _controls.Player.Disable();
        SaveProgress();

        // ADD: unsubscribe
        Enemy.GlobalChaseChanged -= OnGlobalChaseChanged;
    }

    // ADD: chase event handler
    private void OnGlobalChaseChanged(bool active)
    {
        _currentlyChased = active;

        if (!_blockWhileChased) return;

        if (active)
        {
            // If a chase starts while inside reporter mode, force-exit immediately.
            if (_playerInside && IsInReporterMode())
            {
                ForceExitReporterMode("Chase began");
                if (_playerUI != null) _playerUI.UpdateText(_chasedPromptMessage);
            }
        }
        else
        {
            // When chase ends, refresh prompt if inside.
            if (_playerInside && _playerUI != null)
            {
                _playerUI.UpdateText(_enterPromptMessage);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null) return;

        _playerInputReader = reader;
        _playerUI = other.GetComponentInParent<PlayerUI>();
        _playerAnimator = other.GetComponentInParent<Animator>();
        _playerInside = true;

        // MOD: chase-gated prompt + controls
        if (_blockWhileChased && _currentlyChased)
        {
            _playerUI?.UpdateText(_chasedPromptMessage);
            _controls.Player.Disable(); // do not allow interaction
        }
        else
        {
            _playerUI?.UpdateText(_enterPromptMessage);
            _controls.Player.Enable();
        }

        EvaluateReportArtifactVisibility();
    }

    private void OnTriggerExit(Collider other)
    {
        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null || reader != _playerInputReader) return;

        if (_bRollActive) RevertBRoll();

        RestoreBRollFovToDefault(clearCache: true);
        StopReporterAudio();

        _reportAnimDetected = false;
        _reporterDelayRemaining = 0f;
        _reporterAudioStarted = false;

        SetAnimatorReporting(false);

        _playerInputReader.SuppressLockOnToggle = false;
        ResetInputReaderInversionState(_playerInputReader);

        if (EnsureCameraManRefs() && _cameraManFollower != null && !_cameraManFollower.enabled)
        {
            if (_cameraManAgent != null && _cameraManAgent.isOnNavMesh) _cameraManAgent.ResetPath();
            if (_cameraManOriginalStoppingDistance >= 0f) _cameraManAgent.stoppingDistance = _cameraManOriginalStoppingDistance;
            _cameraManFollower.enabled = true;
        }

        EvaluateReportArtifactVisibility();
        SaveProgress();

        _playerUI?.UpdateText(string.Empty);
        _playerInside = false;
        _playerInputReader = null;
        _playerUI = null;
        _pendingUnlockOnReenable = false;

        _entryClipPlayedThisSession = false; // reset entry VO per-session guard
        _controls.Player.Disable();
    }

    private void Update()
    {
        if (!_playerInside || _playerInputReader == null) return;

        // ADD: hard gate while chased
        if (_blockWhileChased && _currentlyChased)
        {
            if (IsInReporterMode())
                ForceExitReporterMode("Chase active while inside trigger");

            if (_playerUI != null)
                _playerUI.UpdateText(_chasedPromptMessage);

            return; // skip reporter logic while chased
        }

        bool inReporterMode = IsReaderLockedOn(_playerInputReader);

        if (inReporterMode && !_reportAnimDetected && DidReportAnimationStart())
        {
            _reportAnimDetected = true;
            _reporterDelayRemaining = Mathf.Max(0f, _reporterTimeDelay);
            _reporterAudioStarted = false;
        }

        if (inReporterMode && _reportAnimDetected)
        {
            if (_reporterDelayRemaining > 0f)
            {
                _reporterDelayRemaining -= Time.deltaTime;
                if (_reporterDelayRemaining < 0f) _reporterDelayRemaining = 0f;
            }

            if (_reporterDelayRemaining <= 0f && !_reporterAudioStarted)
            {
                StartReporterAudio();
                _reporterAudioStarted = true;
            }
        }

        if (inReporterMode && _reportAnimDetected && _reporterDelayRemaining <= 0f && !_bothComplete)
        {
            if (_bRollActive)
            {
                if (IsBRollZoomedEnough())
                {
                    _accumBRollSeconds = Mathf.Min(_requiredBRollSeconds, _accumBRollSeconds + Time.deltaTime);
                }
            }
            else
            {
                _accumReporterSeconds = Mathf.Min(_requiredReporterSeconds, _accumReporterSeconds + Time.deltaTime);
            }

            bool nowComplete = IsReporterComplete() && IsBRollComplete();
            if (nowComplete != _bothComplete)
            {
                _bothComplete = nowComplete;
                EvaluateReportArtifactVisibility();
                SaveProgress();
                if (_bothComplete) RaiseProgressChanged();
            }
        }

        if (_playerUI != null)
        {
            if (inReporterMode)
            {
                float repLeft = Mathf.Max(0f, _requiredReporterSeconds - _accumReporterSeconds);
                float brLeft = Mathf.Max(0f, _requiredBRollSeconds - _accumBRollSeconds);

                string repLine = repLeft <= 0.001f ? "Footage Captured" : $"Footage left: {repLeft:0}s";
                string brLine = brLeft <= 0.001f ? "B-Roll Captured" : $"B-Roll left: {brLeft:0}s";

                if (_bRollActive && brLeft > 0f && !IsBRollZoomedEnough())
                {
                    brLine += "\nZoom in to record B-Roll";
                }

                _playerUI.UpdateText($"{_reporterPromptMessage}{repLine}\n{brLine}");
            }
            else
            {
                _playerUI.UpdateText(_enterPromptMessage);
            }
        }

        if (_bRollActive && !inReporterMode)
        {
            RevertBRoll();
        }

        if (inReporterMode && Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            ToggleBRoll();
        }

        if (_bRollActive && Mouse.current != null && _cameraManCamera != null)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y;
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

        // ADD: block interaction while chased
        if (_blockWhileChased && _currentlyChased)
        {
            _playerUI?.UpdateText(_chasedPromptMessage);
            return;
        }

        _playerInputReader.SuppressLockOnToggle = true;

        if (_playerInputReader.enabled)
        {
            // ENTER
            if (!IsReaderLockedOn(_playerInputReader))
            {
                _playerInputReader.onLockOnToggled?.Invoke();
                _playerInputReader.onSprintDeactivated?.Invoke();
            }
            _playerInputReader.enabled = false;
            _pendingUnlockOnReenable = true;

            if (EnsureCameraManRefs())
            {
                if (_cameraManOriginalStoppingDistance < 0f && _cameraManAgent != null)
                    _cameraManOriginalStoppingDistance = _cameraManAgent.stoppingDistance;

                if (_cameraManCamera != null && !_cameraManFovCached)
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

            _reportAnimDetected = false;
            _reporterDelayRemaining = 0f;
            _reporterAudioStarted = false;

            SetAnimatorReporting(true);

            // NEW: play a single random entry VO (one-shot) per Reporter Mode session
            PlayEntryOneShot();

            UpdateArtifactVisibility(inRecording: true);
            SaveProgress();
        }
        else
        {
            // EXIT
            _playerInputReader.enabled = true;

            if (IsReaderLockedOn(_playerInputReader))
            {
                _playerInputReader.onLockOnToggled?.Invoke();
                _playerInputReader.onSprintDeactivated?.Invoke();
            }

            if (_bRollActive) RevertBRoll();
            else RestoreBRollFovToDefault(clearCache: true);

            if (EnsureCameraManRefs() && _cameraManFollower != null)
            {
                if (_cameraManAgent != null && _cameraManAgent.isOnNavMesh) _cameraManAgent.ResetPath();
                if (_cameraManOriginalStoppingDistance >= 0f) _cameraManAgent.stoppingDistance = _cameraManOriginalStoppingDistance;
                _cameraManFollower.enabled = true;
            }

            StopReporterAudio();

            SetAnimatorReporting(false);
            _reportAnimDetected = false;
            _reporterDelayRemaining = 0f;
            _reporterAudioStarted = false;

            // reset entry VO guard for next entry
            _entryClipPlayedThisSession = false;

            ResetInputReaderInversionState(_playerInputReader);
            _pendingUnlockOnReenable = false;

            EvaluateReportArtifactVisibility();
            SaveProgress();

            if (_bothComplete && !_completedAndDisabled)
            {
                _playerUI?.UpdateText(string.Empty);
                _completedAndDisabled = true;
                RaiseProgressChanged();
                gameObject.SetActive(false);
            }
        }

        _playerInputReader.SuppressLockOnToggle = false;
    }

    private void ToggleBRoll()
    {
        if (_bRollActive) { RevertBRoll(); return; }
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
        MoveCameraManTo(_bRollCameraPosition.position, 0f);

        _bRollActive = true;
    }

    private void RevertBRoll()
    {
        if (_followPlayerCamera != null && _originalLookAtTarget != null)
        {
            SetFollowCameraTarget(_followPlayerCamera, _originalLookAtTarget);
        }

        RestoreBRollFovToDefault(clearCache: false);

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
        EvaluateReportArtifactVisibility();
    }

    private void RestoreBRollFovToDefault(bool clearCache)
    {
        if (_cameraManCamera == null && _cameraManTransform != null)
        {
            _cameraManCamera = _cameraManTransform.GetComponentInChildren<Camera>(true);
        }

        if (_cameraManCamera != null && _cameraManFovCached)
        {
            _cameraManCamera.fieldOfView = _cameraManOriginalFov;
        }

        if (clearCache)
        {
            _cameraManFovCached = false;
        }
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
        _reportAudioSource.spatialBlend = 0f;
        _reportAudioSource.volume = 0f;

        if (_reportClip != null) _reportAudioSource.clip = _reportClip;
    }

    // Add this helper near the other audio helpers
    private void EnsureEntryAudioSource()
    {
        if (_entryAudioSource != null) return;

        // Create a dedicated 2D one-shot source for entry VO
        _entryAudioSource = gameObject.AddComponent<AudioSource>();
        _entryAudioSource.playOnAwake = false;
        _entryAudioSource.loop = false;
        _entryAudioSource.spatialBlend = 0f; // 2D
        _entryAudioSource.volume = Mathf.Clamp01(_entryVolume);

        // Route through same mixer group as reporter audio if available
        if (_reportAudioSource != null)
        {
            try { _entryAudioSource.outputAudioMixerGroup = _reportAudioSource.outputAudioMixerGroup; } catch { /* optional */ }
        }
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
        if (_reportAudioSource == null || !_reportAudioSource.isPlaying) return;

        if (_audioFadeRoutine != null)
        {
            StopCoroutine(_audioFadeRoutine);
            _audioFadeRoutine = null;
        }

        _audioFadeRoutine = StartCoroutine(FadeOutAndStop(_reportAudioSource, _fadeOutDuration));
    }

    // ------- Entry VO helpers (shuffle-bag like Enemy.cs) -------

    private IReadOnlyList<AudioClip> GetActiveEntryClips()
    {
        if (_reporterEntryClips != null && _reporterEntryClips.Count > 0)
            return _reporterEntryClips;

        if (_useGlobalEntryClips && _globalEntryClips != null && _globalEntryClips.Count > 0)
            return _globalEntryClips;

        return Array.Empty<AudioClip>();
    }

    private void RebuildEntryShuffleBag()
    {
        var clips = GetActiveEntryClips();
        _entryShuffleBag = new List<int>();
        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i] != null)
                _entryShuffleBag.Add(i);
        }

        // Fisher-Yates shuffle
        for (int i = _entryShuffleBag.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (_entryShuffleBag[i], _entryShuffleBag[j]) = (_entryShuffleBag[j], _entryShuffleBag[i]);
        }
        _entryShuffleIndex = 0;
    }

    private int GetNextEntryShuffleIndex()
    {
        var clips = GetActiveEntryClips();
        if (clips.Count == 0) return -1;

        if (_entryShuffleBag == null || _entryShuffleBag.Count == 0 || _entryShuffleIndex >= _entryShuffleBag.Count)
            RebuildEntryShuffleBag();

        if (_entryShuffleBag.Count == 0) return -1;

        int idx = _entryShuffleBag[_entryShuffleIndex++];

        // Global immediate anti-repeat: avoid same index as last across boxes if possible
        if (_entryShuffleBag.Count > 1 && idx == _lastEntryIndex)
        {
            // if possible, pick the next in bag this round
            if (_entryShuffleIndex < _entryShuffleBag.Count)
                idx = _entryShuffleBag[_entryShuffleIndex++];
            else
            {
                // bag exhausted; rebuild and pick first
                RebuildEntryShuffleBag();
                if (_entryShuffleBag.Count > 0) idx = _entryShuffleBag[_entryShuffleIndex++];
            }
        }

        return idx;
    }

    // ------------------------------------------------------------

    // Replace your PlayEntryOneShot() with this version
    private void PlayEntryOneShot()
    {
        if (_entryClipPlayedThisSession) return;

        var clips = GetActiveEntryClips();
        if (clips.Count == 0) return;

        int idx = GetNextEntryShuffleIndex();
        if (idx < 0 || idx >= clips.Count) return;

        var clip = clips[idx];
        if (clip == null) return;

        EnsureEntryAudioSource();

        var src = _entryAudioSource != null ? _entryAudioSource : _reportAudioSource;
        if (src == null)
        {
            EnsureAudioSource();
            src = _reportAudioSource;
            if (src == null) return;
        }

        // Avoid overlap with same clip if requested
        if (_entryAvoidOverlap && src.isPlaying && src.clip == clip)
        {
            _entryClipPlayedThisSession = true;
            _lastEntryIndex = idx;
            return;
        }

        // If fallback to the reporter loop source and it's muted, spawn a temp one-shot so it's audible
        if (ReferenceEquals(src, _reportAudioSource) && _reportAudioSource != null && _reportAudioSource.volume <= 0.001f)
        {
            var go = new GameObject("EntryOneShotTemp");
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake = false;
            a.loop = false;
            a.spatialBlend = 0f;
            a.volume = Mathf.Clamp01(_entryVolume);
            try { a.outputAudioMixerGroup = _reportAudioSource.outputAudioMixerGroup; } catch { }
            a.clip = clip;
            a.Play();
            Destroy(go, clip.length + 0.1f);

            _entryClipPlayedThisSession = true;
            _lastEntryIndex = idx;
            return;
        }

        if (ReferenceEquals(src, _entryAudioSource))
            _entryAudioSource.volume = Mathf.Clamp01(_entryVolume);

        src.PlayOneShot(clip, Mathf.Clamp01(_entryVolume));
        _entryClipPlayedThisSession = true;
        _lastEntryIndex = idx;
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

    private bool IsReporterComplete() => _accumReporterSeconds >= Mathf.Max(0f, _requiredReporterSeconds - 0.0001f);
    private bool IsBRollComplete() => _accumBRollSeconds >= Mathf.Max(0f, _requiredBRollSeconds - 0.0001f);

    private void EvaluateReportArtifactVisibility()
    {
        bool prev = _bothComplete;
        _bothComplete = IsReporterComplete() && IsBRollComplete();
        UpdateArtifactVisibility(inRecording: false);

        if (!string.IsNullOrEmpty(_reportSaveKey))
        {
            PlayerPrefs.SetInt(_reportSaveKey, _bothComplete ? 1 : 0);
        }

        if (_bothComplete && !prev)
        {
            // Progress just reached completion; notify (disable happens on exit)
            RaiseProgressChanged();
        }
    }

    private void UpdateArtifactVisibility(bool inRecording)
    {
        if (_reportArtifact == null) return;

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

    private void SetAnimatorReporting(bool value)
    {
        if (_playerAnimator == null) return;
        _playerAnimator.SetBool(_isReportingHash, value);
    }

    // Detects the start of the A_A_Report animation on the given layer.
    private bool DidReportAnimationStart()
    {
        if (_playerAnimator == null) return false;

        // If transitioning into the report state, consider it "started"
        if (_playerAnimator.IsInTransition(_reportLayerIndex))
        {
            var nextInfo = _playerAnimator.GetNextAnimatorStateInfo(_reportLayerIndex);
            if (nextInfo.shortNameHash == _reportStateHash)
                return true;
        }

        // Or if we are already in the report state very early in its timeline
        var currentInfo = _playerAnimator.GetCurrentAnimatorStateInfo(_reportLayerIndex);
        if (currentInfo.shortNameHash == _reportStateHash && currentInfo.normalizedTime < 0.1f)
            return true;

        return false;
    }

    // Returns true if camera FOV is "mostly zoomed" toward min FOV based on _bRollZoomRequirement (0..1)
    private bool IsBRollZoomedEnough()
    {
        if (_cameraManCamera == null) return false;
        // Normalize: 0 = max FOV (zoomed out), 1 = min FOV (fully zoomed)
        float zoom01 = Mathf.InverseLerp(_bRollMaxFov, _bRollMinFov, _cameraManCamera.fieldOfView);
        return zoom01 >= _bRollZoomRequirement;
    }

    // ADD: helper to detect reporter mode from current state (input disabled or lock-on true)
    private bool IsInReporterMode()
    {
        if (_playerInputReader == null) return false;
        bool locked = IsReaderLockedOn(_playerInputReader);
        bool inputDisabled = !_playerInputReader.enabled; // we disable input during reporter mode
        return locked || inputDisabled;
    }

    // ADD: unified exit path reused for chase-forced exits
    private void ForceExitReporterMode(string reason = null)
    {
        if (_playerInputReader == null) return;
        if (!IsInReporterMode()) return;

        _playerInputReader.enabled = true;

        if (IsReaderLockedOn(_playerInputReader))
        {
            _playerInputReader.onLockOnToggled?.Invoke();
            _playerInputReader.onSprintDeactivated?.Invoke();
        }

        if (_bRollActive) RevertBRoll();
        else RestoreBRollFovToDefault(clearCache: true);

        if (EnsureCameraManRefs() && _cameraManFollower != null)
        {
            if (_cameraManAgent != null && _cameraManAgent.isOnNavMesh) _cameraManAgent.ResetPath();
            if (_cameraManOriginalStoppingDistance >= 0f) _cameraManAgent.stoppingDistance = _cameraManOriginalStoppingDistance;
            _cameraManFollower.enabled = true;
        }

        StopReporterAudio();

        SetAnimatorReporting(false);
        _reportAnimDetected = false;
        _reporterDelayRemaining = 0f;
        _reporterAudioStarted = false;

        // reset entry VO guard as we exit
        _entryClipPlayedThisSession = false;

        ResetInputReaderInversionState(_playerInputReader);
        _pendingUnlockOnReenable = false;

        EvaluateReportArtifactVisibility();
        SaveProgress();

        if (_bothComplete && !_completedAndDisabled)
        {
            _playerUI?.UpdateText(string.Empty);
            _completedAndDisabled = true;
            RaiseProgressChanged();
            gameObject.SetActive(false);
        }
    }
}