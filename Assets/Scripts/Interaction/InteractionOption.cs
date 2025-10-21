using System;

[System.Serializable]
public class InteractionOption
{
	public string id;
	public string displayName;

	public InteractionOption(string id, string displayName)
	{
		this.id = id;
		this.displayName = displayName;
	}
}
