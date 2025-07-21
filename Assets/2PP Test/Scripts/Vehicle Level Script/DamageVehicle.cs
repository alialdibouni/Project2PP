using UnityEngine;

public class DamageVehicle : MonoBehaviour
{
    private PlayerDriverInput playerInput;

    // Damage multiplier: tweak as needed for gameplay balance
    public float damageMultiplier = 2.0f;

    [Header("Collision Audio")]
    public AudioClip[] collisionSounds;
    public AudioSource audioSource;

    void Awake()
    {
        playerInput = GetComponent<PlayerDriverInput>();
    }

    void OnCollisionEnter(Collision collision)
    {
        // Check if the collided object has VehicleHealth (AI car)
        VehicleHealth aiHealth = collision.gameObject.GetComponent<VehicleHealth>();
        if (aiHealth != null)
        {
            // Get the player's speed in MPH
            float speed = playerInput.CurrentSpeedMph;

            // Calculate damage (e.g., proportional to speed)
            float damage = speed * damageMultiplier;

            // Apply damage to AI car
            aiHealth.SetHealth(aiHealth.currentHealth - damage);

            // Play a random collision sound
            if (collisionSounds != null && collisionSounds.Length > 0 && audioSource != null)
            {
                int index = Random.Range(0, collisionSounds.Length);
                audioSource.PlayOneShot(collisionSounds[index]);
            }
        }
    }
}
