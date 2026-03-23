using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ESTClock.Windows;

namespace ESTClock;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;

    private const string CommandName = "/est";
    private const string SettingsCommand = "/estsettings";

    public Configuration Configuration { get; private set; }

    public readonly WindowSystem WindowSystem = new("EST Clock");

    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private bool openedOnce = false;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Save();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open real time EST Clock"
        });

        CommandManager.AddHandler(SettingsCommand, new CommandInfo(OnSettingsCommand)
        {
            HelpMessage = "Open EST Clock settings"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
    }

    private void DrawUI()
    {
        // ✅ AUTO START
        if (!openedOnce && ClientState.IsLoggedIn && Configuration.AutoStart)
        {
            MainWindow.IsOpen = true;
            openedOnce = true;
        }

        WindowSystem.Draw();
    }

    public void Dispose()
    {
        Configuration.Save();

        PluginInterface.UiBuilder.Draw -= DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        CommandManager.RemoveHandler(SettingsCommand);
    }

    private void OnCommand(string command, string args) => MainWindow.Toggle();
    private void OnSettingsCommand(string command, string args) => ConfigWindow.Toggle();

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}