using UnityEngine;
using UnityEngine.AI;

public class SelectableCharacter : MonoBehaviour
{
	public string characterName;
	public bool isSelected = false;

	private Renderer rend;
	private Color originalColor;

	private NavMeshAgent agent;

	void Start()
	{
		rend = GetComponentInChildren<Renderer>();
		originalColor = rend.material.color;
		agent = GetComponent<NavMeshAgent>();
	}

	public void SetSelected(bool selected)
	{
		isSelected = selected;
		rend.material.color = selected ? Color.green : originalColor;
	}

	public void MoveTo(Vector3 position)
	{
		if (agent != null)
		{
			agent.SetDestination(position);
		}
	}
}
