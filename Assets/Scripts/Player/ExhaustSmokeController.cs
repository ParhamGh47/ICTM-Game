using UnityEngine;

public class ExhaustSmokeController : MonoBehaviour
{
    [Header("References")]
    public CarController car;
    public ParticleSystem smoke;

    [Header("Particle Settings")]
    public int idleMaxParticles = 10;
    public int accelMaxParticles = 7;
    public int heavyLoadMaxParticles = 15;

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
            main.startSize = 3;
        }
        else if (throttle > 0.05f)
        {

            targetMaxParticles = Mathf.RoundToInt(
                Mathf.Lerp(idleMaxParticles, accelMaxParticles, throttle)
            );
            targetSpeed = Mathf.Lerp(idleSpeed, accelSpeed, throttle);

            main.startSize = 2.5f;

            // Working hard: either held at full throttle from a standstill, or - in a truck the player is
            // changing gear themselves - left in a gear too tall for the speed it is at. Both are the engine
            // straining below its torque, and both smoke the same way. The automatic box has already picked a
            // sensible gear, so Lugging never fires there and its exhaust is exactly as it was.
            if (car.Lugging || (speed < 20f && throttle > 0.6f))
            {
                targetMaxParticles = heavyLoadMaxParticles;
                targetSpeed = heavyLoadSpeed;
            }
        }
        else
        {
            targetMaxParticles = idleMaxParticles;
            targetSpeed = idleSpeed;
            main.startSize = 3;
        }

        main.maxParticles = targetMaxParticles;
        main.startSpeed = targetSpeed;
    }
}
