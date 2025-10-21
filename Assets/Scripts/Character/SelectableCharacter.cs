using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

public class SelectableCharacter : MonoBehaviour
{
	public string characterName;
	public bool isSelected = false;

	private Renderer rend;
	private Color originalColor;
	private NavMeshAgent agent;
	private CharacterStats stats;

	public TMP_Text nameText;
	public Slider hungerSlider;
	public Slider thirstSlider;
	public Slider tirednessSlider;

	[Header("Inventory Grid")]
	public int inventoryWidth = 6;
	public int inventoryHeight = 6;
	public Inventory inventory;

	void Start()
	{
		rend = GetComponentInChildren<Renderer>();
		originalColor = rend.material.color;
		agent = GetComponent<NavMeshAgent>();
		stats = GetComponent<CharacterStats>();

		inventory = new Inventory(inventoryWidth, inventoryHeight);

		// Initialize sliders
		hungerSlider.maxValue = 100f;
		thirstSlider.maxValue = 100f;
		tirednessSlider.maxValue = 100f;
	}

	private void Update()
	{
		if (isSelected)
		{
			nameText.text = characterName;
			hungerSlider.value = stats.hunger;
			thirstSlider.value = stats.thirst;
			tirednessSlider.value = stats.tiredness;
		}
	}

	public void SetSelected(bool selected)
	{
		isSelected = selected;
		rend.material.color = selected ? Color.green : originalColor;
	}

	public void MoveTo(Vector3 position)
	{
		if (agent != null)
			agent.SetDestination(position);
	}

	public void InteractWith(IInteractable target, string actionId)
	{
		StopAllCoroutines();
		StartCoroutine(MoveAndPerform(target, actionId));
	}

	private IEnumerator MoveAndPerform(IInteractable target, string actionId)
	{
		Vector3 dest = target.GetInteractionPoint();
		MoveTo(dest);

		while (Vector3.Distance(transform.position, dest) > 2f)
		{
			yield return null;
		}

		target.ExecuteInteraction(this, actionId);
	}

	public void Drink(float amount)
	{
		Debug.Log($"{characterName} is drinking");
		stats?.Drink(amount);
	}

	public void Eat(float nutrition)
	{
		Debug.Log($"{characterName} is eating");
		stats?.Eat(nutrition);
	}
	public void Sleep()
	{
		Debug.Log($"{characterName} is sleeping");
		stats?.Sleep(8f);
	}
}
