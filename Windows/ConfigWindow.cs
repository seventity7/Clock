using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ESTClock;
using ESTClock.Windows;

namespace ESTClock.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin)
        : base("EST Clock - Config###ConfigWindow")
    {
        Flags = ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoCollapse |
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(350, 200);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, 1.0f));
    }

    public override void Draw()
    {
        var stick = !configuration.IsConfigWindowMovable;

        if (ImGui.Checkbox("Stick clock", ref stick))
        {
            configuration.IsConfigWindowMovable = !stick;
            configuration.Save();
        }

        ImGui.Separator();

        var scale = configuration.ClockTextScale;
        if (ImGui.SliderFloat("Text Size", ref scale, 0.5f, 5.0f, "%.2f"))
        {
            configuration.ClockTextScale = scale;
            configuration.Save();
        }

        var opacity = configuration.ClockBackgroundOpacity;
        if (ImGui.SliderFloat("Background Opacity", ref opacity, 0.0f, 1.0f, "%.2f"))
        {
            configuration.ClockBackgroundOpacity = opacity;
            configuration.Save();
        }

        // 🔥 NOVO: cor do background
        var bgColor = configuration.ClockBackgroundColor;
        if (ImGui.ColorEdit4("Background Color", ref bgColor))
        {
            configuration.ClockBackgroundColor = bgColor;
            configuration.Save();
        }

        var textColor = configuration.ClockTextColor;
        if (ImGui.ColorEdit4("Text color", ref textColor))
        {
            configuration.ClockTextColor = textColor;
            configuration.Save();
        }

        var shadowColor = configuration.ClockShadowColor;
        if (ImGui.ColorEdit4("Shadow color", ref shadowColor))
        {
            configuration.ClockShadowColor = shadowColor;
            configuration.Save();
        }
        var autoStart = configuration.AutoStart;

        if (ImGui.Checkbox("Auto-Start", ref autoStart))
        {
            configuration.AutoStart = autoStart;
            configuration.Save();
        }

            ImGui.SameLine();

            ImGui.TextColored(
            autoStart ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
        autoStart ? "ON" : "OFF"
        );
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
    }
}