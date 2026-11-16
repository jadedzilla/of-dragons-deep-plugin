using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using OfDragonsDeep.Windows;

namespace OfDragonsDeep;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/dragonsdeep";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; }

    private readonly WindowSystem windowSystem = new("OfDragonsDeep");
    private readonly MainWindow mainWindow;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        mainWindow = new MainWindow(this);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(new FishGuideOverlay(this, mainWindow));

        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Of Dragons Deep. \"/dragonsdeep copy\" copies your caught fish straight to the clipboard.",
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        windowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(Command);
    }

    private void OnCommand(string command, string args)
    {
        if (args.Trim().Equals("copy", System.StringComparison.OrdinalIgnoreCase))
        {
            mainWindow.Refresh();
            if (mainWindow.CopyToClipboard(out var count))
                ChatGui.Print($"[Of Dragons Deep] Copied {count} caught fish to the clipboard.");
            else
                ChatGui.PrintError("[Of Dragons Deep] Fish data isn't available yet. Make sure you're logged in.");
            return;
        }

        mainWindow.Toggle();
    }
}
