using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Small helpers for building the inventory UI from code (no prefabs needed).</summary>
public static class UIFactory
{
	public static readonly Color WindowColor = new Color(0.23f, 0.19f, 0.15f, 0.97f);
	public static readonly Color HeaderColor = new Color(0.16f, 0.13f, 0.10f, 1f);
	public static readonly Color CellColor = new Color(0.30f, 0.26f, 0.21f, 1f);
	public static readonly Color CellLineColor = new Color(0.20f, 0.17f, 0.13f, 1f);
	public static readonly Color ItemColor = new Color(0.45f, 0.40f, 0.33f, 0.95f);
	public static readonly Color SlotColor = new Color(0.33f, 0.28f, 0.22f, 1f);
	public static readonly Color TextColor = new Color(0.93f, 0.89f, 0.80f, 1f);
	public static readonly Color MutedTextColor = new Color(0.75f, 0.70f, 0.60f, 1f);
	public static readonly Color ValidColor = new Color(0.35f, 0.85f, 0.35f, 0.35f);
	public static readonly Color InvalidColor = new Color(0.90f, 0.25f, 0.20f, 0.35f);
	public static readonly Color WarningColor = new Color(1f, 0.55f, 0.35f, 1f);

	/// <summary>A RectTransform anchored and pivoted at its parent's top-left corner.</summary>
	public static RectTransform Rect(string name, Transform parent, Vector2 topLeft, Vector2 size)
	{
		var go = new GameObject(name, typeof(RectTransform));
		var rect = (RectTransform)go.transform;
		rect.SetParent(parent, false);
		rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
		rect.pivot = new Vector2(0f, 1f);
		rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
		rect.sizeDelta = size;
		return rect;
	}

	public static Image Panel(string name, Transform parent, Vector2 topLeft, Vector2 size, Color color, bool raycast = true)
	{
		var image = Rect(name, parent, topLeft, size).gameObject.AddComponent<Image>();
		image.color = color;
		image.raycastTarget = raycast;
		return image;
	}

	public static TextMeshProUGUI Text(string name, Transform parent, Vector2 topLeft, Vector2 size, string text,
		float fontSize = 18f, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
	{
		var tmp = Rect(name, parent, topLeft, size).gameObject.AddComponent<TextMeshProUGUI>();
		tmp.text = text;
		tmp.fontSize = fontSize;
		tmp.alignment = align;
		tmp.color = TextColor;
		tmp.raycastTarget = false;
		tmp.textWrappingMode = TextWrappingModes.NoWrap;
		tmp.overflowMode = TextOverflowModes.Ellipsis;
		return tmp;
	}

	public static Button Button(string name, Transform parent, Vector2 topLeft, Vector2 size, string label, System.Action onClick)
	{
		var image = Panel(name, parent, topLeft, size, SlotColor);
		var button = image.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		button.onClick.AddListener(() => onClick());
		Text("Label", image.transform, Vector2.zero, size, label, 18f, TextAlignmentOptions.Center);
		return button;
	}

	/// <summary>An item icon inside a box of <paramref name="boxSize"/>, turned 90° when rotated.</summary>
	public static Image Icon(Transform parent, Item item, Vector2 boxSize, bool rotated)
	{
		var rect = Rect("Icon", parent, Vector2.zero, boxSize);
		rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = Vector2.zero;
		rect.sizeDelta = rotated ? new Vector2(boxSize.y, boxSize.x) : boxSize;
		rect.localRotation = rotated ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;

		var image = rect.gameObject.AddComponent<Image>();
		image.sprite = item != null ? item.icon : null;
		image.preserveAspect = true;
		image.raycastTarget = false;
		image.color = item != null && item.icon != null ? Color.white : Color.clear;
		return image;
	}
}
