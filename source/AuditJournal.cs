using System.Text;
using System.Text.Json;

namespace OldenEra.RetaliationHints;

// Rotate without deleting old evidence. A session can span several files.
public sealed class AuditJournal
{
    private readonly string _folder, _version;
    private readonly long _partLimit;
    private long _bytes;
    private int _part;
    public string Session { get; } = $"session-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
    public string CurrentPath { get; private set; }
    public AuditJournal(string folder, string version, long partLimit = 16 * 1024 * 1024)
    {
        _folder = folder; _version = version; _partLimit = partLimit;
        Directory.CreateDirectory(folder); StartPart();
    }
    private void StartPart()
    {
        CurrentPath = Path.Combine(_folder, $"{Session}-part{++_part:D3}.jsonl");
        _bytes = 0;
        Append(JsonSerializer.Serialize(new { type = "session", schema = 2, session = Session,
            part = _part, mod = _version, steamBuild = 25672315, startedUtc = DateTime.UtcNow,
            luckPolicy = "exclude_non_neutral", mode = "observe_only" }) + Environment.NewLine);
    }
    public void Write(object record)
    {
        string line = JsonSerializer.Serialize(record) + Environment.NewLine;
        int bytes = Encoding.UTF8.GetByteCount(line);
        if (bytes > _partLimit / 2) throw new IOException("Запись сверки превышает допустимый размер");
        if (_bytes + bytes > _partLimit) StartPart();
        Append(line);
    }
    private void Append(string line)
    {
        File.AppendAllText(CurrentPath, line, new UTF8Encoding(false));
        _bytes += Encoding.UTF8.GetByteCount(line);
    }
}
