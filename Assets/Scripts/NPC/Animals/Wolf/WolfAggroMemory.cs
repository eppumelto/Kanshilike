using UnityEngine;

public sealed class WolfAggroMemory
{
    public Transform Target { get; private set; }
    public SelectableCharacter Character { get; private set; }
    public float Until { get; private set; }

    public void Set(SelectableCharacter attacker, float now, float durationSeconds)
    {
        Character = attacker;
        Target = attacker != null ? attacker.transform : null;
        Until = now + durationSeconds;
    }

    public bool TryGetLiveTarget(float now, out Transform target, out SelectableCharacter character)
    {
        bool alive = Target != null
            && Character != null
            && Character.Stats != null
            && Character.Stats.IsAlive
            && now < Until;

        if (!alive)
        {
            Clear();
            target = null;
            character = null;
            return false;
        }

        target = Target;
        character = Character;
        return true;
    }

    public void Clear()
    {
        Target = null;
        Character = null;
        Until = 0f;
    }
}
