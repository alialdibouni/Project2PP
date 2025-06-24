using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    public GameObject bulletPrefab; // Assign the bullet prefab in the Inspector
    public GameObject bulletLocation; // Assign the bullet spawn location in the Inspector
    public float bulletSpeed = 20f; // Speed at which the bullet is fired

    private Transform playerTransform;
    void Start()
    {
        // Find the player by tag. Make sure your player GameObject is tagged as "Player"
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            StartCoroutine(SpawnBulletRoutine());
    }

    

    System.Collections.IEnumerator SpawnBulletRoutine()
    {
        while (true)
        {
            SpawnBullet();
            yield return new WaitForSeconds(5f);
        }
    }

    void SpawnBullet()
    {
        if (bulletPrefab != null && bulletLocation != null && playerTransform != null)
        {
            GameObject bullet = Instantiate(
                bulletPrefab,
                bulletLocation.transform.position,
                bulletLocation.transform.rotation
            );

            Rigidbody rb = bullet.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Calculate direction towards the player
                Vector3 direction = (playerTransform.position - bulletLocation.transform.position).normalized;
                rb.linearVelocity = direction * bulletSpeed;
            }

            Destroy(bullet, 5f); // Destroy the bullet after 5 seconds
        }
    }
}
