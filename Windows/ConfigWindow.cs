using System;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface.Windowing;

namespace ESTClock.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("EST Clock - Config###ConfigWindow")
    {
        this.configuration = plugin.Configuration;
        Size = new Vector2(350, 420);
        SizeCondition = (Dalamud.Bindings.ImGui.ImGuiCond)ImGuiCond.Always;
    }

    public void Dispose() { }

    public override void Draw()
    {
        bool saveNeeded = false;

        var auto = configuration.AutoStart;
        if (ImGui.Checkbox("Auto-Start", ref auto)) { configuration.AutoStart = auto; saveNeeded = true; }
        
        var move = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Mode", ref move)) { configuration.IsConfigWindowMovable = move; saveNeeded = true; }

        ImGui.Separator();
        var scale = configuration.ClockTextScale;
        if (ImGui.SliderFloat("Scale", ref scale, 0.5f, 5.0f)) { configuration.ClockTextScale = scale; saveNeeded = true; }

        var txtCol = configuration.ClockTextColor;
        if (ImGui.ColorEdit4("Text Color", ref txtCol)) { configuration.ClockTextColor = txtCol; saveNeeded = true; }

        var shdCol = configuration.ClockShadowColor;
        if (ImGui.ColorEdit4("Shadow Color", ref shdCol)) { configuration.ClockShadowColor = shdCol; saveNeeded = true; }

        var bgCol = configuration.ClockBackgroundColor;
        if (ImGui.ColorEdit4("Box Color", ref bgCol)) { configuration.ClockBackgroundColor = bgCol; saveNeeded = true; }

        var bgOp = configuration.ClockBackgroundOpacity;
        if (ImGui.SliderFloat("Box Opacity", ref bgOp, 0.0f, 1.0f)) { configuration.ClockBackgroundOpacity = bgOp; saveNeeded = true; }

        if (saveNeeded) configuration.Save();
    }
}