using UnityEngine;

public readonly struct CombatExchangeResult
{
    public CombatReactionType Reaction { get; }
    public bool HitLanded { get; }
    public float DamageDealt { get; }
    public float CounterDamage { get; }

    public CombatExchangeResult(CombatReactionType reaction, bool hitLanded, float damageDealt, float counterDamage)
    {
        Reaction = reaction;
        HitLanded = hitLanded;
        DamageDealt = damageDealt;
        CounterDamage = counterDamage;
    }
}

public static class CombatExchangeResolver
{
    public static CombatExchangeResult ResolveAndApply(
        AttackProfile attack,
        ICombatActor attacker,
        ICombatActor defender,
        CharacterStats defenderStats,
        CombatFormulaConfig config,
        GameObject attackSource,
        System.Action<CombatReactionType> onReactionDecided = null)
    {
        if (attacker == null || defender == null || defenderStats == null || config == null || !defenderStats.IsAlive)
            return new CombatExchangeResult(CombatReactionType.None, false, 0f, 0f);

        CombatReactionType reaction = CombatFormulaResolver.ChooseReaction(defender, config);
        onReactionDecided?.Invoke(reaction);

        float defenseRating = CombatFormulaResolver.GetDefenseRating(defender, reaction, config);
        bool defended = Random.value < CombatFormulaResolver.GetDefenseChance(defenseRating, attack.AttackRating);
        bool hitLanded = !defended;
        float counterDamage = 0f;

        if (defended && reaction == CombatReactionType.Counter)
        {
            // The defender's counter uses its own attack (weapon, bite or fists).
            AttackProfile counterAttack = CombatFormulaResolver.BuildAttackProfile(defender, config);
            counterDamage = Mathf.Max(1f, counterAttack.Damage * config.counterDamageFraction);
            attacker.Stats?.ReceiveDamage(counterDamage, defender.ActorTransform != null ? defender.ActorTransform.gameObject : null);
        }

        // Defending trains the skill behind the reaction, more when it works.
        if (reaction != CombatReactionType.None)
        {
            defender.Skills?.GainXP(
                CombatFormulaResolver.GetReactionSkill(reaction, config),
                defended ? config.defenseSuccessXP : config.defenseFailXP);
        }

        if (hitLanded)
        {
            GameObject source = attackSource;
            if (source == null && attacker.ActorTransform != null)
                source = attacker.ActorTransform.gameObject;

            defenderStats.ReceiveDamage(attack.Damage, source);
            return new CombatExchangeResult(reaction, true, attack.Damage, counterDamage);
        }

        return new CombatExchangeResult(reaction, false, 0f, counterDamage);
    }
}
