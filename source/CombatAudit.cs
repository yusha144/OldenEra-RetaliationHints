using BepInEx;
using HarmonyLib;
using Hex.Session.Battle;
using Il2CppInterop.Runtime.InteropTypes;
using System.Text;
using System.Text.Json;
using UnityEngine;

namespace OldenEra.RetaliationHints;

// Observers never change arguments/results, execute game actions or consume RNG.
internal static class CombatAudit
{
    private static Harmony _patches;
    private static bool _ready;
    private static string _summaryPath;
    private static AuditJournal _journal;
    private static readonly Dictionary<string, int> Totals = new();
    private static readonly AuditCommandBook Commands = new();
    private static readonly Dictionary<long, CommandScope> Pending = new();
    private static readonly Stack<CommandScope> Scheduling = new();
    private static readonly Stack<DamageScope> DamageScopes = new();
    private static readonly List<string> Recent = new();
    private static AuditPreview _calculated, _shown;
    private static double _shownAt;
    private static long _ignoredCommands, _ignoredSteps;
    internal static bool Enabled => _ready && Plugin.Instance.CombatAuditEnabled.Value;

    internal sealed class CommandScope
    {
        public AuditCommand Command;
        public etw Queue;
        public AuditPreview Shown;
        public double ShownAt, StartedAt;
        public readonly Dictionary<long, ety> NativeSteps = new();
    }

    internal sealed class DamageScope
    {
        public AuditExchange Exchange;
        public Unit Receiver, Source;
        public eib Ability;
        public ekb Dealer;
        public AuditUnit Before, SourceBefore;
        public int Input;
        public bool Seen;
        public string Phase;
    }

    internal static void Initialize()
    {
        if (!Plugin.Instance.CombatAuditEnabled.Value) return;
        try
        {
            string folder = Path.Combine(Paths.BepInExRootPath, "RetaliationHints", "CombatAudit");
            _summaryPath = Path.Combine(folder, "latest-summary.txt");
            _journal = new(folder, Plugin.Version);
            _patches = new Harmony(Plugin.Id + ".audit");
            Patch(typeof(eox), nameof(eox.bgqc), nameof(BeginCommand), null, nameof(EndCommand));
            Patch(typeof(etw), nameof(etw.bhej), null, nameof(Queued));
            Patch(typeof(etk), nameof(etk.bhdy), nameof(OpeningStarted));
            Patch(typeof(etm), nameof(etm.bhdy), nameof(CounterStarted));
            Patch(typeof(exk), nameof(exk.bijb), nameof(CounterExecuting));
            Patch(typeof(exr), nameof(exr.bikt), nameof(BeforeDamage), null, nameof(AfterDamage));
            Patch(typeof(enb), nameof(enb.bfmq), nameof(DamageEvent));
            _ready = true;
            Summary();
            Plugin.Instance.Log.LogInfo($"Combat audit enabled: {_journal.CurrentPath}");
        }
        catch (Exception error) { Disable(error); }
    }

    private static void Patch(Type type, string method, string prefix = null, string postfix = null, string finalizer = null)
    {
        HarmonyMethod Hook(string name) => name == null ? null : new(typeof(CombatAudit), name);
        _patches.Patch(AccessTools.Method(type, method), Hook(prefix), Hook(postfix), null, Hook(finalizer), null);
    }

    internal static void Calculated(PreviewCapture.Context context, LossRange? losses, string unavailable, AuditTrace trace)
    {
        if (!Enabled) return;
        Guard(() => _calculated = new(Key(context), Snapshot(context.Attacker), Snapshot(context.Defender),
            context.Damage, losses, context.CanCounter, context.Defender.bigr("counter_before_attack"), unavailable, trace));
    }

    internal static void Presented(PreviewCapture.Context context)
    {
        if (!Enabled || Pending.Count != 0 || _calculated == null) return;
        Guard(() =>
        {
            if (_calculated.Key != Key(context) || _calculated.Opening != context.Damage
                || _calculated.CanCounter != context.CanCounter) return;
            _shown = _calculated; _shownAt = Time.realtimeSinceStartupAsDouble;
        });
    }

