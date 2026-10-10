namespace OldenEra.RetaliationHints;

// Pure health arithmetic. Full stacks in the game exclude the wounded top creature.
public readonly record struct StackHealth(int Count, int Hp, int TopHp)
{
    public long Total => Count <= 0 ? 0 : (long)(Count - 1) * Hp + TopHp;
    public StackHealth Hit(int damage)
    {
        long remaining = Math.Max(0, Total - Math.Max(0, damage));
        if (remaining == 0) return new(0, Hp, 0);
        return new((int)((remaining - 1) / Hp + 1), Hp, (int)((remaining - 1) % Hp + 1));
    }
}

public readonly record struct DamageRange(int Min, int Max)
{
    public bool Valid => Min >= 0 && Max >= Min;
}
public readonly record struct LossRange(int Min, int Max, bool BeforeAttack)
{
    public string Numbers => Min == Max ? Min.ToString() : $"{Min}-{Max}";
}

public static class ForecastMath
{
    // Evaluate both outgoing endpoints, then both retaliation endpoints. In the normal
    // order the stronger opening hit leaves fewer creatures available to retaliate.
    public static LossRange Evaluate(StackHealth attacker, StackHealth defender, bool canCounter,
        bool beforeAttack, Func<StackHealth, StackHealth, bool, DamageRange> damage)
    {
        Validate(attacker); Validate(defender);
        if (!canCounter || attacker.Count == 0 || defender.Count == 0) return new(0, 0, beforeAttack);
        if (beforeAttack)
        {
            var reply = damage(defender, attacker, true); Check(reply);
            return new(attacker.Count - attacker.Hit(reply.Min).Count,
                       attacker.Count - attacker.Hit(reply.Max).Count, true);
        }
        var first = damage(attacker, defender, false); Check(first);
        int min = attacker.Count, max = 0;
        foreach (int opening in new[] { first.Min, first.Max })
        {
            var survivors = defender.Hit(opening);
            if (survivors.Count == 0) { min = 0; continue; }
            var reply = damage(survivors, attacker, true); Check(reply);
            min = Math.Min(min, attacker.Count - attacker.Hit(reply.Min).Count);
            max = Math.Max(max, attacker.Count - attacker.Hit(reply.Max).Count);
        }
        return new(min, max, false);
    }
    private static void Check(DamageRange range)
    {
        if (!range.Valid) throw new ArgumentOutOfRangeException(nameof(range));
    }
    private static void Validate(StackHealth state)
    {
        if (state.Count < 0 || state.Hp <= 0 || state.TopHp < 0 || state.TopHp > state.Hp
            || (state.Count > 0 && state.TopHp == 0)) throw new ArgumentOutOfRangeException(nameof(state));
    }
}
