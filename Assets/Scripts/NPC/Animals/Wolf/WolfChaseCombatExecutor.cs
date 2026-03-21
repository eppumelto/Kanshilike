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
        if (combatFormulaConfig == null)
        {
            agent.SetDestination(detectedTarget != null ? detectedTarget.position : attackerObject.transform.position);
            return WolfChaseStepResult.Continue;
        }

        if (detectedTarget == null || detectedCharacter == null || detectedCharacter.Stats == null || !detectedCharacter.Stats.IsAlive)
            return WolfChaseStepResult.ReturnToIdle;

        float dist = Vector3.Distance(attackerObject.transform.position, detectedTarget.position);
        if (WolfDecisionUtility.ShouldGiveUpChase(dist, personality.chaseGiveUpDistance))
            return WolfChaseStepResult.ReturnToIdle;

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
        if (Time.time < nextAttackTime)
            return WolfChaseStepResult.Continue;

        CombatExchangeResolver.ResolveAndApply(
            attack,
            attacker,
            detectedCharacter,
            detectedCharacter.Stats,
            combatFormulaConfig,
            attackerObject);

        nextAttackTime = Time.time + attack.Interval;
        return WolfChaseStepResult.Continue;
    }
}
