using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace OfDragonsDeep;

public static class Exporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public static string Export(IReadOnlyList<FishEntry> fish, ExportFormat format, bool includeUncaught)
    {
        var list = includeUncaught ? fish : fish.Where(f => f.Caught).ToList();

        return format switch
        {
            ExportFormat.Json => ToJson(list, includeUncaught),
            ExportFormat.IdList => string.Join(",", list.Where(f => f.Caught).Select(f => f.ItemId)),
            ExportFormat.Csv => ToCsv(list),
            ExportFormat.NameList => string.Join("\n", list.Where(f => f.Caught).Select(f => f.Name)),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    public const string TrackerUrl = "https://jadedzilla.github.io/of-dragons-deep/";

    /// <summary>
    /// Builds a link that imports the caught fish into the Of Dragons Deep tracker:
    /// <c>?import=1.&lt;base64url(deflate-raw(varint deltas of the sorted item ids))&gt;</c>.
    /// A full log fits in roughly 400 characters.
    /// </summary>
    public static string TrackerLink(IEnumerable<FishEntry> fish)
    {
        var ids = fish.Where(f => f.Caught).Select(f => f.ItemId).Distinct().OrderBy(id => id);

        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            uint prev = 0;
            foreach (var id in ids)
            {
                var delta = id - prev;
                prev = id;
                while (delta >= 0x80)
                {
                    deflate.WriteByte((byte)(delta & 0x7F | 0x80));
                    delta >>= 7;
                }
                deflate.WriteByte((byte)delta);
            }
        }

        var encoded = Convert.ToBase64String(buffer.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{TrackerUrl}?import=1.{encoded}";
    }

    private static string ToJson(IReadOnlyList<FishEntry> list, bool includeUncaught)
    {
        var payload = new
        {
            source = "OfDragonsDeep",
            version = 1,
            exportedAt = DateTime.UtcNow.ToString("o"),
            fish = list.Select(f => includeUncaught
                ? (object)new { id = f.ItemId, name = f.Name, type = KindName(f.Kind), caught = f.Caught }
                : new { id = f.ItemId, name = f.Name, type = KindName(f.Kind) }),
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string ToCsv(IReadOnlyList<FishEntry> list)
    {
        var sb = new StringBuilder("id,name,type,caught\n");
        foreach (var f in list)
            sb.Append(f.ItemId).Append(',')
              .Append(CsvEscape(f.Name)).Append(',')
              .Append(KindName(f.Kind)).Append(',')
              .Append(f.Caught ? "true" : "false").Append('\n');
        return sb.ToString();
    }

    private static string CsvEscape(string s)
        => s.IndexOfAny([',', '"', '\n']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    private static string KindName(FishKind k) => k == FishKind.Fishing ? "fishing" : "spearfishing";

    public static string ExportAchievements(IReadOnlyList<AchievementEntry> list, ExportFormat format)
    {
        return format switch
        {
            ExportFormat.Json => JsonSerializer.Serialize(new
            {
                source = "OfDragonsDeep",
                version = 1,
                exportedAt = DateTime.UtcNow.ToString("o"),
                category = "Fisher",
                completed = list.Count(a => a.Complete),
                total = list.Count,
                points = list.Where(a => a.Complete).Sum(a => (int)a.Points),
                totalPoints = list.Sum(a => (int)a.Points),
                achievements = list.Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    points = a.Points,
                    complete = a.Complete,
                    current = a.Complete ? a.Max ?? a.Current : a.Current,
                    max = a.Max,
                }),
            }, JsonOptions),
            ExportFormat.IdList => string.Join(",", list.Where(a => a.Complete).Select(a => a.Id)),
            ExportFormat.Csv => AchievementsCsv(list),
            ExportFormat.NameList => string.Join("\n", list.Select(a =>
                (a.Complete ? "[x] " : "[ ] ") + a.Name +
                (!a.Complete && a.Current.HasValue && a.Max.HasValue ? $" ({a.Current}/{a.Max})" : ""))),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    private static string AchievementsCsv(IReadOnlyList<AchievementEntry> list)
    {
        var sb = new StringBuilder("id,name,points,complete,current,max\n");
        foreach (var a in list)
            sb.Append(a.Id).Append(',')
              .Append(CsvEscape(a.Name)).Append(',')
              .Append(a.Points).Append(',')
              .Append(a.Complete ? "true" : "false").Append(',')
              .Append(a.Current?.ToString() ?? "").Append(',')
              .Append(a.Max?.ToString() ?? "").Append('\n');
        return sb.ToString();
    }

    public static string FormatLabel(ExportFormat f) => f switch
    {
        ExportFormat.Json => "JSON",
        ExportFormat.IdList => "IDs (comma separated)",
        ExportFormat.Csv => "CSV",
        ExportFormat.NameList => "Names (one per line)",
        _ => f.ToString(),
    };
}
