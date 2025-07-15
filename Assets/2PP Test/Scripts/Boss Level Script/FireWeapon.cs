using UnityEngine;

public class FireWeapon : MonoBehaviour
{
    [Header("Bullet Settings")]
    public GameObject bulletPrefab; // Assign the bullet prefab in the Inspector
    public GameObject bulletLocation; // Assign the bullet spawn location in the Inspector
    public float bulletSpeed = 20f; // Speed at which the bullet is fired
    public float reloadTime = 0.5f; // Minimum time between shots
    public float bulletLifetime = 5f; // Time after which the bullet is destroyed
    public string[] ignoredTags; // Tags to ignore for bullet collision

    private float lastFireTime = 0f;
    private PlayerBossMovement playerBossMovement; // Reference to player movement script

    void Start()
    {
        playerBossMovement = GetComponent<PlayerBossMovement>();
    }

    void Update()
    {
        // Only allow firing in battle mode
        if (playerBossMovement != null && playerBossMovement.isBattleMode)
        {
            // Fire when Spacebar is pressed or held, but respect reloadTime
            if (Input.GetKey(KeyCode.Space) && Time.time - lastFireTime >= reloadTime)
            {
                SpawnBullet();
                lastFireTime = Time.time;
            }
        }
    }

    void SpawnBullet()
    {
        if (bulletPrefab != null && bulletLocation != null && playerBossMovement != null && playerBossMovement.enemyTarget != null)
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
                // Fire towards the enemyTarget
                Vector3 direction = (playerBossMovement.enemyTarget.position - bulletLocation.transform.position).normalized;
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
