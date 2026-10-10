namespace OldenEra.RetaliationHints;

// A pooled native host can survive a battle while its canvas/font is replaced.
internal readonly record struct HintBinding(int BattleGeneration, IntPtr Host, IntPtr Parent,
    IntPtr Font, IntPtr Material);
