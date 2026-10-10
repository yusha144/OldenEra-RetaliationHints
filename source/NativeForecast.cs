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
        if (context.Counter?.cjuf == null) throw new NotSupportedException("Counterattack dealer not available");
        if (!Plugin.Instance.Patterns.TryGet(context.Counter.cjuf.data.attackPatternSid, out float multiplier))
            throw new NotSupportedException("Unsupported counterattack pattern: " + context.Counter.cjuf.data.attackPatternSid);
        // Damage absorption, resurrection/protection and temporary stacks need a
        // sequence simulation; presenting their simple HP subtraction as exact is unsafe.
        if (context.Attacker.cmwp.tempFullStacks != 0 || context.Defender.cmwp.tempFullStacks != 0
            || context.Attacker.stats.overkillProtection != 0 || context.Defender.stats.overkillProtection != 0)
            throw new NotSupportedException("Temporary stacks or overkill protection");

        return ForecastMath.Evaluate(attacker, defender, true, before, (sourceHealth, targetHealth, counter) =>
        {
            if (!counter) return opening;
            var source = Snapshot(context.Defender, sourceHealth, context.Defender.ctvn);
            var target = Snapshot(context.Attacker, targetHealth, context.Destination);
            var ability = context.Counter.MemberwiseClone().Cast<eib>();
            ability.cjua = source;
            var parameters = new DamageUiParams(source.Cast<ekf>(), ability.Cast<ekg>(), target.Cast<ekj>(),
                multiplier, ESideCastType.Unit, 0);
            var forecast = evs.bhyd(parameters, true);
            if (forecast == null || !forecast.cmpr || !forecast.cmqa)
                throw new NotSupportedException("Native damage preview unavailable for counterattack");
            return new(forecast.cmps, forecast.cmpt);
        });
    }

    private static StackHealth Health(Unit unit)
    {
        int hp = unit.stats.hp, count = unit.cmwp.ctxr;
        return new(count, hp, count == 0 ? 0 : unit.cmwp.cnbx > 0 ? unit.cmwp.cnbx : hp);
    }

    private static Unit Snapshot(Unit original, StackHealth health, BattleHex position)
    {
        // Shallow-copy immutable/read-only references; detach every field we change.
        // Do not initialize a unit, add it to the battle, run actions or call damage APIs.
        var copy = original.MemberwiseClone().Cast<Unit>();
        var data = original.cmwp.MemberwiseClone().Cast<UnitData>();
        copy._cmwp_k__BackingField = data;
        data.unit = copy;
        data.fullStacks = Math.Max(0, health.Count - 1);
        data.tempFullStacks = 0;
        data.cnbx = health.TopHp;
        var location = original.ckyn.MemberwiseClone().Cast<enh>();
        copy.ckyn = location;
        location.ckyu = copy;
        location.ckyt = position;
        return copy;
    }
}
