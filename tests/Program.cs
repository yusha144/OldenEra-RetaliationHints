using OldenEra.RetaliationHints;

static void Equal<T>(T expected, T actual, string reason)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{reason}: expected {expected}, received {actual}");
}

// A wounded top creature must die before a second full creature can be lost.
var wounded = new StackHealth(10, 10, 3);
Equal(new StackHealth(10, 10, 1), wounded.Hit(2), "sublethal wounded top");
Equal(new StackHealth(9, 10, 10), wounded.Hit(3), "exact top HP");
Equal(new StackHealth(8, 10, 10), wounded.Hit(13), "top plus full creature");
Equal(new StackHealth(0, 10, 0), wounded.Hit(9999), "overkill clamp");
Console.WriteLine("PASS: wounded creature and casualty boundaries");

var attacker = new StackHealth(20, 10, 10);
var defender = new StackHealth(10, 10, 10);
var none = ForecastMath.Evaluate(attacker, defender, false, false,
    (_, _, _) => throw new Exception("Disabled retaliation must not calculate damage"));
Equal(new LossRange(0, 0, false), none, "no counter");
Console.WriteLine("PASS: exhausted or prohibited retaliation");

// Switch between the same target's eligible/ineligible attacks without evaluating
// unsupported on-hit effects when the native game already disallows a counter.
int calculations = 0;
foreach (bool allowed in new[] { true, false, true, false })
{
    var hint = HintResult.ForPreview(allowed, () => { calculations++; return new(1, 3, false); });
    Equal(allowed ? "Потери ~ 1-3" : "Без контрудара", hint.Text, "native counter decision selects text");
    Equal(allowed, hint.Danger, "no-counter status is neutral");
}
Equal(2, calculations, "disabled retaliation bypasses damage/effect calculation");
Equal("Потери ~ 0", HintResult.ForPreview(true, () => new(0, 0, false)).Text,
    "counter with zero casualties is not an absent counter");
Equal("Потери до атаки ~ 2-4", HintResult.ForPreview(true, () => new(2, 4, true)).Text,
    "preemptive counter still reports casualties");
bool unavailable = false;
try { HintResult.ForPreview(true, () => throw new ForecastUnavailableException("unsupported effect")); }
catch (ForecastUnavailableException) { unavailable = true; }
Equal(true, unavailable, "unsupported calculation never becomes no counter");
Console.WriteLine("PASS: native no-counter status, transitions, zero casualties and unavailable estimates");

var normal = ForecastMath.Evaluate(attacker, defender, true, false,
    (source, _, counter) => counter ? new(source.Count * 2, source.Count * 4) : new(20, 50));
Equal(new LossRange(1, 3, false), normal, "surviving 5-8 enemies retaliate");
// An exhaustive integer sweep is an independent oracle for this simple scenario.
var losses = new List<int>();
for (int opening = 20; opening <= 50; opening++)
{
    int survivors = (100 - opening + 9) / 10;
    for (int reply = survivors * 2; reply <= survivors * 4; reply++) losses.Add(reply / 10);
}
Equal(losses.Min(), normal.Min, "minimum agrees with all intermediate rolls");
Equal(losses.Max(), normal.Max, "maximum agrees with all intermediate rolls");
Console.WriteLine("PASS: weaker retaliation after first-strike losses");

var lethal = ForecastMath.Evaluate(attacker, defender, true, false,
    (_, _, counter) => counter ? throw new Exception("Dead stack cannot retaliate") : new(100, 150));
Equal(new LossRange(0, 0, false), lethal, "certain kill");
var maybeLethal = ForecastMath.Evaluate(attacker, defender, true, false,
    (source, _, counter) => counter ? new(source.Count * 10, source.Count * 20) : new(80, 110));
Equal(new LossRange(0, 4, false), maybeLethal, "possible kill");
Console.WriteLine("PASS: certain and possible elimination of defender");

var preemptive = ForecastMath.Evaluate(attacker, defender, true, true,
    (source, _, counter) => counter ? new(source.Count * 2, source.Count * 4)
        : throw new Exception("Preemptive defender strikes before the opening attack"));
Equal(new LossRange(2, 4, true), preemptive, "full stack preemptive hit");
Console.WriteLine("PASS: preemptive retaliation uses original defender strength");

