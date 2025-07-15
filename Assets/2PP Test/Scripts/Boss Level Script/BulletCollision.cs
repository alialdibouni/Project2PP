using System;
using UnityEngine;

public class BulletCollision : MonoBehaviour
{
    [Header("Raycast Ignore Settings")]
    public string[] ignoredTags; // Assign tags to ignore in Inspector or via script

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

        // Optional: Add player-specific logic here
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Bullet hit the player!");
            // TODO: Apply damage or other effects
            Destroy(gameObject);
            return;
        }

        if (collision.gameObject.CompareTag("Enemy"))
        {
            Debug.Log("Bullet hit the enemy!");
            // TODO: Apply damage or other effects
            Destroy(gameObject);
            return;
        }


        // Destroy the bullet on any other collision
        Destroy(gameObject);
    }
}
