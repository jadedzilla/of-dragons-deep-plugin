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
        if (module == null)
            return 0;
        return Math.Max(module->SortedUnseenFish.Count, module->UnseenFishCount);
    }

    /// <summary>
    /// Marks every fish as seen and returns how many were cleared. Must run on the framework (UI) thread.
    /// Entries are removed one at a time (like std::set::erase), so the set's head node stays in place:
    /// StdSet.Clear() frees the head too, which crashed the game when it next read the set (1.0.1).
    /// </summary>
    public static int MarkAllSeen()
    {
        var module = FishingModule.Instance();
        if (module == null)
            return 0;

        var cleared = Count();
        if (cleared == 0)
            return 0;

        // Copy first: removing while enumerating would walk freed nodes.
        var ids = module->SortedUnseenFish.ToArray();
        foreach (var id in ids)
            module->SortedUnseenFish.Remove(id); // frees just that node with the game's allocator

        foreach (ref var slot in module->UnseenFish)
            slot = 0;
        module->UnseenFishCount = 0;

        // Same as the game's own list changes (e.g. favourite mounts): flag the file and save it.
        module->IsSavePending = true;
        module->SaveFile(false);

        var agent = AgentFishGuide.Instance();
        if (agent != null && agent->IsAgentActive())
            agent->UpdateFishList(); // redraws the list's icons without the dots

        Plugin.Log.Information($"Marked {cleared} fish as seen in the Fish Guide (removed {ids.Length} from the set).");
        return cleared;
    }
}
