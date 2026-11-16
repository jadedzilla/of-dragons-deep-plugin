using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace OfDragonsDeep.Windows;

public sealed class MainWindow : Window
{
    private readonly Plugin plugin;

    private List<FishEntry>? fish;
    private string exportText = string.Empty;
    private string status = string.Empty;

    public MainWindow(Plugin plugin)
        : base("Of Dragons Deep###OfDragonsDeepMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 180),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void OnOpen() => Refresh();

    /// <summary>Re-reads the fishing log from game memory and rebuilds the export text.</summary>
    public void Refresh()
    {
        var cfg = plugin.Configuration;

        if (!Plugin.ClientState.IsLoggedIn)
        {
            fish = null;
            exportText = string.Empty;
            status = "Log in to a character to read your fishing log.";
            return;
        }

        try
        {
            fish = FishCollector.Collect(cfg.IncludeSpearfishing);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Failed to read fish log");
            fish = null;
        }

        if (fish == null)
        {
            exportText = string.Empty;
            status = "Couldn't read fish data yet. Try again in a moment.";
            return;
        }

        exportText = Exporter.Export(fish, cfg.Format, cfg.IncludeUncaught);
        status = BuildSummary(fish);
    }

    public bool CopyToClipboard(out int caughtCount)
    {
        caughtCount = fish?.Count(f => f.Caught) ?? 0;
        if (fish == null || exportText.Length == 0)
            return false;

        ImGui.SetClipboardText(exportText);
        return true;
    }

    /// <summary>Copies a link that opens the Of Dragons Deep tracker and imports the caught fish.</summary>
    public bool CopyLinkToClipboard(out int caughtCount)
    {
        caughtCount = fish?.Count(f => f.Caught) ?? 0;
        if (fish == null)
            return false;

        ImGui.SetClipboardText(Exporter.TrackerLink(fish));
        return true;
    }

    private static string BuildSummary(List<FishEntry> fish)
    {
        var line = fish.Where(f => f.Kind == FishKind.Fishing).ToList();
        var spear = fish.Where(f => f.Kind == FishKind.Spearfishing).ToList();
        var text = $"Fishing log: {line.Count(f => f.Caught)}/{line.Count}";
        if (spear.Count > 0)
            text += $"   Spearfishing log: {spear.Count(f => f.Caught)}/{spear.Count}";
        return text;
    }

    public override void Draw()
    {
        var cfg = plugin.Configuration;

        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo("Format", Exporter.FormatLabel(cfg.Format)))
        {
            foreach (var f in Enum.GetValues<ExportFormat>())
            {
                if (ImGui.Selectable(Exporter.FormatLabel(f), f == cfg.Format))
                {
                    cfg.Format = f;
                    cfg.Save();
                    Refresh();
                }
            }
            ImGui.EndCombo();
        }

        if (ImGui.BeginTabBar("##tabs"))
        {
            if (ImGui.BeginTabItem("Fish"))
            {
                DrawFishTab(cfg);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Achievements"))
            {
                DrawAchievementsTab(cfg);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawFishTab(Configuration cfg)
    {
        var changed = false;

        var includeSpear = cfg.IncludeSpearfishing;
        if (ImGui.Checkbox("Include spearfishing", ref includeSpear))
        {
            cfg.IncludeSpearfishing = includeSpear;
            changed = true;
        }

        ImGui.SameLine();
        var includeUncaught = cfg.IncludeUncaught;
        if (ImGui.Checkbox("Include uncaught (JSON/CSV)", ref includeUncaught))
        {
            cfg.IncludeUncaught = includeUncaught;
            changed = true;
        }

        var showGuideButton = cfg.ShowFishGuideButton;
        if (ImGui.Checkbox("Show export button on the Fish Guide", ref showGuideButton))
        {
            cfg.ShowFishGuideButton = showGuideButton;
            changed = true;
        }

        if (changed)
        {
            cfg.Save();
            Refresh();
        }

        if (ImGui.Button("Refresh"))
            Refresh();

        ImGui.SameLine();
        if (ImGui.Button("Copy to clipboard") && CopyToClipboard(out var count))
            status = $"Copied {count} caught fish to the clipboard.  ({BuildSummary(fish!)})";

        ImGui.SameLine();
        if (ImGui.Button("Copy tracker link") && CopyLinkToClipboard(out var linkCount))
            status = $"Copied a link that imports {linkCount} caught fish into Of Dragons Deep. Paste it into your browser.";
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Exporter.TrackerUrl);

        ImGui.TextWrapped(status);
    }

    private int achievementRevision = -1;
    private ExportFormat achievementFormat;
    private string achievementText = string.Empty;
    private string achievementStatus = string.Empty;

    private void DrawAchievementsTab(Configuration cfg)
    {
        var tracker = plugin.Achievements;

        if (!Plugin.ClientState.IsLoggedIn)
        {
            ImGui.TextWrapped("Log in to a character to read your achievements.");
            return;
        }

        var includeProgress = cfg.IncludeAchievementProgress;
        if (ImGui.Checkbox("Include progress for unfinished achievements", ref includeProgress))
        {
            cfg.IncludeAchievementProgress = includeProgress;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Progress (e.g. 312/500) is asked from the server one achievement at a time, so loading takes a little longer.");

        ImGui.BeginDisabled(tracker.IsBusy);
        if (ImGui.Button(tracker.Entries.Count == 0 ? "Load achievements" : "Reload achievements"))
        {
            achievementStatus = string.Empty;
            tracker.Start(cfg.IncludeAchievementProgress);
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(tracker.Entries.Count == 0 || tracker.IsBusy);
        if (ImGui.Button("Copy to clipboard##achievements"))
        {
            ImGui.SetClipboardText(achievementText);
            achievementStatus = "Copied to the clipboard.";
        }
        ImGui.EndDisabled();

        var entries = tracker.Entries;
        if (tracker.IsBusy)
            ImGui.TextUnformatted(entries.Count == 0
                ? "Loading achievements..."
                : $"Getting progress from the server... {tracker.ProgressRemaining} left");
        else if (tracker.Error != null)
            ImGui.TextWrapped(tracker.Error);
        else if (entries.Count == 0)
            ImGui.TextWrapped("Exports the Fisher achievements (including ocean fishing). Press Load achievements to start.");
        else
        {
            var done = entries.Where(a => a.Complete).ToList();
            ImGui.TextUnformatted(
                $"Fisher achievements: {done.Count}/{entries.Count} complete, " +
                $"{done.Sum(a => (int)a.Points)}/{entries.Sum(a => (int)a.Points)} points");
        }

        if (achievementStatus.Length > 0)
            ImGui.TextUnformatted(achievementStatus);

        if (achievementRevision != tracker.Revision || achievementFormat != cfg.Format)
        {
            achievementRevision = tracker.Revision;
            achievementFormat = cfg.Format;
            achievementText = entries.Count == 0 ? string.Empty : Exporter.ExportAchievements(entries, cfg.Format);
        }

    }
}
