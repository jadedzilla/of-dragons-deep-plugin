using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace OfDragonsDeep.Windows;

/// <summary>
/// Opened by Export (Fish Guide button or main window): a progress bar while the data is collected, then
/// Copy link / Open link / Copy JSON.
/// </summary>
public sealed class ExportWindow : Window
{
    private const float Width = 330;

    private readonly Plugin plugin;

    public ExportWindow(Plugin plugin)
        : base("Export###OfDragonsDeepExport",
               ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse |
               ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking)
    {
        this.plugin = plugin;
    }

    /// <summary>Starts a fresh collection and shows this window.</summary>
    public void StartExport()
    {
        plugin.Export.Prepare();
        IsOpen = true;
    }

    public override void PreDraw()
    {
        var vp = ImGuiHelpers.MainViewport;
        ImGui.SetNextWindowPos(vp.Pos + vp.Size / 2, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14, 12));
    }

    public override void PostDraw() => ImGui.PopStyleVar();

    public override void Draw()
    {
        var export = plugin.Export;

        if (!Plugin.ClientState.IsLoggedIn)
        {
            ImGui.TextUnformatted("Log in to a character to export.");
            return;
        }

        if (export.IsBusy)
        {
            var (fraction, label) = export.Progress;
            ImGui.TextUnformatted("Collecting your fish and achievements...");
            ImGui.ProgressBar(fraction, new Vector2(Width, 0), label);
            return;
        }

        if (export.Data == null)
        {
            ImGui.TextUnformatted(export.Status.Length > 0 ? export.Status : "Nothing collected yet.");
            if (ImGui.Button("Try again"))
                export.Prepare();
            return;
        }

        ImGui.TextUnformatted($"Ready: {export.Data.Summary}.");
        ImGui.Spacing();

        var buttonWidth = (Width - 2 * ImGui.GetStyle().ItemSpacing.X) / 3;
        if (ImGui.Button("Copy link", new Vector2(buttonWidth, 0)))
            export.CopyLink();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"A link to {Exporter.TrackerUrl} that imports your fish and achievements.");

        ImGui.SameLine();
        if (ImGui.Button("Open link", new Vector2(buttonWidth, 0)))
            export.OpenInBrowser();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Open the tracker in your browser with everything ready to import.");

        ImGui.SameLine();
        if (ImGui.Button("Copy JSON", new Vector2(buttonWidth, 0)))
            export.CopyJson();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Your caught fish and achievements as one JSON string.");

        // Feedback after a button, e.g. "Copied a tracker link with ..."
        if (export.Status.Length > 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled(export.Status);
        }
    }
}
