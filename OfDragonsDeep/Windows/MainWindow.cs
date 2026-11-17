using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace OfDragonsDeep.Windows;

public sealed class MainWindow : Window
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("Of Dragons Deep###OfDragonsDeepMain", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize)
    {
        this.plugin = plugin;
    }

    // Window sized to its content, with a little more breathing room than the default padding.
    public override void PreDraw() => ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14, 12));

    public override void PostDraw() => ImGui.PopStyleVar();

    public override void OnOpen() => plugin.Export.RefreshFish();

    public override void Draw()
    {
        var cfg = plugin.Configuration;
        var export = plugin.Export;

        if (!Plugin.ClientState.IsLoggedIn)
        {
            ImGui.TextUnformatted("Log in to a character to export your fish and achievements.");
            return;
        }

        // Summary
        var fish = export.Fish;
        if (fish == null)
        {
            ImGui.TextUnformatted("Fishing log: not read yet");
        }
        else
        {
            var line = fish.Where(f => f.Kind == FishKind.Fishing).ToList();
            var spear = fish.Where(f => f.Kind == FishKind.Spearfishing).ToList();
            var text = $"Fishing log: {line.Count(f => f.Caught)}/{line.Count}";
            if (spear.Count > 0)
                text += $"   Spearfishing log: {spear.Count(f => f.Caught)}/{spear.Count}";
            ImGui.TextUnformatted(text);
        }

        var achievements = plugin.Achievements.Entries;
        ImGui.TextUnformatted(achievements.Count == 0
            ? "Fishing achievements: loaded when you export"
            : $"Fishing achievements: {achievements.Count(a => a.Complete)}/{achievements.Count} complete, " +
              $"{achievements.Where(a => a.Complete).Sum(a => (int)a.Points)}/{achievements.Sum(a => (int)a.Points)} points");

        ImGui.Spacing();

        // Export
        ImGui.BeginDisabled(export.IsBusy);
        if (ImGui.Button("Copy tracker link"))
            export.Copy(ExportKind.Link);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip($"A link to {Exporter.TrackerUrl} that imports your fish and achievements.");

        ImGui.SameLine();
        if (ImGui.Button("Open in browser"))
            export.Copy(ExportKind.Open);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Open the tracker with your fish and achievements ready to import.");

        ImGui.SameLine();
        if (ImGui.Button("Copy JSON"))
            export.Copy(ExportKind.Json);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Your caught fish and achievements as one JSON string.");
        ImGui.EndDisabled();


        // Auto-resizing window: plain text (wrapped text would have no width to wrap to).
        if (export.Status.Length > 0)
            ImGui.TextUnformatted(export.Status);

        ImGui.Separator();

        // Options
        var includeSpear = cfg.IncludeSpearfishing;
        if (ImGui.Checkbox("Include spearfishing", ref includeSpear))
        {
            cfg.IncludeSpearfishing = includeSpear;
            cfg.Save();
            export.RefreshFish();
        }

        var includeProgress = cfg.IncludeAchievementProgress;
        if (ImGui.Checkbox("Include achievement progress", ref includeProgress))
        {
            cfg.IncludeAchievementProgress = includeProgress;
            cfg.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Progress like 312/500 for unfinished achievements. It's asked from the server one\n" +
                             "achievement at a time, so the first export takes a few seconds (reused for 10 minutes).");

        var showGuideButton = cfg.ShowFishGuideButton;
        if (ImGui.Checkbox("Show export button on the Fish Guide", ref showGuideButton))
        {
            cfg.ShowFishGuideButton = showGuideButton;
            cfg.Save();
        }
    }
}
