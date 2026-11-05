using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Security.Cryptography;

namespace OldenEra.RetaliationHints;

[BepInPlugin(Id, "Olden Era Retaliation Hints", Version)]
public sealed class Plugin : BasePlugin
{
    internal const string Id = "local.oldenera.retaliationhints";
    internal const string Version = "0.1.16";
    internal static Plugin Instance;
    internal ConfigEntry<bool> Enabled;
    internal ConfigEntry<float> TextScale;
    internal ConfigEntry<bool> CombatAuditEnabled;
    internal readonly AttackPatterns Patterns = new();
    private Harmony _patches;
    private readonly HashSet<string> _reported = new();
    private readonly HashSet<string> _traced = new();
    private readonly HashSet<string> _matchups = new();

    internal void BeginBattleDiagnostics(int battle)
    {
        _traced.Clear();
        Log.LogInfo($"Battle binding changed: {battle}. Display resources will be rebound.");
    }

    internal void TraceMatch(Hex.Session.Battle.Unit attacker, Hex.Session.Battle.Unit target, bool allowed, bool paired)
    {
        string key = $"{emh.lastBattleId}:{attacker.cndf}->{target.cndf}:{allowed}:{paired}";
        if (_matchups.Count < 48 && _matchups.Add(key))
            Log.LogInfo($"Preview pair [{key}]: battle={emh.lastBattleId}, temporary={attacker.cphj.tempFullStacks}/{target.cphj.tempFullStacks}.");
    }

    public override void Load()
    {
        Instance = this;
        Enabled = Config.Bind("Display", "Enabled", true, "Show a local estimate of retaliation casualties during a native battle attack preview.");
        TextScale = Config.Bind("Display", "TextScale", 1f, "Text size multiplier (0.7 to 1.8). Does not affect battle rules.");
        CombatAuditEnabled = Config.Bind("Diagnostics", "CombatAudit", false,
            "Автоматическая сверка показанного прогноза с событиями боя. Удача и неудача исключаются. Отчёты сохраняются локально. Требуется перезапуск игры.");
        try
        {
            if (!Matches("GameAssembly.dll", "D5C932FC0E22BE3049204773CF2A9CC57F6A84D08540921D99506D53B96386E2")
                || !Matches("HeroesOldenEra_Data/il2cpp_data/Metadata/global-metadata.dat", "C40298DE4A952A0CE5158BF07C392335EEA1F6B39B544101FF04B974794D1105"))
            { Log.LogWarning("Retaliation Hints disabled: unsupported game build (expected Steam 26428102)."); return; }
            Patterns.Load(Path.Combine(Paths.GameRootPath, "HeroesOldenEra_Data", "StreamingAssets", "Core.zip"));
            _patches = new Harmony(Id);
            _patches.PatchAll(typeof(Plugin).Assembly);
            CombatAudit.Initialize();
            AddComponent<RetaliationOverlay>();
            Log.LogInfo($"Retaliation Hints {Version} loaded; {Patterns.Count} supported attack patterns. Current-state estimate; optional passive combat audit. No battle commands or RNG calls.");
        }
        catch (Exception error)
        {
            _patches?.UnpatchSelf();
            Log.LogError($"Retaliation Hints could not start: {error}");
        }
    }

    internal void Report(string context, Exception error)
    {
        if (_reported.Add(context + error.GetType().Name)) Log.LogWarning($"{context}: {error}");
    }

    internal void TraceOnce(string stage, string detail)
    {
        if (_traced.Count < 24 && _traced.Add(stage)) Log.LogInfo($"Preview stage [{stage}]: {detail}");
    }

    private static bool Matches(string path, string expected)
    {
        using var stream = File.OpenRead(Path.Combine(Paths.GameRootPath, path));
        using var hash = SHA256.Create();
        return Convert.ToHexString(hash.ComputeHash(stream)) == expected;
    }
}
