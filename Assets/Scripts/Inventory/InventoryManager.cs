using UnityEngine;

public class Inventory
{
	public int width;
	public int height;
	public Item[,] slots;

	public Inventory(int w, int h)
	{
		width = w;
		height = h;
		slots = new Item[w, h];
	}

	public bool AddItem(Item item)
	{
		for (int x = 0; x <= width - item.size.x; x++)
		{
			for (int y = 0; y <= height - item.size.y; y++)
			{
				if (CanPlaceItemAt(item, x, y))
				{
					PlaceItem(item, x, y);
					return true;
				}
			}
		}
		Debug.Log("No space for item: " + item.itemName);
		return false;
	}

	private bool CanPlaceItemAt(Item item, int startX, int startY)
	{
		for (int x = 0; x < item.size.x; x++)
			for (int y = 0; y < item.size.y; y++)
				if (slots[startX + x, startY + y] != null)
					return false;
		return true;
	}

	private void PlaceItem(Item item, int startX, int startY)
	{
		for (int x = 0; x < item.size.x; x++)
			for (int y = 0; y < item.size.y; y++)
				slots[startX + x, startY + y] = item;
	}

	public bool RemoveItem(Item item)
	{
		for (int x = 0; x < width; x++)
			for (int y = 0; y < height; y++)
				if (slots[x, y] == item)
					slots[x, y] = null;
		return true;
	}

	public bool HasItem(string itemName)
	{
		foreach (var item in slots)
			if (item != null && item.itemName == itemName)
				return true;
		return false;
	}
}
