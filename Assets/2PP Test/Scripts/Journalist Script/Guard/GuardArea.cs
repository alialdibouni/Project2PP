using System.Collections.Generic;
using UnityEngine;

// Attach this to the trigger volume that defines the guard area.
// Ensure the collider is set to "IsTrigger". Add a kinematic Rigidbody here
// if the player doesn't have a Rigidbody to guarantee trigger callbacks.
[RequireComponent(typeof(Collider))]
public class GuardArea : MonoBehaviour
{
    [SerializeField] private List<Enemy> enemies = new List<Enemy>();

    private void Awake()
    {
        if (enemies == null)
            enemies = new List<Enemy>();

        // Backward-compat: if none assigned, try populate from parents
        if (enemies.Count == 0)
        {
            var found = GetComponentsInParent<Enemy>(includeInactive: true);
            if (found != null && found.Length > 0)
            {
                enemies.AddRange(found);
            }
        }

        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null)
                enemies[i].SetPlayerInGuardArea(true);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null)
                enemies[i].SetPlayerInGuardArea(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null)
                enemies[i].SetPlayerInGuardArea(false);
        }
    }
}