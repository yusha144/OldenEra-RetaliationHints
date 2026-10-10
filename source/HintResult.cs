namespace OldenEra.RetaliationHints;

internal readonly record struct HintResult(string Text, bool Danger)
{
    internal const string Unavailable = "Потери: нет расчёта";

    internal static HintResult ForPreview(bool canCounter, Func<LossRange> calculate)
    {
        // Native refusal is conclusive without simulating any on-hit effects.
        // A failed estimate or zero casualties does not mean the enemy cannot hit.
        if (!canCounter) return new("Без контрудара", false);
        var loss = calculate();
        return new((loss.BeforeAttack ? "Потери до атаки ~ " : "Потери ~ ") + loss.Numbers,
            loss.Max > 0);
    }
}
