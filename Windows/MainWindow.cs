using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;

namespace Clock.Windows;

// Main clock window and rendering flow.
public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private const float MinuteDigitGap = 0.3f;
    private const float SuffixHorizontalOffset = -0.3f;
    private const string ColonText = " : ";
    private const string LocalColonText = ":";
    private const float LocalMinuteDigitGap = 0.15f;
    private const float LocalColonSideTighten = -2.0f;
    private const float TechHourTextOffsetX = 11.0f;
    private const float TechColonTextOffsetX = -7.0f;
    private const float TechBadgeOffsetX = 6.0f;
    private const float SeparateLocalPanelGap = 4.0f;
    private const float InvisibleWindowPadding = 16.0f;
    private const float MainPanelExtraSize = 5.5f;
    private const float LocalPanelExtraSize = 6.5f;
    private const float PanelRoundingReduction = 1.5f;
    private IDisposable?[]? windowStyleScopes;

    public MainWindow(Plugin plugin)
        : base("###ClockMainWindow")
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

        var profile = plugin.Configuration.GetActiveProfile();
        var mainPanelSize = GetMainPanelSize(profile);
        var totalContentSize = mainPanelSize;

        if (profile.ShowLocalTime)
        {
            var localLayout = GetLocalClockLayout(profile);
            var adjustedLocalPanelSize = GetAdjustedLocalPanelSize(profile, localLayout);

            if (profile.LayoutMode == ClockLayoutMode.Vertical)
            {
                totalContentSize = new Vector2(
                    mainPanelSize.X + SeparateLocalPanelGap + adjustedLocalPanelSize.X,
                    MathF.Max(mainPanelSize.Y, adjustedLocalPanelSize.Y)
                );
            }
            else if (profile.LocalTimePlacement == LocalTimePlacement.InsideMainPanel)
            {
                totalContentSize = new Vector2(
                    MathF.Max(mainPanelSize.X, adjustedLocalPanelSize.X),
                    adjustedLocalPanelSize.Y + mainPanelSize.Y
                );
            }
            else
            {
                totalContentSize = new Vector2(
                    MathF.Max(mainPanelSize.X, adjustedLocalPanelSize.X),
                    adjustedLocalPanelSize.Y + SeparateLocalPanelGap + mainPanelSize.Y
                );
            }
        }

        var totalSize = totalContentSize + new Vector2(InvisibleWindowPadding * 2.0f, InvisibleWindowPadding * 2.0f);
        ImGui.SetNextWindowSize(totalSize, ImGuiCond.Always);

        DisposeWindowStyleScopes();
        windowStyleScopes =
        [
            ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero),
            ImRaii.PushStyle(ImGuiStyleVar.WindowBorderSize, 0.0f),
            ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, 0.0f),
            ImRaii.PushColor(ImGuiCol.WindowBg, Vector4.Zero)
        ];
    }
    // Draw paths are intentionally explicit; tiny UI changes are easier to spot this way.

    public override void Draw()
    {
        var profile = plugin.Configuration.GetActiveProfile();
        var mainPanelSize = GetMainPanelSize(profile);

        var styleMetrics = GetStyleMetrics(profile.DisplayStyle);
        var mainScale = GetMainScale(profile, styleMetrics);
        var badgeScale = profile.DisplayStyle == ClockDisplayStyle.Countdown
            ? GetCountdownSuffixScale(mainScale)
            : MathF.Max(0.35f, mainScale * styleMetrics.BadgeScaleMultiplier);

        var parts = GetClockParts();
        var badgeText = GetMainBadgeText();
        var badgeTextSize = profile.ShowIcon
            ? CalculateMainBadgeTextSize(profile, badgeText, badgeScale)
            : Vector2.Zero;
        var layout = GetClockLayoutMetrics(mainScale, parts);

        var windowPos = ImGui.GetWindowPos();
        var contentOrigin = windowPos + new Vector2(InvisibleWindowPadding, InvisibleWindowPadding);
        if (!profile.ShowLocalTime)
        {
            DrawOverlayInfoLines(profile, contentOrigin.X, contentOrigin.Y, mainPanelSize.X);
            DrawWithAlarmTilt(contentOrigin, mainPanelSize, () =>
                DrawMainPanel(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, contentOrigin, mainPanelSize, windowPos));
            return;
        }

        var localLayout = GetLocalClockLayout(profile);
        var adjustedLocalPanelSize = GetAdjustedLocalPanelSize(profile, localLayout);
        var localTopOverflow = GetLocalTopOverflow(profile);
        var localLeftOverflow = GetLocalLeftOverflow(profile);

        if (profile.LayoutMode == ClockLayoutMode.Vertical)
        {
            var baseCombinedHeight = MathF.Max(mainPanelSize.Y, localLayout.PanelSize.Y);
            var combinedWidth = mainPanelSize.X + SeparateLocalPanelGap + adjustedLocalPanelSize.X;
            var combinedHeight = MathF.Max(mainPanelSize.Y, adjustedLocalPanelSize.Y);

            var mainPanelPosVertical = new Vector2(
                contentOrigin.X,
                contentOrigin.Y + MathF.Floor((baseCombinedHeight - mainPanelSize.Y) * 0.5f));

            var localPanelPosVertical = new Vector2(
                contentOrigin.X + mainPanelSize.X + SeparateLocalPanelGap + localLeftOverflow,
                contentOrigin.Y + localTopOverflow + MathF.Floor((baseCombinedHeight - localLayout.PanelSize.Y) * 0.5f));

            if (profile.LocalTimePlacement == LocalTimePlacement.InsideMainPanel)
            {
                var combinedSize = new Vector2(combinedWidth, combinedHeight);
                DrawWithAlarmTilt(contentOrigin, combinedSize, () =>
                {
                    DrawMainPanelBackground(profile, styleMetrics, contentOrigin, combinedSize);
                    DrawMainClockContent(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, mainPanelPosVertical, mainPanelSize, windowPos);
                    DrawLocalClockPanel(profile, localPanelPosVertical, localLayout.PanelSize, windowPos, true);
                });
            }
            else
            {
                DrawWithAlarmTilt(mainPanelPosVertical, mainPanelSize, () =>
                    DrawMainPanel(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, mainPanelPosVertical, mainPanelSize, windowPos));
                DrawLocalClockPanel(profile, localPanelPosVertical, localLayout.PanelSize, windowPos, false);
            }

            var overlayTopY = MathF.Min(mainPanelPosVertical.Y, localPanelPosVertical.Y);
            DrawOverlayInfoLines(profile, contentOrigin.X, overlayTopY, combinedWidth);
            return;
        }

        if (profile.LocalTimePlacement == LocalTimePlacement.InsideMainPanel)
        {
            var combinedSize = new Vector2(
                MathF.Max(mainPanelSize.X, adjustedLocalPanelSize.X),
                adjustedLocalPanelSize.Y + mainPanelSize.Y
            );

            var localPanelPosInside = new Vector2(
                contentOrigin.X + MathF.Floor((combinedSize.X - adjustedLocalPanelSize.X) * 0.5f) + localLeftOverflow,
                contentOrigin.Y + localTopOverflow);

            var mainPanelPos = new Vector2(
                contentOrigin.X + MathF.Floor((combinedSize.X - mainPanelSize.X) * 0.5f),
                contentOrigin.Y + localLayout.PanelSize.Y
            );

            DrawWithAlarmTilt(contentOrigin, combinedSize, () =>
            {
                DrawMainPanelBackground(profile, styleMetrics, contentOrigin, combinedSize);
                DrawLocalClockPanel(profile, localPanelPosInside, localLayout.PanelSize, windowPos, true);
                DrawMainClockContent(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, mainPanelPos, mainPanelSize, windowPos);
            });
            DrawOverlayInfoLines(profile, contentOrigin.X, MathF.Min(localPanelPosInside.Y, mainPanelPos.Y), combinedSize.X);
            return;
        }

        var totalContentWidth = MathF.Max(mainPanelSize.X, adjustedLocalPanelSize.X);

        var localPanelPos = new Vector2(
            contentOrigin.X + MathF.Floor((totalContentWidth - adjustedLocalPanelSize.X) * 0.5f) + localLeftOverflow,
            contentOrigin.Y + localTopOverflow
        );

        DrawLocalClockPanel(profile, localPanelPos, localLayout.PanelSize, windowPos, false);

        var mainPanelPosOutside = new Vector2(
            contentOrigin.X + MathF.Floor((totalContentWidth - mainPanelSize.X) * 0.5f),
            contentOrigin.Y + localLayout.PanelSize.Y + SeparateLocalPanelGap
        );

        DrawOverlayInfoLines(profile, contentOrigin.X, MathF.Min(localPanelPos.Y, mainPanelPosOutside.Y), totalContentWidth);
        DrawWithAlarmTilt(mainPanelPosOutside, mainPanelSize, () =>
            DrawMainPanel(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, mainPanelPosOutside, mainPanelSize, windowPos));
    }


    private void DrawOverlayInfoLines(ClockProfile profile, float x, float panelTopY, float width)
    {
        var lines = BuildOverlayInfoLines(profile);
        if (lines.Count == 0)
            return;

        var lineHeights = lines.Select(line => GetOverlayLineSize(profile, line).Y).ToArray();
        var fullHeight = lineHeights.Sum() + MathF.Max(0, lines.Count - 1) * 1.0f;
        var cursorY = panelTopY - fullHeight - 3.0f;
        var drawList = ImGui.GetForegroundDrawList();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var size = GetOverlayLineSize(profile, line);
            var lineX = x + MathF.Floor((width - size.X) * 0.5f) + line.HorizontalOffset;
            var lineStyle = GetStyleMetrics(line.DisplayStyle);
            var shadow = line.ShowShadowText ? line.ShadowColor : new Vector4(0, 0, 0, 0);
            var labelScale = GetOverlayLabelScale(profile, line.Scale);
            Vector2 labelSize;
            using (PushPresetAuxFont(profile))
                labelSize = CalculateScaledTextSize(line.Label, labelScale);
            var timeScale = GetClockFontTimeScale(profile, line.Scale);
            var isTimeRun = IsDigitalClockRun(line.TimeText);
            Vector2 timeSize;
            using (plugin.PushClockTimeFont(GetFontForClockText(profile, line.TimeText)))
                timeSize = isTimeRun && IsSegmentFont(profile.TimeTextFont)
                    ? GetDigitalTimeRunSize(line.TimeText, timeScale)
                    : CalculateClockTextSize(line.TimeText, timeScale);

            var lineHeight = MathF.Max(labelSize.Y, timeSize.Y);
            var labelPos = new Vector2(lineX, cursorY - line.VerticalOffset + MathF.Floor((lineHeight - labelSize.Y) * 0.5f));
            var timePos = new Vector2(lineX + labelSize.X, cursorY - line.VerticalOffset + MathF.Floor((lineHeight - timeSize.Y) * 0.5f));

            if (!string.IsNullOrEmpty(line.Label))
            {
                using (PushPresetAuxFont(profile))
                    DrawOutlinedTextScaledOnList(drawList, line.Label, labelPos, labelScale, line.TextColor, shadow, lineStyle);
            }

            using (plugin.PushClockTimeFont(GetFontForClockText(profile, line.TimeText)))
            {
                if (isTimeRun && IsSegmentFont(profile.TimeTextFont))
                    DrawDigitalTimeRunOnList(drawList, line.TimeText, timePos, timeScale, line.TextColor, shadow, lineStyle);
                else
                    DrawClockTextScaledOnList(drawList, line.TimeText, timePos, timeScale, line.TextColor, shadow, lineStyle, true, GetCartoonSecondaryColonOffset(profile));
            }
            cursorY += lineHeights[i] + 1.0f;
        }
    }

    private List<OverlayLine> BuildOverlayInfoLines(ClockProfile profile)
    {
        var lines = new List<OverlayLine>();

        if (plugin.Configuration.ShowNextAlarmOnOverlay && TryBuildNextAlarmLine(out var alarmLine))
            lines.Add(new OverlayLine(alarmLine.Label, alarmLine.TimeText, Math.Clamp(plugin.Configuration.NextAlarmOverlayTextScale, 0.45f, 1.8f), plugin.Configuration.NextAlarmOverlayVerticalOffset, plugin.Configuration.NextAlarmOverlayHorizontalOffset, profile.NextAlarmOverlayTextColor, profile.NextAlarmOverlayShadowColor, profile.NextAlarmOverlayDisplayStyle, profile.NextAlarmOverlayShowShadowText));

        if (plugin.Configuration.ShowMaintenanceOnOverlay && TryBuildMaintenanceLine(out var maintenanceLine))
            lines.Add(new OverlayLine(maintenanceLine.Label, maintenanceLine.TimeText, Math.Clamp(plugin.Configuration.MaintenanceOverlayTextScale, 0.45f, 1.8f), plugin.Configuration.MaintenanceOverlayVerticalOffset, 0f, profile.MaintenanceOverlayTextColor, profile.MaintenanceOverlayShadowColor, profile.MaintenanceOverlayDisplayStyle, profile.MaintenanceOverlayShowShadowText));

        return lines;
    }

    private bool TryBuildNextAlarmLine(out OverlayTextParts line)
    {
        line = default;
        var alarms = plugin.Configuration.Alarms;
        if (alarms == null || alarms.Count == 0)
            return false;

        var now = DateTime.UtcNow;
        AlarmEntry? best = null;
        DateTime bestUtc = DateTime.MaxValue;

        foreach (var alarm in alarms)
        {
            if (!alarm.Enabled)
                continue;

            var pendingSnooze = alarm.SnoozedUntilUtc > DateTime.MinValue && !alarm.SnoozeTriggered;
            if (alarm.HasTriggered && !pendingSnooze)
                continue;

            if (!AlarmConfigurationService.TryGetPendingTriggerUtc(alarm, out var utc))
                continue;

            if (utc < now.AddSeconds(-2))
                continue;

            if (utc < bestUtc)
            {
                bestUtc = utc;
                best = alarm;
            }
        }

        if (best == null)
            return false;

        line = new OverlayTextParts(plugin.T("Alarm: "), FormatDigitalDuration(bestUtc - now));
        return true;
    }

    private bool TryBuildMaintenanceLine(out OverlayTextParts line)
    {
        line = default;
        if (!plugin.Configuration.HasDetectedMaintenanceTime || plugin.Configuration.DetectedMaintenanceStartUtc <= DateTime.MinValue)
            return false;

        var now = DateTime.UtcNow;
        var startUtc = DateTime.SpecifyKind(plugin.Configuration.DetectedMaintenanceStartUtc, DateTimeKind.Utc);
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(startUtc, TimeZoneInfo.Local);
        if (localStart.Date != DateTime.Now.Date)
            return false;

        var diff = startUtc - now;
        var timeText = diff.TotalSeconds > 0
            ? FormatDigitalDuration(diff)
            : plugin.T("now");

        line = new OverlayTextParts(plugin.T("Maintenance: "), timeText);
        return true;
    }

    private static string FormatDigitalDuration(TimeSpan time)
    {
        if (time.TotalSeconds < 0)
            time = TimeSpan.Zero;

        var hours = (int)Math.Floor(time.TotalHours);
        return $"{hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
    }

    private readonly record struct OverlayTextParts(string Label, string TimeText);

    private readonly record struct OverlayLine(string Label, string TimeText, float Scale, float VerticalOffset, float HorizontalOffset, Vector4 TextColor, Vector4 ShadowColor, ClockDisplayStyle DisplayStyle, bool ShowShadowText);

    private static float GetOverlayLabelScale(ClockProfile profile, float scale)
    {
        return profile.TimeTextFont == ClockTimeTextFont.Default
            ? scale
            : GetClockFontTimeScale(profile, scale);
    }

    private static bool UsesPresetAuxFont(ClockProfile profile)
    {
        return profile.DisplayStyle is ClockDisplayStyle.Tech or ClockDisplayStyle.Countdown;
    }

    private static ClockTimeTextFont GetPresetAuxFont(ClockProfile profile)
    {
        return UsesPresetAuxFont(profile) ? ClockTimeTextFont.Digital : profile.TimeTextFont;
    }

    private IDisposable PushPresetAuxFont(ClockProfile profile)
    {
        return plugin.PushClockTimeFont(GetPresetAuxFont(profile));
    }

    private static float GetMainColonSideTighten(ClockProfile profile)
    {
        return profile.DisplayStyle == ClockDisplayStyle.Tech ? 3.0f : 0.0f;
    }

    private Vector2 GetOverlayLineSize(ClockProfile profile, OverlayLine line)
    {
        var labelScale = GetOverlayLabelScale(profile, line.Scale);
        Vector2 labelSize;
        using (PushPresetAuxFont(profile))
            labelSize = CalculateScaledTextSize(line.Label, labelScale);
        var timeScale = GetClockFontTimeScale(profile, line.Scale);
        Vector2 timeSize;
        var isTimeRun = IsDigitalClockRun(line.TimeText);
        using (plugin.PushClockTimeFont(GetFontForClockText(profile, line.TimeText)))
            timeSize = isTimeRun && IsSegmentFont(profile.TimeTextFont)
                ? GetDigitalTimeRunSize(line.TimeText, timeScale)
                : CalculateClockTextSize(line.TimeText, timeScale);

        return new Vector2(labelSize.X + timeSize.X, MathF.Max(labelSize.Y, timeSize.Y));
    }

    public override void PostDraw()
    {
        DisposeWindowStyleScopes();
    }

    // The main clock window styling is intentionaly scoped around the invisible host window so themes rendering stays doesnt affect the rest of Dalamud UI.
    private void DisposeWindowStyleScopes()
    {
        if (windowStyleScopes == null)
            return;

        for (var i = windowStyleScopes.Length - 1; i >= 0; i--)
            windowStyleScopes[i]?.Dispose();

        windowStyleScopes = null;
    }

    private static float GetLocalTopOverflow(ClockProfile profile)
    {
        return MathF.Max(0.0f, -profile.LocalTimeVerticalOffset);
    }

    private static float GetLocalBottomOverflow(ClockProfile profile)
    {
        return MathF.Max(0.0f, profile.LocalTimeVerticalOffset);
    }

    private static float GetLocalLeftOverflow(ClockProfile profile)
    {
        return MathF.Max(0.0f, -profile.LocalTimeHorizontalOffset);
    }

    private static float GetLocalRightOverflow(ClockProfile profile)
    {
        return MathF.Max(0.0f, profile.LocalTimeHorizontalOffset);
    }

    private static Vector2 GetAdjustedLocalPanelSize(ClockProfile profile, LocalClockLayoutMetrics localLayout)
    {
        return new Vector2(
            localLayout.PanelSize.X + GetLocalLeftOverflow(profile) + GetLocalRightOverflow(profile),
            localLayout.PanelSize.Y + GetLocalTopOverflow(profile) + GetLocalBottomOverflow(profile));
    }

    private void DrawMainPanel(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        ClockParts parts,
        string badgeText,
        Vector2 badgeTextSize,
        ClockLayoutMetrics layout,
        float mainScale,
        float badgeScale,
        Vector2 panelPos,
        Vector2 panelSize,
        Vector2 windowPos)
    {
        DrawMainPanelBackground(profile, styleMetrics, panelPos, panelSize);
        DrawMainClockContent(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, panelPos, panelSize, windowPos);
    }

    private void DrawMainPanelBackground(ClockProfile profile, StyleMetrics styleMetrics, Vector2 panelPos, Vector2 panelSize)
    {
        var drawList = ImGui.GetWindowDrawList();
        var panelMin = panelPos;
        var panelMax = panelPos + panelSize;
        var panelRounding = GetPanelRounding(styleMetrics);

        var panelColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
            profile.ClockBackgroundColor.X,
            profile.ClockBackgroundColor.Y,
            profile.ClockBackgroundColor.Z,
            profile.ClockBackgroundOpacity));

        if (styleMetrics.TechPanel)
        {
            DrawTechPanelBackground(drawList, panelMin, panelMax, panelSize, panelRounding, panelColor, profile);
            return;
        }

        if (styleMetrics.CartoonPanel)
        {
            DrawCartoonPanelBackground(drawList, panelMin, panelMax, panelSize, panelRounding, profile);
            return;
        }

        if (styleMetrics.CountdownPanel)
            return;

        if (styleMetrics.DigitalPanel)
        {
            if (!profile.ShowBorder)
            {
                drawList.AddRectFilled(panelMin, panelMax, panelColor, panelRounding);
                return;
            }

            var outerShadow = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.55f));
            var outerBevel = ImGui.ColorConvertFloat4ToU32(new Vector4(0.82f, 0.82f, 0.80f, profile.BorderOpacity));
            var innerBevel = ImGui.ColorConvertFloat4ToU32(new Vector4(0.22f, 0.22f, 0.22f, 0.95f));
            var glassTop = ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.08f, 0.08f, 0.38f));

            drawList.AddRectFilled(panelMin + new Vector2(3f, 4f), panelMax + new Vector2(3f, 4f), outerShadow, panelRounding);
            DrawDigitalTopBezelTab(drawList, panelMin, panelMax, panelSize, outerShadow, profile);
            drawList.AddRectFilled(panelMin, panelMax, outerBevel, panelRounding);
            drawList.AddRectFilled(panelMin + new Vector2(2f, 2f), panelMax - new Vector2(2f, 2f), innerBevel, MathF.Max(0f, panelRounding - 1.5f));
            drawList.AddRectFilled(panelMin + new Vector2(5.5f, 5.5f), panelMax - new Vector2(5.5f, 5.5f), panelColor, MathF.Max(0f, panelRounding - 4f));
            drawList.AddRectFilled(panelMin + new Vector2(6.5f, 6.5f), new Vector2(panelMax.X - 6.5f, panelMin.Y + MathF.Max(9f, panelSize.Y * 0.25f)), glassTop, MathF.Max(0f, panelRounding - 5f));
            return;
        }

        drawList.AddRectFilled(panelMin, panelMax, panelColor, panelRounding);

        if (profile.ShowBorder)
        {
            var borderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
                profile.BorderColor.X,
                profile.BorderColor.Y,
                profile.BorderColor.Z,
                profile.BorderOpacity));
            drawList.AddRect(panelMin, panelMax, borderColor, panelRounding, ImDrawFlags.None, styleMetrics.BorderThickness);
        }
    }





    private static void DrawCartoonPanelBackground(ImDrawListPtr drawList, Vector2 panelMin, Vector2 panelMax, Vector2 panelSize, float panelRounding, ClockProfile profile)
    {
        panelMin = new Vector2(panelMin.X, panelMin.Y - 3f);
        panelSize = new Vector2(panelSize.X, panelSize.Y + 3f);

        var shadow = ImGui.ColorConvertFloat4ToU32(new Vector4(0.35f, 0.08f, 0.05f, 0.32f));
        var redShell = ImGui.ColorConvertFloat4ToU32(new Vector4(1.00f, 0.28f, 0.36f, MathF.Max(0.65f, profile.BorderOpacity)));
        var redDark = ImGui.ColorConvertFloat4ToU32(new Vector4(0.82f, 0.12f, 0.20f, MathF.Max(0.55f, profile.BorderOpacity * 0.9f)));
        var whiteShell = ImGui.ColorConvertFloat4ToU32(new Vector4(0.95f, 0.95f, 0.88f, 1f));
        var screen = ImGui.ColorConvertFloat4ToU32(new Vector4(0.56f, 0.86f, 0.94f, 1f));
        var screenTop = ImGui.ColorConvertFloat4ToU32(new Vector4(0.72f, 0.94f, 1.00f, 0.68f));
        var screenBorder = ImGui.ColorConvertFloat4ToU32(new Vector4(0.03f, 0.05f, 0.08f, 1f));
        var tabBlue = ImGui.ColorConvertFloat4ToU32(new Vector4(0.16f, 0.29f, 0.68f, 1f));
        var tabBlueDark = ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.17f, 0.43f, 1f));
        var orange = ImGui.ColorConvertFloat4ToU32(new Vector4(1.00f, 0.47f, 0.20f, 1f));
        var orangeDark = ImGui.ColorConvertFloat4ToU32(new Vector4(0.78f, 0.20f, 0.12f, 1f));
        var yellow = ImGui.ColorConvertFloat4ToU32(new Vector4(1.00f, 0.82f, 0.22f, 1f));
        var cyanMark = ImGui.ColorConvertFloat4ToU32(new Vector4(0.18f, 0.70f, 0.82f, 0.33f));
        var white = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.96f));
        var marble = ImGui.ColorConvertFloat4ToU32(new Vector4(0.66f, 0.66f, 0.62f, 0.52f));

        var outerRounding = MathF.Max(8f, panelRounding + 1.5f);
        var shellMin = panelMin + new Vector2(2f, 5f);
        var shellMax = panelMax - new Vector2(2f, 2f);

        var bottomMin = new Vector2(panelMin.X + panelSize.X * 0.21f, shellMax.Y - 1f);
        var bottomMax = new Vector2(panelMax.X - panelSize.X * 0.16f, shellMax.Y + Math.Clamp(panelSize.Y * 0.045f, 3f, 5f));
        drawList.AddRectFilled(bottomMin + new Vector2(0f, 3f), bottomMax + new Vector2(0f, 3f), orangeDark, 2f);
        drawList.AddRectFilled(bottomMin, bottomMax, orange, 2f);
        drawList.AddRectFilled(new Vector2(bottomMin.X + 6f, bottomMax.Y - 2f), new Vector2(bottomMax.X - 8f, bottomMax.Y + 1f), orangeDark, 1.0f);

        var tabWidth = Math.Clamp(panelSize.X * 0.47f, 74f, 150f);
        var tabHeight = Math.Clamp(panelSize.Y * 0.065f, 3f, 6f);
        var tabLeft = (panelMin.X + panelMax.X - tabWidth) * 0.5f;
        var tabTop = shellMin.Y - tabHeight + 1f;
        var tabMin = new Vector2(tabLeft, tabTop);
        var tabMax = new Vector2(tabLeft + tabWidth, shellMin.Y + 4f);
        drawList.AddRectFilled(tabMin + new Vector2(0f, 2f), tabMax + new Vector2(0f, 2f), tabBlueDark, 4f);
        drawList.AddRectFilled(tabMin, tabMax, tabBlue, 4f);
        drawList.AddCircleFilled(new Vector2(tabMin.X + 15f, tabTop + tabHeight * 0.55f), 4f, white, 20);
        drawList.AddRectFilled(new Vector2(tabMin.X + tabWidth * 0.42f, tabTop + tabHeight * 0.38f), new Vector2(tabMin.X + tabWidth * 0.77f, tabTop + tabHeight * 0.72f), white, 5f);

        drawList.AddRectFilled(shellMin + new Vector2(3f, 4f), shellMax + new Vector2(3f, 4f), shadow, outerRounding);
        drawList.AddRectFilled(shellMin, shellMax, redShell, outerRounding);
        drawList.AddRect(shellMin + new Vector2(2f, 2f), shellMax - new Vector2(2f, 2f), redDark, outerRounding - 2f, ImDrawFlags.None, 1.2f);

        var innerMin = shellMin + new Vector2(Math.Clamp(panelSize.X * 0.065f, 8f, 13f), Math.Clamp(panelSize.Y * 0.15f, 9f, 16f));
        var innerMax = shellMax - new Vector2(Math.Clamp(panelSize.X * 0.055f, 8f, 12f), Math.Clamp(panelSize.Y * 0.10f, 6f, 11f));
        drawList.AddRectFilled(innerMin - new Vector2(1.5f, 1.5f), innerMax + new Vector2(1.5f, 1.5f), whiteShell, 3.5f);
        drawList.AddRectFilled(innerMin, innerMax, screen, 3.2f);
        drawList.AddRect(innerMin, innerMax, screenBorder, 3.2f, ImDrawFlags.None, 1.8f);
        drawList.AddRectFilled(innerMin + new Vector2(2f, 2f), new Vector2(innerMax.X - 2f, innerMin.Y + MathF.Max(9f, (innerMax.Y - innerMin.Y) * 0.28f)), screenTop, 2.5f);

        var dotX = shellMin.X + Math.Clamp(panelSize.X * 0.12f, 12f, 22f);
        drawList.AddCircleFilled(new Vector2(dotX, innerMin.Y + 11f), 6f, yellow, 24);
        drawList.AddCircleFilled(new Vector2(dotX, innerMin.Y + 29f), 6f, yellow, 24);

        var rightX = innerMax.X - Math.Clamp(panelSize.X * 0.10f, 13f, 24f);
        drawList.AddBezierCubic(new Vector2(rightX, innerMin.Y + 1f), new Vector2(rightX + 8f, innerMin.Y + 10f), new Vector2(rightX - 5f, innerMin.Y + 18f), new Vector2(rightX + 10f, innerMin.Y + 27f), marble, 1.5f, 12);
        drawList.AddBezierCubic(new Vector2(rightX + 6f, innerMax.Y - 3f), new Vector2(rightX - 9f, innerMax.Y - 12f), new Vector2(rightX + 9f, innerMax.Y - 19f), new Vector2(rightX - 1f, innerMax.Y - 28f), marble, 1.4f, 12);

        drawList.AddCircle(new Vector2(innerMin.X + 35f, innerMin.Y + 3f), 12f, cyanMark, 24, 3f);
        drawList.AddCircle(new Vector2(innerMax.X - 15f, innerMax.Y - 7f), 18f, cyanMark, 24, 4f);
        drawList.AddCircleFilled(new Vector2(innerMin.X + 9f, innerMin.Y + 28f), 2.2f, white, 10);
        drawList.AddCircleFilled(new Vector2(innerMin.X + 16f, innerMin.Y + 20f), 2.0f, white, 10);
        drawList.AddLine(new Vector2(innerMin.X + 12f, innerMin.Y + 17f), new Vector2(innerMin.X + 12f, innerMin.Y + 26f), white, 1.2f);
        drawList.AddLine(new Vector2(innerMin.X + 7f, innerMin.Y + 21.5f), new Vector2(innerMin.X + 17f, innerMin.Y + 21.5f), white, 1.2f);
    }

    private static void DrawTechPanelBackground(ImDrawListPtr drawList, Vector2 panelMin, Vector2 panelMax, Vector2 panelSize, float panelRounding, uint panelColor, ClockProfile profile)
    {
        if (!profile.ShowBorder)
        {
            drawList.AddRectFilled(panelMin, panelMax, panelColor, panelRounding);
            return;
        }

        var shadow = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.42f));
        var shell = ImGui.ColorConvertFloat4ToU32(new Vector4(0.13f, 0.23f, 0.24f, profile.BorderOpacity));
        var shellDark = ImGui.ColorConvertFloat4ToU32(new Vector4(0.07f, 0.13f, 0.14f, Math.Min(0.96f, profile.BorderOpacity)));
        var innerLine = ImGui.ColorConvertFloat4ToU32(new Vector4(0.19f, 0.31f, 0.32f, profile.BorderOpacity * 0.85f));
        var cornerLine = ImGui.ColorConvertFloat4ToU32(new Vector4(0.18f, 0.31f, 0.32f, profile.BorderOpacity * 0.65f));
        var accent = ImGui.ColorConvertFloat4ToU32(new Vector4(1.00f, 0.16f, 0.22f, profile.BorderOpacity * 0.82f));

        drawList.AddRectFilled(panelMin + new Vector2(3f, 4f), panelMax + new Vector2(3f, 4f), shadow, panelRounding);
        drawList.AddRectFilled(panelMin, panelMax, shell, panelRounding);
        drawList.AddRectFilled(panelMin + new Vector2(5f, 5f), panelMax - new Vector2(5f, 5f), shellDark, MathF.Max(2f, panelRounding - 5f));
        drawList.AddRectFilled(panelMin + new Vector2(10f, 10f), panelMax - new Vector2(10f, 10f), panelColor, MathF.Max(2f, panelRounding - 10f));
        drawList.AddRect(panelMin + new Vector2(10f, 10f), panelMax - new Vector2(10f, 10f), innerLine, MathF.Max(2f, panelRounding - 10f), ImDrawFlags.None, 1.1f);

        var cut = MathF.Min(22f, panelSize.Y * 0.32f);
        drawList.AddLine(panelMin + new Vector2(cut, 6f), panelMin + new Vector2(6f, cut), cornerLine, 1.25f);
        drawList.AddLine(new Vector2(panelMax.X - cut, panelMin.Y + 6f), new Vector2(panelMax.X - 6f, panelMin.Y + cut), cornerLine, 1.25f);
        drawList.AddLine(new Vector2(panelMin.X + cut, panelMax.Y - 6f), new Vector2(panelMin.X + 6f, panelMax.Y - cut), cornerLine, 1.25f);
        drawList.AddLine(panelMax - new Vector2(cut, 6f), panelMax - new Vector2(6f, cut), cornerLine, 1.25f);

        var dotRadius = Math.Clamp(panelSize.Y * 0.07f, 3.0f, 6.5f);
        drawList.AddCircleFilled(new Vector2(panelMin.X + 12f, (panelMin.Y + panelMax.Y) * 0.5f), dotRadius, accent, 24);
        drawList.AddCircleFilled(new Vector2(panelMax.X - 12f, (panelMin.Y + panelMax.Y) * 0.5f), dotRadius, accent, 24);
        drawList.AddCircle(new Vector2(panelMin.X + 12f, (panelMin.Y + panelMax.Y) * 0.5f), dotRadius + 1.4f, innerLine, 24, 1f);
        drawList.AddCircle(new Vector2(panelMax.X - 12f, (panelMin.Y + panelMax.Y) * 0.5f), dotRadius + 1.4f, innerLine, 24, 1f);
    }

    private static void DrawDigitalTopBezelTab(ImDrawListPtr drawList, Vector2 panelMin, Vector2 panelMax, Vector2 panelSize, uint shadow, ClockProfile profile)
    {
        var tabBase = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 1f));
        var tabOverlayColor = new Vector4(profile.ClockTextColor.X, profile.ClockTextColor.Y, profile.ClockTextColor.Z, Math.Clamp(profile.ClockTextColor.W * 0.70f, 0.0f, 0.70f));
        var tabOverlay = ImGui.ColorConvertFloat4ToU32(tabOverlayColor);
        var whiteOutline = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.75f));
        var topHighlight = ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.24f));

        if (profile.LayoutMode == ClockLayoutMode.Vertical)
        {
            var tabHeight = Math.Clamp(panelSize.Y * 0.45f, 58f, 142f);
            var verticalTabWidth = MathF.Max(5f, Math.Clamp(panelSize.X * 0.115f, 6f, 9.5f) - 1f);
            var centerY = (panelMin.Y + panelMax.Y) * 0.5f;
            var tabRight = panelMin.X + 2f;
            var tabLeft = tabRight - verticalTabWidth;
            var top = centerY - (tabHeight * 0.5f);
            var bottom = centerY + (tabHeight * 0.5f);
            var rounding = MathF.Max(4.0f, verticalTabWidth * 0.9f);

            var shadowMin = new Vector2(tabLeft + 2f, top + 1f);
            var shadowMax = new Vector2(tabRight + 2f, bottom + 1f);
            var tabMin = new Vector2(tabLeft, top);
            var tabMax = new Vector2(tabRight, bottom);

            drawList.AddRectFilled(shadowMin, shadowMax, shadow, rounding);
            drawList.AddRectFilled(tabMin, tabMax, tabBase, rounding);
            drawList.AddRectFilled(tabMin, tabMax, tabOverlay, rounding);
            drawList.AddRect(tabMin, tabMax, whiteOutline, rounding, ImDrawFlags.None, 1.0f);
            drawList.AddLine(new Vector2(tabLeft + 1f, top + 8f), new Vector2(tabLeft + 1f, bottom - 8f), topHighlight, 1.0f);
            return;
        }

        var tabWidth = Math.Clamp(panelSize.X * 0.52f, 82f, 178f);
        var tabHeightHorizontal = MathF.Max(5f, Math.Clamp(panelSize.Y * 0.115f, 6f, 9.5f) - 1f);
        var centerX = (panelMin.X + panelMax.X) * 0.5f;
        var tabBottom = panelMin.Y + 2f;
        var tabTop = tabBottom - tabHeightHorizontal;
        var left = centerX - (tabWidth * 0.5f);
        var right = centerX + (tabWidth * 0.5f);
        var roundingHorizontal = MathF.Max(4.0f, tabHeightHorizontal * 0.9f);

        var shadowMinHorizontal = new Vector2(left + 1f, tabTop + 2f);
        var shadowMaxHorizontal = new Vector2(right + 1f, tabBottom + 2f);
        var tabMinHorizontal = new Vector2(left, tabTop);
        var tabMaxHorizontal = new Vector2(right, tabBottom);

        drawList.AddRectFilled(shadowMinHorizontal, shadowMaxHorizontal, shadow, roundingHorizontal);
        drawList.AddRectFilled(tabMinHorizontal, tabMaxHorizontal, tabBase, roundingHorizontal);
        drawList.AddRectFilled(tabMinHorizontal, tabMaxHorizontal, tabOverlay, roundingHorizontal);
        drawList.AddRect(tabMinHorizontal, tabMaxHorizontal, whiteOutline, roundingHorizontal, ImDrawFlags.None, 1.0f);
        drawList.AddLine(new Vector2(left + 8f, tabTop + 1f), new Vector2(right - 8f, tabTop + 1f), topHighlight, 1.0f);
    }

    private void DrawMainClockContent(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        ClockParts parts,
        string badgeText,
        Vector2 badgeTextSize,
        ClockLayoutMetrics layout,
        float mainScale,
        float badgeScale,
        Vector2 panelPos,
        Vector2 panelSize,
        Vector2 windowPos)
    {
        if (profile.LayoutMode == ClockLayoutMode.Horizontal)
            DrawHorizontal(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, panelPos, panelSize, windowPos);
        else
            DrawVertical(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, panelPos, panelSize, windowPos);
    }

    private void DrawLocalClockPanel(
        ClockProfile profile,
        Vector2 panelPos,
        Vector2 panelSize,
        Vector2 windowPos,
        bool isInsideMainPanel)
    {
        var localLayout = GetLocalClockLayout(profile);
        var localStyleMetrics = GetStyleMetrics(profile.LocalTimeDisplayStyle);
        var drawList = ImGui.GetWindowDrawList();

        var panelOffset = new Vector2(profile.LocalTimeHorizontalOffset, profile.LocalTimeVerticalOffset);
        var drawMin = panelPos + panelOffset;
        var drawMax = panelPos + panelSize + panelOffset;
        var panelRounding = isInsideMainPanel ? 0.0f : GetPanelRounding(localStyleMetrics);

        if (!isInsideMainPanel && profile.LocalTimeBackgroundOpacity > 0.0f)
        {
            var backgroundColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
                profile.LocalTimeBackgroundColor.X,
                profile.LocalTimeBackgroundColor.Y,
                profile.LocalTimeBackgroundColor.Z,
                profile.LocalTimeBackgroundOpacity));

            drawList.AddRectFilled(drawMin, drawMax, backgroundColor, panelRounding);
        }

        if (!isInsideMainPanel && profile.LocalTimeShowBorder)
        {
            var borderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
                profile.LocalTimeBorderColor.X,
                profile.LocalTimeBorderColor.Y,
                profile.LocalTimeBorderColor.Z,
                profile.LocalTimeBorderOpacity));

            drawList.AddRect(drawMin, drawMax, borderColor, panelRounding, ImDrawFlags.None, localStyleMetrics.BorderThickness);
        }

        if (localLayout.IsVertical)
        {
            DrawLocalClockPanelVertical(profile, localLayout, localStyleMetrics, panelPos, panelSize, windowPos);
            return;
        }

        var contentX = panelPos.X + MathF.Floor((panelSize.X - localLayout.ContentSize.X) * 0.5f) + profile.LocalTimeHorizontalOffset;
        var contentY = panelPos.Y + MathF.Floor((panelSize.Y - localLayout.ContentSize.Y) * 0.5f) + profile.LocalTimeVerticalOffset;

        var shadowColor = profile.LocalTimeShowShadowText ? profile.LocalTimeShadowColor : new Vector4(0, 0, 0, 0);
        float timeStartX = contentX;

        if (localLayout.UseBadge)
        {
            var badgeMin = new Vector2(
                contentX,
                contentY + MathF.Floor((localLayout.ContentSize.Y - localLayout.BadgeSize.Y) * 0.5f) + localStyleMetrics.BadgeVerticalOffset);
            var badgeMax = badgeMin + localLayout.BadgeSize;
            DrawLocalBadge(profile, localStyleMetrics, localLayout.BadgeText, localLayout.BadgeScale, badgeMin, badgeMax, windowPos);
            timeStartX = badgeMax.X + localStyleMetrics.BadgeGap;
        }
        else
        {
            var prefixPos = new Vector2(
                contentX,
                contentY + MathF.Floor((localLayout.ContentSize.Y - localLayout.PrefixSize.Y) * 0.5f));

            DrawOutlinedTextScaled(
                localLayout.PrefixText,
                prefixPos - windowPos,
                localLayout.Scale,
                profile.LocalTimeTextColor,
                shadowColor,
                localStyleMetrics);

            timeStartX = contentX + localLayout.PrefixSize.X;
        }

        var timePos = new Vector2(
            timeStartX,
            contentY + MathF.Floor((localLayout.ContentSize.Y - localLayout.TimeLayout.TotalSize.Y) * 0.5f));

        DrawClockHorizontal(
            localLayout.Parts,
            timePos - windowPos,
            localLayout.Scale,
            profile.LocalTimeTextColor,
            shadowColor,
            localStyleMetrics,
            LocalColonText,
            LocalMinuteDigitGap,
            LocalColonSideTighten);
    }

    private void DrawLocalClockPanelVertical(
        ClockProfile profile,
        LocalClockLayoutMetrics localLayout,
        StyleMetrics styleMetrics,
        Vector2 panelPos,
        Vector2 panelSize,
        Vector2 windowPos)
    {
        float lineHeight;
        using (plugin.PushClockTimeFont(profile.TimeTextFont))
            lineHeight = CalculateScaledTextSize("8", localLayout.Scale).Y;

        float contentX = panelPos.X + MathF.Floor((panelSize.X - localLayout.ContentSize.X) * 0.5f) + profile.LocalTimeHorizontalOffset;
        float contentY = panelPos.Y + MathF.Floor((panelSize.Y - localLayout.ContentSize.Y) * 0.5f) + profile.LocalTimeVerticalOffset;

        float centerStartX = contentX + localLayout.LabelColumnWidth + (localLayout.LabelColumnWidth > 0 ? styleMetrics.BadgeGap : 0f);
        float centerLineWidth = localLayout.TimeLayout.TotalSize.X;
        var shadowColor = profile.LocalTimeShowShadowText ? profile.LocalTimeShadowColor : new Vector4(0, 0, 0, 0);

        if (localLayout.Parts.IsFullText)
        {
            var fullPos = new Vector2(
                centerStartX + MathF.Floor((centerLineWidth - localLayout.TimeLayout.TotalSize.X) * 0.5f),
                contentY + MathF.Floor((localLayout.ContentSize.Y - localLayout.TimeLayout.TotalSize.Y) * 0.5f));
            DrawClockTextScaled(localLayout.Parts.FullText, fullPos - windowPos, localLayout.Scale, profile.LocalTimeTextColor, shadowColor, styleMetrics);
            return;
        }

        float timeStartY = contentY + MathF.Floor((localLayout.ContentSize.Y - localLayout.TimeLayout.TotalSize.Y) * 0.5f);
        if (!string.IsNullOrWhiteSpace(localLayout.Parts.Prefix))
        {
            DrawCenteredLineCustom(localLayout.Parts.Prefix, centerStartX, centerLineWidth, timeStartY, localLayout.Scale, profile.LocalTimeTextColor, shadowColor, styleMetrics);
            timeStartY += CalculateClockTextSize(localLayout.Parts.Prefix, localLayout.Scale).Y + MathF.Max(2f, 2f * localLayout.Scale);
        }

        var leftDigits = GetVerticalLeftLines(localLayout.Parts.Left);
        float leftBlockHeight = leftDigits.Length * lineHeight;
        float colonY = timeStartY + leftBlockHeight;
        float minuteStartY = colonY + lineHeight;
        var labelStartY = contentY + MathF.Floor((localLayout.ContentSize.Y - localLayout.LabelTextHeight) * 0.5f);

        if (localLayout.UseBadge)
        {
            float badgeMinY = colonY - MathF.Floor((localLayout.LabelTextHeight - lineHeight) * 0.5f);
            var badgeMin = new Vector2(contentX, badgeMinY + styleMetrics.BadgeVerticalOffset);
            var badgeMax = new Vector2(badgeMin.X + localLayout.LabelColumnWidth, badgeMin.Y + localLayout.LabelTextHeight);
            DrawLocalBadgeVertical(profile, styleMetrics, localLayout.BadgeText, localLayout.BadgeScale, badgeMin, badgeMax, windowPos);
        }
        else
        {
            using (PushPresetAuxFont(profile))
                DrawVerticalStackedText(localLayout.LabelText, contentX, localLayout.LabelColumnWidth, labelStartY, localLayout.BadgeScale, profile.LocalTimeTextColor, shadowColor, styleMetrics, windowPos);
        }

        using (plugin.PushClockTimeFont(profile.TimeTextFont))
        {
            for (int i = 0; i < leftDigits.Length; i++)
                DrawCenteredLineCustom(leftDigits[i], centerStartX, centerLineWidth, timeStartY + (i * lineHeight), localLayout.Scale, profile.LocalTimeTextColor, shadowColor, styleMetrics);

            var colonVisible = ShouldShowColon(plugin.Configuration.ColonAnimation);
            DrawCenteredLineCustom(LocalColonText, centerStartX, centerLineWidth, colonY, localLayout.Scale, profile.LocalTimeTextColor, shadowColor, styleMetrics, colonVisible);
            DrawCenteredLineCustom(localLayout.Parts.MinuteLeft, centerStartX, centerLineWidth, minuteStartY, localLayout.Scale, profile.LocalTimeTextColor, shadowColor, styleMetrics);
            DrawCenteredLineCustom(localLayout.Parts.MinuteRight, centerStartX, centerLineWidth, minuteStartY + lineHeight, localLayout.Scale, profile.LocalTimeTextColor, shadowColor, styleMetrics);

            var nextY = minuteStartY + (lineHeight * 2);
            if (localLayout.Parts.HasSeconds)
            {
                DrawCenteredLineCustom(LocalColonText, centerStartX, centerLineWidth, nextY, localLayout.Scale, profile.LocalTimeTextColor, shadowColor, styleMetrics, colonVisible);
                DrawCenteredLineCustom(localLayout.Parts.SecondLeft, centerStartX, centerLineWidth, nextY + lineHeight, localLayout.Scale, profile.LocalTimeTextColor, shadowColor, styleMetrics);
                DrawCenteredLineCustom(localLayout.Parts.SecondRight, centerStartX, centerLineWidth, nextY + (lineHeight * 2), localLayout.Scale, profile.LocalTimeTextColor, shadowColor, styleMetrics);
                nextY += lineHeight * 3;
            }

            if (!string.IsNullOrWhiteSpace(localLayout.Parts.Suffix))
            {
                nextY += MathF.Max(2f, 2f * localLayout.Scale);
                DrawCenteredLineCustom(localLayout.Parts.Suffix, centerStartX, centerLineWidth, nextY, localLayout.Scale, profile.LocalTimeTextColor, shadowColor, styleMetrics);
            }
        }
    }

    private static float GetMainContentHorizontalShift(ClockProfile profile, StyleMetrics styleMetrics)
    {
        if (styleMetrics.TechPanel)
            return 6.0f;

        return styleMetrics.DigitalPanel ? 4.0f : 0.0f;
    }

    private static float GetHorizontalBadgeOffsetX(ClockProfile profile)
    {
        return profile.DisplayStyle == ClockDisplayStyle.Tech ? TechBadgeOffsetX : 0.0f;
    }

    private static float GetHorizontalBadgeOverflow(ClockProfile profile)
    {
        return MathF.Abs(GetHorizontalBadgeOffsetX(profile));
    }

    private static Vector2 GetVerticalPresetBadgeOffset(ClockProfile profile, StyleMetrics styleMetrics)
    {
        if (profile.LayoutMode != ClockLayoutMode.Vertical)
            return Vector2.Zero;

        if (styleMetrics.CartoonPanel)
            return new Vector2(3f, 5f);

        if (styleMetrics.TechPanel)
            return new Vector2(2f, 4f);

        return styleMetrics.DigitalPanel ? new Vector2(1f, 3f) : Vector2.Zero;
    }


    private Vector2 GetCountdownMainPanelSize(ClockProfile profile, ClockParts parts, Vector2 badgeTextSize, float mainScale, float badgeScale, StyleMetrics styleMetrics)
    {
        var content = profile.LayoutMode == ClockLayoutMode.Vertical
            ? GetCountdownVerticalContentSize(profile, parts, badgeTextSize, mainScale, badgeScale, styleMetrics)
            : GetCountdownContentSize(profile, parts, mainScale);

        if (profile.ShowIcon && profile.LayoutMode != ClockLayoutMode.Vertical)
        {
            var badgeWidth = badgeTextSize.X + (styleMetrics.BadgePaddingX * 2f);
            var badgeHeight = badgeTextSize.Y + (styleMetrics.BadgePaddingY * 2f);
            content = new Vector2(content.X + badgeWidth + styleMetrics.BadgeGap, MathF.Max(content.Y, badgeHeight));
        }

        return new Vector2(
            content.X + (styleMetrics.MainPaddingX * 2f) + MainPanelExtraSize + 8f,
            content.Y + (styleMetrics.MainPaddingY * 2f) + MainPanelExtraSize + 6f);
    }

    private Vector2 GetCountdownContentSize(ClockProfile profile, ClockParts parts, float scale)
    {
        using (plugin.PushClockTimeFont(profile.TimeTextFont))
        {
            var digit = CalculateScaledTextSize("8", scale);
            var cardW = GetCountdownCardWidth(digit, scale);
            var cardH = GetCountdownCardHeight(digit, scale);
            var digitGap = MathF.Max(2f, 3f * scale);
            var groupGap = GetCountdownColonGap(scale);
            var colonScale = GetCountdownColonScale(scale);
            float colonW;
            float labelH;
            var labelScale = GetCountdownLabelScale(scale);
            using (PushPresetAuxFont(profile))
            {
                colonW = CalculateScaledTextSize(LocalColonText, colonScale).X;
                labelH = CalculateScaledTextSize("MINS", labelScale).Y;
            }
            var groups = GetCountdownGroups(parts, parts);
            var width = 0f;

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                if (groupIndex > 0)
                    width += groupGap + colonW + groupGap;

                var group = groups[groupIndex];
                var cardsWidth = group.Text.Length * cardW + MathF.Max(0, group.Text.Length - 1) * digitGap;
                float labelWidth;
                using (PushPresetAuxFont(profile))
                    labelWidth = CalculateScaledTextSize(group.Label, labelScale).X;
                width += MathF.Max(cardsWidth, labelWidth);
            }

            if (!string.IsNullOrWhiteSpace(parts.Prefix))
            {
                using (PushPresetAuxFont(profile))
                    width += CalculateScaledTextSize(parts.Prefix + " ", GetCountdownSuffixScale(scale)).X;
            }
            if (!string.IsNullOrWhiteSpace(parts.Suffix))
            {
                using (PushPresetAuxFont(profile))
                    width += GetCountdownColonGap(scale) * 0.35f + GetCountdownTextPlateSize(parts.Suffix.ToUpperInvariant(), GetCountdownSuffixScale(scale), GetStyleMetrics(profile.DisplayStyle)).X;
            }

            return new Vector2(width, cardH + labelH + MathF.Max(4f, 5f * scale));
        }
    }

    private Vector2 GetCountdownVerticalContentSize(ClockProfile profile, ClockParts parts, Vector2 badgeTextSize, float mainScale, float badgeScale, StyleMetrics styleMetrics)
    {
        using (plugin.PushClockTimeFont(profile.TimeTextFont))
        {
            var digit = CalculateScaledTextSize("8", mainScale);
            var cardW = GetCountdownCardWidth(digit, mainScale);
            var cardH = GetCountdownCardHeight(digit, mainScale);
            var lineGap = MathF.Max(3f, 4f * mainScale);
            var colonScale = GetCountdownColonScale(mainScale);
            Vector2 colonSize;
            var labelScale = GetCountdownLabelScale(mainScale);
            using (PushPresetAuxFont(profile))
                colonSize = CalculateScaledTextSize(LocalColonText, colonScale);
            var suffixScale = GetCountdownSuffixScale(mainScale);
            var groups = GetCountdownGroups(parts, parts);

            var width = MathF.Max(cardW, colonSize.X);
            var height = 0f;

            if (!string.IsNullOrWhiteSpace(parts.Prefix))
            {
                using (PushPresetAuxFont(profile))
                    height += CalculateScaledTextSize(parts.Prefix, suffixScale).Y + lineGap;
            }

            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                if (groupIndex > 0)
                    height += colonSize.Y + lineGap;

                var group = groups[groupIndex];
                Vector2 labelSize;
                using (PushPresetAuxFont(profile))
                    labelSize = CalculateScaledTextSize(group.Label, labelScale);
                width = MathF.Max(width, labelSize.X);
                height += group.Text.Length * cardH + MathF.Max(0, group.Text.Length - 1) * lineGap;
                height += MathF.Max(2f, 3f * mainScale) + labelSize.Y + lineGap;
            }

            if (!string.IsNullOrWhiteSpace(parts.Suffix))
            {
                Vector2 suffixPlate;
                using (PushPresetAuxFont(profile))
                    suffixPlate = GetCountdownTextPlateSize(parts.Suffix.ToUpperInvariant(), suffixScale, styleMetrics);
                width = MathF.Max(width, suffixPlate.X);
                height += suffixPlate.Y + lineGap;
            }

            if (profile.ShowIcon)
            {
                var badgeRotatedWidth = GetBadgeVerticalWidth(badgeTextSize, styleMetrics);
                var badgeRotatedHeight = GetBadgeVerticalHeight(profile, badgeTextSize, styleMetrics, GetMainBadgeText(), badgeScale);
                width += badgeRotatedWidth + styleMetrics.BadgeGap;
                height = MathF.Max(height, badgeRotatedHeight);
            }

            return new Vector2(width, height);
        }
    }

    private static float GetCountdownCardWidth(Vector2 digitSize, float scale)
    {
        return MathF.Max(18f, digitSize.X * 0.82f + (8f * scale));
    }

    private static float GetCountdownCardHeight(Vector2 digitSize, float scale)
    {
        return MathF.Max(30f, digitSize.Y * 1.06f + (4f * scale));
    }

    private static float GetCountdownLabelScale(float scale)
    {
        return MathF.Max(0.60f, scale * 0.56f);
    }

    private static float GetCountdownSuffixScale(float scale)
    {
        return MathF.Max(0.82f, scale * 0.78f);
    }

    private static float GetCountdownColonScale(float scale)
    {
        return MathF.Max(0.75f, scale * 0.86f);
    }

    private static float GetCountdownColonGap(float scale)
    {
        return MathF.Max(6f, 6f * scale);
    }

    private readonly record struct CountdownGroup(string Text, string PreviousText, string Label);

    private static List<CountdownGroup> GetCountdownGroups(ClockParts parts, ClockParts previous)
    {
        var left = string.IsNullOrWhiteSpace(parts.Left) ? "00" : parts.Left.PadLeft(2, '0');
        var previousLeft = string.IsNullOrWhiteSpace(previous.Left) ? left : previous.Left.PadLeft(2, '0');

        var groups = new List<CountdownGroup>
        {
            new(left, previousLeft, "HRS"),
            new(parts.MinuteLeft + parts.MinuteRight, previous.MinuteLeft + previous.MinuteRight, "MINS")
        };

        if (parts.HasSeconds)
            groups.Add(new CountdownGroup(parts.SecondLeft + parts.SecondRight, previous.SecondLeft + previous.SecondRight, "SECS"));

        return groups;
    }

    private ClockParts GetPreviousClockPartsForCountdown(ClockParts current)
    {
        var zoneId = plugin.Configuration.SelectedTimeZoneId;
        var utc = DateTime.UtcNow.AddSeconds(-1);

        if (plugin.TryGetAlarmOverlayVisual(out var alarmUtc, out var alarmZoneId, out _))
        {
            utc = alarmUtc;
            zoneId = alarmZoneId;
        }

        var dateInZone = TimeZoneHelper.ConvertFromUtc(utc, zoneId);
        var previous = BuildClockParts(dateInZone, plugin.Configuration.TimeFormat, plugin.Configuration.GetActiveProfile().LayoutMode);
        return previous.IsFullText ? current : previous;
    }

    private void DrawCountdownHorizontal(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        ClockParts parts,
        string badgeText,
        Vector2 badgeTextSize,
        float mainScale,
        float badgeScale,
        Vector2 panelPos,
        Vector2 panelSize,
        Vector2 windowPos)
    {
        if (parts.IsFullText)
        {
            var fullPos = new Vector2(
                panelPos.X + MathF.Floor((panelSize.X - CalculateClockTextSize(parts.FullText, mainScale).X) * 0.5f),
                panelPos.Y + MathF.Floor((panelSize.Y - CalculateClockTextSize(parts.FullText, mainScale).Y) * 0.5f));
            DrawClockTextScaled(parts.FullText, fullPos - windowPos, mainScale, GetMainClockTextColor(profile), profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0, 0, 0, 0), styleMetrics);
            return;
        }

        var contentSize = GetCountdownContentSize(profile, parts, mainScale);
        var badgeWidth = profile.ShowIcon ? badgeTextSize.X + (styleMetrics.BadgePaddingX * 2f) : 0f;
        var badgeHeight = profile.ShowIcon ? badgeTextSize.Y + (styleMetrics.BadgePaddingY * 2f) : 0f;
        var totalW = contentSize.X + (profile.ShowIcon ? badgeWidth + styleMetrics.BadgeGap : 0f);
        var startX = panelPos.X + MathF.Floor((panelSize.X - totalW) * 0.5f);
        var startY = panelPos.Y + MathF.Floor((panelSize.Y - MathF.Max(contentSize.Y, badgeHeight)) * 0.5f);

        if (profile.ShowIcon)
        {
            var badgeMin = new Vector2(startX, startY + MathF.Floor((MathF.Max(contentSize.Y, badgeHeight) - badgeHeight) * 0.5f) + styleMetrics.BadgeVerticalOffset);
            DrawBadge(profile, styleMetrics, badgeText, badgeScale, badgeMin, badgeMin + new Vector2(badgeWidth, badgeHeight), windowPos);
            startX += badgeWidth + styleMetrics.BadgeGap;
        }

        var previous = GetPreviousClockPartsForCountdown(parts);
        var groups = GetCountdownGroups(parts, previous);
        var drawList = ImGui.GetWindowDrawList();
        var textColor = GetMainClockTextColor(profile);
        var shadow = profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0f, 0f, 0f, 0f);

        using (plugin.PushClockTimeFont(profile.TimeTextFont))
        {
            var digit = CalculateScaledTextSize("8", mainScale);
            var cardW = GetCountdownCardWidth(digit, mainScale);
            var cardH = GetCountdownCardHeight(digit, mainScale);
            var digitGap = MathF.Max(2f, 3f * mainScale);
            var groupGap = GetCountdownColonGap(mainScale);
            var labelScale = GetCountdownLabelScale(mainScale);
            var suffixScale = GetCountdownSuffixScale(mainScale);
            var colonScale = GetCountdownColonScale(mainScale);
            var labelGap = MathF.Max(3f, 4f * mainScale);
            var x = startX;

            if (!string.IsNullOrWhiteSpace(parts.Prefix))
            {
                var prefix = parts.Prefix + " ";
                using (PushPresetAuxFont(profile))
                {
                    DrawOutlinedTextScaled(prefix, new Vector2(x, startY + cardH * 0.22f) - windowPos, suffixScale, textColor, shadow, styleMetrics);
                    x += CalculateScaledTextSize(prefix, suffixScale).X;
                }
            }

            var progress = GetCountdownFlipProgress();
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                if (groupIndex > 0)
                {
                    x += groupGap;
                    Vector2 colonSize;
                    using (PushPresetAuxFont(profile))
                        colonSize = CalculateScaledTextSize(LocalColonText, colonScale);
                    var colonPos = new Vector2(x, startY + MathF.Floor((cardH - colonSize.Y) * 0.48f));
                    if (ShouldShowColon(plugin.Configuration.ColonAnimation))
                    {
                        using (PushPresetAuxFont(profile))
                            DrawCountdownColon(drawList, LocalColonText, colonPos, colonScale * 1.08f, textColor, shadow, styleMetrics);
                    }
                    x += colonSize.X + groupGap;
                }

                var group = groups[groupIndex];
                var cardsWidth = group.Text.Length * cardW + MathF.Max(0, group.Text.Length - 1) * digitGap;
                Vector2 labelSize;
                using (PushPresetAuxFont(profile))
                    labelSize = CalculateScaledTextSize(group.Label, labelScale);
                var groupWidth = MathF.Max(cardsWidth, labelSize.X);
                var digitX = x + MathF.Floor((groupWidth - cardsWidth) * 0.5f);

                for (var i = 0; i < group.Text.Length; i++)
                {
                    var currentChar = group.Text[i].ToString();
                    var previousChar = i < group.PreviousText.Length ? group.PreviousText[i].ToString() : currentChar;
                    DrawCountdownCard(profile, drawList, new Vector2(digitX, startY), new Vector2(cardW, cardH), currentChar, previousChar, progress, mainScale, textColor, shadow, styleMetrics);
                    digitX += cardW + digitGap;
                }

                var labelPos = new Vector2(x + MathF.Floor((groupWidth - labelSize.X) * 0.5f), startY + cardH + labelGap);
                using (PushPresetAuxFont(profile))
                    DrawOutlinedTextScaled(group.Label, labelPos - windowPos, labelScale, textColor, shadow, styleMetrics);
                x += groupWidth;
            }

            if (!string.IsNullOrWhiteSpace(parts.Suffix))
            {
                var suffixText = parts.Suffix.ToUpperInvariant();
                Vector2 plateSize;
                using (PushPresetAuxFont(profile))
                    plateSize = GetCountdownTextPlateSize(suffixText, suffixScale, styleMetrics);
                var plateMin = new Vector2(x + groupGap * 0.35f, startY + MathF.Floor((cardH - plateSize.Y) * 0.5f));
                using (PushPresetAuxFont(profile))
                    DrawCountdownPlateText(profile, drawList, suffixText, plateMin, plateMin + plateSize, suffixScale, textColor, shadow, styleMetrics);
            }
        }
    }

    private void DrawCountdownVertical(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        ClockParts parts,
        string badgeText,
        Vector2 badgeTextSize,
        float mainScale,
        float badgeScale,
        Vector2 panelPos,
        Vector2 panelSize,
        Vector2 windowPos)
    {
        if (parts.IsFullText)
        {
            var fullPos = new Vector2(
                panelPos.X + MathF.Floor((panelSize.X - CalculateClockTextSize(parts.FullText, mainScale).X) * 0.5f),
                panelPos.Y + MathF.Floor((panelSize.Y - CalculateClockTextSize(parts.FullText, mainScale).Y) * 0.5f));
            DrawClockTextScaled(parts.FullText, fullPos - windowPos, mainScale, GetMainClockTextColor(profile), profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0, 0, 0, 0), styleMetrics);
            return;
        }

        var contentSize = GetCountdownVerticalContentSize(profile, parts, badgeTextSize, mainScale, badgeScale, styleMetrics);
        var contentX = panelPos.X + MathF.Floor((panelSize.X - contentSize.X) * 0.5f) + GetMainContentHorizontalShift(profile, styleMetrics);
        var contentY = panelPos.Y + MathF.Floor((panelSize.Y - contentSize.Y) * 0.5f) + GetTechTextVerticalOffset(profile, styleMetrics);

        var badgeRotatedWidth = profile.ShowIcon ? GetBadgeVerticalWidth(badgeTextSize, styleMetrics) : 0f;
        var badgeRotatedHeight = profile.ShowIcon ? GetBadgeVerticalHeight(profile, badgeTextSize, styleMetrics, badgeText, badgeScale) : 0f;
        var columnX = contentX + (profile.ShowIcon ? badgeRotatedWidth + styleMetrics.BadgeGap : 0f);
        var columnW = contentSize.X - (profile.ShowIcon ? badgeRotatedWidth + styleMetrics.BadgeGap : 0f);

        if (profile.ShowIcon)
        {
            var badgeMin = new Vector2(contentX, contentY + MathF.Floor((contentSize.Y - badgeRotatedHeight) * 0.5f) + styleMetrics.BadgeVerticalOffset) + GetVerticalPresetBadgeOffset(profile, styleMetrics);
            DrawBadgeVertical(profile, styleMetrics, badgeText, badgeScale, badgeMin, badgeMin + new Vector2(badgeRotatedWidth, badgeRotatedHeight), windowPos);
        }

        var previous = GetPreviousClockPartsForCountdown(parts);
        var groups = GetCountdownGroups(parts, previous);
        var drawList = ImGui.GetWindowDrawList();
        var textColor = GetMainClockTextColor(profile);
        var shadow = profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0f, 0f, 0f, 0f);

        using (plugin.PushClockTimeFont(profile.TimeTextFont))
        {
            var digit = CalculateScaledTextSize("8", mainScale);
            var cardW = GetCountdownCardWidth(digit, mainScale);
            var cardH = GetCountdownCardHeight(digit, mainScale);
            var lineGap = MathF.Max(3f, 4f * mainScale);
            var labelGap = MathF.Max(2f, 3f * mainScale);
            var labelScale = GetCountdownLabelScale(mainScale);
            var suffixScale = GetCountdownSuffixScale(mainScale);
            var colonScale = GetCountdownColonScale(mainScale);
            var y = contentY;

            if (!string.IsNullOrWhiteSpace(parts.Prefix))
            {
                Vector2 prefixSize;
                using (PushPresetAuxFont(profile))
                {
                    prefixSize = CalculateScaledTextSize(parts.Prefix, suffixScale);
                    DrawOutlinedTextScaled(parts.Prefix, new Vector2(columnX + MathF.Floor((columnW - prefixSize.X) * 0.5f), y) - windowPos, suffixScale, textColor, shadow, styleMetrics);
                }
                y += prefixSize.Y + lineGap;
            }

            var progress = GetCountdownFlipProgress();
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                if (groupIndex > 0)
                {
                    var colonVisible = ShouldShowColon(plugin.Configuration.ColonAnimation);
                    Vector2 colonSize;
                    using (PushPresetAuxFont(profile))
                        colonSize = CalculateScaledTextSize(LocalColonText, colonScale);
                    if (colonVisible)
                    {
                        using (PushPresetAuxFont(profile))
                            DrawCountdownColon(drawList, LocalColonText, new Vector2(columnX + MathF.Floor((columnW - colonSize.X) * 0.5f), y), colonScale * 1.08f, textColor, shadow, styleMetrics);
                    }
                    y += colonSize.Y + lineGap;
                }

                var group = groups[groupIndex];
                var cardX = columnX + MathF.Floor((columnW - cardW) * 0.5f);
                for (var i = 0; i < group.Text.Length; i++)
                {
                    var currentChar = group.Text[i].ToString();
                    var previousChar = i < group.PreviousText.Length ? group.PreviousText[i].ToString() : currentChar;
                    DrawCountdownCard(profile, drawList, new Vector2(cardX, y), new Vector2(cardW, cardH), currentChar, previousChar, progress, mainScale, textColor, shadow, styleMetrics);
                    y += cardH + lineGap;
                }

                Vector2 labelSize;
                using (PushPresetAuxFont(profile))
                    labelSize = CalculateScaledTextSize(group.Label, labelScale);
                using (PushPresetAuxFont(profile))
                    DrawOutlinedTextScaled(group.Label, new Vector2(columnX + MathF.Floor((columnW - labelSize.X) * 0.5f), y + labelGap - lineGap) - windowPos, labelScale, textColor, shadow, styleMetrics);
                y += labelGap + labelSize.Y;
            }

            if (!string.IsNullOrWhiteSpace(parts.Suffix))
            {
                y += lineGap;
                var suffixText = parts.Suffix.ToUpperInvariant();
                Vector2 plateSize;
                using (PushPresetAuxFont(profile))
                    plateSize = GetCountdownTextPlateSize(suffixText, suffixScale, styleMetrics);
                var plateMin = new Vector2(columnX + MathF.Floor((columnW - plateSize.X) * 0.5f), y);
                using (PushPresetAuxFont(profile))
                    DrawCountdownPlateText(profile, drawList, suffixText, plateMin, plateMin + plateSize, suffixScale, textColor, shadow, styleMetrics);
            }
        }
    }

    private Vector2 GetCountdownTextPlateSize(string text, float scale, StyleMetrics styleMetrics)
    {
        var size = CalculateScaledTextSize(text, scale);
        var padX = MathF.Max(5f, styleMetrics.BadgePaddingX * 0.82f);
        var padY = MathF.Max(2f, styleMetrics.BadgePaddingY * 0.60f);
        return new Vector2(size.X + padX * 2f, size.Y + padY * 2f);
    }

    private void DrawCountdownPlate(ClockProfile profile, ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, StyleMetrics styleMetrics, bool badgePlate = false)
    {
        var baseOpacity = Math.Clamp(profile.ClockBackgroundOpacity * profile.ClockBackgroundColor.W, 0f, 1f);
        var borderColor = badgePlate ? profile.IconBorderColor : profile.BorderColor;
        var borderOpacity = badgePlate
            ? Math.Clamp(profile.IconBorderOpacity * profile.IconBorderColor.W, 0f, 1f)
            : Math.Clamp(profile.BorderOpacity * profile.BorderColor.W, 0f, 1f);
        var drawBorder = badgePlate ? profile.ShowIconBorder : profile.ShowBorder;
        var bg = profile.ClockBackgroundColor;
        var top = ImGui.ColorConvertFloat4ToU32(new Vector4(Math.Clamp(bg.X + 0.070f, 0f, 1f), Math.Clamp(bg.Y + 0.070f, 0f, 1f), Math.Clamp(bg.Z + 0.070f, 0f, 1f), baseOpacity));
        var bottom = ImGui.ColorConvertFloat4ToU32(new Vector4(Math.Clamp(bg.X - 0.065f, 0f, 1f), Math.Clamp(bg.Y - 0.065f, 0f, 1f), Math.Clamp(bg.Z - 0.065f, 0f, 1f), baseOpacity));
        var border = ImGui.ColorConvertFloat4ToU32(new Vector4(borderColor.X, borderColor.Y, borderColor.Z, borderOpacity));
        var centerY = min.Y + (max.Y - min.Y) * 0.52f;

        if (drawBorder && borderOpacity > 0.001f)
            drawList.AddRectFilled(min + new Vector2(1.5f, 2.0f), max + new Vector2(1.5f, 2.0f), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.30f * borderOpacity)), rounding);
        drawList.AddRectFilled(min, max, bottom, rounding);
        drawList.AddRectFilled(min, new Vector2(max.X, centerY), top, rounding);
        if (drawBorder && borderOpacity > 0.001f)
            drawList.AddRect(min, max, border, rounding, ImDrawFlags.None, MathF.Max(1f, styleMetrics.BorderThickness));
    }

    private void DrawCountdownPlateText(ClockProfile profile, ImDrawListPtr drawList, string text, Vector2 min, Vector2 max, float scale, Vector4 textColor, Vector4 shadow, StyleMetrics styleMetrics, bool badgePlate = false)
    {
        var textSize = CalculateScaledTextSize(text, scale);
        var textPos = new Vector2(
            min.X + (((max.X - min.X) - textSize.X) * 0.5f),
            min.Y + (((max.Y - min.Y) - textSize.Y) * 0.5f));

        DrawCountdownPlate(profile, drawList, min, max, styleMetrics.BadgeRounding, styleMetrics, badgePlate);
        DrawOutlinedTextScaledOnList(drawList, text, textPos, scale, textColor, shadow, GetBadgeTextShadowMetrics(styleMetrics));
    }

    private void DrawCountdownColon(ImDrawListPtr drawList, string text, Vector2 pos, float scale, Vector4 textColor, Vector4 shadow, StyleMetrics styleMetrics)
    {
        DrawOutlinedTextScaledOnList(drawList, text, pos, scale, textColor, shadow, styleMetrics);
        DrawOutlinedTextScaledOnList(drawList, text, pos + new Vector2(MathF.Max(0.45f, scale * 0.45f), 0f), scale, textColor, shadow, styleMetrics);
    }

    private static float GetCountdownFlipProgress()
    {
        var ms = DateTime.UtcNow.Millisecond;
        return Math.Clamp(ms / 680f, 0f, 1f);
    }

    private void DrawCountdownCard(ClockProfile profile, ImDrawListPtr drawList, Vector2 pos, Vector2 size, string currentText, string previousText, float progress, float scale, Vector4 textColor, Vector4 shadow, StyleMetrics styleMetrics)
    {
        var rounding = MathF.Max(3f, size.X * 0.10f);
        var baseOpacity = Math.Clamp(profile.ClockBackgroundOpacity * profile.ClockBackgroundColor.W, 0f, 1f);
        var borderOpacity = Math.Clamp(profile.BorderOpacity * profile.BorderColor.W, 0f, 1f);
        var drawBorder = profile.ShowBorder && borderOpacity > 0.001f;
        var bg = profile.ClockBackgroundColor;
        var cardTop = ImGui.ColorConvertFloat4ToU32(new Vector4(Math.Clamp(bg.X + 0.070f, 0f, 1f), Math.Clamp(bg.Y + 0.070f, 0f, 1f), Math.Clamp(bg.Z + 0.070f, 0f, 1f), baseOpacity));
        var cardBottom = ImGui.ColorConvertFloat4ToU32(new Vector4(Math.Clamp(bg.X - 0.065f, 0f, 1f), Math.Clamp(bg.Y - 0.065f, 0f, 1f), Math.Clamp(bg.Z - 0.065f, 0f, 1f), baseOpacity));
        var bevel = ImGui.ColorConvertFloat4ToU32(new Vector4(profile.BorderColor.X, profile.BorderColor.Y, profile.BorderColor.Z, borderOpacity));
        var hinge = ImGui.ColorConvertFloat4ToU32(new Vector4(Math.Clamp(profile.BorderColor.X + 0.22f, 0f, 1f), Math.Clamp(profile.BorderColor.Y + 0.22f, 0f, 1f), Math.Clamp(profile.BorderColor.Z + 0.22f, 0f, 1f), borderOpacity));
        var centerY = pos.Y + size.Y * 0.52f;

        if (drawBorder)
            drawList.AddRectFilled(pos + new Vector2(1.8f, 2.4f), pos + size + new Vector2(1.8f, 2.4f), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.38f * borderOpacity)), rounding);
        drawList.AddRectFilled(pos, pos + size, cardBottom, rounding);
        drawList.AddRectFilled(pos, new Vector2(pos.X + size.X, centerY), cardTop, rounding);
        if (drawBorder)
            drawList.AddRect(pos, pos + size, bevel, rounding, ImDrawFlags.None, MathF.Max(1f, styleMetrics.BorderThickness));

        if (previousText != currentText && progress < 1f)
        {
            var t = 1f - MathF.Pow(1f - Math.Clamp(progress, 0f, 1f), 3f);
            var previousOffset = size.Y * t;
            var currentOffset = -size.Y * (1f - t);

            DrawCountdownFaceText(drawList, pos, size, previousText, scale, textColor, shadow, styleMetrics, previousOffset);
            DrawCountdownFaceText(drawList, pos, size, currentText, scale, textColor, shadow, styleMetrics, currentOffset);

            var shadeAlpha = 0.30f * (1f - Math.Abs((t * 2f) - 1f));
            drawList.AddRectFilled(pos + new Vector2(1f, 1f), pos + size - new Vector2(1f, 1f), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, shadeAlpha)), rounding);
            if (drawBorder)
                drawList.AddLine(new Vector2(pos.X + 2f, centerY), new Vector2(pos.X + size.X - 2f, centerY), bevel, MathF.Max(1f, styleMetrics.BorderThickness));
        }
        else
        {
            DrawCountdownFaceText(drawList, pos, size, currentText, scale, textColor, shadow, styleMetrics);
        }

        var cut = MathF.Max(0.55f, 0.55f * scale);
        drawList.AddLine(new Vector2(pos.X + 2.0f, centerY - cut * 2f), new Vector2(pos.X + size.X - 2.0f, centerY - cut * 2f), ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.14f)), MathF.Max(1f, cut));
        drawList.AddLine(new Vector2(pos.X + 1.6f, centerY - cut), new Vector2(pos.X + size.X - 1.6f, centerY - cut), ImGui.ColorConvertFloat4ToU32(new Vector4(0.55f, 0.55f, 0.55f, 0.34f)), MathF.Max(1f, cut));
        drawList.AddLine(new Vector2(pos.X + 1.5f, centerY), new Vector2(pos.X + size.X - 1.5f, centerY), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.72f)), MathF.Max(1f, cut));
        drawList.AddLine(new Vector2(pos.X + 1.6f, centerY + cut), new Vector2(pos.X + size.X - 1.6f, centerY + cut), ImGui.ColorConvertFloat4ToU32(new Vector4(0.35f, 0.35f, 0.35f, 0.32f)), MathF.Max(1f, cut));
        drawList.AddLine(new Vector2(pos.X + 2.0f, centerY + cut * 2f), new Vector2(pos.X + size.X - 2.0f, centerY + cut * 2f), ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 0.07f)), MathF.Max(1f, cut));
        if (borderOpacity > 0.001f)
        {
            drawList.AddRectFilled(new Vector2(pos.X - 2f, centerY - 3f), new Vector2(pos.X + 2f, centerY + 3f), hinge, 1f);
            drawList.AddRectFilled(new Vector2(pos.X + size.X - 2f, centerY - 3f), new Vector2(pos.X + size.X + 2f, centerY + 3f), hinge, 1f);
        }
    }

    private void DrawCountdownFaceText(ImDrawListPtr drawList, Vector2 pos, Vector2 size, string text, float scale, Vector4 textColor, Vector4 shadow, StyleMetrics styleMetrics, float yOffset = 0f)
    {
        var textSize = CalculateScaledTextSize(text, scale);
        var textPos = new Vector2(
            pos.X + MathF.Floor((size.X - textSize.X) * 0.5f),
            pos.Y + MathF.Floor((size.Y - textSize.Y) * 0.5f) + yOffset);

        drawList.PushClipRect(pos + new Vector2(1f, 1f), pos + size - new Vector2(1f, 1f), true);
        DrawOutlinedTextScaledOnList(drawList, text, textPos, scale, textColor, shadow, styleMetrics);
        drawList.PopClipRect();
    }


    private void DrawHorizontal(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        ClockParts parts,
        string badgeText,
        Vector2 badgeTextSize,
        ClockLayoutMetrics layout,
        float mainScale,
        float badgeScale,
        Vector2 panelPos,
        Vector2 panelSize,
        Vector2 windowPos)
    {
        if (styleMetrics.CountdownPanel)
        {
            DrawCountdownHorizontal(profile, styleMetrics, parts, badgeText, badgeTextSize, mainScale, badgeScale, panelPos, panelSize, windowPos);
            return;
        }

        var badgeHeight = profile.ShowIcon ? badgeTextSize.Y + (styleMetrics.BadgePaddingY * 2.0f) : 0.0f;
        var badgeWidth = profile.ShowIcon ? badgeTextSize.X + (styleMetrics.BadgePaddingX * 2.0f) : 0.0f;
        var contentHeight = profile.ShowIcon ? MathF.Max(layout.TotalSize.Y, badgeHeight) : layout.TotalSize.Y;
        var contentWidth = (profile.ShowIcon ? badgeWidth + styleMetrics.BadgeGap : 0.0f) + layout.TotalSize.X;

        var contentHorizontalShift = GetMainContentHorizontalShift(profile, styleMetrics);
        float currentX = panelPos.X + MathF.Floor((panelSize.X - contentWidth) * 0.5f) + contentHorizontalShift;
        float contentTop = panelPos.Y + MathF.Floor((panelSize.Y - contentHeight) * 0.5f);

        if (profile.ShowIcon)
        {
            var badgeMin = new Vector2(
                currentX + GetHorizontalBadgeOffsetX(profile),
                contentTop + MathF.Floor((contentHeight - badgeHeight) * 0.5f) + styleMetrics.BadgeVerticalOffset
            );

            var badgeMax = new Vector2(
                badgeMin.X + badgeWidth,
                badgeMin.Y + badgeHeight
            );

            DrawBadge(profile, styleMetrics, badgeText, badgeScale, badgeMin, badgeMax, windowPos);
            currentX += badgeWidth + styleMetrics.BadgeGap;
        }

        var timePos = new Vector2(
            currentX + MathF.Floor((layout.TotalSize.X - layout.TotalSize.X) * 0.5f),
            panelPos.Y + MathF.Floor((panelSize.Y - layout.TotalSize.Y) * 0.5f) + GetTechTextVerticalOffset(profile, styleMetrics)
        );

        DrawClockHorizontal(
            parts,
            timePos - windowPos,
            mainScale,
            GetMainClockTextColor(profile),
            profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0, 0, 0, 0),
            styleMetrics);
    }

    private void DrawVertical(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        ClockParts parts,
        string badgeText,
        Vector2 badgeTextSize,
        ClockLayoutMetrics layout,
        float mainScale,
        float badgeScale,
        Vector2 panelPos,
        Vector2 panelSize,
        Vector2 windowPos)
    {
        if (styleMetrics.CountdownPanel)
        {
            DrawCountdownVertical(profile, styleMetrics, parts, badgeText, badgeTextSize, mainScale, badgeScale, panelPos, panelSize, windowPos);
            return;
        }

        float lineHeight;
        using (plugin.PushClockTimeFont(profile.TimeTextFont))
            lineHeight = CalculateScaledTextSize("8", mainScale).Y;

        float badgeRotatedWidth = profile.ShowIcon ? GetBadgeVerticalWidth(badgeTextSize, styleMetrics) : 0f;
        float badgeRotatedHeight = profile.ShowIcon ? GetBadgeVerticalHeight(profile, badgeTextSize, styleMetrics, badgeText, badgeScale) : 0f;
        float fullWidth = badgeRotatedWidth + (badgeRotatedWidth > 0 ? styleMetrics.BadgeGap : 0f) + layout.TotalSize.X;

        var contentHorizontalShift = GetMainContentHorizontalShift(profile, styleMetrics);
        float contentStartX = panelPos.X + MathF.Floor((panelSize.X - fullWidth) * 0.5f) + contentHorizontalShift;
        float centerStartX = contentStartX + badgeRotatedWidth + (badgeRotatedWidth > 0 ? styleMetrics.BadgeGap : 0f);
        float centerLineWidth = layout.TotalSize.X;

        if (parts.IsFullText)
        {
            var fullPos = new Vector2(
                panelPos.X + MathF.Floor((panelSize.X - layout.TotalSize.X) * 0.5f) + contentHorizontalShift,
                panelPos.Y + MathF.Floor((panelSize.Y - layout.TotalSize.Y) * 0.5f));
            DrawClockTextScaled(parts.FullText, fullPos - windowPos, mainScale, GetMainClockTextColor(profile), profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0, 0, 0, 0), styleMetrics);
            return;
        }

        float startY = panelPos.Y + MathF.Floor((panelSize.Y - layout.TotalSize.Y) * 0.5f) + GetTechTextVerticalOffset(profile, styleMetrics);
        var shadow = profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0, 0, 0, 0);

        if (!string.IsNullOrWhiteSpace(parts.Prefix))
        {
            DrawCenteredLineCustom(parts.Prefix, centerStartX, centerLineWidth, startY, mainScale, GetMainClockTextColor(profile), shadow, styleMetrics);
            startY += CalculateClockTextSize(parts.Prefix, mainScale).Y + MathF.Max(2f, 2f * mainScale);
        }

        var leftDigits = GetVerticalLeftLines(parts.Left);
        float leftBlockHeight = leftDigits.Length * lineHeight;
        float colonY = startY + leftBlockHeight;
        float minuteStartY = colonY + lineHeight;

        if (profile.ShowIcon)
        {
            float badgeMinY = colonY - MathF.Floor((badgeRotatedHeight - lineHeight) * 0.5f);
            var badgeMin = new Vector2(contentStartX, badgeMinY + styleMetrics.BadgeVerticalOffset) + GetVerticalPresetBadgeOffset(profile, styleMetrics);
            var badgeMax = new Vector2(badgeMin.X + badgeRotatedWidth, badgeMin.Y + badgeRotatedHeight);
            DrawBadgeVertical(profile, styleMetrics, badgeText, badgeScale, badgeMin, badgeMax, windowPos);
        }

        using (plugin.PushClockTimeFont(profile.TimeTextFont))
        {
            for (int i = 0; i < leftDigits.Length; i++)
                DrawCenteredLine(leftDigits[i], centerStartX, centerLineWidth, startY + (i * lineHeight), mainScale, profile, styleMetrics);

            var colonVisible = ShouldShowColon(plugin.Configuration.ColonAnimation);
            DrawCenteredLine(ColonText, centerStartX, centerLineWidth, colonY, mainScale, profile, styleMetrics, colonVisible);
            DrawCenteredLine(parts.MinuteLeft, centerStartX, centerLineWidth, minuteStartY, mainScale, profile, styleMetrics);
            DrawCenteredLine(parts.MinuteRight, centerStartX, centerLineWidth, minuteStartY + lineHeight, mainScale, profile, styleMetrics);

            var nextY = minuteStartY + (lineHeight * 2);
            if (parts.HasSeconds)
            {
                DrawCenteredLine(ColonText, centerStartX, centerLineWidth, nextY, mainScale, profile, styleMetrics, colonVisible);
                DrawCenteredLine(parts.SecondLeft, centerStartX, centerLineWidth, nextY + lineHeight, mainScale, profile, styleMetrics);
                DrawCenteredLine(parts.SecondRight, centerStartX, centerLineWidth, nextY + (lineHeight * 2), mainScale, profile, styleMetrics);
                nextY += lineHeight * 3;
            }

            if (!string.IsNullOrWhiteSpace(parts.Suffix))
            {
                nextY += MathF.Max(2f, 2f * mainScale);
                using (plugin.PushClockTimeFont(ShouldUseDigitalSuffix(profile) ? profile.TimeTextFont : ClockTimeTextFont.Default))
                    DrawCenteredLineCustom(parts.Suffix, centerStartX, centerLineWidth, nextY, mainScale, GetMainClockTextColor(profile), shadow, styleMetrics);
            }
        }
    }

    private Vector2 CalculateMainBadgeTextSize(ClockProfile profile, string badgeText, float badgeScale)
    {
        using (PushPresetAuxFont(profile))
            return CalculateScaledTextSize(badgeText, badgeScale);
    }



    private static float GetBadgeVerticalWidth(Vector2 badgeTextSize, StyleMetrics styleMetrics)
    {
        return badgeTextSize.Y + (styleMetrics.BadgePaddingX * 2.0f);
    }

    private float GetBadgeVerticalHeight(ClockProfile profile, Vector2 badgeTextSize, StyleMetrics styleMetrics, string badgeText, float badgeScale)
    {
        float totalLetterHeight = 0f;
        using (PushPresetAuxFont(profile))
        {
            foreach (var letter in badgeText)
                totalLetterHeight += ImGui.CalcTextSize(letter.ToString()).Y * badgeScale;
        }

        return totalLetterHeight + (styleMetrics.BadgePaddingY * 2.0f) + 2.0f;
    }

    private string[] GetVerticalLeftLines(string left)
    {
        if (string.IsNullOrWhiteSpace(left))
            return new[] { "0" };

        return left.Select(c => c.ToString()).ToArray();
    }

    private void DrawCenteredLine(
        string text,
        float startX,
        float availableWidth,
        float lineY,
        float scale,
        ClockProfile profile,
        StyleMetrics styleMetrics,
        bool visible = true)
    {
        var activeProfile = plugin.Configuration.GetActiveProfile();
        var isDigitalTime = IsSegmentFont(activeProfile.TimeTextFont) && IsDigitalClockRun(text);
        var fontForText = isDigitalTime || activeProfile.TimeTextFont != ClockTimeTextFont.Default || UsesPresetAuxFont(activeProfile)
            ? GetFontForClockText(activeProfile, text)
            : ClockTimeTextFont.Default;
        var textStyle = IsDigitalClockRun(text) ? GetTimeTextMetrics(activeProfile, styleMetrics) : styleMetrics;
        var plainSegmentColon = isDigitalTime && IsColonOnlyText(text) && !ShouldDrawInactiveColonSegments(activeProfile, textStyle);
        Vector2 size;
        using (plugin.PushClockTimeFont(fontForText))
            size = isDigitalTime && !plainSegmentColon ? GetDigitalTimeRunSize(text, scale) : CalculateClockTextSize(text, scale);
        var pos = new Vector2(
            startX + MathF.Floor((availableWidth - size.X) * 0.5f),
            lineY
        );

        var mainTextColor = GetMainClockTextColor(profile);
        var color = visible
            ? mainTextColor
            : new Vector4(mainTextColor.X, mainTextColor.Y, mainTextColor.Z, 0f);

        var shadow = visible && profile.ShowShadowText
            ? profile.ClockShadowColor
            : new Vector4(0, 0, 0, 0);

        if (activeProfile.TimeTextFont == ClockTimeTextFont.Ka1 && IsColonOnlyText(text))
            DrawCartoonColon(pos - ImGui.GetWindowPos() + GetCartoonMainColonOffset(activeProfile), scale, color, shadow, textStyle);
        else if (visible && isDigitalTime && !plainSegmentColon)
            DrawDigitalTimeRun(text, pos - ImGui.GetWindowPos(), scale, color, shadow, textStyle);
        else
        {
            var finalTimeShadow = isDigitalTime
                ? new Vector4(0f, 0f, 0f, 0f)
                : shadow;
            DrawClockTextScaled(text, pos - ImGui.GetWindowPos(), scale, color, finalTimeShadow, textStyle);
        }
    }


    private void DrawCenteredLineCustom(
        string text,
        float startX,
        float availableWidth,
        float lineY,
        float scale,
        Vector4 color,
        Vector4 shadow,
        StyleMetrics styleMetrics,
        bool visible = true)
    {
        var activeProfile = plugin.Configuration.GetActiveProfile();
        var isDigitalTime = IsSegmentFont(activeProfile.TimeTextFont) && IsDigitalClockRun(text);
        var fontForText = isDigitalTime || activeProfile.TimeTextFont != ClockTimeTextFont.Default || UsesPresetAuxFont(activeProfile)
            ? GetFontForClockText(activeProfile, text)
            : ClockTimeTextFont.Default;
        var textStyle = IsDigitalClockRun(text) ? GetTimeTextMetrics(activeProfile, styleMetrics) : styleMetrics;
        var plainSegmentColon = isDigitalTime && IsColonOnlyText(text) && !ShouldDrawInactiveColonSegments(activeProfile, textStyle);
        Vector2 size;
        using (plugin.PushClockTimeFont(fontForText))
            size = isDigitalTime && !plainSegmentColon ? GetDigitalTimeRunSize(text, scale) : CalculateClockTextSize(text, scale);
        var pos = new Vector2(
            startX + MathF.Floor((availableWidth - size.X) * 0.5f),
            lineY
        );

        var finalColor = visible
            ? color
            : new Vector4(color.X, color.Y, color.Z, 0f);

        var finalShadow = visible
            ? shadow
            : new Vector4(0, 0, 0, 0);

        if (activeProfile.TimeTextFont == ClockTimeTextFont.Ka1 && IsColonOnlyText(text))
            DrawCartoonColon(pos - ImGui.GetWindowPos() + GetCartoonSecondaryColonOffset(activeProfile), scale, finalColor, finalShadow, textStyle);
        else if (visible && isDigitalTime && !plainSegmentColon)
            DrawDigitalTimeRun(text, pos - ImGui.GetWindowPos(), scale, finalColor, finalShadow, textStyle);
        else
        {
            var finalTimeShadow = isDigitalTime
                ? new Vector4(0f, 0f, 0f, 0f)
                : finalShadow;
            DrawClockTextScaled(text, pos - ImGui.GetWindowPos(), scale, finalColor, finalTimeShadow, textStyle);
        }
    }

    private static bool IsSegmentFont(ClockTimeTextFont font)
    {
        return font == ClockTimeTextFont.Digital || font == ClockTimeTextFont.Technology;
    }

    private static bool IsDigitalClockRun(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        foreach (var ch in text)
        {
            if (!char.IsDigit(ch) && ch != ':' && !char.IsWhiteSpace(ch))
                return false;
        }

        return true;
    }



    private void DrawInactiveDigitalRun(string text, Vector2 basePos, float scale, bool drawColon)
    {
        var x = basePos.X;
        var inactiveStyle = GetDigitalInactiveTextMetrics();
        var inactiveColor = GetDigitalInactiveSegmentColor();
        var blankShadow = new Vector4(0f, 0f, 0f, 0f);

        foreach (var ch in text)
        {
            var letter = ch.ToString();
            if (char.IsDigit(ch))
            {
                DrawOutlinedTextScaled("8", new Vector2(x, basePos.Y), scale, inactiveColor, blankShadow, inactiveStyle);
                x += CalculateScaledTextSize("8", scale).X;
                continue;
            }

            if (ch == ':')
            {
                if (drawColon)
                    DrawOutlinedTextScaled(":", new Vector2(x, basePos.Y), scale, inactiveColor, blankShadow, inactiveStyle);
                x += CalculateScaledTextSize(":", scale).X;
                continue;
            }

            x += CalculateScaledTextSize(letter, scale).X;
        }
    }

    private static bool ShouldDrawInactiveColonSegments(ClockProfile profile, StyleMetrics styleMetrics)
    {
        if (profile.LayoutMode != ClockLayoutMode.Vertical)
            return true;

        return !styleMetrics.TechPanel && !styleMetrics.DigitalPanel;
    }

    private Vector2 GetDigitalTimeRunSize(string text, float scale)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Vector2.Zero;

        var width = 0f;
        var height = 0f;
        var first = true;
        var groups = text.Split(':');

        foreach (var group in groups)
        {
            if (!first)
            {
                var colonSize = CalculateScaledTextSize(":", scale);
                width += colonSize.X;
                height = MathF.Max(height, colonSize.Y);
            }

            var slotCount = Math.Max(1, group.Length);
            for (var i = 0; i < slotCount; i++)
            {
                var digitSize = CalculateScaledTextSize("8", scale);
                width += digitSize.X;
                height = MathF.Max(height, digitSize.Y);
            }

            first = false;
        }

        return new Vector2(width, height);
    }

    private void DrawDigitalTimeRun(string text, Vector2 basePos, float scale, Vector4 textColor, Vector4 outlineColor, StyleMetrics styleMetrics)
    {
        var x = basePos.X;
        var groups = text.Split(':');
        var activeProfile = plugin.Configuration.GetActiveProfile();
        var drawInactiveColon = ShouldDrawInactiveColonSegments(activeProfile, styleMetrics);

        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            if (groupIndex > 0)
            {
                if (drawInactiveColon)
                    DrawOutlinedTextScaled(":", new Vector2(x, basePos.Y), scale, GetDigitalInactiveSegmentColor(), new Vector4(0f, 0f, 0f, 0f), GetDigitalInactiveTextMetrics());
                DrawOutlinedTextScaled(":", new Vector2(x, basePos.Y), scale, textColor, outlineColor, styleMetrics);
                x += CalculateScaledTextSize(":", scale).X;
            }

            var group = groups[groupIndex];
            var slotCount = Math.Max(1, group.Length);
            var paddedGroup = group.PadLeft(slotCount, ' ');
            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                DrawOutlinedTextScaled("8", new Vector2(x, basePos.Y), scale, GetDigitalInactiveSegmentColor(), new Vector4(0f, 0f, 0f, 0f), GetDigitalInactiveTextMetrics());

                var ch = paddedGroup[slotIndex];
                if (!char.IsWhiteSpace(ch))
                    DrawOutlinedTextScaled(ch.ToString(), new Vector2(x, basePos.Y), scale, textColor, outlineColor, styleMetrics);

                x += CalculateScaledTextSize("8", scale).X;
            }
        }
    }

    private void DrawDigitalTimeRunOnList(ImDrawListPtr drawList, string text, Vector2 basePos, float scale, Vector4 textColor, Vector4 outlineColor, StyleMetrics styleMetrics)
    {
        var x = basePos.X;
        var groups = text.Split(':');
        var activeProfile = plugin.Configuration.GetActiveProfile();
        var drawInactiveColon = ShouldDrawInactiveColonSegments(activeProfile, styleMetrics);

        for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
        {
            if (groupIndex > 0)
            {
                if (drawInactiveColon)
                    DrawOutlinedTextScaledOnList(drawList, ":", new Vector2(x, basePos.Y), scale, GetDigitalInactiveSegmentColor(), new Vector4(0f, 0f, 0f, 0f), GetDigitalInactiveTextMetrics());
                DrawOutlinedTextScaledOnList(drawList, ":", new Vector2(x, basePos.Y), scale, textColor, outlineColor, styleMetrics);
                x += CalculateScaledTextSize(":", scale).X;
            }

            var group = groups[groupIndex];
            var slotCount = Math.Max(1, group.Length);
            var paddedGroup = group.PadLeft(slotCount, ' ');
            for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                DrawOutlinedTextScaledOnList(drawList, "8", new Vector2(x, basePos.Y), scale, GetDigitalInactiveSegmentColor(), new Vector4(0f, 0f, 0f, 0f), GetDigitalInactiveTextMetrics());

                var ch = paddedGroup[slotIndex];
                if (!char.IsWhiteSpace(ch))
                    DrawOutlinedTextScaledOnList(drawList, ch.ToString(), new Vector2(x, basePos.Y), scale, textColor, outlineColor, styleMetrics);

                x += CalculateScaledTextSize("8", scale).X;
            }
        }
    }

    private void DrawVerticalStackedText(
        string text,
        float startX,
        float availableWidth,
        float startY,
        float scale,
        Vector4 color,
        Vector4 shadow,
        StyleMetrics styleMetrics,
        Vector2 windowPos)
    {
        foreach (var ch in text)
        {
            var letter = ch.ToString();
            var size = CalculateScaledTextSize(letter, scale);
            var pos = new Vector2(
                startX + MathF.Floor((availableWidth - size.X) * 0.5f),
                startY);

            DrawOutlinedTextScaled(letter, pos - windowPos, scale, color, shadow, styleMetrics);
            startY += size.Y;
        }
    }

    private static StyleMetrics GetBadgeTextShadowMetrics(StyleMetrics styleMetrics)
    {
        return new StyleMetrics
        {
            MainPaddingX = styleMetrics.MainPaddingX,
            MainPaddingY = styleMetrics.MainPaddingY,
            BadgePaddingX = styleMetrics.BadgePaddingX,
            BadgePaddingY = styleMetrics.BadgePaddingY,
            BadgeGap = styleMetrics.BadgeGap,
            MainRounding = styleMetrics.MainRounding,
            BadgeRounding = styleMetrics.BadgeRounding,
            BadgeVerticalOffset = styleMetrics.BadgeVerticalOffset,
            BorderThickness = styleMetrics.BorderThickness,
            OutlineOffset = MathF.Max(0.35f, styleMetrics.OutlineOffset * 0.55f),
            BadgeScaleMultiplier = styleMetrics.BadgeScaleMultiplier,
            DigitalPanel = styleMetrics.DigitalPanel,
            DigitalText = styleMetrics.DigitalText,
            TechPanel = styleMetrics.TechPanel,
            CountdownPanel = styleMetrics.CountdownPanel
        };
    }

    private void DrawBadge(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        string badgeText,
        float badgeScale,
        Vector2 badgeMin,
        Vector2 badgeMax,
        Vector2 windowPos)
    {
        var drawList = ImGui.GetWindowDrawList();

        if (styleMetrics.CountdownPanel)
        {
            using (PushPresetAuxFont(profile))
            {
                DrawCountdownPlateText(profile, drawList, badgeText, badgeMin, badgeMax, badgeScale, GetMainBadgeTextColor(profile), profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0, 0, 0, 0), styleMetrics, true);
            }
            return;
        }

        var badgeFillColor = ImGui.ColorConvertFloat4ToU32(profile.IconBackgroundColor);
        drawList.AddRectFilled(badgeMin, badgeMax, badgeFillColor, styleMetrics.BadgeRounding);

        if (profile.ShowIconBorder)
        {
            var badgeBorderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
                profile.IconBorderColor.X,
                profile.IconBorderColor.Y,
                profile.IconBorderColor.Z,
                profile.IconBorderOpacity));
            drawList.AddRect(badgeMin, badgeMax, badgeBorderColor, styleMetrics.BadgeRounding, ImDrawFlags.None, styleMetrics.BorderThickness);
        }

        var badgeTextPos = new Vector2(
            badgeMin.X + styleMetrics.BadgePaddingX,
            badgeMin.Y + styleMetrics.BadgePaddingY
        );

        using (PushPresetAuxFont(profile))
        {
            if (styleMetrics.TechPanel)
                DrawOutlinedTextScaled(badgeText, badgeTextPos - windowPos, badgeScale, GetMainBadgeTextColor(profile), profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0, 0, 0, 0), GetBadgeTextShadowMetrics(styleMetrics));
            else
                DrawTextScaled(badgeText, badgeTextPos - windowPos, badgeScale, GetMainBadgeTextColor(profile));
        }
    }

    private void DrawLocalBadge(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        string badgeText,
        float badgeScale,
        Vector2 badgeMin,
        Vector2 badgeMax,
        Vector2 windowPos)
    {
        var drawList = ImGui.GetWindowDrawList();

        var badgeFillColor = ImGui.ColorConvertFloat4ToU32(profile.LocalTimeIconBackgroundColor);
        drawList.AddRectFilled(badgeMin, badgeMax, badgeFillColor, styleMetrics.BadgeRounding);

        if (profile.LocalTimeShowIconBorder)
        {
            var badgeBorderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
                profile.LocalTimeIconBorderColor.X,
                profile.LocalTimeIconBorderColor.Y,
                profile.LocalTimeIconBorderColor.Z,
                profile.LocalTimeIconBorderOpacity));
            drawList.AddRect(badgeMin, badgeMax, badgeBorderColor, styleMetrics.BadgeRounding, ImDrawFlags.None, styleMetrics.BorderThickness);
        }

        var badgeTextPos = new Vector2(
            badgeMin.X + styleMetrics.BadgePaddingX,
            badgeMin.Y + styleMetrics.BadgePaddingY
        );

        using (PushPresetAuxFont(profile))
            DrawTextScaled(badgeText, badgeTextPos - windowPos, badgeScale, profile.LocalTimeIconTextColor);
    }

    private void DrawLocalBadgeVertical(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        string badgeText,
        float badgeScale,
        Vector2 badgeMin,
        Vector2 badgeMax,
        Vector2 windowPos)
    {
        var drawList = ImGui.GetWindowDrawList();

        var badgeFillColor = ImGui.ColorConvertFloat4ToU32(profile.LocalTimeIconBackgroundColor);
        drawList.AddRectFilled(badgeMin, badgeMax, badgeFillColor, styleMetrics.BadgeRounding);

        if (profile.LocalTimeShowIconBorder)
        {
            var badgeBorderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
                profile.LocalTimeIconBorderColor.X,
                profile.LocalTimeIconBorderColor.Y,
                profile.LocalTimeIconBorderColor.Z,
                profile.LocalTimeIconBorderOpacity));
            drawList.AddRect(badgeMin, badgeMax, badgeBorderColor, styleMetrics.BadgeRounding, ImDrawFlags.None, styleMetrics.BorderThickness);
        }

        var letters = badgeText.Select(c => c.ToString()).ToArray();
        if (letters.Length == 0)
            return;

        using (PushPresetAuxFont(profile))
        {
            float totalHeight = 0f;
            foreach (var letter in letters)
                totalHeight += CalculateScaledTextSize(letter, badgeScale).Y;

            float availableHeight = badgeMax.Y - badgeMin.Y;
            float startY = badgeMin.Y + MathF.Floor((availableHeight - totalHeight) * 0.5f);
            float centerX = badgeMin.X + MathF.Floor((badgeMax.X - badgeMin.X) * 0.5f);

            foreach (var letter in letters)
            {
                var size = CalculateScaledTextSize(letter, badgeScale);
                var pos = new Vector2(
                    centerX - (size.X * 0.5f),
                    startY
                );

                DrawTextScaled(letter, pos - windowPos, badgeScale, profile.LocalTimeIconTextColor);
                startY += size.Y;
            }
        }
    }

    private void DrawBadgeVertical(
        ClockProfile profile,
        StyleMetrics styleMetrics,
        string badgeText,
        float badgeScale,
        Vector2 badgeMin,
        Vector2 badgeMax,
        Vector2 windowPos)
    {
        var drawList = ImGui.GetWindowDrawList();

        if (styleMetrics.CountdownPanel)
        {
            DrawCountdownPlate(profile, drawList, badgeMin, badgeMax, styleMetrics.BadgeRounding, styleMetrics, true);

            var countdownLetters = badgeText.Select(c => c.ToString()).ToArray();
            if (countdownLetters.Length == 0)
                return;

            using (PushPresetAuxFont(profile))
            {
                float totalHeight = 0f;
                foreach (var letter in countdownLetters)
                    totalHeight += CalculateScaledTextSize(letter, badgeScale).Y;

                var availableHeight = badgeMax.Y - badgeMin.Y;
                var startY = badgeMin.Y + ((availableHeight - totalHeight) * 0.5f) - MathF.Max(0.35f, badgeScale * 0.45f);
                var centerX = badgeMin.X + ((badgeMax.X - badgeMin.X) * 0.5f);
                var color = GetMainBadgeTextColor(profile);
                var shadow = profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0, 0, 0, 0);

                foreach (var letter in countdownLetters)
                {
                    var size = CalculateScaledTextSize(letter, badgeScale);
                    var pos = new Vector2(centerX - (size.X * 0.5f), startY);
                    DrawOutlinedTextScaledOnList(drawList, letter, pos, badgeScale, color, shadow, GetBadgeTextShadowMetrics(styleMetrics));
                    startY += size.Y;
                }
            }
            return;
        }

        var badgeFillColor = ImGui.ColorConvertFloat4ToU32(profile.IconBackgroundColor);
        drawList.AddRectFilled(badgeMin, badgeMax, badgeFillColor, styleMetrics.BadgeRounding);

        if (profile.ShowIconBorder)
        {
            var badgeBorderColor = ImGui.ColorConvertFloat4ToU32(new Vector4(
                profile.IconBorderColor.X,
                profile.IconBorderColor.Y,
                profile.IconBorderColor.Z,
                profile.IconBorderOpacity));
            drawList.AddRect(badgeMin, badgeMax, badgeBorderColor, styleMetrics.BadgeRounding, ImDrawFlags.None, styleMetrics.BorderThickness);
        }

        var letters = badgeText.Select(c => c.ToString()).ToArray();
        if (letters.Length == 0)
            return;

        using (PushPresetAuxFont(profile))
        {
            float totalHeight = 0f;
            foreach (var letter in letters)
                totalHeight += CalculateScaledTextSize(letter, badgeScale).Y;

            float availableHeight = badgeMax.Y - badgeMin.Y;
            float startY = badgeMin.Y + MathF.Floor((availableHeight - totalHeight) * 0.5f);
            float centerX = badgeMin.X + MathF.Floor((badgeMax.X - badgeMin.X) * 0.5f);

            foreach (var letter in letters)
            {
                var size = CalculateScaledTextSize(letter, badgeScale);
                var pos = new Vector2(
                    centerX - (size.X * 0.5f),
                    startY
                );

                if (styleMetrics.TechPanel)
                    DrawOutlinedTextScaled(letter, pos - windowPos, badgeScale, GetMainBadgeTextColor(profile), profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0, 0, 0, 0), GetBadgeTextShadowMetrics(styleMetrics));
                else
                    DrawTextScaled(letter, pos - windowPos, badgeScale, GetMainBadgeTextColor(profile));
                startY += size.Y;
            }
        }
    }

    private bool ShouldUseDigitalSuffix(ClockProfile profile)
    {
        return profile.TimeTextFont != ClockTimeTextFont.Default;
    }

    private float GetSuffixHorizontalOffset(ClockProfile profile, float scale)
    {
        if (profile.DisplayStyle == ClockDisplayStyle.Tech)
            return (1.65f * scale) - 2.0f;

        return (ShouldUseDigitalSuffix(profile) ? 1.05f : SuffixHorizontalOffset) * scale;
    }

    private float GetSuffixVerticalOffset(ClockProfile profile)
    {
        return profile.DisplayStyle == ClockDisplayStyle.Tech ? -1.0f : ShouldUseDigitalSuffix(profile) ? 1.0f : 0.0f;
    }

    private static float GetTechTextVerticalOffset(ClockProfile profile, StyleMetrics styleMetrics)
    {
        if (!styleMetrics.TechPanel)
            return 0.0f;

        return profile.TimeTextFont == ClockTimeTextFont.Digital ? 0.0f : 3.0f;
    }

    private float GetSuffixRightPadding(ClockProfile profile, float scale)
    {
        if (profile.DisplayStyle == ClockDisplayStyle.Tech)
            return 11.0f * scale;

        return ShouldUseDigitalSuffix(profile) ? 4.0f * scale : 0.0f;
    }

    private Vector2 CalculateSuffixTextSize(ClockProfile profile, string text, float scale)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Vector2.Zero;

        using (plugin.PushClockTimeFont(ShouldUseDigitalSuffix(profile) ? GetPresetAuxFont(profile) : ClockTimeTextFont.Default))
            return CalculateScaledTextSize(text, scale);
    }

    private void DrawClockSuffixText(ClockProfile profile, string text, Vector2 pos, float scale, Vector4 color, Vector4 shadow, StyleMetrics styleMetrics)
    {
        var drawPos = new Vector2(pos.X, pos.Y + GetSuffixVerticalOffset(profile));
        if (ShouldUseDigitalSuffix(profile))
        {
            using (PushPresetAuxFont(profile))
                DrawOutlinedTextScaled(text, drawPos, scale, color, shadow, styleMetrics);
        }
        else
        {
            using (plugin.PushClockTimeFont(ClockTimeTextFont.Default))
                DrawOutlinedTextScaled(text, drawPos, scale, color, shadow, styleMetrics);
        }
    }

    private void DrawClockHorizontal(
        ClockParts parts,
        Vector2 basePos,
        float scale,
        Vector4 color,
        Vector4 shadow,
        StyleMetrics styleMetrics)
    {
        var profile = plugin.Configuration.GetActiveProfile();
        DrawClockHorizontal(parts, basePos, scale, color, shadow, styleMetrics, ColonText, MinuteDigitGap, GetMainColonSideTighten(profile));
    }

    private void DrawClockHorizontal(
        ClockParts parts,
        Vector2 basePos,
        float scale,
        Vector4 color,
        Vector4 shadow,
        StyleMetrics styleMetrics,
        string colonText,
        float minuteDigitGap,
        float colonSideTighten,
        bool drawInactiveSegments = true)
    {
        var profile = plugin.Configuration.GetActiveProfile();
        var textStyle = GetTimeTextMetrics(profile, styleMetrics);
        var cartoonColonOffset = colonText == ColonText && Math.Abs(minuteDigitGap - MinuteDigitGap) < 0.001f && Math.Abs(colonSideTighten) < 0.001f
            ? GetCartoonMainColonOffset(profile)
            : GetCartoonSecondaryColonOffset(profile);

        if (parts.IsFullText)
        {
            using (plugin.PushClockTimeFont(profile.TimeTextFont))
            {
                var fullTextShadow = shadow;

                if (drawInactiveSegments && IsSegmentFont(profile.TimeTextFont) && IsDigitalClockRun(parts.FullText))
                    DrawInactiveDigitalRun(parts.FullText, basePos, scale, ShouldDrawInactiveColonSegments(profile, styleMetrics));

                DrawClockTextScaled(parts.FullText, basePos, scale, color, fullTextShadow, textStyle, true, cartoonColonOffset);
            }
            return;
        }

        var prefixText = string.IsNullOrWhiteSpace(parts.Prefix) ? string.Empty : parts.Prefix + " ";
        var suffixText = string.IsNullOrWhiteSpace(parts.Suffix) ? string.Empty : " " + parts.Suffix;
        var prefixSize = CalculateClockTextSize(prefixText, scale);
        var suffixSize = CalculateSuffixTextSize(profile, suffixText, scale);

        var x = basePos.X;
        if (!string.IsNullOrWhiteSpace(prefixText))
        {
            DrawClockTextScaled(prefixText, new Vector2(x, basePos.Y), scale, color, shadow, styleMetrics);
            x += prefixSize.X;
        }

        using (plugin.PushClockTimeFont(profile.TimeTextFont))
        {
            var leftSize = CalculateScaledTextSize(parts.Left, scale);
            var colonSize = CalculateClockTextSize(colonText, scale);
            var minuteLeftSize = CalculateScaledTextSize(parts.MinuteLeft, scale);
            var minuteRightSize = CalculateScaledTextSize(parts.MinuteRight, scale);
            var secondLeftSize = CalculateScaledTextSize(parts.SecondLeft, scale);
            var secondRightSize = CalculateScaledTextSize(parts.SecondRight, scale);

            var colonVisible = ShouldShowColon(plugin.Configuration.ColonAnimation);
            var colonColor = colonVisible ? color : new Vector4(color.X, color.Y, color.Z, 0f);
            var colonShadow = colonVisible ? shadow : new Vector4(0, 0, 0, 0);

            var digitalSlots = drawInactiveSegments && IsSegmentFont(profile.TimeTextFont);
            var drawInactiveColon = digitalSlots && ShouldDrawInactiveColonSegments(profile, styleMetrics);
            var inactiveStyle = GetDigitalInactiveTextMetrics();
            var inactiveColor = GetDigitalInactiveSegmentColor();
            var activeShadow = shadow;
            var activeColonShadow = colonShadow;

            var leftSlotText = digitalSlots ? GetDigitalSlotText(parts.Left, 2) : parts.Left;
            var leftSlotSize = digitalSlots ? CalculateScaledTextSize(leftSlotText, scale) : leftSize;
            var leftDrawX = x + MathF.Max(0f, leftSlotSize.X - leftSize.X);
            if (profile.DisplayStyle == ClockDisplayStyle.Tech)
                leftDrawX += TechHourTextOffsetX;

            if (digitalSlots)
                DrawOutlinedTextScaled(leftSlotText, new Vector2(x, basePos.Y), scale, inactiveColor, new Vector4(0f, 0f, 0f, 0f), inactiveStyle);

            DrawOutlinedTextScaled(parts.Left, new Vector2(leftDrawX, basePos.Y), scale, color, activeShadow, textStyle);

            var colonX = x + leftSlotSize.X - colonSideTighten;
            if (profile.DisplayStyle == ClockDisplayStyle.Tech)
                colonX += TechColonTextOffsetX;
            var colonPos = new Vector2(colonX, basePos.Y);
            if (profile.TimeTextFont == ClockTimeTextFont.Ka1)
            {
                DrawCartoonColon(colonPos + cartoonColonOffset, scale, colonColor, activeColonShadow, textStyle);
            }
            else
            {
                using (plugin.PushClockTimeFont(GetFontForClockText(profile, colonText)))
                {
                    if (drawInactiveColon)
                        DrawOutlinedTextScaled(colonText, colonPos, scale, inactiveColor, new Vector4(0f, 0f, 0f, 0f), inactiveStyle);
                    DrawOutlinedTextScaled(colonText, colonPos, scale, colonColor, activeColonShadow, textStyle);
                }
            }

            x += leftSlotSize.X + colonSize.X - (colonSideTighten * 2.0f);

            if (digitalSlots)
                DrawOutlinedTextScaled("8", new Vector2(x, basePos.Y), scale, inactiveColor, new Vector4(0f, 0f, 0f, 0f), inactiveStyle);
            DrawOutlinedTextScaled(parts.MinuteLeft, new Vector2(x, basePos.Y), scale, color, activeShadow, textStyle);

            var minuteLeftSlotSize = digitalSlots ? CalculateScaledTextSize("8", scale) : minuteLeftSize;
            var secondMinuteX = x + minuteLeftSlotSize.X + (minuteDigitGap * scale);
            if (digitalSlots)
                DrawOutlinedTextScaled("8", new Vector2(secondMinuteX, basePos.Y), scale, inactiveColor, new Vector4(0f, 0f, 0f, 0f), inactiveStyle);
            DrawOutlinedTextScaled(parts.MinuteRight, new Vector2(secondMinuteX, basePos.Y), scale, color, activeShadow, textStyle);
            var minuteRightSlotSize = digitalSlots ? CalculateScaledTextSize("8", scale) : minuteRightSize;
            x = secondMinuteX + minuteRightSlotSize.X;

            if (parts.HasSeconds)
            {
                var secondsColonX = x - (colonSideTighten * scale);
                if (profile.DisplayStyle == ClockDisplayStyle.Tech)
                    secondsColonX += TechColonTextOffsetX;
                var secondsColonPos = new Vector2(secondsColonX, basePos.Y);
                if (profile.TimeTextFont == ClockTimeTextFont.Ka1)
                {
                    DrawCartoonColon(secondsColonPos + cartoonColonOffset, scale, colonColor, activeColonShadow, textStyle);
                }
                else
                {
                    using (plugin.PushClockTimeFont(GetFontForClockText(profile, colonText)))
                    {
                        if (drawInactiveColon)
                            DrawOutlinedTextScaled(colonText, secondsColonPos, scale, inactiveColor, new Vector4(0f, 0f, 0f, 0f), inactiveStyle);
                        DrawOutlinedTextScaled(colonText, secondsColonPos, scale, colonColor, activeColonShadow, textStyle);
                    }
                }
                x += colonSize.X - (colonSideTighten * 2.0f);
                if (digitalSlots)
                    DrawOutlinedTextScaled("8", new Vector2(x, basePos.Y), scale, inactiveColor, new Vector4(0f, 0f, 0f, 0f), inactiveStyle);
                DrawOutlinedTextScaled(parts.SecondLeft, new Vector2(x, basePos.Y), scale, color, activeShadow, textStyle);
                var secondLeftSlotSize = digitalSlots ? CalculateScaledTextSize("8", scale) : secondLeftSize;
                var secondDigitX = x + secondLeftSlotSize.X + (minuteDigitGap * scale);
                if (digitalSlots)
                    DrawOutlinedTextScaled("8", new Vector2(secondDigitX, basePos.Y), scale, inactiveColor, new Vector4(0f, 0f, 0f, 0f), inactiveStyle);
                DrawOutlinedTextScaled(parts.SecondRight, new Vector2(secondDigitX, basePos.Y), scale, color, activeShadow, textStyle);
                var secondRightSlotSize = digitalSlots ? CalculateScaledTextSize("8", scale) : secondRightSize;
                x = secondDigitX + secondRightSlotSize.X;
            }
        }

        if (!string.IsNullOrWhiteSpace(suffixText))
        {
            var suffixX = x + GetSuffixHorizontalOffset(profile, scale);
            DrawClockSuffixText(profile, suffixText, new Vector2(suffixX, basePos.Y), scale, color, shadow, styleMetrics);
        }
    }

    private static StyleMetrics GetDigitalInactiveTextMetrics()
    {
        return new StyleMetrics
        {
            OutlineOffset = 0f,
            DigitalText = false
        };
    }

    private static Vector4 GetDigitalInactiveSegmentColor()
    {
        return new Vector4(0.22f, 0.22f, 0.22f, 0.62f);
    }

    private static string GetDigitalSlotText(string text, int minDigits)
    {
        var digits = string.IsNullOrEmpty(text) ? 0 : text.Count(char.IsDigit);
        return new string('8', Math.Max(minDigits, digits));
    }



    private bool ShouldShowColon(ColonAnimationMode mode)
    {
        long ms = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

        return mode switch
        {
            ColonAnimationMode.AlwaysVisible => true,
            ColonAnimationMode.Hidden => false,
            ColonAnimationMode.SlowBlink => (ms % 2200) < 1200,
            ColonAnimationMode.FastBlink => (ms % 900) < 450,
            _ => (ms % 1800) < 1000
        };
    }


    private static Vector2 GetCartoonColonSize(float scale)
    {
        var digit = CalculateScaledTextSize("8", scale);
        return new Vector2(MathF.Max(9.0f, digit.Y * 0.48f), digit.Y);
    }

    private static void AddCartoonColon(ImDrawListPtr drawList, Vector2 pos, float scale, Vector4 color)
    {
        if (color.W <= 0.0f)
            return;

        var size = GetCartoonColonSize(scale);
        var dot = MathF.Max(1.8f, size.Y * 0.07f);
        var centerX = pos.X + size.X * 0.5f;
        var topY = pos.Y + size.Y * 0.35f;
        var bottomY = pos.Y + size.Y * 0.64f;
        drawList.AddCircleFilled(new Vector2(centerX, topY), dot, ImGui.GetColorU32(color), 12);
        drawList.AddCircleFilled(new Vector2(centerX, bottomY), dot, ImGui.GetColorU32(color), 12);
    }

    private void DrawCartoonColon(Vector2 basePos, float scale, Vector4 textColor, Vector4 outlineColor, StyleMetrics styleMetrics)
    {
        var drawList = ImGui.GetWindowDrawList();
        var windowPos = ImGui.GetWindowPos();
        var pos = windowPos + basePos;

        if (outlineColor.W > 0.0f)
        {
            var outline = MathF.Max(0.35f, styleMetrics.OutlineOffset) * scale;
            var offsets = new[]
            {
                new Vector2(-outline, 0f),
                new Vector2( outline, 0f),
                new Vector2( 0f,-outline),
                new Vector2( 0f, outline),
                new Vector2(-outline,-outline),
                new Vector2( outline,-outline),
                new Vector2(-outline, outline),
                new Vector2( outline, outline),
            };

            foreach (var offset in offsets)
                AddCartoonColon(drawList, pos + offset, scale, outlineColor);
        }

        AddCartoonColon(drawList, pos, scale, textColor);
    }

    private static void DrawCartoonColonOnList(ImDrawListPtr drawList, Vector2 basePos, float scale, Vector4 textColor, Vector4 outlineColor, StyleMetrics styleMetrics)
    {
        if (outlineColor.W > 0.0f)
        {
            var outline = MathF.Max(0.35f, styleMetrics.OutlineOffset) * scale;
            var offsets = new[]
            {
                new Vector2(-outline, 0f),
                new Vector2( outline, 0f),
                new Vector2( 0f,-outline),
                new Vector2( 0f, outline),
                new Vector2(-outline,-outline),
                new Vector2( outline,-outline),
                new Vector2(-outline, outline),
                new Vector2( outline, outline),
            };

            foreach (var offset in offsets)
                AddCartoonColon(drawList, basePos + offset, scale, outlineColor);
        }

        AddCartoonColon(drawList, basePos, scale, textColor);
    }

    private void DrawTextScaled(string text, Vector2 basePos, float scale, Vector4 textColor)
    {
        ImGui.SetWindowFontScale(scale);
        ImGui.SetCursorPos(basePos);
        ImGui.TextColored(textColor, text);
        ImGui.SetWindowFontScale(1.0f);
    }

    private void DrawOutlinedTextScaled(string text, Vector2 basePos, float scale, Vector4 textColor, Vector4 outlineColor, StyleMetrics styleMetrics)
    {
        ImGui.SetWindowFontScale(scale);

        if (outlineColor.W > 0.0f)
        {
            float outline = styleMetrics.OutlineOffset;

            var offsets = new[]
            {
                new Vector2(-outline, 0f),
                new Vector2( outline, 0f),
                new Vector2( 0f,-outline),
                new Vector2( 0f, outline),
                new Vector2(-outline,-outline),
                new Vector2( outline,-outline),
                new Vector2(-outline, outline),
                new Vector2( outline, outline),
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


    private static void DrawOutlinedTextScaledOnList(ImDrawListPtr drawList, string text, Vector2 basePos, float scale, Vector4 textColor, Vector4 outlineColor, StyleMetrics styleMetrics)
    {
        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * scale;

        if (outlineColor.W > 0.0f)
        {
            var outline = styleMetrics.OutlineOffset * scale;
            var outlineColorU32 = ImGui.GetColorU32(outlineColor);
            var offsets = new[]
            {
                new Vector2(-outline, 0f),
                new Vector2( outline, 0f),
                new Vector2( 0f,-outline),
                new Vector2( 0f, outline),
                new Vector2(-outline,-outline),
                new Vector2( outline,-outline),
                new Vector2(-outline, outline),
                new Vector2( outline, outline),
            };

            foreach (var offset in offsets)
                drawList.AddText(font, fontSize, basePos + offset, outlineColorU32, text);
        }

        drawList.AddText(font, fontSize, basePos, ImGui.GetColorU32(textColor), text);
    }

    private static Vector2 CalculateScaledTextSize(string text, float scale)
    {
        return ImGui.CalcTextSize(text) * scale;
    }

    private Vector2 CalculateClockTextSize(string text, float scale)
    {
        var profile = plugin.Configuration.GetActiveProfile();
        if (profile.TimeTextFont == ClockTimeTextFont.Ka1 && IsColonOnlyText(text))
            return GetCartoonColonSize(scale);

        if (ShouldDrawMixedClockText(profile, text))
            return CalculateMixedClockTextSize(profile, text, scale);

        using (plugin.PushClockTimeFont(GetFontForClockText(profile, text)))
            return CalculateScaledTextSize(text, scale);
    }

    private static bool UsesCartoonTimeFont(ClockProfile profile)
    {
        return profile.TimeTextFont == ClockTimeTextFont.Ka1 || profile.DisplayStyle == ClockDisplayStyle.Cartoon;
    }

    private static Vector2 GetCartoonMainColonOffset(ClockProfile profile)
    {
        return profile.TimeTextFont == ClockTimeTextFont.Ka1 ? new Vector2(0f, 2f) : Vector2.Zero;
    }

    private static Vector2 GetCartoonSecondaryColonOffset(ClockProfile profile)
    {
        return profile.TimeTextFont == ClockTimeTextFont.Ka1 ? new Vector2(-1f, 1f) : Vector2.Zero;
    }

    private static float GetClockFontTimeScale(ClockProfile profile, float scale)
    {
        return UsesCartoonTimeFont(profile) ? MathF.Max(0.35f, scale * 0.70f) : scale;
    }

    private static bool IsColonOnlyText(string text)
    {
        return !string.IsNullOrWhiteSpace(text) && text.Trim().All(ch => ch == ':');
    }

    private static bool ShouldDrawMixedClockText(ClockProfile profile, string text)
    {
        return (profile.TimeTextFont == ClockTimeTextFont.Ka1 || UsesPresetAuxFont(profile)) && !string.IsNullOrEmpty(text) && text.Contains(':');
    }

    private static ClockTimeTextFont GetFontForClockText(ClockProfile profile, string text)
    {
        if (profile.TimeTextFont == ClockTimeTextFont.Ka1 && IsColonOnlyText(text))
            return ClockTimeTextFont.Default;

        if (profile.DisplayStyle == ClockDisplayStyle.Countdown && IsColonOnlyText(text))
            return ClockTimeTextFont.Digital;

        if (UsesPresetAuxFont(profile) && !IsDigitalClockRun(text))
            return ClockTimeTextFont.Digital;

        return profile.TimeTextFont;
    }

    private static float GetCountdownInlineColonPadding(ClockProfile profile, float scale)
    {
        return profile.TimeTextFont == ClockTimeTextFont.Countdown ? MathF.Max(3f, 3f * scale) : 0f;
    }

    private Vector2 CalculateMixedClockTextSize(ClockProfile profile, string text, float scale)
    {
        var width = 0f;
        var height = 0f;

        foreach (var ch in text)
        {
            var letter = ch.ToString();
            if (letter == ":")
            {
                if (profile.TimeTextFont == ClockTimeTextFont.Ka1)
                {
                    var colonSize = GetCartoonColonSize(scale);
                    width += colonSize.X;
                    height = MathF.Max(height, colonSize.Y);
                }
                else
                {
                    using (plugin.PushClockTimeFont(GetFontForClockText(profile, letter)))
                    {
                        var size = CalculateScaledTextSize(letter, scale);
                        width += size.X + (GetCountdownInlineColonPadding(profile, scale) * 2f);
                        height = MathF.Max(height, size.Y);
                    }
                }
                continue;
            }

            using (plugin.PushClockTimeFont(GetFontForClockText(profile, letter)))
            {
                var size = CalculateScaledTextSize(letter, scale);
                width += size.X;
                height = MathF.Max(height, size.Y);
            }
        }

        return new Vector2(width, height);
    }

    private void DrawClockTextScaled(string text, Vector2 basePos, float scale, Vector4 textColor, Vector4 outlineColor, StyleMetrics styleMetrics, bool animateCartoonColon = false, Vector2? cartoonColonOffset = null)
    {
        var profile = plugin.Configuration.GetActiveProfile();
        var colonOffset = cartoonColonOffset.GetValueOrDefault();
        var colonVisible = !animateCartoonColon || ShouldShowColon(plugin.Configuration.ColonAnimation);
        var colonColor = colonVisible ? textColor : new Vector4(textColor.X, textColor.Y, textColor.Z, 0f);
        var colonOutline = colonVisible ? outlineColor : new Vector4(0f, 0f, 0f, 0f);
        if (profile.TimeTextFont == ClockTimeTextFont.Ka1 && IsColonOnlyText(text))
        {
            DrawCartoonColon(basePos + colonOffset, scale, colonColor, colonOutline, styleMetrics);
            return;
        }

        if (!ShouldDrawMixedClockText(profile, text))
        {
            using (plugin.PushClockTimeFont(GetFontForClockText(profile, text)))
                DrawOutlinedTextScaled(text, basePos, scale, textColor, outlineColor, styleMetrics);
            return;
        }

        var x = basePos.X;
        foreach (var ch in text)
        {
            var letter = ch.ToString();
            if (letter == ":")
            {
                if (profile.TimeTextFont == ClockTimeTextFont.Ka1)
                {
                    DrawCartoonColon(new Vector2(x, basePos.Y) + colonOffset, scale, colonColor, colonOutline, styleMetrics);
                    x += GetCartoonColonSize(scale).X;
                }
                else
                {
                    var pad = GetCountdownInlineColonPadding(profile, scale);
                    using (plugin.PushClockTimeFont(GetFontForClockText(profile, letter)))
                    {
                        DrawOutlinedTextScaled(letter, new Vector2(x + pad, basePos.Y), scale, colonColor, colonOutline, styleMetrics);
                        x += CalculateScaledTextSize(letter, scale).X + (pad * 2f);
                    }
                }
                continue;
            }

            using (plugin.PushClockTimeFont(GetFontForClockText(profile, letter)))
            {
                DrawOutlinedTextScaled(letter, new Vector2(x, basePos.Y), scale, textColor, outlineColor, styleMetrics);
                x += CalculateScaledTextSize(letter, scale).X;
            }
        }
    }

    private void DrawClockTextScaledOnList(ImDrawListPtr drawList, string text, Vector2 basePos, float scale, Vector4 textColor, Vector4 outlineColor, StyleMetrics styleMetrics, bool animateCartoonColon = false, Vector2? cartoonColonOffset = null)
    {
        var profile = plugin.Configuration.GetActiveProfile();
        var colonOffset = cartoonColonOffset.GetValueOrDefault();
        var colonVisible = !animateCartoonColon || ShouldShowColon(plugin.Configuration.ColonAnimation);
        var colonColor = colonVisible ? textColor : new Vector4(textColor.X, textColor.Y, textColor.Z, 0f);
        var colonOutline = colonVisible ? outlineColor : new Vector4(0f, 0f, 0f, 0f);
        if (profile.TimeTextFont == ClockTimeTextFont.Ka1 && IsColonOnlyText(text))
        {
            DrawCartoonColonOnList(drawList, basePos + colonOffset, scale, colonColor, colonOutline, styleMetrics);
            return;
        }

        if (!ShouldDrawMixedClockText(profile, text))
        {
            using (plugin.PushClockTimeFont(GetFontForClockText(profile, text)))
                DrawOutlinedTextScaledOnList(drawList, text, basePos, scale, textColor, outlineColor, styleMetrics);
            return;
        }

        var x = basePos.X;
        foreach (var ch in text)
        {
            var letter = ch.ToString();
            if (letter == ":")
            {
                if (profile.TimeTextFont == ClockTimeTextFont.Ka1)
                {
                    DrawCartoonColonOnList(drawList, new Vector2(x, basePos.Y) + colonOffset, scale, colonColor, colonOutline, styleMetrics);
                    x += GetCartoonColonSize(scale).X;
                }
                else
                {
                    var pad = GetCountdownInlineColonPadding(profile, scale);
                    using (plugin.PushClockTimeFont(GetFontForClockText(profile, letter)))
                    {
                        DrawOutlinedTextScaledOnList(drawList, letter, new Vector2(x + pad, basePos.Y), scale, colonColor, colonOutline, styleMetrics);
                        x += CalculateScaledTextSize(letter, scale).X + (pad * 2f);
                    }
                }
                continue;
            }

            using (plugin.PushClockTimeFont(GetFontForClockText(profile, letter)))
            {
                DrawOutlinedTextScaledOnList(drawList, letter, new Vector2(x, basePos.Y), scale, textColor, outlineColor, styleMetrics);
                x += CalculateScaledTextSize(letter, scale).X;
            }
        }
    }

    private ClockParts GetClockParts()
    {
        var zoneId = plugin.Configuration.SelectedTimeZoneId;
        var utc = DateTime.UtcNow;

        if (plugin.TryGetAlarmOverlayVisual(out var alarmUtc, out var alarmZoneId, out _))
        {
            utc = alarmUtc;
            zoneId = alarmZoneId;
        }

        var dateInZone = TimeZoneHelper.ConvertFromUtc(utc, zoneId);
        var parts = BuildClockParts(dateInZone, plugin.Configuration.TimeFormat, plugin.Configuration.GetActiveProfile().LayoutMode);

        return parts;
    }

    private string GetMainBadgeText()
    {
        var zoneId = plugin.Configuration.SelectedTimeZoneId;
        if (plugin.TryGetAlarmOverlayVisual(out _, out var alarmZoneId, out _))
            zoneId = alarmZoneId;

        return TimeZoneHelper.ToShortText(zoneId);
    }

    private Vector4 GetMainClockTextColor(ClockProfile profile)
    {
        if (!plugin.TryGetAlarmOverlayVisual(out _, out _, out var progress))
            return profile.ClockTextColor;

        return GetAlarmGold(profile.ClockTextColor.W, progress, true);
    }

    private Vector4 GetMainBadgeTextColor(ClockProfile profile)
    {
        return plugin.TryGetAlarmOverlayVisual(out _, out _, out var progress)
            ? GetAlarmGold(profile.IconTextColor.W, progress, false)
            : profile.IconTextColor;
    }

    private static Vector4 GetAlarmGold(float alpha, float progress, bool pulse)
    {
        const float baseR = 1.0f;
        const float baseG = 0.72f;
        const float baseB = 0.18f;

        if (!pulse)
            return new Vector4(baseR, baseG, baseB, alpha);

        var t = 0.5f + (MathF.Sin(progress * MathF.PI * 14.0f) * 0.5f);
        return new Vector4(
            1.0f,
            MathF.Min(1.0f, baseG + (0.18f * t)),
            MathF.Min(1.0f, baseB + (0.20f * t)),
            alpha);
    }

    private float GetAlarmTiltRadians()
    {
        if (!plugin.TryGetAlarmOverlayVisual(out _, out _, out var progress))
            return 0f;

        return MathF.Sin(progress * MathF.PI * 30.0f) * 0.028f;
    }

    private unsafe void DrawWithAlarmTilt(Vector2 panelPos, Vector2 panelSize, Action draw)
    {
        var angle = GetAlarmTiltRadians();
        if (MathF.Abs(angle) < 0.001f)
        {
            draw();
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var firstVertex = drawList.VtxBuffer.Size;
        draw();

        var center = panelPos + (panelSize * 0.5f);
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        var data = drawList.VtxBuffer.Data;

        for (var i = firstVertex; i < drawList.VtxBuffer.Size; i++)
        {
            var pos = data[i].Pos;
            var dx = pos.X - center.X;
            var dy = pos.Y - center.Y;
            data[i].Pos = new Vector2(
                center.X + (dx * cos) - (dy * sin),
                center.Y + (dx * sin) + (dy * cos));
        }
    }

    private static float GetMainScale(ClockProfile profile, StyleMetrics styleMetrics)
    {
        var scale = MathF.Max(0.5f, profile.ClockTextScale);
        return styleMetrics.CartoonPanel || UsesCartoonTimeFont(profile)
            ? GetClockFontTimeScale(profile, scale)
            : scale;
    }

    private Vector2 GetMainPanelSize(ClockProfile profile)
    {
        var styleMetrics = GetStyleMetrics(profile.DisplayStyle);
        var mainScale = GetMainScale(profile, styleMetrics);
        var badgeScale = profile.DisplayStyle == ClockDisplayStyle.Countdown
            ? GetCountdownSuffixScale(mainScale)
            : MathF.Max(0.35f, mainScale * styleMetrics.BadgeScaleMultiplier);

        var parts = GetClockParts();
        var badgeText = GetMainBadgeText();
        var badgeSize = profile.ShowIcon
            ? CalculateMainBadgeTextSize(profile, badgeText, badgeScale)
            : Vector2.Zero;

        var layout = GetClockLayoutMetrics(mainScale, parts);

        if (styleMetrics.CountdownPanel)
            return GetCountdownMainPanelSize(profile, parts, badgeSize, mainScale, badgeScale, styleMetrics);

        if (profile.LayoutMode == ClockLayoutMode.Horizontal)
        {

            var iconWidth = profile.ShowIcon ? (styleMetrics.BadgePaddingX * 2.0f) + badgeSize.X : 0.0f;
            var gapWidth = profile.ShowIcon ? styleMetrics.BadgeGap : 0.0f;
            var contentHeight = profile.ShowIcon
                ? MathF.Max(layout.TotalSize.Y, badgeSize.Y + (styleMetrics.BadgePaddingY * 2.0f))
                : layout.TotalSize.Y;

            var techHeightExtra = styleMetrics.TechPanel ? 8.0f : 0.0f;
            var cartoonHeightExtra = styleMetrics.CartoonPanel ? 16.0f : 0.0f;
            var cartoonWidthExtra = styleMetrics.CartoonPanel ? 8.0f : 0.0f;
            var horizontalExtra = MathF.Max(0.0f, GetMainContentHorizontalShift(profile, styleMetrics));

            return new Vector2(
                iconWidth + gapWidth + layout.TotalSize.X + GetHorizontalBadgeOverflow(profile) + MainPanelExtraSize + 1.0f + horizontalExtra + cartoonWidthExtra,
                contentHeight + MainPanelExtraSize + techHeightExtra + cartoonHeightExtra
            );
        }

        var badgeRotatedWidth = profile.ShowIcon ? GetBadgeVerticalWidth(badgeSize, styleMetrics) : 0f;
        float fullWidth = badgeRotatedWidth +
                          (badgeRotatedWidth > 0 ? styleMetrics.BadgeGap : 0f) +
                          layout.TotalSize.X;

        var contentHorizontalExtra = MathF.Max(0.0f, GetMainContentHorizontalShift(profile, styleMetrics));

        var cartoonVerticalHeightExtra = styleMetrics.CartoonPanel ? 30.0f : 0.0f;

        return new Vector2(
            fullWidth + MainPanelExtraSize + 1.0f + contentHorizontalExtra + (styleMetrics.CartoonPanel ? 8.0f : 0.0f),
            layout.TotalSize.Y + MainPanelExtraSize + (styleMetrics.TechPanel ? 8.0f : 0.0f) + cartoonVerticalHeightExtra
        );
    }

    private LocalClockLayoutMetrics GetLocalClockLayout(ClockProfile profile)
    {
        var scale = GetClockFontTimeScale(profile, MathF.Max(0.35f, profile.LocalTimeTextScale));
        var styleMetrics = GetStyleMetrics(profile.LocalTimeDisplayStyle);
        var parts = GetLocalClockParts(profile.LocalTimeFormat, profile.LayoutMode);
        const string badgeText = "LT";

        if (profile.LayoutMode == ClockLayoutMode.Vertical)
        {
            var badgeScale = MathF.Max(0.35f, scale * styleMetrics.BadgeScaleMultiplier);
            var timeLayout = GetClockLayoutMetrics(scale, parts, ClockLayoutMode.Vertical, LocalColonText, LocalMinuteDigitGap, 0.0f);

            if (profile.LocalTimeShowIcon)
            {
                Vector2 badgeTextSize;
                using (PushPresetAuxFont(profile))
                    badgeTextSize = CalculateScaledTextSize(badgeText, badgeScale);
                var labelColumnWidth = GetBadgeVerticalWidth(badgeTextSize, styleMetrics);
                var labelTextHeight = GetBadgeVerticalHeight(profile, badgeTextSize, styleMetrics, badgeText, badgeScale);
                var contentHeight = MathF.Max(timeLayout.TotalSize.Y, labelTextHeight);
                var contentWidth = labelColumnWidth + (labelColumnWidth > 0 ? styleMetrics.BadgeGap : 0f) + timeLayout.TotalSize.X;

                return new LocalClockLayoutMetrics
                {
                    IsVertical = true,
                    UseBadge = true,
                    BadgeText = badgeText,
                    Scale = scale,
                    BadgeScale = badgeScale,
                    LabelColumnWidth = labelColumnWidth,
                    LabelTextHeight = labelTextHeight,
                    Parts = parts,
                    TimeLayout = timeLayout,
                    ContentSize = new Vector2(contentWidth, contentHeight),
                    BasePanelHeight = contentHeight,
                    ExtraTop = 0f,
                    ExtraBottom = 0f,
                    PanelSize = new Vector2(
                        contentWidth + LocalPanelExtraSize + 1.0f,
                        contentHeight + LocalPanelExtraSize)
                };
            }

            const string labelText = "LT";
            float plainTextHeight;
            float plainTextWidth;
            using (PushPresetAuxFont(profile))
            {
                plainTextHeight = GetBadgeVerticalTextHeight(labelText, badgeScale);
                plainTextWidth = MathF.Max(CalculateScaledTextSize("L", badgeScale).X, CalculateScaledTextSize("T", badgeScale).X);
            }
            var contentHeightPlain = MathF.Max(timeLayout.TotalSize.Y, plainTextHeight);
            var contentWidthPlain = plainTextWidth + (plainTextWidth > 0 ? styleMetrics.BadgeGap : 0f) + timeLayout.TotalSize.X;

            return new LocalClockLayoutMetrics
            {
                IsVertical = true,
                UseBadge = false,
                Scale = scale,
                BadgeScale = badgeScale,
                LabelText = labelText,
                LabelColumnWidth = plainTextWidth,
                LabelTextHeight = plainTextHeight,
                Parts = parts,
                TimeLayout = timeLayout,
                ContentSize = new Vector2(contentWidthPlain, contentHeightPlain),
                BasePanelHeight = contentHeightPlain,
                ExtraTop = 0f,
                ExtraBottom = 0f,
                PanelSize = new Vector2(
                    contentWidthPlain + LocalPanelExtraSize + 1.0f,
                    contentHeightPlain + LocalPanelExtraSize)
            };
        }

        var timeLayoutHorizontal = GetClockLayoutMetrics(scale, parts, ClockLayoutMode.Horizontal, LocalColonText, LocalMinuteDigitGap, LocalColonSideTighten);

        if (profile.LocalTimeShowIcon)
        {
            var badgeScale = MathF.Max(0.35f, scale * styleMetrics.BadgeScaleMultiplier);
            Vector2 badgeTextSize;
            using (PushPresetAuxFont(profile))
                badgeTextSize = CalculateScaledTextSize(badgeText, badgeScale);
            var badgeSize = new Vector2(
                badgeTextSize.X + (styleMetrics.BadgePaddingX * 2.0f),
                badgeTextSize.Y + (styleMetrics.BadgePaddingY * 2.0f));
            var contentWidth = badgeSize.X + styleMetrics.BadgeGap + timeLayoutHorizontal.TotalSize.X;
            var contentHeight = MathF.Max(badgeSize.Y, timeLayoutHorizontal.TotalSize.Y);

            return new LocalClockLayoutMetrics
            {
                IsVertical = false,
                UseBadge = true,
                Scale = scale,
                BadgeScale = badgeScale,
                BadgeText = badgeText,
                BadgeSize = badgeSize,
                Parts = parts,
                TimeLayout = timeLayoutHorizontal,
                ContentSize = new Vector2(contentWidth, contentHeight),
                BasePanelHeight = contentHeight,
                ExtraTop = 0f,
                ExtraBottom = 0f,
                PanelSize = new Vector2(
                    contentWidth + LocalPanelExtraSize + 1.0f,
                    contentHeight + LocalPanelExtraSize)
            };
        }

        const string prefix = "LT ";
        var prefixSize = CalculateScaledTextSize(prefix, scale);
        var contentWidthHorizontal = prefixSize.X + timeLayoutHorizontal.TotalSize.X;
        var contentHeightHorizontal = MathF.Max(prefixSize.Y, timeLayoutHorizontal.TotalSize.Y);

        return new LocalClockLayoutMetrics
        {
            IsVertical = false,
            UseBadge = false,
            Scale = scale,
            PrefixText = prefix,
            PrefixSize = prefixSize,
            Parts = parts,
            TimeLayout = timeLayoutHorizontal,
            ContentSize = new Vector2(contentWidthHorizontal, contentHeightHorizontal),
            BasePanelHeight = contentHeightHorizontal,
            ExtraTop = 0f,
            ExtraBottom = 0f,
            PanelSize = new Vector2(
                contentWidthHorizontal + LocalPanelExtraSize + 1.0f,
                contentHeightHorizontal + LocalPanelExtraSize)
        };
    }



    private static float GetPanelRounding(StyleMetrics styleMetrics)
    {
        return MathF.Max(0.0f, styleMetrics.MainRounding - PanelRoundingReduction);
    }

    private static float GetBadgeVerticalTextHeight(string badgeText, float badgeScale)
    {
        float totalLetterHeight = 0f;
        foreach (var letter in badgeText)
            totalLetterHeight += CalculateScaledTextSize(letter.ToString(), badgeScale).Y;

        return totalLetterHeight;
    }

    private static ClockParts GetLocalClockParts(ClockTimeFormat format, ClockLayoutMode layoutMode)
    {
        return BuildClockParts(DateTime.Now, format, layoutMode);
    }

    private static ClockParts BuildClockParts(DateTime dateTime, ClockTimeFormat format, ClockLayoutMode layoutMode = ClockLayoutMode.Horizontal)
    {
        var minutes = dateTime.ToString("mm");
        var suffix = dateTime.ToString("tt", CultureInfo.InvariantCulture).ToLowerInvariant().Replace("am", "a.m").Replace("pm", "p.m");
        var seconds = dateTime.ToString("ss");

        return format switch
        {
            ClockTimeFormat.TwelveHour => new ClockParts
            {
                Left = dateTime.ToString("%h", CultureInfo.InvariantCulture),
                MinuteLeft = minutes[0].ToString(),
                MinuteRight = minutes[1].ToString(),
                Suffix = suffix
            },
            ClockTimeFormat.TwelveHourSeconds => new ClockParts
            {
                Left = dateTime.ToString("%h", CultureInfo.InvariantCulture),
                MinuteLeft = minutes[0].ToString(),
                MinuteRight = minutes[1].ToString(),
                SecondLeft = seconds[0].ToString(),
                SecondRight = seconds[1].ToString(),
                Suffix = suffix
            },
            ClockTimeFormat.TwentyFourHourSeconds => new ClockParts
            {
                Left = dateTime.ToString("HH", CultureInfo.InvariantCulture),
                MinuteLeft = minutes[0].ToString(),
                MinuteRight = minutes[1].ToString(),
                SecondLeft = seconds[0].ToString(),
                SecondRight = seconds[1].ToString()
            },
            ClockTimeFormat.WeekdayTwentyFourHour when layoutMode == ClockLayoutMode.Vertical => new ClockParts
            {
                FullText = dateTime.ToString("ddd HH:mm", CultureInfo.InvariantCulture)
            },
            ClockTimeFormat.WeekdayTwentyFourHour => new ClockParts
            {
                Prefix = dateTime.ToString("ddd", CultureInfo.InvariantCulture),
                Left = dateTime.ToString("HH", CultureInfo.InvariantCulture),
                MinuteLeft = minutes[0].ToString(),
                MinuteRight = minutes[1].ToString()
            },
            ClockTimeFormat.DateTwentyFourHour when layoutMode == ClockLayoutMode.Vertical => new ClockParts
            {
                FullText = dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            },
            ClockTimeFormat.DateTwentyFourHour => new ClockParts
            {
                Prefix = dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Left = dateTime.ToString("HH", CultureInfo.InvariantCulture),
                MinuteLeft = minutes[0].ToString(),
                MinuteRight = minutes[1].ToString()
            },
            _ => new ClockParts
            {
                Left = dateTime.ToString("HH", CultureInfo.InvariantCulture),
                MinuteLeft = minutes[0].ToString(),
                MinuteRight = minutes[1].ToString()
            }
        };
    }

    private ClockLayoutMetrics GetClockLayoutMetrics(float scale, ClockParts parts)
    {
        var profile = plugin.Configuration.GetActiveProfile();
        return GetClockLayoutMetrics(scale, parts, profile.LayoutMode, ColonText, MinuteDigitGap, GetMainColonSideTighten(profile));
    }

    private ClockLayoutMetrics GetClockLayoutMetrics(float scale, ClockParts parts, ClockLayoutMode layoutMode)
    {
        var profile = plugin.Configuration.GetActiveProfile();
        return GetClockLayoutMetrics(scale, parts, layoutMode, ColonText, MinuteDigitGap, GetMainColonSideTighten(profile));
    }

    private ClockLayoutMetrics GetClockLayoutMetrics(float scale, ClockParts parts, ClockLayoutMode layoutMode, string colonText, float minuteDigitGap, float colonSideTighten)
    {
        if (parts.IsFullText)
        {
            return new ClockLayoutMetrics
            {
                TotalSize = CalculateClockTextSize(parts.FullText, scale)
            };
        }

        var activeProfile = plugin.Configuration.GetActiveProfile();
        var prefixText = string.IsNullOrWhiteSpace(parts.Prefix) ? string.Empty : parts.Prefix + " ";
        var suffixText = string.IsNullOrWhiteSpace(parts.Suffix) ? string.Empty : " " + parts.Suffix;
        var prefixSize = CalculateClockTextSize(prefixText, scale);
        var suffixSize = CalculateSuffixTextSize(activeProfile, suffixText, scale);

        if (layoutMode == ClockLayoutMode.Vertical)
        {
            var leftLines = GetVerticalLeftLines(parts.Left);
            float maxWidth = 0f;
            float verticalTotalHeight = 0f;
            float lineHeight = CalculateClockTextSize("8", scale).Y;

            if (!string.IsNullOrWhiteSpace(parts.Prefix))
            {
                var prefixVerticalSize = CalculateClockTextSize(parts.Prefix, scale);
                maxWidth = MathF.Max(maxWidth, prefixVerticalSize.X);
                verticalTotalHeight += prefixVerticalSize.Y + MathF.Max(2f, 2f * scale);
            }

            foreach (var line in leftLines)
            {
                var size = CalculateClockTextSize(line, scale);
                maxWidth = MathF.Max(maxWidth, size.X);
                verticalTotalHeight += lineHeight;
            }

            maxWidth = MathF.Max(maxWidth, CalculateClockTextSize(colonText, scale).X);
            maxWidth = MathF.Max(maxWidth, CalculateClockTextSize(parts.MinuteLeft, scale).X);
            maxWidth = MathF.Max(maxWidth, CalculateClockTextSize(parts.MinuteRight, scale).X);

            verticalTotalHeight += lineHeight;
            verticalTotalHeight += lineHeight;
            verticalTotalHeight += lineHeight;

            if (parts.HasSeconds)
            {
                maxWidth = MathF.Max(maxWidth, CalculateClockTextSize(parts.SecondLeft, scale).X);
                maxWidth = MathF.Max(maxWidth, CalculateClockTextSize(parts.SecondRight, scale).X);
                verticalTotalHeight += lineHeight;
                verticalTotalHeight += lineHeight;
                verticalTotalHeight += lineHeight;
            }

            if (!string.IsNullOrWhiteSpace(parts.Suffix))
            {
                var suffixVerticalSize = CalculateSuffixTextSize(activeProfile, parts.Suffix, scale);
                maxWidth = MathF.Max(maxWidth, suffixVerticalSize.X + GetSuffixRightPadding(activeProfile, scale));
                verticalTotalHeight += MathF.Max(2f, 2f * scale) + suffixVerticalSize.Y;
            }

            return new ClockLayoutMetrics
            {
                TotalSize = new Vector2(maxWidth, verticalTotalHeight)
            };
        }

        var useDigitalSlots = IsSegmentFont(activeProfile.TimeTextFont);
        var leftSize = CalculateClockTextSize(useDigitalSlots ? GetDigitalSlotText(parts.Left, 2) : parts.Left, scale);
        var colonSize = CalculateClockTextSize(colonText, scale);
        var minuteLeftSize = CalculateClockTextSize(useDigitalSlots && !string.IsNullOrWhiteSpace(parts.MinuteLeft) ? "8" : parts.MinuteLeft, scale);
        var minuteRightSize = CalculateClockTextSize(useDigitalSlots && !string.IsNullOrWhiteSpace(parts.MinuteRight) ? "8" : parts.MinuteRight, scale);
        var secondLeftSize = CalculateClockTextSize(useDigitalSlots && !string.IsNullOrWhiteSpace(parts.SecondLeft) ? "8" : parts.SecondLeft, scale);
        var secondRightSize = CalculateClockTextSize(useDigitalSlots && !string.IsNullOrWhiteSpace(parts.SecondRight) ? "8" : parts.SecondRight, scale);

        float totalWidth =
            prefixSize.X +
            leftSize.X +
            colonSize.X +
            minuteLeftSize.X +
            (minuteDigitGap * scale) +
            minuteRightSize.X -
            (colonSideTighten * 2.0f);

        if (parts.HasSeconds)
        {
            totalWidth +=
                colonSize.X +
                secondLeftSize.X +
                (minuteDigitGap * scale) +
                secondRightSize.X -
                (colonSideTighten * 2.0f);
        }

        if (!string.IsNullOrWhiteSpace(suffixText))
            totalWidth += GetSuffixHorizontalOffset(activeProfile, scale) + suffixSize.X + GetSuffixRightPadding(activeProfile, scale);

        var suffixHeight = suffixSize.Y + GetSuffixVerticalOffset(activeProfile);
        float horizontalTotalHeight = MathF.Max(
            prefixSize.Y,
            MathF.Max(leftSize.Y, MathF.Max(colonSize.Y, MathF.Max(minuteLeftSize.Y, MathF.Max(minuteRightSize.Y, MathF.Max(secondLeftSize.Y, MathF.Max(secondRightSize.Y, suffixHeight))))))
        );

        return new ClockLayoutMetrics
        {
            TotalSize = new Vector2(totalWidth, horizontalTotalHeight)
        };
    }

    private static StyleMetrics GetTimeTextMetrics(ClockProfile profile, StyleMetrics styleMetrics)
    {
        if (!IsSegmentFont(profile.TimeTextFont) || styleMetrics.DigitalText)
            return styleMetrics;

        return new StyleMetrics
        {
            MainPaddingX = styleMetrics.MainPaddingX,
            MainPaddingY = styleMetrics.MainPaddingY,
            BadgePaddingX = styleMetrics.BadgePaddingX,
            BadgePaddingY = styleMetrics.BadgePaddingY,
            BadgeGap = styleMetrics.BadgeGap,
            MainRounding = styleMetrics.MainRounding,
            BadgeRounding = styleMetrics.BadgeRounding,
            BadgeVerticalOffset = styleMetrics.BadgeVerticalOffset,
            BorderThickness = styleMetrics.BorderThickness,
            OutlineOffset = styleMetrics.OutlineOffset,
            BadgeScaleMultiplier = styleMetrics.BadgeScaleMultiplier,
            DigitalPanel = styleMetrics.DigitalPanel,
            TechPanel = styleMetrics.TechPanel,
            CartoonPanel = styleMetrics.CartoonPanel,
            DigitalText = true
        };
    }

    private static StyleMetrics GetStyleMetrics(ClockDisplayStyle style)
    {
        return style switch
        {
            ClockDisplayStyle.Minimal => new StyleMetrics
            {
                MainPaddingX = 6f,
                MainPaddingY = 3f,
                BadgePaddingX = 4f,
                BadgePaddingY = 1f,
                BadgeGap = 5f,
                MainRounding = 4f,
                BadgeRounding = 3f,
                BadgeVerticalOffset = 0f,
                BorderThickness = 1f,
                OutlineOffset = 0.6f,
                BadgeScaleMultiplier = 0.42f
            },

            ClockDisplayStyle.StrongShadow => new StyleMetrics
            {
                MainPaddingX = 8f,
                MainPaddingY = 4f,
                BadgePaddingX = 5f,
                BadgePaddingY = 2f,
                BadgeGap = 8f,
                MainRounding = 9f,
                BadgeRounding = 5f,
                BadgeVerticalOffset = 1f,
                BorderThickness = 1.3f,
                OutlineOffset = 1.4f,
                BadgeScaleMultiplier = 0.48f
            },

            ClockDisplayStyle.SoftGlass => new StyleMetrics
            {
                MainPaddingX = 8f,
                MainPaddingY = 4f,
                BadgePaddingX = 5f,
                BadgePaddingY = 2f,
                BadgeGap = 7f,
                MainRounding = 11f,
                BadgeRounding = 7f,
                BadgeVerticalOffset = 0f,
                BorderThickness = 1f,
                OutlineOffset = 0.85f,
                BadgeScaleMultiplier = 0.46f
            },

            ClockDisplayStyle.RetroPanel => new StyleMetrics
            {
                MainPaddingX = 9f,
                MainPaddingY = 5f,
                BadgePaddingX = 5f,
                BadgePaddingY = 2f,
                BadgeGap = 4f,
                MainRounding = 2f,
                BadgeRounding = 2f,
                BadgeVerticalOffset = 1f,
                BorderThickness = 1.5f,
                OutlineOffset = 1.2f,
                BadgeScaleMultiplier = 0.44f
            },

            ClockDisplayStyle.Tech => new StyleMetrics
            {
                MainPaddingX = 25f,
                MainPaddingY = 18f,
                BadgePaddingX = 6f,
                BadgePaddingY = 3f,
                BadgeGap = -2f,
                MainRounding = 10f,
                BadgeRounding = 4f,
                BadgeVerticalOffset = 2f,
                BorderThickness = 1.4f,
                OutlineOffset = 1.05f,
                BadgeScaleMultiplier = 0.72f,
                TechPanel = true,
                DigitalText = true
            },


            ClockDisplayStyle.Cartoon => new StyleMetrics
            {
                MainPaddingX = 24f,
                MainPaddingY = 18f,
                BadgePaddingX = 5f,
                BadgePaddingY = 2f,
                BadgeGap = 5f,
                MainRounding = 11f,
                BadgeRounding = 3.2f,
                BadgeVerticalOffset = 2f,
                BorderThickness = 1.1f,
                OutlineOffset = 0.9f,
                BadgeScaleMultiplier = 0.44f,
                CartoonPanel = true
            },

            ClockDisplayStyle.Countdown => new StyleMetrics
            {
                MainPaddingX = 6f,
                MainPaddingY = 6f,
                BadgePaddingX = 7f,
                BadgePaddingY = 4f,
                BadgeGap = 9f,
                MainRounding = 11f,
                BadgeRounding = 4f,
                BadgeVerticalOffset = -9f,
                BorderThickness = 1.2f,
                OutlineOffset = 1.0f,
                BadgeScaleMultiplier = 0.50f,
                CountdownPanel = true
            },

            ClockDisplayStyle.Digital => new StyleMetrics
            {
                MainPaddingX = 17f,
                MainPaddingY = 10f,
                BadgePaddingX = 5f,
                BadgePaddingY = 2f,
                BadgeGap = 2f,
                MainRounding = 13f,
                BadgeRounding = 4f,
                BadgeVerticalOffset = 1f,
                BorderThickness = 1.65f,
                OutlineOffset = 0f,
                BadgeScaleMultiplier = 0.60f,
                DigitalPanel = true,
                DigitalText = true
            },

            _ => new StyleMetrics
            {
                MainPaddingX = 7f,
                MainPaddingY = 3f,
                BadgePaddingX = 4f,
                BadgePaddingY = 1f,
                BadgeGap = 7f,
                MainRounding = 8f,
                BadgeRounding = 4f,
                BadgeVerticalOffset = 1f,
                BorderThickness = 1f,
                OutlineOffset = 1f,
                BadgeScaleMultiplier = 0.45f
            }
        };
    }

    private sealed class ClockParts
    {
        public string Prefix = "";
        public string Left = "";
        public string MinuteLeft = "";
        public string MinuteRight = "";
        public string SecondLeft = "";
        public string SecondRight = "";
        public string Suffix = "";
        public string FullText = "";
        public bool HasSeconds => !string.IsNullOrWhiteSpace(SecondLeft) && !string.IsNullOrWhiteSpace(SecondRight);
        public bool IsFullText => !string.IsNullOrWhiteSpace(FullText);
    }

    private sealed class LocalClockLayoutMetrics
    {
        public bool IsVertical;
        public bool UseBadge;
        public float Scale;
        public float BadgeScale;
        public string BadgeText = "";
        public Vector2 BadgeSize;
        public string PrefixText = "";
        public Vector2 PrefixSize;
        public string LabelText = "";
        public float LabelColumnWidth;
        public float LabelTextHeight;
        public ClockParts Parts = new();
        public ClockLayoutMetrics TimeLayout = new();
        public Vector2 ContentSize;
        public float BasePanelHeight;
        public float ExtraTop;
        public float ExtraBottom;
        public Vector2 PanelSize;
    }

    private sealed class ClockLayoutMetrics
    {
        public Vector2 TotalSize;
    }

    private sealed class StyleMetrics
    {
        public float MainPaddingX;
        public float MainPaddingY;
        public float BadgePaddingX;
        public float BadgePaddingY;
        public float BadgeGap;
        public float MainRounding;
        public float BadgeRounding;
        public float BadgeVerticalOffset;
        public float BorderThickness;
        public float OutlineOffset;
        public float BadgeScaleMultiplier;
        public bool DigitalPanel;
        public bool TechPanel;
        public bool CartoonPanel;
        public bool CountdownPanel;
        public bool DigitalText;
    }
}
