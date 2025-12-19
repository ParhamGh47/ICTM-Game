using UnityEngine;
using System.Collections;

public class BoostManager : MonoBehaviour
{
    [Header("Boost Particles")]
    public ParticleSystem particleA;
    public ParticleSystem particleB;
    public ParticleSystem particleC;
    public ParticleSystem particleD;

    [Header("Settings")]
    public float boostDuration = 1.5f;

    private Coroutine boostRoutine;
    private CameraController cameraController;

    private void Start()
    {
        cameraController = FindObjectOfType<CameraController>();
    }

    public void PlayBoost()
    {
        if (boostRoutine != null)
            StopCoroutine(boostRoutine);

        boostRoutine = StartCoroutine(BoostCoroutine());

        if (cameraController != null)
            cameraController.TriggerBoostCamera();
    }

    private IEnumerator BoostCoroutine()
    {
        SetParticles(true);

        yield return new WaitForSeconds(boostDuration);

        SetParticles(false);
        boostRoutine = null;
    }

    private void SetParticles(bool state)
    {
        HandleParticle(particleA, state);
        HandleParticle(particleB, state);
        HandleParticle(particleC, state);
        HandleParticle(particleD, state);
    }

    private void HandleParticle(ParticleSystem ps, bool enable)
    {
        if (ps == null)
            return;

        if (enable)
            ps.Play();
        else
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