    internal static void Hidden() { _calculated = null; }
    internal static void BattleChanged()
    {
        if (!Enabled) return;
        Guard(() =>
        {
            foreach (var scope in Pending.Values.ToArray())
                Finish(scope, false, "Смена или завершение боя до подтверждения конца действия");
            _shown = _calculated = null;
        });
    }

    internal static void Tick()
    {
        if (!Enabled || Pending.Count == 0) return;
        Guard(() =>
        {
            foreach (var scope in Pending.Values.ToArray())
            {
                var command = scope.Command;
                if (!command.SchedulingComplete) continue;
                foreach (var step in command.Exchange.Steps)
                    step.Finished = scope.NativeSteps[step.Pointer].cmee == ety.EState.Finished;
                var queue = scope.Queue;
                if (!queue.ctwi && queue.cmdr.Count == 0 && queue.cmds.Count == 0
                    && (queue.cmdt == null || queue.cmdt.cmee == ety.EState.Finished))
                {
                    bool complete = command.Exchange.Steps.All(s => s.Finished);
                    Finish(scope, complete, complete ? "Очередь и все шаги команды завершены"
                        : "Очередь пуста, но не все зарегистрированные шаги завершены");
                }
                else if (Time.realtimeSinceStartupAsDouble - scope.StartedAt > 120)
                    Finish(scope, false, "Не дождались подтверждённого завершения действия за 120 секунд");
            }
        });
    }

    private static bool IsLive(eor battle, etw queue)
    {
        var active = elb.ckia?.ckic;
        return AuditRouting.IsLive(Ptr(battle), Ptr(queue), Ptr(active), Ptr(active?.clhf));
    }

    private static CommandScope CreateScope(eor battle, etw queue, bool observed)
    {
        var active = elb.ckia?.ckic;
        var command = Commands.Begin(Ptr(battle), Ptr(queue), Ptr(active), Ptr(active?.clhf));
        if (command == null) return new(); // sentinel for a nested foreign scheduler
        command.Exchange.CommandObserved = observed;
        var scope = new CommandScope { Command = command, Queue = queue, Shown = _shown,
            ShownAt = _shownAt, StartedAt = Time.realtimeSinceStartupAsDouble };
        Pending.Add(command.Id, scope);
        return scope;
    }

    // One native scheduling invocation owns the whole sequence. No game method is called here.
    private static void BeginCommand(eox __instance, out CommandScope __state)
    {
        __state = null;
        if (!Enabled) return;
        CommandScope scope = null;
        Guard(() =>
        {
            var battle = __instance.cliq;
            scope = CreateScope(battle, battle?.clhf, true);
            if (scope.Command == null) _ignoredCommands++;
            Scheduling.Push(scope);
        });
        __state = scope;
    }

    private static void EndCommand(CommandScope __state, Exception __exception)
    {
        if (__state == null) return;
        Guard(() =>
        {
            if (Scheduling.Count == 0 || !ReferenceEquals(Scheduling.Peek(), __state))
                throw new InvalidOperationException("Нарушена вложенность команд сверки");
            Scheduling.Pop();
            var command = __state.Command;
            if (command == null) return;
            command.SchedulingComplete = true;
            if (__exception != null) command.Exchange.ObserverFailure = "Исключение в игровом планировщике атаки";
            if (!command.HasOpening) { Commands.Remove(command); Pending.Remove(command.Id); }
        });
    }

