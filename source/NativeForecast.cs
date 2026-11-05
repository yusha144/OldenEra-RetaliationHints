using Hex.Configs;
using Hex.Session.Battle;
using BattleHex = Hex.Session.Battle.Hex;

namespace OldenEra.RetaliationHints;

internal static class NativeForecast
{
    internal static LossRange Calculate(PreviewCapture.Context context, DamageRange opening, AuditTrace trace = null)
    {
        var attacker = Health(context.Attacker);
        var defender = Health(context.Defender);
        bool before = context.Defender.bjof("counter_before_attack");
        if (!context.CanCounter) return new(0, 0, false);
        if (context.Counter?.clyt == null) throw Unavailable("нет данных контрудара");
        if (!Plugin.Instance.Patterns.TryGet(context.Counter.clyt.data.attackPatternSid, out float multiplier))
            throw Unavailable("особая форма контрудара");
        if (context.Attacker.stats.overkillProtection != 0 || context.Defender.stats.overkillProtection != 0)
            throw Unavailable("защита от гибели");
        CheckDirectEffects(context.Counter);
        if (!before)
        {
            CheckDirectEffects(context.Attack);
            var dealer = context.Attack.clyt?.data;
            if (!string.IsNullOrEmpty(dealer?.buff?.sid) || !string.IsNullOrEmpty(dealer?.sideBuff?.sid)
                || HasSideEffect(context.Attack.clyu?.data) || HasSideEffect(context.Attack.clyv?.data)
                || !string.IsNullOrEmpty(context.Attack.clyx.tempSelfBuff))
                throw Unavailable("эффект после попадания");
        }
        var healing = before ? null : LifeSteal(context, attacker);
        var cache = new Dictionary<(StackHealth, StackHealth), DamageRange>();

        return ForecastMath.Evaluate(attacker, defender, true, before, (sourceHealth, targetHealth, counter) =>
        {
            if (!counter) return opening;
            if (cache.TryGetValue((sourceHealth, targetHealth), out var cached)) return cached;
            var source = Snapshot(context.Defender, sourceHealth, context.Defender.cwoz);
            var target = Snapshot(context.Attacker, targetHealth, context.Destination);
            var ability = context.Counter.MemberwiseClone().Cast<ejh>();
            ability.clyo = source;
            var parameters = new DamageUiParams(source.Cast<ell>(), ability.Cast<elm>(), target.Cast<elp>(),
                multiplier, ESideCastType.Unit, 0);
            var forecast = exv.bjfp(parameters, true);
            if (forecast == null || !forecast.cpag || !forecast.cpap)
                throw Unavailable("нет прогноза контрудара");
            var range = new DamageRange(forecast.cpah, forecast.cpai);
            trace?.Branches.Add(new(sourceHealth, targetHealth, range));
            cache[(sourceHealth, targetHealth)] = range;
            return range;
        }, healing);
    }

    private static bool HasSideEffect(CastDealerConfig data)
    {
        if (data == null) return false;
        // The game normalizes missing side dealers into allocated empty configs.
        // Pattern/target metadata alone does not deal damage or apply an effect.
        return EffectContent.HasPayload(data.buff?.sid, data.sideBuff?.sid,
            data.targetMechanics?.Length > 0, data.damageTarget_,
            data.minBaseDmg, data.maxBaseDmg, data.minStackDmg, data.maxStackDmg,
            data.minDamagePerEnergy, data.maxDamagePerEnergy,
            data.minDamagePerEnergyLevel, data.maxDamagePerEnergyLevel,
            data.minStackPercentDmg, data.maxStackPercentDmg, data.minPercentDmg, data.maxPercentDmg,
            data.minStatDmg, data.maxStatDmg, data.minBuffNumDmg, data.maxBuffNumDmg,
            data.minBuffNumStackDmg, data.maxBuffNumStackDmg, data.minDebuffNumDmg,
            data.maxDebuffNumDmg, data.minDebuffNumStackDmg, data.maxDebuffNumStackDmg,
            data.levelDmg, data.statDmgMult);
    }

    // Reject direct on-hit mechanics we cannot simulate, rather than omitting a
    // guaranteed kill/drain/movement effect while displaying an ordinary number.
    private static void CheckDirectEffects(ejh ability)
    {
        var dealer = ability.clyt?.data;
        if (dealer?.targetMechanics != null && dealer.targetMechanics.Length > 0)
            throw Unavailable("особый эффект удара");
        if (ability.clyw != null)
            foreach (var mechanic in ability.clyw)
                if (mechanic != null && mechanic.cnsx != EMechType.Vampirism)
                    throw Unavailable("особый эффект атаки");
        // Self healing after the counter does not change casualties of that hit.
        // Opening self-healing is handled explicitly below.
    }

