using System;
using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace OfDragonsDeep;

public enum FishKind
{
    Fishing,
    Spearfishing,
}

/// <param name="ItemId">Item sheet row id — the stable id most trackers (Teamcraft, Garland, etc.) use.</param>
/// <param name="LogId">FishParameter / SpearfishingItem row id (the in-game log index).</param>
public sealed record FishEntry(uint ItemId, uint LogId, string Name, FishKind Kind, bool Caught);

public static class FishCollector
{
    /// <summary>
    /// Reads the caught-fish bitmasks from PlayerState. Must be called on the framework thread while logged in.
    /// Returns null if player data isn't loaded yet.
    /// </summary>
    public static unsafe List<FishEntry>? Collect(bool includeSpearfishing)
    {
        var ps = PlayerState.Instance();
        if (ps == null || !ps->IsLoaded)
            return null;

        var result = new List<FishEntry>();

        // Line fishing: FishParameter rows that appear in the Fishing Log.
        var fishSheet = Plugin.DataManager.GetExcelSheet<FishParameter>();
        foreach (var row in fishSheet)
        {
            if (!row.IsInLog)
                continue;
            if (!row.Item.TryGetValue<Item>(out var item) || item.RowId == 0)
                continue;

            bool caught;
            try
            {
                caught = ps->IsFishCaught(row.RowId);
            }
            catch (Exception)
            {
                // Sheet is newer than the bitmask size known to ClientStructs; skip.
                continue;
            }

            result.Add(new FishEntry(item.RowId, row.RowId, item.Name.ExtractText(), FishKind.Fishing, caught));
        }

        if (includeSpearfishing)
        {
            var spearSheet = Plugin.DataManager.GetExcelSheet<SpearfishingItem>();
            foreach (var row in spearSheet)
            {
                // Rows >= 30000 are not part of the spearfishing log bitmask.
                if (row.RowId <= 20000 || row.RowId >= 30000)
                    continue;
                if (!row.Item.IsValid || row.Item.RowId == 0)
                    continue;

                bool caught;
                try
                {
                    caught = ps->IsSpearfishCaught(row.RowId);
                }
                catch (Exception)
                {
                    continue;
                }

                var item = row.Item.Value;
                result.Add(new FishEntry(item.RowId, row.RowId, item.Name.ExtractText(), FishKind.Spearfishing, caught));
            }
        }

        // An item can appear more than once; keep one entry per item, preferring "caught".
        return result
            .GroupBy(f => f.ItemId)
            .Select(g => g.OrderByDescending(f => f.Caught).First())
            .OrderBy(f => f.Kind)
            .ThenBy(f => f.LogId)
            .ToList();
    }
}
