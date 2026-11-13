using System.Collections.Generic;

/// <summary>One thing an NPC might say. The highest priority present wins; ties pick at random.</summary>
public readonly struct NpcTalkLine
{
	public readonly string Text;
	public readonly int Priority;
	/// <summary>Whether the NPC stops and faces the speaker afterwards.</summary>
	public readonly bool Hold;

	public NpcTalkLine(string text, int priority, bool hold)
	{
		Text = text;
		Priority = priority;
		Hold = hold;
	}
}

/// <summary>Adds lines an NPC may say when talked to (tribe sayings, grudge barks). The archetype's lines are priority 0.</summary>
public interface INpcTalkSource
{
	void CollectLines(Character interactor, List<NpcTalkLine> into);
}
