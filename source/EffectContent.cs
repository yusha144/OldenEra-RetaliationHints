namespace OldenEra.RetaliationHints;

// Empty, allocated side-dealer configs are normal game data, not effects.
internal static class EffectContent
{
    internal static bool HasPayload(string buff, string sideBuff, bool mechanics, string damageTarget,
        params float[] damage) =>
        !string.IsNullOrWhiteSpace(buff) || !string.IsNullOrWhiteSpace(sideBuff) || mechanics
        || (!string.IsNullOrWhiteSpace(damageTarget)
            && !string.Equals(damageTarget, "none", StringComparison.OrdinalIgnoreCase)
            && damage.Any(value => value != 0));
}
