using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f; // Maximum health, editable in Inspector
    public float health = 100f;    // Current health, editable in Inspector
    public float recoveryRate = 10f; // Health per second
    public float recoveryDelay = 2f; // Seconds to wait before starting recovery

    private float lastDamageTime = -Mathf.Infinity;
    private float lastHealth;

    public Image healthBar; 

    void Start()
    {
        // Ensure health does not exceed maxHealth at start
        health = Mathf.Clamp(health, 0f, maxHealth);
        lastHealth = health;
    }

    // Clamp health between 0 and maxHealth
    public void SetHealth(float value)
    {
        health = Mathf.Clamp(value, 0f, maxHealth);
    }

    void Update()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);

        // Detect damage
        if (health < lastHealth)
        {
            healthBar.fillAmount = health / maxHealth;
            lastDamageTime = Time.time;
        }

        // Only recover if enough time has passed since last damage, health is not full, and health is above 0
        if (Time.time - lastDamageTime > recoveryDelay && health < maxHealth && health > 0f)
        {
            health += recoveryRate * Time.deltaTime;
            health = Mathf.Clamp(health, 0f, maxHealth);
            healthBar.fillAmount = health / maxHealth;
        }

        if (health <= 0)
        {
            Debug.Log("Player has died");
        }

        lastHealth = health;
    }
}
