using UnityEngine;

[System.Serializable]
public class Item
{
	public string itemName;
	public Sprite icon;            // For UI
	public Vector2Int size = new Vector2Int(1, 1); // Width x Height
	public int quantity = 1;
	public bool isEquipable = false;
	public EquipmentSlot equipSlot; // If equipable
}

public enum EquipmentSlot
{
	Head,
	Body,
	Weapon,
	Shield,
	Legs
}
