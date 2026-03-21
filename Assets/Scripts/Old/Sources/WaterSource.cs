using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterSource : MonoBehaviour, IInteractable
{
	public string GetName() => "Well";
	public float amount = 10f;
	public List<InteractionOption> GetAvailableInteractions(SelectableCharacter interactor)
	{
		List<InteractionOption> options = new List<InteractionOption>();

		options.Add(new InteractionOption("drink_water", "Drink Water"));
		return options;
	}

	public void ExecuteInteraction(SelectableCharacter interactor, string actionId)
	{
		switch (actionId)
		{
			case "drink_water":
				interactor.Drink(amount);
				break;
		}
	}

	public Vector3 GetInteractionPoint() => transform.position;
}
