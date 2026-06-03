// may07 - cleaning UI
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Clock.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private const float MinuteDigitGap = 0.3f;
    private const float SuffixHorizontalOffset = -0.3f;
    private const string ColonText = " : ";
    private const string LocalColonText = ":";
    private const float LocalMinuteDigitGap = 0.15f;
    private const float LocalColonSideTighten = -2.0f;
    private const float SeparateLocalPanelGap = 4.0f;
    private const float InvisibleWindowPadding = 16.0f;
    private const float MainPanelExtraSize = 5.5f;
    private const float LocalPanelExtraSize = 6.5f;
    private const float PanelRoundingReduction = 1.5f;

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

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
    }

    public override void Draw()
    {
        var profile = plugin.Configuration.GetActiveProfile();
        var mainPanelSize = GetMainPanelSize(profile);

        var styleMetrics = GetStyleMetrics(profile.DisplayStyle);
        var mainScale = MathF.Max(0.5f, profile.ClockTextScale);
        var badgeScale = MathF.Max(0.35f, mainScale * styleMetrics.BadgeScaleMultiplier);

        var parts = GetClockParts();
        var badgeText = TimeZoneHelper.ToShortText(plugin.Configuration.SelectedTimeZoneId);
        var badgeTextSize = profile.ShowIcon
            ? CalculateScaledTextSize(badgeText, badgeScale)
            : Vector2.Zero;
        var layout = GetClockLayoutMetrics(mainScale, parts);

        var windowPos = ImGui.GetWindowPos();
        var contentOrigin = windowPos + new Vector2(InvisibleWindowPadding, InvisibleWindowPadding);

        if (!profile.ShowLocalTime)
        {
            DrawOverlayInfoLines(profile, contentOrigin.X, contentOrigin.Y, mainPanelSize.X);
            DrawMainPanel(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, contentOrigin, mainPanelSize, windowPos);
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
                DrawMainPanelBackground(profile, styleMetrics, contentOrigin, combinedSize);
                DrawMainClockContent(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, mainPanelPosVertical, mainPanelSize, windowPos);
                DrawLocalClockPanel(profile, localPanelPosVertical, localLayout.PanelSize, windowPos, true);
            }
            else
            {
                DrawMainPanel(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, mainPanelPosVertical, mainPanelSize, windowPos);
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

            DrawMainPanelBackground(profile, styleMetrics, contentOrigin, combinedSize);

            var localPanelPosInside = new Vector2(
                contentOrigin.X + MathF.Floor((combinedSize.X - adjustedLocalPanelSize.X) * 0.5f) + localLeftOverflow,
                contentOrigin.Y + localTopOverflow);

            DrawLocalClockPanel(profile, localPanelPosInside, localLayout.PanelSize, windowPos, true);

            var mainPanelPos = new Vector2(
                contentOrigin.X + MathF.Floor((combinedSize.X - mainPanelSize.X) * 0.5f),
                contentOrigin.Y + localLayout.PanelSize.Y
            );

            DrawMainClockContent(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, mainPanelPos, mainPanelSize, windowPos);
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
        DrawMainPanel(profile, styleMetrics, parts, badgeText, badgeTextSize, layout, mainScale, badgeScale, mainPanelPosOutside, mainPanelSize, windowPos);
    }


    private void DrawOverlayInfoLines(ClockProfile profile, float x, float panelTopY, float width)
    {
        var lines = BuildOverlayInfoLines();
        if (lines.Count == 0)
            return;

        var lineHeights = lines.Select(line => ImGui.GetTextLineHeight() * line.Scale).ToArray();
        var fullHeight = lineHeights.Sum() + MathF.Max(0, lines.Count - 1) * 1.0f;
        var cursorY = panelTopY - fullHeight - 3.0f;
        var drawList = ImGui.GetForegroundDrawList();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var size = CalculateScaledTextSize(line.Text, line.Scale);
            var lineX = x + MathF.Floor((width - size.X) * 0.5f);
            var lineStyle = GetStyleMetrics(line.DisplayStyle);
            var shadow = line.ShowShadowText ? line.ShadowColor : new Vector4(0, 0, 0, 0);
            DrawOutlinedTextScaledOnList(drawList, line.Text, new Vector2(lineX, cursorY - line.VerticalOffset), line.Scale, line.TextColor, shadow, lineStyle);
            cursorY += lineHeights[i] + 1.0f;
        }
    }

    private List<OverlayLine> BuildOverlayInfoLines()
    {
        var lines = new List<OverlayLine>();

        if (plugin.Configuration.ShowNextAlarmOnOverlay && TryBuildNextAlarmLine(out var alarmLine))
            lines.Add(new OverlayLine(alarmLine, Math.Clamp(plugin.Configuration.NextAlarmOverlayTextScale, 0.45f, 1.8f), plugin.Configuration.NextAlarmOverlayVerticalOffset, plugin.Configuration.NextAlarmOverlayTextColor, plugin.Configuration.NextAlarmOverlayShadowColor, plugin.Configuration.NextAlarmOverlayDisplayStyle, plugin.Configuration.NextAlarmOverlayShowShadowText));

        if (plugin.Configuration.ShowMaintenanceOnOverlay && TryBuildMaintenanceLine(out var maintenanceLine))
            lines.Add(new OverlayLine(maintenanceLine, Math.Clamp(plugin.Configuration.MaintenanceOverlayTextScale, 0.45f, 1.8f), plugin.Configuration.MaintenanceOverlayVerticalOffset, plugin.Configuration.MaintenanceOverlayTextColor, plugin.Configuration.MaintenanceOverlayShadowColor, plugin.Configuration.MaintenanceOverlayDisplayStyle, plugin.Configuration.MaintenanceOverlayShowShadowText));

        return lines;
    }

    private bool TryBuildNextAlarmLine(out string line)
    {
        line = string.Empty;
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

        line = $"Alarm: {FormatDigitalDuration(bestUtc - now)}";
        return true;
    }

    private bool TryBuildMaintenanceLine(out string line)
    {
        line = string.Empty;
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
            : "now";

        line = $"Maintenance: {timeText}";
        return true;
    }

    private static string FormatDigitalDuration(TimeSpan time)
    {
        if (time.TotalSeconds < 0)
            time = TimeSpan.Zero;

        var hours = (int)Math.Floor(time.TotalHours);
        return $"{hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
    }

    private readonly record struct OverlayLine(string Text, float Scale, float VerticalOffset, Vector4 TextColor, Vector4 ShadowColor, ClockDisplayStyle DisplayStyle, bool ShowShadowText);

    public override void PostDraw()
    {
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(3);
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
        float lineHeight = CalculateScaledTextSize("8", localLayout.Scale).Y;
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
            DrawOutlinedTextScaled(localLayout.Parts.FullText, fullPos - windowPos, localLayout.Scale, profile.LocalTimeTextColor, shadowColor, styleMetrics);
            return;
        }

        float timeStartY = contentY + MathF.Floor((localLayout.ContentSize.Y - localLayout.TimeLayout.TotalSize.Y) * 0.5f);
        if (!string.IsNullOrWhiteSpace(localLayout.Parts.Prefix))
        {
            DrawCenteredLineCustom(localLayout.Parts.Prefix, centerStartX, centerLineWidth, timeStartY, localLayout.Scale, profile.LocalTimeTextColor, shadowColor, styleMetrics);
            timeStartY += CalculateScaledTextSize(localLayout.Parts.Prefix, localLayout.Scale).Y + MathF.Max(2f, 2f * localLayout.Scale);
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
            DrawVerticalStackedText(localLayout.LabelText, contentX, localLayout.LabelColumnWidth, labelStartY, localLayout.BadgeScale, profile.LocalTimeTextColor, shadowColor, styleMetrics, windowPos);
        }

        for (int i = 0; i < leftDigits.Length; i++)
        {
            DrawCenteredLineCustom(leftDigits[i], centerStartX, centerLineWidth, timeStartY + (i * lineHeight), localLayout.Scale, profile.LocalTimeTextColor, shadowColor, styleMetrics);
        }

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
        var badgeHeight = profile.ShowIcon ? badgeTextSize.Y + (styleMetrics.BadgePaddingY * 2.0f) : 0.0f;
        var badgeWidth = profile.ShowIcon ? badgeTextSize.X + (styleMetrics.BadgePaddingX * 2.0f) : 0.0f;
        var contentHeight = profile.ShowIcon ? MathF.Max(layout.TotalSize.Y, badgeHeight) : layout.TotalSize.Y;
        var contentWidth = (profile.ShowIcon ? badgeWidth + styleMetrics.BadgeGap : 0.0f) + layout.TotalSize.X;

        float currentX = panelPos.X + MathF.Floor((panelSize.X - contentWidth) * 0.5f);
        float contentTop = panelPos.Y + MathF.Floor((panelSize.Y - contentHeight) * 0.5f);

        if (profile.ShowIcon)
        {
            var badgeMin = new Vector2(
                currentX,
                contentTop + MathF.Floor((contentHeight - badgeHeight) * 0.5f) + styleMetrics.BadgeVerticalOffset
            );

            var badgeMax = new Vector2(
                badgeMin.X + badgeWidth,
                badgeMin.Y + badgeHeight
            );

            DrawBadge(profile, styleMetrics, badgeText, badgeScale, badgeMin, badgeMax, windowPos);
            currentX = badgeMax.X + styleMetrics.BadgeGap;
        }

        var timePos = new Vector2(
            currentX,
            panelPos.Y + MathF.Floor((panelSize.Y - layout.TotalSize.Y) * 0.5f)
        );

        DrawClockHorizontal(
            parts,
            timePos - windowPos,
            mainScale,
            profile.ClockTextColor,
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
        float lineHeight = CalculateScaledTextSize("8", mainScale).Y;
        float badgeRotatedWidth = profile.ShowIcon ? GetBadgeVerticalWidth(badgeTextSize, styleMetrics) : 0f;
        float badgeRotatedHeight = profile.ShowIcon ? GetBadgeVerticalHeight(badgeTextSize, styleMetrics, badgeText, badgeScale) : 0f;
        float fullWidth = badgeRotatedWidth + (badgeRotatedWidth > 0 ? styleMetrics.BadgeGap : 0f) + layout.TotalSize.X;

        float contentStartX = panelPos.X + MathF.Floor((panelSize.X - fullWidth) * 0.5f);
        float centerStartX = contentStartX + badgeRotatedWidth + (badgeRotatedWidth > 0 ? styleMetrics.BadgeGap : 0f);
        float centerLineWidth = layout.TotalSize.X;

        if (parts.IsFullText)
        {
            var fullPos = new Vector2(
                panelPos.X + MathF.Floor((panelSize.X - layout.TotalSize.X) * 0.5f),
                panelPos.Y + MathF.Floor((panelSize.Y - layout.TotalSize.Y) * 0.5f));
            DrawOutlinedTextScaled(parts.FullText, fullPos - windowPos, mainScale, profile.ClockTextColor, profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0, 0, 0, 0), styleMetrics);
            return;
        }

        float startY = panelPos.Y + MathF.Floor((panelSize.Y - layout.TotalSize.Y) * 0.5f);
        var shadow = profile.ShowShadowText ? profile.ClockShadowColor : new Vector4(0, 0, 0, 0);

        if (!string.IsNullOrWhiteSpace(parts.Prefix))
        {
            DrawCenteredLineCustom(parts.Prefix, centerStartX, centerLineWidth, startY, mainScale, profile.ClockTextColor, shadow, styleMetrics);
            startY += CalculateScaledTextSize(parts.Prefix, mainScale).Y + MathF.Max(2f, 2f * mainScale);
        }

        var leftDigits = GetVerticalLeftLines(parts.Left);
        float leftBlockHeight = leftDigits.Length * lineHeight;
        float colonY = startY + leftBlockHeight;
        float minuteStartY = colonY + lineHeight;

        if (profile.ShowIcon)
        {
            float badgeMinY = colonY - MathF.Floor((badgeRotatedHeight - lineHeight) * 0.5f);
            var badgeMin = new Vector2(contentStartX, badgeMinY + styleMetrics.BadgeVerticalOffset);
            var badgeMax = new Vector2(badgeMin.X + badgeRotatedWidth, badgeMin.Y + badgeRotatedHeight);
            DrawBadgeVertical(profile, styleMetrics, badgeText, badgeScale, badgeMin, badgeMax, windowPos);
        }

        for (int i = 0; i < leftDigits.Length; i++)
        {
            DrawCenteredLine(leftDigits[i], centerStartX, centerLineWidth, startY + (i * lineHeight), mainScale, profile, styleMetrics);
        }

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
            DrawCenteredLineCustom(parts.Suffix, centerStartX, centerLineWidth, nextY, mainScale, profile.ClockTextColor, shadow, styleMetrics);
        }
    }

    private static float GetBadgeVerticalWidth(Vector2 badgeTextSize, StyleMetrics styleMetrics)
    {
        return badgeTextSize.Y + (styleMetrics.BadgePaddingX * 2.0f);
    }

    private static float GetBadgeVerticalHeight(Vector2 badgeTextSize, StyleMetrics styleMetrics, string badgeText, float badgeScale)
    {
        float totalLetterHeight = 0f;
        foreach (var letter in badgeText)
            totalLetterHeight += ImGui.CalcTextSize(letter.ToString()).Y * badgeScale;

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
        var size = CalculateScaledTextSize(text, scale);
        var pos = new Vector2(
            startX + MathF.Floor((availableWidth - size.X) * 0.5f),
            lineY
        );

        var color = visible
            ? profile.ClockTextColor
            : new Vector4(profile.ClockTextColor.X, profile.ClockTextColor.Y, profile.ClockTextColor.Z, 0f);

        var shadow = visible && profile.ShowShadowText
            ? profile.ClockShadowColor
            : new Vector4(0, 0, 0, 0);

        DrawOutlinedTextScaled(text, pos - ImGui.GetWindowPos(), scale, color, shadow, styleMetrics);
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
        var size = CalculateScaledTextSize(text, scale);
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

        DrawOutlinedTextScaled(text, pos - ImGui.GetWindowPos(), scale, finalColor, finalShadow, styleMetrics);
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

        DrawTextScaled(badgeText, badgeTextPos - windowPos, badgeScale, profile.IconTextColor);
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

            DrawTextScaled(letter, pos - windowPos, badgeScale, profile.IconTextColor);
            startY += size.Y;
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
        DrawClockHorizontal(parts, basePos, scale, color, shadow, styleMetrics, ColonText, MinuteDigitGap, 0.0f);
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
        float colonSideTighten)
    {
        if (parts.IsFullText)
        {
            DrawOutlinedTextScaled(parts.FullText, basePos, scale, color, shadow, styleMetrics);
            return;
        }

        var prefixText = string.IsNullOrWhiteSpace(parts.Prefix) ? string.Empty : parts.Prefix + " ";
        var suffixText = string.IsNullOrWhiteSpace(parts.Suffix) ? string.Empty : " " + parts.Suffix;
        var prefixSize = CalculateScaledTextSize(prefixText, scale);
        var leftSize = CalculateScaledTextSize(parts.Left, scale);
        var colonSize = CalculateScaledTextSize(colonText, scale);
        var minuteLeftSize = CalculateScaledTextSize(parts.MinuteLeft, scale);
        var minuteRightSize = CalculateScaledTextSize(parts.MinuteRight, scale);
        var secondLeftSize = CalculateScaledTextSize(parts.SecondLeft, scale);
        var secondRightSize = CalculateScaledTextSize(parts.SecondRight, scale);

        var colonVisible = ShouldShowColon(plugin.Configuration.ColonAnimation);
        var colonColor = colonVisible ? color : new Vector4(color.X, color.Y, color.Z, 0f);
        var colonShadow = colonVisible ? shadow : new Vector4(0, 0, 0, 0);

        var x = basePos.X;
        if (!string.IsNullOrWhiteSpace(prefixText))
        {
            DrawOutlinedTextScaled(prefixText, new Vector2(x, basePos.Y), scale, color, shadow, styleMetrics);
            x += prefixSize.X;
        }

        DrawOutlinedTextScaled(parts.Left, new Vector2(x, basePos.Y), scale, color, shadow, styleMetrics);

        var colonPos = new Vector2(x + leftSize.X - colonSideTighten, basePos.Y);
        DrawOutlinedTextScaled(colonText, colonPos, scale, colonColor, colonShadow, styleMetrics);

        x += leftSize.X + colonSize.X - (colonSideTighten * 2.0f);

        DrawOutlinedTextScaled(parts.MinuteLeft, new Vector2(x, basePos.Y), scale, color, shadow, styleMetrics);

        var secondMinuteX = x + minuteLeftSize.X + (minuteDigitGap * scale);
        DrawOutlinedTextScaled(parts.MinuteRight, new Vector2(secondMinuteX, basePos.Y), scale, color, shadow, styleMetrics);
        x = secondMinuteX + minuteRightSize.X;

        if (parts.HasSeconds)
        {
            var secondsColonX = x - (colonSideTighten * scale);
            DrawOutlinedTextScaled(colonText, new Vector2(secondsColonX, basePos.Y), scale, colonColor, colonShadow, styleMetrics);
            x += colonSize.X - (colonSideTighten * 2.0f);
            DrawOutlinedTextScaled(parts.SecondLeft, new Vector2(x, basePos.Y), scale, color, shadow, styleMetrics);
            var secondDigitX = x + secondLeftSize.X + (minuteDigitGap * scale);
            DrawOutlinedTextScaled(parts.SecondRight, new Vector2(secondDigitX, basePos.Y), scale, color, shadow, styleMetrics);
            x = secondDigitX + secondRightSize.X;
        }

        if (!string.IsNullOrWhiteSpace(suffixText))
        {
            var suffixX = x + (SuffixHorizontalOffset * scale);
            DrawOutlinedTextScaled(suffixText, new Vector2(suffixX, basePos.Y), scale, color, shadow, styleMetrics);
        }
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

    private ClockParts GetClockParts()
    {
        var zoneId = plugin.Configuration.SelectedTimeZoneId;
        var dateInZone = TimeZoneHelper.ConvertFromUtc(DateTime.UtcNow, zoneId);
        return BuildClockParts(dateInZone, plugin.Configuration.TimeFormat, plugin.Configuration.GetActiveProfile().LayoutMode);
    }

    private Vector2 GetMainPanelSize(ClockProfile profile)
    {
        var styleMetrics = GetStyleMetrics(profile.DisplayStyle);
        var mainScale = MathF.Max(0.5f, profile.ClockTextScale);
        var badgeScale = MathF.Max(0.35f, mainScale * styleMetrics.BadgeScaleMultiplier);

        var parts = GetClockParts();
        var badgeText = TimeZoneHelper.ToShortText(plugin.Configuration.SelectedTimeZoneId);
        var badgeSize = profile.ShowIcon
            ? CalculateScaledTextSize(badgeText, badgeScale)
            : Vector2.Zero;

        var layout = GetClockLayoutMetrics(mainScale, parts);

        if (profile.LayoutMode == ClockLayoutMode.Horizontal)
        {
            var iconWidth = profile.ShowIcon ? (styleMetrics.BadgePaddingX * 2.0f) + badgeSize.X : 0.0f;
            var gapWidth = profile.ShowIcon ? styleMetrics.BadgeGap : 0.0f;
            var contentHeight = profile.ShowIcon
                ? MathF.Max(layout.TotalSize.Y, badgeSize.Y + (styleMetrics.BadgePaddingY * 2.0f))
                : layout.TotalSize.Y;

            return new Vector2(
                iconWidth + gapWidth + layout.TotalSize.X + MainPanelExtraSize + 1.0f,
                contentHeight + MainPanelExtraSize
            );
        }

        var badgeRotatedWidth = profile.ShowIcon ? GetBadgeVerticalWidth(badgeSize, styleMetrics) : 0f;
        float fullWidth = badgeRotatedWidth +
                          (badgeRotatedWidth > 0 ? styleMetrics.BadgeGap : 0f) +
                          layout.TotalSize.X;

        return new Vector2(
            fullWidth + MainPanelExtraSize + 1.0f,
            layout.TotalSize.Y + MainPanelExtraSize
        );
    }

    private LocalClockLayoutMetrics GetLocalClockLayout(ClockProfile profile)
    {
        var scale = MathF.Max(0.35f, profile.LocalTimeTextScale);
        var styleMetrics = GetStyleMetrics(profile.LocalTimeDisplayStyle);
        var parts = GetLocalClockParts(profile.LocalTimeFormat, profile.LayoutMode);
        const string badgeText = "LT";

        if (profile.LayoutMode == ClockLayoutMode.Vertical)
        {
            var badgeScale = MathF.Max(0.35f, scale * styleMetrics.BadgeScaleMultiplier);
            var timeLayout = GetClockLayoutMetrics(scale, parts, ClockLayoutMode.Vertical, LocalColonText, LocalMinuteDigitGap, 0.0f);

            if (profile.LocalTimeShowIcon)
            {
                var badgeTextSize = CalculateScaledTextSize(badgeText, badgeScale);
                var labelColumnWidth = GetBadgeVerticalWidth(badgeTextSize, styleMetrics);
                var labelTextHeight = GetBadgeVerticalHeight(badgeTextSize, styleMetrics, badgeText, badgeScale);
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
            var plainTextHeight = GetBadgeVerticalTextHeight(labelText, badgeScale);
            var plainTextWidth = MathF.Max(CalculateScaledTextSize("L", badgeScale).X, CalculateScaledTextSize("T", badgeScale).X);
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
            var badgeTextSize = CalculateScaledTextSize(badgeText, badgeScale);
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

    private static float GetOutlinePadding(StyleMetrics styleMetrics)
    {
        return MathF.Ceiling(styleMetrics.OutlineOffset + styleMetrics.BorderThickness + 2.0f);
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
        var suffix = dateTime.ToString("tt", CultureInfo.InvariantCulture).ToLowerInvariant().Replace("am", "a.m.").Replace("pm", "p.m.");
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
        return GetClockLayoutMetrics(scale, parts, plugin.Configuration.GetActiveProfile().LayoutMode, ColonText, MinuteDigitGap, 0.0f);
    }

    private ClockLayoutMetrics GetClockLayoutMetrics(float scale, ClockParts parts, ClockLayoutMode layoutMode)
    {
        return GetClockLayoutMetrics(scale, parts, layoutMode, ColonText, MinuteDigitGap, 0.0f);
    }

    private ClockLayoutMetrics GetClockLayoutMetrics(float scale, ClockParts parts, ClockLayoutMode layoutMode, string colonText, float minuteDigitGap, float colonSideTighten)
    {
        if (parts.IsFullText)
        {
            return new ClockLayoutMetrics
            {
                TotalSize = CalculateScaledTextSize(parts.FullText, scale)
            };
        }

        var prefixText = string.IsNullOrWhiteSpace(parts.Prefix) ? string.Empty : parts.Prefix + " ";
        var suffixText = string.IsNullOrWhiteSpace(parts.Suffix) ? string.Empty : " " + parts.Suffix;
        var prefixSize = CalculateScaledTextSize(prefixText, scale);
        var suffixSize = CalculateScaledTextSize(suffixText, scale);

        if (layoutMode == ClockLayoutMode.Vertical)
        {
            var leftLines = GetVerticalLeftLines(parts.Left);
            float maxWidth = 0f;
            float verticalTotalHeight = 0f;
            float lineHeight = CalculateScaledTextSize("8", scale).Y;

            if (!string.IsNullOrWhiteSpace(parts.Prefix))
            {
                var prefixVerticalSize = CalculateScaledTextSize(parts.Prefix, scale);
                maxWidth = MathF.Max(maxWidth, prefixVerticalSize.X);
                verticalTotalHeight += prefixVerticalSize.Y + MathF.Max(2f, 2f * scale);
            }

            foreach (var line in leftLines)
            {
                var size = CalculateScaledTextSize(line, scale);
                maxWidth = MathF.Max(maxWidth, size.X);
                verticalTotalHeight += lineHeight;
            }

            maxWidth = MathF.Max(maxWidth, CalculateScaledTextSize(colonText, scale).X);
            maxWidth = MathF.Max(maxWidth, CalculateScaledTextSize(parts.MinuteLeft, scale).X);
            maxWidth = MathF.Max(maxWidth, CalculateScaledTextSize(parts.MinuteRight, scale).X);

            verticalTotalHeight += lineHeight;
            verticalTotalHeight += lineHeight;
            verticalTotalHeight += lineHeight;

            if (parts.HasSeconds)
            {
                maxWidth = MathF.Max(maxWidth, CalculateScaledTextSize(parts.SecondLeft, scale).X);
                maxWidth = MathF.Max(maxWidth, CalculateScaledTextSize(parts.SecondRight, scale).X);
                verticalTotalHeight += lineHeight;
                verticalTotalHeight += lineHeight;
                verticalTotalHeight += lineHeight;
            }

            if (!string.IsNullOrWhiteSpace(parts.Suffix))
            {
                var suffixVerticalSize = CalculateScaledTextSize(parts.Suffix, scale);
                maxWidth = MathF.Max(maxWidth, suffixVerticalSize.X);
                verticalTotalHeight += MathF.Max(2f, 2f * scale) + suffixVerticalSize.Y;
            }

            return new ClockLayoutMetrics
            {
                TotalSize = new Vector2(maxWidth, verticalTotalHeight)
            };
        }

        var leftSize = CalculateScaledTextSize(parts.Left, scale);
        var colonSize = CalculateScaledTextSize(colonText, scale);
        var minuteLeftSize = CalculateScaledTextSize(parts.MinuteLeft, scale);
        var minuteRightSize = CalculateScaledTextSize(parts.MinuteRight, scale);
        var secondLeftSize = CalculateScaledTextSize(parts.SecondLeft, scale);
        var secondRightSize = CalculateScaledTextSize(parts.SecondRight, scale);

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
            totalWidth += (SuffixHorizontalOffset * scale) + suffixSize.X;

        float horizontalTotalHeight = MathF.Max(
            prefixSize.Y,
            MathF.Max(leftSize.Y, MathF.Max(colonSize.Y, MathF.Max(minuteLeftSize.Y, MathF.Max(minuteRightSize.Y, MathF.Max(secondLeftSize.Y, MathF.Max(secondRightSize.Y, suffixSize.Y))))))
        );

        return new ClockLayoutMetrics
        {
            TotalSize = new Vector2(totalWidth, horizontalTotalHeight)
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
                BadgeGap = 9f,
                MainRounding = 2f,
                BadgeRounding = 2f,
                BadgeVerticalOffset = 1f,
                BorderThickness = 1.5f,
                OutlineOffset = 1.2f,
                BadgeScaleMultiplier = 0.44f
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
    }
}
