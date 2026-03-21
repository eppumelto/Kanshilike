using System.Collections.Generic;
using UnityEngine;

public interface IInteractable
{
	string GetName();
	List<InteractionOption> GetAvailableInteractions(SelectableCharacter interactor);
	void ExecuteInteraction(SelectableCharacter interactor, string actionId);
	Vector3 GetInteractionPoint();
	// Returns how many seconds this action takes before ExecuteInteraction is called. 0 = instant.
	// TODO: Feed character skill level into the caller to scale duration.
	float GetInteractionDuration(string actionId) => 0f;
	// Returns the label text shown above the character during a timed action (e.g. "Harvesting...").
	string GetInteractionLabel(string actionId) => string.Empty;
}
