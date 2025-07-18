using UnityEngine;

public class VehicleHealth : MonoBehaviour
{

    public float maxHealth = 100f; // Maximum health of the vehicle
    public float currentHealth = 100f; // Current health of the vehicle

    public ParticleSystem health75Particle;
    public ParticleSystem health50Particle;
    public ParticleSystem health25Particle;

    public ParticleSystem fireParticle1;
    public ParticleSystem fireParticle2;
    public ParticleSystem fireParticle3;

    public void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
        CheckHealthParticles();
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
        CheckHealthParticles();
    }

    private void CheckHealthParticles()
    {
        if (currentHealth <= maxHealth * 0.75f && !health75Particle.isPlaying)
        {
            health75Particle.Play();
        }
        else if (currentHealth > maxHealth * 0.75f && health75Particle.isPlaying)
        {
            health75Particle.Stop();
        }

        if (currentHealth <= maxHealth * 0.50f && !health50Particle.isPlaying)
        {
            health75Particle.Stop();
            health50Particle.Play();
        }
        else if (currentHealth > maxHealth * 0.50f && health50Particle.isPlaying)
        {
            health50Particle.Stop();
        }

        if (currentHealth <= maxHealth * 0.25f && !health25Particle.isPlaying)
        {
            health50Particle.Stop();
            health25Particle.Play();
        }
        else if (currentHealth > maxHealth * 0.25f && health25Particle.isPlaying)
        {
            health25Particle.Stop();
        }

        if (currentHealth <= maxHealth * 0.1f)
        {
            // Play fire particles when health is zero
            if (!fireParticle1.isPlaying) fireParticle1.Play();
            if (!fireParticle2.isPlaying) fireParticle2.Play();
            if (!fireParticle3.isPlaying) fireParticle3.Play();
        }
        else
        {
            // Stop fire particles when health is above zero
            if (fireParticle1.isPlaying) fireParticle1.Stop();
            if (fireParticle2.isPlaying) fireParticle2.Stop();
            if (fireParticle3.isPlaying) fireParticle3.Stop();
        }
    }
}
