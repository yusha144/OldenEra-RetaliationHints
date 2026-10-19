using System.Text.Json;
using OldenEra.RetaliationHints;

internal static class CombatAuditTests
{
    private static int _checks;
    private static void Equal<T>(T expected, T actual, string reason)
    {
        _checks++;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Combat audit: {reason}: expected {expected}, received {actual}");
    }

    private static AuditUnit Unit(long pointer, StackHealth health) => new(pointer, (int)pointer,
        "unit-" + pointer, (int)pointer + 10, health, new() { ["maxInDmg"] = 0 }, Array.Empty<string>());

    // Hand-specified observations: 10 enemies with 10 HP take 30 damage, then
    // seven survivors kill two of our 20 creatures. No game functions are run.
    private static AuditExchange Ordinary()
    {
        var attacker = Unit(1, new(20, 10, 10));
        var defender = Unit(2, new(10, 10, 10));
        var remaining = Unit(2, new(7, 10, 10));
        return new()
        {
            Id = 1, Preview = new(new(100, 3, 1, 2, 5, 12, 11), attacker, defender,
                new(20, 50), new(1, 3, false), true, false, null, new()),
            OpeningStarted = true, CounterStarted = true, CounterSteps = 1,
            OpeningLuck = 1, CounterLuck = 1,
            Hits =
            {
                new("opening", 30, 30, 3, false, "None", attacker, defender, remaining),
                new("counter", 20, 20, 2, false, "None", remaining, attacker, Unit(1, new(18, 10, 10)))
            }
        };
    }

    private static void Forecast(AuditExchange x, LossRange? losses, DamageRange? opening = null,
        bool canCounter = true, bool preemptive = false)
    {
        var p = x.Preview;
        x.Preview = new(p.Key, p.Attacker, p.Defender, opening ?? p.Opening,
            losses, canCounter, preemptive, losses == null ? "Unsupported effect" : null, p.Trace);
    }

    private static string Status(AuditExchange x) => x.Evaluate(true, "queue finished").Status;

