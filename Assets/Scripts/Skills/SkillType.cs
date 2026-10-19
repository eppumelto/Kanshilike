/// <summary>
/// All trainable skills. Add new entries here to extend the system —
/// then create a matching SkillDefinition asset in Assets/Skills/Data/.
///
/// Unity serializes enums by their numeric value, so every entry has an explicit
/// number. Never change or reuse an existing number; give new skills the next free one.
/// </summary>
public enum SkillType
{
    Unarmed    = 0,
    Axes       = 1,
    Strength   = 2,
    Athletics  = 3,
    Harvesting = 4,
    Dexterity  = 5,
    Block      = 6,
    Spears     = 7,
    Swords     = 8,
    Blunt      = 9,
}
