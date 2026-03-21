using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach to the Skills UI panel GameObject.
/// For each skill, assign its Image (set Image Type = Filled) and TMP_Text in the Inspector list.
/// Call Bind(skillSet) when a character is selected, Unbind() when deselected.
/// </summary>
public class SkillsPanel : MonoBehaviour
{
    [Serializable]
    public class SkillUIEntry
    {
        public SkillType skillType;

        [Tooltip("Image with Image Type = Filled. fillAmount shows XP progress toward next level.")]
        public Image xpFill;

        [Tooltip("TMP_Text that shows the current level number.")]
        public TMP_Text levelText;
    }

    [SerializeField] private List<SkillUIEntry> entries = new List<SkillUIEntry>();

    private SkillSet bound;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Bind to a character's SkillSet and start displaying its values.</summary>
    public void Bind(SkillSet skillSet)
    {
        if (bound != null)
            bound.OnSkillLevelUp -= OnLevelUp;

        bound = skillSet;

        if (bound != null)
        {
            bound.OnSkillLevelUp += OnLevelUp;
            RefreshAll();
        }
    }

    /// <summary>Detach from the current character.</summary>
    public void Unbind()
    {
        if (bound != null)
            bound.OnSkillLevelUp -= OnLevelUp;

        bound = null;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void OnDestroy() => Unbind();

    void Update()
    {
        if (bound == null) return;

        // Refresh both the XP fill and the level text every frame.
        // This avoids any missed Bind/RefreshAll call from producing stale or blank UI.
        foreach (var e in entries)
        {
            int level = bound.GetLevel(e.skillType);

            if (e.levelText != null)
                e.levelText.text = level.ToString();

            if (e.xpFill != null)
            {
                float needed  = bound.GetXPToNextLevel(e.skillType);
                float current = bound.GetCurrentXP(e.skillType);
                e.xpFill.fillAmount = needed > 0f ? Mathf.Clamp01(current / needed) : 1f;
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void RefreshAll()
    {
        foreach (var e in entries)
            RefreshEntry(e);
    }

    private void RefreshEntry(SkillUIEntry e)
    {
        if (bound == null) return;

        int level = bound.GetLevel(e.skillType);

        if (e.levelText != null)
            e.levelText.text = level.ToString();

        if (e.xpFill != null)
        {
            float needed = bound.GetXPToNextLevel(e.skillType);
            float current = bound.GetCurrentXP(e.skillType);
            e.xpFill.fillAmount = needed > 0f ? Mathf.Clamp01(current / needed) : 1f;
        }
    }

    private void OnLevelUp(SkillType skillType, int newLevel)
    {
        var e = entries.Find(x => x.skillType == skillType);
        if (e != null) RefreshEntry(e);
    }
}
