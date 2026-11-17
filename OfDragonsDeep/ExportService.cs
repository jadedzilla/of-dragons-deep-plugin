using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;

namespace OfDragonsDeep;

/// <summary>Everything collected by one Export data: the tracker link and the JSON, ready to use.</summary>
public sealed record ExportData(string Link, string Json, string Summary, DateTime CollectedAt);

/// <summary>
/// Export in two steps. <see cref="Prepare"/> collects the fish log and achievements (completion is instant;
/// progress is asked from the server and reused for 10 minutes). Once <see cref="Data"/> is set, opening or
/// copying the link and copying the JSON are instant.
/// </summary>
public sealed class ExportService : IDisposable
{
    private readonly Plugin plugin;
    private bool collecting;

    public string Status { get; private set; } = string.Empty;
    public DateTime StatusAt { get; private set; }
    public bool IsBusy => collecting;

    /// <summary>The collected export, or null until Export data has finished.</summary>
    public ExportData? Data { get; private set; }

    /// <summary>Fish read during the last export or <see cref="RefreshFish"/>; null if unavailable.</summary>
    public List<FishEntry>? Fish { get; private set; }

    public ExportService(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.PluginInterface.UiBuilder.Draw += OnDraw;
    }

    public void Dispose() => Plugin.PluginInterface.UiBuilder.Draw -= OnDraw;

    /// <summary>Starts collecting fish and achievements; <see cref="Data"/> is set when done.</summary>
    public void Prepare()
    {
        if (collecting)
            return;
        if (!Plugin.ClientState.IsLoggedIn)
        {
            SetStatus("Log in to a character first.");
            return;
        }

        collecting = true;
        Data = null;
        plugin.Achievements.Start(plugin.Configuration.IncludeAchievementProgress);
        SetStatus(string.Empty);
    }

    /// <summary>Collection progress for the progress bar: 0..1 and a label.</summary>
    public (float Fraction, string Label) Progress
    {
        get
        {
            var t = plugin.Achievements;
            if (!collecting)
                return (Data != null ? 1f : 0f, Data != null ? "Done" : "");
            if (t.LoadingCompletion)
                return (0.05f, "Loading achievements...");
            if (t.ProgressTotal > 0)
            {
                var done = t.ProgressTotal - t.ProgressRemaining;
                return (0.1f + 0.9f * done / t.ProgressTotal, $"Achievement progress {done}/{t.ProgressTotal}");
            }
            return (0.95f, "Reading your fishing log...");
        }
    }

    /// <summary>Forget the collected export (e.g. after an option changed), so Export data has to run again.</summary>
    public void Discard()
    {
        Data = null;
        Status = string.Empty;
    }

    public void OpenInBrowser()
    {
        if (Data == null)
            return;
        Util.OpenLink(Data.Link);
        SetStatus($"Opened the tracker with {Data.Summary}.");
    }

    public void CopyLink()
    {
        if (Data == null)
            return;
        ImGui.SetClipboardText(Data.Link);
        SetStatus($"Copied a tracker link with {Data.Summary}.");
    }

    public void CopyJson()
    {
        if (Data == null)
            return;
        ImGui.SetClipboardText(Data.Json);
        SetStatus($"Copied JSON with {Data.Summary}.");
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
        if (!Plugin.ClientState.IsLoggedIn)
        {
            // Another character's data must not be exported.
            if (Data != null || collecting)
            {
                Data = null;
                collecting = false;
                Status = string.Empty;
            }
            return;
        }

        if (!collecting)
            return;

        var tracker = plugin.Achievements;
        if (tracker.IsBusy)
            return;

        collecting = false;
        RefreshFish();
        if (Fish == null)
        {
            SetStatus("Couldn't read your fishing log yet. Try again in a moment.");
            return;
        }

        var achievements = tracker.Error == null && tracker.Entries.Count > 0 ? tracker.Entries : null;
        var fishCount = Fish.Count(f => f.Caught);
        var summary = achievements != null
            ? $"{fishCount} fish and {achievements.Count(a => a.Complete)}/{achievements.Count} achievements"
            : $"{fishCount} fish";

        Data = new ExportData(
            Exporter.TrackerLink(Fish, achievements),
            Exporter.ToJson(Fish, achievements),
            summary,
            DateTime.Now);

        SetStatus(achievements != null
            ? string.Empty
            : "Achievements couldn't be loaded, so only fish are included.");
    }

    private void SetStatus(string text)
    {
        Status = text;
        StatusAt = DateTime.UtcNow;
    }
}
