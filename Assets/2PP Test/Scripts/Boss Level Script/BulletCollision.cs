using System;
using UnityEngine;

public class BulletCollision : MonoBehaviour
{
    [Header("Raycast Ignore Settings")]
    public string[] ignoredTags; // Assign tags to ignore in Inspector or via script

    public float bulletDamage = 10f; // Set this value when spawning the bullet

    void OnCollisionEnter(Collision collision)
    {
        // Ignore collision with specified tags
        if (ignoredTags != null && ignoredTags.Length > 0)
        {
            foreach (string tag in ignoredTags)
            {
                if (collision.gameObject.CompareTag(tag))
                {
                    return; // Do not destroy bullet
                }
            }
        }

        // Player hit logic
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Bullet hit the player!");
            Health health = collision.gameObject.GetComponent<Health>();
            if (health != null)
            {
                health.health -= bulletDamage;
                Debug.Log("Player health after hit: " + health.health);
            }
            Destroy(gameObject);
            return;
        }

        // Enemy hit logic
        if (collision.gameObject.CompareTag("Enemy"))
        {
            Debug.Log("Bullet hit the enemy!");
            Destroy(gameObject);
            return;
        }

        // Destroy the bullet on any other collision
        Destroy(gameObject);
    }
}
