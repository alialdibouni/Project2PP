using UnityEngine;
using UnityEngine.UI;

public class BossHealth : MonoBehaviour
{
    public float maxHealth = 500f;
    public float health = 500f;

    public Image healthBar;

    private float lastHealth;

    public BulletProjectile bulletProjectile;

    private float originalBulletSpeed;
    private float originalReloadTime;

    [SerializeField] private int enrageState = 0;

    private PauseMenu pauseMenu; // Reference to PauseMenu

    void Start()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
        lastHealth = health;
        if (healthBar != null)
            healthBar.fillAmount = health / maxHealth;

        if (bulletProjectile == null)
            bulletProjectile = GetComponent<BulletProjectile>();

        if (bulletProjectile != null)
        {
            originalBulletSpeed = bulletProjectile.bulletSpeed;
            originalReloadTime = bulletProjectile.reloadTime;
        }

        pauseMenu = Object.FindFirstObjectByType<PauseMenu>(); ; // Find PauseMenu in the scene
    }

    

    public void SetHealth(float value)
    {
        health = Mathf.Clamp(value, 0f, maxHealth);
        if (healthBar != null)
            healthBar.fillAmount = health / maxHealth;
    }

    void Update()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);

        if (health != lastHealth && healthBar != null)
        {
            healthBar.fillAmount = health / maxHealth;
        }

        if (bulletProjectile != null)
        {
            if (health < 0.25f * maxHealth && enrageState != 3)
            {
                bulletProjectile.bulletSpeed = originalBulletSpeed * 1.75f;
                bulletProjectile.reloadTime = originalReloadTime * 0.25f;
                enrageState = 3;
            }
            else if (health < 0.5f * maxHealth && enrageState != 2 && health >= 0.25f * maxHealth)
            {
                bulletProjectile.bulletSpeed = originalBulletSpeed * 1.5f;
                bulletProjectile.reloadTime = originalReloadTime * 0.5f;
                enrageState = 2;
            }
            else if (health < 0.75f * maxHealth && enrageState != 1 && health >= 0.5f * maxHealth)
            {
                bulletProjectile.bulletSpeed = originalBulletSpeed * 1.25f;
                bulletProjectile.reloadTime = originalReloadTime * 0.75f;
                enrageState = 1;
            }
            else if (health >= 0.75f * maxHealth && enrageState != 0)
            {
                bulletProjectile.bulletSpeed = originalBulletSpeed;
                bulletProjectile.reloadTime = originalReloadTime;
                enrageState = 0;
            }
        }

        if (health <= 0)
        {
            Debug.Log("Boss has died");
            if (pauseMenu != null)
            {
                pauseMenu.ShowVictoryScreen();
            }
        }

        lastHealth = health;
    }
}
