using System;
using UnityEngine;

public readonly struct CombatAnimationEventArgs
{
    public readonly GameObject Attacker;
    public readonly GameObject Defender;
    public readonly CombatReactionType Reaction;
    public readonly float Damage;

    public CombatAnimationEventArgs(GameObject attacker, GameObject defender, CombatReactionType reaction, float damage)
    {
        Attacker = attacker;
        Defender = defender;
        Reaction = reaction;
        Damage = damage;
    }
}

public interface ICombatAnimationEventSource
{
    event Action<CombatAnimationEventArgs> OnAttackWindup;
    event Action<CombatAnimationEventArgs> OnAttackImpact;
    event Action<CombatAnimationEventArgs> OnReactionResolved;
}
