using System;
using UnityEngine;

public class BulletCollision : MonoBehaviour
{

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnCollisionEnter(Collision collision)
    {
        // Optional: Add player-specific logic here
        if (collision.gameObject.CompareTag("Player"))
        {
            Debug.Log("Bullet hit the player!");
            // TODO: Apply damage or other effects
            Destroy(gameObject);
        }

        // Destroy the bullet on any collision
        Destroy(gameObject);
    }
}
