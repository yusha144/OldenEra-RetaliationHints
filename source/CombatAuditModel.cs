namespace OldenEra.RetaliationHints;

// Managed values only. Reports must never retain/serialize native game objects.
public sealed class AuditUnit
{
    public long Pointer { get; }
    public int Id { get; }
    public string Sid { get; }
    public int Hex { get; }
    public StackHealth Health { get; }
    public SortedDictionary<string, float> Stats { get; }
    public string[] Buffs { get; }
    public AuditUnit(long pointer, int id, string sid, int hex, StackHealth health,
        SortedDictionary<string, float> stats, string[] buffs) =>
        (Pointer, Id, Sid, Hex, Health, Stats, Buffs) = (pointer, id, sid, hex, health, stats, buffs);
}
public readonly record struct AuditKey(long Battle, int BattleId, long Attacker, long Defender,
    long Ability, int TargetHex, int Destination);
public readonly record struct AuditBranch(StackHealth Defender, StackHealth Attacker, DamageRange Damage);
public sealed class AuditTrace
{
    public List<AuditBranch> Branches { get; set; } = new();
}
public sealed class AuditPreview
{
    public AuditKey Key { get; }
    public AuditUnit Attacker { get; }
    public AuditUnit Defender { get; }
    public DamageRange Opening { get; }
    public LossRange? Losses { get; }
    public bool CanCounter { get; }
    public bool BeforeAttack { get; }
    public string Unavailable { get; }
    public AuditTrace Trace { get; }
    public AuditPreview(AuditKey key, AuditUnit attacker, AuditUnit defender, DamageRange opening,
        LossRange? losses, bool canCounter, bool beforeAttack, string unavailable, AuditTrace trace) =>
        (Key, Attacker, Defender, Opening, Losses, CanCounter, BeforeAttack, Unavailable, Trace) =
        (key, attacker, defender, opening, losses, canCounter, beforeAttack, unavailable, trace);
}
public readonly record struct AuditHit(string Phase, int InputDamage, int Damage, int Killed,
    bool Immune, string Mechanic, AuditUnit Source, AuditUnit Before, AuditUnit After);
public readonly record struct AuditVerdict(string Status, string Reason, int? ActualLosses = null,
    bool? LossesInRange = null, bool? OpeningDamageInRange = null, bool? OpeningStateMatches = null);

public sealed class AuditExchange
{
    public long Id { get; set; }
    public AuditPreview Preview { get; set; }
    public string PairingFailure { get; set; }
    public string ObserverFailure { get; set; }
    public bool OpeningStarted { get; set; }
    public bool CounterStarted { get; set; }
    public int CounterSteps { get; set; }
    public float? OpeningLuck { get; set; }
    public float? CounterLuck { get; set; }
    public string ActualCounter { get; set; }
    public AuditUnit DefenderBeforeCounter { get; set; }
    public AuditUnit AttackerBeforeCounter { get; set; }
    public List<AuditHit> Hits { get; set; } = new();
    public List<AuditStep> Steps { get; set; } = new();
    public bool CommandObserved { get; set; }
    public int OtherDamageEvents { get; set; }
    public bool MissingDamageEvent { get; set; }
    public bool UnattributedDirectDamage { get; set; }

    public AuditVerdict Evaluate(bool completed, string endReason)
    {
        if (ObserverFailure != null) return new("observer_error", ObserverFailure);
        if (!completed) return new("incomplete", endReason);
        if (Preview == null || PairingFailure != null)
            return new("unpaired", PairingFailure ?? "Нет показанного прогноза для этой атаки");
        if (Preview.BeforeAttack) return new("excluded_preemptive", "Упреждающая ответка пока вне сверки");
        // Read the factors already rolled by the game. Never roll or normalize them.
        if (NonNeutral(OpeningLuck) || (CounterStarted && NonNeutral(CounterLuck))
            || Steps.Any(s => (s.Kind == "opening" ? s.Started : s.Executed) && NonNeutral(s.Luck)))
            return new("excluded_luck", "Сработала удача или неудача; точность не оценивается");
        if (!OpeningStarted || !OpeningLuck.HasValue ||
            (CounterStarted && !CounterLuck.HasValue) || UnattributedDirectDamage)
            return new("unpaired", "Не подтверждена последовательность игровых ударов");
        if (Preview.Losses == null) return new("unavailable", Preview.Unavailable ?? "Нет числового прогноза");
        var opening = Hits.Where(h => h.Phase == "opening").ToArray();
        var counter = Hits.Where(h => h.Phase == "counter").ToArray();
        if (counter.Length != 0 && !CounterStarted)
            return new("unpaired", "Событие ответки не связано с подтверждённым выполнением удара");
        if (Hits.Any(h => h.Damage < 0 || h.InputDamage < 0 || h.Killed < 0))
            return new("observer_error", "Недопустимые значения в игровом событии урона");
        if (opening.Length > 1 || counter.Length > 1 || CounterSteps > 1
            || Steps.Count(s => s.Kind == "opening") > 1)
            return new("multiple_hits", "Несколько ударов; они не объединяются в одну ответку");
        if (MissingDamageEvent || opening.Length != 1 || (CounterStarted && counter.Length != 1))
            return new("missing_damage_event", "Нет полного события урона; отсутствие записи не означает нулевые потери");
        int losses = counter.Length == 0 ? 0 : counter[0].Killed;
        var expected = Preview.Losses.Value;
        bool lossOk = losses >= expected.Min && losses <= expected.Max && (Preview.CanCounter || !CounterStarted);
        var first = opening[0];
        // Real 0.1.11 recordings show full event damage, including overkill.
        // Only health subtraction is capped at the victim's remaining HP.
        bool openingOk = first.Damage >= Preview.Opening.Min && first.Damage <= Preview.Opening.Max;
        int ordinaryDamage = first.InputDamage;
        if (Preview.Defender.Stats.TryGetValue("maxInDmg", out float cap) && cap > 0)
            ordinaryDamage = Math.Min(ordinaryDamage, (int)cap);
        bool stateOk = Preview.Defender.Health.Hit(ordinaryDamage) == first.After.Health;
        return new(lossOk && openingOk && stateOk ? "matched" : "mismatch",
            lossOk && openingOk && stateOk ? "Наблюдавшийся результат входит в прогноз" : "Найдено расхождение",
            losses, lossOk, openingOk, stateOk);
    }

    private static bool NonNeutral(float? value) => value.HasValue &&
        (!float.IsFinite(value.Value) || Math.Abs(value.Value - 1f) > .00001f);
}

public static class AuditMatching
{
    public static bool Matches(AuditKey shown, AuditKey command, double ageSeconds) =>
        shown == command && ageSeconds >= 0 && ageSeconds <= 2;

    public static string Phase(long step, long openingStep, long counterStep,
        long caster, long receiver, long ability, long dealer,
        long attacker, long defender, long openingAbility, long openingDealer,
        long counterAbility, long counterDealer)
    {
        if (step != 0 && step == openingStep && caster == attacker && receiver == defender
            && ability == openingAbility && dealer != 0 && dealer == openingDealer) return "opening";
        if (step != 0 && step == counterStep && caster == defender && receiver == attacker
            && ability == counterAbility && dealer != 0 && dealer == counterDealer) return "counter";
        return "other";
    }
}
