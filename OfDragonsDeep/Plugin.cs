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
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; }
    public AchievementTracker Achievements { get; }
    public ExportService Export { get; }

    private readonly WindowSystem windowSystem = new("OfDragonsDeep");
    private readonly MainWindow mainWindow;
    public ExportWindow ExportWindow { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Achievements = new AchievementTracker();
        Export = new ExportService(this);
        ExportWindow = new ExportWindow(this);
        mainWindow = new MainWindow(this);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(ExportWindow);
        windowSystem.AddWindow(new FishGuideOverlay(this, mainWindow));

        CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Of Dragons Deep.",
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += mainWindow.Toggle;
        // The options live in the main window.
        PluginInterface.UiBuilder.OpenConfigUi += mainWindow.Toggle;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= mainWindow.Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= mainWindow.Toggle;
        windowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(Command);
        Export.Dispose();
        Achievements.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        mainWindow.Toggle();
    }
}
