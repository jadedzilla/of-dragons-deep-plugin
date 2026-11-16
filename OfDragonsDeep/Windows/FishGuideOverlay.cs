using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace OfDragonsDeep.Windows;

/// <summary>
/// A small button bar pinned to the top-right corner of the in-game Fish Guide (fishing / spearfishing log).
/// </summary>
public sealed class FishGuideOverlay : Window
{
    private const string AddonName = "FishGuide2";

    private readonly Plugin plugin;
    private readonly MainWindow mainWindow;

    private Vector2 anchor;
    private string feedback = string.Empty;
    private DateTime feedbackUntil;

    public FishGuideOverlay(Plugin plugin, MainWindow mainWindow)
        : base("###OfDragonsDeepFishGuideOverlay",
               ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove |
               ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav |
               ImGuiWindowFlags.NoDocking)
    {
        this.plugin = plugin;
        this.mainWindow = mainWindow;
        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;
    }

    public override unsafe bool DrawConditions()
    {
        if (!plugin.Configuration.ShowFishGuideButton)
            return false;

        var unit = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(AddonName).Address;
        if (unit == null || !unit->IsVisible || unit->RootNode == null)
            return false;

        var width = unit->RootNode->Width * unit->Scale;
        // Top-right corner of the Fish Guide, in ImGui screen coordinates.
        anchor = ImGuiHelpers.MainViewport.Pos + new Vector2(unit->X + width, unit->Y);
        return true;
    }

    public override void PreDraw()
    {
        // Pivot (1,1): our window's bottom-right sits on the Fish Guide's top-right corner, just above it.
        ImGui.SetNextWindowPos(anchor, ImGuiCond.Always, new Vector2(1, 1));
    }

    private void Copy(bool link)
    {
        mainWindow.Refresh();
        var ok = link ? mainWindow.CopyLinkToClipboard(out var count) : mainWindow.CopyToClipboard(out count);
        feedback = !ok ? "Fish data not ready"
            : link ? $"Link copied ({count} fish)!"
            : $"Copied {count} fish!";
        feedbackUntil = DateTime.UtcNow.AddSeconds(3);
    }

    public override void Draw()
    {
        if (DateTime.UtcNow < feedbackUntil)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(feedback);
            ImGui.SameLine();
        }

        if (ImGui.Button("Export caught fish"))
            ImGui.OpenPopup("OfDragonsDeepExportMenu");

        if (ImGui.BeginPopup("OfDragonsDeepExportMenu"))
        {
            if (ImGui.Selectable("Copy tracker link"))
                Copy(link: true);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"A link to {Exporter.TrackerUrl} that imports your caught fish when opened.");

            if (ImGui.Selectable($"Copy as {Exporter.FormatLabel(plugin.Configuration.Format)}"))
                Copy(link: false);

            ImGui.EndPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("..."))
            mainWindow.IsOpen = true;

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Open Of Dragons Deep (format and options)");
    }
}
