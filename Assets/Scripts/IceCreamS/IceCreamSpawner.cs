using UnityEngine;

public class IceCreamSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject[] particlePrefabs;

    [Header("Spawn Area")]
    public Transform spawnArea;

    public Vector2 spawnSize = new Vector2(4f, 4f);

    [Header("Level Timing")]
    public float levelDuration = 160f;

    private float elapsedTime;
    private float spawnTimer;

    void Update()
    {
        elapsedTime += Time.deltaTime;

        float currentInterval = GetSpawnInterval(elapsedTime);

        if (currentInterval <= 0f)
            return;

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= currentInterval)
        {
            SpawnParticle();
            spawnTimer = 0f;
        }
    }

    float GetSpawnInterval(float time)
    {
        if (time < 10f) return -1f;

        if (time < 45f) return 0.2f;
        if (time < 70f) return 0.08f;
        if (time < 90f) return 0.04f;
        if (time < 105f) return 0.01f;
        if (time < 120f) return 0.005f;
        if (time < 145f) return 0.002f;

        return 0.02f;
    }

    void SpawnParticle()
    {
        if (particlePrefabs.Length == 0 || spawnArea == null) return;

        GameObject prefab =
            particlePrefabs[Random.Range(0, particlePrefabs.Length)];

        float x = Random.Range(-spawnSize.x * 0.5f, spawnSize.x * 0.5f);
        float z = Random.Range(-spawnSize.y * 0.5f, spawnSize.y * 0.5f);

        Vector3 spawnPos = spawnArea.position + new Vector3(x, 0f, z);

        Instantiate(prefab, spawnPos, Quaternion.identity);
    }

    void OnDrawGizmosSelected()
    {
        if (spawnArea == null) return;

        Gizmos.color = Color.cyan;

        Vector3 center = spawnArea.position;
        float halfX = spawnSize.x * 0.5f;
        float halfZ = spawnSize.y * 0.5f;

        Vector3 p1 = center + new Vector3(-halfX, 0f, -halfZ);
        Vector3 p2 = center + new Vector3(-halfX, 0f,  halfZ);
        Vector3 p3 = center + new Vector3( halfX, 0f,  halfZ);
        Vector3 p4 = center + new Vector3( halfX, 0f, -halfZ);

        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);
    }
}