    private static void Queued(etw __instance, ety __0)
    {
        if (!Enabled) return;
        Guard(() =>
        {
            var opening = __0?.TryCast<etk>();
            var counter = __0?.TryCast<etm>();
            if (opening == null && counter == null) return;
            // Reject foreign simulations BEFORE touching previews or pending commands.
            if (!IsLive(__0.cmec, __instance)) { _ignoredSteps++; return; }
            var scope = Scheduling.Count == 0 ? null : Scheduling.Peek();
            if (scope?.Command == null || scope.Command.Queue != Ptr(__instance)
                || scope.Command.Battle != Ptr(__0.cmec))
            {
                if (opening == null) return; // unknown counter will invalidate matching damage, never imply zero
                scope = CreateScope(__0.cmec, __instance, false);
                scope.Command.SchedulingComplete = true;
            }
            var command = scope.Command;
            var item = command.Exchange;
            bool first = opening != null && !command.HasOpening;
            var step = new AuditStep { Pointer = Ptr(__0), Kind = opening == null ? "counter" : "opening",
                Caster = Ptr(opening?.cmbq ?? counter?.cmcf), Target = Ptr(counter?.cmcg),
                Ability = Ptr(opening?.cmbp ?? counter?.cmcb), Dealer = Ptr((opening?.cmbp ?? counter?.cmcb)?.cjuf) };
            if (!Commands.AddStep(command, step)) return;
            scope.NativeSteps.Add(step.Pointer, __0);
            if (first)
            {
                var attacker = opening.cmbq;
                int hex = opening.cmbn?.Length == 1 ? opening.cmbn[0] : -1;
                int destination = attacker?.ctvn?.index ?? -1;
                var type = opening.cmbp?.cjuj?.csus;
                if ((type == Hex.Configs.EAttackType.Melee || type == Hex.Configs.EAttackType.Range)
                    && opening.cmbo != null && opening.cmbo.Length > 0) destination = opening.cmbo[0];
                var cell = hex >= 0 ? attacker?.ctuv?.clgw?.dpr(hex) : null;
                var defender = cell?.clbi;
                // A shown preview can be claimed by only one command, including nested schedulers.
                var preview = ReferenceEquals(scope.Shown, _shown) ? scope.Shown : null;
                if (defender == null && preview != null && cell?.fieldObjects != null)
                    foreach (var fieldObject in cell.fieldObjects)
                    {
                        var unit = fieldObject?.TryCast<Unit>();
                        if (Ptr(unit) == preview.Key.Defender) { defender = unit; break; }
                    }
                step.Target = Ptr(defender);
                var key = new AuditKey(Ptr(attacker?.ctuv), elb.lastBattleId, Ptr(attacker), Ptr(defender),
                    Ptr(opening.cmbp), hex, destination);
                string failure = null;
                if (!item.CommandObserved) failure = "Не наблюдалась исходная команда атаки";
                else if (preview == null || !AuditMatching.Matches(preview.Key, key,
                    Time.realtimeSinceStartupAsDouble - scope.ShownAt))
                    failure = "Нет свежего показанного прогноза с той же целью, атакой и клеткой";
                else if (!SameState(preview.Attacker, Snapshot(attacker)) || !SameState(preview.Defender, Snapshot(defender)))
                    failure = "Состояние отрядов изменилось между показом прогноза и командой";
                item.Preview = failure == null ? preview : null; item.PairingFailure = failure;
                _shown = _calculated = null;
                Write(new { type = "queued", id = item.Id, key, queue = command.Queue,
                    commandObserved = item.CommandObserved, pairingFailure = failure, preview = item.Preview });
                foreach (var prior in item.Steps) Write(new { type = "step_queued", id = item.Id, step = prior });
                Summary();
            }
            else if (command.HasOpening) Write(new { type = "step_queued", id = item.Id, step });
        });
    }

    private static AuditCommand CurrentCommand()
    {
        var queue = elb.ckia?.ckic?.clhf;
        return Commands.ForStep(Ptr(queue), Ptr(queue?.cmdt));
    }
    private static AuditStep CurrentStep(AuditCommand command) =>
        command?.Exchange.Steps.FirstOrDefault(s => s.Pointer == Ptr(elb.ckia?.ckic?.clhf?.cmdt));

