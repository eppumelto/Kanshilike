using UnityEngine;

public interface ICombatActor
{
    Transform ActorTransform { get; }
    CharacterStats Stats { get; }
    SkillSet Skills { get; }
    EquipmentManager Equipment { get; }

    bool CanUseEquipment { get; }
    bool CanBlock { get; }
    bool CanCounter { get; }
}