    private static Func<StackHealth, int, StackHealth> LifeSteal(PreviewCapture.Context context, StackHealth health)
    {
        var mechanics = context.Attack.clyw;
        if (mechanics == null || mechanics.Length == 0) return null;
        var active = new List<etz>();
        foreach (var mechanic in mechanics)
        {
            if (mechanic == null || mechanic.cnsx != EMechType.Vampirism) continue;
            var life = mechanic.TryCast<etz>();
            if (life == null || !life.cnsw.afterAttack || life.coby != ETargetAllegiance.Self)
                throw Unavailable("особое лечение после удара");
            // These are read-only native blocker checks, also used by etz.bhub.
            if (context.Attacker.cwoi.bfja(mechanic) || eqd.bhpp(context.Defender, mechanic)) continue;
            if (life.cobz != EHealType.WithTempStacks && life.cobz != EHealType.WithRealUntilStart
                && life.cobz != EHealType.WithoutRespawn)
                throw Unavailable("особое воскрешение");
            if (health.Temporary > 0 && life.cobz == EHealType.WithRealUntilStart)
                throw Unavailable("воскрешение с временными существами");
            if (!float.IsFinite(life.cobx) || life.cobx < 0 || life.cobw < 0)
                throw Unavailable("неизвестное лечение");
            active.Add(life);
        }
        if (active.Count == 0) return null;
        if (active.Count > 1) throw Unavailable("несколько источников лечения");
        // A splash attack can heal from multiple victims, not just the hovered one.
        if (!Plugin.Instance.Patterns.IsSingleDamageCell(context.Attack.clyt.data.attackPatternSid))
            throw Unavailable("лечение от нескольких целей");
        if (context.Attacker.stats.limitStacks) throw Unavailable("особый предел численности");
        var effect = active[0];
        return (state, inflicted) =>
        {
            // Match native order: truncate base heal, apply final-healing bonus,
            // then vampirism modifier and the game's health cap. No event execution.
            float raw = effect.cobw + inflicted * effect.cobx;
            if (!float.IsFinite(raw) || raw >= int.MaxValue) throw Unavailable("слишком большой прогноз");
            int amount = eqd.bhph(context.Attacker, (int)raw);
            float adjusted = amount * (1f + context.Attacker.stats.vampirismHealMultiplier);
            if (!float.IsFinite(adjusted) || adjusted >= int.MaxValue) throw Unavailable("неизвестное лечение");
            amount = Math.Max(0, (int)adjusted);
            // bjqx is the read-only cap function; do NOT call bjqy/bjqv (mutators).
            amount = ezu.bjrp(effect.cobz, context.Attacker.cphj, context.Attacker.stats, amount);
            if (state.Total + amount > int.MaxValue) throw Unavailable("слишком большой прогноз");
            return state.Heal(amount, int.MaxValue, effect.cobz == EHealType.WithTempStacks);
        };
    }

    private static ForecastUnavailableException Unavailable(string reason) => new(reason);

    internal static StackHealth Health(Unit unit)
    {
        var data = unit.cphj;
        return StackHealth.FromNativeCounts(data.fullStacks, data.tempFullStacks,
            data.cpmr, unit.stats.hp, data.cpmu);
    }

    private static Unit Snapshot(Unit original, StackHealth health, BattleHex position)
    {
        var copy = original.MemberwiseClone().Cast<Unit>();
        var data = original.cphj.MemberwiseClone().Cast<UnitData>();
        copy._cphj_k__BackingField = data;
        data.unit = copy;
        // Temporary creatures are consumed first; the wounded top may itself be
        // temporary. Preserve that distinction instead of converting them to real units.
        bool tempTop = health.TopTemporary;
        data.fullStacks = Math.Max(0, health.Count - health.Temporary - (tempTop ? 0 : 1));
        data.tempFullStacks = Math.Max(0, health.Temporary - (tempTop ? 1 : 0));
        data.cpmu = tempTop;
        data.cpmr = health.TopHp;
        var location = original.cnde.MemberwiseClone().Cast<eon>();
        copy.cnde = location;
        location.cndl = copy;
        location.cndk = position;
        return copy;
    }
}
