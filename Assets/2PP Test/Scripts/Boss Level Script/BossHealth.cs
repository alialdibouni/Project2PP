using UnityEngine;
using UnityEngine.UI;

public class BossHealth : MonoBehaviour
{
    public float maxHealth = 500f; // Maximum health, editable in Inspector
    public float health = 500f;    // Current health, editable in Inspector

    public Image healthBar; // Reference to the health bar UI element

    private float lastHealth;

    void Start()
    {
        // Ensure health does not exceed maxHealth at start
        health = Mathf.Clamp(health, 0f, maxHealth);
        lastHealth = health;
        if (healthBar != null)
            healthBar.fillAmount = health / maxHealth;
    }

    // Clamp health between 0 and maxHealth
    public void SetHealth(float value)
    {
        health = Mathf.Clamp(value, 0f, maxHealth);
        if (healthBar != null)
            healthBar.fillAmount = health / maxHealth;
    }

    void Update()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);

        // Update health bar if health changed
        if (health != lastHealth && healthBar != null)
        {
            healthBar.fillAmount = health / maxHealth;
        }

        if (health <= 0)
        {
            Debug.Log("Boss has died");
        }

        lastHealth = health;
    }
}
