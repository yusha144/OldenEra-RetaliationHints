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
        internal BattleHex Destination; internal bool CanCounter;
        internal BhBattleUnitInfoView TargetView; internal int Frame;
        internal Context(eof hover, Unit attacker, Unit defender, eib attack, eib counter,
            BattleHex destination, bool canCounter, BhBattleUnitInfoView targetView, int frame)
        { Hover = hover; Attacker = attacker; Defender = defender; Attack = attack; Counter = counter;
          Destination = destination; CanCounter = canCounter; TargetView = targetView; Frame = frame; }
    }
    internal static Context Current;
    internal static readonly Dictionary<IntPtr, DamageSample> Samples = new();
    internal static bool Same(Il2CppObjectBase a, Il2CppObjectBase b) => a != null && b != null && a.Pointer == b.Pointer;

    [HarmonyPatch(typeof(BhBattleUnitInfoView), nameof(BhBattleUnitInfoView.bghr))]
    private static class DamageShown
    {
        private static void Postfix(BhBattleUnitInfoView __instance, evr __0)
        {
            try
            {
                if (!Plugin.Instance.Enabled.Value || __0 == null || !__0.cmpr || !__0.cmqa) return;
                if (Samples.Count > 128) Samples.Clear();
                Samples[__instance.Pointer] = new(__instance, new(__0.cmps, __0.cmpt), Time.frameCount);
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
                var hover = elb.ckia.ckid;
                if (hover == null || !Same(hover.cldm, __0) || !Same(hover.cldl, __2)
                    || !Same(hover.clcx, __3) || !Same(__3, __1.ctvn)) return;
                var view = __1.cmww?.TryCast<eml>()?.ckom?.cmot;
                if (!view) return;
                var counter = __result ? __1.ckyj.bdui(__1, __0, __2, __instance.cliq.clgw, __instance.clis, __4) : null;
                var destination = __0.ctvn;
                var type = __2.cjuj.csus;
                if ((type == Hex.Configs.EAttackType.Melee || type == Hex.Configs.EAttackType.Range)
                    && __4 != null && __4.Count > 0) destination = __4[0];
                Current = new(hover, __0, __1, __2, counter, destination, __result, view, Time.frameCount);
            }
            catch (Exception e) { Current = null; Plugin.Instance.Report("Capturing retaliation preview", e); }
        }
    }

    [HarmonyPatch(typeof(eof), nameof(eof.bgdu))]
    private static class PreviewHidden
    {
        private static void Postfix() { Current = null; Samples.Clear(); }
    }
}
