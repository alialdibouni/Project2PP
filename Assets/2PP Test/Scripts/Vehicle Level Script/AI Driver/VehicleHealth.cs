using UnityEngine;

public class VehicleHealth : MonoBehaviour
{

    public float maxHealth = 100f; // Maximum health of the vehicle
    public float currentHealth = 100f; // Current health of the vehicle


    public void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    // Update is called once per frame
    void Update()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }
}
