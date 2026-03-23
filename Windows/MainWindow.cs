using System;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface.Windowing;

namespace ESTClock.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin) : base("EST CLOCK", 
        (Dalamud.Bindings.ImGui.ImGuiWindowFlags)(ImGuiWindowFlags.NoTitleBar | 
                                                  ImGuiWindowFlags.NoScrollbar | 
                                                  ImGuiWindowFlags.NoDecoration | 
                                                  ImGuiWindowFlags.AlwaysAutoResize))
    {
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var config = plugin.Configuration;

        // Gerencia se a janela é clicável ou não
        if (!config.IsConfigWindowMovable)
            Flags |= (Dalamud.Bindings.ImGui.ImGuiWindowFlags)ImGuiWindowFlags.NoInputs;
        else
            Flags &= ~(Dalamud.Bindings.ImGui.ImGuiWindowFlags)ImGuiWindowFlags.NoInputs;

        DateTime estTime;
        try {
            estTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Eastern Standard Time");
        } catch {
            estTime = DateTime.UtcNow.AddHours(-5);
        }

        var text = $"{estTime:hh:mm tt} EST";

        // Estilização Segura do Fundo
        var bgColor = new Vector4(config.ClockBackgroundColor.X, config.ClockBackgroundColor.Y, config.ClockBackgroundColor.Z, config.ClockBackgroundOpacity);
        
        ImGui.PushStyleColor(ImGuiCol.WindowBg, bgColor);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 5));

        ImGui.SetWindowFontScale(config.ClockTextScale);

        // Desenho do Texto com Sombra Fake (mais estável que DrawList)
        var pos = ImGui.GetCursorPos();
        
        // Sombra
        ImGui.SetCursorPos(pos + new Vector2(2, 2));
        ImGui.TextColored(config.ClockShadowColor, text);
        
        // Texto Principal
        ImGui.SetCursorPos(pos);
        ImGui.TextColored(config.ClockTextColor, text);

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();
    }
}