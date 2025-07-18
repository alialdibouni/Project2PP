using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    public float maxHealth = 100f;
    public float health = 100f;
    public float recoveryRate = 10f;
    public float recoveryDelay = 2f;

    private float lastDamageTime = -Mathf.Infinity;
    private float lastHealth;

    public Image healthBar;

    private PauseMenu pauseMenu; // Reference to PauseMenu

    void Start()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);
        lastHealth = health;
        pauseMenu = Object.FindFirstObjectByType<PauseMenu>(); // Find PauseMenu in the scene
    } 

    

    public void SetHealth(float value)
    {
        health = Mathf.Clamp(value, 0f, maxHealth);
    }

    void Update()
    {
        health = Mathf.Clamp(health, 0f, maxHealth);

        if (health < lastHealth)
        {
            healthBar.fillAmount = health / maxHealth;
            lastDamageTime = Time.time;
        }

        if (Time.time - lastDamageTime > recoveryDelay && health < maxHealth && health > 0f)
        {
            health += recoveryRate * Time.deltaTime;
            health = Mathf.Clamp(health, 0f, maxHealth);
            healthBar.fillAmount = health / maxHealth;
        }

        if (health <= 0)
        {
            Debug.Log("Player has died");
            if (pauseMenu != null)
            {
                pauseMenu.ShowDeathScreen();
            }
        }

        lastHealth = health;
    }
}
