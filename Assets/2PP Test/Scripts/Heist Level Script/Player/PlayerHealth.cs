using UnityEngine;
using UnityEngine.UI;
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Bar")]
    public float health;
    private float lerpTimer;
    public float maxhealth = 100f;
    public float chipSpeed = 2f;
    public Image frontHealthBar;
    public Image backHealthBar;

    [Header("Damage Overlay")]
    public Image overlay; //damage overlay image
    public float duration; //duration of the overlay that stays fully opaque
    public float fadeSpeed; //speed at which the overlay fades

    private float durationTimer; //timer to track the duration of the overlay

    [Header("Regeneration")]
    public float regenDelay = 3f; // seconds after taking damage before regen starts
    public float regenRate = 10f; // health per second
    private float timeSinceLastDamage;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        health = maxhealth;
        overlay.color = new Color(overlay.color.r, overlay.color.g, overlay.color.b, 0);
        timeSinceLastDamage = regenDelay; // allow regen if ever needed and not recently damaged
    }

    // Update is called once per frame
    void Update()
    {
        health = Mathf.Clamp(health, 0, maxhealth);

        // Track time since last damage for regen
        timeSinceLastDamage += Time.deltaTime;

        // Passive regeneration after delay
        if (health > 0f && health < maxhealth && timeSinceLastDamage >= regenDelay)
        {
            health += regenRate * Time.deltaTime;
            if (health > maxhealth) health = maxhealth;
        }

        UpdateHealthUI();

        if (overlay.color.a > 0)
        {
            if (health <= 30)
                return;
            durationTimer += Time.deltaTime;
            if (durationTimer > duration)
            {
                //fade the image
                float tempAlpha = overlay.color.a;
                tempAlpha -= fadeSpeed * Time.deltaTime;
                overlay.color = new Color(overlay.color.r, overlay.color.g, overlay.color.b, tempAlpha);
            }
        }
    }

    public void UpdateHealthUI()
    {
        //Debug.Log(health);
        float fillF = frontHealthBar.fillAmount;
        float fillB = backHealthBar.fillAmount;
        float hFraction = health / maxhealth;
        if (fillB > hFraction)
        {
            frontHealthBar.fillAmount = hFraction;
            backHealthBar.color = Color.red;
            lerpTimer += Time.deltaTime;
            float percentComplete = lerpTimer / chipSpeed;
            percentComplete = percentComplete * percentComplete;
            backHealthBar.fillAmount = Mathf.Lerp(fillB, hFraction, percentComplete);
        }
        if (fillF < hFraction)
        {
            backHealthBar.color = Color.green;
            backHealthBar.fillAmount = hFraction;
            lerpTimer += Time.deltaTime;
            float percentComplete = lerpTimer / chipSpeed;
            percentComplete = percentComplete * percentComplete;
            frontHealthBar.fillAmount = Mathf.Lerp(fillF, backHealthBar.fillAmount, percentComplete);
        }
    }

    public void TakeDamage(float damage)
    {
        health -= damage;
        lerpTimer = 0f;
        durationTimer = 0f;
        overlay.color = new Color(overlay.color.r, overlay.color.g, overlay.color.b, 1);
        timeSinceLastDamage = 0f; // reset regen timer
    }

    public void RestoreHealth(float healAmount)
    {
        health += healAmount;
        lerpTimer = 0f;
    }
}
