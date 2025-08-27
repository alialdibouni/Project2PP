using UnityEngine;

public class HealthPack : Interactable
{

    [SerializeField] private float healAmount = 20f; // Amount of health to restore

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    protected override void Interact()
    {
        PlayerHealth playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
        


        if (playerHealth != null)
        {
            //have it so if the health is full, it doesn't do anything
            if (playerHealth.health == playerHealth.maxhealth)
            {
                Debug.Log("Player health is already full.");
                return;
            }
            else
            {
                playerHealth.RestoreHealth(healAmount); // Restore 20 health points
                Debug.Log("Player healed by 20 points.");
                Destroy(gameObject); // Remove the health pack from the scene
            }
        }
        else
        {
            Debug.LogWarning("PlayerHealth component not found in the scene.");
        }
    }
}
