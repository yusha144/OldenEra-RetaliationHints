namespace OldenEra.RetaliationHints;

// Managed identities only. One scheduler invocation owns all of its queued
// opening/counter steps, even when another command is queued before it finishes.
public sealed class AuditStep
{
    public long Pointer { get; set; }
    public string Kind { get; set; }
    public long Caster { get; set; }
    public long Target { get; set; }
    public long Ability { get; set; }
    public long Dealer { get; set; }
    public bool Started { get; set; }
    public bool Executed { get; set; }
    public bool Finished { get; set; }
    public float? Luck { get; set; }
}

public sealed class AuditCommand
{
    public long Id { get; }
    public long Battle { get; }
    public long Queue { get; }
    public bool SchedulingComplete { get; set; }
    public AuditExchange Exchange { get; }
    public bool HasOpening => Exchange.Steps.Any(s => s.Kind == "opening");
    internal AuditCommand(long id, long battle, long queue)
    {
        Id = id; Battle = battle; Queue = queue;
        Exchange = new() { Id = id, CommandObserved = true };
    }
}

public static class AuditRouting
{
    public static bool IsLive(long battle, long queue, long activeBattle, long activeQueue) =>
        activeBattle != 0 && activeQueue != 0 && battle == activeBattle && queue == activeQueue;

    public static string Phase(AuditStep step, AuditPreview preview, long caster, long target, long ability, long dealer)
    {
        if (step == null || preview == null || dealer == 0 || step.Dealer != dealer || step.Ability != ability)
            return "other";
        var key = preview.Key;
        if (step.Kind == "opening" && caster == key.Attacker && target == key.Defender && caster == step.Caster)
            return "opening";
        if (step.Kind == "counter" && caster == key.Defender && target == key.Attacker
            && caster == step.Caster && target == step.Target) return "counter";
        return "other";
    }
}

public sealed class AuditCommandBook
{
    private long _next;
    private readonly Dictionary<long, AuditCommand> _commands = new();
    private readonly Dictionary<long, AuditCommand> _owners = new();
    public AuditCommand[] Pending => _commands.Values.ToArray();

    public AuditCommand Begin(long battle, long queue, long activeBattle, long activeQueue)
    {
        if (!AuditRouting.IsLive(battle, queue, activeBattle, activeQueue)) return null;
        if (_commands.Count >= 128) throw new InvalidOperationException("Слишком много незавершённых команд сверки");
        var command = new AuditCommand(++_next, battle, queue);
        _commands.Add(command.Id, command);
        return command;
    }

    public bool AddStep(AuditCommand command, AuditStep step)
    {
        if (!_commands.ContainsKey(command.Id) || step.Pointer == 0)
            throw new InvalidOperationException("Шаг без зарегистрированной команды");
        if (_owners.TryGetValue(step.Pointer, out var owner))
        {
            if (owner != command) throw new InvalidOperationException("Шаг принадлежит другой незавершённой команде");
            return false;
        }
        if (command.Exchange.Steps.Count >= 128) throw new InvalidOperationException("Слишком много шагов одной команды");
        command.Exchange.Steps.Add(step); _owners.Add(step.Pointer, command);
        return true;
    }

    public AuditCommand ForStep(long queue, long step) =>
        step != 0 && _owners.TryGetValue(step, out var command) && command.Queue == queue ? command : null;

    public void Remove(AuditCommand command)
    {
        foreach (var step in command.Exchange.Steps) _owners.Remove(step.Pointer);
        _commands.Remove(command.Id);
    }
}
