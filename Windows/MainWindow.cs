using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ESTClock;

namespace ESTClock.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin)
        : base("EST CLOCK",
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoDecoration)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(50, 30),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (plugin.Configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
            Flags &= ~ImGuiWindowFlags.NoResize;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
            Flags |= ImGuiWindowFlags.NoResize;
        }
    }

    public override void Draw()
    {
        TimeZoneInfo estZone;

        try
        {
            estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch
        {
            estZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }

        var estTime = TimeZoneInfo.ConvertTime(DateTime.UtcNow, estZone);

        var text = estTime.ToString("hh:mm") + estTime.ToString("tt").ToLower() + " EST";

        var scale = plugin.Configuration.ClockTextScale;

        var baseSize = ImGui.CalcTextSize(text);
        var textSize = baseSize * scale;

        ImGui.SetNextWindowSize(textSize + new Vector2(10, 10), ImGuiCond.Once);

        var bgOpacity = plugin.Configuration.ClockTransparent ? 0.0f : 0.5f;

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, bgOpacity));
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 1.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0, 0));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);

        var windowSize = ImGui.GetWindowSize();
        var windowPos = ImGui.GetWindowPos();

        var basePos = new Vector2(
            (windowSize.X - textSize.X) * 0.5f,
            (windowSize.Y - textSize.Y) * 0.5f
        );

        basePos = new Vector2(
            (float)Math.Floor(basePos.X),
            (float)Math.Floor(basePos.Y)
        );

        var textColor = plugin.Configuration.ClockTextColor;
        var shadowColor = plugin.Configuration.ClockShadowColor;

        ImGui.SetWindowFontScale(scale);

        var textScreenPos = windowPos + basePos;

        var rectMin = textScreenPos;
        var rectMax = textScreenPos + textSize;

        var bgColor = plugin.Configuration.ClockBackgroundColor;

        var rectColor = ImGui.ColorConvertFloat4ToU32(
            new Vector4(bgColor.X, bgColor.Y, bgColor.Z, plugin.Configuration.ClockBackgroundOpacity)
        );

        // 🔥 BORDAS ARREDONDADAS (raio ajustável)
        float rounding = 6.0f;

        ImGui.GetWindowDrawList().AddRectFilled(rectMin, rectMax, rectColor, rounding);

        var glowOffsets = new[]
        {
            new Vector2(1f, 0f),
            new Vector2(-1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(0f, -1f),
        };

        foreach (var offset in glowOffsets)
        {
            ImGui.SetCursorPos(basePos + offset);
            ImGui.TextColored(new Vector4(textColor.X, textColor.Y, textColor.Z, 0.15f), text);
        }

        var shadowOffsets = new[]
        {
            new Vector2(1.5f, 0f),
            new Vector2(-1.5f, 0f),
            new Vector2(0f, 1.5f),
            new Vector2(0f, -1.5f),
        };

        foreach (var offset in shadowOffsets)
        {
            ImGui.SetCursorPos(basePos + offset);
            ImGui.TextColored(shadowColor, text);
        }

        ImGui.SetCursorPos(basePos);
        ImGui.TextColored(textColor, text);

        ImGui.SetWindowFontScale(1.0f);

        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor();
    }
}