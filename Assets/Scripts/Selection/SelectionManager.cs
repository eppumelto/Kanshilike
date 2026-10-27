using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Selecting and commanding the squad.
///
///   click              select only that character (click empty ground: clear)
///   Shift+click        add / remove a character
///   drag               box-select everyone the box touches (Shift: add to the selection)
///   double-click       select every squad member on screen
///   Ctrl+A             select the whole squad
///   right-click        move / interact with the selection
///   I                  inventory of the selection
/// </summary>
public class SelectionManager : MonoBehaviour
{
	private const float DragThreshold = 6f;      // pixels before a click becomes a box
	private const float DoubleClickSeconds = 0.3f;

	public Camera mainCamera;
	public List<SelectableCharacter> selectedCharacters = new List<SelectableCharacter>();

	public System.Action<IReadOnlyList<SelectableCharacter>> OnSelectionChanged;
	public SelectableCharacter PrimarySelected => selectedCharacters.Count > 0 ? selectedCharacters[0] : null;

	private readonly List<SelectableCharacter> squad = new List<SelectableCharacter>();
	private readonly HashSet<SelectableCharacter> previewed = new HashSet<SelectableCharacter>();
	private Vector2 pressPosition;
	private bool pressedInWorld;
	private bool boxing;
	private float lastClickTime = -1f;
	private SelectableCharacter lastClicked;

	private void Start()
	{
		if (mainCamera == null)
			mainCamera = Camera.main;

		squad.AddRange(FindObjectsByType<SelectableCharacter>(FindObjectsSortMode.InstanceID));
		SelectionHud.Instance.Bind(this);
	}

	private void Update()
	{
		// Characters that died since last frame drop out of the selection.
		if (selectedCharacters.RemoveAll(c => c == null || !c.IsAlive) > 0)
			NotifySelectionChanged();

		if (Input.GetKeyDown(KeyCode.I))
			InventoryScreen.Instance.Toggle(selectedCharacters);

		if (CtrlHeld && Input.GetKeyDown(KeyCode.A))
			SetSelection(AliveSquad(), additive: false);

		HandleLeftMouse();
		HandleRightMouse();
	}

	// Clicks on inventory windows or other UI must not also select or command in the world.
	private static bool PointerOverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
	private static bool ShiftHeld => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
	private static bool CtrlHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

	// ── Selection API (also used by the squad bar) ──────────────────────────

	public void SelectOnly(SelectableCharacter character)
	{
		SetSelection(new[] { character }, additive: false);
	}

	public void Toggle(SelectableCharacter character)
	{
		if (character == null)
			return;

		if (selectedCharacters.Contains(character))
		{
			selectedCharacters.Remove(character);
			character.SetSelected(false);
		}
		else if (character.IsAlive)
		{
			selectedCharacters.Add(character);
			character.SetSelected(true);
		}
		NotifySelectionChanged();
	}

	public void ClearSelection()
	{
		if (selectedCharacters.Count == 0)
			return;

		foreach (var c in selectedCharacters)
			if (c != null)
				c.SetSelected(false);
		selectedCharacters.Clear();
		NotifySelectionChanged();
	}

	/// <summary>Selects these characters; without <paramref name="additive"/> everyone else is deselected.</summary>
	public void SetSelection(IEnumerable<SelectableCharacter> characters, bool additive)
	{
		var wanted = new HashSet<SelectableCharacter>();
		foreach (var c in characters)
			if (c != null && c.IsAlive)
				wanted.Add(c);

		bool changed = false;
		if (!additive)
		{
			for (int i = selectedCharacters.Count - 1; i >= 0; i--)
			{
				var c = selectedCharacters[i];
				if (wanted.Contains(c))
					continue;
				selectedCharacters.RemoveAt(i);
				if (c != null)
					c.SetSelected(false);
				changed = true;
			}
		}

		// Keep the order characters were picked in: the first one is the "primary".
		foreach (var c in characters)
		{
			if (c == null || !wanted.Contains(c) || selectedCharacters.Contains(c))
				continue;
			selectedCharacters.Add(c);
			c.SetSelected(true);
			changed = true;
		}

		if (changed)
			NotifySelectionChanged();
	}

	private void NotifySelectionChanged()
	{
		OnSelectionChanged?.Invoke(selectedCharacters);
	}

	private IEnumerable<SelectableCharacter> AliveSquad()
	{
		foreach (var c in squad)
			if (c != null && c.IsAlive)
				yield return c;
	}

	// ── Left mouse: click, double-click, box ────────────────────────────────

	private void HandleLeftMouse()
	{
		Vector2 mouse = Input.mousePosition;

		if (Input.GetMouseButtonDown(0) && !PointerOverUI)
		{
			pressPosition = mouse;
			pressedInWorld = true;
			boxing = false;
		}

		if (!pressedInWorld)
			return;

		if (Input.GetMouseButton(0))
		{
			if (!boxing && (mouse - pressPosition).magnitude >= DragThreshold)
				boxing = true;

			if (boxing)
			{
				Rect rect = ScreenRect(pressPosition, mouse);
				SelectionHud.Instance.ShowBox(pressPosition, mouse);
				SetPreview(CharactersIn(rect));
			}
		}

		if (Input.GetMouseButtonUp(0))
		{
			if (boxing)
				SetSelection(CharactersIn(ScreenRect(pressPosition, mouse)), additive: ShiftHeld);
			else
				HandleClick(mouse);

			SelectionHud.Instance.HideBox();
			SetPreview(null);
			pressedInWorld = false;
			boxing = false;
		}
	}

