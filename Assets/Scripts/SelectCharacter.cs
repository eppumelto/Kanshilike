using UnityEngine;
using System.Collections.Generic;

public class SelectCharacter : MonoBehaviour
{
	public Camera mainCamera;

	private List<CharacterInfo> selectedCharacters = new List<CharacterInfo>();
	private HashSet<PlayerMovement> selectedMovements = new HashSet<PlayerMovement>();

	void Update()
	{
		if (Input.GetMouseButtonDown(0))
			HandleSelection();
	}

	void HandleSelection()
	{
		Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
		if (!Physics.Raycast(ray, out RaycastHit hit))
			return;

		CharacterInfo character = hit.collider.GetComponentInParent<CharacterInfo>();
		PlayerMovement movement = hit.collider.GetComponentInParent<PlayerMovement>();

		if (character == null || movement == null)
			return;

		// Use the list as the only source of truth
		if (selectedCharacters.Contains(character))
		{
			// Unselect character
			selectedCharacters.Remove(character);
			selectedMovements.Remove(movement);

			character.Highlight(false);
			movement.selected = false;

			Debug.Log("Unselected: " + character.characterName);
		}
		else
		{
			// Select character
			selectedCharacters.Add(character);
			selectedMovements.Add(movement);

			character.Highlight(true);
			movement.selected = true;

			Debug.Log("Selected: " + character.characterName);
		}
	}

	public void ClearSelection()
	{
		foreach (var character in selectedCharacters)
			character.Highlight(false);
		foreach (var movement in selectedMovements)
			movement.selected = false;

		selectedCharacters.Clear();
		selectedMovements.Clear();
	}
}
