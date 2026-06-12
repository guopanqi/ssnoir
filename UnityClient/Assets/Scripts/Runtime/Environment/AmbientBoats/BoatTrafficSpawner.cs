using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns ambient boats inside one or more rectangular river zones.
/// Each zone's local +Z axis is the sailing direction.
/// </summary>
public sealed class BoatTrafficSpawner : MonoBehaviour
{
    [System.Serializable]
    public sealed class SpawnZone
    {
        [Tooltip("Create an empty GameObject over a river segment. Its local +Z axis is the sailing direction.")]
        public Transform anchor;

        [Tooltip("Rectangle size in the zone's local X/Z plane.")]
        public Vector2 size = new Vector2(4f, 12f);

        [Tooltip("Vertical offset above the anchor.")]
        public float heightOffset = 0f;

        [Tooltip("Optional probability weight. Larger values spawn more boats in this zone.")]
        [Min(0f)]
        public float weight = 1f;

        public SpawnZone()
        {
            size = new Vector2(4f, 12f);
            heightOffset = 0f;
            weight = 1f;
        }
    }

    [Header("Boat Prefabs")]
    [SerializeField] private AmbientBoat[] boatPrefabs;

    [Header("River Zones")]
    [SerializeField] private SpawnZone[] spawnZones;

    [Header("Traffic")]
    [SerializeField, Min(0)] private int initialBoatCount = 4;
    [SerializeField, Min(1)] private int maxAliveBoats = 12;
    [SerializeField, Min(0.05f)] private float minSpawnInterval = 1.5f;
    [SerializeField, Min(0.05f)] private float maxSpawnInterval = 4.5f;

    [Header("Boat Lifetime")]
    [SerializeField] private Vector2 speedRange = new Vector2(0.7f, 1.5f);
    [SerializeField] private Vector2 lifetimeRange = new Vector2(14f, 28f);
    [SerializeField, Min(0f)] private float fadeInDuration = 2.2f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 3.5f;

    [Header("Variation")]
    [Tooltip("Small random yaw variation around the zone direction.")]
    [SerializeField, Range(0f, 20f)] private float yawVariation = 4f;

    private readonly HashSet<AmbientBoat> aliveBoats = new HashSet<AmbientBoat>();
    private Coroutine spawnLoop;

    private void OnEnable()
    {
        spawnLoop = StartCoroutine(SpawnLoop());
    }

    private void Start()
    {
        for (int i = 0; i < initialBoatCount && aliveBoats.Count < maxAliveBoats; i++)
        {
            SpawnOne();
        }
    }

    private void OnDisable()
    {
        if (spawnLoop != null)
        {
            StopCoroutine(spawnLoop);
            spawnLoop = null;
        }
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            float min = Mathf.Min(minSpawnInterval, maxSpawnInterval);
            float max = Mathf.Max(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(Random.Range(min, max));

            aliveBoats.RemoveWhere(boat => boat == null);

            if (aliveBoats.Count < maxAliveBoats)
            {
                SpawnOne();
            }
        }
    }

    [ContextMenu("Spawn One Boat")]
    public void SpawnOne()
    {
        AmbientBoat prefab = PickBoatPrefab();
        SpawnZone zone = PickZone();

        if (prefab == null || zone == null || zone.anchor == null)
        {
            Debug.LogWarning("BoatTrafficSpawner: assign at least one boat prefab and one valid spawn zone.", this);
            return;
        }

        Vector3 localPosition = new Vector3(
            Random.Range(-zone.size.x * 0.5f, zone.size.x * 0.5f),
            zone.heightOffset,
            Random.Range(-zone.size.y * 0.5f, zone.size.y * 0.5f)
        );

        Vector3 worldPosition = zone.anchor.TransformPoint(localPosition);
        float yaw = Random.Range(-yawVariation, yawVariation);
        Quaternion worldRotation = zone.anchor.rotation * Quaternion.Euler(0f, yaw, 0f);

        AmbientBoat boat = Instantiate(prefab, worldPosition, worldRotation);
        float speed = Random.Range(Mathf.Min(speedRange.x, speedRange.y), Mathf.Max(speedRange.x, speedRange.y));
        float lifetime = Random.Range(Mathf.Min(lifetimeRange.x, lifetimeRange.y), Mathf.Max(lifetimeRange.x, lifetimeRange.y));

        boat.Initialize(
            worldRotation * Vector3.forward,
            speed,
            lifetime,
            fadeInDuration,
            fadeOutDuration,
            OnBoatFinished
        );

        aliveBoats.Add(boat);
    }

    private AmbientBoat PickBoatPrefab()
    {
        if (boatPrefabs == null || boatPrefabs.Length == 0)
        {
            return null;
        }

        return boatPrefabs[Random.Range(0, boatPrefabs.Length)];
    }

    private SpawnZone PickZone()
    {
        if (spawnZones == null || spawnZones.Length == 0)
        {
            return null;
        }

        float totalWeight = 0f;

        foreach (SpawnZone zone in spawnZones)
        {
            if (zone != null && zone.anchor != null)
            {
                totalWeight += Mathf.Max(0f, zone.weight);
            }
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float choice = Random.value * totalWeight;

        foreach (SpawnZone zone in spawnZones)
        {
            if (zone == null || zone.anchor == null)
            {
                continue;
            }

            choice -= Mathf.Max(0f, zone.weight);

            if (choice <= 0f)
            {
                return zone;
            }
        }

        return spawnZones[spawnZones.Length - 1];
    }

    private void OnBoatFinished(AmbientBoat boat)
    {
        aliveBoats.Remove(boat);
    }

    private void OnDrawGizmosSelected()
    {
        if (spawnZones == null)
        {
            return;
        }

        foreach (SpawnZone zone in spawnZones)
        {
            if (zone == null || zone.anchor == null)
            {
                continue;
            }

            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = zone.anchor.localToWorldMatrix;

            Gizmos.DrawWireCube(
                new Vector3(0f, zone.heightOffset, 0f),
                new Vector3(zone.size.x, 0.05f, zone.size.y)
            );

            Gizmos.DrawLine(
                new Vector3(0f, zone.heightOffset, 0f),
                new Vector3(0f, zone.heightOffset, zone.size.y * 0.65f)
            );

            Gizmos.matrix = oldMatrix;
        }
    }
}
