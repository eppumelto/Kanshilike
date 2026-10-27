using UnityEngine;

public struct AttackProfile
{
    /// <summary>Compared against the defender's block/dodge/counter rating to decide if the hit lands.</summary>
    public float AttackRating;
    public float Damage;
    public float Range;
    public float Interval;
    public SkillType UsedWeaponSkill;
}

/// <summary>Combat maths. See CombatFormulaConfig for how skills feed in.</summary>
public static class CombatFormulaResolver
{
    public static AttackProfile BuildAttackProfile(ICombatActor actor, CombatFormulaConfig config)
    {
        WeaponItem weapon = GetWeapon(actor);
        SkillType weaponSkill = weapon != null ? weapon.weaponSkill : config.unarmedSkill;

        float baseDamage = config.baseUnarmedDamage;
        float range = config.baseUnarmedRange;
        float speed = 1f;
        float ratingMultiplier = 1f;

        if (weapon != null)
        {
            baseDamage = weapon.damage;
            range = weapon.attackRange;
            speed = weapon.attackSpeedMultiplier;
        }
        else if (actor is INaturalAttacker natural)
        {
            NaturalAttack bite = natural.NaturalAttack;
            baseDamage = bite.damage;
            range = bite.range;
            speed = bite.speedMultiplier;
            ratingMultiplier = bite.attackRatingMultiplier;
        }

        float strength = GetSkillProgress(actor, config.strengthSkill);
        float weaponProgress = GetSkillProgress(actor, weaponSkill);

        float attackRating = config.baseAttackRating * ratingMultiplier
            * (1f + weaponProgress * config.weaponSkillAttackScale + strength * config.strengthAttackScale);

        float damage = baseDamage
            * (1f + strength * config.strengthDamageScale + weaponProgress * config.weaponSkillDamageScale);

        float initiative = GetInitiativeMultiplier(actor, config);

        return new AttackProfile
        {
            AttackRating = Mathf.Max(0.1f, attackRating),
            Damage = Mathf.Max(1f, damage),
            Range = Mathf.Max(0.2f, range),
            Interval = Mathf.Max(config.minAttackInterval, config.baseAttackInterval / Mathf.Max(0.1f, speed * initiative)),
            UsedWeaponSkill = weaponSkill
        };
    }

    public static float GetInitiativeMultiplier(ICombatActor actor, CombatFormulaConfig config)
    {
        return 1f
            + GetSkillProgress(actor, config.initiativeSkill) * config.initiativeSkillScale
            + GetSkillProgress(actor, config.strengthSkill) * config.initiativeStrengthScale;
    }

    public static float GetBlockRating(ICombatActor actor, CombatFormulaConfig config)
    {
        if (!actor.CanBlock)
            return 0f;

        float block = config.blockBaseRating * (1f + GetSkillProgress(actor, config.blockSkill) * config.blockSkillScale);

        if (actor.CanUseEquipment && actor.Equipment != null && actor.Equipment.GetEquipped(EquipSlot.Shield) != null)
            block *= config.shieldBlockMultiplier;

        return block;
    }

    public static float GetDodgeRating(ICombatActor actor, CombatFormulaConfig config)
    {
        return config.dodgeBaseRating * (1f + GetSkillProgress(actor, config.dodgeSkill) * config.dodgeSkillScale);
    }

    public static float GetCounterRating(ICombatActor actor, CombatFormulaConfig config)
    {
        if (!actor.CanCounter)
            return 0f;

        return config.counterBaseRating * (1f + GetSkillProgress(actor, config.counterSkill) * config.counterSkillScale);
    }

    public static float GetDefenseRating(ICombatActor defender, CombatReactionType reaction, CombatFormulaConfig config)
    {
        switch (reaction)
        {
            case CombatReactionType.Block: return GetBlockRating(defender, config);
            case CombatReactionType.Dodge: return GetDodgeRating(defender, config);
            case CombatReactionType.Counter: return GetCounterRating(defender, config);
            default: return 0f;
        }
    }

    /// <summary>Chance (0–1) that a defense of the given rating stops an attack of the given rating.</summary>
    public static float GetDefenseChance(float defenseRating, float attackRating)
    {
        if (defenseRating <= 0f)
            return 0f;

        return defenseRating / (defenseRating + Mathf.Max(0.1f, attackRating));
    }

    public static SkillType GetReactionSkill(CombatReactionType reaction, CombatFormulaConfig config)
    {
        switch (reaction)
        {
            case CombatReactionType.Block: return config.blockSkill;
            case CombatReactionType.Counter: return config.counterSkill;
            default: return config.dodgeSkill;
        }
    }

    /// <summary>
    /// Picks a reaction at random by the configured weights, tilted toward the reactions the
    /// defender is more skilled at (a master's weight is doubled).
    /// </summary>
    public static CombatReactionType ChooseReaction(ICombatActor defender, CombatFormulaConfig config)
    {
        float blockWeight = defender.CanBlock ? config.blockWeight * (1f + GetSkillProgress(defender, config.blockSkill)) : 0f;
        float dodgeWeight = config.dodgeWeight * (1f + GetSkillProgress(defender, config.dodgeSkill));
        float counterWeight = defender.CanCounter ? config.counterWeight * (1f + GetSkillProgress(defender, config.counterSkill)) : 0f;

        float total = blockWeight + dodgeWeight + counterWeight;
        if (total <= 0f)
            return CombatReactionType.None;

        float roll = Random.value * total;
        if (roll < blockWeight)
            return CombatReactionType.Block;

        roll -= blockWeight;
        if (roll < dodgeWeight)
            return CombatReactionType.Dodge;

        return CombatReactionType.Counter;
    }

    private static WeaponItem GetWeapon(ICombatActor actor)
    {
        if (!actor.CanUseEquipment || actor.Equipment == null)
            return null;

        return actor.Equipment.GetEquipped(EquipSlot.Hand) as WeaponItem;
    }

    /// <summary>0 for an untrained (or skill-less) actor, 1 at the skill's max bonus (2.0 by default).</summary>
    private static float GetSkillProgress(ICombatActor actor, SkillType skill)
    {
        if (actor.Skills == null)
            return 0f;

        return Mathf.Max(0f, actor.Skills.GetBonus(skill) - 1f);
    }
}
