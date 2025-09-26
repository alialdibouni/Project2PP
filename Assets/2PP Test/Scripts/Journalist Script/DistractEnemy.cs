using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events; // ADD: for UnityEvents
using Synty.AnimationBaseLocomotion.Samples.InputSystem; // for InputReader

public class DistractEnemy : MonoBehaviour
{
    [Header("Activation")]
    [Tooltip("Require the player to be inside this trigger to use distraction.")]
    [SerializeField] private bool requirePlayerInside = true;

    [Tooltip("Key to press (uses Unity Input System Keyboard)")]
    [SerializeField] private KeyCode displayOnlyKey = KeyCode.E; // display only; actual read uses Keyboard.current.eKey

    [Header("Targeting")]
    [Tooltip("Radius around the Search Point used to find guards to distract.")]
    [SerializeField] private float distractRadius = 12f;

    [Tooltip("Affect only the closest guard within radius. If unchecked, affects all within radius.")]
    [SerializeField] private bool affectClosestOnly = true;

    [Tooltip("Optional override for where the guard should 'search'. If null, uses this object's transform.")]
    [SerializeField] private Transform searchPoint;

    [Header("Cooldown")]
    [Tooltip("Time (seconds) between uses.")]
    [SerializeField] private float cooldownSeconds = 3f;

    [Header("UI Prompt (Optional)")]
    [SerializeField] private bool showPrompt = true;
    [TextArea]
    [SerializeField] private string promptMessage = "Press E to Distract nearby guard(s)";
    [TextArea]
    [SerializeField] private string cooldownPromptMessage = "Distraction cooling down...";
    [TextArea]
    [SerializeField] private string activePromptMessage = "Distraction active...";
    [TextArea]
    [SerializeField] private string usedPromptMessage = "Distraction already handled.";

    [Header("Distraction Audio (Optional - can be disabled)")]
    [SerializeField] private bool useBuiltInAudio = false;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip activateClip;
    [SerializeField] private bool loopAudio = true;
    [SerializeField] private float maxActiveDuration = 20f;

    // ADD: built-in audio fade settings
    [Header("Built-in Audio Fade")]
    [SerializeField] private float builtInFadeOutDuration = 0.6f;
    private Coroutine _builtInFadeRoutine;

    [Header("Resolution Rules")]
    [Tooltip("Stop the distraction as soon as a guard gets close to the point.")]
    [SerializeField] private bool stopWhenGuardArrives = true;
    [Tooltip("Distance at which a guard is considered to have arrived.")]
    [SerializeField] private float arriveStopDistance = 2f;
    [Tooltip("If true, this distractor can only be used once. Otherwise, it can be reused after cooldown.")]
    [SerializeField] private bool singleUse = false;

    [Header("Events")]
    [Tooltip("Invoked as soon as the distraction is activated (call your Radio/CarAlarm methods here).")]
    [SerializeField] private UnityEvent onActivated;
    [Tooltip("Invoked when a guard arrives and the distraction is stopped (e.g., turn off radio/alarm).")]
    [SerializeField] private UnityEvent onGuardArrived;
    [Tooltip("Invoked whenever the distraction is deactivated (timeout or manual stop).")]
    [SerializeField] private UnityEvent onDeactivated;

    [Header("Behavior When No Guards Nearby")]
    [Tooltip("If true, onActivated still fires even if no guard is found in range.")]
    [SerializeField] private bool invokeOnInteractEvenIfNoGuard = true; // ADD

    private bool _playerInside;
    private PlayerUI _playerUI;
    private float _cooldownRemaining;
    private bool _promptShownByUs;

    private bool _active;
    private float _activeTimer;
    private bool _consumed;
    private readonly List<Enemy> _activeTargets = new List<Enemy>();

    private void OnTriggerEnter(Collider other)
    {
        if (!requirePlayerInside) return;

        // Detect player by presence of InputReader (consistent with ReporterTriggerBox usage)
        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null) return;

        _playerInside = true;
        _playerUI = other.GetComponentInParent<PlayerUI>();

        if (showPrompt && _playerUI != null)
        {
            _playerUI.UpdateText(GetCurrentPrompt());
            _promptShownByUs = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!requirePlayerInside) return;

        var reader = other.GetComponentInParent<InputReader>();
        if (reader == null) return;

        _playerInside = false;

        if (_promptShownByUs && _playerUI != null)
        {
            _playerUI.UpdateText(string.Empty);
        }

