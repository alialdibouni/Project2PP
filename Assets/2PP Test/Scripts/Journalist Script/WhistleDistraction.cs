using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player-attached whistle distraction:
/// - Press H to whistle (cooldown gated).
/// - Nearby enemies are forced into SearchState at the whistle position.
/// - Their sightDistance is temporarily reduced to give the player leeway.
/// - Once they leave SearchState (back to Patrol or something else), sightDistance is restored.
/// </summary>
public class WhistleDistraction : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Keyboard key (new Input System) used to trigger the whistle.")]
    [SerializeField] private Key triggerKey = Key.H;

    [Header("Whistle Settings")]
    [Tooltip("Cooldown between whistles (seconds).")]
    [SerializeField] private float whistleCooldown = 2f;
    [Tooltip("Radius to attract / distract enemies.")]
    [SerializeField] private float whistleRadius = 15f;
    [Tooltip("Force only the closest enemy instead of all inside radius.")]
    [SerializeField] private bool affectClosestOnly = false;
    [Tooltip("Limit the number of enemies affected (0 = no limit, ignored if Affect Closest Only).")]
    [SerializeField] private int maxEnemies = 0;

    [Header("Sight Distance Reduction")]
    [Tooltip("Original enemy sightDistance will be reduced to this while searching due to whistle.")]
    [SerializeField] private float reducedSightDistance = 2f;

    [Header("Audio")]
    [Tooltip("AudioSource for whistle playback (2D or 3D). If null one will be created.")]
    [SerializeField] private AudioSource whistleAudioSource;
    [Tooltip("Random whistle clips; one is chosen per whistle (no immediate repeat; shuffle cycle).")]
    [SerializeField] private AudioClip[] whistleClips;
    [Range(0f, 1f)][SerializeField] private float whistleVolume = 1f;

    [Header("Filters")]
    [Tooltip("Ignore enemies that are already chasing (optional).")]
    [SerializeField] private bool skipIfInChase = true;
    [Tooltip("Ignore enemies currently processing a catch / fade.")]
    [SerializeField] private bool skipIfCatching = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.8f, 0.2f, 0.25f);

    // Cooldown timer
    private float _cooldownRemaining;

    // Track modified enemies (enemy -> original sightDistance)
    private readonly Dictionary<Enemy, float> _modifiedEnemies = new Dictionary<Enemy, float>();

    // Shuffle bag for whistle audio
    private List<int> _shuffleBag;
    private int _shuffleIndex;

    private void Awake()
    {
        if (whistleAudioSource == null)
        {
            whistleAudioSource = gameObject.AddComponent<AudioSource>();
            whistleAudioSource.playOnAwake = false;
            whistleAudioSource.loop = false;
            whistleAudioSource.spatialBlend = 0f; // 2D by default; adjust as desired
        }
        RebuildWhistleShuffleBag();
    }

    private void Update()
    {
        // Cooldown ticking
        if (_cooldownRemaining > 0f)
            _cooldownRemaining -= Time.deltaTime;

        // Key input (new Input System)
        if (Keyboard.current != null &&
            Keyboard.current[triggerKey].wasPressedThisFrame &&
            _cooldownRemaining <= 0f)
        {
            PerformWhistle();
        }

        // Monitor enemies & restore sightDistance when they exit SearchState
        MaintainModifiedEnemies();
    }

    private void PerformWhistle()
    {
        Vector3 origin = transform.position;

        // Play whistle audio
        PlayWhistleClip();

        // Acquire enemies
        Enemy[] all = FindObjectsOfType<Enemy>();
        if (all == null || all.Length == 0)
        {
            if (debugLog) Debug.Log("[WhistleDistraction] No enemies in scene.");
            _cooldownRemaining = whistleCooldown;
            return;
        }

        List<(Enemy enemy, float sqr)> candidates = new List<(Enemy, float)>();
        float maxSqr = whistleRadius * whistleRadius;

        foreach (var e in all)
        {
            if (e == null) continue;

            // Distance check
            float d2 = (e.transform.position - origin).sqrMagnitude;
            if (d2 > maxSqr) continue;

            if (skipIfCatching && e.IsProcessingCatch) continue;

            // Optionally detect chase state by state name string (Enemy currentState holds that) or by looking at its StateMachine
            if (skipIfInChase)
            {
                var sm = e.GetComponent<StateMachine>();
                if (sm != null && sm.activeState != null)
                {
                    string stateName = sm.activeState.GetType().Name;
                    if (stateName.Contains("Chase")) continue;
                }
            }

            candidates.Add((e, d2));
        }

        if (candidates.Count == 0)
        {
            if (debugLog) Debug.Log("[WhistleDistraction] No valid enemies in radius.");
            _cooldownRemaining = whistleCooldown;
            return;
        }

        if (affectClosestOnly)
        {
            candidates.Sort((a, b) => a.sqr.CompareTo(b.sqr));
            ApplyWhistleToEnemy(candidates[0].enemy, origin);
        }
        else
        {
            // Sort so if there's a max limit we take nearest ones
            candidates.Sort((a, b) => a.sqr.CompareTo(b.sqr));
            int applied = 0;
            foreach (var c in candidates)
            {
                if (maxEnemies > 0 && applied >= maxEnemies) break;
                ApplyWhistleToEnemy(c.enemy, origin);
                applied++;
            }
        }

        _cooldownRemaining = whistleCooldown;
        if (debugLog) Debug.Log($"[WhistleDistraction] Whistle applied to {(!affectClosestOnly ? Mathf.Min(candidates.Count, maxEnemies > 0 ? maxEnemies : candidates.Count) : 1)} enemy(ies).");
    }

    private void ApplyWhistleToEnemy(Enemy enemy, Vector3 whistlePosition)
    {
        if (enemy == null) return;

        // Record original sightDistance if not already under whistle modification
        if (!_modifiedEnemies.ContainsKey(enemy))
        {
            _modifiedEnemies[enemy] = enemy.sightDistance;
            enemy.sightDistance = Mathf.Max(0.1f, reducedSightDistance);
        }

        // Force search behavior similarly to DistractEnemy
        enemy.LastKnownPos = whistlePosition;
        var sm = enemy.GetComponent<StateMachine>();
        if (sm != null)
        {
            sm.ChangeState(new SearchState());
        }
    }

    private void MaintainModifiedEnemies()
    {
        if (_modifiedEnemies.Count == 0) return;

        // Collect to restore
        var restoreList = ListPool<Enemy>.Get();

        foreach (var kvp in _modifiedEnemies)
        {
            var enemy = kvp.Key;
            if (enemy == null)
            {
                restoreList.Add(enemy);
                continue;
            }

            var sm = enemy.GetComponent<StateMachine>();
            bool stillSearching = false;
            if (sm != null && sm.activeState != null)
            {
                string stateName = sm.activeState.GetType().Name;
                if (stateName.Contains("Search")) stillSearching = true;
            }

            if (!stillSearching)
            {
                // Done searching => restore
                restoreList.Add(enemy);
            }
        }

        if (restoreList.Count > 0)
        {
            foreach (var e in restoreList)
            {
                if (e != null && _modifiedEnemies.TryGetValue(e, out var original))
                {
                    e.sightDistance = original;
                }
                _modifiedEnemies.Remove(e);
            }
        }

        ListPool<Enemy>.Release(restoreList);
    }

    private void PlayWhistleClip()
    {
        if (whistleClips == null || whistleClips.Length == 0) return;

        int idx = GetNextWhistleIndex();
        if (idx < 0) return;

        var clip = whistleClips[idx];
        if (clip == null) return;

        if (whistleAudioSource.isPlaying)
            whistleAudioSource.Stop();

        whistleAudioSource.clip = clip;
        whistleAudioSource.volume = whistleVolume;
        whistleAudioSource.loop = false;
        whistleAudioSource.Play();
    }

    private void RebuildWhistleShuffleBag()
    {
        _shuffleBag = new List<int>();
        if (whistleClips != null)
        {
            for (int i = 0; i < whistleClips.Length; i++)
                if (whistleClips[i] != null)
                    _shuffleBag.Add(i);
        }
        // Fisher-Yates
        for (int i = _shuffleBag.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_shuffleBag[i], _shuffleBag[j]) = (_shuffleBag[j], _shuffleBag[i]);
        }
        _shuffleIndex = 0;
    }

    private int GetNextWhistleIndex()
    {
        if (whistleClips == null || whistleClips.Length == 0) return -1;
        if (_shuffleBag == null || _shuffleBag.Count == 0 || _shuffleIndex >= _shuffleBag.Count)
            RebuildWhistleShuffleBag();
        if (_shuffleBag.Count == 0) return -1;
        return _shuffleBag[_shuffleIndex++];
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, whistleRadius);
    }
}

/// <summary>
/// Lightweight list pooling to avoid GC while tracking modified enemies.
/// </summary>
static class ListPool<T>
{
    private static readonly Stack<List<T>> _pool = new Stack<List<T>>();

    public static List<T> Get()
    {
        return _pool.Count > 0 ? _pool.Pop() : new List<T>();
    }

    public static void Release(List<T> list)
    {
        list.Clear();
        _pool.Push(list);
    }
}
