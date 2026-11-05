using UnityEngine;
using UnityEngine.AI;

public enum AnimalChaseStepResult
{
    Continue,
    ReturnToIdle
}

public static class AnimalChaseCombatExecutor
{
    public static AnimalChaseStepResult Execute(
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
            return AnimalChaseStepResult.ReturnToIdle;

        float dist = Vector3.Distance(attackerObject.transform.position, detectedTarget.position);
        if (AnimalDecisionUtility.ShouldGiveUpChase(dist, personality.chaseGiveUpDistance))
            return AnimalChaseStepResult.ReturnToIdle;

        if (combatFormulaConfig == null)
        {
            // No combat configured: just follow the target.
            agent.SetDestination(detectedTarget.position);
            return AnimalChaseStepResult.Continue;
        }

        if (!reservedTargetSlot.TryReserve(attackerObject, detectedCharacter))
        {
            // Queue full: keep pressure while waiting for an active slot.
            agent.SetDestination(detectedTarget.position);
            return AnimalChaseStepResult.Continue;
        }

        AttackProfile attack = CombatFormulaResolver.BuildAttackProfile(attacker, combatFormulaConfig);
        if (dist > attack.Range)
        {
            agent.SetDestination(detectedTarget.position);
            return AnimalChaseStepResult.Continue;
        }

        agent.ResetPath();
        CombatFacing.TurnToward(attackerObject.transform, detectedTarget.position);
        if (Time.time < nextAttackTime)
            return AnimalChaseStepResult.Continue;

        CombatExchangeResolver.ResolveAndApply(
            attack,
            attacker,
            detectedCharacter,
            detectedCharacter.Stats,
            combatFormulaConfig,
            attackerObject);

        nextAttackTime = Time.time + attack.Interval;
        return AnimalChaseStepResult.Continue;
    }
}