bool rejected = false;
try { ForecastMath.Evaluate(attacker, defender, true, false, (_, _, _) => new(-1, 2)); }
catch (ArgumentOutOfRangeException) { rejected = true; }
Equal(true, rejected, "unknown/invalid damage must not turn into zero losses");
Console.WriteLine("PASS: invalid forecasts fail explicitly");

// A ghost's added creatures are part of the next counterattack's target stack.
var ghost = new StackHealth(5, 10, 4);
var grown = ghost.Heal(18, int.MaxValue, true);
Equal(new StackHealth(7, 10, 2, 2, true), grown, "vampirism adds temporary creatures");
Equal(new StackHealth(6, 10, 10, 1, true), grown.Hit(2), "temporary wounded top dies first");
Equal(new StackHealth(5, 10, 10), grown.Hit(12), "temporary pool depleted");
Equal(new StackHealth(4, 10, 10), grown.Hit(22), "damage reaches original creatures");
Equal(new StackHealth(5, 10, 10), ghost.Heal(999, 50, false), "resurrection cap");
var withHealing = ForecastMath.Evaluate(ghost, defender, true, false,
    (_, _, counter) => counter ? new(8, 18) : new(18, 18),
    (state, dealt) => state.Heal(dealt, int.MaxValue, true));
Equal(new LossRange(1, 2, false), withHealing, "losses counted from healed stack, not net army growth");
var woundedHealed = ForecastMath.Evaluate(new(5, 10, 2), defender, true, false,
    (_, _, counter) => counter ? new(5, 5) : new(8, 8),
    (state, dealt) => state.Heal(dealt, 50, false));
Equal(new LossRange(0, 0, false), woundedHealed, "healing prevents wounded creature death");

// Both opening endpoints would give zero losses here; a middle roll gives one.
var middleRoll = ForecastMath.Evaluate(new(3, 10, 10), defender, true, false,
    (_, _, counter) => counter ? new(3, 3) : new(4, 14),
    (state, dealt) => state.Heal(dealt, int.MaxValue, true));
var healingOracle = Enumerable.Range(4, 11).Select(dealt =>
    (int)Math.Ceiling((30 + dealt) / 10.0) - (int)Math.Ceiling((30 + dealt - 3) / 10.0)).ToArray();
Equal(new LossRange(healingOracle.Min(), healingOracle.Max(), false), middleRoll, "all intermediate heal rolls");
Equal(1, middleRoll.Max, "intermediate casualty maximum");
Equal(new LossRange(2, 4, true), ForecastMath.Evaluate(attacker, defender, true, true,
    (_, _, _) => new(20, 40), (_, _) => throw new Exception("No healing before a preemptive counter")),
    "preemptive hit precedes vampirism");
Equal(new LossRange(0, 0, false), ForecastMath.Evaluate(ghost, defender, true, false,
    (_, _, _) => new(100, 10000), (_, _) => throw new Exception("No counter after certain kill")),
    "lethal opening bypasses healing work limit");
bool limited = false;
try { ForecastMath.Evaluate(ghost, new(1000, 10, 10), true, false,
    (_, _, _) => new(0, 1000), (state, _) => state); }
catch (ForecastUnavailableException) { limited = true; }
Equal(true, limited, "excessive healing range is unavailable rather than sampled");
Console.WriteLine("PASS: temporary creatures, healing caps, intermediate rolls and preemptive order");

var selection = new PreviewKey((IntPtr)1, (IntPtr)2, (IntPtr)3, (IntPtr)4, (IntPtr)5, (IntPtr)6, 42);
Equal(true, selection.Matches(selection), "callbacks for one visible forecast can join in either order");
foreach (var stale in new[] { selection with { Battle = (IntPtr)9 }, selection with { Hover = (IntPtr)9 },
    selection with { Attacker = (IntPtr)9 }, selection with { Target = (IntPtr)9 },
    selection with { Ability = (IntPtr)9 }, selection with { Cell = (IntPtr)9 }, selection with { Frame = 41 } })
    Equal(false, selection.Matches(stale), "old battle/target/direction/frame must not join new damage");
Console.WriteLine("PASS: preview selection identity rejects stale data");

