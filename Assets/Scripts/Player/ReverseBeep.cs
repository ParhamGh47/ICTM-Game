using UnityEngine;
using System.Collections;

public class ReverseBeep : MonoBehaviour
{
    public AudioSource reverseBeep;

    private void Start()
    {
        reverseBeep = GetComponent<AudioSource>();
    }

    public void SoundReverse()
    {
        if (!reverseBeep.isPlaying)
        {
            StartCoroutine(PlayWithDelay(0.5f));
        }
    }

    private IEnumerator PlayWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        reverseBeep.Play();
    }

    public void StopReverse()
    {
        if (reverseBeep.isPlaying)
        {
            reverseBeep.Stop();
        }
    }
}
