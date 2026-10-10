using System.IO.Compression;
using System.Text.Json;

namespace OldenEra.RetaliationHints;

internal sealed class AttackPatterns
{
    private readonly Dictionary<string, float> _primary = new();
    internal int Count => _primary.Count;
    internal bool TryGet(string sid, out float multiplier)
    {
        multiplier = 0;
        return sid != null && _primary.TryGetValue(sid, out multiplier);
    }
    internal void Load(string archive)
    {
        _primary.Clear();
        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("DB/attack_patterns/", StringComparison.Ordinal) && e.FullName.EndsWith(".json")))
        {
            using var reader = new StreamReader(entry.Open());
            using var doc = JsonDocument.Parse(reader.ReadToEnd());
            foreach (var pattern in doc.RootElement.GetProperty("array").EnumerateArray())
            {
                float? value = null; bool valid = true;
                foreach (var rotation in pattern.GetProperty("rotations").EnumerateArray())
                {
                    int x = rotation.GetProperty("charX").GetInt32(), y = rotation.GetProperty("charY").GetInt32();
                    int width = rotation.GetProperty("dimX").GetInt32();
                    var cells = rotation.GetProperty("damagePattern");
                    int index = y * width + x;
                    if (index < 0 || index >= cells.GetArrayLength()) { valid = false; break; }
                    float current = cells[index].GetSingle();
                    if (!float.IsFinite(current) || current <= 0 || (value.HasValue && Math.Abs(value.Value - current) > .0001f))
                    { valid = false; break; }
                    value = current;
                }
                if (valid && value.HasValue) _primary[pattern.GetProperty("sid").GetString()] = value.Value;
            }
        }
    }
}
