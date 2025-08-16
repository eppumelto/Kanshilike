using UnityEngine;
using UnityEngine.AI;

public class PlayerMovement : MonoBehaviour
{
	public Camera cam;
	private NavMeshAgent agent;
	public int speed = 10;

	public bool selected = false;

	void Start()
	{
		agent = GetComponent<NavMeshAgent>();
		agent.speed = speed;
	}

	void Update()
	{

		if (selected)
		{
			if (Input.GetMouseButtonDown(1)) // Right click to move
			{
				Ray ray = cam.ScreenPointToRay(Input.mousePosition);
				RaycastHit hit;

				if (Physics.Raycast(ray, out hit))
				{
					agent.SetDestination(hit.point);

				}
			}
		}
	}
}

