using System.Collections.Generic;
using UnityEngine;

public interface IInteractable
{
	string GetName();
	List<InteractionOption> GetAvailableInteractions(SelectableCharacter interactor);
	void ExecuteInteraction(SelectableCharacter interactor, string actionId);
	Vector3 GetInteractionPoint();
}