    private static void OpeningStarted(etk __instance)
    {
        if (!Enabled) return;
        Guard(() =>
        {
            var command = Commands.ForStep(Ptr(__instance.cmec?.clhf), Ptr(__instance));
            if (command == null) return;
            var step = command.Exchange.Steps.Single(s => s.Pointer == Ptr(__instance));
            step.Started = true; step.Luck = __instance.cmbu;
            command.Exchange.OpeningStarted = true;
            command.Exchange.OpeningLuck ??= step.Luck;
            Write(new { type = "step_started", id = command.Id, step });
        });
    }

    private static void CounterStarted(etm __instance)
    {
        if (!Enabled) return;
        Guard(() =>
        {
            var command = Commands.ForStep(Ptr(__instance.cmec?.clhf), Ptr(__instance));
            if (command == null) return;
            var item = command.Exchange;
            var step = item.Steps.Single(s => s.Pointer == Ptr(__instance));
            step.Started = true; step.Luck = __instance.cmcd;
            var p = item.Preview;
            if (p != null && step.Caster == p.Key.Defender && step.Target == p.Key.Attacker)
            {
                item.CounterLuck = step.Luck;
                item.ActualCounter = $"{step.Ability:X}:{__instance.cmcb?.cjuf?.data?.attackPatternSid}";
                item.DefenderBeforeCounter = Snapshot(__instance.cmcf);
                item.AttackerBeforeCounter = Snapshot(__instance.cmcg);
            }
            Write(new { type = "step_started", id = command.Id, step,
                defender = item.DefenderBeforeCounter, attacker = item.AttackerBeforeCounter });
        });
    }

    // A scheduled counter can be cancelled; only the native execution call confirms it.
    private static void CounterExecuting(exk __instance)
    {
        if (!Enabled) return;
        Guard(() =>
        {
            var command = CurrentCommand(); var step = CurrentStep(command);
            if (step == null || step.Kind != "counter" || step.Caster != Ptr(__instance.cnaa) || step.Executed) return;
            step.Executed = true;
            command.Exchange.CounterStarted = true; command.Exchange.CounterSteps++;
            command.Exchange.CounterLuck = step.Luck;
            Write(new { type = "step_executing", id = command.Id, step });
        });
    }

    private static void BeforeDamage(exr __instance, ekf __0, ekg __1, ekb __2, int __3, out DamageScope __state)
    {
        __state = null;
        if (!Enabled) return;
        DamageScope scope = null;
        Guard(() =>
        {
            var source = __0?.TryCast<Unit>(); var target = __instance.cnck;
            if (source == null || target == null || !IsLive(target.ctuv, target.ctuv?.clhf)
                || Ptr(source.ctuv) != Ptr(target.ctuv)) return;
            var command = CurrentCommand(); var step = CurrentStep(command); var item = command?.Exchange;
            if (item?.Preview == null || step == null) return;
            var ability = __1?.TryCast<eib>();
            string phase = AuditRouting.Phase(step, item.Preview, Ptr(source), Ptr(target), Ptr(ability), Ptr(__2));
            if (phase == "opening") step.Executed = true;
            scope = new() { Exchange = item, Receiver = target, Source = source, Ability = ability,
                Dealer = __2, Before = Snapshot(target), SourceBefore = Snapshot(source), Input = __3, Phase = phase };
            DamageScopes.Push(scope);
        });
        __state = scope;
    }

    private static void Unattributed(ObjectTookDamageEventArgs e, AuditExchange item)
    {
        var key = item.Preview?.Key;
        if (key.HasValue && ((Ptr(e.caster) == key.Value.Attacker && Ptr(e.damageReceiver) == key.Value.Defender)
            || (Ptr(e.caster) == key.Value.Defender && Ptr(e.damageReceiver) == key.Value.Attacker)))
        { item.OtherDamageEvents++; item.UnattributedDirectDamage = true; }
    }

