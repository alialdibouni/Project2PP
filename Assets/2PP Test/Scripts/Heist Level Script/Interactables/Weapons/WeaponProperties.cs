using UnityEngine;

public class WeaponProperites : Weapon
{
    [Header("Weapon Raycast")]
    [SerializeField] private LayerMask hitMask = ~0; // everything by default

    protected override void PerformShot()
    {
        // Simple hitscan from center of camera; replace with your own logic as needed
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        // Debug ray like PlayerInteract
        Debug.DrawRay(ray.origin, ray.direction * range, Color.red);

        if (Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            var health = hit.collider.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(damage);
            }

            // Add impact VFX/SFX here if desired
            Debug.Log($"Weapon hit: {hit.collider.name}");
        }
    }
}