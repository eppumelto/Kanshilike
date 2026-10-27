using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Added by CharacterStats when an entity dies. Stops it moving and tips it over onto its side.
/// The body stays in the world; if it has an Inventory (its own, or one CorpseLoot made), it can
/// be looted with a right-click.
/// </summary>
public class Corpse : MonoBehaviour, IInteractable
{
    private const float FallDuration = 0.6f;
    private const string LootActionId = "loot";

    private Inventory Loot => GetComponent<Inventory>();

    public string GetName() => $"{DisplayName()} (dead)";

    public List<InteractionOption> GetAvailableInteractions(SelectableCharacter interactor)
    {
        var options = new List<InteractionOption>();
        if (Loot != null)
            options.Add(new InteractionOption(LootActionId, "Loot"));
        return options;
    }

    public void ExecuteInteraction(SelectableCharacter interactor, string actionId)
    {
        if (actionId == LootActionId && Loot != null)
            InventoryScreen.Instance.OpenLoot(interactor, Loot, GetName());
    }

    public Vector3 GetInteractionPoint() => transform.position;

    private string DisplayName()
    {
        var character = GetComponent<SelectableCharacter>();
        if (character != null && !string.IsNullOrEmpty(character.characterName))
            return character.characterName;
        return gameObject.name;
    }

    private void Start()
    {
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            if (agent.isOnNavMesh)
                agent.ResetPath();
            agent.enabled = false;
        }

        var body = GetComponent<Rigidbody>();
        if (body != null)
            body.isKinematic = true;

        StartCoroutine(FallOver());
    }

    private IEnumerator FallOver()
    {
        Quaternion start = transform.rotation;
        // Roll onto the side around the facing direction, so it lands lying sideways.
        Quaternion end = Quaternion.AngleAxis(90f, transform.forward) * start;

        // Pivot on the ground below the origin instead of spinning in place.
        Bounds bounds = GetWorldBounds();
        Vector3 pivot = new Vector3(transform.position.x, bounds.min.y, transform.position.z);
        Vector3 offset = transform.position - pivot;

        for (float t = 0f; t < FallDuration; t += Time.deltaTime)
        {
            // Ease in, like gravity.
            float k = Mathf.Clamp01(t / FallDuration);
            k *= k;
            Quaternion step = Quaternion.Slerp(start, end, k);
            transform.SetPositionAndRotation(pivot + step * Quaternion.Inverse(start) * offset, step);
            yield return null;
        }

        transform.SetPositionAndRotation(pivot + end * Quaternion.Inverse(start) * offset, end);
    }

    private Bounds GetWorldBounds()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
