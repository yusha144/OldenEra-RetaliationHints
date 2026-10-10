namespace OldenEra.RetaliationHints;

// Pure health arithmetic. Full stacks in the game exclude the wounded top creature.
public readonly record struct StackHealth(int Count, int Hp, int TopHp, int Temporary = 0, bool TopTemporary = false)
{
    public long Total => Count <= 0 ? 0 : (long)(Count - 1) * Hp + TopHp;
    public StackHealth Hit(int damage)
    {
        long remaining = Math.Max(0, Total - Math.Max(0, damage));
        if (remaining == 0) return new(0, Hp, 0);
        int count = (int)((remaining - 1) / Hp + 1);
        int killed = Count - count;
        int temporary = Temporary - Math.Min(Temporary, Math.Max(0, killed - (TopTemporary ? 0 : 1)));
        return new(count, Hp, (int)((remaining - 1) % Hp + 1), temporary,
            killed == 0 ? TopTemporary : temporary > 0);
    }

    public StackHealth Heal(int amount, long limit, bool temporary)
    {
        if (Count == 0 || amount <= 0 || limit <= Total) return this;
        long total = Math.Min(limit, Total + amount);
        int count = checked((int)((total - 1) / Hp + 1));
        return new(count, Hp, (int)((total - 1) % Hp + 1), Temporary + (temporary ? count - Count : 0),
            count == Count ? TopTemporary : temporary);
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
        bool beforeAttack, Func<StackHealth, StackHealth, bool, DamageRange> damage,
        Func<StackHealth, int, StackHealth> afterOpening = null)
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
        if (first.Min >= defender.Total) return new(0, 0, false);
        int lastSurvivingRoll = (int)Math.Min(first.Max, defender.Total - 1);
        int min = first.Max >= defender.Total ? 0 : int.MaxValue, max = 0;
        // Healing changes the wounded creature's HP modulo max HP. Endpoints alone
        // can miss casualties in the middle. Check every possible opening roll for
        // this branch, with a strict work limit instead of a misleading sampled range.
        if (afterOpening != null && (long)lastSurvivingRoll - first.Min > 256)
            throw new ForecastUnavailableException("широкий разброс с лечением");
        IEnumerable<int> rolls = afterOpening == null ? new[] { first.Min, lastSurvivingRoll }
            : Enumerable.Range(first.Min, lastSurvivingRoll - first.Min + 1);
        foreach (int opening in rolls)
        {
            var survivors = defender.Hit(opening);
            if (survivors.Count == 0) { min = 0; continue; }
            var healed = afterOpening?.Invoke(attacker, (int)Math.Min(defender.Total, opening)) ?? attacker;
            Validate(healed);
            var reply = damage(survivors, healed, true); Check(reply);
            min = Math.Min(min, healed.Count - healed.Hit(reply.Min).Count);
            max = Math.Max(max, healed.Count - healed.Hit(reply.Max).Count);
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
            || state.Temporary < 0 || state.Temporary > state.Count
            || (state.TopTemporary && state.Temporary == 0)
            || (state.Count > 0 && state.TopHp == 0)) throw new ArgumentOutOfRangeException(nameof(state));
    }
}

public sealed class ForecastUnavailableException : NotSupportedException
{
    public ForecastUnavailableException(string reason) : base(reason) { }
}
