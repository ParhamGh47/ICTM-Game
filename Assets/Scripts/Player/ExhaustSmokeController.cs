using UnityEngine;

public class ExhaustSmokeController : MonoBehaviour
{
    [Header("References")]
    public CarController car;
    public ParticleSystem smoke;

    [Header("Particle Settings")]
    public int idleMaxParticles = 10;
    public int accelMaxParticles = 50;
    public int heavyLoadMaxParticles = 60;

    [Header("Speed Settings")]
    public float idleSpeed = 0.5f;
    public float accelSpeed = 1.2f;
    public float heavyLoadSpeed = 1.8f;

    private ParticleSystem.MainModule main;

    void Start()
    {
        main = smoke.main;
    }

    void Update()
    {
        float throttle = car.throttleInput;
        float speed = car.currentSpeedKPH;

        int targetMaxParticles;
        float targetSpeed;

        if (Mathf.Abs(throttle) < 0.05f && speed < 2f)
        {
            targetMaxParticles = idleMaxParticles;
            targetSpeed = idleSpeed;
        }
        else if (throttle > 0.05f)
        {

            targetMaxParticles = Mathf.RoundToInt(
                Mathf.Lerp(idleMaxParticles, accelMaxParticles, throttle)
            );
            targetSpeed = Mathf.Lerp(idleSpeed, accelSpeed, throttle);

            if (speed < 20f && throttle > 0.6f)
            {
                targetMaxParticles = heavyLoadMaxParticles;
                targetSpeed = heavyLoadSpeed;
            }
        }
        else
        {
            targetMaxParticles = idleMaxParticles;
            targetSpeed = idleSpeed;
        }

        main.maxParticles = targetMaxParticles;
        main.startSpeed = targetSpeed;
    }
}
