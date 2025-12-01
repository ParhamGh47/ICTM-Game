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
        if (Input.GetKeyDown(KeyCode.H) && Time.time >= nextHonkTime)
        {
            hornSound.Play();
            nextHonkTime = Time.time + cooldownTime;
        }
    }
}