        _promptShownByUs = false;
        _playerUI = null;
    }

    private void Update()
    {
        if (_cooldownRemaining > 0f)
            _cooldownRemaining -= Time.deltaTime;

        // Update the UI prompt dynamically if we own it
        if (_promptShownByUs && _playerUI != null && showPrompt)
        {
            _playerUI.UpdateText(GetCurrentPrompt());
        }

        if (_active)
        {
            _activeTimer += Time.deltaTime;

            // Stop when any guard arrives
            if (stopWhenGuardArrives && HasAnyTargetArrived())
            {
                StopDistraction(guardArrived: true);
            }
            else if (maxActiveDuration > 0f && _activeTimer >= maxActiveDuration)
            {
                StopDistraction(guardArrived: false);
            }
        }

        if (requirePlayerInside && !_playerInside) return;
        if (_consumed) return; // single-use consumed

        if (Keyboard.current == null) return;
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryDistract();
        }
    }

    public bool TryDistract()
    {
        if (_cooldownRemaining > 0f) return false;
        if (_active) return false; // already active
        if (_consumed) return false; // single-use already handled

        Vector3 anchor = (searchPoint != null ? searchPoint.position : transform.position);

        // Find nearby enemies; simple approach using scene scan (OK for small counts)
        Enemy[] all = FindObjectsOfType<Enemy>();
        List<Enemy> hits = null;
        Enemy closest = null;

        if (all != null && all.Length > 0)
        {
            hits = new List<Enemy>();
            float bestSqr = float.MaxValue;
            float maxSqr = distractRadius * distractRadius;

            foreach (var e in all)
            {
                if (e == null) continue;
                float sqr = (e.transform.position - anchor).sqrMagnitude;
                if (sqr <= maxSqr)
                {
                    hits.Add(e);
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        closest = e;
                    }
                }
            }
        }

        _activeTargets.Clear();

        if (hits == null || hits.Count == 0)
        {
            if (!invokeOnInteractEvenIfNoGuard) return false;

            // No guards, but still fire external behavior (radio, alarm, etc.")
            BeginDistraction();
            _cooldownRemaining = cooldownSeconds;
            return true;
        }

        if (affectClosestOnly)
        {
            ForceEnemySearch(closest, anchor);
            _activeTargets.Add(closest);
        }
        else
        {
            for (int i = 0; i < hits.Count; i++)
            {
                ForceEnemySearch(hits[i], anchor);
                _activeTargets.Add(hits[i]);
            }
        }

        // Start distraction (audio, events)
        BeginDistraction();

        _cooldownRemaining = cooldownSeconds;
        return true;
    }

    private void BeginDistraction()
    {
        if (useBuiltInAudio)
        {
            EnsureAudioSource();

            if (audioSource != null)
            {
                // cancel any pending fade so we can restart cleanly
                if (_builtInFadeRoutine != null)
                {
                    StopCoroutine(_builtInFadeRoutine);
                    _builtInFadeRoutine = null;
                }

                if (audioSource.clip == null && activateClip != null)
                    audioSource.clip = activateClip;

                audioSource.loop = loopAudio;
                audioSource.volume = Mathf.Clamp01(audioSource.volume); // keep current volume
                audioSource.Play();
            }
        }

        _active = true;
        _activeTimer = 0f;

        onActivated?.Invoke(); // external scripts (e.g., Radio.ActivateRadio)
    }

    // MOD: add fade option
    private void StopDistraction(bool guardArrived, bool fadeBuiltInAudio = false)
    {
        if (useBuiltInAudio && audioSource != null && audioSource.isPlaying)
        {
            if (fadeBuiltInAudio)
            {
                // fade then stop
                if (_builtInFadeRoutine != null)
                    StopCoroutine(_builtInFadeRoutine);
                _builtInFadeRoutine = StartCoroutine(FadeOutAndStopAudioSource(audioSource, builtInFadeOutDuration));
            }
            else
            {
                audioSource.Stop();
            }
        }

        _active = false;
        _activeTargets.Clear();

        if (singleUse)
            _consumed = true;

        if (guardArrived)
            onGuardArrived?.Invoke();

        // External audio/scripts (e.g., Radio) should be wired here
        // to fade out via UnityEvent -> Radio.FadeOutAndStop()
        onDeactivated?.Invoke();
    }

    private System.Collections.IEnumerator FadeOutAndStopAudioSource(AudioSource src, float duration)
    {
        if (src == null) yield break;

        float startVol = src.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = duration > 0f ? t / duration : 1f;
            src.volume = Mathf.Lerp(startVol, 0f, k);
            yield return null;
        }
        src.volume = 0f;
        src.Stop();
        // restore volume for next activation
        src.volume = startVol <= 0f ? 1f : startVol;
        _builtInFadeRoutine = null;
    }

    private void OnEnable()
    {
        // ADD: listen for player-caught notifications
        Enemy.PlayerCaught += OnPlayerCaught;
    }

    private void OnDisable()
    {
        Enemy.PlayerCaught -= OnPlayerCaught;
    }

    // ADD: player caught handler -> fade and stop distraction
    private void OnPlayerCaught()
    {
        if (_active)
        {
            // Fade built-in audio if used; also fires your UnityEvents
            StopDistraction(guardArrived: false, fadeBuiltInAudio: true);
        }
    }

    private bool HasAnyTargetArrived()
    {
        if (_activeTargets.Count == 0) return false;

        Vector3 anchor = (searchPoint != null ? searchPoint.position : transform.position);
        float sqr = arriveStopDistance * arriveStopDistance;

        // Cull nulls and check arrival
        for (int i = _activeTargets.Count - 1; i >= 0; i--)
        {
            var e = _activeTargets[i];
            if (e == null)
            {
                _activeTargets.RemoveAt(i);
                continue;
            }

            float d2 = (e.transform.position - anchor).sqrMagnitude;
            if (d2 <= sqr)
                return true;
        }

        return false;
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            // Reasonable 3D defaults for world props
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f; // 3D
            audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            audioSource.minDistance = 3f;
            audioSource.maxDistance = 25f;
        }
    }

    private static void ForceEnemySearch(Enemy enemy, Vector3 searchAt)
    {
        if (enemy == null) return;

        // Ensure guards head to the distract location
        enemy.LastKnownPos = searchAt;

        // Switch to SearchState; it will return to PatrolState on its own after searching
        var sm = enemy.GetComponent<StateMachine>();
        if (sm != null)
        {
            sm.ChangeState(new SearchState());
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 anchor = (searchPoint != null ? searchPoint.position : transform.position);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(anchor, distractRadius);
        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        Gizmos.DrawSphere(anchor, 0.2f);

        // Arrival radius
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(anchor, arriveStopDistance);
    }

    private string GetCurrentPrompt()
    {
        if (_consumed) return usedPromptMessage;
        if (_active) return activePromptMessage;
        if (_cooldownRemaining > 0f) return cooldownPromptMessage;
        return promptMessage;
    }
}
