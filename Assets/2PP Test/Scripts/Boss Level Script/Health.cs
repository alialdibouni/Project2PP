using UnityEngine;

public class Health : MonoBehaviour
{
    public float health = 100f; // Default health value
    public float recoveryRate = 10f; // Health per second
    public float recoveryDelay = 2f; // Seconds to wait before starting recovery

    private float lastDamageTime = -Mathf.Infinity;
    private float lastHealth;

    void Start()
    {
        lastHealth = health;
    }

    // Clamp health between 0 and 100
    public void SetHealth(float value)
    {
        health = Mathf.Clamp(value, 0f, 100f);
    }

    void Update()
    {
        health = Mathf.Clamp(health, 0f, 100f);

        // Detect damage
        if (health < lastHealth)
        {
            lastDamageTime = Time.time;
        }

        // Only recover if enough time has passed since last damage, health is not full, and health is above 0
        if (Time.time - lastDamageTime > recoveryDelay && health < 100f && health > 0f)
        {
            health += recoveryRate * Time.deltaTime;
            health = Mathf.Clamp(health, 0f, 100f);
        }

        if (health <= 0)
        {
            Debug.Log("Player has died");
        }

        lastHealth = health;
    }
}
