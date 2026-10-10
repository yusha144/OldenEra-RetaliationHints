using Hex.Configs;
using Hex.Session.Battle;
using BattleHex = Hex.Session.Battle.Hex;

namespace OldenEra.RetaliationHints;

internal static class NativeForecast
{
    internal static LossRange Calculate(PreviewCapture.Context context, DamageRange opening)
    {
        var attacker = Health(context.Attacker);
        var defender = Health(context.Defender);
        bool before = context.Defender.bigr("counter_before_attack");
        if (!context.CanCounter) return new(0, 0, false);
        if (context.Counter?.cjuf == null) throw Unavailable("нет данных контрудара");
        if (!Plugin.Instance.Patterns.TryGet(context.Counter.cjuf.data.attackPatternSid, out float multiplier))
            throw Unavailable("особая форма контрудара");
        if (context.Attacker.stats.overkillProtection != 0 || context.Defender.stats.overkillProtection != 0)
            throw Unavailable("защита от гибели");
        CheckDirectEffects(context.Counter);
        if (!before)
        {
            CheckDirectEffects(context.Attack);
            var dealer = context.Attack.cjuf?.data;
            if (!string.IsNullOrEmpty(dealer?.buff?.sid) || !string.IsNullOrEmpty(dealer?.sideBuff?.sid)
                || HasSideEffect(context.Attack.cjug?.data) || HasSideEffect(context.Attack.cjuh?.data)
                || !string.IsNullOrEmpty(context.Attack.cjuj.tempSelfBuff))
                throw Unavailable("эффект после попадания");
        }
        var healing = before ? null : LifeSteal(context, attacker);
        var cache = new Dictionary<(StackHealth, StackHealth), DamageRange>();

        return ForecastMath.Evaluate(attacker, defender, true, before, (sourceHealth, targetHealth, counter) =>
        {
            if (!counter) return opening;
            if (cache.TryGetValue((sourceHealth, targetHealth), out var cached)) return cached;
            var source = Snapshot(context.Defender, sourceHealth, context.Defender.ctvn);
            var target = Snapshot(context.Attacker, targetHealth, context.Destination);
            var ability = context.Counter.MemberwiseClone().Cast<eib>();
            ability.cjua = source;
            var parameters = new DamageUiParams(source.Cast<ekf>(), ability.Cast<ekg>(), target.Cast<ekj>(),
                multiplier, ESideCastType.Unit, 0);
            var forecast = evs.bhyd(parameters, true);
            if (forecast == null || !forecast.cmpr || !forecast.cmqa)
                throw Unavailable("нет прогноза контрудара");
            var range = new DamageRange(forecast.cmps, forecast.cmpt);
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
    private static void CheckDirectEffects(eib ability)
    {
        var dealer = ability.cjuf?.data;
        if (dealer?.targetMechanics != null && dealer.targetMechanics.Length > 0)
            throw Unavailable("особый эффект удара");
        if (ability.cjui != null)
            foreach (var mechanic in ability.cjui)
                if (mechanic != null && mechanic.clny != EMechType.Vampirism)
                    throw Unavailable("особый эффект атаки");
        // Self healing after the counter does not change casualties of that hit.
        // Opening self-healing is handled explicitly below.
    }

    private static Func<StackHealth, int, StackHealth> LifeSteal(PreviewCapture.Context context, StackHealth health)
    {
        var mechanics = context.Attack.cjui;
        if (mechanics == null || mechanics.Length == 0) return null;
        var active = new List<esm>();
        foreach (var mechanic in mechanics)
        {
            if (mechanic == null || mechanic.clny != EMechType.Vampirism) continue;
            var life = mechanic.TryCast<esm>();
            if (life == null || !life.clnx.afterAttack || life.clvw != ETargetAllegiance.Self)
                throw Unavailable("особое лечение после удара");
            // These are read-only native blocker checks, also used by esm.bgty.
            if (context.Attacker.ctuw.bejl(mechanic) || eox.bgpn(context.Defender, mechanic)) continue;
            if (life.clvx != EHealType.WithTempStacks && life.clvx != EHealType.WithRealUntilStart
                && life.clvx != EHealType.WithoutRespawn)
                throw Unavailable("особое воскрешение");
            if (health.Temporary > 0 && life.clvx == EHealType.WithRealUntilStart)
                throw Unavailable("воскрешение с временными существами");
            if (!float.IsFinite(life.clvv) || life.clvv < 0 || life.clvu < 0)
                throw Unavailable("неизвестное лечение");
            active.Add(life);
        }
        if (active.Count == 0) return null;
        if (active.Count > 1) throw Unavailable("несколько источников лечения");
        // A splash attack can heal from multiple victims, not just the hovered one.
        if (!Plugin.Instance.Patterns.IsSingleDamageCell(context.Attack.cjuf.data.attackPatternSid))
            throw Unavailable("лечение от нескольких целей");
        if (context.Attacker.stats.limitStacks) throw Unavailable("особый предел численности");
        var effect = active[0];
        return (state, inflicted) =>
        {
            // Match native order: truncate base heal, apply final-healing bonus,
            // then vampirism modifier and the game's health cap. No event execution.
            float raw = effect.clvu + inflicted * effect.clvv;
            if (!float.IsFinite(raw) || raw >= int.MaxValue) throw Unavailable("слишком большой прогноз");
            int amount = eox.bgpf(context.Attacker, (int)raw);
            float adjusted = amount * (1f + context.Attacker.stats.vampirismHealMultiplier);
            if (!float.IsFinite(adjusted) || adjusted >= int.MaxValue) throw Unavailable("неизвестное лечение");
            amount = Math.Max(0, (int)adjusted);
            // bikb is the read-only cap function; do NOT call bikc/bijz (mutators).
            amount = exq.bikb(effect.clvx, context.Attacker.cmwp, context.Attacker.stats, amount);
            if (state.Total + amount > int.MaxValue) throw Unavailable("слишком большой прогноз");
            return state.Heal(amount, int.MaxValue, effect.clvx == EHealType.WithTempStacks);
        };
    }

    private static ForecastUnavailableException Unavailable(string reason) => new(reason);

    private static StackHealth Health(Unit unit)
    {
        var data = unit.cmwp;
        int hp = unit.stats.hp, count = data.ctxr;
        int temporary = data.tempFullStacks + (data.cnca && data.cnbx > 0 ? 1 : 0);
        return new(count, hp, count == 0 ? 0 : data.cnbx > 0 ? data.cnbx : hp, temporary,
            data.cnbx > 0 ? data.cnca : temporary > 0);
    }

    private static Unit Snapshot(Unit original, StackHealth health, BattleHex position)
    {
        var copy = original.MemberwiseClone().Cast<Unit>();
        var data = original.cmwp.MemberwiseClone().Cast<UnitData>();
        copy._cmwp_k__BackingField = data;
        data.unit = copy;
        // Temporary creatures are consumed first; the wounded top may itself be
        // temporary. Preserve that distinction instead of converting them to real units.
        bool tempTop = health.TopTemporary;
        data.fullStacks = Math.Max(0, health.Count - health.Temporary - (tempTop ? 0 : 1));
        data.tempFullStacks = Math.Max(0, health.Temporary - (tempTop ? 1 : 0));
        data.cnca = tempTop;
        data.cnbx = health.TopHp;
        var location = original.ckyn.MemberwiseClone().Cast<enh>();
        copy.ckyn = location;
        location.ckyu = copy;
        location.ckyt = position;
        return copy;
    }
}
