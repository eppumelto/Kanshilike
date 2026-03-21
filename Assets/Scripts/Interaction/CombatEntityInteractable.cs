using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic combat interaction bridge for living entities (wolves, NPCs, etc.).
/// Attach to an entity so SelectionManager can right-click it and route attacks.
/// </summary>
public class CombatEntityInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string displayName = "Target";
    [SerializeField] private string attackActionId = "attack";
    [SerializeField] private string attackDisplayName = "Attack";

    private CharacterStats stats;

    private void Awake()
    {
        stats = GetComponent<CharacterStats>();
        if (stats == null)
            stats = GetComponentInParent<CharacterStats>();
    }

    public string GetName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
    }

    public List<InteractionOption> GetAvailableInteractions(SelectableCharacter interactor)
    {
        List<InteractionOption> options = new List<InteractionOption>();
        if (stats == null || stats.IsAlive)
            options.Add(new InteractionOption(attackActionId, attackDisplayName));

        return options;
    }

    public void ExecuteInteraction(SelectableCharacter interactor, string actionId)
    {
        if (interactor == null || actionId != attackActionId)
            return;

        interactor.Attack(this);
    }

    public Vector3 GetInteractionPoint()
    {
        return transform.position;
    }

    public float GetInteractionDuration(string actionId)
    {
        return 0f;
    }

    public string GetInteractionLabel(string actionId)
    {
        return string.Empty;
    }
}