    // Called after damage has been applied and before the game's listeners.
    private static void DamageEvent(BattleEventArgs __1)
    {
        if (!Enabled) return;
        Guard(() =>
        {
            var e = __1?.TryCast<ObjectTookDamageEventArgs>();
            if (e == null) return;
            var command = CurrentCommand();
            var scope = DamageScopes.Count == 0 ? null : DamageScopes.Peek();
            bool matches = command != null && scope != null && scope.Exchange == command.Exchange && !scope.Seen
                && Ptr(e.caster) == Ptr(scope.Source) && Ptr(e.damageReceiver) == Ptr(scope.Receiver)
                && Ptr(e.castSource) == Ptr(scope.Ability) && Ptr(e.dealer) == Ptr(scope.Dealer);
            if (!matches)
            {
                var receiver = e.damageReceiver?.TryCast<Unit>();
                if (receiver == null || !IsLive(receiver.ctuv, receiver.ctuv?.clhf)) return;
                if (command == null)
                    foreach (var pending in Commands.Pending) Unattributed(e, pending.Exchange);
                else Unattributed(e, command.Exchange);
                return;
            }
            // The scope already proved battle ownership before damage. Do not
            // require a lethally hit unit to retain its map/battle membership.
            var item = scope.Exchange;
            scope.Seen = true;
            var hit = new AuditHit(scope.Phase, scope.Input, e.damage, e.stacksDestroyed,
                e.immuneToDamage, e.fromMech.ToString(), scope.SourceBefore, scope.Before, Snapshot(scope.Receiver));
            if (item.Hits.Count >= 128) { item.ObserverFailure = "Слишком много событий в одной команде"; return; }
            item.Hits.Add(hit);
            if (scope.Phase == "other") item.OtherDamageEvents++;
            Write(new { type = "damage", id = item.Id, step = Ptr(elb.ckia?.ckic?.clhf?.cmdt), hit });
        });
    }

    // Void finalizers neither replace nor suppress game exceptions.
    private static void AfterDamage(DamageScope __state, Exception __exception)
    {
        if (__state == null) return;
        Guard(() =>
        {
            if (DamageScopes.Count > 0 && ReferenceEquals(DamageScopes.Peek(), __state)) DamageScopes.Pop();
            else { DamageScopes.Clear(); __state.Exchange.ObserverFailure = "Нарушена вложенность событий урона"; }
            if (__exception != null) __state.Exchange.ObserverFailure = "Исключение в игровом обработчике урона";
            if (!__state.Seen && __state.Phase != "other") __state.Exchange.MissingDamageEvent = true;
        });
    }

