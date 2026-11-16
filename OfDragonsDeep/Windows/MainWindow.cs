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
            MinimumSize = new Vector2(420, 320),
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
        var changed = false;

        ImGui.SetNextItemWidth(220);
        if (ImGui.BeginCombo("Format", Exporter.FormatLabel(cfg.Format)))
        {
            foreach (var f in Enum.GetValues<ExportFormat>())
            {
                if (ImGui.Selectable(Exporter.FormatLabel(f), f == cfg.Format))
                {
                    cfg.Format = f;
                    changed = true;
                }
            }
            ImGui.EndCombo();
        }

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

        ImGui.TextWrapped(status);
        ImGui.Separator();

        // Read-only text box so the list can also be selected/copied by hand.
        var text = exportText;
        ImGui.InputTextMultiline(
            "##export",
            ref text,
            Math.Max(text.Length + 1, 1024),
            new Vector2(-1, -1),
            ImGuiInputTextFlags.ReadOnly);
    }
}
