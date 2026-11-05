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

        // A bow's draw weight sets its power, so an archer's Strength adds nothing to a shot.
        float strength = weapon is BowItem ? 0f : GetSkillProgress(actor, config.strengthSkill);
        float weaponProgress = GetSkillProgress(actor, weaponSkill);

        float attackRating = config.baseAttackRating * ratingMultiplier
            * (1f + weaponProgress * config.weaponSkillAttackScale + strength * config.strengthAttackScale);

        float damage = baseDamage
            * (1f + strength * config.strengthDamageScale + weaponProgress * config.weaponSkillDamageScale);

        float initiative = GetInitiativeMultiplier(actor, config);

        ShieldItem carriedShield = GetShield(actor);
        if (carriedShield != null)
            speed *= carriedShield.attackSpeedMultiplier;

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
        if (!actor.CanBlock)
            return 0f;

        float block = config.blockBaseRating * (1f + GetSkillProgress(actor, config.blockSkill) * config.blockSkillScale);

        if (coveringShield != null)
            block *= coveringShield.blockRatingMultiplier;

        return block;
    }

    public static float GetDodgeRating(ICombatActor actor, CombatFormulaConfig config)
    {
        if (!actor.CanDodge)
            return 0f;

        return config.dodgeBaseRating * (1f + GetSkillProgress(actor, config.dodgeSkill) * config.dodgeSkillScale);
    }

    public static float GetCounterRating(ICombatActor actor, CombatFormulaConfig config)
    {
        if (!actor.CanCounter)
            return 0f;

        return config.counterBaseRating * (1f + GetSkillProgress(actor, config.counterSkill) * config.counterSkillScale);
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
    /// defender is more skilled at (a master's weight is doubled) and toward blocking when a
    /// shield covers the attack.
    /// </summary>
    public static CombatReactionType ChooseReaction(ICombatActor defender, CombatFormulaConfig config, ShieldItem coveringShield = null)
    {
        float blockWeight = defender.CanBlock ? config.blockWeight * (1f + GetSkillProgress(defender, config.blockSkill)) : 0f;
        if (coveringShield != null)
            blockWeight *= coveringShield.blockWeightMultiplier;
        float dodgeWeight = defender.CanDodge ? config.dodgeWeight * (1f + GetSkillProgress(defender, config.dodgeSkill)) : 0f;
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

    /// <summary>The bow the actor is holding, or null.</summary>
    public static BowItem GetBow(ICombatActor actor)
    {
        return GetWeapon(actor) as BowItem;
    }

    /// <summary>Widest a shot can stray from the aim point, in degrees. Bow skill and the aim skill narrow it, a moving target widens it.</summary>
    public static float GetBowSpread(ICombatActor actor, BowItem bow, CombatFormulaConfig config, float targetSpeed)
    {
        float skill = Mathf.Lerp(GetSkillProgress(actor, bow.weaponSkill), GetSkillProgress(actor, config.aimSkill), config.aimSkillShare);
        float spread = Mathf.Lerp(bow.untrainedSpread, bow.masterSpread, skill);
        return spread + bow.movingTargetSpread * Mathf.Clamp01(targetSpeed / bow.movingTargetSpeed);
    }

    /// <summary>Rough chance (0–1) that a shot lands on a target of the given radius: how much of the spread the target fills.</summary>
    public static float GetBowHitChance(ICombatActor actor, BowItem bow, CombatFormulaConfig config, float distance, float targetSpeed, float targetRadius)
    {
        float spread = GetBowSpread(actor, bow, config, targetSpeed);
        if (spread <= 0f)
            return 1f;

        float targetAngle = Mathf.Atan2(targetRadius, Mathf.Max(0.01f, distance)) * Mathf.Rad2Deg;
        float fill = targetAngle / spread;
        return Mathf.Clamp01(fill * fill);
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
