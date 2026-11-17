using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;

namespace OfDragonsDeep;

public enum ExportKind
{
    /// <summary>Copy the tracker link.</summary>
    Link,
    /// <summary>Copy the JSON string.</summary>
    Json,
    /// <summary>Open the tracker link in the browser (nothing is copied).</summary>
    Open,
}

/// <summary>
/// Runs an export: refreshes achievements (completion is instant; progress is asked from the server and reused for
/// 10 minutes), then copies the link or JSON on the next UI frame once everything is in.
/// </summary>
public sealed class ExportService : IDisposable
{
    private readonly Plugin plugin;
    private ExportKind? pending;

    public string Status { get; private set; } = string.Empty;
    public DateTime StatusAt { get; private set; }
    public bool IsBusy => pending != null;

    /// <summary>Fish read during the last export or <see cref="RefreshFish"/>; null if unavailable.</summary>
    public List<FishEntry>? Fish { get; private set; }

    public ExportService(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.PluginInterface.UiBuilder.Draw += OnDraw;
    }

    public void Dispose() => Plugin.PluginInterface.UiBuilder.Draw -= OnDraw;

    /// <summary>Starts an export: copies the link or JSON, or opens the link, once everything is collected.</summary>
    public void Copy(ExportKind kind)
    {
        if (IsBusy)
            return;
        if (!Plugin.ClientState.IsLoggedIn)
        {
            SetStatus("Log in to a character first.");
            return;
        }

        pending = kind;
        plugin.Achievements.Start(plugin.Configuration.IncludeAchievementProgress);
        SetStatus("Collecting your fish and achievements...");
    }

    public void RefreshFish()
    {
        try
        {
            Fish = Plugin.ClientState.IsLoggedIn ? FishCollector.Collect(plugin.Configuration.IncludeSpearfishing) : null;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to read fish log");
            Fish = null;
        }
    }

    private void OnDraw()
    {
        if (pending is not { } kind)
            return;

        var tracker = plugin.Achievements;
        if (tracker.IsBusy)
        {
            Status = tracker.Entries.Count == 0
                ? "Loading achievements..."
                : $"Getting achievement progress from the server... {tracker.ProgressRemaining} left";
            return;
        }

        pending = null;
        RefreshFish();
        if (Fish == null)
        {
            SetStatus("Couldn't read your fishing log yet. Try again in a moment.");
            return;
        }

        var achievements = tracker.Error == null && tracker.Entries.Count > 0 ? tracker.Entries : null;
        var fishCount = Fish.Count(f => f.Caught);
        var contents = achievements != null
            ? $"{fishCount} fish and {achievements.Count(a => a.Complete)}/{achievements.Count} achievements"
            : $"{fishCount} fish";
        var note = achievements != null ? "" : " Achievements couldn't be loaded, so they're not included.";

        switch (kind)
        {
            case ExportKind.Open:
                Util.OpenLink(Exporter.TrackerLink(Fish, achievements));
                SetStatus($"Opened the tracker with {contents}.{note}");
                break;
            case ExportKind.Link:
                ImGui.SetClipboardText(Exporter.TrackerLink(Fish, achievements));
                SetStatus($"Copied a tracker link with {contents}.{note}");
                break;
            default:
                ImGui.SetClipboardText(Exporter.ToJson(Fish, achievements));
                SetStatus($"Copied JSON with {contents}.{note}");
                break;
        }
    }

    private void SetStatus(string text)
    {
        Status = text;
        StatusAt = DateTime.UtcNow;
    }
}
