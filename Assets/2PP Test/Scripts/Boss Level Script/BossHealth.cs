using UnityEngine;

public class BossHealth : MonoBehaviour
{
    public float maxHealth = 500f; // Maximum health, editable in Inspector
    public float health = 500f;    // Current health, editable in Inspector

    // Clamp health between 0 and maxHealth
    public void SetHealth(float value)
    {
        health = Mathf.Clamp(value, 0f, maxHealth);
    }

    void Start()
    {
        // Ensure health does not exceed maxHealth at start
        health = Mathf.Clamp(health, 0f, maxHealth);
    }

    void Update()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);

        if (health <= 0)
        {
            Debug.Log("Boss has died");
        }
    }
}