	private void HandleClick(Vector2 mouse)
	{
		SelectableCharacter clicked = CharacterUnder(mouse);

		if (clicked == null)
		{
			// Ground, sky or a body: clear unless adding with Shift.
			if (!ShiftHeld)
				ClearSelection();
			lastClicked = null;
			return;
		}

		bool doubleClick = clicked == lastClicked && Time.unscaledTime - lastClickTime <= DoubleClickSeconds;
		lastClicked = clicked;
		lastClickTime = Time.unscaledTime;

		if (doubleClick)
		{
			// Everyone in view, with the clicked character first (primary).
			var onScreen = new List<SelectableCharacter> { clicked };
			onScreen.AddRange(CharactersIn(new Rect(0f, 0f, Screen.width, Screen.height)));
			SetSelection(onScreen, additive: ShiftHeld);
			lastClicked = null; // a third click starts over
		}
		else if (ShiftHeld)
			Toggle(clicked);
		else
			SelectOnly(clicked);
	}

	private SelectableCharacter CharacterUnder(Vector2 mouse)
	{
		Ray ray = mainCamera.ScreenPointToRay(mouse);
		if (!Physics.Raycast(ray, out RaycastHit hit))
			return null;

		var character = hit.collider.GetComponentInParent<SelectableCharacter>();
		return character != null && character.IsAlive ? character : null;
	}

	/// <summary>Living squad members whose on-screen outline overlaps the rectangle (pixels, origin bottom-left).</summary>
	private List<SelectableCharacter> CharactersIn(Rect rect)
	{
		var result = new List<SelectableCharacter>();
		foreach (var c in AliveSquad())
			if (TryGetScreenRect(c, out Rect r) && r.Overlaps(rect))
				result.Add(c);
		return result;
	}

	/// <summary>The character's collider bounds projected to the screen, so touching any part of them counts.</summary>
	private bool TryGetScreenRect(SelectableCharacter character, out Rect rect)
	{
		var col = character.GetComponentInChildren<Collider>();
		Bounds b = col != null ? col.bounds : new Bounds(character.transform.position, Vector3.one);

		Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
		Vector2 max = new Vector2(float.MinValue, float.MinValue);
		bool anyInFront = false;
		for (int i = 0; i < 8; i++)
		{
			Vector3 corner = new Vector3(
				(i & 1) == 0 ? b.min.x : b.max.x,
				(i & 2) == 0 ? b.min.y : b.max.y,
				(i & 4) == 0 ? b.min.z : b.max.z);
			Vector3 p = mainCamera.WorldToScreenPoint(corner);
			if (p.z <= 0f)
				continue;
			anyInFront = true;
			min = Vector2.Min(min, p);
			max = Vector2.Max(max, p);
		}

		rect = anyInFront ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : default;
		return anyInFront;
	}

	private static Rect ScreenRect(Vector2 a, Vector2 b) => Rect.MinMaxRect(
		Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

	/// <summary>Faint rings on whoever the box would select right now.</summary>
	private void SetPreview(List<SelectableCharacter> characters)
	{
		var next = characters != null ? new HashSet<SelectableCharacter>(characters) : new HashSet<SelectableCharacter>();

		foreach (var c in previewed)
			if (c != null && !next.Contains(c))
				c.Ring.SetPreview(false);
		foreach (var c in next)
			if (!previewed.Contains(c))
				c.Ring.SetPreview(true);

		previewed.Clear();
		previewed.UnionWith(next);
	}

	// ── Right mouse: move / interact ────────────────────────────────────────

	private void HandleRightMouse()
	{
		if (!Input.GetMouseButtonDown(1) || PointerOverUI || selectedCharacters.Count == 0)
			return;

		Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
		if (!Physics.Raycast(ray, out RaycastHit hit))
			return;

		// A body's own interactions (loot) win over whatever it had while alive.
		Corpse corpse = hit.collider.GetComponentInParent<Corpse>();
		IInteractable interactable = corpse != null ? corpse : hit.collider.GetComponentInParent<IInteractable>();
		var options = interactable?.GetAvailableInteractions(selectedCharacters[0]);

		if (options != null && options.Count > 0)
		{
			// For now, auto-pick the first action
			string chosenAction = options[0].id;
			Debug.Log($"Interacting with {interactable.GetName()} using '{options[0].displayName}'");

			// One looter is enough; everything else is done by the whole selection.
			if (corpse != null)
				selectedCharacters[0].InteractWith(interactable, chosenAction);
			else
				foreach (var c in selectedCharacters)
					c.InteractWith(interactable, chosenAction);
		}
		else
		{
			// Plain ground, or something with nothing to do (e.g. a corpse): just walk there.
			foreach (var c in selectedCharacters)
				c.CommandMove(hit.point);
		}
	}
}
