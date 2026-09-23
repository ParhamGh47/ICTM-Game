using UnityEngine;

public class CarHorn : MonoBehaviour
{
    private AudioSource hornSound;
    private float cooldownTime = 3f;
    private float nextHonkTime = 0f;

    void Start()
    {
        hornSound = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (PauseTracker.Instance != null && PauseTracker.Instance.isPaused)
            return;

        // H on the keyboard, X / square on a gamepad.
        if (GameInput.HornPressed() && Time.time >= nextHonkTime)
        {
            hornSound.Play();
            nextHonkTime = Time.time + cooldownTime;

            // Tell the traffic. The passing cars cannot reach this script - they are in their own
            // assembly - so the horn reports itself to them and each car decides whether the player
            // was behind it and close enough to be worth pulling over for.
            AICarController.ReportHorn(transform);
        }
    }
}