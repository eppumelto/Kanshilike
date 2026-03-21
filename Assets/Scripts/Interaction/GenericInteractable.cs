using System.Collections.Generic;
using UnityEngine;

public enum InteractionActionType
{
	Drink,
	Eat,
	Sleep,
	Collect,
	Attack,
	Harvest
}

[System.Serializable]
public class InteractionEntry
{
	public string id;
	public string displayName;
	public InteractionActionType actionType;

	[Tooltip("Value passed to Drink (amount) or Eat (nutrition). Unused for Sleep/Collect/Attack.")]
	public float value;

	[Tooltip("Max uses before this interaction disappears. 0 = unlimited.")]
	public int maxUses = 0;

	[Tooltip("Randomize uses at Start between randomMin and randomMax (inclusive). Only used when both are > 0.")]
	public int randomMin = 0;
	public int randomMax = 0;

	[Tooltip("Item added to inventory when actionType is Collect or Harvest.")]
	public Item item;

	[Tooltip("How many seconds the Harvest action takes (before skill speed multipliers are applied).")]
	public float harvestDuration = 3f;

	[Tooltip("Text shown above the character while this timed action is in progress (e.g. 'Harvesting...', 'Drinking...').")]
	public string progressLabel = "Working...";

	[Tooltip("Optional: GameObject instantiated in place of this object when Harvest uses run out (e.g. a depleted bush model).")]
	public GameObject depletedPrefab;

	[HideInInspector]
	public int usesRemaining;
}

public class GenericInteractable : MonoBehaviour, IInteractable
{
	[SerializeField] private string objectName = "Object";
	[SerializeField] private List<InteractionEntry> interactions = new List<InteractionEntry>();

	void Start()
	{
		foreach (var entry in interactions)
		{
			if (entry.randomMin > 0 && entry.randomMax >= entry.randomMin)
				entry.usesRemaining = Random.Range(entry.randomMin, entry.randomMax + 1);
			else
				entry.usesRemaining = entry.maxUses;
		}
	}

	public string GetName() => objectName;

	public float GetInteractionDuration(string actionId)
	{
		var entry = interactions.Find(e => e.id == actionId);
		if (entry != null && entry.actionType == InteractionActionType.Harvest)
			return entry.harvestDuration;
		return 0f;
	}

	public string GetInteractionLabel(string actionId)
	{
		var entry = interactions.Find(e => e.id == actionId);
		return entry != null ? entry.progressLabel : string.Empty;
	}

	public List<InteractionOption> GetAvailableInteractions(SelectableCharacter interactor)
	{
		var options = new List<InteractionOption>();
		foreach (var entry in interactions)
		{
			if (entry.maxUses > 0 && entry.usesRemaining <= 0)
				continue;
			options.Add(new InteractionOption(entry.id, entry.displayName));
		}
		return options;
	}

	public void ExecuteInteraction(SelectableCharacter interactor, string actionId)
	{
		var entry = interactions.Find(e => e.id == actionId);
		if (entry == null) return;

		switch (entry.actionType)
		{
			case InteractionActionType.Drink:
				interactor.Drink(entry.value);
				break;
			case InteractionActionType.Eat:
				interactor.Eat(entry.value);
				break;
			case InteractionActionType.Sleep:
				interactor.Sleep();
				break;
			case InteractionActionType.Collect:
				interactor.collectItem(entry.item);
				Destroy(gameObject);
				return; // object destroyed, skip use decrement
			case InteractionActionType.Attack:
				interactor.Attack(this);
				break;
			case InteractionActionType.Harvest:
				interactor.collectItem(entry.item);
				if (entry.maxUses > 0)
				{
					entry.usesRemaining--;
					if (entry.usesRemaining <= 0 && entry.depletedPrefab != null)
					{
						Instantiate(entry.depletedPrefab, transform.position, transform.rotation);
						Destroy(gameObject);
					}
				}
				return; // use decrement already handled above
		}

		if (entry.maxUses > 0)
			entry.usesRemaining--;
	}

	public Vector3 GetInteractionPoint() => transform.position;
}
