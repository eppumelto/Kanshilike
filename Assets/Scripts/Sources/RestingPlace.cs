using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RestingPlace : MonoBehaviour, IInteractable
{
	public string GetName() => "Bed";
	public List<InteractionOption> GetAvailableInteractions(SelectableCharacter interactor)
	{
		List<InteractionOption> options = new List<InteractionOption>();

		options.Add(new InteractionOption("sleep", "Sleep"));
		return options;
	}

	public void ExecuteInteraction(SelectableCharacter interactor, string actionId)
	{
		switch (actionId)
		{
			case "sleep":
				interactor.Sleep();
				break;
		}
	}

	public Vector3 GetInteractionPoint() => transform.position;
}
