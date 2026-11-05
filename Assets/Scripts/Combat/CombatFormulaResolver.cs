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

/// <summary>
/// Reads skills and items from combat actors and feeds them into CombatRules, where the maths
/// lives. See CombatFormulaConfig for how skills feed in.
/// </summary>
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

        // A bow's draw weight sets its power, so an archer's Strength adds nothing to a shot.
        float strength = weapon is BowItem ? 0f : GetSkillProgress(actor, config.strengthSkill);
        float weaponProgress = GetSkillProgress(actor, weaponSkill);

        ShieldItem carriedShield = GetShield(actor);
        if (carriedShield != null)
            speed *= carriedShield.attackSpeedMultiplier;

        return new AttackProfile
        {
            AttackRating = CombatRules.AttackRating(config.baseAttackRating, ratingMultiplier, weaponProgress, config.weaponSkillAttackScale, strength, config.strengthAttackScale),
            Damage = CombatRules.Damage(baseDamage, strength, config.strengthDamageScale, weaponProgress, config.weaponSkillDamageScale),
            Range = Mathf.Max(0.2f, range),
            Interval = CombatRules.AttackInterval(config.baseAttackInterval, config.minAttackInterval, speed, GetInitiativeMultiplier(actor, config)),
            UsedWeaponSkill = weaponSkill
        };
    }

    public static float GetInitiativeMultiplier(ICombatActor actor, CombatFormulaConfig config)
    {
        return CombatRules.Initiative(
            GetSkillProgress(actor, config.initiativeSkill), config.initiativeSkillScale,
            GetSkillProgress(actor, config.strengthSkill), config.initiativeStrengthScale);
    }

    /// <summary>The shield the actor carries, or null.</summary>
    public static ShieldItem GetShield(ICombatActor actor)
    {
        if (!actor.CanUseEquipment || actor.Equipment == null)
            return null;

        return actor.Equipment.GetEquipped(EquipSlot.Shield) as ShieldItem;
    }

    /// <summary>The defender's shield if an attack from the attacker's position falls inside its arc, otherwise null.</summary>
    public static ShieldItem GetCoveringShield(ICombatActor defender, ICombatActor attacker)
    {
        if (attacker == null || attacker.ActorTransform == null)
            return GetShield(defender);

        return GetCoveringShield(defender, attacker.ActorTransform.position);
    }

    /// <summary>The defender's shield if an attack coming from the given point falls inside its arc, otherwise null.</summary>
    public static ShieldItem GetCoveringShield(ICombatActor defender, Vector3 attackOrigin)
    {
        ShieldItem shield = GetShield(defender);
        if (shield == null || defender.ActorTransform == null)
            return shield;

        Vector3 toAttacker = attackOrigin - defender.ActorTransform.position;
        toAttacker.y = 0f;
        Vector3 facing = defender.ActorTransform.forward;
        facing.y = 0f;
        return Vector3.Angle(facing, toAttacker) <= shield.coverageAngle * 0.5f ? shield : null;
    }

    public static float GetBlockRating(ICombatActor actor, CombatFormulaConfig config, ShieldItem coveringShield = null)
    {
        return CombatRules.DefenseRating(actor.CanBlock, config.blockBaseRating, GetSkillProgress(actor, config.blockSkill), config.blockSkillScale,
            coveringShield != null ? coveringShield.blockRatingMultiplier : 1f);
    }

    public static float GetDodgeRating(ICombatActor actor, CombatFormulaConfig config)
    {
        return CombatRules.DefenseRating(actor.CanDodge, config.dodgeBaseRating, GetSkillProgress(actor, config.dodgeSkill), config.dodgeSkillScale);
    }

    public static float GetCounterRating(ICombatActor actor, CombatFormulaConfig config)
    {
        return CombatRules.DefenseRating(actor.CanCounter, config.counterBaseRating, GetSkillProgress(actor, config.counterSkill), config.counterSkillScale);
    }

    public static float GetDefenseRating(ICombatActor defender, CombatReactionType reaction, CombatFormulaConfig config, ShieldItem coveringShield = null)
    {
        switch (reaction)
        {
            case CombatReactionType.Block: return GetBlockRating(defender, config, coveringShield);
            case CombatReactionType.Dodge: return GetDodgeRating(defender, config);
            case CombatReactionType.Counter: return GetCounterRating(defender, config);
            default: return 0f;
        }
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
    /// defender is more skilled at (a master's weight is doubled) and toward blocking when a
    /// shield covers the attack.
    /// </summary>
    public static CombatReactionType ChooseReaction(ICombatActor defender, CombatFormulaConfig config, ShieldItem coveringShield = null)
    {
        return CombatRules.ChooseReaction(
            CombatRules.ReactionWeight(defender.CanBlock, config.blockWeight, GetSkillProgress(defender, config.blockSkill),
                coveringShield != null ? coveringShield.blockWeightMultiplier : 1f),
            CombatRules.ReactionWeight(defender.CanDodge, config.dodgeWeight, GetSkillProgress(defender, config.dodgeSkill)),
            CombatRules.ReactionWeight(defender.CanCounter, config.counterWeight, GetSkillProgress(defender, config.counterSkill)),
            Random.value);
    }

    /// <summary>The bow the actor is holding, or null.</summary>
    public static BowItem GetBow(ICombatActor actor)
    {
        return GetWeapon(actor) as BowItem;
    }

    /// <summary>Widest a shot can stray from the aim point, in degrees. Bow skill and the aim skill narrow it, a moving target widens it.</summary>
    public static float GetBowSpread(ICombatActor actor, BowItem bow, CombatFormulaConfig config, float targetSpeed)
    {
        return CombatRules.BowSpread(
            GetSkillProgress(actor, bow.weaponSkill), GetSkillProgress(actor, config.aimSkill), config.aimSkillShare,
            bow.untrainedSpread, bow.masterSpread, bow.movingTargetSpread, targetSpeed, bow.movingTargetSpeed);
    }

    /// <summary>Rough chance (0–1) that a shot lands on a target of the given radius.</summary>
    public static float GetBowHitChance(ICombatActor actor, BowItem bow, CombatFormulaConfig config, float distance, float targetSpeed, float targetRadius)
    {
        return CombatRules.BowHitChance(GetBowSpread(actor, bow, config, targetSpeed), distance, targetRadius);
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

        return CombatRules.SkillProgress(actor.Skills.GetBonus(skill));
    }
}
