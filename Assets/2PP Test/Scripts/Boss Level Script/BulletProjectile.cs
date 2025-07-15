using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    public GameObject bulletPrefab; // Assign the bullet prefab in the Inspector
    public GameObject bulletLocation; // Assign the bullet spawn location in the Inspector
    public float bulletSpeed = 20f; // Speed at which the bullet is fired
    public float reloadTime = 5f;
    public float bulletLifetime = 5f; // Time after which the bullet is destroyed
    public float waitForSeconds = 5f; // Time to wait before starting the bullet routine

    private Transform playerTransform;
    private TargetPlayer targetPlayer; // Reference to TargetPlayer

    [Header("Raycast Ignore Settings")]
    public string[] ignoredTags;

    void Start()
    {
        // Find the player by tag. Make sure your player GameObject is tagged as "Player"
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // Find the TargetPlayer script in the scene
        targetPlayer = Object.FindFirstObjectByType<TargetPlayer>();

        // Wait 5 seconds before starting the bullet routine
        StartCoroutine(DelayedStart());
    }

    System.Collections.IEnumerator DelayedStart()
    {
        yield return new WaitForSeconds(waitForSeconds);
        StartCoroutine(SpawnBulletRoutine());
    }

    System.Collections.IEnumerator SpawnBulletRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(reloadTime);

            // Only shoot if the player is in view
            if (targetPlayer != null && targetPlayer.IsPlayerInView)
            {
                SpawnBullet();
            }
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
            Collider bulletCollider = bullet.GetComponent<Collider>();
            Collider spawnCollider = bulletLocation.GetComponent<Collider>();

            if (bulletCollider != null && spawnCollider != null)
            {
                Physics.IgnoreCollision(bulletCollider, spawnCollider);
            }

            if (rb != null)
            {
                Vector3 direction = (playerTransform.position - bulletLocation.transform.position).normalized;
                rb.linearVelocity = direction * bulletSpeed;
            }

            // Ignore collisions with all colliders with ignored tags
            if (bulletCollider != null && ignoredTags != null && ignoredTags.Length > 0)
            {
                foreach (string tag in ignoredTags)
                {
                    GameObject[] ignoredObjects = GameObject.FindGameObjectsWithTag(tag);
                    foreach (GameObject obj in ignoredObjects)
                    {
                        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
                        foreach (Collider col in colliders)
                        {
                            Physics.IgnoreCollision(bulletCollider, col);
                        }
                    }
                }
            }
            Destroy(bullet, bulletLifetime);
        }
    }
}
