using UnityEngine;

/// <summary>
/// Backward compatibility wrapper for AnimalPredatorAI.
///
/// This component maintains the public interface of the original WolfAI
/// while delegating all behavior to AnimalPredatorAI, allowing existing
/// Wolf prefabs and scene references to continue working unchanged.
///
/// The wrapper ensures AnimalPredatorAI is present on the same GameObject
/// and coordinates configuration to bridge Wolf-specific fields to the generic base.
/// </summary>
public class WolfAI : MonoBehaviour, ICombatActor
{
    // ------------------------------------------------------------------ //
    //  Inspector  (preserved for backward compat with existing prefabs)
    // ------------------------------------------------------------------ //

    [SerializeField] private AnimalPersonality personality;
    [SerializeField] private WolfBehaviorProfile behaviorProfile;
    [SerializeField] private CombatFormulaConfig combatFormulaConfig;
    [SerializeField] private bool retaliateWhenAttacked = true;
    [SerializeField, Min(0f)] private float retaliationMemorySeconds = 8f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    // ------------------------------------------------------------------ //
    //  State
    // ------------------------------------------------------------------ //

    private AnimalPredatorAI predatorAI;

    public Transform ActorTransform => predatorAI != null ? predatorAI.ActorTransform : transform;
    public CharacterStats Stats => predatorAI != null ? predatorAI.Stats : null;
    public SkillSet Skills => predatorAI != null ? predatorAI.Skills : null;
    public EquipmentManager Equipment => predatorAI != null ? predatorAI.Equipment : null;
    public bool CanUseEquipment => predatorAI != null && predatorAI.CanUseEquipment;
    public bool CanBlock => predatorAI != null && predatorAI.CanBlock;
    public bool CanCounter => predatorAI != null && predatorAI.CanCounter;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void OnEnable()
    {
        // Ensure AnimalPredatorAI component exists
        predatorAI = GetComponent<AnimalPredatorAI>();
        if (predatorAI == null)
        {
            predatorAI = gameObject.AddComponent<AnimalPredatorAI>();
        }

        // Sync configuration from WolfAI fields to AnimalPredatorAI fields
        SyncConfiguration();
    }

    private void OnDisable()
    {
        // Cleanup handled by AnimalPredatorAI
    }

    // ------------------------------------------------------------------ //
    //  Configuration Sync
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Mirror WolfAI's inspector fields to AnimalPredatorAI via reflection.
    /// This ensures configuration set on the WolfAI component reaches the underlying predator AI.
    /// </summary>
    private void SyncConfiguration()
    {
        if (predatorAI == null)
            return;

        var predatorFieldInfo = typeof(AnimalPredatorAI).GetField(
            "personality",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var profileFieldInfo = typeof(AnimalPredatorAI).GetField(
            "behaviorProfile",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var combatFieldInfo = typeof(AnimalPredatorAI).GetField(
            "combatFormulaConfig",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var retaliateFieldInfo = typeof(AnimalPredatorAI).GetField(
            "retaliateWhenAttacked",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var retaliationMemoryFieldInfo = typeof(AnimalPredatorAI).GetField(
            "retaliationMemorySeconds",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        var gizmosFieldInfo = typeof(AnimalPredatorAI).GetField(
            "drawGizmos",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (predatorFieldInfo != null)
            predatorFieldInfo.SetValue(predatorAI, personality);
        if (profileFieldInfo != null)
            profileFieldInfo.SetValue(predatorAI, behaviorProfile);
        if (combatFieldInfo != null)
            combatFieldInfo.SetValue(predatorAI, combatFormulaConfig);
        if (retaliateFieldInfo != null)
            retaliateFieldInfo.SetValue(predatorAI, retaliateWhenAttacked);
        if (retaliationMemoryFieldInfo != null)
            retaliationMemoryFieldInfo.SetValue(predatorAI, retaliationMemorySeconds);
        if (gizmosFieldInfo != null)
            gizmosFieldInfo.SetValue(predatorAI, drawGizmos);
    }

    // ------------------------------------------------------------------ //
    //  ICombatActor delegation
    // ------------------------------------------------------------------ //

    // All ICombatActor interface members are forwarded to predatorAI via properties above.
}

