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

        // A shield only counts when the attacker is inside its arc.
        ShieldItem coveringShield = CombatFormulaResolver.GetCoveringShield(defender, attacker);

        CombatReactionType reaction = CombatFormulaResolver.ChooseReaction(defender, config, coveringShield);
        onReactionDecided?.Invoke(reaction);

        float defenseRating = CombatFormulaResolver.GetDefenseRating(defender, reaction, config, coveringShield);
        bool defended = Random.value < CombatFormulaResolver.GetDefenseChance(defenseRating, attack.AttackRating);
        bool hitLanded = !defended;
        float counterDamage = 0f;

        GameObject source = attackSource;
        if (source == null && attacker.ActorTransform != null)
            source = attacker.ActorTransform.gameObject;

        defenderStats.NotifyAttacked(source, defended && reaction == CombatReactionType.Block && coveringShield != null);

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
            // A failed block with a shield in the way still takes the edge off the hit.
            float damage = attack.Damage;
            if (reaction == CombatReactionType.Block && coveringShield != null)
                damage *= 1f - coveringShield.failedBlockDamageReduction;

            defenderStats.ReceiveDamage(damage, source);
            return new CombatExchangeResult(reaction, true, damage, counterDamage);
        }

        return new CombatExchangeResult(reaction, false, 0f, counterDamage);
    }

    /// <summary>
    /// An arrow reaching a defender. Whether it was on target is already settled by its flight, so
    /// the only defense left is a shield facing where it came from. No dodge, parry or counter.
    /// </summary>
    public static CombatExchangeResult ResolveProjectileHit(
        AttackProfile attack,
        ICombatActor defender,
        CharacterStats defenderStats,
        CombatFormulaConfig config,
        GameObject shooter,
        Vector3 cameFrom)
    {
        if (defender == null || defenderStats == null || config == null || !defenderStats.IsAlive)
            return new CombatExchangeResult(CombatReactionType.None, false, 0f, 0f);

        ShieldItem coveringShield = CombatFormulaResolver.GetCoveringShield(defender, cameFrom);
        float blockRating = coveringShield != null ? CombatFormulaResolver.GetBlockRating(defender, config, coveringShield) : 0f;
        CombatReactionType reaction = blockRating > 0f ? CombatReactionType.Block : CombatReactionType.None;
        bool blocked = reaction == CombatReactionType.Block
            && Random.value < CombatFormulaResolver.GetDefenseChance(blockRating, attack.AttackRating);

        defenderStats.NotifyAttacked(shooter, blocked);

        if (reaction == CombatReactionType.Block)
            defender.Skills?.GainXP(config.blockSkill, blocked ? config.defenseSuccessXP : config.defenseFailXP);

        if (blocked)
            return new CombatExchangeResult(reaction, false, 0f, 0f);

        float damage = attack.Damage;
        if (reaction == CombatReactionType.Block)
            damage *= 1f - coveringShield.failedBlockDamageReduction;

        // Wound first, so anything reacting to the damage already moves at the slowed speed.
        defenderStats.Wound(config.arrowWoundSpeedMultiplier, config.minWoundSpeedMultiplier);
        defenderStats.ReceiveDamage(damage, shooter);
        return new CombatExchangeResult(reaction, true, damage, 0f);
    }

    /// <summary>The target as a combat actor: its own, or a stand-in built from its components.</summary>
    public static ICombatActor ResolveDefender(Component target, CharacterStats stats)
    {
        if (target.TryGetComponent<ICombatActor>(out var actor))
            return actor;

        EquipmentManager equipment = target.GetComponent<EquipmentManager>();
        bool hasEquipment = equipment != null;
        return new CombatActorProxy(
            target.transform,
            stats,
            target.GetComponent<SkillSet>(),
            equipment,
            canUseEquipment: hasEquipment,
            canBlock: hasEquipment,
            canDodge: true,
            canCounter: true);
    }
}
