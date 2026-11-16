using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OfDragonsDeep;

/// <summary>
/// One export with everything: caught fish plus Fisher achievements, as a JSON string or a tracker link.
/// </summary>
public static class Exporter
{
    public const string TrackerUrl = "https://jadedzilla.github.io/of-dragons-deep/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record AchievementJson(
        [property: JsonPropertyName("id")] uint Id,
        [property: JsonPropertyName("complete")] bool Complete,
        [property: JsonPropertyName("current")] uint? Current,
        [property: JsonPropertyName("max")] uint? Max);

    /// <summary>
    /// <c>{"source":"OfDragonsDeep","version":2,"exportedAt":…,"fish":[item ids],"achievements":[{id,complete,current?,max?}]}</c>.
    /// <c>achievements</c> is left out when they couldn't be loaded.
    /// </summary>
    public static string ToJson(IEnumerable<FishEntry> fish, IReadOnlyList<AchievementEntry>? achievements)
    {
        var payload = new Dictionary<string, object>
        {
            ["source"] = "OfDragonsDeep",
            ["version"] = 2,
            ["exportedAt"] = DateTime.UtcNow.ToString("o"),
            ["fish"] = CaughtIds(fish),
        };
        if (achievements != null)
        {
            payload["achievements"] = achievements
                .OrderBy(a => a.Id)
                .Select(a => a.Complete
                    ? new AchievementJson(a.Id, true, null, null)
                    : new AchievementJson(a.Id, false, a.Current, a.Max))
                .ToList();
        }
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    /// <summary>
    /// <c>{TrackerUrl}?import=2.&lt;data&gt;</c>: base64url (no padding) of raw-DEFLATE'd LEB128 varints:
    /// fish count, then the sorted item ids as deltas; then, if achievements are included, their count and per
    /// achievement (sorted by id) the id delta and a status: 0 = not complete, 1 = complete,
    /// 2 = not complete followed by current and max progress.
    /// </summary>
    public static string TrackerLink(IEnumerable<FishEntry> fish, IReadOnlyList<AchievementEntry>? achievements)
    {
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            var ids = CaughtIds(fish);
            WriteVarint(deflate, (uint)ids.Count);
            uint prev = 0;
            foreach (var id in ids)
            {
                WriteVarint(deflate, id - prev);
                prev = id;
            }

            if (achievements != null)
            {
                var sorted = achievements.OrderBy(a => a.Id).ToList();
                WriteVarint(deflate, (uint)sorted.Count);
                prev = 0;
                foreach (var a in sorted)
                {
                    WriteVarint(deflate, a.Id - prev);
                    prev = a.Id;
                    if (a.Complete)
                    {
                        WriteVarint(deflate, 1);
                    }
                    else if (a.Current.HasValue && a.Max.HasValue)
                    {
                        WriteVarint(deflate, 2);
                        WriteVarint(deflate, a.Current.Value);
                        WriteVarint(deflate, a.Max.Value);
                    }
                    else
                    {
                        WriteVarint(deflate, 0);
                    }
                }
            }
        }

        var encoded = Convert.ToBase64String(buffer.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{TrackerUrl}?import=2.{encoded}";
    }

    private static List<uint> CaughtIds(IEnumerable<FishEntry> fish)
        => fish.Where(f => f.Caught).Select(f => f.ItemId).Distinct().OrderBy(id => id).ToList();

    private static void WriteVarint(Stream s, uint value)
    {
        while (value >= 0x80)
        {
            s.WriteByte((byte)(value & 0x7F | 0x80));
            value >>= 7;
        }
        s.WriteByte((byte)value);
    }
}
