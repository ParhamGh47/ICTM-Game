using UnityEngine;

public class SpeedEffect : MonoBehaviour
{
    [Header("References")]
    public CarController car;
    public ParticleSystem leftEffect;
    public ParticleSystem rightEffect;

    [Header("Speed Settings")]
    public float minSpeed = 30f;
    public float maxSpeed = 150f;

    [Header("Particle Settings")]
    public float maxParticles = 60f;

    private ParticleSystem.EmissionModule leftEm;
    private ParticleSystem.EmissionModule rightEm;

    void Start()
    {
        if (leftEffect != null)
            leftEm = leftEffect.emission;

        if (rightEffect != null)
            rightEm = rightEffect.emission;
    }

    void Update()
    {
        if (car == null) return;

        if (car.throttleInput < 0f)
        {
            SetEmission(0f);
            return;
        }

        float speed = car.currentSpeedKPH;

        float t = Mathf.InverseLerp(minSpeed, maxSpeed, speed);
        float emissionRate = Mathf.Lerp(0f, maxParticles, t);

        SetEmission(emissionRate);
    }

    private void SetEmission(float value)
    {
        if (leftEffect != null)
        {
            var em = leftEm;
            em.rateOverTime = value;
        }

        if (rightEffect != null)
        {
            var em = rightEm;
            em.rateOverTime = value;
        }
    }
}