// Runtime-normalized empty dealers exist even on plain attacks. Native defaults
// include statDmgMult=1: presence and nonzero multiplier alone must not veto them.
foreach (string target in new string[] { null, "", "none" })
    Equal(false, EffectContent.HasPayload("", null, false, target, 0, 0, 1),
        "empty side dealer with default multiplier still permits casualty calculation");
Equal(true, EffectContent.HasPayload("slow", null, false, "none", 0, 0, 1), "actual on-hit buff");
Equal(true, EffectContent.HasPayload(null, null, true, "none", 0), "actual side mechanic");
Equal(true, EffectContent.HasPayload(null, null, false, "self", 0, 10, 1), "actual side damage");
Console.WriteLine("PASS: empty normalized side-dealers do not block ordinary attacks");

// Three battles may reuse the same pooled host, canvas and fonts. Each battle
// must receive a fresh widget; updates within that battle must reuse its widget.
var bound = default(HintBinding);
int widgets = 0;
foreach (int generation in new[] { 1, 1, 2, 2, 3, 3 })
{
    var wanted = new HintBinding(generation, (IntPtr)10, (IntPtr)20, (IntPtr)30, (IntPtr)40);
    if (bound != wanted) { bound = wanted; widgets++; }
}
Equal(3, widgets, "pooled UI must rebind in every subsequent battle");
Equal(false, bound == (bound with { Parent = (IntPtr)21 }), "canvas replaced within battle");
Equal(false, bound == (bound with { Font = (IntPtr)31 }), "font replaced within battle");
Equal(false, bound == (bound with { Material = (IntPtr)41 }), "font material replaced within battle");
Console.WriteLine("PASS: successive battles and replaced display resources invalidate binding");

// The hint follows the attack-direction cursor and flips at edges without covering
// its hotspot. These are screen-pixel checks, not a claim of live Unity rendering.
foreach (var screen in new[] { (Width: 1280f, Height: 720f), (Width: 2560f, Height: 1440f) })
{
    float scale = Math.Clamp(screen.Height / 1080, .8f, 2f);
    foreach (var mouse in new[] { (X: screen.Width / 2, Y: screen.Height / 2),
        (X: screen.Width - 2, Y: screen.Height - 2), (X: 2f, Y: 2f), (X: screen.Width - 2, Y: 2f) })
    {
        var box = CursorPlacement.Place(screen.Width, screen.Height, mouse.X, mouse.Y, 240 * scale, 32 * scale, scale);
        Equal(true, box.Valid && box.X >= 0 && box.Y >= 0 && box.X + box.Width <= screen.Width
            && box.Y + box.Height <= screen.Height, "cursor hint fits viewport");
        Equal(false, mouse.X >= box.X - 16 * scale && mouse.X <= box.X + box.Width + 16 * scale
            && mouse.Y >= box.Y - 16 * scale && mouse.Y <= box.Y + box.Height + 16 * scale, "sword hotspot stays clear");
    }
}
Equal(false, CursorPlacement.Place(1920, 1080, -10, 200, 240, 32, 1).Valid, "outside game viewport");
Console.WriteLine("PASS: cursor placement and screen-edge flips");

if (args.Length > 0)
{
    var patterns = new AttackPatterns(); patterns.Load(args[0]);
    foreach (var sid in new[] { "attack_single_x100", "attack_reach_x1_x100_x100_with_delay",
        "attack_swipe_x100_x100", "attack_rumble_without_self_x1_x100_x50", "attack_swipe_x100_x100_x2" })
    {
        Equal(true, patterns.TryGet(sid, out float multiplier), sid);
        Equal(1f, multiplier, sid + " primary multiplier");
    }
    Equal(true, patterns.TryGet("attack_swirl_with_target_x50", out float half), "half-strength primary hit");
    Equal(.5f, half, "half-strength multiplier");
    Equal(false, patterns.TryGet("unknown-pattern", out _), "unknown pattern");
    Equal(true, patterns.IsSingleDamageCell("attack_single_x100"), "ghost's single-target life steal");
    Equal(false, patterns.IsSingleDamageCell("attack_swipe_x100_x100"), "multi-target healing cannot use one victim");
    Console.WriteLine($"PASS: installed game's counterattack patterns ({patterns.Count} supported patterns loaded)");
}
