using UnityEngine;

[CreateAssetMenu(fileName = "CombatFormulaConfig", menuName = "Combat/Formula Config")]
public class CombatFormulaConfig : ScriptableObject
{
    [Header("General")]
    [Min(0.1f)] public float baseUnarmedDamage = 5f;
    [Min(0.1f)] public float baseUnarmedRange = 1.4f;
    [Min(0.01f)] public float baseAttackInterval = 1.2f;
    [Min(0.1f)] public float minAttackInterval = 0.2f;

    [Header("Attack Formula")]
    [Min(0f)] public float strengthAttackWeight = 0.25f;
    [Min(0f)] public float weaponSkillAttackWeight = 0.7f;

    [Header("Initiative Formula")]
    public SkillType initiativeSkill = SkillType.Athletics;
    [Min(0f)] public float initiativeSkillWeight = 0.4f;
    [Min(0f)] public float initiativeStrengthWeight = 0.2f;

    [Header("Defense Formula")]
    [Min(0f)] public float blockBasePower = 2f;
    [Min(0f)] public float dodgeBasePower = 2f;
    [Min(0f)] public float counterBasePower = 1f;
    [Min(0f)] public float blockSkillWeight = 0.5f;
    [Min(0f)] public float dodgeSkillWeight = 0.6f;
    [Min(0f)] public float counterSkillWeight = 0.45f;
    [Min(0f)] public float shieldBlockBonus = 3f;

    [Header("Skill Mapping")]
    public SkillType strengthSkill = SkillType.Strength;
    public SkillType unarmedSkill = SkillType.Unarmed;
    public SkillType dodgeSkill = SkillType.Athletics;
    public SkillType blockSkill = SkillType.Strength;
    public SkillType counterSkill = SkillType.Unarmed;

    [Header("Reaction Weights")]
    [Min(0f)] public float blockWeight = 1f;
    [Min(0f)] public float dodgeWeight = 1f;
    [Min(0f)] public float counterWeight = 0.6f;
}
