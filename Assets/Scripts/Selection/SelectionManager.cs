using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
	public Camera mainCamera;
	public List<SelectableCharacter> selectedCharacters = new List<SelectableCharacter>();

	private Vector2 dragStartPos;
	private bool isDragging = false;

	void Update()
	{
		HandleMouseInput();
	}

	void HandleMouseInput()
	{
		// Left mouse button pressed
		if (Input.GetMouseButtonDown(0))
		{
			dragStartPos = Input.mousePosition;
			isDragging = true;
		}

		// Left mouse button released
		if (Input.GetMouseButtonUp(0))
		{
			if (isDragging)
			{
				// Check if it was a click or a drag
				if ((dragStartPos - (Vector2)Input.mousePosition).magnitude < 5f)
				{
					HandleClickSelection();
				}
				else
				{
					HandleDragSelection();
				}

				isDragging = false;
			}
		}

		// Right-click to interact or move
		if (Input.GetMouseButtonDown(1))
		{
			Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
			if (Physics.Raycast(ray, out RaycastHit hit))
			{
				var interactable = hit.collider.GetComponentInParent<IInteractable>();

				if (interactable != null)
				{
					var options = interactable.GetAvailableInteractions(selectedCharacters[0]);
					if (options.Count > 0)
					{
						// For now, auto-pick the first action
						string chosenAction = options[0].id;
						Debug.Log($"Interacting with {interactable.GetName()} using '{options[0].displayName}'");

						foreach (var c in selectedCharacters)
							c.InteractWith(interactable, chosenAction);
					}
				}
				else
				{
					// Move
					foreach (var c in selectedCharacters)
						c.MoveTo(hit.point);
				}
			}
		}
	}

	void HandleClickSelection()
	{
		bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

		Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
		if (Physics.Raycast(ray, out RaycastHit hit))
		{
			SelectableCharacter character = hit.collider.GetComponentInParent<SelectableCharacter>();
			if (character != null)
			{
				if (selectedCharacters.Contains(character))
				{
					if (shiftHeld)
					{
						// Deselect if shift is held
						character.SetSelected(false);
						selectedCharacters.Remove(character);
					}
				}
				else
				{
					if (!shiftHeld)
					{
						DeselectAll();
					}
					character.SetSelected(true);
					selectedCharacters.Add(character);
				}
			}
			else if (!shiftHeld)
			{
				// Clicked empty space without shift: deselect all
				DeselectAll();
			}
		}
	}

	void HandleDragSelection()
	{
		bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		Rect selectionRect = GetScreenRect(dragStartPos, Input.mousePosition);

		SelectableCharacter[] allCharacters = FindObjectsOfType<SelectableCharacter>();

		foreach (var character in allCharacters)
		{
			Vector3 screenPos = mainCamera.WorldToScreenPoint(character.transform.position);

			if (screenPos.z < 0) continue;

			if (selectionRect.Contains(screenPos))
			{
				if (!selectedCharacters.Contains(character))
				{
					selectedCharacters.Add(character);
					character.SetSelected(true);
				}
			}
			else if (!shiftHeld)
			{
				if (selectedCharacters.Contains(character))
				{
					selectedCharacters.Remove(character);
					character.SetSelected(false);
				}
			}
		}
	}

	void DeselectAll()
	{
		foreach (var c in selectedCharacters)
		{
			c.SetSelected(false);
		}
		selectedCharacters.Clear();
	}

	Rect GetScreenRect(Vector2 start, Vector2 end)
	{
		float x = Mathf.Min(start.x, end.x);
		float y = Mathf.Min(start.y, end.y);
		float width = Mathf.Abs(start.x - end.x);
		float height = Mathf.Abs(start.y - end.y);
		return new Rect(x, y, width, height);
	}

	void OnGUI()
	{
		if (isDragging)
		{
			Rect rect = GetScreenRect(dragStartPos, Input.mousePosition);
			GUI.color = new Color(0, 1, 0, 0.2f);
			GUI.DrawTexture(rect, Texture2D.whiteTexture);
			GUI.color = Color.green;
			GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - 2, rect.width, 2), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(rect.x, rect.y, 2, rect.height), Texture2D.whiteTexture);
			GUI.DrawTexture(new Rect(rect.x + rect.width - 2, rect.y, 2, rect.height), Texture2D.whiteTexture);
		}
	}
}
