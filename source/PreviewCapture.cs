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
        internal epl Hover; internal Unit Attacker, Defender; internal ejh Attack, Counter;
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
    internal static int BattleGeneration { get; private set; }
    internal static Context Current;
    internal static bool Same(Il2CppObjectBase a, Il2CppObjectBase b) => a != null && b != null && a.Pointer == b.Pointer;

    internal static void Reset() { Current = null; _sample = null; _decision = null; }
    internal static void ObserveBattle()
    {
        var battle = emh.cmlp?.cmlr?.Pointer ?? IntPtr.Zero;
        if (battle != _battle || emh.lastBattleId != _battleId)
        {
            CombatAudit.BattleChanged();
            Reset(); _battle = battle; _battleId = emh.lastBattleId; BattleGeneration++;
            Plugin.Instance.BeginBattleDiagnostics(_battleId);
        }
    }
    private static PreviewKey Key(epl hover, Unit attacker, Unit target, ejh attack) =>
        new(_battle, hover.Pointer, attacker.Pointer, target.Pointer, attack.Pointer,
            hover.cngo.Pointer, Time.frameCount);

    internal static bool Occupies(Unit unit, BattleHex hex)
    {
        if (unit == null || hex == null) return false;
        if (Same(hex.cnez, unit) || Same(hex, unit.cwnz)) return true;
        var objects = hex.fieldObjects;
        if (objects != null)
            for (int i = 0; i < objects.Count; i++) if (Same(objects[i], unit)) return true;
        return false;
    }
    private static Unit Target(BattleHex hex)
    {
        if (hex == null) return null;
        if (hex.cnez != null) return hex.cnez;
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
        context.TargetView = _sample.View; context.Damage = _sample.Damage;
        Current = context;
        Plugin.Instance.TraceMatch(context.Attacker, context.Defender, context.CanCounter, true);
        Plugin.Instance.TraceOnce(context.CanCounter ? "counter-captured" : "no-counter-captured",
            $"Paired native preview: {context.Attacker.cncf} -> {context.Defender.cncf}, counter={context.CanCounter}.");
    }

    [HarmonyPatch(typeof(BhBattleUnitInfoView), nameof(BhBattleUnitInfoView.bhhb))]
    private static class DamageShown
    {
        private static void Postfix(BhBattleUnitInfoView __instance, exu __0)
        {
            try
            {
                ObserveBattle();
                if (!Plugin.Instance.Enabled.Value || !emh.cmlp) return;
                var hover = emh.cmlp.cmls;
                var attacker = hover?.cnhd?.TryCast<Unit>();
                var attack = hover?.cnhc?.TryCast<ejh>();
                var target = Target(hover?.cngo);
                if (attacker == null || attack == null || target == null || Same(attacker, target)) return;
                // Same lookup used by epl.bgdp: battle renderer by FieldObject id,
                // then its native info view. A healing or splash panel cannot match.
                var view = emh.cmlp.cmma?.bgyj<ens>(target.cwnj)?.cmsb?.coyi;
                if (!Same(view, __instance)) return;
                if (__0 == null || !__0.cozg || !__0.cozp || __0.cozh < 0 || __0.cozi < __0.cozh)
                { Reset(); return; }
                _sample = new() { Key = Key(hover, attacker, target, attack), View = __instance,
                    Damage = new(__0.cozh, __0.cozi) };
                Plugin.Instance.TraceOnce("native-damage", $"Native target damage: {attacker.cncf} -> {target.cncf}, {__0.cozh}-{__0.cozi} HP.");
                Publish();
            }
            catch (Exception error) { Reset(); Plugin.Instance.Report("Capturing attack panel", error); }
        }
    }

    [HarmonyPatch(typeof(eqd), nameof(eqd.bhpx))]
    private static class CounterChecked
    {
        private static void Postfix(eqd __instance, Unit __0, Unit __1, ejh __2, BattleHex __3,
            Il2CppSystem.Collections.Generic.List<BattleHex> __4, bool __result)
        {
            try
            {
                ObserveBattle();
                if (!Plugin.Instance.Enabled.Value || !emh.cmlp || __0 == null || __1 == null || __2 == null) return;
                var hover = emh.cmlp.cmls;
                if (hover == null || !Same(hover.cnhd, __0) || !Same(hover.cnhc, __2)
                    || !Same(hover.cngo, __3) || !Occupies(__1, __3)) return;
                var key = Key(hover, __0, __1, __2);
                Current = null;
                var context = new Context { Hover = hover, Attacker = __0, Defender = __1, Attack = __2,
                    TargetHex = __3, CanCounter = __result, Frame = Time.frameCount };
                // Preserve the selected destination even without a counter so
                // the passive auditor can match a moving attack to its preview.
                context.Destination = __0.cwnz;
                var type = __2.clxy?.cvly;
                if ((type == Hex.Configs.EAttackType.Melee || type == Hex.Configs.EAttackType.Range)
                    && __4 != null && __4.Count > 0) context.Destination = __4[0];
                if (__result)
                    context.Counter = __1.cnca.bete(__1, __0, __2, __instance.cnmh.cnkn, __instance.cnmj, __4);
                _decision = new() { Key = key, Context = context };
                // Bounded per matchup diagnostics still distinguish ghost/cavalry
                // after another creature has already produced the first log line.
                Plugin.Instance.TraceMatch(__0, __1, __result, _sample?.Key.Matches(key) == true);
                Publish();
            }
            catch (Exception error) { Reset(); Plugin.Instance.Report("Capturing retaliation decision", error); }
        }
    }

    [HarmonyPatch(typeof(epl), nameof(epl.bhdb))]
    private static class PreviewHidden
    {
        private static void Postfix(epl __instance)
        { if (Same(emh.cmlp?.cmls, __instance)) Reset(); }
    }
}
