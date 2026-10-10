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
    Console.WriteLine($"PASS: installed game's counterattack patterns ({patterns.Count} supported patterns loaded)");
}
