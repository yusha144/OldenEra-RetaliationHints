namespace OldenEra.RetaliationHints;

internal readonly record struct PreviewKey(IntPtr Battle, IntPtr Hover, IntPtr Attacker,
    IntPtr Target, IntPtr Ability, IntPtr Cell, int Frame)
{
    internal bool Matches(PreviewKey other) => Battle != IntPtr.Zero && this == other;
}
