using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;

namespace OfDragonsDeep;

public enum ExportKind
{
    Link,
    Json,
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

    /// <summary>The last tracker link copied, so it can be opened in the browser.</summary>
    public string? LastLink { get; private set; }

    /// <summary>Fish read during the last export or <see cref="RefreshFish"/>; null if unavailable.</summary>
    public List<FishEntry>? Fish { get; private set; }

    public ExportService(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.PluginInterface.UiBuilder.Draw += OnDraw;
    }

    public void Dispose() => Plugin.PluginInterface.UiBuilder.Draw -= OnDraw;

    public void Copy(ExportKind kind)
    {
        if (IsBusy)
            return;
        if (!Plugin.ClientState.IsLoggedIn)
        {
            LastLink = null;
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
        var text = kind == ExportKind.Link
            ? Exporter.TrackerLink(Fish, achievements)
            : Exporter.ToJson(Fish, achievements);
        ImGui.SetClipboardText(text);
        if (kind == ExportKind.Link)
            LastLink = text;

        var what = kind == ExportKind.Link ? "Copied a tracker link" : "Copied JSON";
        var fishCount = Fish.Count(f => f.Caught);
        SetStatus(achievements != null
            ? $"{what} with {fishCount} fish and {achievements.Count(a => a.Complete)}/{achievements.Count} achievements."
            : $"{what} with {fishCount} fish. Achievements couldn't be loaded, so they're not included.");
    }

    /// <summary>Opens the last copied tracker link in the default browser.</summary>
    public void OpenLastLink()
    {
        if (LastLink != null)
            Util.OpenLink(LastLink);
    }

    private void SetStatus(string text)
    {
        Status = text;
        StatusAt = DateTime.UtcNow;
    }
}
