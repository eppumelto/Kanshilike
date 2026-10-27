using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen overlay for selection: the drag box and the squad bar. Builds its own canvas,
/// drawn under the inventory windows.
/// </summary>
public class SelectionHud : MonoBehaviour
{
	private static readonly Color BoxFill = new Color(0.35f, 1f, 0.45f, 0.12f);
	private static readonly Color BoxEdge = new Color(0.35f, 1f, 0.45f, 0.9f);
	private const float EdgeWidth = 2f;
	private const float CardWidth = 170f;
	private const float CardHeight = 72f;
	private const float CardGap = 8f;

	private static SelectionHud instance;

	private RectTransform root;
	private RectTransform box;
	private RectTransform squadBar;
	private readonly List<SquadCard> cards = new List<SquadCard>();
	private SelectionManager manager;
	private float nextLayoutCheck;
	private float lastHudTop = -1f;

	public static SelectionHud Instance
	{
		get
		{
			if (instance == null)
				instance = new GameObject("SelectionHud").AddComponent<SelectionHud>();
			return instance;
		}
	}

	private void Awake()
	{
		instance = this;

		var canvas = gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 50; // under the inventory windows (100)
		var scaler = gameObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;
		gameObject.AddComponent<GraphicRaycaster>();
		root = (RectTransform)transform;

		BuildBox();
	}

	// ── Drag box ────────────────────────────────────────────────────────────

	private void BuildBox()
	{
		box = UIFactory.Panel("SelectionBox", root, Vector2.zero, Vector2.zero, BoxFill, raycast: false).rectTransform;
		box.anchorMin = box.anchorMax = box.pivot = Vector2.zero; // positioned in bottom-left screen coordinates, like the mouse

		// Edges: stretch along each side of the box.
		AddEdge(new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -EdgeWidth), Vector2.zero);
		AddEdge(new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, EdgeWidth));
		AddEdge(new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(EdgeWidth, 0f));
		AddEdge(new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-EdgeWidth, 0f), Vector2.zero);
		box.gameObject.SetActive(false);
	}

	private void AddEdge(Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
	{
		var edge = UIFactory.Panel("Edge", box, Vector2.zero, Vector2.zero, BoxEdge, raycast: false).rectTransform;
		edge.anchorMin = anchorMin;
		edge.anchorMax = anchorMax;
		edge.offsetMin = offsetMin;
		edge.offsetMax = offsetMax;
	}

	/// <summary>Shows the box between two screen points (pixels, origin bottom-left, like Input.mousePosition).</summary>
	public void ShowBox(Vector2 screenA, Vector2 screenB)
	{
		Vector2 a = ScreenToCanvas(Vector2.Min(screenA, screenB));
		Vector2 b = ScreenToCanvas(Vector2.Max(screenA, screenB));
		box.anchoredPosition = a;
		box.sizeDelta = b - a;
		box.gameObject.SetActive(true);
	}

	public void HideBox() => box.gameObject.SetActive(false);

	private Vector2 ScreenToCanvas(Vector2 screen)
	{
		// Canvas units from the bottom-left corner.
		return screen / root.lossyScale.x;
	}

	// ── Squad bar ───────────────────────────────────────────────────────────

	public void Bind(SelectionManager selectionManager)
	{
		manager = selectionManager;
		manager.OnSelectionChanged += _ => RefreshCards();
		BuildSquadBar();
	}

	private void BuildSquadBar()
	{
		if (squadBar != null)
			Destroy(squadBar.gameObject);
		cards.Clear();

		var squad = new List<SelectableCharacter>(FindObjectsByType<SelectableCharacter>(FindObjectsSortMode.InstanceID));
		squad.Sort((x, y) => string.CompareOrdinal(x.characterName, y.characterName));

		float width = squad.Count * CardWidth + Mathf.Max(0, squad.Count - 1) * CardGap;
		squadBar = UIFactory.Rect("SquadBar", root, Vector2.zero, new Vector2(width, CardHeight));
		squadBar.anchorMin = squadBar.anchorMax = new Vector2(0.5f, 0f);
		squadBar.pivot = new Vector2(0.5f, 0f);

		for (int i = 0; i < squad.Count; i++)
			cards.Add(SquadCard.Create(squadBar, squad[i], manager, new Vector2(i * (CardWidth + CardGap), 0f), new Vector2(CardWidth, CardHeight)));

		PlaceSquadBar();
		RefreshCards();
	}

	private void Update()
	{
		// Keep the bar just above the scene's bottom HUD, which may move with the window size.
		if (squadBar != null && Time.unscaledTime >= nextLayoutCheck)
		{
			nextLayoutCheck = Time.unscaledTime + 1f;
			PlaceSquadBar();
		}
	}

	private void PlaceSquadBar()
	{
		float hudTop = BottomHudTop();
		if (Mathf.Approximately(hudTop, lastHudTop))
			return;
		lastHudTop = hudTop;
		squadBar.anchoredPosition = new Vector2(0f, hudTop / root.lossyScale.x + 12f);
	}

	/// <summary>Top edge, in screen pixels, of the scene's UI docked to the bottom of the screen (0 if none).</summary>
	private float BottomHudTop()
	{
		float top = 0f;
		var corners = new Vector3[4];
		foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
		{
			if (canvas.renderMode != RenderMode.ScreenSpaceOverlay || canvas.transform == transform || canvas.GetComponent<InventoryScreen>() != null)
				continue;

			foreach (var graphic in canvas.GetComponentsInChildren<Graphic>())
			{
				if (graphic.color.a <= 0.01f)
					continue;
				graphic.rectTransform.GetWorldCorners(corners);
				// Docked to the bottom: starts at or below the screen's bottom edge, and stays in its lower half.
				if (corners[0].y <= 2f && corners[1].y < Screen.height * 0.5f)
					top = Mathf.Max(top, corners[1].y);
			}
		}
		return top;
	}

	public void RefreshCards()
	{
		foreach (var card in cards)
			if (card != null)
				card.Refresh();
	}
}
