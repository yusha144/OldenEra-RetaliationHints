using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using System.Security.Cryptography;

namespace OldenEra.RetaliationHints;

[BepInPlugin(Id, "Olden Era Retaliation Hints", "0.1.0")]
public sealed class Plugin : BasePlugin
{
    internal const string Id = "local.oldenera.retaliationhints";
    internal static Plugin Instance;
    internal ConfigEntry<bool> Enabled;
    internal ConfigEntry<float> TextScale;
    internal readonly AttackPatterns Patterns = new();
    private Harmony _patches;
    private readonly HashSet<string> _reported = new();

    public override void Load()
    {
        Instance = this;
        Enabled = Config.Bind("Display", "Enabled", true, "Show a local estimate of retaliation casualties during a native battle attack preview.");
        TextScale = Config.Bind("Display", "TextScale", 1f, "Text size multiplier (0.7 to 1.8). Does not affect battle rules.");
        try
        {
            if (!Matches("GameAssembly.dll", "D9972B0E7F7DD1C17758808E2F1B80E0A728946416D9A41DEAA99BAD744DE53A")
                || !Matches("HeroesOldenEra_Data/il2cpp_data/Metadata/global-metadata.dat", "99117DC93F4B427967A2FCA49378E1B39B3202401B15234A98328FAA7D489817"))
            { Log.LogWarning("Retaliation Hints disabled: unsupported game build (expected Steam 25672315)."); return; }
            Patterns.Load(Path.Combine(Paths.GameRootPath, "HeroesOldenEra_Data", "StreamingAssets", "Core.zip"));
            _patches = new Harmony(Id);
            _patches.PatchAll(typeof(Plugin).Assembly);
            AddComponent<RetaliationOverlay>();
            Log.LogInfo($"Retaliation Hints 0.1.0 loaded; {Patterns.Count} supported attack patterns. Current-state estimate, excluding luck and triggered effects; no battle commands or RNG calls.");
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

    private static bool Matches(string path, string expected)
    {
        using var stream = File.OpenRead(Path.Combine(Paths.GameRootPath, path));
        using var hash = SHA256.Create();
        return Convert.ToHexString(hash.ComputeHash(stream)) == expected;
    }
}
