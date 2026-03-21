using UnityEngine;

/// <summary>
/// One asset per skill. Configure the XP curve and bonus curve in the Inspector.
/// Create via Assets > Create > Skills > Skill Definition.
/// </summary>
[CreateAssetMenu(fileName = "NewSkillDefinition", menuName = "Skills/Skill Definition")]
public class SkillDefinition : ScriptableObject
{
    public SkillType skillType;
    public string displayName;

    [Range(1, 100)]
    public int maxLevel = 100;

    [Tooltip("X = current level (0–100), Y = XP required to reach the NEXT level.\n" +
             "Example: level 0 needs 100 XP, level 99 needs 10 000 XP.")]
    public AnimationCurve xpPerLevel = AnimationCurve.Linear(0, 100, 100, 10000);

    [Tooltip("X = current level (0–100), Y = bonus multiplier.\n" +
             "1.0 = no bonus, 2.0 = double damage/speed/yield, etc.")]
    public AnimationCurve bonusCurve = AnimationCurve.Linear(0, 1f, 100, 2f);

    /// <summary>XP required to advance from <paramref name="level"/> to the next level.</summary>
    public float GetXPRequired(int level) => xpPerLevel.Evaluate(level);

    /// <summary>Bonus multiplier at the given level (read by combat, movement, harvesting, etc.).</summary>
    public float GetBonus(int level) => bonusCurve.Evaluate(level);
}
