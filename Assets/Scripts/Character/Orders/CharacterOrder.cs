using System.Collections;

/// <summary>
/// What a Character is doing: walking somewhere, fighting, harvesting, sleeping... A Character runs
/// one order at a time (see Character.GiveOrder). Giving it a new order ends the current one first,
/// so each order cleans up after itself in End instead of being cancelled by hand.
/// </summary>
public abstract class CharacterOrder
{
	/// <summary>Whether self-defence may replace this order when a hostile actor attacks.</summary>
	public virtual bool YieldsToSelfDefence => true;

	/// <summary>Whether any attack, even friendly fire, cuts this order short.</summary>
	public virtual bool StopsWhenAttacked => false;

	/// <summary>Runs as a coroutine on the character. The order ends when this finishes.</summary>
	public abstract IEnumerator Run(Character character);

	/// <summary>Called once when the order finishes or is replaced, to tidy up labels, paths and slots.</summary>
	public virtual void End(Character character)
	{
	}
}
