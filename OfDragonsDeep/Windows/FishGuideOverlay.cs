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
        if (ImGui.Button("Export"))
            plugin.ExportWindow.StartExport();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Export your caught fish and fishing achievements to Of Dragons Deep.");

        ImGui.SameLine();
        if (ImGui.Button("..."))
            mainWindow.IsOpen = true;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Of Dragons Deep options");
    }
}
