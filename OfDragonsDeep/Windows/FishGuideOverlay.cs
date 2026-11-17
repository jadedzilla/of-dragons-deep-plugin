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

    public override void Draw()
    {
        var export = plugin.Export;
        var showStatus = export.IsBusy || DateTime.UtcNow - export.StatusAt < TimeSpan.FromSeconds(4);
        if (showStatus && export.Status.Length > 0)
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(export.Status);
            ImGui.SameLine();
        }

        if (export.Data == null || export.IsBusy)
        {
            ImGui.BeginDisabled(export.IsBusy);
            if (ImGui.Button("Export data"))
                export.Prepare();
            ImGui.EndDisabled();
        }
        else
        {
            if (ImGui.Button("Export"))
                ImGui.OpenPopup("OfDragonsDeepExportMenu");
        }

        if (ImGui.BeginPopup("OfDragonsDeepExportMenu"))
        {
            if (ImGui.Selectable("Open in browser"))
                export.OpenInBrowser();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Open the tracker with your fish and achievements ready to import.");

            if (ImGui.Selectable("Copy tracker link"))
                export.CopyLink();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"A link to {Exporter.TrackerUrl} that imports your fish and achievements.");

            if (ImGui.Selectable("Copy JSON"))
                export.CopyJson();

            ImGui.Separator();
            if (ImGui.Selectable("Export again"))
                export.Prepare();
            if (ImGui.IsItemHovered() && export.Data != null)
                ImGui.SetTooltip($"Collected at {export.Data.CollectedAt:t}. Collect again to pick up anything new.");

            ImGui.EndPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("..."))
            mainWindow.IsOpen = true;

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Open Of Dragons Deep (options)");
    }
}
