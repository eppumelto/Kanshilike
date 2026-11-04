using UnityEngine;
using UnityEngine.AI;

public enum WolfChaseStepResult
{
    Continue,
    ReturnToIdle
}

public static class WolfChaseCombatExecutor
{
    public static WolfChaseStepResult Execute(
        ICombatActor attacker,
        GameObject attackerObject,
        NavMeshAgent agent,
        AnimalPersonality personality,
        CombatFormulaConfig combatFormulaConfig,
        CombatSlotReservation reservedTargetSlot,
        Transform detectedTarget,
        SelectableCharacter detectedCharacter,
        ref float nextAttackTime)
    {
        if (detectedTarget == null || detectedCharacter == null || detectedCharacter.Stats == null || !detectedCharacter.Stats.IsAlive)
            return WolfChaseStepResult.ReturnToIdle;

        float dist = Vector3.Distance(attackerObject.transform.position, detectedTarget.position);
        if (WolfDecisionUtility.ShouldGiveUpChase(dist, personality.chaseGiveUpDistance))
            return WolfChaseStepResult.ReturnToIdle;

        if (combatFormulaConfig == null)
        {
            // No combat configured: just follow the target.
            agent.SetDestination(detectedTarget.position);
            return WolfChaseStepResult.Continue;
        }

        if (!reservedTargetSlot.TryReserve(attackerObject, detectedCharacter))
        {
            // Queue full: keep pressure while waiting for an active slot.
            agent.SetDestination(detectedTarget.position);
            return WolfChaseStepResult.Continue;
        }

        AttackProfile attack = CombatFormulaResolver.BuildAttackProfile(attacker, combatFormulaConfig);
        if (dist > attack.Range)
        {
            agent.SetDestination(detectedTarget.position);
            return WolfChaseStepResult.Continue;
        }

        agent.ResetPath();
        CombatFacing.TurnToward(attackerObject.transform, detectedTarget.position);
        if (Time.time < nextAttackTime)
            return WolfChaseStepResult.Continue;

        CombatExchangeResult result = CombatExchangeResolver.ResolveAndApply(
            attack,
            attacker,
            detectedCharacter,
            detectedCharacter.Stats,
            combatFormulaConfig,
            attackerObject);

        Debug.Log(
            $"[Combat] {attackerObject.name} attacked {detectedTarget.name}: " +
            (result.HitLanded
                ? $"hit for {result.DamageDealt:F1} ({result.Reaction} failed)."
                : $"prevented by {result.Reaction}, counter damage {result.CounterDamage:F1}."),
            attackerObject);

        nextAttackTime = Time.time + attack.Interval;
        return WolfChaseStepResult.Continue;
    }
}
