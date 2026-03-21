using UnityEngine;

public readonly struct CombatExchangeResult
{
    public CombatReactionType Reaction { get; }
    public bool HitLanded { get; }
    public float DamageDealt { get; }
    public float CounterDamage { get; }

    public CombatExchangeResult(CombatReactionType reaction, bool hitLanded, float damageDealt, float counterDamage)
    {
        Reaction = reaction;
        HitLanded = hitLanded;
        DamageDealt = damageDealt;
        CounterDamage = counterDamage;
    }
}

public static class CombatExchangeResolver
{
    public static CombatExchangeResult ResolveAndApply(
        AttackProfile attack,
        ICombatActor attacker,
        ICombatActor defender,
        CharacterStats defenderStats,
        CombatFormulaConfig config,
        GameObject attackSource,
        System.Action<CombatReactionType> onReactionDecided = null)
    {
        if (attacker == null || defender == null || defenderStats == null || config == null || !defenderStats.IsAlive)
            return new CombatExchangeResult(CombatReactionType.None, false, 0f, 0f);

        CombatReactionType reaction = CombatFormulaResolver.ChooseReaction(defender, config);
        onReactionDecided?.Invoke(reaction);

        float attackRoll = attack.AttackPower + Random.Range(0f, 3f);
        bool hitLanded = true;
        float counterDamage = 0f;

        switch (reaction)
        {
            case CombatReactionType.Block:
            {
                float blockPower = CombatFormulaResolver.GetBlockPower(defender, config) + Random.Range(0f, 2f);
                hitLanded = attackRoll > blockPower;
                break;
            }
            case CombatReactionType.Dodge:
            {
                float dodgePower = CombatFormulaResolver.GetDodgePower(defender, config) + Random.Range(0f, 2f);
                hitLanded = attackRoll > dodgePower;
                break;
            }
            case CombatReactionType.Counter:
            {
                float counterPower = CombatFormulaResolver.GetCounterPower(defender, config) + Random.Range(0f, 2f);
                bool counterSucceeded = counterPower >= attackRoll;
                if (counterSucceeded)
                {
                    hitLanded = false;
                    counterDamage = Mathf.Max(1f, counterPower * 0.5f);
                    attacker.Stats?.ReceiveDamage(counterDamage, defender.ActorTransform != null ? defender.ActorTransform.gameObject : null);
                }
                break;
            }
        }

        if (hitLanded)
        {
            GameObject source = attackSource;
            if (source == null && attacker.ActorTransform != null)
                source = attacker.ActorTransform.gameObject;

            defenderStats.ReceiveDamage(attack.Damage, source);
            return new CombatExchangeResult(reaction, true, attack.Damage, counterDamage);
        }

        return new CombatExchangeResult(reaction, false, 0f, counterDamage);
    }
}
