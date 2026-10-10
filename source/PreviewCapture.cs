using HarmonyLib;
using Hex.Session.Battle;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using BattleHex = Hex.Session.Battle.Hex;

namespace OldenEra.RetaliationHints;

// All hook arguments are native reference types. We do not detour DamageUiParams,
// a non-blittable IL2CPP value type. No hook changes any argument or return value.
internal static class PreviewCapture
{
    internal sealed class DamageSample
    {
        internal BhBattleUnitInfoView View; internal DamageRange Damage; internal int Frame;
        internal DamageSample(BhBattleUnitInfoView view, DamageRange damage, int frame)
        { View = view; Damage = damage; Frame = frame; }
    }
    internal sealed class Context
    {
        internal eof Hover; internal Unit Attacker, Defender; internal eib Attack, Counter;
        internal BattleHex Destination, TargetHex; internal bool CanCounter;
        internal BhBattleUnitInfoView TargetView; internal DamageRange Damage; internal int Frame;
        internal Context(eof hover, Unit attacker, Unit defender, eib attack, eib counter,
            BattleHex destination, BattleHex targetHex, bool canCounter, DamageSample sample, int frame)
        { Hover = hover; Attacker = attacker; Defender = defender; Attack = attack; Counter = counter;
          Destination = destination; TargetHex = targetHex; CanCounter = canCounter;
          TargetView = sample.View; Damage = sample.Damage; Frame = frame; }
    }
    private sealed class Scope
    {
        internal eof Hover;
        internal Unit Target;
        internal DamageSample Sample;
    }
    [ThreadStatic] private static Stack<Scope> _scopes;
    private static Scope Active => _scopes?.Count > 0 ? _scopes.Peek() : null;
    internal static Context Current;
    internal static bool Same(Il2CppObjectBase a, Il2CppObjectBase b) => a != null && b != null && a.Pointer == b.Pointer;

    // A large creature can occupy a hovered cell other than its origin. Match the
    // actual occupant; do not require the mouse hex to equal Unit.ctvn.
    internal static bool Occupies(Unit unit, BattleHex hex)
    {
        if (unit == null || hex == null) return false;
        if (Same(hex.clbi, unit) || Same(hex, unit.ctvn)) return true;
        var objects = hex.fieldObjects;
        if (objects != null)
            for (int i = 0; i < objects.Count; i++) if (Same(objects[i], unit)) return true;
        return false;
    }

    // This reference-only native method shows damage, then checks retaliation.
    // Scope both hooks to that call so an AI check or an adjacent splash target
    // cannot replace the hovered target's preview. No DamageUiParams detour.
    [HarmonyPatch(typeof(eof), nameof(eof.bgdy))]
    private static class NativePreview
    {
        private static void Prefix(eof __instance, FieldObject __0)
        {
            _scopes ??= new();
            var scope = new Scope(); _scopes.Push(scope);
            try
            {
                if (!Plugin.Instance.Enabled.Value || !elb.ckia || !Same(elb.ckia.ckid, __instance)) return;
                Plugin.Instance.TraceOnce("native-preview", "Native hover preview reached the mod.");
                var target = __0?.TryCast<Unit>();
                if (!Occupies(target, __instance.clcx)) return;
                scope.Hover = __instance; scope.Target = target;
                Current = null;
            }
            catch (Exception error) { Plugin.Instance.Report("Opening preview capture", error); }
        }

        // Always unwind, including native exceptions. A void finalizer neither
        // replaces nor suppresses the original exception.
        private static void Finalizer() { if (_scopes?.Count > 0) _scopes.Pop(); }
    }

    [HarmonyPatch(typeof(BhBattleUnitInfoView), nameof(BhBattleUnitInfoView.bghr))]
    private static class DamageShown
    {
        private static void Postfix(BhBattleUnitInfoView __instance, evr __0)
        {
            try
            {
                if (!Plugin.Instance.Enabled.Value || __0 == null || !__0.cmpr || !__0.cmqa) return;
                var scope = Active;
                if (scope?.Hover == null) return;
                scope.Sample = new(__instance, new(__0.cmps, __0.cmpt), Time.frameCount);
                Plugin.Instance.TraceOnce("native-damage", $"Native outgoing range captured: {__0.cmps}-{__0.cmpt} HP.");
            }
            catch (Exception e) { Plugin.Instance.Report("Capturing attack preview", e); }
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
                if (!Plugin.Instance.Enabled.Value || !elb.ckia || __0 == null || __1 == null || __2 == null) return;
                var scope = Active;
                var hover = scope?.Hover;
                if (hover == null || !Same(scope.Target, __1) || !Same(hover.cldm, __0) || !Same(hover.cldl, __2)
                    || !Same(hover.clcx, __3)) return;
                // Clear the old hint immediately for a shot/prohibited/exhausted
                // retaliation. Trust the native result, not a guessed attack type:
                // EAttackType.Range is a reaching melee attack, not Shoot.
                if (!__result) { Current = null; Plugin.Instance.TraceOnce("counter-disallowed", "Native hover check disallows retaliation; hint hidden."); return; }
                var sample = scope.Sample;
                if (sample == null || !sample.View || !sample.Damage.Valid)
                { Plugin.Instance.TraceOnce("missing-damage", "Native counter allowed, but the same hover call supplied no damage panel."); return; }
                var counter = __1.ckyj.bdui(__1, __0, __2, __instance.cliq.clgw, __instance.clis, __4);
                var destination = __0.ctvn;
                var type = __2.cjuj.csus;
                if ((type == Hex.Configs.EAttackType.Melee || type == Hex.Configs.EAttackType.Range)
                    && __4 != null && __4.Count > 0) destination = __4[0];
                Current = new(hover, __0, __1, __2, counter, destination, __3, __result, sample, Time.frameCount);
                Plugin.Instance.TraceOnce("counter-captured", $"Retaliation context captured from native hover: {__0.ckyo} -> {__1.ckyo}, attack={type}.");
            }
            catch (Exception e) { Current = null; Plugin.Instance.Report("Capturing retaliation preview", e); }
        }
    }

    [HarmonyPatch(typeof(eof), nameof(eof.bgdu))]
    private static class PreviewHidden
    {
        private static void Postfix(eof __instance)
        {
            if (Same(Current?.Hover, __instance)) Current = null;
        }
    }
}
