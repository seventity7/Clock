using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace ESTClock.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private const float MainPaddingX = 7.0f;
    private const float MainPaddingY = 3.0f;
    private const float BadgePaddingX = 4.0f;
    private const float BadgePaddingY = 1.0f;
    private const float BadgeGap = 7.0f;
    private const float MainRounding = 8.0f;
    private const float BadgeRounding = 4.0f;

    private const float BadgeVerticalOffset = 1.0f;
    private const float MinuteDigitGap = 0.3f;
    private const float SuffixHorizontalOffset = -0.3f;

    private const string ColonText = " : ";

    public MainWindow(Plugin plugin)
        : base("###ESTClockMainWindow")
    {
        this.plugin = plugin;

        Flags =
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse |
            ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoDecoration;

        RespectCloseHotkey = false;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(50, 20),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
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

        var config = plugin.Configuration;
        var mainScale = MathF.Max(0.5f, config.ClockTextScale);
        var badgeScale = MathF.Max(0.35f, mainScale * 0.45f);

        var badgeText = "EST";
        var badgeSize = config.ShowIcon
            ? CalculateScaledTextSize(badgeText, badgeScale)
            : Vector2.Zero;

        var layout = GetClockLayoutMetrics(mainScale);

        var iconWidth = config.ShowIcon ? (BadgePaddingX * 2.0f) + badgeSize.X : 0.0f;
        var gapWidth = config.ShowIcon ? BadgeGap : 0.0f;
        var contentHeight = config.ShowIcon
            ? MathF.Max(layout.TotalSize.Y, badgeSize.Y + (BadgePaddingY * 2.0f))
            : layout.TotalSize.Y;

        var totalSize = new Vector2(
            MainPaddingX + iconWidth + gapWidth + layout.TotalSize.X + MainPaddingX,
            MainPaddingY + contentHeight + MainPaddingY
        );

        ImGui.SetNextWindowSize(totalSize, ImGuiCond.Always);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
    }

    public override void Draw()
    {
        var config = plugin.Configuration;
        var mainScale = MathF.Max(0.5f, config.ClockTextScale);
        var badgeScale = MathF.Max(0.35f, mainScale * 0.45f);

        var badgeText = "EST";
        var parts = GetClockParts();
        var layout = GetClockLayoutMetrics(mainScale);

        var badgeTextSize = config.ShowIcon
            ? CalculateScaledTextSize(badgeText, badgeScale)
            : Vector2.Zero;

        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var drawList = ImGui.GetWindowDrawList();

        var panelMin = windowPos;
        var panelMax = windowPos + windowSize;

        var panelColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
            config.ClockBackgroundColor.X,
            config.ClockBackgroundColor.Y,
            config.ClockBackgroundColor.Z,
            config.ClockBackgroundOpacity));

        drawList.AddRectFilled(panelMin, panelMax, panelColor, MainRounding);

        if (config.ShowBorder)
        {
            var borderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
                config.BorderColor.X,
                config.BorderColor.Y,
                config.BorderColor.Z,
                config.BorderOpacity));
            drawList.AddRect(panelMin, panelMax, borderColor, MainRounding, ImDrawFlags.None, 1.0f);
        }

        float currentX = windowPos.X + MainPaddingX;

        if (config.ShowIcon)
        {
            var badgeHeight = badgeTextSize.Y + (BadgePaddingY * 2.0f);
            var badgeWidth = badgeTextSize.X + (BadgePaddingX * 2.0f);

            var contentHeight = MathF.Max(layout.TotalSize.Y, badgeHeight);
            var contentTop = windowPos.Y + MathF.Floor((windowSize.Y - contentHeight) * 0.5f);

            var badgeMin = new Vector2(
                currentX,
                contentTop + MathF.Floor((contentHeight - badgeHeight) * 0.5f) + BadgeVerticalOffset
            );

            var badgeMax = new Vector2(
                badgeMin.X + badgeWidth,
                badgeMin.Y + badgeHeight
            );

            var iconBg = config.IconBackgroundColor;
            var badgeFillColor = ImGui.ColorConvertFloat4ToU32(iconBg);

            drawList.AddRectFilled(badgeMin, badgeMax, badgeFillColor, BadgeRounding);

            if (config.ShowIconBorder)
            {
                var badgeBorderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
                    config.IconBorderColor.X,
                    config.IconBorderColor.Y,
                    config.IconBorderColor.Z,
                    config.IconBorderOpacity));
                drawList.AddRect(badgeMin, badgeMax, badgeBorderColor, BadgeRounding, ImDrawFlags.None, 1.0f);
            }

            var badgeTextPos = new Vector2(
                badgeMin.X + BadgePaddingX,
                badgeMin.Y + BadgePaddingY
            );

            DrawTextScaled(
                badgeText,
                badgeTextPos - windowPos,
                badgeScale,
                config.IconTextColor
            );

            currentX = badgeMax.X + BadgeGap;
        }

        var timePos = new Vector2(
            currentX,
            windowPos.Y + MathF.Floor((windowSize.Y - layout.TotalSize.Y) * 0.5f)
        );

        DrawClockStable(
            parts,
            timePos - windowPos,
            mainScale,
            config.ClockTextColor,
            config.ShowShadowText ? config.ClockShadowColor : new Vector4(0, 0, 0, 0)
        );
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(3);
    }

    private void DrawClockStable(ClockParts parts, Vector2 basePos, float scale, Vector4 color, Vector4 shadow)
    {
        var leftSize = CalculateScaledTextSize(parts.Left, scale);
        var colonSize = CalculateScaledTextSize(ColonText, scale);
        var minuteLeftSize = CalculateScaledTextSize(parts.MinuteLeft, scale);
        var minuteRightSize = CalculateScaledTextSize(parts.MinuteRight, scale);

        DrawOutlinedTextScaled(parts.Left, basePos, scale, color, shadow);

        var colonPos = new Vector2(basePos.X + leftSize.X, basePos.Y);

        var colonColor = parts.ShowColon ? color : new Vector4(color.X, color.Y, color.Z, 0f);
        var colonShadow = parts.ShowColon ? shadow : new Vector4(0, 0, 0, 0);

        DrawOutlinedTextScaled(ColonText, colonPos, scale, colonColor, colonShadow);

        float x = basePos.X + leftSize.X + colonSize.X;

        DrawOutlinedTextScaled(parts.MinuteLeft, new Vector2(x, basePos.Y), scale, color, shadow);

        float secondMinuteX = x + minuteLeftSize.X + (MinuteDigitGap * scale);

        DrawOutlinedTextScaled(parts.MinuteRight, new Vector2(secondMinuteX, basePos.Y), scale, color, shadow);

        float suffixX = secondMinuteX + minuteRightSize.X + (SuffixHorizontalOffset * scale);

        DrawOutlinedTextScaled(" " + parts.Suffix, new Vector2(suffixX, basePos.Y), scale, color, shadow);
    }

    private void DrawTextScaled(string text, Vector2 basePos, float scale, Vector4 textColor)
    {
        ImGui.SetWindowFontScale(scale);
        ImGui.SetCursorPos(basePos);
        ImGui.TextColored(textColor, text);
        ImGui.SetWindowFontScale(1.0f);
    }

    private void DrawOutlinedTextScaled(string text, Vector2 basePos, float scale, Vector4 textColor, Vector4 outlineColor)
    {
        ImGui.SetWindowFontScale(scale);

        if (outlineColor.W > 0.0f)
        {
            var offsets = new[]
            {
                new Vector2(-1f, 0f),
                new Vector2( 1f, 0f),
                new Vector2( 0f,-1f),
                new Vector2( 0f, 1f),
                new Vector2(-1f,-1f),
                new Vector2( 1f,-1f),
                new Vector2(-1f, 1f),
                new Vector2( 1f, 1f),
            };

            foreach (var offset in offsets)
            {
                ImGui.SetCursorPos(basePos + offset);
                ImGui.TextColored(outlineColor, text);
            }
        }

        ImGui.SetCursorPos(basePos);
        ImGui.TextColored(textColor, text);

        ImGui.SetWindowFontScale(1.0f);
    }

    private static Vector2 CalculateScaledTextSize(string text, float scale)
    {
        return ImGui.CalcTextSize(text) * scale;
    }

    private static ClockParts GetClockParts()
    {
        var est = TimeZoneInfo.ConvertTime(DateTime.UtcNow, GetEasternTimeZone());

        var minutes = est.ToString("mm");
        var suffix = est.ToString("tt").ToLower().Replace("am", "a.m.").Replace("pm", "p.m.");

        return new ClockParts
        {
            Left = est.ToString("%h"),
            MinuteLeft = minutes[0].ToString(),
            MinuteRight = minutes[1].ToString(),
            Suffix = suffix,
            ShowColon = (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond % 1800) < 1000
        };
    }

    private static ClockLayoutMetrics GetClockLayoutMetrics(float scale)
    {
        var parts = GetClockParts();

        var leftSize = CalculateScaledTextSize(parts.Left, scale);
        var colonSize = CalculateScaledTextSize(ColonText, scale);
        var minuteLeftSize = CalculateScaledTextSize(parts.MinuteLeft, scale);
        var minuteRightSize = CalculateScaledTextSize(parts.MinuteRight, scale);
        var suffixSize = CalculateScaledTextSize(" " + parts.Suffix, scale);

        float totalWidth =
            leftSize.X +
            colonSize.X +
            minuteLeftSize.X +
            (MinuteDigitGap * scale) +
            minuteRightSize.X +
            (SuffixHorizontalOffset * scale) +
            suffixSize.X;

        float totalHeight = MathF.Max(
            leftSize.Y,
            MathF.Max(colonSize.Y, MathF.Max(minuteLeftSize.Y, MathF.Max(minuteRightSize.Y, suffixSize.Y)))
        );

        return new ClockLayoutMetrics
        {
            TotalSize = new Vector2(totalWidth, totalHeight)
        };
    }

    private static TimeZoneInfo GetEasternTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
    }

    private sealed class ClockParts
    {
        public string Left = "";
        public string MinuteLeft = "";
        public string MinuteRight = "";
        public string Suffix = "";
        public bool ShowColon;
    }

    private sealed class ClockLayoutMetrics
    {
        public Vector2 TotalSize;
    }
}