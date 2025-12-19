using UnityEngine;

public class BonesSound : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] boneClips;

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        if (audioSource == null || boneClips == null || boneClips.Length == 0)
            return;

        int randomIndex = Random.Range(0, boneClips.Length);
        audioSource.PlayOneShot(boneClips[randomIndex]);
    }
}
