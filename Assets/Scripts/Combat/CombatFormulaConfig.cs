using UnityEngine;

/// <summary>
/// Tuning for melee combat.
///
/// Skills: a skill's SkillDefinition.bonusCurve gives 1.0 at level 0 and (by default) 2.0 at
/// level 100. The formulas use (bonus - 1) as "skill progress" (0 → 1), multiplied by the
/// *Scale values below. A scale of 1 means a master doubles that number; 0.5 means +50%.
///
/// Hits: the defender picks a reaction (block / dodge / counter). Its chance to succeed is
/// defense / (defense + attack), so equal ratings give 50%, and a rating twice as high wins 2 in 3.
/// </summary>
[CreateAssetMenu(fileName = "CombatFormulaConfig", menuName = "Combat/Formula Config")]
public class CombatFormulaConfig : ScriptableObject
{
    [Header("Unarmed")]
    [Min(0.1f)] public float baseUnarmedDamage = 5f;
    [Min(0.1f)] public float baseUnarmedRange = 1.4f;
    [Tooltip("Seconds between attacks before weapon speed and initiative.")]
    [Min(0.01f)] public float baseAttackInterval = 1.2f;
    [Min(0.1f)] public float minAttackInterval = 0.2f;

    [Header("Attack")]
    [Tooltip("Attack rating of an untrained attacker. Compared against block/dodge/counter ratings.")]
    [Min(0.1f)] public float baseAttackRating = 10f;
    [Tooltip("How much the weapon skill (or Unarmed) raises attack rating at level 100.")]
    [Min(0f)] public float weaponSkillAttackScale = 1f;
    [Tooltip("How much Strength raises attack rating at level 100.")]
    [Min(0f)] public float strengthAttackScale = 0.25f;
    [Tooltip("How much Strength raises damage at level 100.")]
    [Min(0f)] public float strengthDamageScale = 0.5f;
    [Tooltip("How much the weapon skill raises damage at level 100.")]
    [Min(0f)] public float weaponSkillDamageScale = 0.5f;

    [Header("Attack Speed")]
    public SkillType initiativeSkill = SkillType.Athletics;
    [Tooltip("How much the initiative skill speeds up attacks at level 100 (0.4 = 40% faster).")]
    [Min(0f)] public float initiativeSkillScale = 0.4f;
    [Min(0f)] public float initiativeStrengthScale = 0.2f;

    [Header("Defense")]
    [Min(0f)] public float blockBaseRating = 10f;
    [Min(0f)] public float dodgeBaseRating = 8f;
    [Min(0f)] public float counterBaseRating = 5f;
    [Min(0f)] public float blockSkillScale = 1f;
    [Min(0f)] public float dodgeSkillScale = 1f;
    [Min(0f)] public float counterSkillScale = 1f;
    [Tooltip("Block rating multiplier while a shield is equipped.")]
    [Min(1f)] public float shieldBlockMultiplier = 1.5f;
    [Tooltip("A successful counter deals this fraction of the defender's own damage to the attacker.")]
    [Min(0f)] public float counterDamageFraction = 0.5f;

    [Header("Skill Mapping")]
    public SkillType strengthSkill = SkillType.Strength;
    public SkillType unarmedSkill = SkillType.Unarmed;
    public SkillType dodgeSkill = SkillType.Athletics;
    public SkillType blockSkill = SkillType.Block;
    public SkillType counterSkill = SkillType.Dexterity;

    [Header("Reaction Weights")]
    [Tooltip("How often each reaction is picked, before skill tilts the choice toward the defender's best one.")]
    [Min(0f)] public float blockWeight = 1f;
    [Min(0f)] public float dodgeWeight = 1f;
    [Min(0f)] public float counterWeight = 0.6f;

    [Header("Defender XP")]
    [Tooltip("XP for the reaction's skill when the defender stops the attack.")]
    [Min(0f)] public float defenseSuccessXP = 6f;
    [Tooltip("XP for the reaction's skill when the attack gets through anyway.")]
    [Min(0f)] public float defenseFailXP = 2f;
}
