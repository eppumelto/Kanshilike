using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kenshi-style skill component. Attach to any character alongside CharacterStats.
/// Drag all SkillDefinition assets into the 'Definitions' list in the Inspector.
///
/// Usage:
///   skillSet.GainXP(SkillType.Axes, 10f);      // called by combat / harvesting code
///   float dmgMult = skillSet.GetBonus(SkillType.Axes);
///   int level     = skillSet.GetLevel(SkillType.Athletics);
/// </summary>
public class SkillSet : MonoBehaviour
{
    // ── Runtime entry ────────────────────────────────────────────────────────
    [Serializable]
    public class SkillEntry
    {
        public SkillDefinition definition;
        public int    level     = 0;
        public float  currentXP = 0f;
    }

    // ── Inspector ────────────────────────────────────────────────────────────
    [SerializeField] private List<SkillDefinition> definitions = new List<SkillDefinition>();

    // ── Runtime state ────────────────────────────────────────────────────────
    private readonly Dictionary<SkillType, SkillEntry> skills = new Dictionary<SkillType, SkillEntry>();

    /// <summary>Fired whenever a skill levels up. Args: (skillType, newLevel).</summary>
    public event Action<SkillType, int> OnSkillLevelUp;

    // ── Lifecycle ────────────────────────────────────────────────────────────
    void Awake()
    {
        foreach (var def in definitions)
        {
            if (def == null) continue;
            skills[def.skillType] = new SkillEntry { definition = def };
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Award XP to a skill. Handles multi-level-up in a single call.
    /// Safe to call even if the skill is not registered.
    /// </summary>
    public void GainXP(SkillType skillType, float amount)
    {
        if (!skills.TryGetValue(skillType, out var entry)) return;

        entry.currentXP += amount;

        // Multi-level-up support
        while (entry.level < entry.definition.maxLevel)
        {
            float needed = entry.definition.GetXPRequired(entry.level);
            if (entry.currentXP < needed) break;

            entry.currentXP -= needed;
            entry.level++;
            Debug.Log($"[Skills] {gameObject.name} — {entry.definition.displayName} reached level {entry.level}!");
            OnSkillLevelUp?.Invoke(skillType, entry.level);
        }
    }

    /// <summary>Current level (0 – maxLevel) of the given skill.</summary>
    public int GetLevel(SkillType skillType) =>
        skills.TryGetValue(skillType, out var e) ? e.level : 0;

    /// <summary>
    /// Bonus multiplier at the current level, driven by the skill's bonusCurve.
    /// Returns 1.0 if the skill is not registered (no bonus = safe default).
    /// </summary>
    public float GetBonus(SkillType skillType) =>
        skills.TryGetValue(skillType, out var e) ? e.definition.GetBonus(e.level) : 1f;

    /// <summary>Current accumulated XP toward the next level.</summary>
    public float GetCurrentXP(SkillType skillType) =>
        skills.TryGetValue(skillType, out var e) ? e.currentXP : 0f;

    /// <summary>XP still required to reach the next level.</summary>
    public float GetXPToNextLevel(SkillType skillType) =>
        skills.TryGetValue(skillType, out var e) ? e.definition.GetXPRequired(e.level) : 0f;
}
