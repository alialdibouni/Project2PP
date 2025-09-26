using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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

    private bool _playerInside;
    private PlayerUI _playerUI;
    private float _cooldownRemaining;
    private bool _promptShownByUs;

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
            _playerUI.UpdateText(promptMessage);
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

        if (requirePlayerInside && !_playerInside) return;

        if (Keyboard.current == null) return;
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryDistract();
        }
    }

    public bool TryDistract()
    {
        if (_cooldownRemaining > 0f) return false;

        Vector3 anchor = (searchPoint != null ? searchPoint.position : transform.position);

        // Find nearby enemies; simple approach using scene scan (OK for small counts)
        Enemy[] all = FindObjectsOfType<Enemy>();
        if (all == null || all.Length == 0) return false;

        Enemy closest = null;
        float bestSqr = float.MaxValue;
        List<Enemy> hits = new List<Enemy>();

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

        if (hits.Count == 0) return false;

        if (affectClosestOnly)
        {
            ForceEnemySearch(closest, anchor);
        }
        else
        {
            for (int i = 0; i < hits.Count; i++)
                ForceEnemySearch(hits[i], anchor);
        }

        _cooldownRemaining = cooldownSeconds;
        return true;
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
    }
}
