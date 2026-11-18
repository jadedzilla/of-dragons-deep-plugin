using System;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace OfDragonsDeep;

/// <summary>
/// The Fish Guide's green "new" dots: the game keeps newly recorded fish in <see cref="FishingModule"/> (a sorted
/// set, a 50-slot list and a count, saved in the character's settings file) until each one is viewed.
/// </summary>
public static unsafe class UnseenFish
{
    /// <summary>How many fish currently have the "new" dot (0 if unknown).</summary>
    public static int Count()
    {
        var module = FishingModule.Instance();
        return module == null ? 0 : Math.Max(module->SortedUnseenFish.Count, module->UnseenFishCount);
    }

    /// <summary>
    /// Marks every fish as seen: empties the set and list, saves the settings file, and refreshes an open
    /// Fish Guide. Returns how many were cleared. Must run on the framework (UI) thread.
    /// </summary>
    public static int MarkAllSeen()
    {
        var module = FishingModule.Instance();
        if (module == null)
            return 0;

        var cleared = Count();
        if (cleared == 0)
            return 0;

        module->SortedUnseenFish.Clear(); // frees the nodes with the game's allocator
        foreach (ref var id in module->UnseenFish)
            id = 0;
        module->UnseenFishCount = 0;

        // Same as the game's own list changes (e.g. favourite mounts): flag the file and save it.
        module->IsSavePending = true;
        module->SaveFile(false);

        var agent = AgentFishGuide.Instance();
        if (agent != null && agent->IsAgentActive())
            agent->UpdateFishList(); // redraws the list's icons without the dots

        Plugin.Log.Information($"Marked {cleared} fish as seen in the Fish Guide.");
        return cleared;
    }
}
