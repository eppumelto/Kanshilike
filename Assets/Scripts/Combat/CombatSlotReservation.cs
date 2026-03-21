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
        if (!slotController.TryEnterCombat(attacker))
            return false;

        reservedSlots = slotController;
        reservedTarget = targetComponent;
        return true;
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
