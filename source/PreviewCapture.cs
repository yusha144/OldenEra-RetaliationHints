using HarmonyLib;
using Hex.Session.Battle;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using BattleHex = Hex.Session.Battle.Hex;

namespace OldenEra.RetaliationHints;

// Join the actual damage panel and native counter decision by battle, selection
// and frame. Neither callback depends on being inside one particular bgdy overload.
internal static class PreviewCapture
{
    internal sealed class Context
    {
        internal eof Hover; internal Unit Attacker, Defender; internal eib Attack, Counter;
        internal BattleHex Destination, TargetHex; internal bool CanCounter;
        internal BhBattleUnitInfoView TargetView; internal DamageRange Damage; internal int Frame;
    }
    private sealed class Sample
    {
        internal PreviewKey Key; internal BhBattleUnitInfoView View; internal DamageRange Damage;
    }
    private sealed class Decision
    {
        internal PreviewKey Key; internal Context Context;
    }
    private static Sample _sample;
    private static Decision _decision;
    private static IntPtr _battle;
    private static int _battleId;
    internal static Context Current;
    internal static bool Same(Il2CppObjectBase a, Il2CppObjectBase b) => a != null && b != null && a.Pointer == b.Pointer;

    internal static void Reset() { Current = null; _sample = null; _decision = null; }
    internal static void ObserveBattle()
    {
        var battle = elb.ckia?.ckic?.Pointer ?? IntPtr.Zero;
        if (battle != _battle || elb.lastBattleId != _battleId)
        { Reset(); _battle = battle; _battleId = elb.lastBattleId; }
    }
    private static PreviewKey Key(eof hover, Unit attacker, Unit target, eib attack) =>
        new(_battle, hover.Pointer, attacker.Pointer, target.Pointer, attack.Pointer,
            hover.clcx.Pointer, Time.frameCount);

    internal static bool Occupies(Unit unit, BattleHex hex)
    {
        if (unit == null || hex == null) return false;
        if (Same(hex.clbi, unit) || Same(hex, unit.ctvn)) return true;
        var objects = hex.fieldObjects;
        if (objects != null)
            for (int i = 0; i < objects.Count; i++) if (Same(objects[i], unit)) return true;
        return false;
    }
    private static Unit Target(BattleHex hex)
    {
        if (hex == null) return null;
        if (hex.clbi != null) return hex.clbi;
        var objects = hex.fieldObjects;
        if (objects != null)
            for (int i = 0; i < objects.Count; i++)
            { var unit = objects[i]?.TryCast<Unit>(); if (unit != null) return unit; }
        return null;
    }
    private static void Publish()
    {
        if (_sample == null || _decision == null || !_sample.Key.Matches(_decision.Key)) return;
        var context = _decision.Context;
        if (!context.CanCounter) { Current = null; return; }
        context.TargetView = _sample.View; context.Damage = _sample.Damage;
        Current = context;
        Plugin.Instance.TraceMatch(context.Attacker, context.Defender, true, true);
        Plugin.Instance.TraceOnce("counter-captured", $"Paired native preview: {context.Attacker.ckyo} -> {context.Defender.ckyo}.");
    }

    [HarmonyPatch(typeof(BhBattleUnitInfoView), nameof(BhBattleUnitInfoView.bghr))]
    private static class DamageShown
    {
        private static void Postfix(BhBattleUnitInfoView __instance, evr __0)
        {
            try
            {
                ObserveBattle();
                if (!Plugin.Instance.Enabled.Value || !elb.ckia) return;
                var hover = elb.ckia.ckid;
                var attacker = hover?.cldm?.TryCast<Unit>();
                var attack = hover?.cldl?.TryCast<eib>();
                var target = Target(hover?.clcx);
                if (attacker == null || attack == null || target == null || Same(attacker, target)) return;
                // Same lookup used by eof.bgdp: battle renderer by FieldObject id,
                // then its native info view. A healing or splash panel cannot match.
                var view = elb.ckia.ckil?.bfzc<eml>(target.ctux)?.ckom?.cmot;
                if (!Same(view, __instance)) return;
                if (__0 == null || !__0.cmpr || !__0.cmqa || __0.cmps < 0 || __0.cmpt < __0.cmps)
                { Reset(); return; }
                _sample = new() { Key = Key(hover, attacker, target, attack), View = __instance,
                    Damage = new(__0.cmps, __0.cmpt) };
                Plugin.Instance.TraceOnce("native-damage", $"Native target damage: {attacker.ckyo} -> {target.ckyo}, {__0.cmps}-{__0.cmpt} HP.");
                Publish();
            }
            catch (Exception error) { Reset(); Plugin.Instance.Report("Capturing attack panel", error); }
        }
    }

    [HarmonyPatch(typeof(eox), nameof(eox.bgqn))]
    private static class CounterChecked
    {
        private static void Postfix(eox __instance, Unit __0, Unit __1, eib __2, BattleHex __3,
            Il2CppSystem.Collections.Generic.List<BattleHex> __4, bool __result)
        {
            try
            {
                ObserveBattle();
                if (!Plugin.Instance.Enabled.Value || !elb.ckia || __0 == null || __1 == null || __2 == null) return;
                var hover = elb.ckia.ckid;
                if (hover == null || !Same(hover.cldm, __0) || !Same(hover.cldl, __2)
                    || !Same(hover.clcx, __3) || !Occupies(__1, __3)) return;
                var key = Key(hover, __0, __1, __2);
                Current = null;
                var context = new Context { Hover = hover, Attacker = __0, Defender = __1, Attack = __2,
                    TargetHex = __3, CanCounter = __result, Frame = Time.frameCount };
                if (__result)
                {
                    context.Counter = __1.ckyj.bdui(__1, __0, __2, __instance.cliq.clgw, __instance.clis, __4);
                    context.Destination = __0.ctvn;
                    var type = __2.cjuj.csus;
                    if ((type == Hex.Configs.EAttackType.Melee || type == Hex.Configs.EAttackType.Range)
                        && __4 != null && __4.Count > 0) context.Destination = __4[0];
                }
                _decision = new() { Key = key, Context = context };
                // Bounded per matchup diagnostics still distinguish ghost/cavalry
                // after another creature has already produced the first log line.
                Plugin.Instance.TraceMatch(__0, __1, __result, _sample?.Key.Matches(key) == true);
                Publish();
            }
            catch (Exception error) { Reset(); Plugin.Instance.Report("Capturing retaliation decision", error); }
        }
    }

    [HarmonyPatch(typeof(eof), nameof(eof.bgdu))]
    private static class PreviewHidden
    {
        private static void Postfix(eof __instance)
        { if (Same(elb.ckia?.ckid, __instance)) Reset(); }
    }
}
