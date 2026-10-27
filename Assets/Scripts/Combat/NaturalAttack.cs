using UnityEngine;

/// <summary>
/// Built-in attack for creatures that fight without weapons (wolf bite, claws, ...).
/// Used instead of CombatFormulaConfig's unarmed values when the attacker has no weapon equipped.
/// </summary>
[System.Serializable]
public struct NaturalAttack
{
    [Min(0.1f)] public float damage;
    [Min(0.2f)] public float range;
    [Tooltip("Attack speed multiplier. >1 is faster, <1 is slower.")]
    [Min(0.1f)] public float speedMultiplier;
    [Tooltip("Multiplies the attack rating, i.e. how hard this attack is to block, dodge or counter. 1 = same as an untrained human.")]
    [Min(0.1f)] public float attackRatingMultiplier;

    // Tuned so an untrained, unarmed human with 100 HP beats one 100 HP wolf a bit over half the time.
    public static NaturalAttack Wolf => new NaturalAttack
    {
        damage = 5f,
        range = 1.6f,
        speedMultiplier = 1f,
        attackRatingMultiplier = 1f
    };
}

/// <summary>Combat actors with a NaturalAttack implement this so attack profiles can use it.</summary>
public interface INaturalAttacker
{
    NaturalAttack NaturalAttack { get; }
}