    private static AuditKey Key(PreviewCapture.Context c) => new(Ptr(c.Attacker.ctuv), elb.lastBattleId,
        Ptr(c.Attacker), Ptr(c.Defender), Ptr(c.Attack), c.TargetHex.index, c.Destination?.index ?? c.Attacker.ctvn.index);
    private static long Ptr(Il2CppObjectBase value) => value?.Pointer.ToInt64() ?? 0;
    private static bool SameState(AuditUnit a, AuditUnit b) => JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);

    private static AuditUnit Snapshot(Unit unit)
    {
        var s = unit.stats;
        var stats = new SortedDictionary<string, float>(StringComparer.Ordinal)
        {
            ["offence"] = s.offence, ["defence"] = s.defence, ["damageMin"] = s.damageMin,
            ["damageMax"] = s.damageMax, ["armorPen"] = s.armorPen, ["attackPen"] = s.attackPen,
            ["outAllDmgMod"] = s.outAllDmgMod, ["inAllDmgMod"] = s.inAllDmgMod,
            ["finalDamageBonusPercent"] = s.finalDamageBonusPercent,
            ["finalAbilityDamageBonusPercent"] = s.finalAbilityDamageBonusPercent,
            ["damagePerHex"] = s.damagePerHex, ["maxInDmg"] = s.maxInDmg,
            ["stacksToLeave"] = s.stacksToLeave, ["overkillProtection"] = s.overkillProtection,
            ["numCounters"] = s.numCounters, ["remainingCountersNative"] = unit.cmwp.cnbt
        };
        var buffs = new List<string>();
        if (unit.ctuw?.ckby != null)
            foreach (var pair in unit.ctuw.ckby)
                if (pair.Value != null)
                    foreach (var buff in pair.Value)
                        if (buff != null) buffs.Add($"{buff.sid}:{buff.duration}:{buff.charges}");
        buffs.Sort(StringComparer.Ordinal);
        return new(Ptr(unit), unit.ctux, unit.ckyo, unit.ctvn?.index ?? -1,
            NativeForecast.Health(unit), stats, buffs.ToArray());
    }

    private static void Finish(CommandScope scope, bool completed, string reason)
    {
        var command = scope.Command; var item = command.Exchange;
        Commands.Remove(command); Pending.Remove(command.Id);
        if (!command.HasOpening) return;
        var verdict = item.Evaluate(completed, reason);
        Totals.TryGetValue(verdict.Status, out int count); Totals[verdict.Status] = count + 1;
        Write(new { type = "result", endUtc = DateTime.UtcNow, completed, reason, verdict, exchange = item });
        string line = $"#{item.Id}: {verdict.Status}. {verdict.Reason}";
        if (item.Preview != null)
            line += $"; {item.Preview.Attacker.Sid} / {item.Preview.Defender.Sid}; прогноз {item.Preview.Losses?.Numbers ?? "нет"}; потери {verdict.ActualLosses?.ToString() ?? "не оценены"}";
        Recent.Add(line); if (Recent.Count > 30) Recent.RemoveAt(0);
        Plugin.Instance.Log.LogInfo("Combat audit " + line);
        Summary();
    }

    private static void Write(object record) => _journal.Write(record);

    private static void Summary()
    {
        int Count(string key) => Totals.TryGetValue(key, out int n) ? n : 0;
        int all = Totals.Values.Sum(), ok = Count("matched"), bad = Count("mismatch");
        File.WriteAllText(_summaryPath,
            $"Retaliation Hints {Plugin.Version}. Автоматическая сверка боя\nОбновлено: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
            $"Завершённых команд: {all}\nВ пределах прогноза: {ok}\nРасхождений: {bad}\n" +
            $"Исключено из-за удачи/неудачи: {Count("excluded_luck")}\nОстальные пропуски проверки: {all - ok - bad - Count("excluded_luck")}\n" +
            $"Незавершённых команд: {Commands.Pending.Count(c => c.HasOpening)}\n" +
            $"Отфильтровано чужих команд/шагов: {_ignoredCommands}/{_ignoredSteps}\n" +
            "Совпадение означает только попадание наблюдавшегося результата в диапазон. Полное покрытие механик не подтверждено.\n" +
            $"Сеанс: {_journal.Session}\nТекущий файл: {_journal.CurrentPath}\n" +
            "При заполнении файла запись продолжается в следующей части. Предыдущие части сохраняются.\n\n" +
            string.Join("\n", Recent), new UTF8Encoding(false));
    }

    private static void Guard(Action action)
    {
        try { action(); }
        catch (Exception error)
        {
            foreach (var command in Commands.Pending)
                command.Exchange.ObserverFailure = error.GetType().Name + ": " + error.Message;
            Disable(error);
        }
    }

    private static void Disable(Exception error)
    {
        _ready = false;
        Plugin.Instance.Report("Combat audit disabled", error);
        try
        {
            if (_summaryPath != null)
                File.AppendAllText(_summaryPath,
                    $"\nСВЕРКА ОСТАНОВЛЕНА: {error.GetType().Name}: {error.Message}\n" +
                    "Незавершённые команды не считаются проверенными. Подсказка продолжает работать.\n",
                    new UTF8Encoding(false));
        }
        catch { /* The original error is already in the BepInEx log. */ }
    }
}
