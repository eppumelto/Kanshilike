using UnityEngine;

public sealed class CombatSlotReservation
{
    private AttackerSlotController reservedSlots;
    private Component reservedTarget;

    public bool TryReserve(GameObject attacker, Component targetComponent)
    {
        AttackerSlotController slotController = targetComponent != null
            ? targetComponent.GetComponentInParent<AttackerSlotController>()
            : null;

        if (slotController == null)
        {
            Release(attacker);
            return true;
        }

        if (reservedSlots == slotController && reservedTarget == targetComponent)
            return slotController.IsActiveAttacker(attacker);

        Release(attacker);

        // Remember the target even when we only got a place in its queue, so Release can
        // take us back out of the queue. Otherwise an attacker that gives up while queued
        // is later promoted into an active slot it never uses.
        reservedSlots = slotController;
        reservedTarget = targetComponent;
        return slotController.TryEnterCombat(attacker);
    }

    public void Release(GameObject attacker)
    {
        if (reservedSlots == null)
            return;

        if (reservedSlots.IsActiveAttacker(attacker))
            reservedSlots.ExitCombat(attacker);
        else
            reservedSlots.RemoveQueued(attacker);

        reservedSlots = null;
        reservedTarget = null;
    }
}
