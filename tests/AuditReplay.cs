using System.Security.Cryptography;
using System.Text.Json;
using OldenEra.RetaliationHints;

internal static class AuditReplay
{
    public static void Run(string path, string reportPath = null, bool fixture = false)
    {
        var rows = File.ReadLines(path).Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => JsonSerializer.Deserialize<JsonElement>(s)).ToArray();
        var results = rows.Where(r => r.GetProperty("type").GetString() == "result").ToArray();
        var queued = rows.Where(r => r.GetProperty("type").GetString() == "queued")
            .ToDictionary(r => r.GetProperty("id").GetInt64());
        var samples = results.Select(r => new
        {
            Row = r, Exchange = r.GetProperty("exchange").Deserialize<AuditExchange>(),
            Old = r.GetProperty("verdict").Deserialize<AuditVerdict>()
        }).ToArray();
        var liveBattles = samples.Where(s => s.Exchange.Preview != null)
            .Select(s => s.Exchange.Preview.Key.Battle).ToHashSet();
        var verdicts = new List<AuditVerdict>();
        var retained = new List<AuditVerdict>();
        var changes = new List<object>();
        var missing = new List<long>();
        int filtered = 0, eligible = 0;
        foreach (var sample in samples)
        {
            var x = sample.Exchange;
            var verdict = x.Evaluate(sample.Row.GetProperty("completed").GetBoolean(), sample.Row.GetProperty("reason").GetString());
            bool accepted = true;
            if (x.Preview != null && !sample.Row.GetProperty("completed").GetBoolean()) missing.Add(x.Id);
            if (queued.TryGetValue(x.Id, out var q))
            {
                var key = q.GetProperty("key").Deserialize<AuditKey>();
                // v1 did not store active UI or queue identities. Known live battle
                // objects come only from successful preview pairing in this file.
                // Queue guards and command lifetime are checked by separate tests.
                accepted = liveBattles.Any(b => AuditRouting.IsLive(key.Battle, 1, b, 1));
                if (!accepted)
                {
                    if (x.Preview != null) throw new Exception("Filter discarded a real paired preview");
                    filtered++;
                }
            }
            if (sample.Old.Status == "matched" && verdict.Status != "matched")
                throw new Exception($"Previously valid observation #{x.Id} regressed to {verdict.Status}");
            if (sample.Old.Status != verdict.Status)
            {
                bool expectedCorrection = sample.Old.Status == "mismatch" && verdict.Status == "matched"
                    && sample.Old.LossesInRange == true && sample.Old.OpeningStateMatches == true
                    && x.Hits.Any(h => h.Phase == "opening" && h.Damage > h.Before.Health.Total);
                if (!expectedCorrection) throw new Exception($"Unexpected verdict change for #{x.Id}");
                changes.Add(new { id = x.Id, oldStatus = sample.Old.Status, newStatus = verdict.Status, reason = "full_overkill_damage" });
            }
            if (verdict.Status is "matched" or "mismatch")
            {
                eligible++;
                // Independent observation checks: native casualties equal change in
                // creature count; HP loss is distinct from full event damage.
                foreach (var hit in x.Hits.Where(h => h.Phase is "opening" or "counter"))
                    if (hit.Killed != hit.Before.Health.Count - hit.After.Health.Count)
                        throw new Exception($"Recorded casualty transition disagrees for #{x.Id}");
            }
            verdicts.Add(verdict);
            if (accepted) retained.Add(verdict);
        }
        if (fixture && (changes.Count != 11 || verdicts.Count(v => v.Status == "matched") != 12 || !missing.Contains(211)))
            throw new Exception("Captured 0.1.11 regression fixture changed unexpectedly");
        var report = new
        {
            schema = 1, evaluator = "0.1.12", source = Path.GetFullPath(path),
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            results = results.Length, paired = samples.Count(s => s.Exchange.Preview != null), eligible,
            correctedOverkillVerdicts = changes.Count, changes,
            allReevaluatedStatuses = verdicts.GroupBy(v => v.Status).ToDictionary(g => g.Key, g => g.Count()),
            retainedRecords = retained.Count,
            retainedBattleStatuses = retained.GroupBy(v => v.Status).ToDictionary(g => g.Key, g => g.Count()),
            knownLiveBattleObjects = liveBattles.Count, foreignBattleRecords = filtered,
            filterEvidence = "v1 battle identities only; active queue identity was not recorded",
            sequenceEvidence = "native command boundaries and discarded damage cannot be recovered from v1; covered by separate queue regression tests",
            unrecoverableIncompletePaired = missing
        };
        if (reportPath != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"PASS: saved audit replay: {results.Length} results, {eligible} eligible, {changes.Count} overkill corrections, {filtered} foreign-battle records, {missing.Count} unrecoverable incomplete paired record(s)");
    }
}
