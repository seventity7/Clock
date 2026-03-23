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
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;

    public Configuration Configuration { get; private set; }
    public readonly WindowSystem WindowSystem = new("EST Clock");

    private ConfigWindow ConfigWindow { get; set; }
    private MainWindow MainWindow { get; set; }

    private bool openedOnce = false;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler("/est", new CommandInfo((_, _) => MainWindow.Toggle()) { HelpMessage = "Mostrar/Ocultar Relógio" });
        CommandManager.AddHandler("/estsettings", new CommandInfo((_, _) => ConfigWindow.Toggle()) { HelpMessage = "Configurações" });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += () => ConfigWindow.Toggle();
    }

    private void DrawUI()
    {
        // Corrigido para evitar a warning CS0618 e prevenir crash
        if (ClientState == null || !ClientState.IsLoggedIn) return;

        if (!openedOnce && Configuration.AutoStart)
        {
            MainWindow.IsOpen = true;
            openedOnce = true;
        }
        WindowSystem.Draw();
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler("/est");
        CommandManager.RemoveHandler("/estsettings");
        PluginInterface.UiBuilder.Draw -= DrawUI;
    }
}