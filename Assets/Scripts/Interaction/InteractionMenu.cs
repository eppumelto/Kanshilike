using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Small pop-up to pick one of several right-click options. Closes on a choice, on Escape, or on
/// a click anywhere outside it.
/// </summary>
public class InteractionMenu : MonoBehaviour
{
	private const float Width = 180f;
	private const float RowHeight = 30f;
	private const float Padding = 6f;

	private static InteractionMenu instance;

	private RectTransform root;
	private RectTransform panel;
	private int openedFrame;

	public static InteractionMenu Instance
	{
		get
		{
			if (instance == null)
				instance = new GameObject("InteractionMenu").AddComponent<InteractionMenu>();
			return instance;
		}
	}

	public static bool IsOpen => instance != null && instance.panel != null;

	private void Awake()
	{
		instance = this;
		var canvas = gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 60; // over the squad bar (50), under the inventory windows (100)
		var scaler = gameObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;
		gameObject.AddComponent<GraphicRaycaster>();
		root = (RectTransform)transform;
	}

	/// <summary>Opens at a screen position (pixels, origin bottom-left, like Input.mousePosition).</summary>
	public void Show(Vector2 screenPos, string title, List<InteractionOption> options, Action<InteractionOption> onChosen)
	{
		Hide();
		openedFrame = Time.frameCount;

		float height = Padding * 2f + RowHeight * (options.Count + 1);
		panel = UIFactory.Panel("Menu", root, Vector2.zero, new Vector2(Width, height), UIFactory.WindowColor).rectTransform;
		UIFactory.Text("Title", panel, new Vector2(Padding, Padding), new Vector2(Width - Padding * 2f, RowHeight), title)
			.color = UIFactory.MutedTextColor;

		for (int i = 0; i < options.Count; i++)
		{
			InteractionOption option = options[i];
			Vector2 topLeft = new Vector2(Padding, Padding + RowHeight * (i + 1)); // UIFactory: y grows downward
			UIFactory.Button(option.id, panel, topLeft, new Vector2(Width - Padding * 2f, RowHeight - 4f), option.displayName, () =>
			{
				Hide();
				onChosen?.Invoke(option);
			});
		}

		Place(screenPos, new Vector2(Width, height));
	}

	public void Hide()
	{
		if (panel != null)
			Destroy(panel.gameObject);
		panel = null;
	}

	private void Update()
	{
		if (panel == null || Time.frameCount == openedFrame)
			return;

		if (Input.GetKeyDown(KeyCode.Escape))
		{
			Hide();
			return;
		}

		bool pressed = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
		if (pressed && !RectTransformUtility.RectangleContainsScreenPoint(panel, Input.mousePosition, null))
			Hide();
	}

	// Puts the panel's top-left at the mouse, kept inside the screen.
	private void Place(Vector2 screenPos, Vector2 size)
	{
		float scale = root.lossyScale.x > 0f ? root.lossyScale.x : 1f;
		Vector2 local = screenPos / scale;
		Vector2 canvasSize = root.rect.size;
		local.x = Mathf.Clamp(local.x, 0f, Mathf.Max(0f, canvasSize.x - size.x));
		local.y = Mathf.Clamp(local.y, size.y, Mathf.Max(size.y, canvasSize.y));

		panel.anchorMin = panel.anchorMax = Vector2.zero;
		panel.pivot = new Vector2(0f, 1f);
		panel.anchoredPosition = local;
	}
}
