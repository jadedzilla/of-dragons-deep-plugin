using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace FishExporter;

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

    private static string ToJson(IReadOnlyList<FishEntry> list, bool includeUncaught)
    {
        var payload = new
        {
            source = "FishExporter",
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

    public static string FormatLabel(ExportFormat f) => f switch
    {
        ExportFormat.Json => "JSON",
        ExportFormat.IdList => "Item IDs (comma separated)",
        ExportFormat.Csv => "CSV",
        ExportFormat.NameList => "Names (one per line)",
        _ => f.ToString(),
    };
}
