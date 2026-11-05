using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// Spawns groups of one animal type on the NavMesh when the scene starts. Each group is either
/// a single animal or a pack or herd that stays together and shares one personality
/// (see AnimalGroup). Use one spawner per animal type.
/// </summary>
public class AnimalSpawner : MonoBehaviour
{
    private const int MaxPlacementAttempts = 30;

    [FormerlySerializedAs("wolfPrefab")]
    [SerializeField] private GameObject animalPrefab;

    [Tooltip("One is picked at random per group. Leave empty to use the prefab's personality.")]
    [SerializeField] private AnimalPersonality[] personalities;

    [Header("Groups")]
    [SerializeField, Min(1)] private int groupCount = 3;
    [Tooltip("Chance that a group is a single animal. 1 = always solitary.")]
    [FormerlySerializedAs("loneWolfChance")]
    [SerializeField, Range(0f, 1f)] private float loneChance = 0.3f;
    [FormerlySerializedAs("packSizeMin")]
    [SerializeField, Min(2)] private int groupSizeMin = 3;
    [FormerlySerializedAs("packSizeMax")]
    [SerializeField, Min(2)] private int groupSizeMax = 5;

    [Header("Placement")]
    [Tooltip("Groups spawn within this distance of the spawner.")]
    [SerializeField, Min(0f)] private float spawnRadius = 80f;
    [SerializeField, Min(0f)] private float minDistanceBetweenGroups = 40f;
    [Tooltip("Keeps animals from spawning right on top of player characters.")]
    [SerializeField, Min(0f)] private float minDistanceFromCharacters = 30f;
    [Tooltip("How far group members start from the group's centre, and how far they spread out around their shared waypoint.")]
    [SerializeField, Min(0.5f)] private float memberSpacing = 4f;

    [Header("Roaming")]
    [Tooltip("Seconds before a group picks a new place to roam to.")]
    [SerializeField, Min(1f)] private float waypointInterval = 20f;

    private readonly List<Vector3> groupCentres = new List<Vector3>();

    private void Start()
    {
        if (animalPrefab == null)
        {
            Debug.LogWarning($"[AnimalSpawner] '{name}' has no animal prefab assigned.", this);
            return;
        }

        SelectableCharacter[] characters = FindObjectsByType<SelectableCharacter>(FindObjectsSortMode.None);

        for (int i = 0; i < groupCount; i++)
        {
            if (TryFindGroupCentre(characters, out Vector3 centre))
                SpawnGroup(centre);
            else
                Debug.LogWarning($"[AnimalSpawner] No valid spot for group {i + 1} of {groupCount}. Try a larger spawn radius or smaller minimum distances.", this);
        }
    }

    private bool TryFindGroupCentre(SelectableCharacter[] characters, out Vector3 centre)
    {
        for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, memberSpacing * 2f, NavMesh.AllAreas)
                && IsFarEnoughFromOthers(hit.position, characters))
            {
                centre = hit.position;
                return true;
            }
        }

        centre = default;
        return false;
    }

    private bool IsFarEnoughFromOthers(Vector3 position, SelectableCharacter[] characters)
    {
        foreach (Vector3 other in groupCentres)
        {
            if (Vector3.Distance(position, other) < minDistanceBetweenGroups)
                return false;
        }

        foreach (SelectableCharacter character in characters)
        {
            if (Vector3.Distance(position, character.transform.position) < minDistanceFromCharacters)
                return false;
        }

        return true;
    }

    private void SpawnGroup(Vector3 centre)
    {
        groupCentres.Add(centre);

        bool lone = Random.value < loneChance;
        int size = lone ? 1 : Random.Range(groupSizeMin, Mathf.Max(groupSizeMin, groupSizeMax) + 1);

        AnimalPersonality personality = personalities != null && personalities.Length > 0
            ? personalities[Random.Range(0, personalities.Length)]
            : null;

        var group = new AnimalGroup(centre, personality, memberSpacing, waypointInterval);

        for (int i = 0; i < size; i++)
        {
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject animal = Instantiate(animalPrefab, GetMemberPosition(centre, i), rotation, transform);
            animal.name = lone ? $"Lone {animalPrefab.name}" : $"Group {groupCentres.Count} {animalPrefab.name} {i + 1}";

            // WolfAI adds AnimalPredatorAI in OnEnable, which has already run by now.
            if (animal.TryGetComponent(out AnimalPredatorAI predator))
                predator.JoinGroup(group);
            else
                Debug.LogWarning($"[AnimalSpawner] '{animalPrefab.name}' has no AnimalPredatorAI, so it can't join a group.", this);
        }

        string personalityName = personality != null ? personality.name : "prefab personality";
        Debug.Log($"[AnimalSpawner] Spawned {size} {animalPrefab.name} ({personalityName}) at {centre}.", this);
    }

    private Vector3 GetMemberPosition(Vector3 centre, int index)
    {
        if (index == 0)
            return centre;

        Vector2 offset = Random.insideUnitCircle * memberSpacing;
        Vector3 candidate = centre + new Vector3(offset.x, 0f, offset.y);
        return NavMesh.SamplePosition(candidate, out NavMeshHit hit, memberSpacing, NavMesh.AllAreas)
            ? hit.position
            : centre;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
#endif
}
