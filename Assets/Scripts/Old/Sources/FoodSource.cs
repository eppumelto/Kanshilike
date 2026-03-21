using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Progress;

public class FoodSource : MonoBehaviour, IInteractable
{
	public string GetName() => "Blueberries";
	//how much filling you get
	public float Nutrition = 50f;
	//how much food can be collected
	public int amount;

	void Start()
	{
		amount = Random.Range(1, 5);
	}

	public List<InteractionOption> GetAvailableInteractions(SelectableCharacter interactor)
	{
		List<InteractionOption> options = new List<InteractionOption>();

		options.Add(new InteractionOption("eat_berry", "Eat"));
		if (amount == 0)
		{
			options.Clear();
		}

		return options;
	}

	public void ExecuteInteraction(SelectableCharacter interactor, string actionId)
	{
		switch (actionId)
		{
			case "eat_berry":
				interactor.Eat(Nutrition);
				amount = amount - 1;
				break;
		}
	}

	public Vector3 GetInteractionPoint() => transform.position;
}
