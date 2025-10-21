using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventorySlotUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler
{
	public Image icon;
	public Item currentItem;
	private Transform originalParent;
	private Canvas canvas;

	private void Start()
	{
		canvas = GetComponentInParent<Canvas>();
		icon.enabled = false;
	}

	public void SetItem(Item item)
	{
		currentItem = item;
		if (item != null)
		{
			icon.sprite = item.icon;
			icon.enabled = true;
		}
		else
		{
			icon.enabled = false;
		}
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		if (currentItem != null)
		{
			originalParent = transform.parent;
			icon.transform.SetParent(canvas.transform);
		}
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (currentItem != null)
			icon.transform.position = Input.mousePosition;
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if (currentItem != null)
		{
			// Here you can implement drop logic to a slot
			icon.transform.SetParent(originalParent);
			icon.transform.localPosition = Vector3.zero;
		}
	}
}