    public static void Run()
    {
        var x = Ordinary();
        Equal("matched", Status(x), "ordinary exchange");
        Equal(2, x.Evaluate(true, "done").ActualLosses.Value, "use exact event casualties");
        // One observed value inside a range does not validate its unobserved endpoints.
        Forecast(x, new(2, 2, false));
        Equal("matched", Status(x), "inclusive singleton");
        Forecast(x, new(0, 1, false));
        Equal("mismatch", Status(x), "counter casualties outside shown range");
        x = Ordinary(); Forecast(x, new(1, 3, false), new(40, 50));
        Equal(false, x.Evaluate(true, "done").OpeningDamageInRange.Value, "opening damage mismatch");
        Equal("mismatch", Status(x), "matching losses alone are insufficient");

        x = Ordinary(); Forecast(x, new(0, 0, false), new(100, 150));
        x.CounterStarted = false; x.CounterSteps = 0; x.CounterLuck = .5f;
        x.Hits.RemoveAt(1);
        x.Hits[0] = x.Hits[0] with { InputDamage = 150, Damage = 150, Killed = 10, After = Unit(2, new(0, 10, 0)) };
        Equal("matched", Status(x), "event retains full overkill; cancelled counter luck is irrelevant");
        Equal(0, x.Evaluate(true, "done").ActualLosses.Value, "confirmed finished lethal action has no counter");
        Forecast(x, new(0, 0, false), new(110, 140));
        Equal("mismatch", Status(x), "an actual overkill outside the native range is still a mismatch");
        x = Ordinary(); Forecast(x, new(0, 0, false), canCounter: false);
        x.CounterStarted = false; x.CounterSteps = 0; x.CounterLuck = null; x.Hits.RemoveAt(1);
        Equal("matched", Status(x), "native no-counter decision");
        x = Ordinary(); Forecast(x, new(0, 0, false));
        x.Hits[1] = x.Hits[1] with { InputDamage = 5, Damage = 5, Killed = 0, After = Unit(1, new(20, 10, 5)) };
        Equal("matched", Status(x), "counter that wounds but kills nobody");
        Forecast(x, new(0, 0, false), canCounter: false);
        Equal("mismatch", Status(x), "no-counter label contradicted even with zero casualties");

        x = Ordinary(); x.Hits.RemoveAt(1);
        Equal("missing_damage_event", Status(x), "executed counter without a damage event is not zero");
        x = Ordinary(); x.MissingDamageEvent = true;
        Equal("missing_damage_event", Status(x), "suppressed or missing event");
        x = Ordinary(); x.Hits.Clear();
        Equal("missing_damage_event", Status(x), "empty recording is not a successful check");
        x = Ordinary(); x.CounterStarted = false;
        Equal("unpaired", Status(x), "orphan counter event");
        x = Ordinary(); x.UnattributedDirectDamage = true;
        Equal("unpaired", Status(x), "unknown hit in same exchange");
        x = Ordinary(); x.OpeningLuck = null;
        Equal("unpaired", Status(x), "unknown opening multiplier");
        x = Ordinary(); x.CounterLuck = null;
        Equal("unpaired", Status(x), "unknown counter multiplier");
        x = Ordinary(); x.Hits.Add(x.Hits[1]);
        Equal("multiple_hits", Status(x), "multiple damage events");
        x = Ordinary(); x.CounterSteps = 2;
        Equal("multiple_hits", Status(x), "two counter actions are not one counter");
        x = Ordinary(); x.Hits.Add(x.Hits[1] with { Phase = "other", Killed = 9 });
        Equal(2, x.Evaluate(true, "done").ActualLosses.Value, "reflection and unrelated damage are not ordinary retaliation");

        foreach (float factor in new[] { .5f, 1.5f, 2f })
        {
            x = Ordinary(); x.OpeningLuck = factor;
            Equal("excluded_luck", Status(x), "opening luck/non-luck excluded even if result fits");
            x = Ordinary(); x.CounterLuck = factor;
            Equal("excluded_luck", Status(x), "counter luck/non-luck excluded");
        }
        x = Ordinary(); Forecast(x, null);
        Equal("unavailable", Status(x), "unavailable forecast is not zero");
        x = Ordinary(); Forecast(x, new(1, 3, true), preemptive: true);
        Equal("excluded_preemptive", Status(x), "scope is ordinary counters only");
        x = Ordinary(); x.Preview = null;
        Equal("unpaired", Status(x), "attacks without shown preview");
        x = Ordinary();
        Equal("incomplete", x.Evaluate(false, "battle changed").Status, "unfinished queue");
        x.ObserverFailure = "read failed";
        Equal("observer_error", Status(x), "observer failure cannot score success");

        // Death protection is deliberately NOT implemented in forecast arithmetic.
        // The observer must expose that discrepancy rather than reproduce it.
        x = Ordinary(); Forecast(x, new(1, 3, false), new(100, 150));
        x.Hits[0] = x.Hits[0] with { InputDamage = 150, Damage = 80, Killed = 8, After = Unit(2, new(2, 10, 10)) };
        Equal("mismatch", Status(x), "minimum survivors contradict ordinary health projection");
        Equal(false, x.Evaluate(true, "done").OpeningStateMatches.Value, "identify health transition discrepancy");
        x = Ordinary(); x.Preview.Defender.Stats["maxInDmg"] = 30;
        x.Hits[0] = x.Hits[0] with { InputDamage = 80 };
        Equal("matched", Status(x), "existing per-hit cap is not mistaken for death protection");
        x = Ordinary();
        var wounded = Unit(2, new(10, 10, 3));
        var p = x.Preview;
        x.Preview = new(p.Key, p.Attacker, wounded, new(13, 13), p.Losses, true, false, null, p.Trace);
        x.Hits[0] = x.Hits[0] with { InputDamage = 13, Damage = 13, Killed = 2,
            Before = wounded, After = Unit(2, new(8, 10, 10)) };
        Equal("matched", Status(x), "wounded top is preserved in saved health");

        var key = Ordinary().Preview.Key;
        Equal(true, AuditMatching.Matches(key, key, 2), "click transition retains last shown preview briefly");
        foreach (double age in new[] { -1d, 2.001, double.NaN, double.PositiveInfinity })
            Equal(false, AuditMatching.Matches(key, key, age), "stale/invalid timestamp");
        foreach (var other in new[] { key with { Battle = 9 }, key with { BattleId = 9 },
            key with { Attacker = 9 }, key with { Defender = 9 }, key with { Ability = 9 },
            key with { TargetHex = 9 }, key with { Destination = 9 } })
            Equal(false, AuditMatching.Matches(key, other, 0), "wrong battle/target/attack/direction");
        string Phase(long step, long caster, long target, long ability, long dealer) =>
            AuditMatching.Phase(step, 100, 200, caster, target, ability, dealer, 1, 2, 5, 6, 7, 8);
        Equal("opening", Phase(100, 1, 2, 5, 6), "opening identity");
        Equal("counter", Phase(200, 2, 1, 7, 8), "counter identity");
        Equal("other", Phase(200, 2, 1, 7, 9), "secondary dealer cannot impersonate direct counter");
        Equal("other", Phase(100, 2, 1, 7, 8), "right pair but wrong action");
        Equal("other", Phase(200, 1, 2, 7, 8), "wrong direction");
        Equal("other", Phase(0, 2, 1, 7, 8), "missing current action");

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(Ordinary()));
        Equal(100L, json.RootElement.GetProperty("Preview").GetProperty("Defender")
            .GetProperty("Health").GetProperty("Total").GetInt64(), "serialized health, not native object reference");
        Equal(2, json.RootElement.GetProperty("Hits")[1].GetProperty("Killed").GetInt32(), "exact casualties survive JSON export");
        Equal("matched", Status(JsonSerializer.Deserialize<AuditExchange>(JsonSerializer.Serialize(Ordinary()))),
            "saved observations can be replayed through the production evaluator");
        CheckCommandSequence();
        CheckJournalRotation();
        Console.WriteLine($"PASS: passive combat audit ({_checks} assertions: pairing, completed actions, casualties, exclusions, health and JSON)");
    }

    private static void CheckCommandSequence()
    {
        var book = new AuditCommandBook();
        var a = book.Begin(10, 20, 10, 20);
        a.Exchange.Preview = Ordinary().Preview;
        var first = new AuditStep { Pointer = 211, Kind = "opening", Caster = 1, Target = 2, Ability = 5, Dealer = 6 };
        book.AddStep(a, first);
        Equal(null, book.Begin(30, 40, 10, 20), "foreign simulated battle is rejected before touching live state");
        Equal(null, book.Begin(10, 40, 10, 20), "same battle but wrong queue is rejected");
        Equal(null, book.Begin(0, 0, 0, 0), "no live battle");
        Equal(a, book.ForStep(20, 211), "ignored simulation cannot replace the pending opening");
        Equal(1, book.Pending.Length, "foreign simulations produce no pending records");

        // Regression for old #211: a second queued strike with unresolved target
        // must not finish/reset the original record. Identities are synthetic;
        // the old log did not record native command/step pointers.
        var second = new AuditStep { Pointer = 212, Kind = "opening", Caster = 1, Target = 0, Ability = 5, Dealer = 6 };
        book.AddStep(a, second);
        Equal(a, book.ForStep(20, 211), "first strike retained after a second strike queues");
        Equal(a, book.ForStep(20, 212), "all strikes of one command retain the original forecast");
        Equal(2, a.Exchange.Steps.Count, "second strike kept even when its target is temporarily absent");
        Equal(false, book.AddStep(a, second), "duplicate queue callback cannot double-count a strike");
        a.Exchange.OpeningStarted = true; a.Exchange.OpeningLuck = 1;
        a.Exchange.Hits.Add(Ordinary().Hits[0]);
        Equal("multiple_hits", Status(a.Exchange), "sequence is recorded but not mis-scored as a single counter");
        second.Started = true; second.Luck = 1.5f;
        Equal("excluded_luck", Status(a.Exchange), "luck on a later strike also excludes the sequence");

        var b = book.Begin(10, 20, 10, 20);
        var third = new AuditStep { Pointer = 213, Kind = "opening", Caster = 3, Target = 4, Ability = 7, Dealer = 8 };
        book.AddStep(b, third);
        Equal(a, book.ForStep(20, 211), "another queued command cannot overwrite the first command");
        Equal(b, book.ForStep(20, 213), "another command owns its own step");
        Equal(null, book.ForStep(99, 211), "routing validates queue identity");
        Equal("opening", AuditRouting.Phase(first, a.Exchange.Preview, 1, 2, 5, 6), "registered opening routes by actual step");
        Equal("other", AuditRouting.Phase(first, a.Exchange.Preview, 2, 1, 5, 6), "reversed hit is not an opening");
        var counter = new AuditStep { Pointer = 214, Kind = "counter", Caster = 2, Target = 1, Ability = 9, Dealer = 10 };
        book.AddStep(a, counter);
        Equal("counter", AuditRouting.Phase(counter, a.Exchange.Preview, 2, 1, 9, 10), "counter is attached to its command");
        Equal("other", AuditRouting.Phase(counter, a.Exchange.Preview, 2, 1, 9, 11), "side dealer is not direct retaliation");
        book.Remove(a);
        Equal(null, book.ForStep(20, 211), "finished command releases first strike");
        Equal(null, book.ForStep(20, 212), "finished command releases subsequent strike");
        Equal(b, book.ForStep(20, 213), "finishing one command preserves the other");
        Equal(1, book.Pending.Length, "only the finished command is removed");
    }

    private static void CheckJournalRotation()
    {
        string folder = Path.Combine(Path.GetTempPath(), "RetaliationHints-AuditTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var writer = new AuditJournal(folder, "test", 1024);
            for (int i = 0; i < 25; i++) writer.Write(new { type = "sample", id = i, text = new string('x', 100) });
            var files = Directory.GetFiles(folder, "*.jsonl").OrderBy(p => p).ToArray();
            Equal(true, files.Length > 1, "full journal continues in another file");
            var ids = new List<int>();
            foreach (string file in files)
            {
                Equal(true, new FileInfo(file).Length <= 1024, "rotation observes per-file limit");
                var rows = File.ReadAllLines(file).Select(s => JsonSerializer.Deserialize<JsonElement>(s)).ToArray();
                Equal("session", rows[0].GetProperty("type").GetString(), "each part identifies its session");
                Equal(writer.Session, rows[0].GetProperty("session").GetString(), "same session across files");
                ids.AddRange(rows.Skip(1).Select(r => r.GetProperty("id").GetInt32()));
            }
            Equal(string.Join(",", Enumerable.Range(0, 25)), string.Join(",", ids), "rotation loses or duplicates no records");
        }
        finally
        {
            // Only files created by this test, no recursive filesystem operation.
            foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }
    }
}
