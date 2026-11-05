public enum InteractionKind
{
	/// <summary>Walk to the target, optionally work for a while, then perform the action once.</summary>
	Task,
	/// <summary>Keep attacking until the target dies or disappears, or another order arrives.</summary>
	Attack
}

[System.Serializable]
public class InteractionOption
{
	public string id;
	public string displayName;
	public InteractionKind kind;

	public InteractionOption(string id, string displayName, InteractionKind kind = InteractionKind.Task)
	{
		this.id = id;
		this.displayName = displayName;
		this.kind = kind;
	}
}
