using UnityEngine;

public struct AttackProfile
{
    public float AttackPower;
    public float Damage;
    public float Range;
    public float Interval;
    public SkillType UsedWeaponSkill;
}

public static class CombatFormulaResolver
{
    public static AttackProfile BuildAttackProfile(ICombatActor actor, CombatFormulaConfig config)
    {
        WeaponItem weapon = GetWeapon(actor);
        SkillType weaponSkill = weapon != null ? weapon.weaponSkill : config.unarmedSkill;

        float strengthBonus = GetSkillBonus(actor, config.strengthSkill);
        float weaponBonus = GetSkillBonus(actor, weaponSkill);

        float baseDamage = weapon != null ? weapon.damage : config.baseUnarmedDamage;
        float range = weapon != null ? weapon.attackRange : config.baseUnarmedRange;
        float weaponSpeed = weapon != null ? weapon.attackSpeedMultiplier : 1f;
        float initiative = GetInitiativeMultiplier(actor, config);

        float attackPower = baseDamage
            + (strengthBonus * config.strengthAttackWeight)
            + (weaponBonus * config.weaponSkillAttackWeight);

        return new AttackProfile
        {
            AttackPower = Mathf.Max(1f, attackPower),
            Damage = Mathf.Max(1f, baseDamage + strengthBonus * 0.15f),
            Range = Mathf.Max(0.2f, range),
            Interval = Mathf.Max(config.minAttackInterval, config.baseAttackInterval / Mathf.Max(0.1f, weaponSpeed * initiative)),
            UsedWeaponSkill = weaponSkill
        };
    }

    public static float GetInitiativeMultiplier(ICombatActor actor, CombatFormulaConfig config)
    {
        float initiativeSkillBonus = GetSkillBonus(actor, config.initiativeSkill);
        float strengthBonus = GetSkillBonus(actor, config.strengthSkill);

        float initiative = 1f
            + (initiativeSkillBonus * config.initiativeSkillWeight)
            + (strengthBonus * config.initiativeStrengthWeight);

        return Mathf.Max(0.1f, initiative);
    }

    public static float GetBlockPower(ICombatActor actor, CombatFormulaConfig config)
    {
        if (!actor.CanBlock)
            return 0f;

        float block = config.blockBasePower + GetSkillBonus(actor, config.blockSkill) * config.blockSkillWeight;

        if (actor.CanUseEquipment && actor.Equipment != null && actor.Equipment.GetEquipped(EquipSlot.Shield) != null)
            block += config.shieldBlockBonus;

        return Mathf.Max(0f, block);
    }

    public static float GetDodgePower(ICombatActor actor, CombatFormulaConfig config)
    {
        float dodge = config.dodgeBasePower + GetSkillBonus(actor, config.dodgeSkill) * config.dodgeSkillWeight;
        return Mathf.Max(0f, dodge);
    }

    public static float GetCounterPower(ICombatActor actor, CombatFormulaConfig config)
    {
        if (!actor.CanCounter)
            return 0f;

        float counter = config.counterBasePower + GetSkillBonus(actor, config.counterSkill) * config.counterSkillWeight;
        return Mathf.Max(0f, counter);
    }

    public static CombatReactionType ChooseReaction(ICombatActor defender, CombatFormulaConfig config)
    {
        float blockWeight = defender.CanBlock ? config.blockWeight * (1f + GetBlockPower(defender, config) * 0.05f) : 0f;
        float dodgeWeight = config.dodgeWeight * (1f + GetDodgePower(defender, config) * 0.05f);
        float counterWeight = defender.CanCounter ? config.counterWeight * (1f + GetCounterPower(defender, config) * 0.05f) : 0f;

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

    private static float GetSkillBonus(ICombatActor actor, SkillType skill)
    {
        return actor.Skills != null ? actor.Skills.GetBonus(skill) : 1f;
    }
}
