using UnityEngine;

// Attach this to the trigger volume that defines the guard area.
// Ensure the collider is set to "IsTrigger". Add a kinematic Rigidbody here
// if the player doesn't have a Rigidbody to guarantee trigger callbacks.
[RequireComponent(typeof(Collider))]
public class GuardArea : MonoBehaviour
{
    [SerializeField] private Enemy enemy;

    private void Awake()
    {
        if (enemy == null)
        {
            enemy = GetComponentInParent<Enemy>();
        }

        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && enemy != null)
        {
            enemy.SetPlayerInGuardArea(true);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && enemy != null)
        {
            enemy.SetPlayerInGuardArea(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && enemy != null)
        {
            enemy.SetPlayerInGuardArea(false);
        }
    }
}